using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using B83.Collections;

namespace Native_Modem
{
    public partial class HalfDuplexModem
    {
        class RxThread
        {
            readonly Protocol protocol;

            public SampleFIFO RxFIFO { get; }

            //Frame detection
            readonly int syncWaitSamples;
            readonly int waitSFDTimeout;
            readonly RingBuffer<float> syncBuffer;
            readonly List<float> syncBitBuffer;

            //Frame demodulation
            readonly List<float> byteSampleBuffer;
            readonly byte[] headerBuffer;

            public RxThread(Protocol protocol, int bufferSize, string saveAudioTo = null)
            {
                this.protocol = protocol;

                RxFIFO = new SampleFIFO(protocol.SampleRate, bufferSize, saveAudioTo);

                syncWaitSamples = protocol.SamplesPerBit - 1;
                waitSFDTimeout = protocol.Preamble.Count;
                syncBuffer = new RingBuffer<float>(protocol.ClockSync.Length);
                syncBitBuffer = new List<float>(protocol.SamplesPerBit);

                byteSampleBuffer = new List<float>(protocol.SamplesPerTenBits);
                headerBuffer = new byte[4];
            }

            public void Dispose()
            {
                RxFIFO.Dispose();
            }

            public async Task<bool> WaitUntilQuiet(Func<bool> cancel)
            {
                TaskStatus status = await WaitUntilQuietInternal(cancel, () => false);
                return status == TaskStatus.Success;
            }

            async Task<TaskStatus> WaitUntilQuietInternal(Func<bool> cancel, Func<bool> interrupt)
            {
                int quietCount = 0;
                while (quietCount <= protocol.QuietCriteria)
                {
                    TaskStatus status = await TaskUtilities.Wait(
                        until: () => !RxFIFO.IsEmpty,
                        interrupt: interrupt,
                        cancel: cancel);
                    if (status != TaskStatus.Success)
                    {
                        return status;
                    }

                    float sample = RxFIFO.Pop();

                    if (MathF.Abs(sample) < protocol.Threshold)
                    {
                        quietCount++;
                    }
                    else
                    {
                        quietCount = 0;
                    }
                }
                return TaskStatus.Success;
            }

            bool DetectLevel(List<float> samples)
            {
                float sum = 0f;
                for (int i = 0; i < protocol.SamplesPerBit; i++)
                {
                    sum += samples[i];
                }
                return sum > 0f;
            }

            public async Task<TaskStatus> DetectFrame(Func<bool> cancel, Func<bool> interrupt)
            {
                bool syncing = true;
                bool detected = false;
                float localMax = 0f;
                int waitSFDCount = 0;
                byte syncBits = 0;
                TaskStatus tempStatus;

                syncBuffer.FillWith(0f);

                while (true)
                {
                    tempStatus = await TaskUtilities.Wait(
                        until: () => !RxFIFO.IsEmpty,
                        interrupt: interrupt,
                        cancel: cancel);
                    if (tempStatus != TaskStatus.Success)
                    {
                        return tempStatus;
                    }

                    float sample = RxFIFO.Pop();
                    syncBuffer.Add(sample);

                    if (syncing)
                    {
                        float syncPower = 0f;
                        for (int i = 0; i < protocol.ClockSync.Length; i++)
                        {
                            syncPower += syncBuffer[i] * protocol.ClockSync[i];
                        }

                        if (syncPower > protocol.ClockSyncPowerThreshold && syncPower > localMax)
                        {
                            localMax = syncPower;
                            detected = true;
                            syncBitBuffer.Clear();
                        }
                        else if (detected)
                        {
                            syncBitBuffer.Add(sample);
                            if (syncBitBuffer.Count >= syncWaitSamples)
                            {
                                localMax = 0f;
                                syncing = false;
                                syncBits = 0;
                                waitSFDCount = 0;
                                detected = false;
                            }
                        }
                    }
                    else
                    {
                        syncBitBuffer.Add(sample);
                        if (syncBitBuffer.Count == protocol.SamplesPerBit)
                        {
                            syncBits <<= 1;
                            if (DetectLevel(syncBitBuffer))
                            {
                                syncBits |= 0x1;
                            }
                            syncBitBuffer.Clear();
                            waitSFDCount++;

                            if (syncBits == protocol.SFDByte)
                            {
                                return TaskStatus.Success;
                            }
                            else if (waitSFDCount >= waitSFDTimeout)
                            {
                                syncing = true;
                                syncBuffer.FillWith(0f);
                                tempStatus = await WaitUntilQuietInternal(cancel, interrupt);
                                if (tempStatus != TaskStatus.Success)
                                {
                                    return tempStatus;
                                }
                            }
                        }
                    }
                }
            }

            bool DemodulateByte(List<float> samples, ref int phase, out byte data)
            {
                int sampleCount = 0;
                int raw = 0;
                for (int i = 0; i < 10; i++)
                {
                    float sum = 0f;
                    for (int j = 0; j < protocol.SamplesPerBit; j++)
                    {
                        sum += samples[sampleCount++];
                    }

                    try
                    {
                        if (protocol.GetBit(sum, ref phase))
                        {
                            raw |= 0x1 << i;
                        }
                    }
                    catch (Exception e)
                    {
                        data = 0;
                        return false;
                    }
                }
                data = Protocol.Convert10To8(raw);
                return true;
            }

            public async Task<(bool, byte[])> DemodulateFrame(Func<bool> cancel)
            {
                byte[] frameBuffer = null;
                int bytesDecoded = 0;
                int phase = protocol.StartPhase;

                byteSampleBuffer.Clear();

                while (true)
                {
                    if (!await TaskUtilities.WaitUntilUnless(
                        () => !RxFIFO.IsEmpty, 
                        () => cancel.Invoke()))
                    {
                        return (false, null);
                    }

                    float sample = RxFIFO.Pop();
                    byteSampleBuffer.Add(sample);
                    if (byteSampleBuffer.Count == protocol.SamplesPerTenBits)
                    {
                        if (!DemodulateByte(byteSampleBuffer, ref phase, out byte data))
                        {
                            return (true, null);
                        }
                        byteSampleBuffer.Clear();
                        if (bytesDecoded < 4)
                        {
                            headerBuffer[bytesDecoded++] = data;
                            if (bytesDecoded == 4)
                            {
                                if (headerBuffer[3] > protocol.FrameMaxDataBytes)
                                {
                                    return (true, null);
                                }
                                frameBuffer = new byte[8 + headerBuffer[3]];
                                Array.Copy(headerBuffer, frameBuffer, 4);
                            }
                        }
                        else
                        {
                            frameBuffer[bytesDecoded++] = data;
                            if (bytesDecoded == frameBuffer.Length)
                            {
                                return (true, frameBuffer);
                            }
                        }
                    }
                }
            }
        }
    }
}
