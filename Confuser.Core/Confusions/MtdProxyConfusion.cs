using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using System.Security.Cryptography;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using System.IO;
using System.Globalization;
using System.IO.Compression;

namespace Confuser.Core.Confusions
{
    public class MtdProxyConfusion : StructureConfusion
    {
        public override Priority Priority
        {
            get { return Priority.TypeLevel; }
        }
        public override string Name
        {
            get { return "Method Proxy Confusion"; }
        }
        public override ProcessType Process
        {
            get { return ProcessType.Pre | ProcessType.Real; }
        }



        public override void PreConfuse(Confuser cr, AssemblyDefinition asm)
        {
            mcd = asm.MainModule.Import(typeof(MulticastDelegate));
            v = asm.MainModule.Import(typeof(void));
            obj = asm.MainModule.Import(typeof(object));
            ptr = asm.MainModule.Import(typeof(IntPtr));

            txts = new List<Context>();
            cr.Log("<delegates>");
            cr.AddLv();
            foreach (TypeDefinition def in asm.MainModule.GetAllTypes().ToArray())
            {
                if (def.Name == "<Module>") continue;
                ProcessMethods(cr, def, asm.MainModule, new Processer(CreateDelegate));
            }
            cr.SubLv();
            cr.Log("</delegates>");

            TypeDefinition mod = asm.MainModule.GetType("<Module>");
            AssemblyDefinition i = AssemblyDefinition.ReadAssembly(typeof(MtdProxyConfusion).Assembly.Location);
            proxy = i.MainModule.GetType("Confuser.Core.Confusions.MtdProxyConfusion").Methods.FirstOrDefault(mtd => mtd.Name == "Injection");
            proxy = CecilHelper.Inject(asm.MainModule, proxy);
            mod.Methods.Add(proxy);
            proxy.IsAssembly = true;
            proxy.Name = GetName("Proxy" + Guid.NewGuid().ToString());

            cr.Log("<refs>");
            cr.AddLv();
            CreateFieldBridges(cr, asm.MainModule);
            cr.SubLv();
            cr.Log("</refs>");
        }
        public override void DoConfuse(Confuser cr, AssemblyDefinition asm)
        {
            InitModuleCctor(asm.MainModule);

            cr.Log("<mtds>");
            cr.AddLv();
            CreateCtors(cr, asm.MainModule);
            cr.SubLv();
            cr.Log("</mtds>");

            FinalizeModuleCctor(asm.MainModule);
        }
        public override void PostConfuse(Confuser cr, AssemblyDefinition asm)
        {
            throw new InvalidOperationException();
        }


        private delegate void Processer(Confuser cr, MethodBody Bdy, Instruction Inst, MethodReference MtdRef, ModuleDefinition Mod);

        MethodDefinition proxy;
        private class Context { public MethodBody bdy; public bool isVirt; public Instruction inst; public FieldDefinition fld; public TypeDefinition dele; public MethodReference mtdRef;}
        List<Context> txts;
        private void ProcessMethods(Confuser cr, TypeDefinition def, ModuleDefinition mod, Processer d)
        {
            foreach (MethodDefinition mtd in def.Methods)
            {
                ProcessMethod(cr, mtd, mod, d);
            }
        }
        private void ProcessMethod(Confuser cr, MethodDefinition mtd, ModuleDefinition mod, Processer d)
        {
            if (!mtd.HasBody || mtd.DeclaringType.FullName == "<Module>") return;

            MethodBody bdy = mtd.Body;
            foreach(Instruction inst in bdy.Instructions)
            {
                if ((inst.OpCode.Code == Code.Call || inst.OpCode.Code == Code.Callvirt) &&
                    (inst.Operand as MethodReference).Name != ".ctor" && (inst.Operand as MethodReference).Name != ".cctor" &&
                    !(inst.Operand as MethodReference).DeclaringType.Resolve().IsInterface &&
                    !(inst.Operand as MethodReference).DeclaringType.Resolve().HasGenericParameters)
                {
                    d(cr, mtd.Body, inst, inst.Operand as MethodReference, mod);
                }
            }
        }


