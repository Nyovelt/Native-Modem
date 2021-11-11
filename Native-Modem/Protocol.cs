using NAudio.Wave;
using System.Collections;

namespace Native_Modem
{
    /// <summary>
    /// Frame: [ preamble (32bits) | 
    /// dest_addr (8bits) | 
    /// src_addr (8bits) | 
    /// type (8bits) | 
    /// length (8bits) | 
    /// payload (variable) | 
    /// crc32 (32bits) ]
    /// </summary>
    public class Protocol
    {
        public enum Type
        {
            DATA = 0xAA,
            ACKNOWLEDGEMENT = 0xAB,
            MACPING_REQ = 0xAC,
            MACPING_REPLY = 0xAD
        }

        public BitArray Preamble { get; }
        public int IPGBits { get; }
        public float[] ClockSync { get; }
        public float ClockSyncPower { get; }
        public byte SFDByte { get; }
        public float Amplitude { get; }
        public WaveFormat WaveFormat { get; }
        public int SamplesPerBit { get; }
        public int SamplesPerByte { get; }
        public byte FrameMaxDataBytes { get; }

        public Protocol(float amplitude,int sampleRate, int samplesPerBit)
        {
            // preamble 32 bits: 10101010 10101010 10101010 10101011
            Preamble = new BitArray(new byte[4] { 0x55, 0x55, 0x55, 0xD5 });
            IPGBits = 32;

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
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
            SamplesPerBit = samplesPerBit;
            SamplesPerByte = samplesPerBit << 3;
            FrameMaxDataBytes = 252;
        }
    }
}
