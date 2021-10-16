using STH1123.ReedSolomon;
using System;
using System.Collections;

namespace Native_Modem
{

    class Decoder
    {
        byte[][] data; // 130 * 20 => 10010 valid bits
        bool[] Correct;
        private GenericGF gf;
        private ReedSolomonDecoder decoder;
        public Decoder()
        {
            // initialize  ReedSolomon
            gf = new GenericGF(285, 256, 1);
            decoder = new ReedSolomonDecoder(gf);
            data = new byte[130][];
            for (int i = 0; i < 130; i++)
            {
                data[i] = new byte[20];
            }
            Correct = new bool[130]; 

        }

        public BitArray decodeToArray(BitArray bitArray)
        {
            // to byte[][]
            //foreach (bool i in bitArray)
            //{
            //    Console.Write((i ? 1 : 0));
            //}
            var bitArray_index = 0;
            for (var i = 0; i < 130; ++i)
            {

                for (var j = 0; j < 20; ++j)
                {
                    int have_even_0 = 0;
                    
                    for (var k = 0; k < 7; ++k)
                    {
                        data[i][j] += (byte)(bitArray[bitArray_index] ? 1 : 0);
                        have_even_0 += bitArray[bitArray_index] == false ? 1 : 0;
                        bitArray_index++;
                        data[i][j] <<= 1;
                    }
                    data[i][j] += (byte)(bitArray[bitArray_index] ? 1 : 0);
                    if (((((have_even_0 % 2) == 1) ? true : false) != bitArray[bitArray_index]) && (j < 11))
                    {
                        data[i][j] = 0; // See source code, equals to tell where it's wrong
                    }
                    bitArray_index++;
                    //Console.Write($" {data[i][j]} ");
                }
                
                    
                var status = decoder.TryDecodeEx(data[i],9, out var decodeResult); // return True it will
                Console.WriteLine(status);
                if (status)
                {
                    data[i] = decodeResult;
                    Correct[i] = true;
                }
                

                //if (i < 5)
                //    Console.WriteLine(BitConverter.ToString(data[i]));
               
            }
            bitArray_index = 20800;
            for (var i = 0; i < 130; ++i)
            {
                if (Correct[i])
                {
                    bitArray_index += 160;
                }
                else
                {
                    for (var j = 0; j < 20; ++j)
                    {
                        int have_even_0 = 0;

                        for (var k = 0; k < 7; ++k)
                        {
                            if (bitArray_index > bitArray.Count)
                            {
                                continue;
                            }
                            data[i][j] += (byte)(bitArray[bitArray_index] ? 1 : 0);
                            have_even_0 += bitArray[bitArray_index] == false ? 1 : 0;
                            bitArray_index++;
                            data[i][j] <<= 1;
                        }
                        data[i][j] += (byte)(bitArray[bitArray_index] ? 1 : 0);
                        if (((((have_even_0 % 2) == 1) ? true : false) != bitArray[bitArray_index]) && (j < 11))
                        {
                            data[i][j] = 0; // See source code, equals to tell where it's wrong
                        }
                        bitArray_index++;
                        //Console.Write($" {data[i][j]} ");
                    }


                    var status = decoder.TryDecodeEx(data[i], 9, out var decodeResult); // return True it will
                    Console.WriteLine(status);
                    if (status)
                    {
                        data[i] = decodeResult;
                        Correct[i] = true;
                    }
                }
                //if (i < 5)
                //    Console.WriteLine(BitConverter.ToString(data[i]));

            }

            BitArray return_bitArray = new BitArray(10000);
            bitArray_index = 0;
            for (var i=0; i<130; ++i)
            {
                for (var j=0; j < 11; j++)
                {
                    for (var k = 7; k >= 1; --k)
                    {
                        if (bitArray_index < 10000) { 
                        return_bitArray[bitArray_index] = (1 & data[i][j] >> k) == 1;
                        bitArray_index++;
                        //Console.Write(bitArray_index - 1);
                        }
                        //else: frop

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
