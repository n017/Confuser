using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security;
using System.Text;
using System.Reflection;
using System.Collections;
using System.Security.Cryptography;

static class CompressShell
{
    public static Assembly DecryptAsm(object sender, ResolveEventArgs e)
    {
        byte[] b = Encoding.UTF8.GetBytes(e.Name);
        for (int i = 0; i < b.Length; i++)
            b[i] = (byte)(b[i] ^ 0x12345678 ^ i);
        string resName = Encoding.UTF8.GetString(b);
        Stream str = typeof(CompressShell).Assembly.GetManifestResourceStream(resName);
        if (str != null)
        {
            byte[] asmDat;
            using (BinaryReader rdr = new BinaryReader(str))
            {
                asmDat = rdr.ReadBytes((int)str.Length);
            }
            asmDat = Decrypt(asmDat);
            var asm = Assembly.Load(asmDat);
            byte[] over = new byte[asmDat.Length];
            Buffer.BlockCopy(over, 0, asmDat, 0, asmDat.Length);

            return asm;
        }
        return null;
    }

    static byte[] Decrypt(byte[] asm)
    {
        byte[] dat;
        DeflateStream str = new DeflateStream(new MemoryStream(asm), CompressionMode.Decompress);
        byte[] iv;
        byte[] key;
        using (BinaryReader rdr = new BinaryReader(str))
        {
            dat = rdr.ReadBytes(rdr.ReadInt32());
            iv = rdr.ReadBytes(rdr.ReadInt32());
            key = rdr.ReadBytes(rdr.ReadInt32());
        }
        int key0 = 0x12345678;
        for (int j = 0; j < key.Length; j += 4)
        {
            key[j + 0] ^= (byte)((key0 & 0x000000ff) >> 0);
            key[j + 1] ^= (byte)((key0 & 0x0000ff00) >> 8);
            key[j + 2] ^= (byte)((key0 & 0x00ff0000) >> 16);
            key[j + 3] ^= (byte)((key0 & 0xff000000) >> 24);
        }
        RijndaelManaged rijn = new RijndaelManaged();
        using (CryptoStream s = new CryptoStream(new MemoryStream(dat), rijn.CreateDecryptor(key, iv), CryptoStreamMode.Read))
        {
            byte[] l = new byte[4];
            s.Read(l, 0, 4);
            byte[] ret = new byte[BitConverter.ToUInt32(l, 0)];
            byte[] buff = new byte[0x1000];
            int len = buff.Length;
            int idx = 0;
            while (len == buff.Length)
            {
                len = s.Read(buff, 0, buff.Length);
                Buffer.BlockCopy(buff, 0, ret, idx, len);
                idx += len;
            }
            return ret;
        }
    }

    static string Res = "fcc78551-8e82-4fd6-98dd-7ce4fcb0a59f";

    [STAThread]
    static int Main(string[] args)
    {
        Stream str = Assembly.GetEntryAssembly().GetManifestResourceStream(Res);
        byte[] asmDat;
        using (BinaryReader rdr = new BinaryReader(str))
        {
            asmDat = rdr.ReadBytes((int)str.Length);
        }
        asmDat = Decrypt(asmDat);
        var asm = Assembly.Load(asmDat);
        byte[] over = new byte[asmDat.Length];
        Buffer.BlockCopy(over, 0, asmDat, 0, asmDat.Length);
        object ret;
        if (asm.EntryPoint.GetParameters().Length == 1)
            ret = asm.EntryPoint.Invoke(null, new object[] { args });
        else
            ret = asm.EntryPoint.Invoke(null, null);
        if (ret is int)
            return (int)ret;
        else
            return 0;
    }
}