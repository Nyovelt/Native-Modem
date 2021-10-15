using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using STH1123.ReedSolomon;
using NullFX.CRC;
namespace Native_Modem
{
    class Encoder
    {
        byte[][] data; // 130 * 20 => 10010 valid bits
        private GenericGF gf;
        private ReedSolomonEncoder encoder;

        public Encoder()
        {
            // initialize  ReedSolomon
            gf = new GenericGF(285, 256, 1);
            encoder = new ReedSolomonEncoder(gf);

            // generate 130*20 array
            data = new byte[130][];
            for (int i = 0; i < 130; i++)
            {
                data[i] = new byte[11];
            }

        }



        public BitArray encodeToIntArray(BitArray bitArray)
        {
            //   | 7 bit of valid bit | check bit |   => 8 bit in total
            //   | 11 bulk of bit |  9 bit of ReedSolomon bit |

            var index_i = 0;
            var index_j = 0;
            for (var i = 0; i < bitArray.Length; i += 7)
            {
                var tmp_int = 0;
                int have_even_0 = 0;

                for (var j = 0; j < 7; ++j)
                {
                    if (i + j < bitArray.Length)
                    {
                        tmp_int += (bitArray[i + j] == true ? 1 : 0);
                        have_even_0 += (bitArray[i + j] == false ? 1 : 0);
                    }
                    else
                    {
                        tmp_int += 0;
                        have_even_0 += 1;
                    }
                    tmp_int <<= 1;
                }
                tmp_int += (have_even_0 % 2);

                data[index_i][index_j] = (byte)tmp_int;

                index_j += 1;
                if (index_j == 11 || i + 7 > bitArray.Length)
                {
                    //Console.WriteLine(BitConverter.ToString(data[index_i]));
                    data[index_i] = encoder.EncodeEx(data[index_i], 9);
                    //if (index_i < 5)
                    //    Console.WriteLine(BitConverter.ToString(data[index_i]));
                    index_i += 1;
                    index_j = 0;
                }

            }

            //foreach (var i in data)
            //{
            //    foreach(var j in i)
            //    {
            //        Console.Write($" {j} ");
            //    }
            //}

            BitArray return_bitArray = new BitArray(20800);
            int bitArray_index = 0;
            for (int i = 0; i < 130; ++i)
            {
                for (int j = 0; j < 20; ++j)
                {
                    byte pickup_int = data[i][j];
                    for (int k = 7; k >= 0; --k)
                    {
                        return_bitArray[bitArray_index] = (1 & pickup_int >> k) == 1;
                        bitArray_index++;
                    }
                }
            }
            //foreach (bool i in return_bitArray)
            //{
            //    Console.Write((i ? 1 : 0));
            //}
            return return_bitArray;
        }
    }
}
