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
    public class ObfuscationHelper
    {
        public static readonly ObfuscationHelper Instance = new ObfuscationHelper(
            null, typeof(Confuser).Assembly.GetName().Version.ToString().GetHashCode());

        MD5 md5 = MD5.Create();
        int seed;
        Confuser cr;
        internal ObfuscationHelper(Confuser cr, int seed)
        {
            this.cr = cr;
            this.seed = seed;
        }

        public string GetNewName(string originalName)
        {
            return GetNewName(originalName, NameMode.Unreadable);
        }
        public string GetNewName(string originalName, NameMode mode)
        {
            string ret;
            switch (mode)
            {
                case NameMode.Unreadable: ret = RenameUnreadable(originalName); break;
                case NameMode.ASCII: ret = RenameASCII(originalName); break;
                case NameMode.Letters: ret = RenameLetters(originalName); break;
                default: throw new InvalidOperationException();
            }
            if (cr != null)
                cr.Database.AddEntry("Rename", originalName, ret);
            return ret;
        }

        public string GetRandomString()
        {
            if (cr == null) // Hey! Should not use GetRandomString on static instance
                // because it make output vary!
                return Guid.NewGuid().ToString();

            byte[] ret = new byte[8];
            cr.Random.NextBytes(ret);
            return Convert.ToBase64String(ret);
        }
        public string GetRandomName()
        {
            return GetNewName(GetRandomString());
        }
        public string GetRandomName(NameMode mode)
        {
            return GetNewName(GetRandomString(), mode);
        }

        string RenameUnreadable(string originalName)
        {
            BitArray arr = new BitArray(md5.ComputeHash(Encoding.UTF8.GetBytes(originalName)));

            Random rand = new Random(originalName.GetHashCode() * seed);
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
                while (m > 32)
                {
                    ret.Append((char)(m % 32 + 1));
                    m /= 32;
                }
            }
            return ret.ToString();
        }
        string RenameASCII(string originalName)
        {
            BitArray arr = new BitArray(md5.ComputeHash(Encoding.UTF8.GetBytes(originalName)));

            Random rand = new Random(originalName.GetHashCode() * seed);
            byte[] xorB = new byte[arr.Length / 8];
            rand.NextBytes(xorB);
            BitArray xor = new BitArray(xorB);

            BitArray result = arr.Xor(xor);
            byte[] ret = new byte[result.Length / 8];
            result.CopyTo(ret, 0);

            return Convert.ToBase64String(ret);
        }
        string RenameLetters(string originalName)
        {
            BitArray arr = new BitArray(md5.ComputeHash(Encoding.UTF8.GetBytes(originalName)));

            Random rand = new Random(originalName.GetHashCode() * seed);
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

        public RijndaelManaged CreateRijndael()
        {
            if (cr == null) // Hey again! Should not use CreateRijndaelManaged on static
                // instance because it make output vary!
                return null;
            RijndaelManaged ret = new RijndaelManaged();
            byte[] key = new byte[ret.KeySize / 8];
            cr.Random.NextBytes(key);
            ret.Key = key;
            byte[] iv = new byte[ret.BlockSize / 8];
            cr.Random.NextBytes(iv);
            ret.IV = iv;
            return ret;
        }
    }
}