        TypeReference mcd;
        TypeReference v;
        TypeReference obj;
        TypeReference ptr;
        private void CreateDelegate(Confuser cr, MethodBody Bdy, Instruction Inst, MethodReference MtdRef, ModuleDefinition Mod)
        {
            //Limitation
            if ((MtdRef.HasThis && MtdRef.DeclaringType.IsValueType) ||
                MtdRef.HasGenericParameters) return;

            Context txt = new Context();
            txt.inst = Inst;
            txt.bdy = Bdy;
            txt.mtdRef = MtdRef;
            if ((txt.dele = Mod.GetType(GetSignatureO(MtdRef))) == null)
            {
                txt.dele = new TypeDefinition("", GetSignatureO(MtdRef), TypeAttributes.NotPublic | TypeAttributes.Sealed, mcd);
                Mod.Types.Add(txt.dele);

                MethodDefinition cctor = new MethodDefinition(".ctor", 0, v);
                cctor.IsRuntime = true;
                cctor.HasThis = true;
                cctor.IsHideBySig = true;
                cctor.IsRuntimeSpecialName = true;
                cctor.IsSpecialName = true;
                cctor.IsPublic = true;
                cctor.Parameters.Add(new ParameterDefinition(obj));
                cctor.Parameters.Add(new ParameterDefinition(ptr));
                txt.dele.Methods.Add(cctor);

                MethodDefinition invoke = new MethodDefinition("Invoke", 0, MtdRef.ReturnType);
                invoke.IsRuntime = true;
                invoke.HasThis = true;
                invoke.IsHideBySig = true;
                invoke.IsVirtual = true;
                invoke.IsPublic = true;

                if (MtdRef.HasThis)
                {
                    invoke.Parameters.Add(new ParameterDefinition(obj));
                    for (int i = 0; i < MtdRef.Parameters.Count; i++)
                    {
                        invoke.Parameters.Add(new ParameterDefinition(GetNameO(MtdRef.Parameters[i]), MtdRef.Parameters[i].Attributes, MtdRef.Parameters[i].ParameterType));
                    }
                }
                else
                {
                    for (int i = 0; i < MtdRef.Parameters.Count; i++)
                    {
                        invoke.Parameters.Add(new ParameterDefinition(GetNameO(MtdRef.Parameters[i]), MtdRef.Parameters[i].Attributes, MtdRef.Parameters[i].ParameterType));
                    }
                }
                txt.dele.Methods.Add(invoke);

                cr.Log("<delegate sig='" + GetSignature(MtdRef) + "'/>");
            }
            txts.Add(txt);
        }
        private void CreateFieldBridges(Confuser cr, ModuleDefinition Mod)
        {
            foreach (Context txt in txts)
            {
                ////////////////Field
                if ((txt.fld = Mod.GetType("<Module>").Fields.FirstOrDefault(fld => fld.Name == GetId(Mod, txt.inst.OpCode.Name == "callvirt", txt.mtdRef))) == null)
                {
                    txt.fld = new FieldDefinition(GetId(Mod, txt.inst.OpCode.Name == "callvirt", txt.mtdRef), FieldAttributes.Static | FieldAttributes.Assembly, txt.dele);
                    Mod.GetType("<Module>").Fields.Add(txt.fld);
                }
                ////////////////Bridge
                MethodDefinition bdge;
                if ((bdge = Mod.GetType("<Module>").Methods.FirstOrDefault(mtd => mtd.Name == GetNameO(txt.mtdRef))) == null)
                {
                    bdge = new MethodDefinition(GetNameO(txt.mtdRef), MethodAttributes.Static | MethodAttributes.Assem, txt.mtdRef.ReturnType);
                    if (txt.mtdRef.HasThis)
                    {
                        bdge.Parameters.Add(new ParameterDefinition(Mod.Import(typeof(object))));

                        for (int i = 0; i < txt.mtdRef.Parameters.Count; i++)
                        {
                            bdge.Parameters.Add(new ParameterDefinition(GetNameO(txt.mtdRef.Parameters[i]), txt.mtdRef.Parameters[i].Attributes, txt.mtdRef.Parameters[i].ParameterType));
                        }
                    }
                    else
                    {
                        for (int i = 0; i < txt.mtdRef.Parameters.Count; i++)
                        {
                            bdge.Parameters.Add(new ParameterDefinition(GetNameO(txt.mtdRef.Parameters[i]), txt.mtdRef.Parameters[i].Attributes, txt.mtdRef.Parameters[i].ParameterType));
                        }
                    }
                    {
                        ILProcessor psr = bdge.Body.GetILProcessor();
                        psr.Emit(OpCodes.Ldsfld, txt.fld);
                        for (int i = 0; i < bdge.Parameters.Count; i++)
                        {
                            psr.Emit(OpCodes.Ldarg, bdge.Parameters[i]);
                        }
                        psr.Emit(txt.inst.OpCode, txt.dele.Methods.FirstOrDefault(mtd => mtd.Name == "Invoke"));
                        psr.Emit(OpCodes.Ret);
                    }
                    Mod.GetType("<Module>").Methods.Add(bdge);
                }

                ////////////////Replace
                txt.isVirt = txt.inst.OpCode.Name == "callvirt";
                txt.inst.OpCode = OpCodes.Call;
                txt.inst.Operand = bdge;

                cr.Log("<ref sig='" + txt.mtdRef.ToString() + "'/>");
            }
        }
        private void CreateCtors(Confuser cr, ModuleDefinition Mod)
        {
            MethodDefinition cctor = Mod.GetType("<Module>").GetStaticConstructor();
            ILProcessor wkr = cctor.Body.GetILProcessor();

            foreach (Context txt in txts)
            {
                ////////////////Cctor
                txt.fld.Name = GetId(Mod, txt.isVirt, txt.mtdRef);
                wkr.Emit(OpCodes.Ldtoken, txt.fld);
                wkr.Emit(OpCodes.Call, proxy);

                cr.Log("<dat id='" + txt.fld.MetadataToken.ToUInt32().ToString("X8") + "'/>");
            }
        }

