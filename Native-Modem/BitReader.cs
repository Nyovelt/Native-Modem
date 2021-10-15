using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace Native_Modem
{
    public static class BitReader
    {
        public static BitArray ReadBits(StreamReader stringStream)
        {
            List<bool> bits = new List<bool>();

            foreach (char c in stringStream.ReadLine())
            {
                if (int.TryParse(c.ToString(), out int bit) && (bit == 0 || bit == 1))
                {
                    bits.Add(bit == 1);
                }
                else
                {
                    throw new Exception("bit stream parse error!");
                }
            }
            return new BitArray(bits.ToArray());
        }

        public static byte[] ReadBitsIntoBytes(StreamReader stringStream)
        {
            List<byte> bytes = new List<byte>();

            int bitCount = 0;
            int byteData = 0;
            foreach (char c in stringStream.ReadLine())
            {
                if (int.TryParse(c.ToString(), out int bit) && (bit == 0 || bit == 1))
                {
                    byteData |= bit << (bitCount++);
                    if (bitCount == 8)
                    {
                        bytes.Add((byte)byteData);
                        bitCount = 0;
                        byteData = 0;
                    }
                }
                else
                {
                    throw new Exception("bit stream parse error!");
                }
            }

            if (bitCount != 0)
            {
                Console.WriteLine("bit stream cannot fit in bytes, padding with zeroes");
                bytes.Add((byte)byteData);
            }
            return bytes.ToArray();
        }
    }
}
