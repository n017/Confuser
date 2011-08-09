using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Confuser.Core.Poly;
using System.IO;
using Mono.Cecil.Cil;
using System.IO.Compression;
using Mono.Cecil.Rocks;
using Confuser.Core.Poly.Visitors;

namespace Confuser.Core.Confusions
{
    public class ConstantConfusion : IConfusion
    {
        class Phase1 : StructurePhase
        {
            public Phase1(ConstantConfusion cc) { this.cc = cc; }
            ConstantConfusion cc;
            public override IConfusion Confusion
            {
                get { return cc; }
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

            public override void Initialize(ModuleDefinition mod)
            {
                this.mod = mod;

                cc.dats = new List<Data>();
                cc.idx = 0;
                cc.dict = new Dictionary<object, int>();
            }

            public override void DeInitialize()
            {
                //
            }

            ModuleDefinition mod;
            public override void Process(ConfusionParameter parameter)
            {
                if (Array.IndexOf(parameter.GlobalParameters.AllKeys, "dynamic") == -1)
                {
                    ProcessSafe(parameter); return;
                }

                Random rand = new Random();
                TypeDefinition modType = mod.GetType("<Module>");

                AssemblyDefinition id = AssemblyDefinition.ReadAssembly(typeof(Iid).Assembly.Location);
                cc.strer = id.MainModule.GetType("Encryptions").Methods.FirstOrDefault(mtd => mtd.Name == "Constants");
                cc.strer = CecilHelper.Inject(mod, cc.strer);
                modType.Methods.Add(cc.strer);
                byte[] n = new byte[0x10]; rand.NextBytes(n);
                cc.strer.Name = Encoding.UTF8.GetString(n);
                cc.strer.IsAssembly = true;
                AddHelper(cc.strer, HelperAttribute.NoInjection);

                cc.key0 = (int)(rand.NextDouble() * int.MaxValue);
                cc.key1 = (int)(rand.NextDouble() * int.MaxValue);
                cc.key2 = (int)(rand.NextDouble() * int.MaxValue);
                cc.key3 = (int)(rand.NextDouble() * int.MaxValue);

                rand.NextBytes(n);
                byte[] dat = new byte[0x10];
                rand.NextBytes(dat);
                rand.NextBytes(cc.types);
                while (cc.types.Distinct().Count() != 5) rand.NextBytes(cc.types);

                cc.strer.Body.SimplifyMacros();
                foreach (Instruction inst in cc.strer.Body.Instructions)
                {
                    if ((inst.Operand as string) == "PADDINGPADDINGPADDING")
                        inst.Operand = Encoding.UTF8.GetString(n);
                    if ((inst.Operand as string) == "PADDINGPADDINGPADDINGPADDING")
                        inst.Operand = Encoding.UTF8.GetString(dat);
                    else if (inst.Operand is int && (int)inst.Operand == 12345678)
                        inst.Operand = cc.key0;
                    else if (inst.Operand is int && (int)inst.Operand == 0x67452301)
                        inst.Operand = cc.key1;
                    else if (inst.Operand is int && (int)inst.Operand == 0x3bd523a0)
                        inst.Operand = cc.key2;
                    else if (inst.Operand is int && (int)inst.Operand == 0x5f6f36c0)
                        inst.Operand = cc.key3;
                    else if (inst.Operand is int && (int)inst.Operand == 11)
                        inst.Operand = (int)cc.types[0];
                    else if (inst.Operand is int && (int)inst.Operand == 22)
                        inst.Operand = (int)cc.types[1];
                    else if (inst.Operand is int && (int)inst.Operand == 33)
                        inst.Operand = (int)cc.types[2];
                    else if (inst.Operand is int && (int)inst.Operand == 44)
                        inst.Operand = (int)cc.types[3];
                    else if (inst.Operand is int && (int)inst.Operand == 55)
                        inst.Operand = (int)cc.types[4];
                }

                cc.resId = Encoding.UTF8.GetString(n);
            }
            private void ProcessSafe(ConfusionParameter parameter)
            {
                Random rand = new Random();
                TypeDefinition modType = mod.GetType("<Module>");

                AssemblyDefinition i = AssemblyDefinition.ReadAssembly(typeof(Iid).Assembly.Location);
                cc.strer = i.MainModule.GetType("Encryptions").Methods.FirstOrDefault(mtd => mtd.Name == "SafeConstants");
                cc.strer = CecilHelper.Inject(mod, cc.strer);
                modType.Methods.Add(cc.strer);
                byte[] n = new byte[0x10]; rand.NextBytes(n);
                cc.strer.Name = Encoding.UTF8.GetString(n);
                cc.strer.IsAssembly = true;

                cc.key0 = rand.Next();
                cc.key1 = rand.Next();
                cc.key2 = rand.Next();
                cc.key3 = rand.Next();

                rand.NextBytes(n);
                byte[] dat = new byte[0x10];
                rand.NextBytes(dat);
                rand.NextBytes(cc.types);
                while (cc.types.Distinct().Count() != 5) rand.NextBytes(cc.types);

                cc.strer.Body.SimplifyMacros();
                foreach (Instruction inst in cc.strer.Body.Instructions)
                {
                    if ((inst.Operand as string) == "PADDINGPADDINGPADDING")
                        inst.Operand = Encoding.UTF8.GetString(n);
                    if ((inst.Operand as string) == "PADDINGPADDINGPADDINGPADDING")
                        inst.Operand = Encoding.UTF8.GetString(dat);
                    else if (inst.Operand is int && (int)inst.Operand == 12345678)
                        inst.Operand = cc.key0;
                    else if (inst.Operand is int && (int)inst.Operand == 0x67452301)
                        inst.Operand = cc.key1;
                    else if (inst.Operand is int && (int)inst.Operand == 0x3bd523a0)
                        inst.Operand = cc.key2;
                    else if (inst.Operand is int && (int)inst.Operand == 0x5f6f36c0)
                        inst.Operand = cc.key3;
                    else if (inst.Operand is int && (int)inst.Operand == 11)
                        inst.Operand = (int)cc.types[0];
                    else if (inst.Operand is int && (int)inst.Operand == 22)
                        inst.Operand = (int)cc.types[1];
                    else if (inst.Operand is int && (int)inst.Operand == 33)
                        inst.Operand = (int)cc.types[2];
                    else if (inst.Operand is int && (int)inst.Operand == 44)
                        inst.Operand = (int)cc.types[3];
                    else if (inst.Operand is int && (int)inst.Operand == 55)
                        inst.Operand = (int)cc.types[4];
                }
                cc.strer.Body.OptimizeMacros();
                cc.strer.Body.ComputeOffsets();

                cc.resId = Encoding.UTF8.GetString(n);
            }
        }
        class Phase3 : StructurePhase, IProgressProvider
        {
            public Phase3(ConstantConfusion cc) { this.cc = cc; }
            ConstantConfusion cc;
            public override IConfusion Confusion
            {
                get { return cc; }
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

            public override void Initialize(ModuleDefinition mod)
            {
                this.mod = mod;
            }

            public override void DeInitialize()
            {
                MemoryStream str = new MemoryStream();
                using (BinaryWriter wtr = new BinaryWriter(new DeflateStream(str, CompressionMode.Compress)))
                {
                    foreach (Data dat in cc.dats)
                    {
                        wtr.Write(dat.Type);
                        wtr.Write(dat.Dat.Length);
                        wtr.Write(dat.Dat);
                    }
                }
                mod.Resources.Add(new EmbeddedResource(cc.resId, ManifestResourceAttributes.Private, str.ToArray()));
            }

            struct Context { public MethodDefinition mtd; public ILProcessor psr; public Instruction str;}
            ModuleDefinition mod;
            bool IsNull(object obj)
            {
                if (obj is int)
                    return (int)obj == 0;
                else if (obj is long)
                    return (long)obj == 0;
                else if (obj is float)
                    return (float)obj == 0;
                else if (obj is double)
                    return (double)obj == 0;
                else if (obj is string)
                    return string.IsNullOrEmpty((string)obj);
                else
                    return true;
            }
            void ExtractData(IList<IAnnotationProvider> mtds, List<Context> txts, bool num)
            {
                foreach (MethodDefinition mtd in mtds)
                {
                    if (mtd == cc.strer || !mtd.HasBody) continue;
                    var bdy = mtd.Body;
                    bdy.SimplifyMacros();
                    var insts = bdy.Instructions;
                    ILProcessor psr = bdy.GetILProcessor();
                    bool hasDat = false;
                    for (int i = 0; i < insts.Count; i++)
                    {
                        if (insts[i].OpCode.Code == Code.Ldstr ||
                            (num && (insts[i].OpCode.Code == Code.Ldc_I4 ||
                            insts[i].OpCode.Code == Code.Ldc_I8 ||
                            insts[i].OpCode.Code == Code.Ldc_R4 ||
                            insts[i].OpCode.Code == Code.Ldc_R8)))
                        {
                            hasDat = true;
                            txts.Add(new Context() { mtd = mtd, psr = psr, str = insts[i] });
                        }
                    }
                    if (!hasDat) bdy.OptimizeMacros();
                }
            }
            byte[] GetOperand(object operand, out byte type)
            {
                byte[] ret;
                if (operand is double)
                {
                    ret = BitConverter.GetBytes((double)operand);
                    type = cc.types[0];
                }
                else if (operand is float)
                {
                    ret = BitConverter.GetBytes((float)operand);
                    type = cc.types[1];
                }
                else if (operand is int)
                {
                    ret = BitConverter.GetBytes((int)operand);
                    type = cc.types[2];
                }
                else if (operand is long)
                {
                    ret = BitConverter.GetBytes((long)operand);
                    type = cc.types[3];
                }
                else
                {
                    ret = Encoding.UTF8.GetBytes((string)operand);
                    type = cc.types[4];
                }
                return ret;
            }
            bool IsEqual(byte[] a, byte[] b)
            {
                int l = Math.Min(a.Length, b.Length);
                for (int i = 0; i < l; i++)
                    if (a[i] != b[i]) return false;
                return true;
            }
            void FinalizeBodies(List<Context> txts, int[] ids)
            {
                double total = txts.Count;
                int interval = 1;
                if (total > 1000)
                    interval = (int)total / 100;

                for (int i = 0; i < txts.Count; i++)
                {
                    int idx = txts[i].mtd.Body.Instructions.IndexOf(txts[i].str);
                    Instruction now = txts[i].str;
                    if (IsNull(now.Operand)) continue;
                    Instruction call = Instruction.Create(OpCodes.Call, cc.strer);
                    txts[i].psr.InsertAfter(idx, call);
                    if (now.Operand is int)
                        txts[i].psr.InsertAfter(call, Instruction.Create(OpCodes.Unbox_Any, txts[i].mtd.Module.TypeSystem.Int32));
                    else if (now.Operand is long)
                        txts[i].psr.InsertAfter(call, Instruction.Create(OpCodes.Unbox_Any, txts[i].mtd.Module.TypeSystem.Int64));
                    else if (now.Operand is float)
                        txts[i].psr.InsertAfter(call, Instruction.Create(OpCodes.Unbox_Any, txts[i].mtd.Module.TypeSystem.Single));
                    else if (now.Operand is double)
                        txts[i].psr.InsertAfter(call, Instruction.Create(OpCodes.Unbox_Any, txts[i].mtd.Module.TypeSystem.Double));
                    else
                        txts[i].psr.InsertAfter(call, Instruction.Create(OpCodes.Castclass, txts[i].mtd.Module.TypeSystem.String));
                    txts[i].psr.Replace(idx, Instruction.Create(OpCodes.Ldc_I4, ids[i]));
                    if (i % interval == 0 || i == txts.Count - 1)
                        progresser.SetProgress((i + 1) / txts.Count);
                }

                List<int> hashs = new List<int>();
                for (int i = 0; i < txts.Count; i++)
                {
                    if (hashs.IndexOf(txts[i].mtd.GetHashCode()) == -1)
                    {
                        txts[i].mtd.Body.OptimizeMacros();
                        txts[i].mtd.Body.ComputeHeader();
                        hashs.Add(txts[i].mtd.GetHashCode());
                    }
                }
            }

            public override void Process(ConfusionParameter parameter)
            {
                if (Array.IndexOf(parameter.GlobalParameters.AllKeys, "dynamic") == -1)
                {
                    ProcessSafe(parameter); return;
                }

                List<Context> txts = new List<Context>();
                ExtractData(parameter.Target as IList<IAnnotationProvider>, txts, Array.IndexOf(parameter.GlobalParameters.AllKeys, "numeric") != -1);

                int[] ids;
                bool retry;
                do
                {
                    ids = new int[txts.Count];
                    retry = false;
                    cc.dict.Clear();
                    int seed;
                    cc.exp = ExpressionGenerator.Generate(5, out seed);

                    for (int i = 0; i < txts.Count; i++)
                    {
                        object val = txts[i].str.Operand as object;
                        if (IsNull(val)) continue;

                        if (cc.dict.ContainsKey(val))
                            ids[i] = (int)(cc.dict[val] ^ ComputeHash(txts[i].mtd.MetadataToken.ToUInt32(), (uint)cc.key0, (uint)cc.key1, (uint)cc.key2, (uint)cc.key3));
                        else
                        {
                            ids[i] = (int)(cc.idx ^ ComputeHash(txts[i].mtd.MetadataToken.ToUInt32(), (uint)cc.key0, (uint)cc.key1, (uint)cc.key2, (uint)cc.key3));
                            byte t;
                            byte[] ori = GetOperand(val, out t);

                            int len;
                            byte[] dat = Encrypt(ori, cc.exp, out len);
                            try
                            {
                                if (!IsEqual(Decrypt(dat, len, cc.exp), ori))
                                {
                                    retry = true;
                                    break;
                                }
                            }
                            catch
                            {
                                retry = true;
                                break;
                            }
                            byte[] final = new byte[dat.Length + 4];
                            Buffer.BlockCopy(dat, 0, final, 4, dat.Length);
                            Buffer.BlockCopy(BitConverter.GetBytes(len), 0, final, 0, 4);
                            cc.dats.Add(new Data() { Dat = final, Type = t });
                            cc.dict[val] = cc.idx;
                            cc.idx += final.Length + 5;
                        }
                        System.Diagnostics.Debug.WriteLine(cc.dict[val].ToString() + "        " + val.ToString());
                    }
                } while (retry);

                for (int i = 0; i < cc.strer.Body.Instructions.Count; i++)
                {
                    Instruction inst = cc.strer.Body.Instructions[i];
                    if (inst.Operand is MethodReference && ((MethodReference)inst.Operand).Name == "PolyStart")
                    {
                        List<Instruction> insts = new List<Instruction>();
                        int ptr = i + 1;
                        while (ptr < cc.strer.Body.Instructions.Count)
                        {
                            Instruction z = cc.strer.Body.Instructions[ptr];
                            cc.strer.Body.Instructions.Remove(z);
                            if (z.Operand is MethodReference && ((MethodReference)z.Operand).Name == "PlaceHolder")
                                break;
                            insts.Add(z);
                        }

                        Instruction[] expInsts = new CecilVisitor(cc.exp, true, insts.ToArray(), false).GetInstructions();
                        ILProcessor psr = cc.strer.Body.GetILProcessor();
                        psr.Replace(inst, expInsts[0]);
                        for (int ii = 1; ii < expInsts.Length; ii++)
                        {
                            psr.InsertAfter(expInsts[ii - 1], expInsts[ii]);
                        }
                    }
                }
                cc.strer.Body.OptimizeMacros();
                cc.strer.Body.ComputeOffsets();

                FinalizeBodies(txts, ids);
            }
            void ProcessSafe(ConfusionParameter parameter)
            {
                List<Context> txts = new List<Context>();
                ExtractData(parameter.Target as IList<IAnnotationProvider>, txts, Array.IndexOf(parameter.GlobalParameters.AllKeys, "numeric") != -1);

                int[] ids = new int[txts.Count];
                for (int i = 0; i < txts.Count; i++)
                {
                    int idx = txts[i].mtd.Body.Instructions.IndexOf(txts[i].str);
                    object val = txts[i].str.Operand;
                    if (IsNull(val)) continue;

                    if (cc.dict.ContainsKey(val))
                        ids[i] = (int)(cc.dict[val] ^ ComputeHash(txts[i].mtd.MetadataToken.ToUInt32(), (uint)cc.key0, (uint)cc.key1, (uint)cc.key2, (uint)cc.key3));
                    else
                    {
                        byte t;
                        byte[] ori = GetOperand(val, out t);
                        byte[] dat = EncryptSafe(ori, cc.key0 ^ cc.idx);
                        ids[i] = (int)(cc.idx ^ ComputeHash(txts[i].mtd.MetadataToken.ToUInt32(), (uint)cc.key0, (uint)cc.key1, (uint)cc.key2, (uint)cc.key3));

                        cc.dats.Add(new Data() { Dat = dat, Type = t });
                        cc.dict[val] = cc.idx;
                        cc.idx += dat.Length + 5;
                    }
                }

                FinalizeBodies(txts, ids);
            }

            IProgresser progresser;
            public void SetProgresser(IProgresser progresser)
            {
                this.progresser = progresser;
            }
        }