        private void InitModuleCctor(ModuleDefinition mod)
        {
            MethodDefinition cctor = mod.GetType("<Module>").GetStaticConstructor();
            MethodBody bdy = cctor.Body;
            bdy.Instructions.RemoveAt(bdy.Instructions.Count - 1);
        }
        private void FinalizeModuleCctor(ModuleDefinition mod)
        {
            MethodDefinition cctor = mod.GetType("<Module>").GetStaticConstructor();
            cctor.Body.GetILProcessor().Emit(OpCodes.Ret);
        }

        string GetNameO(MethodReference mbr)
        {
            MD5 md5 = MD5.Create();
            byte[] b = md5.ComputeHash(Encoding.UTF8.GetBytes(mbr.ToString()));
            Random rand = new Random(mbr.ToString().GetHashCode());
            StringBuilder ret = new StringBuilder();
            for (int i = 0; i < b.Length; i += 2)
            {
                ret.Append((char)(((b[i] << 8) + b[i + 1]) ^ rand.Next()));
                if (rand.NextDouble() > 0.75)
                    ret.AppendLine();
            }
            return ret.ToString();
        }
        string GetNameO(ParameterDefinition arg)
        {
            MD5 md5 = MD5.Create();
            byte[] b = md5.ComputeHash(Encoding.UTF8.GetBytes(arg.Name));
            Random rand = new Random(arg.ToString().GetHashCode());
            StringBuilder ret = new StringBuilder();
            for (int i = 0; i < b.Length; i += 2)
            {
                ret.Append((char)(((b[i] << 8) + b[i + 1]) ^ rand.Next()));
                if (rand.NextDouble() > 0.75)
                    ret.AppendLine();
            }
            return ret.ToString();
        }
        string GetSignatureO(MethodReference mbr)
        {
            string sig = GetSignature(mbr);
            MD5 md5 = MD5.Create();
            byte[] b = md5.ComputeHash(Encoding.UTF8.GetBytes(sig));
            Random rand = new Random(sig.GetHashCode());
            StringBuilder ret = new StringBuilder();
            for (int i = 0; i < b.Length; i += 2)
            {
                ret.Append((char)(((b[i] << 8) + b[i + 1]) ^ rand.Next()));
                if (rand.NextDouble() > 0.75)
                    ret.AppendLine();
            }
            return ret.ToString();
        }
        string GetSignature(MethodReference mbr)
        {
            StringBuilder sig = new StringBuilder();
            sig.Append(mbr.ReturnType.FullName);
            if (mbr.Resolve().IsVirtual)
                sig.Append(" virtual");
            if (mbr.HasThis)
                sig.Append(" " + mbr.DeclaringType.ToString());
            if (mbr.Name == ".cctor" || mbr.Name == ".ctor")
                sig.Append(mbr.Name);
            sig.Append(" (");
            if (mbr.HasParameters)
            {
                for (int i = 0; i < mbr.Parameters.Count; i++)
                {
                    if (i > 0)
                    {
                        sig.Append(",");
                    }
                    sig.Append(mbr.Parameters[i].ParameterType.FullName);
                }
            }
            sig.Append(")");
            return sig.ToString();
        }
        string GetName(string n)
        {
            MD5 md5 = MD5.Create();
            byte[] b = md5.ComputeHash(Encoding.UTF8.GetBytes(n));
            Random rand = new Random(n.GetHashCode());
            StringBuilder ret = new StringBuilder();
            for (int i = 0; i < b.Length; i += 2)
            {
                ret.Append((char)(((b[i] << 8) + b[i + 1]) ^ rand.Next()));
                if (rand.NextDouble() > 0.75)
                    ret.AppendLine();
            }
            return ret.ToString();
        }

