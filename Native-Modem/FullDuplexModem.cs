﻿using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

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
        readonly float[] RxBuffer;
        readonly Action<byte, byte[]> onDataReceived;
        readonly Dictionary<byte, uint> rxSessions;
        readonly WaveFileWriter recordWriter;

        /// <summary>
        /// The parameters of onDataReceived are source address and payload
        /// </summary>
        /// <param name="onDataReceived"></param>
        public FullDuplexModem(Protocol protocol, byte macAddress, string driverName, Action<byte, byte[]> onDataReceived, string saveTransportTo = null, string saveRecordTo = null)
        {
            this.protocol = protocol;
            this.macAddress = macAddress;
            this.onDataReceived = onDataReceived;

            Rx = new RxThread(protocol);
            Tx = new TxThread(protocol, Rx, protocol.SampleRate, saveTransportTo);
            TxSessions = new Queue<TransportSession>();

            rxSessions = new Dictionary<byte, uint>();

            asioOut = new AsioOut(driverName);
            AsioUtilities.SetupAsioOut(asioOut);
            asioOut.InitRecordAndPlayback(Tx.TxFIFO.ToWaveProvider(), 1, protocol.SampleRate);

            RxBuffer = new float[asioOut.FramesPerBuffer];

            if (!string.IsNullOrEmpty(saveRecordTo))
            {
                recordWriter = new WaveFileWriter(saveRecordTo, WaveFormat.CreateIeeeFloatWaveFormat(protocol.SampleRate, 1));
            }
            else
            {
                recordWriter = null;
            }

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
            int sampleCount = e.GetAsInterleavedSamples(RxBuffer);
            Rx.ProccessSamples(RxBuffer, sampleCount);
            recordWriter?.WriteSamples(RxBuffer, 0, sampleCount);
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

        void OnFrameReceived(byte[] frame)
        {
            if (!Protocol.Frame.IsValid(frame) ||
                Protocol.Frame.GetDestination(frame) != macAddress)
            {
                Console.WriteLine("Invalid frame received!");
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

                    if (ackSeqNum != NO_ACK)
                    {
                        Tx.TransportFrame(
                            Protocol.Frame.WrapFrameWithoutData(
                                source,
                                macAddress,
                                Protocol.FrameType.Acknowledgement,
                                ackSeqNum), 
                            null);
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
                    if (TxSessions.TryPeek(out TransportSession session))
                    {
                        session.OnReceiveFrame(source, type, seqNum);
                    }
                    break;
            }
        }
    }
}
