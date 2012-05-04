using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;

namespace Confuser.Core
{
    public enum NameMode
    {
        Unreadable,
        ASCII,
        Letters
    }
    public static class ObfuscationHelper
    {
        static MD5 md5 = MD5.Create();

        public static string GetNewName(string originalName)
        {
            return GetNewName(originalName, NameMode.Unreadable);
        }
        public static string GetNewName(string originalName, NameMode mode)
        {
            switch (mode)
            {
                case NameMode.Unreadable: return RenameUnreadable(originalName);
                case NameMode.ASCII: return RenameASCII(originalName);
                case NameMode.Letters: return RenameLetters(originalName);
            } throw new InvalidOperationException();
        }

        static string RenameUnreadable(string originalName)
        {
            BitArray arr = new BitArray(md5.ComputeHash(Encoding.UTF8.GetBytes(originalName)));

            Random rand = new Random(originalName.GetHashCode());
            byte[] xorB = new byte[arr.Length / 8];
            rand.NextBytes(xorB);
            BitArray xor = new BitArray(xorB);

            BitArray result = arr.Xor(xor);
            byte[] ret = new byte[result.Length / 8];
            result.CopyTo(ret, 0);

            return Encoding.Unicode.GetString(ret).Replace("\0", "").Replace(".", "").Replace("/", "");
        }

        static string RenameASCII(string originalName)
        {
            BitArray arr = new BitArray(md5.ComputeHash(Encoding.UTF8.GetBytes(originalName)));

            Random rand = new Random(originalName.GetHashCode());
            byte[] xorB = new byte[arr.Length / 8];
            rand.NextBytes(xorB);
            BitArray xor = new BitArray(xorB);

            BitArray result = arr.Xor(xor);
            byte[] ret = new byte[result.Length / 8];
            result.CopyTo(ret, 0);

            return Convert.ToBase64String(ret);
        }

        static string RenameLetters(string originalName)
        {
            BitArray arr = new BitArray(md5.ComputeHash(Encoding.UTF8.GetBytes(originalName)));

            Random rand = new Random(originalName.GetHashCode());
            byte[] xorB = new byte[arr.Length / 8];
            rand.NextBytes(xorB);
            BitArray xor = new BitArray(xorB);

            BitArray result = arr.Xor(xor);
            byte[] buff = new byte[result.Length / 8];
            result.CopyTo(buff, 0);

            StringBuilder ret = new StringBuilder();
            int m = 0;
            for (int i = 0; i < buff.Length; i++)
            {
                m = (m << 8) + buff[i];
                while (m > 52)
                {
                    int n = m % 26;
                    if (n < 26)
                        ret.Append((char)('A' + n));
                    else
                        ret.Append((char)('a' + (n - 26)));
                    m /= 52;
                }
            }
            return ret.ToString();
        }
    }
}