        struct Data
        {
            public byte[] Dat;
            public byte Type;
        }
        List<Data> dats;
        Dictionary<object, int> dict;
        int idx = 0;

        string resId;
        int key0;
        int key1;
        int key2;
        int key3;
        byte[] types = new byte[5];
        MethodDefinition strer;

        Expression exp;

        public string ID
        {
            get { return "const encrypt"; }
        }
        public string Name
        {
            get { return "Constants Confusion"; }
        }
        public string Description
        {
            get { return "This confusion obfuscate the constants in the code and store them in a encrypted and compressed form."; }
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
        public bool SupportLateAddition
        {
            get { return true; }
        }
        public Behaviour Behaviour
        {
            get { return Behaviour.Inject | Behaviour.AlterCode | Behaviour.Encrypt; }
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

        private static byte[] Encrypt(byte[] bytes, Expression exp, out int len)
        {
            byte[] tmp = new byte[(bytes.Length + 7) & ~7];
            Buffer.BlockCopy(bytes, 0, tmp, 0, bytes.Length);
            len = bytes.Length;

            MemoryStream ret = new MemoryStream();
            using (BinaryWriter wtr = new BinaryWriter(ret))
            {
                for (int i = 0; i < tmp.Length; i++)
                {
                    int en = (int)LongExpressionEvaluator.Evaluate(exp, tmp[i]);
                    Write7BitEncodedInt(wtr, en);
                }
            }

            return ret.ToArray();
        }
        private static byte[] Decrypt(byte[] bytes, int len, Expression exp)
        {
            byte[] ret = new byte[(len + 7) & ~7];

            using (BinaryReader rdr = new BinaryReader(new MemoryStream(bytes)))
            {
                for (int i = 0; i < ret.Length; i++)
                {
                    int r = Read7BitEncodedInt(rdr);
                    ret[i] = (byte)LongExpressionEvaluator.ReverseEvaluate(exp, r);
                }
            }

            return ret;
        }
        private static byte[] EncryptSafe(byte[] bytes, int key)
        {
            Random rand = new Random(key);
            byte[] k = new byte[bytes.Length];
            rand.NextBytes(k);
            System.Collections.BitArray arr = new System.Collections.BitArray(bytes);
            arr.Xor(new System.Collections.BitArray(k));
            arr.CopyTo(k, 0);

            return k;
        }


        static uint ComputeHash(uint x, uint key, uint init0, uint init1, uint init2)
        {
            uint h = init0 ^ x;
            uint h1 = init1;
            uint h2 = init2;
            for (uint i = 1; i <= 64; i++)
            {
                h = (h & 0x00ffffff) << 8 | ((h & 0xff000000) >> 24);
                uint n = (h & 0xff) % 64;
                if (n >= 0 && n < 16)
                {
                    h1 |= (((h & 0x0000ff00) >> 8) & ((h & 0x00ff0000) >> 16)) ^ (~h & 0x000000ff);
                    h2 ^= (h * i + 1) % 16;
                    h += (h1 | h2) ^ key;
                }
                else if (n >= 16 && n < 32)
                {
                    h1 ^= ((h & 0x00ff00ff) << 8) ^ (((h & 0x00ffff00) >> 8) | (~h & 0x0000ffff));
                    h2 += (h * i) % 32;
                    h |= (h1 + ~h2) & key;
                }
                else if (n >= 32 && n < 48)
                {
                    h1 += ((h & 0x000000ff) | ((h & 0x00ff0000) >> 16)) + (~h & 0x000000ff);
                    h2 -= ~(h + n) % 48;
                    h ^= (h1 % h2) | key;
                }
                else if (n >= 48 && n < 64)
                {
                    h1 ^= (((h & 0x00ff0000) >> 16) | ~(h & 0x0000ff)) * (~h & 0x00ff0000);
                    h2 += (h ^ i - 1) % n;
                    h -= ~(h1 ^ h2) + key;
                }
            }
            return h;
        }
    }
}
