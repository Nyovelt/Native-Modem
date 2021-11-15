#define THROW_PHASE_ERROR

using Force.Crc32;
using System;
using System.Collections;

namespace Native_Modem
{
    /// <summary>
    /// Preamble: 32bits
    /// Frame: [ dest_addr (8 bits) | 
    /// src_addr (8 bits) | 
    /// type (4 bits) | 
    /// seq_num (4 bits) | 
    /// length (8 bits) | 
    /// payload (0 - max bytes) | 
    /// crc32 (32 bits) ]
    /// 4B5B
    /// </summary>
    public class Protocol
    {
        static readonly byte[] B5B4 = new byte[32]
        {
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
            0xFF, 0x01, 0x04, 0x05, 0xFF, 0xFF, 0x06, 0x07,
            0xFF, 0xFF, 0x08, 0x09, 0x02, 0x03, 0x0A, 0x0B,
            0xFF, 0xFF, 0x0C, 0x0D, 0x0E, 0x0F, 0x00, 0xFF
        };

        static readonly byte[] B4B5 = new byte[16]
        {
            0x1E, 0x09, 0x14, 0x15, 0x0A, 0x0B, 0x0E, 0x0F,
            0x12, 0x13, 0x16, 0x17, 0x1A, 0x1B, 0x1C, 0x1D
        };

        public static int Convert8To10(byte data)
        {
            int ret = B4B5[data & 0x0F];
            ret |= B4B5[(uint)data >> 4] << 5;
            return ret;
        }

        public static byte Convert10To8(int raw)
        {
            int low = B5B4[raw & 0x1F];
            int high = B5B4[raw >> 5];
            //if (low == 0xFF || high == 0xFF)
            //{
            //    System.Console.WriteLine("B5B4 error!");
            //}
            return (byte)(low | (high << 4));
        }

        public enum FrameType
        {
            Data = 0,
            Data_Start = 1,
            Data_End = 2,
            Acknowledgement = 3,
            MacPing_Req = 4,
            MacPing_Reply = 5
        }

        public static class Frame
        {
            public static byte GetDestination(byte[] frame) => frame[0];
            public static byte GetSource(byte[] frame) => frame[1];
            public static FrameType GetType(byte[] frame) => (FrameType)((uint)frame[2] & 0x0F);
            public static uint GetSequenceNumber(byte[] frame) => (uint)frame[2] >> 4;
            public static byte GetDataLength(byte[] frame) => frame[3];
            public static bool IsValid(byte[] frame) => Crc32Algorithm.IsValidWithCrcAtEnd(frame);

            public static uint RandomSequenceNumber() => (uint)new Random().Next(16);
            public static uint NextSequenceNumberOf(uint seqNum) => (seqNum + 1) & 0x0F;
            public static uint SequenceNumberMinus(uint lhs, uint rhs) => (lhs - rhs) & 0x0F;

            public static byte[] WrapDataFrame(byte dest_addr, byte src_addr, uint seq_num, byte[] data, int offset, byte length)
            {
                byte[] frame = new byte[8 + length];
                frame[0] = dest_addr;
                frame[1] = src_addr;
                frame[2] = (byte)(((uint)FrameType.Data | (seq_num << 4)) & 0xFF);
                frame[3] = length;

                Array.Copy(data, offset, frame, 4, length);

                Crc32Algorithm.ComputeAndWriteToEnd(frame);
                return frame;
            }

            public static byte[] WrapFrameWithoutData(byte dest_addr, byte src_addr, FrameType type, uint seq_num)
            {
                byte[] frame = new byte[8];
                frame[0] = dest_addr;
                frame[1] = src_addr;
                frame[2] = (byte)(((uint)type | (seq_num << 4)) & 0xFF);
                frame[3] = 0;

                Crc32Algorithm.ComputeAndWriteToEnd(frame);
                return frame;
            }

            public static byte[] ExtractData(byte[] frame)
            {
                byte[] ret = new byte[frame[3]];
                Array.Copy(frame, 4, ret, 0, frame[3]);
                return ret;
            }
        }

        public BitArray Preamble { get; }
        public int PreambleSampleCount { get; }

        public bool StartPhase { get; }
        public float[] ClockSync { get; }
        public float ClockSyncPowerThreshold { get; }
        public byte SFDByte { get; }

        public float Amplitude { get; }
        public float Threshold { get; }

        public int SampleRate { get; }

        public int SamplesPerBit { get; }
        public int SamplesPerTenBits { get; }

        public byte FrameMaxDataBytes { get; }

        public int QuietCriteria { get; }
        public double AckTimeout { get; }

        const float syncPowerThreshold = 0.5f;

        public Protocol(float amplitude,int sampleRate, int samplesPerBit, byte maxPayloadSize, double ackTimeout)
        {
            // preamble 32 bits: 10101010 10101010 10101010 10101011
            Preamble = new BitArray(new byte[4] { 0x55, 0x55, 0x55, 0xD5 });
            PreambleSampleCount = Preamble.Count * samplesPerBit;
            StartPhase = false;

            ClockSync = new float[8 * samplesPerBit];
            int counter = 0;
            foreach (bool bit in new BitArray(new byte[1] { 0x55 }))
            {
                float sample = bit ? amplitude : -amplitude;
                for (int i = 0; i < samplesPerBit; i++)
                {
                    ClockSync[counter++] = sample;
                }
            }
            ClockSyncPowerThreshold = amplitude * amplitude * counter * syncPowerThreshold;
            SFDByte = 171;

            Amplitude = amplitude;
            Threshold = amplitude * 0.4f;
            SampleRate = sampleRate;
            SamplesPerBit = samplesPerBit;
            SamplesPerTenBits = samplesPerBit * 10;
            FrameMaxDataBytes = maxPayloadSize;
            QuietCriteria = 4 * samplesPerBit;
            AckTimeout = ackTimeout;
        }

        public static bool GetBit(float power, ref bool phase)
        {
            bool newPhase = power > 0f;
            if (newPhase != phase)
            {
                phase = newPhase;
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
