using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security;
using System.Text;
using System.Reflection;

static class CompressShell
{
    static byte[] Decrypt(byte[] asm)
    {
        for (int i = 0; i < asm.Length; i++)
        {
            asm[i] = (byte)(asm[i] ^ i ^ 0x12345678);
        }
        DeflateStream str = new DeflateStream(new MemoryStream(asm), CompressionMode.Decompress);
        using (BinaryReader rdr = new BinaryReader(str))
        {
            byte[] ret = new byte[rdr.ReadInt32()];
            byte[] over = new byte[0x100];
            int i;
            for (i = 0; i + 0x100 < ret.Length; i += 0x100)
            {
                byte[] b = rdr.ReadBytes(0x100);
                Buffer.BlockCopy(b, 0, ret, i, 0x100);
                Buffer.BlockCopy(over, 0, b, 0, 0x100);
            }
            if (i != ret.Length)
            {
                int re = ret.Length - i;
                byte[] b = rdr.ReadBytes(re);
                Buffer.BlockCopy(b, 0, ret, i, re);
                Buffer.BlockCopy(over, 0, b, 0, re);
            }
            return ret;
        }
    }

    static string Res = "fcc78551-8e82-4fd6-98dd-7ce4fcb0a59f";

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
        GC.Collect();
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