using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NAudio.Wave;
using Force.Crc32;

namespace Native_Modem
{
    public partial class HalfDuplexModem
    {
        enum ModemState
        {
            WaitForQuiet,
            FrameDetection,
            Rx,
            Tx,
            TxACK
        }

        readonly Protocol protocol;
        readonly AsioOut asioOut;
        readonly TxThread Tx;
        readonly RxThread Rx;
        readonly Queue<byte[]> TxPending;
        readonly byte macAddress;
        readonly float[] RxBuffer;
        readonly Action<byte, byte[]> onDataReceived;
        readonly Task loopTask;

        ModemState modemState;

        /// <summary>
        /// The parameters of onDataReceived are source address and payload
        /// </summary>
        /// <param name="onFrameReceived"></param>
        public HalfDuplexModem(Protocol protocol, byte macAddress, string driverName, Action<byte, byte[]> onDataReceived, string saveTransportTo = null, string saveRecordTo = null)
        {
            this.protocol = protocol;
            this.macAddress = macAddress;
            this.onDataReceived = onDataReceived;

            Tx = new TxThread(protocol, protocol.SampleRate, saveTransportTo);
            Rx = new RxThread(protocol, protocol.SampleRate >> 1, saveRecordTo);
            TxPending = new Queue<byte[]>();

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
            modemState = ModemState.FrameDetection;

            asioOut.AudioAvailable += OnRxSamplesAvailable;
            asioOut.Play();

            loopTask = Task.Run(MainLoop);
        }

        public void Dispose()
        {
            loopTask.Dispose();

            asioOut.Stop();
            asioOut.AudioAvailable -= OnRxSamplesAvailable;

            asioOut.Dispose();
            Tx.Dispose();
            Rx.Dispose();
        }

        public void Transport(SendRequest sendRequest)
        {
            int fullFrames = sendRequest.Data.Length / protocol.FrameMaxDataBytes;
            int byteCounter = 0;
            for (int i = 0; i < fullFrames; i++)
            {
                TxPending.Enqueue(WrapFrame(
                    sendRequest.DestinationAddress,
                    Protocol.FrameType.DATA,
                    sendRequest.Data,
                    byteCounter,
                    protocol.FrameMaxDataBytes));
            }

            int remainBytes = sendRequest.Data.Length - byteCounter;
            if (remainBytes != 0)
            {
                TxPending.Enqueue(WrapFrame(
                    sendRequest.DestinationAddress,
                    Protocol.FrameType.DATA,
                    sendRequest.Data,
                    byteCounter,
                    (byte)remainBytes));
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

        byte[] WrapFrame(byte dest_addr, byte type, byte[] data, int offset, byte length)
        {
            byte[] frame = new byte[8 + length];
            frame[0] = dest_addr;
            frame[1] = macAddress;
            frame[2] = type;
            frame[3] = length;

            Array.Copy(data, offset, frame, 4, length);

            Crc32Algorithm.ComputeAndWriteToEnd(frame);
            return frame;
        }

        async Task MainLoop()
        {
            while (true)
            {
                switch (modemState)
                {
                    case ModemState.WaitForQuiet:
                        await Rx.WaitUntilQuiet();
                        break;

                    case ModemState.FrameDetection:
                        break;

                    case ModemState.Rx:
                        break;

                    case ModemState.Tx:
                        break;

                    case ModemState.TxACK:
                        break;
                }
            }
        }
    }
}
