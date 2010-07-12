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
                        else if (inst.Operand is long && (long)inst.Operand == 6666666666666666)
                        {
                            Instruction[] expInsts = new CecilVisitor(exp, true,
                                new Instruction[]{
                                Instruction.Create(OpCodes.Ldloc, strer.Body.Variables.FirstOrDefault(var => var.VariableType.FullName == "System.IO.BinaryReader")),
                                Instruction.Create(OpCodes.Callvirt, asm.MainModule.Import(typeof(BinaryReader).GetMethod("ReadInt64")))
                                 }, false).GetInstructions();
                            ILProcessor psr = strer.Body.GetILProcessor();
                            psr.Replace(inst, expInsts[0]);
                            for (int ii = 1; ii < expInsts.Length; ii++)
                            {
                                psr.InsertAfter(expInsts[ii - 1], expInsts[ii]);
                                t++;
                            }
                            psr.InsertAfter(expInsts[expInsts.Length - 1], Instruction.Create(OpCodes.Conv_I8));
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
                    byte[] dat = Encrypt(val, mtd.MetadataToken.ToUInt32(), eval, out len);
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
        }

        private static byte[] Encrypt(string str, uint mdToken, ReflectionVisitor expEval, out int len)
        {
            byte[] bs = Encoding.Unicode.GetBytes(str);
            byte[] tmp = new byte[(bs.Length + 7) & ~7];
            Buffer.BlockCopy(bs, 0, tmp, 0, bs.Length);
            byte[] ret = new byte[(bs.Length + 7) & ~7];
            len = bs.Length;

            Random rand = new Random((int)mdToken);
            for (int i = 0; i < tmp.Length; i += 8)
            {
                long en = (long)expEval.Eval(BitConverter.ToInt64(tmp, i) ^ rand.Next());
                Debug.WriteLine(BitConverter.ToInt64(tmp, i).ToString() + " => " + en);
                Buffer.BlockCopy(BitConverter.GetBytes(en), 0, ret, i, 8);
            }
            Debug.WriteLine("");

            return ret;
        }
        private static string Decrypt(byte[] bytes, uint mdToken, int len, ReflectionVisitor expEval)
        {
            Random rand = new Random((int)mdToken);

            byte[] ret = new byte[(len + 7) & ~7];

            using (BinaryReader rdr = new BinaryReader(new MemoryStream(bytes)))
            {
                for (int i = 0; i < ret.Length; i += 8)
                {
                    long r = rdr.ReadInt64();
                    long de = (long)expEval.Eval(r) ^ rand.Next();
                    Debug.WriteLine(r.ToString() + " => " + de);
                    Buffer.BlockCopy(BitConverter.GetBytes(de), 0, ret, i, 8);
                }
            }
            Debug.WriteLine("");

            return Encoding.Unicode.GetString(ret, 0, len);
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
                    Random rand = new Random((int)mdTkn);

                    byte[] f = new byte[(len + 7) & ~7];

                    for (int i = 0; i < f.Length; i += 8)
                        Buffer.BlockCopy(BitConverter.GetBytes(6666666666666666 ^ rand.Next()), 0, f, i, 8);


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