        private string GetId(ModuleDefinition mod, bool isVirt, MethodReference mtd)
        {
            string virt = isVirt ? "\r" : "\n";
            char asmRef = (char)(mod.AssemblyReferences.IndexOf(mtd.DeclaringType.Scope as AssemblyNameReference) + 2);
            return asmRef + virt + Encoding.Unicode.GetString(BitConverter.GetBytes(mtd.Resolve().MetadataToken.ToUInt32()));
        }
        private static void Injection(RuntimeFieldHandle f)
        {
            var fld = System.Reflection.FieldInfo.GetFieldFromHandle(f);

            Console.WriteLine(fld.Name.Length);
            Console.WriteLine(((int)fld.Name[0]).ToString("x2"));
            Console.WriteLine(((int)fld.Name[1]).ToString("x2"));
            Console.WriteLine(((int)fld.Name[2]).ToString("x2"));
            Console.WriteLine(((int)fld.Name[3]).ToString("x2"));

            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            if (fld.Name[0] != (char)1)
                asm = System.Reflection.Assembly.Load(asm.GetReferencedAssemblies()[(int)fld.Name[0] - 2]);
            var mtd = asm.GetModules()[0].ResolveMethod(BitConverter.ToInt32(Encoding.Unicode.GetBytes(fld.Name.ToCharArray(), 2, 2), 0)) as System.Reflection.MethodInfo;

            if (mtd.IsStatic)
            {
                fld.SetValue(null, Delegate.CreateDelegate(fld.FieldType, mtd));
            }
            else
            {
                var tmp = mtd.GetParameters();
                Type[] arg = new Type[tmp.Length + 1];
                arg[0] = typeof(object);
                for (int i = 0; i < tmp.Length; i++)
                    arg[i + 1] = tmp[i].ParameterType;

                System.Reflection.Emit.DynamicMethod dm;
                if (mtd.DeclaringType.IsInterface)
                    dm = new System.Reflection.Emit.DynamicMethod("", mtd.ReturnType, arg, true);
                else
                    dm = new System.Reflection.Emit.DynamicMethod("", mtd.ReturnType, arg, mtd.DeclaringType, true);
                var gen = dm.GetILGenerator();
                gen.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
                gen.Emit(System.Reflection.Emit.OpCodes.Castclass, mtd.DeclaringType);
                for (int i = 1; i < arg.Length; i++)
                    gen.Emit(System.Reflection.Emit.OpCodes.Ldarg_S, i);
                gen.Emit((fld.Name[1] == '\r') ? System.Reflection.Emit.OpCodes.Callvirt : System.Reflection.Emit.OpCodes.Call, mtd);
                gen.Emit(System.Reflection.Emit.OpCodes.Ret);

                fld.SetValue(null, dm.CreateDelegate(fld.FieldType));
            }
        }

        public override bool StandardCompatible
        {
            get { return true; }
        }
    }
}