using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace Native_Modem
{
    public class BitStream : IEnumerable<int>
    {
        public int Length { get; }

        readonly List<int> stream;

        public BitStream()
        {
            stream = new List<int>();
            Length = 0;
        }

        public BitStream(StreamReader stringStream)
        {
            stream = new List<int>();
            Length = 0;
            int buffer = 0;
            int bitCount = 0;
            foreach (char c in stringStream.ReadLine())
            {
                if (int.TryParse(c.ToString(), out int bit) && (bit == 0 || bit == 1))
                {
                    buffer |= bit << bitCount;
                    Length++;
                    bitCount++;
                    if (bitCount == 32)
                    {
                        stream.Add(buffer);
                        bitCount = 0;
                    }
                }
                else
                {
                    throw new Exception("bit stream parse error!");
                }
            }
            if (bitCount > 0)
            {
                stream.Add(buffer);
            }
        }

        public IEnumerator<int> GetEnumerator()
        {
            int count = 0;
            foreach (int word in stream)
            {
                for (int j = 0; j < 32; j++)
                {
                    if (count >= Length)
                    {
                        break;
                    }
                    yield return (word >> j) & 1;
                    count++;
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }
    }
}
