using NAudio.Wave;
using System;
using System.Collections.Generic;

namespace Native_Modem
{
    public partial class FullDuplexModem
    {
        readonly Protocol protocol;
        readonly AsioOut asioOut;
        readonly TxThread Tx;
        readonly RxThread Rx;
        readonly Queue<TransportSession> TxSessions;
        readonly byte macAddress;
        readonly Action<byte, byte[]> onFileReceived;
        readonly Action<string> onLogInfo;
        readonly Dictionary<byte, DataReceiveSession> rxSessions;
        readonly WaveFileWriter recordWriter;

        readonly float[] rxBuffer = new float[2048];
        
        /// <summary>
        /// The parameters of onDataReceived are source address and payload
        /// </summary>
        /// <param name="onDataReceived"></param>
        public FullDuplexModem(Protocol protocol, byte macAddress, Action<byte, byte[]> onFileReceived, Action<string> onLogInfo, string saveTransportTo = null, string saveRecordTo = null)
        {
            this.protocol = protocol;
            this.macAddress = macAddress;
            this.onFileReceived = onFileReceived;
            this.onLogInfo = onLogInfo;

            asioOut = new AsioOut(AsioUtilities.SelectAsioDriver());
            AsioUtilities.SetupAsioOut(asioOut);

            Rx = new RxThread(protocol);
            Tx = new TxThread(protocol, Rx, protocol.SampleRate, saveTransportTo);
            TxSessions = new Queue<TransportSession>();

            rxSessions = new Dictionary<byte, DataReceiveSession>();

            if (!string.IsNullOrEmpty(saveRecordTo))
            {
                recordWriter = new WaveFileWriter(saveRecordTo, WaveFormat.CreateIeeeFloatWaveFormat(protocol.SampleRate, 1));
            }
            else
            {
                recordWriter = null;
            }

            asioOut.InitRecordAndPlayback(Tx.TxFIFO.ToWaveProvider(), 1, protocol.SampleRate);
            asioOut.AudioAvailable += OnRxSamplesAvailable;
            asioOut.Play();

            Rx.OnFrameReceived += OnFrameReceived;
        }

        public void Dispose()
        {
            if (TxSessions.TryPeek(out TransportSession session))
            {
                session.OnInterrupted();
            }

            Rx.OnFrameReceived -= OnFrameReceived;

            asioOut.Stop();
            asioOut.AudioAvailable -= OnRxSamplesAvailable;

            recordWriter?.Dispose();

            asioOut.Dispose();
            Tx.Dispose();
        }

        public void TransportData(byte destination, byte[] data)
        {
            DataTransportSession session = new DataTransportSession(destination, this, Tx.TransportFrame, OnTxSessionFinished, data);
            TxSessions.Enqueue(session);
            if (TxSessions.Count == 1)
            {
                ActivateTxSession(session);
            }
        }

        public void MacPerf(byte destination)
        {
            double  timeInterval = 1000; //millisecond
            byte[] data = new byte[10000];
            var rand = new Random();
            rand.NextBytes(data);
            DataTransportSession session = new DataTransportSession(destination, this, Tx.TransportFrame, OnTxSessionFinished, data, timeInterval);
            TxSessions.Enqueue(session);
            if (TxSessions.Count == 1)
                ActivateTxSession(session);
        }
        public void MacPing(byte destination, double timeout)
        {
            MacPingSession session = new MacPingSession(destination, this, Tx.TransportFrame, OnTxSessionFinished, timeout);
            TxSessions.Enqueue(session);
            if (TxSessions.Count == 1)
            {
                ActivateTxSession(session);
            }
        }

        void OnRxSamplesAvailable(object sender, AsioAudioAvailableEventArgs e)
        {
            int sampleCount = e.GetAsInterleavedSamples(rxBuffer);
            Rx.ProcessSamples(rxBuffer, sampleCount);
            recordWriter?.WriteSamples(rxBuffer, 0, sampleCount);
        }

        void OnTxSessionFinished(TransportSession session)
        {
            if (TxSessions.TryPeek(out TransportSession currentSession) && currentSession == session)
            {
                currentSession.OnLogInfo = null;
                TxSessions.Dequeue();
                if (TxSessions.TryPeek(out TransportSession nextSession))
                {
                    ActivateTxSession(nextSession);
                }
            }
        }

        void ActivateTxSession(TransportSession session)
        {
            session.OnLogInfo = onLogInfo;
            session.OnSessionActivated();
        }

        void OnFileReceived(byte source, byte[] data)
        {
            onFileReceived.Invoke(source, data);
            onLogInfo.Invoke($"Received file of lenth {data.Length} from {source}");
        }

        void OnFrameReceived(byte[] frame)
        {
            if (!Protocol.Frame.IsValid(frame))
            {
                onLogInfo.Invoke("Invalid frame received!");
                return;
            }

            if (Protocol.Frame.GetDestination(frame) != macAddress)
            {
                return;
            }

            byte source = Protocol.Frame.GetSource(frame);
            uint seqNum = Protocol.Frame.GetSequenceNumber(frame);
            Protocol.FrameType type = Protocol.Frame.GetType(frame);
            switch (type)
            {
                case Protocol.FrameType.Data:
                case Protocol.FrameType.Data_Start:
                case Protocol.FrameType.Data_End:
                    onLogInfo.Invoke($"Data frame of type {type} received!");
                    if (rxSessions.TryGetValue(source, out DataReceiveSession rxSession))
                    {
                        if (rxSession.IsCompleted && type == Protocol.FrameType.Data_Start)
                        {
                            rxSessions[source] = DataReceiveSession.InitializeSession(
                                source, this, Tx.TransportFrame, OnFileReceived, seqNum);
                        }
                        else
                        {
                            rxSession.ReceiveFrame(frame);
                        }
                    }
                    else
                    {
                        if (type == Protocol.FrameType.Data_Start)
                        {
                            rxSessions[source] = DataReceiveSession.InitializeSession(
                                source, this, Tx.TransportFrame, OnFileReceived, seqNum);
                        }
                    }
                    break;

                case Protocol.FrameType.MacPing_Req:
                    Tx.TransportFrame(
                        Protocol.Frame.WrapFrameWithoutData(
                            source,
                            macAddress,
                            Protocol.FrameType.MacPing_Reply,
                            seqNum),
                        null);
                    break;

                case Protocol.FrameType.Acknowledgement:
                case Protocol.FrameType.MacPing_Reply:
                    if (TxSessions.TryPeek(out TransportSession txSession))
                    {
                        txSession.OnReceiveFrame(source, type, seqNum);
                    }
                    break;
            }
        }
    }
}
