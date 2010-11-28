using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security;
using System.Text;
using System.Reflection;
using System.Collections;

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
        byte[] ret;
        DeflateStream str = new DeflateStream(new MemoryStream(asm), CompressionMode.Decompress);
        using (BinaryReader rdr = new BinaryReader(str))
        {
            ret = rdr.ReadBytes(rdr.ReadInt32());
        }
        int key = 0x12345678;
        for (int i = 0; i < ret.Length; i++)
        {
            ret[i] = (byte)((ret[i] ^ (i % 2 == 0 ? (key & 0xf) - i : (((int)key >> 4) + i))) - i);
        }
        return ret;
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