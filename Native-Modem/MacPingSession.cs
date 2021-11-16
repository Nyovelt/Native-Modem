using System;
using System.Timers;

namespace Native_Modem
{
    public partial class FullDuplexModem
    {
        class MacPingSession : TransportSession
        {
            readonly Timer timer;
            readonly byte[] frame;
            readonly uint seqNum;
            DateTime sendTimestamp;

            public MacPingSession(byte destination, FullDuplexModem modem, Action<byte[], Action<bool>> sendFrame, Action<TransportSession> onFinished, double timeout) : base(destination, modem, sendFrame, onFinished)
            {
                timer = new Timer(timeout);
                timer.AutoReset = false;
                timer.Elapsed += (sender, e) => 
                {
                    OnLogInfo?.Invoke("Timeout!");
                    onFinished?.Invoke(this);
                };

                seqNum = Protocol.Frame.RandomSequenceNumber();
                frame = Protocol.Frame.WrapFrameWithoutData(
                    destination, 
                    modem.macAddress, 
                    Protocol.FrameType.MacPing_Req, 
                    seqNum);
            }

            public override void OnSessionActivated()
            {
                OnLogInfo?.Invoke($"Start pinging {destination}...");
                sendTimestamp = DateTime.Now;
                timer.Start();
                sendFrame.Invoke(frame, null);
            }

            public override void OnReceiveFrame(byte src_addr, Protocol.FrameType type, uint seqNum)
            {
                if (src_addr != destination || type != Protocol.FrameType.MacPing_Reply)
                {
                    return;
                }

                if (seqNum == this.seqNum)
                {
                    timer.Stop();
                    DateTime receiveTimestamp = DateTime.Now;
                    OnLogInfo?.Invoke($"RTT = {(receiveTimestamp - sendTimestamp).TotalMilliseconds}ms");
                    onFinished?.Invoke(this);
                }
            }

            public override void OnInterrupted()
            {
                timer.Stop();
            }
        }
    }
}
