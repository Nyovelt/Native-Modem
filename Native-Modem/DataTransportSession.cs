using System;
using System.Collections.Generic;
using System.Timers;

namespace Native_Modem
{
    public partial class FullDuplexModem
    {
        class DataTransportSession : TransportSession
        {
            readonly Timer timer;
            readonly Queue<byte[]> framesPending;
            uint lastAck;
            int tries;

            public DataTransportSession(byte destination, FullDuplexModem modem, Action<byte[], Action<bool>> sendFrame, Action<TransportSession> onFinished, byte[] data) : base(destination, modem, sendFrame, onFinished)
            {
                timer = new Timer(modem.protocol.AckTimeout);
                timer.AutoReset = false;
                timer.Elapsed += (sender, e) =>
                {
                    FailTransmit();
                };
                framesPending = new Queue<byte[]>();
                lastAck = Protocol.Frame.RandomSequenceNumber();
                tries = 0;

                int fullFrames = data.Length / modem.protocol.FrameMaxDataBytes;
                int byteCounter = 0;
                uint seqCounter = Protocol.Frame.NextSequenceNumberOf(lastAck);

                framesPending.Enqueue(Protocol.Frame.WrapFrameWithoutData(
                    destination,
                    modem.macAddress,
                    Protocol.FrameType.Data_Start,
                    seqCounter));
                seqCounter = Protocol.Frame.NextSequenceNumberOf(seqCounter);

                for (int i = 0; i < fullFrames; i++)
                {
                    framesPending.Enqueue(Protocol.Frame.WrapDataFrame(
                        destination,
                        modem.macAddress,
                        seqCounter,
                        data,
                        byteCounter,
                        modem.protocol.FrameMaxDataBytes));
                    byteCounter += modem.protocol.FrameMaxDataBytes;
                    seqCounter = Protocol.Frame.NextSequenceNumberOf(seqCounter);
                }

                int remainBytes = data.Length - byteCounter;
                if (remainBytes != 0)
                {
                    framesPending.Enqueue(Protocol.Frame.WrapDataFrame(
                        destination,
                        modem.macAddress,
                        seqCounter,
                        data,
                        byteCounter,
                        (byte)remainBytes));
                    seqCounter = Protocol.Frame.NextSequenceNumberOf(seqCounter);
                }

                framesPending.Enqueue(Protocol.Frame.WrapFrameWithoutData(
                    destination,
                    modem.macAddress,
                    Protocol.FrameType.Data_End,
                    seqCounter));
            }

            public override void OnSessionActivated()
            {
                OnLogInfo?.Invoke($"Start sending {framesPending.Count} frames to {destination}...");
                SendFrame();
            }

            public override void OnReceiveFrame(byte src_addr, Protocol.FrameType type, uint seqNum)
            {
                if (src_addr != destination || type != Protocol.FrameType.Acknowledgement)
                {
                    return;
                }

                if (framesPending.Count == 0)
                {
                    onFinished?.Invoke(this);
                    return;
                }

                Console.WriteLine($"Ack {seqNum} received");
                if (seqNum == Protocol.Frame.NextSequenceNumberOf(lastAck))
                {
                    lastAck = seqNum;
                    framesPending.Dequeue();
                    timer.Stop();
                    OnLogInfo?.Invoke($"Frame sent successfully. {framesPending.Count} left.");
                    if (framesPending.Count == 0)
                    {
                        OnLogInfo?.Invoke($"All frames sent successfully.");
                        onFinished?.Invoke(this);
                    }
                    else
                    {
                        tries = 0;
                        SendFrame();
                    }
                }
            }

            public override void OnInterrupted()
            {
                timer.Stop();
            }

            void SendFrame()
            {
                Console.WriteLine("Sending!");
                tries++;
                sendFrame.Invoke(framesPending.Peek(), success =>
                {
                    if (success)
                    {
                        timer.Start();
                    }
                    else
                    {
                        FailTransmit();
                    }
                });
            }

            void FailTransmit()
            {
                if (tries > modem.protocol.MaxRetransmit)
                {
                    OnLogInfo?.Invoke("Transmit failed! Please check the link, or the destination.");
                    onFinished?.Invoke(this);
                }
                else
                {
                    SendFrame();
                }
            }
        }
    }
}
