using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;

namespace Confuser.Core
{
    public static class ObfuscationHelper
    {
        static MD5 md5 = MD5.Create();

        public static string GetNewName(string originalName)
        {
            BitArray arr = new BitArray(BitConverter.GetBytes(originalName.GetHashCode()));

            Random rand = new Random(originalName.GetHashCode());
            byte[] xorB = new byte[arr.Length / 8];
            rand.NextBytes(xorB);
            BitArray xor = new BitArray(xorB);

            BitArray result = arr.Xor(xor);
            byte[] ret = new byte[result.Length / 8];
            result.CopyTo(ret, 0);

            return Encoding.Unicode.GetString(ret).Replace("\0", "").Replace(".", "").Replace("/", "");
        }
    }
}
