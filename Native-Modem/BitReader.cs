using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace Native_Modem
{
    public static class BitReader
    {
        public static BitArray DirtyReadBits(StreamReader stringStream)
        {
            List<bool> bits = new List<bool>();

            //  Hack: Add 100 zeros for heating 
            Random rd = new Random();
            for (var i=0;i < 300; ++i)
            {
                bits.Add(rd.Next(0,2) == 0);
            }


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
