using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Native_Modem
{
    public partial class FullDuplexModem
    {
        class DataReceiveSession
        {
            readonly byte source;
            readonly FullDuplexModem modem;
            readonly Action<byte[], Action<bool>> sendFrame;
            readonly Action<byte, byte[]> onFileReceived;
            readonly List<byte[]> framesReceived;
            uint lastSeqNum;
            bool completed;

            public bool IsCompleted => completed;

            DataReceiveSession(byte source, FullDuplexModem modem, Action<byte[], Action<bool>> sendFrame, Action<byte, byte[]> onFileReceived)
            {
                this.source = source;
                this.modem = modem;
                this.sendFrame = sendFrame;
                this.onFileReceived = onFileReceived;
                framesReceived = new List<byte[]>();
                completed = false;
            }

            public void ReceiveFrame(byte[] frame)
            {
                Protocol.FrameType type = Protocol.Frame.GetType(frame);
                uint seqNum = Protocol.Frame.GetSequenceNumber(frame);
                switch (type)
                {
                    case Protocol.FrameType.Data_Start:
                        if (seqNum != lastSeqNum)
                        {
                            throw new Exception("Data start frames with different sequence number received!");
                        }
                        else
                        {
                            SendAck();
                        }
                        break;

                    case Protocol.FrameType.Data:
                        if (seqNum == Protocol.Frame.NextSequenceNumberOf(lastSeqNum))
                        {
                            lastSeqNum = seqNum;
                            framesReceived.Add(frame);
                        }
                        SendAck();
                        break;

                    case Protocol.FrameType.Data_End:
                        if (!IsCompleted && seqNum == Protocol.Frame.NextSequenceNumberOf(lastSeqNum))
                        {
                            lastSeqNum = seqNum;
                            completed = true;
                            SendAck();
                            ReportFile();
                        }
                        else
                        {
                            SendAck();
                        }
                        break;

                    default:
                        break;
                }
            }

            void SendAck()
            {
                sendFrame?.Invoke(
                    Protocol.Frame.WrapFrameWithoutData(
                        source,
                        modem.macAddress,
                        Protocol.FrameType.Acknowledgement,
                        lastSeqNum),
                    null);
            }

            void ReportFile()
            {
                int length = 0;
                foreach (byte[] frame in framesReceived)
                {
                    length += Protocol.Frame.GetDataLength(frame);
                }
                byte[] file = new byte[length];
                int byteCount = 0;
                foreach (byte[] frame in framesReceived)
                {
                    int len = Protocol.Frame.GetDataLength(frame);
                    Array.Copy(frame, Protocol.Frame.DATA_OFFSET, file, byteCount, len);
                    byteCount += len;
                }
                onFileReceived.Invoke(source, file);
            }

            public static DataReceiveSession InitializeSession(byte source, FullDuplexModem modem, Action<byte[], Action<bool>> sendFrame, Action<byte, byte[]> onFileReceived, uint firstSeqNum)
            {
                DataReceiveSession session = new DataReceiveSession(source, modem, sendFrame, onFileReceived);
                session.lastSeqNum = firstSeqNum;
                session.SendAck();
                return session;
            }
        }
    }
}
