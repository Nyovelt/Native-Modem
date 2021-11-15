using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Native_Modem
{
    public partial class FullDuplexModem
    {
        enum ModemState
        {
            FrameDetection,
            Rx,
            Tx
        }

        readonly Protocol protocol;
        readonly AsioOut asioOut;
        readonly TxThread Tx;
        readonly RxThread Rx;
        readonly Queue<TransportSession> TxSessions;
        readonly byte macAddress;
        readonly float[] RxBuffer;
        readonly Action<byte, byte[]> onDataReceived;
        readonly Dictionary<byte, uint> rxSessions;

        bool disposed;

        ModemState state;

        /// <summary>
        /// The parameters of onDataReceived are source address and payload
        /// </summary>
        /// <param name="onDataReceived"></param>
        public FullDuplexModem(Protocol protocol, byte macAddress, string driverName, Action<byte, byte[]> onDataReceived, string saveTransportTo = null, string saveRecordTo = null)
        {
            this.protocol = protocol;
            this.macAddress = macAddress;
            this.onDataReceived = onDataReceived;
            disposed = false;

            Tx = new TxThread(protocol, protocol.SampleRate, saveTransportTo);
            Rx = new RxThread(protocol, protocol.SampleRate >> 1, saveRecordTo);
            TxSessions = new Queue<TransportSession>();

            rxSessions = new Dictionary<byte, uint>();

            asioOut = new AsioOut(driverName);
            AsioUtilities.SetupAsioOut(asioOut);
            if (protocol.UseStereo)
            {
                asioOut.InitRecordAndPlayback(new MonoToStereoProvider16(Tx.TxFIFO.ToWaveProvider16()), 2, protocol.SampleRate);
            }
            else
            {
                asioOut.InitRecordAndPlayback(Tx.TxFIFO.ToWaveProvider(), 1, protocol.SampleRate);
            }

            RxBuffer = new float[(protocol.UseStereo ? 2 : 1) * asioOut.FramesPerBuffer];
            state = ModemState.FrameDetection;

            asioOut.AudioAvailable += OnRxSamplesAvailable;
            asioOut.Play();

            Task.Run(MainLoop);
        }

        public void Dispose()
        {
            disposed = true;

            asioOut.Stop();
            asioOut.AudioAvailable -= OnRxSamplesAvailable;

            asioOut.Dispose();
            Tx.Dispose();
            Rx.Dispose();
        }

        public void TransportData(byte destination, byte[] data)
        {
            DataTransportSession session = new DataTransportSession(destination, this, OnTxSessionFinished, data);
            TxSessions.Enqueue(session);
            if (TxSessions.Count == 1)
            {
                ActivateTxSession(session);
            }
        }

        public void MacPing(byte destination, double timeout)
        {
            MacPingSession session = new MacPingSession(destination, this, OnTxSessionFinished, timeout);
            TxSessions.Enqueue(session);
            if (TxSessions.Count == 1)
            {
                ActivateTxSession(session);
            }
        }

        void OnRxSamplesAvailable(object sender, AsioAudioAvailableEventArgs e)
        {
            int sampleCount = e.GetAsInterleavedSamples(RxBuffer);
            if (protocol.UseStereo)
            {
                Rx.RxFIFO.PushStereo(RxBuffer, sampleCount);
            }
            else
            {
                Rx.RxFIFO.Push(RxBuffer, sampleCount);
            }
        }

        bool UntilDisposed()
        {
            return disposed;
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

        void OnTxSessionLogInfo(string info)
        {
            Console.WriteLine(info);
        }

        void ActivateTxSession(TransportSession session)
        {
            session.OnLogInfo = OnTxSessionLogInfo;
            session.OnSessionActivated();
        }

        async Task MainLoop()
        {
            TaskStatus tempStatus;
            while (true)
            {
                switch (state)
                {
                    case ModemState.FrameDetection:
                        if (!await Rx.WaitUntilQuiet(UntilDisposed))
                        {
                            return;
                        }

                        tempStatus = await Rx.DetectFrame(UntilDisposed,
                            () => TxSessions.TryPeek(out TransportSession currentTxSession) && currentTxSession.ReadyToSend);
                        switch (tempStatus)
                        {
                            case TaskStatus.Success:
                                state = ModemState.Rx;
                                break;
                            case TaskStatus.Interrupted:
                                state = ModemState.Tx;
                                break;
                            case TaskStatus.Canceled:
                                return;
                            default:
                                return;
                        }
                        break;

                    case ModemState.Rx:
                        (bool notCanceled, byte[] frame) = await Rx.DemodulateFrame(UntilDisposed);
                        if (!notCanceled)
                        {
                            return;
                        }

                        if (frame != null)
                        {
                            if (!Protocol.Frame.IsValid(frame) ||
                                Protocol.Frame.GetDestination(frame) != macAddress)
                            {
                                state = ModemState.FrameDetection;
                            }

                            byte source = Protocol.Frame.GetSource(frame);
                            uint seqNum = Protocol.Frame.GetSequenceNumber(frame);
                            Protocol.FrameType type = Protocol.Frame.GetType(frame);
                            switch (type)
                            {
                                case Protocol.FrameType.Data:
                                case Protocol.FrameType.Data_Start:
                                case Protocol.FrameType.Data_End:
                                    Console.WriteLine($"Data frame of type {type} received!");
                                    const uint NO_ACK = uint.MaxValue;
                                    uint ackSeqNum = seqNum;
                                    if (rxSessions.TryGetValue(source, out uint prevSeqNum))
                                    {
                                        switch (type)
                                        {
                                            case Protocol.FrameType.Data:
                                                if (seqNum == Protocol.Frame.NextSequenceNumberOf(prevSeqNum))
                                                {
                                                    rxSessions[source] = seqNum;
                                                    onDataReceived.Invoke(source, Protocol.Frame.ExtractData(frame));
                                                }
                                                else
                                                {
                                                    ackSeqNum = prevSeqNum;
                                                }
                                                break;
                                            case Protocol.FrameType.Data_Start:
                                                rxSessions[source] = seqNum;
                                                break;
                                            case Protocol.FrameType.Data_End:
                                                if (seqNum == Protocol.Frame.NextSequenceNumberOf(prevSeqNum))
                                                {
                                                    rxSessions.Remove(source);
                                                }
                                                else
                                                {
                                                    ackSeqNum = prevSeqNum;
                                                }
                                                break;
                                        }
                                    }
                                    else
                                    {
                                        if (type == Protocol.FrameType.Data_Start)
                                        {
                                            rxSessions[source] = seqNum;
                                        }
                                        else
                                        {
                                            ackSeqNum = NO_ACK;
                                        }
                                    }

                                    if (ackSeqNum != NO_ACK &&
                                        (!await Rx.WaitUntilQuiet(UntilDisposed) ||
                                        !await Tx.Push(
                                            Protocol.Frame.WrapFrameWithoutData(
                                                source,
                                                macAddress,
                                                Protocol.FrameType.Acknowledgement,
                                                ackSeqNum),
                                            UntilDisposed)))
                                    {
                                        return;
                                    }
                                    break;

                                case Protocol.FrameType.MacPing_Req:
                                    if (!await Tx.Push(
                                        Protocol.Frame.WrapFrameWithoutData(
                                            source,
                                            macAddress,
                                            Protocol.FrameType.MacPing_Reply,
                                            0),
                                        UntilDisposed))
                                    {
                                        return;
                                    }
                                    break;

                                case Protocol.FrameType.Acknowledgement:
                                case Protocol.FrameType.MacPing_Reply:
                                    if (TxSessions.TryPeek(out TransportSession session))
                                    {
                                        session.OnReceiveFrame(source, type, seqNum);
                                    }
                                    break;
                            }
                        }
                        state = ModemState.FrameDetection;
                        break;

                    case ModemState.Tx:
                        if (!await Rx.WaitUntilQuiet(UntilDisposed))
                        {
                            return;
                        }

                        if (!await Tx.Push(TxSessions.Peek().OnGetFrame(), UntilDisposed))
                        {
                            return;
                        }
                        TxSessions.Peek().OnFrameSent();
                        state = ModemState.FrameDetection;
                        break;
                }
            }
        }
    }
}
