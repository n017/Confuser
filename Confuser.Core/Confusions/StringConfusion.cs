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
    public class StringConfusion : IConfusion
    {
        class Phase1 : StructurePhase
        {
            public Phase1(StringConfusion sc) { this.sc = sc; }
            StringConfusion sc;
            public override IConfusion Confusion
            {
                get { return sc; }
            }

            public override int PhaseID
            {
                get { return 1; }
            }

            public override Priority Priority
            {
                get { return Priority.CodeLevel; }
            }

            public override bool WholeRun
            {
                get { return true; }
            }

            public override void Initialize(AssemblyDefinition asm)
            {
                this.asm = asm;
            }

            public override void DeInitialize()
            {
                //
            }

            AssemblyDefinition asm;
            public override void Process(ConfusionParameter parameter)
            {
                sc.dats = new List<byte[]>();
                sc.idx = 0;

                Random rand = new Random();
                TypeDefinition mod = asm.MainModule.GetType("<Module>");

                AssemblyDefinition i = AssemblyDefinition.ReadAssembly(typeof(StringConfusion).Assembly.Location);
                sc.strer = i.MainModule.GetType("Confuser.Core.Confusions.StringConfusion").Methods.FirstOrDefault(mtd => mtd.Name == "Injection");
                sc.strer = CecilHelper.Inject(asm.MainModule, sc.strer);
                mod.Methods.Add(sc.strer);
                byte[] n = new byte[0x10]; rand.NextBytes(n);
                sc.strer.Name = Encoding.UTF8.GetString(n);
                sc.strer.IsAssembly = true;

                int seed;
                sc.exp = ExpressionGenerator.Generate(5, out seed);
                sc.eval = new ReflectionVisitor(sc.exp, false, false);
                sc.inver = new ReflectionVisitor(sc.exp, true, false);

                sc.key0 = (int)(rand.NextDouble() * int.MaxValue);
                sc.key1 = (int)(rand.NextDouble() * int.MaxValue);

                rand.NextBytes(n);

                MethodDefinition read7be = i.MainModule.GetType("Confuser.Core.Confusions.StringConfusion").Methods.FirstOrDefault(mtd => mtd.Name == "Read7BitEncodedInt");
                sc.strer.Body.SimplifyMacros();
                for (int t = 0; t < sc.strer.Body.Instructions.Count; t++)
                {
                    Instruction inst = sc.strer.Body.Instructions[t];
                    if ((inst.Operand as string) == "PADDINGPADDINGPADDING")
                        inst.Operand = Encoding.UTF8.GetString(n);
                    else if (inst.Operand is int && (int)inst.Operand == 12345678)
                        inst.Operand = sc.key0;
                    else if (inst.Operand is int && (int)inst.Operand == 87654321)
                        inst.Operand = sc.key1;
                    else if (inst.Operand is int && (int)inst.Operand == 123)
                    {
                        read7be.Body.SimplifyMacros();
                        ILProcessor read7bePsr = read7be.Body.GetILProcessor();
                        foreach (VariableDefinition var in read7be.Body.Variables)
                            sc.strer.Body.Variables.Add(var);
                        Instruction[] arg = new Instruction[read7be.Body.Instructions.Count];
                        for (int ii = 0; ii < arg.Length; ii++)
                        {
                            Instruction tmp = read7be.Body.Instructions[ii];
                            if (tmp.Operand is ParameterReference)
                            {
                                read7bePsr.Replace(tmp, Instruction.Create(OpCodes.Ldloc, sc.strer.Body.Variables.FirstOrDefault(var => var.VariableType.FullName == "System.IO.BinaryReader")));
                                tmp = read7be.Body.Instructions[ii];
                            }
                            else if (tmp.OpCode == OpCodes.Ret)
                            {
                                read7bePsr.Replace(tmp, Instruction.Create(OpCodes.Conv_I8));
                                tmp = read7be.Body.Instructions[ii];
                            }
                            else if (tmp.Operand is MethodReference)
                                tmp.Operand = asm.MainModule.Import(tmp.Operand as MethodReference);
                            arg[ii] = tmp;
                        }

                        Instruction[] expInsts = new CecilVisitor(sc.exp, true, arg, false).GetInstructions();
                        ILProcessor psr = sc.strer.Body.GetILProcessor();
                        psr.Replace(inst, expInsts[0]);
                        for (int ii = 1; ii < expInsts.Length; ii++)
                        {
                            psr.InsertAfter(expInsts[ii - 1], expInsts[ii]);
                            t++;
                        }
                        psr.InsertAfter(expInsts[expInsts.Length - 1], Instruction.Create(OpCodes.Conv_U1));
                    }
                }
                sc.strer.Body.OptimizeMacros();
                sc.strer.Body.ComputeOffsets();

                EmbeddedResource res = new EmbeddedResource(Encoding.UTF8.GetString(n), ManifestResourceAttributes.Private, (byte[])null);
                sc.resId = asm.MainModule.Resources.Count;
                asm.MainModule.Resources.Add(res);
            }
        }
        class Phase3 : StructurePhase
        {
            public Phase3(StringConfusion sc) { this.sc = sc; }
            StringConfusion sc;
            public override IConfusion Confusion
            {
                get { return sc; }
            }

            public override int PhaseID
            {
                get { return 3; }
            }

            public override Priority Priority
            {
                get { return Priority.Safe; }
            }

            public override bool WholeRun
            {
                get { return false; }
            }

            public override void Initialize(AssemblyDefinition asm)
            {
                this.asm = asm;
            }

            public override void DeInitialize()
            {
                MemoryStream str = new MemoryStream();
                using (BinaryWriter wtr = new BinaryWriter(new DeflateStream(str, CompressionMode.Compress)))
                {
                    foreach (byte[] b in sc.dats)
                        wtr.Write(b);
                }
                asm.MainModule.Resources[sc.resId] = new EmbeddedResource(asm.MainModule.Resources[sc.resId].Name, ManifestResourceAttributes.Private, str.ToArray());
            }

            AssemblyDefinition asm;
            public override void Process(ConfusionParameter parameter)
            {
                MethodDefinition mtd = parameter.Target as MethodDefinition;
                if (mtd == sc.strer || !mtd.HasBody) return;

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

                        int id = (int)((sc.idx + sc.key0) ^ mtd.MetadataToken.ToUInt32());
                        int len;
                        byte[] dat = StringConfusion.Encrypt(val, sc.eval, out len);
                        len = (int)~(len ^ sc.key1);

                        byte[] final = new byte[dat.Length + 4];
                        Buffer.BlockCopy(dat, 0, final, 4, dat.Length);
                        Buffer.BlockCopy(BitConverter.GetBytes(len), 0, final, 0, 4);
                        sc.dats.Add(final);
                        sc.idx += final.Length;

                        Instruction now = insts[i];
                        psr.InsertAfter(now, psr.Create(OpCodes.Call, sc.strer));
                        psr.Replace(now, psr.Create(OpCodes.Ldc_I4, id));
                    }
                }
                bdy.OptimizeMacros();
                bdy.ComputeHeader();

            }
        }


        List<byte[]> dats;
        int idx = 0;

        int resId;
        int key0;
        int key1;
        MethodDefinition strer;

        Expression exp;
        ReflectionVisitor eval;
        ReflectionVisitor inver;

        public string ID
        {
            get { return "string encrypt"; }
        }
        public string Name
        {
            get { return "User Strings Confusion"; }
        }
        public string Description
        {
            get { return "This confusion obfuscate the strings in the code and store them in a encrypted and compressed form."; }
        }
        public Target Target
        {
            get { return Target.Methods; }
        }
        public Preset Preset
        {
            get { return Preset.Minimum; }
        }
        public bool StandardCompatible
        {
            get { return true; }
        }

        Phase[] ps;
        public Phase[] Phases
        {
            get
            {
                if (ps == null)
                    ps = new Phase[] { new Phase1(this), new Phase3(this) };
                return ps;
            }
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
    }
}
