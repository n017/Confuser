using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using System.IO;
using System.IO.Compression;
using Mono.Cecil.Cil;
using Confuser.Core.Confusions;
using System.Diagnostics;
using System.Security;
using System.Security.Permissions;

namespace Confuser.Core
{
    static class Compressor
    {
        public static void Compress(Confuser cr, byte[] asm, AssemblyDefinition o, string dst)
        {
            if (o.Kind == AssemblyKind.Dll)
            {
                File.WriteAllBytes(dst, asm);
                return;
            }
            AssemblyDefinition ldr = AssemblyFactory.DefineAssembly("ConfusingLoader", "ConfusingLoader", o.Runtime, o.Kind);
            ldr.MainModule.Image.ResourceDirectoryRoot = o.MainModule.Image.ResourceDirectoryRoot;
            foreach (Mono.Cecil.Binary.Section sect in o.MainModule.Image.Sections)
                if (sect.Name == ".rsrc")
                {
                    ldr.MainModule.Image.Sections.Add(sect);
                    break;
                }

            EmbeddedResource res = new EmbeddedResource(o.Name.Name, ManifestResourceAttributes.Public);
            res.Data = Encrypt(asm);
            ldr.MainModule.Resources.Add(res);

            AssemblyDefinition ldrC = AssemblyFactory.GetAssembly(typeof(ConfusingLoader).Assembly.Location);
            TypeDefinition t = ldr.MainModule.Inject(ldrC.MainModule.Types["Confuser.Core.Compressor/ConfusingLoader"]);
            foreach (Instruction inst in t.Methods.GetMethod("Main")[0].Body.Instructions)
            {
                if ((inst.Operand is MethodReference) && (inst.Operand as MethodReference).Name == "Decrypt")
                    inst.Operand = t.Methods.GetMethod("Decrypt")[0];
                else if ((inst.Operand is FieldReference) && (inst.Operand as FieldReference).Name == "Res")
                    inst.Operand = t.Fields.GetField("Res");
            }
            foreach (Instruction inst in t.Constructors[0].Body.Instructions)
            {
                if ((inst.Operand is FieldReference) && (inst.Operand as FieldReference).Name == "Res")
                    inst.Operand = t.Fields.GetField("Res");
                else if (inst.Operand is string)
                    inst.Operand = res.Name;
            }
            t.Namespace = "";
            t.DeclaringType = null;
            t.IsNestedPrivate = false;
            t.IsNotPublic = true;
            ldr.EntryPoint = t.Methods.GetMethod("Main")[0];

            string tmp = Path.GetTempFileName();
            AssemblyFactory.SaveAssembly(ldr, tmp);

            cr.ScreenLog("<compress/>");

            Confuser ncr = new Confuser();
            ncr.Confusions.Add(new AntiILDasmConfusion());
            ncr.Confusions.Add(new MdStreamConfusion());
            ncr.Confusions.Add(new StringConfusion());
            ncr.Confusions.Add(new NameConfusion());
            ncr.Confuse(tmp, dst);
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
                string tmp = Path.GetTempPath() + "\\" + Res + ".exe";
                File.WriteAllBytes(tmp, asmDat);
                File.SetAttributes(tmp, System.IO.FileAttributes.Temporary | System.IO.FileAttributes.Hidden);
                string arg = "";
                foreach (string i in args)
                    arg += i + " ";
                Process ps = Process.Start(tmp, arg);
                ps.WaitForExit();
                int ret = ps.ExitCode;
                File.SetAttributes(tmp, System.IO.FileAttributes.Temporary | System.IO.FileAttributes.Hidden);
                File.Delete(tmp);
                return ret;
            }
        }

    }
}
