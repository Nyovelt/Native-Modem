using STH1123.ReedSolomon;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Native_Modem
{
    static class ReedSolomonExtensions
    {
        static public byte[] EncodeEx(this ReedSolomonEncoder encoder, byte[] toEncode, int ecBytes)
        {
            var dataByte = toEncode
                .Select(x => (int)x) // byte[] -> iterator<int>
                .Concat(new int[ecBytes]) // it<int>.append(ecBytes of 0)
                .ToArray();

            encoder.Encode(dataByte, ecBytes);
            return toEncode.Concat(dataByte.TakeLast(ecBytes).Select(x => (byte)x)).ToArray();
        }

        static public bool TryDecodeEx(this ReedSolomonDecoder decoder, byte[] toDecode, int ecBytes, out byte[] result)
        {
            var dataByte = toDecode
                .Select(x => (int)x) // byte[] -> iterator<int>
                .ToArray();

            if (decoder.Decode(dataByte, ecBytes))
            {
                result = dataByte.SkipLast(ecBytes).Select(x => (byte)x).ToArray();
                return true;
            }

            result = null;
            return false;
        }
    }
}
