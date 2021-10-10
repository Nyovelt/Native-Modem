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
    }
}
