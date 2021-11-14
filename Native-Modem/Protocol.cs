#define THROW_PHASE_ERROR

using NAudio.Wave;
using System.Collections;

namespace Native_Modem
{
    /// <summary>
    /// Preamble: 32bits
    /// Frame: [ dest_addr (8 -> 10bits) | 
    /// src_addr (8 -> 10bits) | 
    /// type (8 -> 10bits) | 
    /// length (8 -> 10bits) | 
    /// payload (variable) | 
    /// crc32 (32 -> 40bits) ]
    /// MLT-3 & 4B5B
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

        public static class FrameType
        {
            public static readonly byte DATA = 0;
            public static readonly byte ACKNOWLEDGEMENT = 1;
            public static readonly byte MACPING_REQ = 2;
            public static readonly byte MACPING_REPLY = 3;

            public static string GetName(byte frameType)
            {
                switch (frameType)
                {
                    case 0:
                        return "Data";
                    case 1:
                        return "Acknowledgement";
                    case 2:
                        return "MacPing_Req";
                    case 3:
                        return "MacPing_Reply";
                    default:
                        return "UNKNOWN";
                }
            }
        }

        public BitArray Preamble { get; }
        public float[] PhaseLevel { get; }
        public int StartPhase { get; }
        public int IPGBits { get; }
        public float[] ClockSync { get; }
        public float ClockSyncPowerThreshold { get; }
        public byte SFDByte { get; }
        public float Amplitude { get; }
        public float Threshold { get; }
        public int SampleRate { get; }
        public int SamplesPerBit { get; }
        public int SamplesPerTenBits { get; }
        public byte FrameMaxDataBytes { get; }
        public bool UseStereo { get; }
        public int QuietCriteria { get; }
        public double AckTimeout { get; }

        readonly float powerThreshold;

        const float syncPowerThreshold = 0.5f;

        public Protocol(float amplitude,int sampleRate, int samplesPerBit, byte maxPayloadSize, bool useStereo, double ackTimeout)
        {
            // preamble 32 bits: 10101010 10101010 10101010 10101011
            Preamble = new BitArray(new byte[4] { 0x55, 0x55, 0x55, 0xD5 });
            IPGBits = 16;
            PhaseLevel = new float[4] { -amplitude, 0f, amplitude, 0f };
            StartPhase = 0;

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
            UseStereo = useStereo;
            QuietCriteria = IPGBits >> 1 * SamplesPerBit;
            AckTimeout = ackTimeout;

            powerThreshold = Threshold * samplesPerBit;
        }

        public bool GetBit(float power, ref int phase)
        {
            bool bit;
            if (power > powerThreshold)
            {
                bit = phase == 1;
#if THROW_PHASE_ERROR
                if (!bit && phase != 2)
                {
                    throw new System.Exception($"Phase jumped to high! previously: {(phase == 0 ? "low" : "drop")}");
                }
#endif
                phase = 2;
                return bit;
            }
            else if (power > -powerThreshold)
            {
                bit = phase == 0 || phase == 2;
                if (bit)
                {
                    phase++;
                }
                return bit;
            }
            else
            {
                bit = phase == 3;
#if THROW_PHASE_ERROR
                if (!bit && phase != 0)
                {
                    throw new System.Exception($"Phase jumped to low! previously: {(phase == 1 ? "rise" : "high")}");
                }
#endif
                phase = 0;
                return bit;
            }
        }
    }
}
