using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using System.IO;
using System.IO.Compression;
using System.Diagnostics;
using System.Security;
using System.Security.Permissions;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace Confuser.Core
{
    static class ConfusingLoader
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
            new PermissionSet(PermissionState.Unrestricted).Demand();
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

    static class Compressor
    {
        public static void Compress(Confuser cr, byte[] asm, AssemblyDefinition o, Stream dst)
        {
            if (o.MainModule.Kind == ModuleKind.Dll)
            {
                dst.Write(asm, 0, asm.Length);
                return;
            }
            AssemblyDefinition ldr = AssemblyDefinition.CreateAssembly(new AssemblyNameDefinition("ConfusingLoader_" + o.Name.Name, o.Name.Version), "ConfusingLoader", ModuleKind.Windows);

            EmbeddedResource res = new EmbeddedResource(Encoding.UTF8.GetString(Guid.NewGuid().ToByteArray()), ManifestResourceAttributes.Public, Encrypt(asm));
            ldr.MainModule.Resources.Add(res);

            AssemblyDefinition ldrC = AssemblyDefinition.ReadAssembly(typeof(ConfusingLoader).Assembly.Location);
            TypeDefinition t = CecilHelper.Inject(ldr.MainModule, ldrC.MainModule.GetType(typeof(ConfusingLoader).FullName));
            foreach (Instruction inst in t.GetStaticConstructor().Body.Instructions)
                if (inst.Operand is string)
                    inst.Operand = res.Name;
            t.Namespace = "";
            t.DeclaringType = null;
            t.IsNestedPrivate = false;
            t.IsNotPublic = true;
            ldr.MainModule.Types.Add(t);
            ldr.EntryPoint = t.Methods.FirstOrDefault(mtd => mtd.Name == "Main");

            ldr.Write(dst);
        }

        static byte[] Encrypt(byte[] asm)
        {
            MemoryStream str = new MemoryStream();
            using (BinaryWriter wtr = new BinaryWriter(new DeflateStream(str, CompressionMode.Compress)))
            {
                wtr.Write(asm);
            }
            byte[] ret = str.ToArray();
            for (int i = 0; i < ret.Length; i++)
            {
                ret[i] = (byte)(ret[i] ^ i);
            }
            return ret;
        }
    }
}
