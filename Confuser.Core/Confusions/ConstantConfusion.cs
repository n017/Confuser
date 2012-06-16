using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Confuser.Core.Poly;
using System.IO;
using Mono.Cecil.Cil;
using System.IO.Compression;
using Confuser.Core.Poly.Visitors;
using System.Collections.Specialized;
using Mono.Cecil.Metadata;

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
                cc.txts[mod] = new _Context();

                cc.txts[mod].dats = new List<Data>();
                cc.txts[mod].idx = 0;
                cc.txts[mod].dict = new Dictionary<object, int>();
            }

            public override void DeInitialize()
            {
                //
            }

            ModuleDefinition mod;
            public override void Process(ConfusionParameter parameter)
            {
                Database.AddEntry("Const", "Type", parameter.GlobalParameters["type"] ?? "normal");
                if (parameter.GlobalParameters["type"] != "dynamic" &&
                    parameter.GlobalParameters["type"] != "native")
                {
                    ProcessSafe(parameter); return;
                }
                _Context txt = cc.txts[mod];
                txt.isNative = parameter.GlobalParameters["type"] == "native";

                TypeDefinition modType = mod.GetType("<Module>");

                FieldDefinition constTbl = new FieldDefinition(
                    ObfuscationHelper.GetRandomName(),
                    FieldAttributes.Static | FieldAttributes.CompilerControlled,
                    mod.Import(typeof(Dictionary<uint, object>)));
                modType.Fields.Add(constTbl);
                AddHelper(constTbl, HelperAttribute.NoInjection);

                Database.AddEntry("Const", "ConstTbl", constTbl.FullName);

                FieldDefinition constStream = new FieldDefinition(
                    ObfuscationHelper.GetRandomName(),
                    FieldAttributes.Static | FieldAttributes.CompilerControlled,
                    mod.Import(typeof(Stream)));
                modType.Fields.Add(constStream);
                AddHelper(constStream, HelperAttribute.NoInjection);
                Database.AddEntry("Const", "ConstStream", constStream.FullName);

                txt.consters = CreateConsters(txt, Confuser.Random, "Constants", constTbl, constStream);


                if (txt.isNative)
                {
                    txt.nativeDecr = new MethodDefinition(
                        ObfuscationHelper.GetRandomName(),
                        MethodAttributes.Abstract | MethodAttributes.CompilerControlled |
                        MethodAttributes.ReuseSlot | MethodAttributes.Static,
                        mod.TypeSystem.Int32);
                    txt.nativeDecr.ImplAttributes = MethodImplAttributes.Native;
                    txt.nativeDecr.Parameters.Add(new ParameterDefinition(mod.TypeSystem.Int32));
                    modType.Methods.Add(txt.nativeDecr);
                    Database.AddEntry("Const", "NativeDecr", txt.nativeDecr.FullName);
                }

                var expGen = new ExpressionGenerator(Random.Next());
                int seed = expGen.Seed;
                if (txt.isNative)
                {
                    do
                    {
                        txt.exp = new ExpressionGenerator(Random.Next()).Generate(6);
                        txt.invExp = ExpressionInverser.InverseExpression(txt.exp);
                    } while ((txt.visitor = new x86Visitor(txt.invExp, null)).RegisterOverflowed);
                }
                else
                {
                    txt.exp = expGen.Generate(10);
                    txt.invExp = ExpressionInverser.InverseExpression(txt.exp);
                }
                Database.AddEntry("Const", "Exp", txt.exp);
                Database.AddEntry("Const", "InvExp", txt.invExp);
            }
            private void ProcessSafe(ConfusionParameter parameter)
            {
                _Context txt = cc.txts[mod];

                TypeDefinition modType = mod.GetType("<Module>");

                FieldDefinition constTbl = new FieldDefinition(
                    ObfuscationHelper.GetRandomName(),
                    FieldAttributes.Static | FieldAttributes.CompilerControlled,
                    mod.Import(typeof(Dictionary<uint, object>)));
                modType.Fields.Add(constTbl);
                AddHelper(constTbl, HelperAttribute.NoInjection);
                Database.AddEntry("Const", "ConstTbl", constTbl.FullName);

                FieldDefinition constStream = new FieldDefinition(
                    ObfuscationHelper.GetRandomName(),
                    FieldAttributes.Static | FieldAttributes.CompilerControlled,
                    mod.Import(typeof(Stream)));
                modType.Fields.Add(constStream);
                AddHelper(constStream, HelperAttribute.NoInjection);
                Database.AddEntry("Const", "constStream", constStream.FullName);

                txt.consters = CreateConsters(txt, Random, "SafeConstants", constTbl, constStream);
            }
            Conster[] CreateConsters(_Context txt, Random rand, string injectName,
                                     FieldDefinition constTbl, FieldDefinition constStream)
            {
                AssemblyDefinition injection = AssemblyDefinition.ReadAssembly(typeof(Iid).Assembly.Location);
                MethodDefinition method = injection.MainModule.GetType("Encryptions").Methods.FirstOrDefault(mtd => mtd.Name == injectName);
                List<Conster> ret = new List<Conster>();

                rand.NextBytes(txt.types);
                rand.NextBytes(txt.keyBuff);
                for (int i = 0; i < txt.keyBuff.Length; i++)
                    txt.keyBuff[i] &= 0x7f;
                txt.keyBuff[0] = 7; txt.keyBuff[1] = 0;
                while (txt.types.Distinct().Count() != 5) rand.NextBytes(txt.types);
                txt.resKey = rand.Next();
                txt.resId = Encoding.UTF8.GetString(BitConverter.GetBytes(txt.resKey));
                txt.key = rand.Next();

                Database.AddEntry("Const", "KeyTypes", txt.types);
                Database.AddEntry("Const", "KeyBuff", txt.keyBuff);
                Database.AddEntry("Const", "ResKey", txt.resKey);
                Database.AddEntry("Const", "ResId", txt.resId);
                Database.AddEntry("Const", "Key", txt.key);


                MethodDefinition init = injection.MainModule.GetType("Encryptions").Methods.FirstOrDefault(mtd => mtd.Name == "Initialize");
                {
                    MethodDefinition cctor = mod.GetType("<Module>").GetStaticConstructor();
                    MethodDefinition m = CecilHelper.Inject(mod, init);
                    m.Body.SimplifyMacros();
                    foreach (Instruction inst in m.Body.Instructions)
                    {
                        if (inst.Operand is int && (int)inst.Operand == 0x12345678)
                            inst.Operand = txt.resKey;
                        else if (inst.Operand is FieldReference)
                        {
                            if ((inst.Operand as FieldReference).Name == "constTbl")
                                inst.Operand = constTbl;
                            else if ((inst.Operand as FieldReference).Name == "constStream")
                                inst.Operand = constStream;
                        }
                    }
                    ILProcessor psr = cctor.Body.GetILProcessor();
                    Instruction begin = cctor.Body.Instructions[0];
                    for (int i = m.Body.Instructions.Count - 1; i >= 0; i--)
                    {
                        if (m.Body.Instructions[i].OpCode != OpCodes.Ret)
                            psr.InsertBefore(0, m.Body.Instructions[i]);
                    }
                    cctor.Body.InitLocals = true;
                    foreach (var i in m.Body.Variables)
                        cctor.Body.Variables.Add(i);
                }

                byte[] n = new byte[0x10];
                int typeDefCount = rand.Next(1, 10);
                for (int i = 0; i < typeDefCount; i++)
                {
                    TypeDefinition typeDef = new TypeDefinition(
                        "", ObfuscationHelper.GetRandomName(),
                        TypeAttributes.Class | TypeAttributes.Abstract | TypeAttributes.NotPublic | TypeAttributes.Sealed,
                        mod.TypeSystem.Object);
                    mod.Types.Add(typeDef);
                    int methodCount = rand.Next(1, 5);
                    Database.AddEntry("Const", "ConsterTypes", typeDef.FullName);

                    for (int j = 0; j < methodCount; j++)
                    {
                        MethodDefinition mtd = CecilHelper.Inject(mod, method);
                        mtd.Name = ObfuscationHelper.GetRandomName();
                        mtd.IsCompilerControlled = true;

                        AddHelper(mtd, HelperAttribute.NoInjection);
                        typeDef.Methods.Add(mtd);

                        Database.AddEntry("Const", "ConsterMethods", mtd.FullName);

                        Conster conster = new Conster();
                        conster.key0 = rand.Next();
                        conster.key1 = rand.Next();
                        conster.key2 = rand.Next();
                        conster.key3 = rand.Next();
                        conster.conster = mtd;
                        Database.AddEntry("Const", mtd.FullName, string.Format("{0}, {1}, {2}, {3}", conster.key0, conster.key1, conster.key2, conster.key3));

                        mtd.Body.SimplifyMacros();
                        foreach (Instruction inst in mtd.Body.Instructions)
                        {
                            if (inst.Operand is FieldReference)
                            {
                                if ((inst.Operand as FieldReference).Name == "constTbl")
                                    inst.Operand = constTbl;
                                else if ((inst.Operand as FieldReference).Name == "constStream")
                                    inst.Operand = constStream;
                            }
                            else if (inst.Operand is int && (int)inst.Operand == 0x57425674)
                                inst.Operand = txt.key;
                            else if (inst.Operand is int && (int)inst.Operand == 12345678)
                                inst.Operand = conster.key0;
                            else if (inst.Operand is int && (int)inst.Operand == 0x67452301)
                                inst.Operand = conster.key1;
                            else if (inst.Operand is int && (int)inst.Operand == 0x3bd523a0)
                                inst.Operand = conster.key2;
                            else if (inst.Operand is int && (int)inst.Operand == 0x5f6f36c0)
                                inst.Operand = conster.key3;
                            else if (inst.Operand is int && (int)inst.Operand == 0x263013d3)
                                conster.keyInst = inst;
                            else if (inst.Operand is int && (int)inst.Operand == 11)
                                inst.Operand = (int)txt.types[0];
                            else if (inst.Operand is int && (int)inst.Operand == 22)
                                inst.Operand = (int)txt.types[1];
                            else if (inst.Operand is int && (int)inst.Operand == 33)
                                inst.Operand = (int)txt.types[2];
                            else if (inst.Operand is int && (int)inst.Operand == 44)
                                inst.Operand = (int)txt.types[3];
                            else if (inst.Operand is int && (int)inst.Operand == 55)
                                inst.Operand = (int)txt.types[4];
                        }

                        ret.Add(conster);
                    }
                }
                return ret.ToArray();
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
                _Context txt = cc.txts[mod];
                MemoryStream str = new MemoryStream();
                using (BinaryWriter wtr = new BinaryWriter(new DeflateStream(str, CompressionMode.Compress)))
                {
                    foreach (Data dat in txt.dats)
                    {
                        wtr.Write(dat.Type);
                        wtr.Write(dat.Dat.Length);
                        wtr.Write(dat.Dat);
                    }
                }
                mod.Resources.Add(new EmbeddedResource(txt.resId, ManifestResourceAttributes.Private, str.ToArray()));
            }

            class Context
            {
                public MethodDefinition mtd;
                public ILProcessor psr;
                public Instruction str;
                public uint a;
                public uint b;
                public Conster conster;
            }
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
            void ExtractData(IList<Tuple<IAnnotationProvider, NameValueCollection>> mtds,
                List<Context> txts, bool num, _Context txt)
            {
                foreach (var tuple in mtds)
                {
                    MethodDefinition mtd = tuple.Item1 as MethodDefinition;
                    if (cc.txts[mod].consters.Any(_ => _.conster == mtd) || !mtd.HasBody) continue;
                    var bdy = mtd.Body;
                    var insts = bdy.Instructions;
                    ILProcessor psr = bdy.GetILProcessor();
                    for (int i = 0; i < insts.Count; i++)
                    {
                        if (insts[i].OpCode.Code == Code.Ldstr ||
                            (num && (insts[i].OpCode.Code == Code.Ldc_I4 ||
                            insts[i].OpCode.Code == Code.Ldc_I8 ||
                            insts[i].OpCode.Code == Code.Ldc_R4 ||
                            insts[i].OpCode.Code == Code.Ldc_R8)))
                        {
                            txts.Add(new Context()
                            {
                                mtd = mtd,
                                psr = psr,
                                str = insts[i],
                                a = (uint)Random.Next(),
                                conster = txt.consters[Random.Next(0, txt.consters.Length)]
                            });
                        }
                    }
                }
            }
            byte[] GetOperand(object operand, out byte type)
            {
                byte[] ret;
                if (operand is double)
                {
                    ret = BitConverter.GetBytes((double)operand);
                    type = cc.txts[mod].types[0];
                }
                else if (operand is float)
                {
                    ret = BitConverter.GetBytes((float)operand);
                    type = cc.txts[mod].types[1];
                }
                else if (operand is int)
                {
                    ret = BitConverter.GetBytes((int)operand);
                    type = cc.txts[mod].types[2];
                }
                else if (operand is long)
                {
                    ret = BitConverter.GetBytes((long)operand);
                    type = cc.txts[mod].types[3];
                }
                else
                {
                    ret = Encoding.UTF8.GetBytes((string)operand);
                    type = cc.txts[mod].types[4];
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
            void FinalizeBodies(List<Context> txts)
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

                    Instruction call = Instruction.Create(OpCodes.Call, txts[i].conster.conster);
                    call.SequencePoint = now.SequencePoint;

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
                    txts[i].psr.Replace(idx, Instruction.Create(OpCodes.Ldc_I4, (int)txts[i].a));
                    txts[i].psr.InsertAfter(idx, Instruction.Create(OpCodes.Ldc_I4, (int)txts[i].b));

                    if (i % interval == 0 || i == txts.Count - 1)
                        progresser.SetProgress(i + 1, txts.Count);
                }

                List<int> hashs = new List<int>();
                for (int i = 0; i < txts.Count; i++)
                {
                    if (hashs.IndexOf(txts[i].mtd.GetHashCode()) == -1)
                    {
                        txts[i].mtd.Body.MaxStackSize += 2;
                        hashs.Add(txts[i].mtd.GetHashCode());
                    }
                }
            }

            public override void Process(ConfusionParameter parameter)
            {
                if (parameter.GlobalParameters["type"] != "dynamic" &&
                    parameter.GlobalParameters["type"] != "native")
                {
                    ProcessSafe(parameter); return;
                }
                _Context txt = cc.txts[mod];

                List<Context> txts = new List<Context>();
                ExtractData(
                    parameter.Target as IList<Tuple<IAnnotationProvider, NameValueCollection>>, txts,
                    Array.IndexOf(parameter.GlobalParameters.AllKeys, "numeric") != -1, txt);

                txt.dict.Clear();

                for (int i = 0; i < txts.Count; i++)
                {
                    object val = txts[i].str.Operand as object;
                    if (IsNull(val)) continue;

                    uint x = txts[i].conster.conster.MetadataToken.ToUInt32() ^
                                    (txts[i].conster.conster.DeclaringType.MetadataToken.ToUInt32() * txts[i].a);
                    if (txt.dict.ContainsKey(val))
                        txts[i].b = (uint)txt.dict[val] ^
                                ComputeHash(x,
                                (uint)txts[i].conster.key0,
                                (uint)txts[i].conster.key1,
                                (uint)txts[i].conster.key2,
                                (uint)txts[i].conster.key3);
                    else
                    {
                        txts[i].b = (uint)txt.idx ^
                                ComputeHash(x,
                                (uint)txts[i].conster.key0,
                                (uint)txts[i].conster.key1,
                                (uint)txts[i].conster.key2,
                                (uint)txts[i].conster.key3);
                        byte t;
                        byte[] ori = GetOperand(val, out t);

                        int len;
                        byte[] dat = Encrypt(ori, txt.exp, txt.keyBuff, out len);
                        byte[] final = new byte[dat.Length + 4];
                        Buffer.BlockCopy(dat, 0, final, 4, dat.Length);
                        Buffer.BlockCopy(BitConverter.GetBytes(len ^ txt.key), 0, final, 0, 4);
                        txt.dats.Add(new Data() { Dat = final, Type = t });
                        txt.dict[val] = txt.idx;
                        Database.AddEntry("Const", val.ToString(), string.Format("{0}, {1}, {2}", txts[i].a, txts[i].b, txt.idx));


                        txt.idx += final.Length + 5;
                    }
                }


                foreach (var i in txt.consters)
                {
                    Instruction placeholder = null;
                    foreach (Instruction inst in i.conster.Body.Instructions)
                        if (inst.Operand is MethodReference && (inst.Operand as MethodReference).Name == "PlaceHolder")
                        {
                            placeholder = inst;
                            break;
                        }
                    if (txt.isNative)
                        CecilHelper.Replace(i.conster.Body, placeholder, new Instruction[]
                        {
                            Instruction.Create(OpCodes.Call, txt.nativeDecr)
                        });
                    else
                    {
                        Instruction ldloc = placeholder.Previous;
                        i.conster.Body.Instructions.Remove(placeholder.Previous);   //ldloc
                        CecilHelper.Replace(i.conster.Body, placeholder, new CecilVisitor(txt.invExp, new Instruction[]
                        {
                            ldloc
                        }).GetInstructions());
                    }
                }

                FinalizeBodies(txts);
            }
            void ProcessSafe(ConfusionParameter parameter)
            {
                _Context txt = cc.txts[mod];

                List<Context> txts = new List<Context>();
                ExtractData(
                    parameter.Target as IList<Tuple<IAnnotationProvider, NameValueCollection>>, txts,
                    Array.IndexOf(parameter.GlobalParameters.AllKeys, "numeric") != -1, txt);

                for (int i = 0; i < txts.Count; i++)
                {
                    int idx = txts[i].mtd.Body.Instructions.IndexOf(txts[i].str);
                    object val = txts[i].str.Operand;
                    if (IsNull(val)) continue;

                    uint x = txts[i].conster.conster.MetadataToken.ToUInt32() ^
                                    (txts[i].conster.conster.DeclaringType.MetadataToken.ToUInt32() * txts[i].a);
                    if (txt.dict.ContainsKey(val))
                        txts[i].b = (uint)txt.dict[val] ^
                                ComputeHash(x,
                                (uint)txts[i].conster.key0,
                                (uint)txts[i].conster.key1,
                                (uint)txts[i].conster.key2,
                                (uint)txts[i].conster.key3);
                    else
                    {
                        byte t;
                        byte[] ori = GetOperand(val, out t);
                        byte[] dat = EncryptSafe(ori, (txt.idx + t) * txt.key, txt.keyBuff);
                        txts[i].b = (uint)txt.idx ^
                                ComputeHash(x,
                                (uint)txts[i].conster.key0,
                                (uint)txts[i].conster.key1,
                                (uint)txts[i].conster.key2,
                                (uint)txts[i].conster.key3);

                        txt.dats.Add(new Data() { Dat = dat, Type = t });
                        txt.dict[val] = txt.idx;
                        Database.AddEntry("Const", val.ToString(), string.Format("{0}, {1}, {2}", txts[i].a, txts[i].b, txt.idx));
                        txt.idx += dat.Length + 5;
                    }
                }

                FinalizeBodies(txts);
            }

            IProgresser progresser;
            public void SetProgresser(IProgresser progresser)
            {
                this.progresser = progresser;
            }
        }
        class MdPhase1 : MetadataPhase
        {
            public MdPhase1(ConstantConfusion cc) { this.cc = cc; }
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
                get { return Priority.TypeLevel; }
            }

            public override void Process(NameValueCollection parameters, MetadataProcessor.MetadataAccessor accessor)
            {
                _Context txt = cc.txts[accessor.Module];

                int rid = accessor.TableHeap.GetTable<StandAloneSigTable>(Table.StandAloneSig).AddRow(
                    accessor.BlobHeap.GetBlobIndex(new Mono.Cecil.PE.ByteBuffer(txt.keyBuff)));

                int token = 0x11000000 | rid;
                foreach (var i in txt.consters)
                {
                    i.keyInst.Operand = (int)(token ^ i.conster.MetadataToken.ToInt32());
                }
                Database.AddEntry("Const", "KeyBuffToken", token);

                if (!txt.isNative) return;

                txt.nativeRange = new Range(accessor.Codebase + (uint)accessor.Codes.Position, 0);
                MemoryStream ms = new MemoryStream();
                using (BinaryWriter wtr = new BinaryWriter(ms))
                {
                    wtr.Write(new byte[] { 0x89, 0xe0 });   //   mov eax, esp
                    wtr.Write(new byte[] { 0x53 });   //   push ebx
                    wtr.Write(new byte[] { 0x57 });   //   push edi
                    wtr.Write(new byte[] { 0x56 });   //   push esi
                    wtr.Write(new byte[] { 0x29, 0xe0 });   //   sub eax, esp
                    wtr.Write(new byte[] { 0x83, 0xf8, 0x18 });   //   cmp eax, 24
                    wtr.Write(new byte[] { 0x74, 0x07 });   //   je n
                    wtr.Write(new byte[] { 0x8b, 0x44, 0x24, 0x10 });   //   mov eax, [esp + 4]
                    wtr.Write(new byte[] { 0x50 });   //   push eax
                    wtr.Write(new byte[] { 0xeb, 0x01 });   //   jmp z
                    wtr.Write(new byte[] { 0x51 });   //n: push ecx
                    x86Register ret;                                    //z: 
                    var insts = txt.visitor.GetInstructions(out ret);
                    foreach (var i in insts)
                        wtr.Write(i.Assemble());
                    if (ret != x86Register.EAX)
                        wtr.Write(
                            new x86Instruction()
                            {
                                OpCode = x86OpCode.MOV,
                                Operands = new Ix86Operand[]
                                {
                                    new x86RegisterOperand() { Register = x86Register.EAX },
                                    new x86RegisterOperand() { Register = ret }
                                }
                            }.Assemble());
                    wtr.Write(new byte[] { 0x5e });   //pop esi
                    wtr.Write(new byte[] { 0x5f });   //pop edi
                    wtr.Write(new byte[] { 0x5b });   //pop ebx
                    wtr.Write(new byte[] { 0xc3 });   //ret
                    wtr.Write(new byte[((ms.Length + 3) & ~3) - ms.Length]);
                }
                byte[] codes = ms.ToArray();
                Database.AddEntry("Const", "Native", codes);
                accessor.Codes.WriteBytes(codes);
                accessor.SetCodePosition(accessor.Codebase + (uint)accessor.Codes.Position);
                txt.nativeRange.Length = (uint)codes.Length;
            }
        }
        class MdPhase2 : MetadataPhase
        {
            public MdPhase2(ConstantConfusion cc) { this.cc = cc; }
            ConstantConfusion cc;
            public override IConfusion Confusion
            {
                get { return cc; }
            }

            public override int PhaseID
            {
                get { return 2; }
            }

            public override Priority Priority
            {
                get { return Priority.TypeLevel; }
            }

            public override void Process(NameValueCollection parameters, MetadataProcessor.MetadataAccessor accessor)
            {
                _Context txt = cc.txts[accessor.Module];

                if (!txt.isNative) return;

                var tbl = accessor.TableHeap.GetTable<MethodTable>(Table.Method);
                var row = tbl[(int)txt.nativeDecr.MetadataToken.RID - 1];
                row.Col2 = MethodImplAttributes.Native | MethodImplAttributes.Unmanaged | MethodImplAttributes.PreserveSig;
                row.Col3 &= ~MethodAttributes.Abstract;
                row.Col3 |= MethodAttributes.PInvokeImpl;
                row.Col1 = txt.nativeRange.Start;
                accessor.BodyRanges[txt.nativeDecr.MetadataToken] = txt.nativeRange;

                tbl[(int)txt.nativeDecr.MetadataToken.RID - 1] = row;

                //accessor.Module.Attributes &= ~ModuleAttributes.ILOnly;

            }
        }


        struct Data
        {
            public byte[] Dat;
            public byte Type;
        }
        struct Conster
        {
            public MethodDefinition conster;
            public int key0;
            public int key1;
            public int key2;
            public int key3;
            public Instruction keyInst;
        }
        class _Context
        {
            public List<Data> dats;
            public Dictionary<object, int> dict;
            public int idx = 0;
            public int key;
            public byte[] keyBuff = new byte[16];

            public int resKey;
            public string resId;
            public byte[] types = new byte[5];
            public Conster[] consters;
            public MethodDefinition nativeDecr;

            public bool isNative;
            public Expression exp;
            public Expression invExp;
            public x86Visitor visitor;
            public Range nativeRange;
        }
        Dictionary<ModuleDefinition, _Context> txts = new Dictionary<ModuleDefinition, _Context>();

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
                    ps = new Phase[] { new Phase1(this), new Phase3(this), new MdPhase1(this), new MdPhase2(this) };
                return ps;
            }
        }

        public void Init() { txts.Clear(); }
        public void Deinit() { txts.Clear(); }

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

        private static byte[] Encrypt(byte[] bytes, Expression exp, byte[] keyBuff, out int len)
        {
            byte[] tmp = new byte[(bytes.Length + 7) & ~7];
            Buffer.BlockCopy(bytes, 0, tmp, 0, bytes.Length);
            len = bytes.Length;

            MemoryStream ret = new MemoryStream();
            using (BinaryWriter wtr = new BinaryWriter(ret))
            {
                for (int i = 0; i < tmp.Length; i++)
                {
                    int en = (int)ExpressionEvaluator.Evaluate(exp, tmp[i] ^ keyBuff[i % 16]);
                    Write7BitEncodedInt(wtr, en);
                }
            }

            return ret.ToArray();
        }
        private static byte[] EncryptSafe(byte[] bytes, int key, byte[] keyBuff)
        {
            ushort _m = (ushort)(key >> 16);
            ushort _c = (ushort)(key & 0xffff);
            ushort m = _c; ushort c = _m;
            byte[] ret = (byte[])bytes.Clone();
            for (int i = 0; i < ret.Length; i++)
            {
                ret[i] ^= (byte)(((key * m + c) % 0x100) ^ keyBuff[i % 16]);
                m = (ushort)((key * m + _m) % 0x10000);
                c = (ushort)((key * c + _c) % 0x10000);
            }
            return ret;
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
