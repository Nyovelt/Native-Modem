using System;

namespace Native_Modem
{
    abstract class TransportSession
    {
        protected readonly byte destination;
        protected readonly FullDuplexModem modem;
        protected readonly Action<TransportSession> onFinished;

        public bool ReadyToSend { get; protected set; }
        public Action<string> OnLogInfo;

        public TransportSession(byte destination, FullDuplexModem modem, Action<TransportSession> onFinished)
        {
            this.destination = destination;
            this.modem = modem;
            this.onFinished = onFinished;
            ReadyToSend = false;
        }

        public virtual void OnSessionActivated() { }

        public virtual void OnReceiveFrame(byte src_addr, Protocol.FrameType type, uint seqNum) { }

        public abstract byte[] OnGetFrame();

        public virtual void OnFrameSent() { }
    }
}
