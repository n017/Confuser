using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using System.Collections;
using System.Security.Cryptography;
using System.IO;
using System.Diagnostics;
using System.IO.Compression;
using Confuser.Core.Poly;
using Confuser.Core.Poly.Visitors;

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
        public override Phases Phases
        {
            get { return Phases.Phase1 | Phases.Phase3; }
        }

        public override void Confuse(int phase, Confuser cr, AssemblyDefinition asm, IMemberDefinition[] defs)
        {
            switch (phase)
            {
                case 1:
                    dats = new List<byte[]>();
                    idx = 0;

                    Random rand = new Random();
                    TypeDefinition mod = asm.MainModule.GetType("<Module>");

                    AssemblyDefinition i = AssemblyDefinition.ReadAssembly(typeof(StringConfusion).Assembly.Location);
                    strer = i.MainModule.GetType("Confuser.Core.Confusions.StringConfusion").Methods.FirstOrDefault(mtd => mtd.Name == "Injection");
                    strer = CecilHelper.Inject(asm.MainModule, strer);
                    mod.Methods.Add(strer);
                    byte[] n = new byte[0x10]; rand.NextBytes(n);
                    strer.Name = Encoding.UTF8.GetString(n);
                    strer.IsAssembly = true;

                    int seed;
                    exp = ExpressionGenerator.Generate(5, out seed);
                    eval = new ReflectionVisitor(exp, false, false);
                    inver = new ReflectionVisitor(exp, true, false);

                    key0 = (int)(rand.NextDouble() * int.MaxValue);
                    key1 = (int)(rand.NextDouble() * int.MaxValue);

                    rand.NextBytes(n);

                    MethodDefinition read7be = i.MainModule.GetType("Confuser.Core.Confusions.StringConfusion").Methods.FirstOrDefault(mtd => mtd.Name == "Read7BitEncodedInt");
                    strer.Body.SimplifyMacros();
                    for (int t = 0; t < strer.Body.Instructions.Count; t++)
                    {
                        Instruction inst = strer.Body.Instructions[t];
                        if ((inst.Operand as string) == "PADDINGPADDINGPADDING")
                            inst.Operand = Encoding.UTF8.GetString(n);
                        else if (inst.Operand is int && (int)inst.Operand == 12345678)
                            inst.Operand = key0;
                        else if (inst.Operand is int && (int)inst.Operand == 87654321)
                            inst.Operand = key1;
                        else if (inst.Operand is int && (int)inst.Operand == 123)
                        {
                            read7be.Body.SimplifyMacros();
                            ILProcessor read7bePsr = read7be.Body.GetILProcessor();
                            foreach (VariableDefinition var in read7be.Body.Variables)
                                strer.Body.Variables.Add(var);
                            Instruction[] arg = new Instruction[read7be.Body.Instructions.Count];
                            for (int ii = 0; ii < arg.Length; ii++)
                            {
                                Instruction tmp = read7be.Body.Instructions[ii];
                                if (tmp.Operand is ParameterReference)
                                {
                                    read7bePsr.Replace(tmp, Instruction.Create(OpCodes.Ldloc, strer.Body.Variables.FirstOrDefault(var => var.VariableType.FullName == "System.IO.BinaryReader")));
                                    tmp = tmp = read7be.Body.Instructions[ii];
                                }
                                else if (tmp.OpCode == OpCodes.Ret)
                                {
                                    read7bePsr.Replace(tmp, Instruction.Create(OpCodes.Conv_I8));
                                    tmp = tmp = read7be.Body.Instructions[ii];
                                }
                                arg[ii] = tmp;
                            }

                            Instruction[] expInsts = new CecilVisitor(exp, true, arg, false).GetInstructions();
                            ILProcessor psr = strer.Body.GetILProcessor();
                            psr.Replace(inst, expInsts[0]);
                            for (int ii = 1; ii < expInsts.Length; ii++)
                            {
                                psr.InsertAfter(expInsts[ii - 1], expInsts[ii]);
                                t++;
                            }
                            psr.InsertAfter(expInsts[expInsts.Length - 1], Instruction.Create(OpCodes.Conv_U1));
                        }
                    }
                    strer.Body.OptimizeMacros();
                    strer.Body.ComputeOffsets();

                    EmbeddedResource res = new EmbeddedResource(Encoding.UTF8.GetString(n), ManifestResourceAttributes.Private, (byte[])null);
                    resId = asm.MainModule.Resources.Count;
                    asm.MainModule.Resources.Add(res);
                    break;
                case 3:
                    foreach (MethodDefinition mtd in defs)
                    {
                        ProcessMethod(cr, mtd);
                    }

                    MemoryStream str = new MemoryStream();
                    using (BinaryWriter wtr = new BinaryWriter(new DeflateStream(str, CompressionMode.Compress)))
                    {
                        foreach (byte[] b in dats)
                            wtr.Write(b);
                    }
                    asm.MainModule.Resources[resId] = new EmbeddedResource(asm.MainModule.Resources[resId].Name, ManifestResourceAttributes.Private, str.ToArray());
                    break;
                default: throw new InvalidOperationException();
            }
        }

        Expression exp;
        ReflectionVisitor eval;
        ReflectionVisitor inver;
        List<byte[]> dats;
        int idx = 0;
        int resId;
        MethodDefinition strer;
        int key0; 
        int key1;

        private void ProcessMethod(Confuser cr, MethodDefinition mtd)
        {
            if (mtd == strer || !mtd.HasBody) return;

            var bdy = mtd.Body;
            bdy.SimplifyMacros();
            var insts = bdy.Instructions;
            ILProcessor psr = bdy.GetILProcessor();
            for (int i = 0; i < insts.Count; i++)
            {
                if (insts[i].OpCode.Code == Code.Ldstr)
                {
                    string val = insts[i].Operand as string;
                    if (val == "") continue;

                    int id = (int)((idx + key0) ^ mtd.MetadataToken.ToUInt32());
                    int len;
                    byte[] dat = Encrypt(val, eval, out len);
                    len = (int)~(len ^ key1);

                    byte[] final = new byte[dat.Length + 4];
                    Buffer.BlockCopy(dat, 0, final, 4, dat.Length);
                    Buffer.BlockCopy(BitConverter.GetBytes(len), 0, final, 0, 4);
                    dats.Add(final);
                    idx += final.Length;

                    Instruction now = insts[i];
                    psr.InsertAfter(now, psr.Create(OpCodes.Call, strer));
                    psr.Replace(now, psr.Create(OpCodes.Ldc_I4, id));
                }
            }
            bdy.OptimizeMacros();
            bdy.ComputeHeader();
        }

        private static byte[] Encrypt(string str, ReflectionVisitor expEval, out int len)
        {
            byte[] bs = Encoding.Unicode.GetBytes(str);
            byte[] tmp = new byte[(bs.Length + 7) & ~7];
            Buffer.BlockCopy(bs, 0, tmp, 0, bs.Length);
            len = bs.Length;

            MemoryStream ret = new MemoryStream();
            using (BinaryWriter wtr = new BinaryWriter(ret))
            {
                for (int i = 0; i < tmp.Length; i++)
                {
                    int en = (int)(long)expEval.Eval((long)tmp[i]);
                    Write7BitEncodedInt(wtr, en);
                }
            }

            return ret.ToArray();
        }
        private static string Decrypt(byte[] bytes, int len, ReflectionVisitor expEval)
        {
            byte[] ret = new byte[(len + 7) & ~7];

            using (BinaryReader rdr = new BinaryReader(new MemoryStream(bytes)))
            {
                for (int i = 0; i < ret.Length; i++)
                {
                    int r = Read7BitEncodedInt(rdr);
                    ret[i] = (byte)(long)expEval.Eval((long)r);
                }
            }
            Debug.WriteLine("");

            return Encoding.Unicode.GetString(ret, 0, len);
        }
        static void Write7BitEncodedInt(BinaryWriter wtr, int value)
        {
            // Write out an int 7 bits at a time. The high bit of the byte,
            // when on, tells reader to continue reading more bytes.
            uint v = (uint)value; // support negative numbers
            while (v >= 0x80)
            {
                wtr.Write((byte)(v | 0x80));
                v >>= 7;
            }
            wtr.Write((byte)v);
        }

        static int Read7BitEncodedInt(BinaryReader rdr)
        {
            // Read out an int 7 bits at a time. The high bit
            // of the byte when on means to continue reading more bytes.
            int count = 0;
            int shift = 0;
            byte b;
            do
            {
                b = rdr.ReadByte();
                count |= (b & 0x7F) << shift;
                shift += 7;
            } while ((b & 0x80) != 0);
            return count;
        }

        private static string Injection(int id)
        {
            Dictionary<int, string> hashTbl;
            if ((hashTbl = AppDomain.CurrentDomain.GetData("PADDINGPADDINGPADDING") as Dictionary<int, string>) == null)
                AppDomain.CurrentDomain.SetData("PADDINGPADDINGPADDING", hashTbl = new Dictionary<int, string>());
            string ret;
            if (!hashTbl.TryGetValue(id, out ret))
            {
                System.Reflection.Assembly asm = System.Reflection.Assembly.GetCallingAssembly();
                Stream str = asm.GetManifestResourceStream("PADDINGPADDINGPADDING");
                int mdTkn = new StackFrame(1).GetMethod().MetadataToken;
                using (BinaryReader rdr = new BinaryReader(new DeflateStream(str, CompressionMode.Decompress)))
                {
                    rdr.ReadBytes((mdTkn ^ id) - 12345678);
                    int len = (int)((~rdr.ReadUInt32()) ^ 87654321);

                    ///////////////////
                    byte[] f = new byte[(len + 7) & ~7];

                    for (int i = 0; i < f.Length; i++)
                    {
                        f[i] = 123;
                    }

                    hashTbl[id] = (ret = Encoding.Unicode.GetString(f, 0, len));
                    ///////////////////
                }
            }
            return ret;
        }

        public override bool StandardCompatible
        {
            get { return true; }
        }

        public override string Description
        {
            get { return "This confusion obfuscate the strings in the code and store them in a encrypted and compressed form."; }
        }

        public override Target Target
        {
            get { return Target.Methods; }
        }
    }
}
