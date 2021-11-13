namespace Native_Modem
{
    public struct SendRequest
    {
        public byte DestinationAddress;
        public byte[] Data;

        public SendRequest(byte destinationAddress, byte[] data)
        {
            DestinationAddress = destinationAddress;
            Data = data;
        }
    }
}
