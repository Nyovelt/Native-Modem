using System;
using System.Timers;

namespace Native_Modem
{
    public partial class HalfDuplexModem
    {
        class MacPingSession : TransportSession
        {
            readonly Timer timer;
            readonly byte[] frame;
            readonly uint seqNum;
            DateTime sendTimestamp;

            public MacPingSession(byte destination, HalfDuplexModem modem, Action<TransportSession> onFinished, double timeout) : base(destination, modem, onFinished)
            {
                timer = new Timer(timeout);
                timer.Elapsed += (sender, e) => 
                {
                    OnLogInfo?.Invoke("Timeout!");
                    onFinished.Invoke(this);
                };

                seqNum = Protocol.Frame.RandomSequenceNumber();
                frame = Protocol.Frame.WrapFrameWithoutData(
                    destination, 
                    modem.macAddress, 
                    Protocol.FrameType.MacPing_Req, 
                    seqNum);

                ReadyToSend = true;
            }

            public override void OnSessionActivated()
            {
                OnLogInfo?.Invoke($"Start pinging {destination}...");
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
                    onFinished.Invoke(this);
                }
            }

            public override byte[] OnGetFrame()
            {
                ReadyToSend = false;
                sendTimestamp = DateTime.Now;
                return frame;
            }
        }
    }
}
