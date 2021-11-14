using System;
using System.Collections.Generic;
using System.Timers;

namespace Native_Modem
{
    public partial class HalfDuplexModem
    {
        class TxSession
        {
            public enum TxStatus
            {
                ReadyToSend,
                WaitingForAck,
                Finished
            }

            readonly byte destination;
            readonly Timer timer;
            readonly Queue<byte[]> framesPending;
            uint lastAck;

            public TxStatus Status { get; private set; }

            public TxSession(SendRequest sendRequest, byte maxDataPerFrame, byte macAddress, double ackTimeout)
            {
                destination = sendRequest.DestinationAddress;
                timer = new Timer(ackTimeout);
                timer.Elapsed += (sender, e) => Status = TxStatus.ReadyToSend;
                framesPending = new Queue<byte[]>();
                lastAck = Protocol.Frame.RandomSequenceNumber();

                int fullFrames = sendRequest.Data.Length / maxDataPerFrame;
                int byteCounter = 0;
                uint seqCounter = Protocol.Frame.NextSequenceNumberOf(lastAck);

                framesPending.Enqueue(Protocol.Frame.WrapFrameWithoutData(
                    destination,
                    macAddress,
                    Protocol.FrameType.Data_Start,
                    seqCounter));
                seqCounter = Protocol.Frame.NextSequenceNumberOf(seqCounter);

                for (int i = 0; i < fullFrames; i++)
                {
                    framesPending.Enqueue(Protocol.Frame.WrapDataFrame(
                        destination,
                        macAddress,
                        seqCounter,
                        sendRequest.Data,
                        byteCounter++,
                        maxDataPerFrame));
                    seqCounter = Protocol.Frame.NextSequenceNumberOf(seqCounter);
                }

                int remainBytes = sendRequest.Data.Length - byteCounter;
                if (remainBytes != 0)
                {
                    framesPending.Enqueue(Protocol.Frame.WrapDataFrame(
                        destination,
                        macAddress,
                        seqCounter,
                        sendRequest.Data,
                        byteCounter++,
                        (byte)remainBytes));
                    seqCounter = Protocol.Frame.NextSequenceNumberOf(seqCounter);
                }

                framesPending.Enqueue(Protocol.Frame.WrapFrameWithoutData(
                    destination,
                    macAddress,
                    Protocol.FrameType.Data_End,
                    seqCounter));

                Status = TxStatus.ReadyToSend;
            }

            public bool ReceiveAck(byte src_addr, uint seqNum)
            {
                if (src_addr != destination)
                {
                    return false;
                }

                if (framesPending.Count == 0)
                {
                    return true;
                }


                if (seqNum == Protocol.Frame.NextSequenceNumberOf(lastAck))
                {
                    lastAck = seqNum;
                    framesPending.Dequeue();
                    timer.Stop();
                    if (framesPending.Count == 0)
                    {
                        Status = TxStatus.Finished;
                        return true;
                    }
                    else
                    {
                        Status = TxStatus.ReadyToSend;
                    }
                }
                return false;
            }

            public byte[] GetFrameToSend()
            {
                Status = TxStatus.WaitingForAck;
                return framesPending.Peek();
            }

            public void StartCountdown()
            {
                timer.Start();
            }
        }
    }
}
