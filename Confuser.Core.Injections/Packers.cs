using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security;
using System.Text;

static class CompressShell
{
    static byte[] Decrypt(byte[] asm)
    {
        for (int i = 0; i < asm.Length; i++)
        {
            asm[i] = (byte)(asm[i] ^ i);
        }
        MemoryStream ret = new MemoryStream();
        DeflateStream str = new DeflateStream(new MemoryStream(asm), CompressionMode.Decompress);
        int c;
        byte[] b = new byte[0x100];
        while ((c = str.Read(b, 0, 0x100)) == 0x100) ret.Write(b, 0, 0x100);
        ret.Write(b, 0, c);

        return ret.ToArray();
    }

    static string Res = "fcc78551-8e82-4fd6-98dd-7ce4fcb0a59f";

    static int Main(string[] args)
    {
        Stream str = System.Reflection.Assembly.GetEntryAssembly().GetManifestResourceStream(Res);
        byte[] asmDat;
        using (BinaryReader rdr = new BinaryReader(str))
        {
            asmDat = rdr.ReadBytes((int)str.Length);
        }
        asmDat = Decrypt(asmDat);
        var asm = System.Reflection.Assembly.Load(asmDat);
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