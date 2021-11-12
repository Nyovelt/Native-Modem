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

        public enum Type
        {
            DATA = 0xAA,
            ACKNOWLEDGEMENT = 0xAB,
            MACPING_REQ = 0xAC,
            MACPING_REPLY = 0xAD
        }

        public BitArray Preamble { get; }
        public float[] PhaseLevel { get; }
        public int StartPhase { get; }
        public int IPGBits { get; }
        public float[] ClockSync { get; }
        public float ClockSyncPower { get; }
        public byte SFDByte { get; }
        public float Amplitude { get; }
        public WaveFormat WaveFormat { get; }
        public int SamplesPerBit { get; }
        public int SamplesPerTenBits { get; }
        public byte FrameMaxDataBytes { get; }

        readonly float powerThreshold;

        public Protocol(float amplitude,int sampleRate, int samplesPerBit, byte maxPayloadSize)
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
            ClockSyncPower = amplitude * amplitude * counter;
            SFDByte = 171;

            Amplitude = amplitude;
            powerThreshold = amplitude * 0.45f * samplesPerBit;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
            SamplesPerBit = samplesPerBit;
            SamplesPerTenBits = samplesPerBit * 10;
            FrameMaxDataBytes = maxPayloadSize;
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
