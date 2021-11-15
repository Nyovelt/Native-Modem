using System;

namespace Native_Modem
{
    abstract class TransportSession
    {
        protected readonly byte destination;
        protected readonly FullDuplexModem modem;
        protected readonly Action<TransportSession> onFinished;
        protected readonly Action<byte[], Action> sendFrame;

        public Action<string> OnLogInfo;

        public TransportSession(byte destination, FullDuplexModem modem, Action<byte[], Action> sendFrame, Action<TransportSession> onFinished)
        {
            this.destination = destination;
            this.modem = modem;
            this.sendFrame = sendFrame;
            this.onFinished = onFinished;
        }

        public virtual void OnSessionActivated() { }

        public virtual void OnReceiveFrame(byte src_addr, Protocol.FrameType type, uint seqNum) { }

        public virtual void OnInterrupted() { }
    }
}
