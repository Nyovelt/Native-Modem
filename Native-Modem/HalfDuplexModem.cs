using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NAudio.Wave;
using Force.Crc32;
using System.Timers;

namespace Native_Modem
{
    public partial class HalfDuplexModem
    {
        enum ModemState
        {
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

        readonly byte[] ackFrame;

        bool disposed;

        ModemState state;

        /// <summary>
        /// The parameters of onDataReceived are source address and payload
        /// </summary>
        /// <param name="onDataReceived"></param>
        public HalfDuplexModem(Protocol protocol, byte macAddress, string driverName, Action<byte, byte[]> onDataReceived, string saveTransportTo = null, string saveRecordTo = null)
        {
            this.protocol = protocol;
            this.macAddress = macAddress;
            this.onDataReceived = onDataReceived;
            disposed = false;

            Tx = new TxThread(protocol, protocol.SampleRate, saveTransportTo);
            Rx = new RxThread(protocol, protocol.SampleRate >> 1, saveRecordTo);
            TxPending = new Queue<byte[]>();

            ackFrame = new byte[8];

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
            state = ModemState.FrameDetection;

            asioOut.AudioAvailable += OnRxSamplesAvailable;
            asioOut.Play();

            loopTask = Task.Run(MainLoop);
        }

        public void Dispose()
        {
            disposed = true;

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

        void SetAckFrame(byte dest_addr)
        {
            ackFrame[0] = dest_addr;
            ackFrame[1] = macAddress;
            ackFrame[2] = Protocol.FrameType.ACKNOWLEDGEMENT;
            ackFrame[3] = 0;

            Crc32Algorithm.ComputeAndWriteToEnd(ackFrame);
        }

        bool UntilDisposed()
        {
            return disposed;
        }

        bool ValidateFrame(byte[] frame)
        {
            if (frame[0] != macAddress)
            {
                return false;
            }

            return Crc32Algorithm.IsValidWithCrcAtEnd(frame);
        }

        async Task MainLoop()
        {
            bool waitingForACK = false;
            byte[] frameReceived = null;
            Timer ackTimer = new Timer(protocol.AckTimeout);
            ackTimer.Elapsed += (sender, e) =>
            {
                waitingForACK = false;
            };
            TaskStatus tempStatus;

            while (true)
            {
                switch (state)
                {
                    case ModemState.FrameDetection:
                        if (!await Rx.WaitUntilQuiet(UntilDisposed))
                        {
                            return;
                        }

                        tempStatus = await Rx.DetectFrame(UntilDisposed, () => !waitingForACK && TxPending.Count > 0);
                        switch (tempStatus)
                        {
                            case TaskStatus.Success:
                                state = ModemState.Rx;
                                break;
                            case TaskStatus.Interrupted:
                                state = ModemState.Tx;
                                break;
                            case TaskStatus.Canceled:
                                return;
                            default:
                                return;
                        }
                        break;

                    case ModemState.Rx:
                        (bool notCanceled, byte[] frame) = await Rx.DemodulateFrame(UntilDisposed);
                        if (!notCanceled)
                        {
                            return;
                        }

                        if (frame == null)
                        {
                            state = ModemState.FrameDetection;
                        }
                        else
                        {
                            frameReceived = frame;
                            if (ValidateFrame(frameReceived))
                            {
                                if (frameReceived[2] == Protocol.FrameType.DATA)
                                {
                                    byte[] ret = new byte[frameReceived[3]];
                                    Array.Copy(frameReceived, 4, ret, 0, frameReceived[3]);
                                    onDataReceived.Invoke(frameReceived[1], ret);
                                    state = ModemState.TxACK;
                                }
                                else if (frameReceived[2] == Protocol.FrameType.ACKNOWLEDGEMENT)
                                {
                                    if (waitingForACK)
                                    {
                                        TxPending.Dequeue();
                                        waitingForACK = false;
                                    }
                                    state = ModemState.FrameDetection;
                                }
                                else
                                {
                                    state = ModemState.FrameDetection;
                                    //...
                                }
                            }
                            else
                            {
                                state = ModemState.FrameDetection;
                            }
                        }
                        break;

                    case ModemState.Tx:
                        if (!await Rx.WaitUntilQuiet(UntilDisposed))
                        {
                            return;
                        }

                        if (!await Tx.Push(TxPending.Peek(), UntilDisposed))
                        {
                            return;
                        }
                        waitingForACK = true;
                        ackTimer.Start();
                        state = ModemState.FrameDetection;
                        break;

                    case ModemState.TxACK:
                        //Not waiting for quiet
                        SetAckFrame(frameReceived[1]);
                        if (!await Tx.Push(ackFrame, UntilDisposed))
                        {
                            return;
                        }
                        state = ModemState.FrameDetection;
                        break;
                }
            }
        }
    }
}
