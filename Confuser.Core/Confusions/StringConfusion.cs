using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using System.Collections;
using System.Security.Cryptography;
using System.Reflection;
using System.IO;
using System.Diagnostics;
using Mono.Cecil.Cil;
using System.IO.Compression;

namespace Confuser.Core.Confusions
{
    public class StringConfusion : StructureConfusion
    {
        public override string Name
        {
            get { return "User Strings Confusion"; }
        }
        public override Priority Priority
        {
            get { return Priority.CodeLevel; }
        }
        public override ProcessType Process
        {
            get { return ProcessType.Pre | ProcessType.Real; }
        }

        EmbeddedResource res;
        public override void PreConfuse(Confuser cr, AssemblyDefinition asm)
        {
            dats = new List<byte[]>();
            idx = 0;

            Random rand = new Random();
            TypeDefinition mod = asm.MainModule.Types["<Module>"];

            byte[] n = new byte[0x10]; rand.NextBytes(n);
            res = new EmbeddedResource(Encoding.UTF8.GetString(n), ManifestResourceAttributes.Private);
            asm.MainModule.Resources.Add(res);

            AssemblyDefinition i = AssemblyFactory.GetAssembly(typeof(StringConfusion).Assembly.Location);
            strer = i.MainModule.Types["Confuser.Core.Confusions.StringConfusion"].Methods.GetMethod("Injection")[0];
            strer = asm.MainModule.Inject(strer, mod);
            n = new byte[0x10]; rand.NextBytes(n);
            strer.Name = Encoding.UTF8.GetString(n);
            strer.IsAssembly = true;

            key0 = (int)(rand.NextDouble() * int.MaxValue);
            key1 = (int)(rand.NextDouble() * int.MaxValue);
        }

        public override void DoConfuse(Confuser cr, AssemblyDefinition asm)
        {
            foreach (TypeDefinition def in asm.MainModule.Types)
            {
                ProcessMethods(cr, def);
            }

            MemoryStream str = new MemoryStream();
            using (BinaryWriter wtr = new BinaryWriter(new DeflateStream(str, CompressionMode.Compress)))
            {
                foreach (byte[] b in dats)
                    wtr.Write(b);
            }

            foreach (Instruction inst in (strer.Body as ManagedMethodBody).Instructions)
            {
                if ((inst.Operand as string) == "PADDINGPADDINGPADDING")
                    inst.Operand = res.Name;
                else if (inst.Operand is int && (int)inst.Operand == 12345678)
                    inst.Operand = key0;
                else if (inst.Operand is int && (int)inst.Operand == 87654321)
                    inst.Operand = key1;
            } 
            res.Data = str.ToArray();
        }

        public override void PostConfuse(Confuser cr, AssemblyDefinition asm)
        {
            throw new InvalidOperationException();
        }

        List<byte[]> dats;
        int idx = 0;
        MethodDefinition strer;
        int key0; 
        int key1;

        private void ProcessMethods(Confuser cr, TypeDefinition def)
        {
            foreach (TypeDefinition t in def.NestedTypes)
            {
                ProcessMethods(cr, t);
            }
            foreach (MethodDefinition mtd in def.Constructors)
            {
                ProcessMethod(cr, mtd);
            }
            foreach (MethodDefinition mtd in def.Methods)
            {
                ProcessMethod(cr, mtd);
            }
        }
        private void ProcessMethod(Confuser cr, MethodDefinition mtd)
        {
            if (mtd == strer || !mtd.HasBody) return;

            ManagedMethodBody bdy = mtd.Body as ManagedMethodBody;
            InstructionCollection insts = bdy.Instructions;
            CilWorker wkr = bdy.CilWorker;
            for (int i = 0; i < insts.Count; i++)
            {
                if (insts[i].OpCode.Code == Code.Ldstr)
                {
                    string val = insts[i].Operand as string;
                    if (val == "") continue;
                    byte[] dat = Encrypt(val, mtd.MetadataToken.ToUInt());

                    int id = (int)((idx + key0) ^ mtd.MetadataToken.ToUInt());
                    int len = (int)~(dat.Length ^ key1);

                    byte[] final = new byte[dat.Length + 4];
                    Buffer.BlockCopy(dat, 0, final, 4, dat.Length);
                    Buffer.BlockCopy(BitConverter.GetBytes(len), 0, final, 0, 4);
                    dats.Add(final);
                    idx += final.Length;
                    cr.Log("<string value='" + val + "'/>");

                    Instruction now = insts[i];
                    wkr.InsertBefore(now, wkr.Create(OpCodes.Ldc_I4, id));
                    wkr.Replace(now, wkr.Create(OpCodes.Call, strer));
                }
            }
        }

        private static byte[] Encrypt(string str, uint mdToken)
        {
            Random rand = new Random((int)mdToken);
            byte[] bs = Encoding.UTF8.GetBytes(str);

            int key = 0;
            for (int i = 0; i < bs.Length; i++)
            {
                bs[i] = (byte)(bs[i] ^ (rand.Next() & key));
                key += bs[i];
            }

            return bs;
        }
        private static string Decrypt(byte[] bytes, uint mdToken)
        {
            Random rand = new Random((int)mdToken);

            int key = 0;
            for (int i = 0; i < bytes.Length; i++)
            {
                byte o = bytes[i];
                bytes[i] = (byte)(bytes[i] ^ (rand.Next() & key));
                key += o;
            }
            return Encoding.UTF8.GetString(bytes);
        }
        private static string Injection(int id)
        {
            Assembly asm = Assembly.GetCallingAssembly();
            Stream str = asm.GetManifestResourceStream("PADDINGPADDINGPADDING");
            int mdTkn = new StackFrame(1).GetMethod().MetadataToken;
            using (BinaryReader rdr = new BinaryReader(new DeflateStream(str, CompressionMode.Decompress)))
            {
                rdr.ReadBytes((mdTkn ^ id) - 12345678);
                int len = (int)((~rdr.ReadUInt32()) ^ 87654321);
                byte[] b = rdr.ReadBytes(len);

                ///////////////////
                Random rand = new Random((int)mdTkn);

                int key = 0;
                for (int i = 0; i < b.Length; i++)
                {
                    byte o = b[i];
                    b[i] = (byte)(b[i] ^ (rand.Next() & key));
                    key += o;
                }
                return Encoding.UTF8.GetString(b);
                ///////////////////
            }
        }

        public override bool StandardCompatible
        {
            get { return true; }
        }
    }
}
