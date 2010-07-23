using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using System.Security.Cryptography;
using System.IO;
using System.Globalization;
using System.IO.Compression;

namespace Confuser.Core.Confusions
{
    public class CtorProxyConfusion : StructureConfusion
    {
        public override Priority Priority
        {
            get { return Priority.TypeLevel; }
        }
        public override string Name
        {
            get { return "Constructor Proxy Confusion"; }
        }
        public override Phases Phases
        {
            get { return Phases.Phase1 | Phases.Phase2; }
        }



        public override void Confuse(int phase,Confuser cr, AssemblyDefinition asm, IMemberDefinition[] defs)
        {
            switch (phase)
            {
                case 1:
                    mcd = asm.MainModule.Import(typeof(MulticastDelegate));
                    v = asm.MainModule.Import(typeof(void));
                    obj = asm.MainModule.Import(typeof(object));
                    ptr = asm.MainModule.Import(typeof(IntPtr));

                    txts = new List<Context>();
                    foreach (MethodDefinition mtd in defs)
                    {
                        if (!mtd.HasBody || mtd.DeclaringType.FullName == "<Module>") continue;

                        MethodBody bdy = mtd.Body;
                        foreach (Instruction inst in bdy.Instructions)
                        {
                            if (inst.OpCode.Code == Code.Newobj &&
                                !((inst.Operand as MethodReference).DeclaringType is GenericInstanceType) &&
                                !(inst.Operand is GenericInstanceMethod))
                            {
                                CreateDelegate(cr, mtd.Body, inst, inst.Operand as MethodReference, asm.MainModule);
                            }
                        }
                    }

                    TypeDefinition mod = asm.MainModule.GetType("<Module>");
                    AssemblyDefinition i = AssemblyDefinition.ReadAssembly(typeof(CtorProxyConfusion).Assembly.Location);
                    proxy = i.MainModule.GetType("Confuser.Core.Confusions.CtorProxyConfusion").Methods.FirstOrDefault(mtd => mtd.Name == "Injection");
                    proxy = CecilHelper.Inject(asm.MainModule, proxy);
                    mod.Methods.Add(proxy);
                    proxy.IsAssembly = true;
                    proxy.Name = ObfuscationHelper.GetNewName("Proxy" + Guid.NewGuid().ToString());

                    CreateFieldBridges(cr, asm.MainModule);
                    break;
                case 2:
                    InitModuleCctor(asm.MainModule);
                    CreateCtors(cr, asm.MainModule);
                    FinalizeModuleCctor(asm.MainModule);
                    break;
                default:
                    throw new InvalidOperationException();
            }
        }


        private delegate void Processer(Confuser cr, MethodBody Bdy, Instruction Inst, MethodReference MtdRef, ModuleDefinition Mod);

        MethodDefinition proxy;
        private class Context { public MethodBody bdy; public Instruction inst; public FieldDefinition fld; public TypeDefinition dele; public MethodReference mtdRef;}
        List<Context> txts;

        TypeReference mcd;
        TypeReference v;
        TypeReference obj;
        TypeReference ptr;
        private void CreateDelegate(Confuser cr, MethodBody Bdy, Instruction Inst, MethodReference MtdRef, ModuleDefinition Mod)
        {
            //Limitation
            TypeDefinition tmp = MtdRef.DeclaringType.Resolve();
            if (tmp.BaseType != null &&
                (tmp.BaseType.FullName == "System.MulticastDelegate" ||
                tmp.BaseType.FullName == "System.Delegate"))
                return;

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

                MethodDefinition invoke = new MethodDefinition("Invoke", 0, MtdRef.DeclaringType);
                invoke.IsRuntime = true;
                invoke.HasThis = true;
                invoke.IsHideBySig = true;
                invoke.IsVirtual = true;
                invoke.IsPublic = true;

                for (int i = 0; i < MtdRef.Parameters.Count; i++)
                {
                    invoke.Parameters.Add(new ParameterDefinition(GetNameO(MtdRef.Parameters[i]), MtdRef.Parameters[i].Attributes, MtdRef.Parameters[i].ParameterType));
                }
                txt.dele.Methods.Add(invoke);
            }
            txts.Add(txt);
        }
        private void CreateFieldBridges(Confuser cr, ModuleDefinition Mod)
        {
            foreach (Context txt in txts)
            {
                ////////////////Field
                if ((txt.fld = Mod.GetType("<Module>").Fields.FirstOrDefault(fld => fld.Name == GetId(Mod, txt.mtdRef))) == null)
                {
                    txt.fld = new FieldDefinition(GetId(Mod, txt.mtdRef), FieldAttributes.Static | FieldAttributes.Assembly, txt.dele);
                    Mod.GetType("<Module>").Fields.Add(txt.fld);
                }
                ////////////////Bridge
                MethodDefinition bdge;
                if ((bdge = Mod.GetType("<Module>").Methods.FirstOrDefault(mtd => mtd.Name == GetNameO(txt.mtdRef))) == null)
                {
                    bdge = new MethodDefinition(GetNameO(txt.mtdRef), MethodAttributes.Static | MethodAttributes.Assem, txt.mtdRef.DeclaringType);
                    for (int i = 0; i < txt.mtdRef.Parameters.Count; i++)
                    {
                        bdge.Parameters.Add(new ParameterDefinition(GetNameO(txt.mtdRef.Parameters[i]), txt.mtdRef.Parameters[i].Attributes, txt.mtdRef.Parameters[i].ParameterType));
                    }
                    {
                        ILProcessor wkr = bdge.Body.GetILProcessor();
                        wkr.Emit(OpCodes.Ldsfld, txt.fld);
                        for (int i = 0; i < bdge.Parameters.Count; i++)
                        {
                            wkr.Emit(OpCodes.Ldarg, bdge.Parameters[i]);
                        }
                        wkr.Emit(OpCodes.Call, txt.dele.Methods.FirstOrDefault(mtd => mtd.Name == "Invoke"));
                        wkr.Emit(OpCodes.Ret);
                    }
                    Mod.GetType("<Module>").Methods.Add(bdge);
                }

                ////////////////Replace
                txt.inst.OpCode = OpCodes.Call;
                txt.inst.Operand = bdge;
            }
        }
        private void CreateCtors(Confuser cr, ModuleDefinition Mod)
        {
            MethodDefinition cctor = Mod.GetType("<Module>").GetStaticConstructor();
            ILProcessor wkr = cctor.Body.GetILProcessor();

            foreach (Context txt in txts)
            {
                ////////////////Cctor
                txt.fld.Name = GetId(Mod, txt.mtdRef);
                wkr.Emit(OpCodes.Ldtoken, txt.fld);
                wkr.Emit(OpCodes.Call, proxy);
            }
        }

        private void InitModuleCctor(ModuleDefinition mod)
        {
            MethodDefinition cctor = mod.GetType("<Module>").GetStaticConstructor();
            MethodBody bdy = cctor.Body as MethodBody;
            bdy.Instructions.RemoveAt(bdy.Instructions.Count - 1);
        }
        private void FinalizeModuleCctor(ModuleDefinition mod)
        {
            MethodDefinition cctor = mod.GetType("<Module>").GetStaticConstructor();
            cctor.Body.GetILProcessor().Emit(OpCodes.Ret);
        }

        string GetNameO(MethodReference mbr)
        {
            return ObfuscationHelper.GetNewName(mbr.ToString());
        }
        string GetNameO(ParameterDefinition arg)
        {
            return ObfuscationHelper.GetNewName(arg.Name);
        }
        string GetSignatureO(MethodReference mbr)
        {
            return ObfuscationHelper.GetNewName(GetSignature(mbr));
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

        private string GetId(ModuleDefinition mod, MethodReference mtd)
        {
            char asmRef = (char)(mod.AssemblyReferences.IndexOf(mtd.DeclaringType.Scope as AssemblyNameReference) + 2);
            return asmRef + Encoding.Unicode.GetString(BitConverter.GetBytes(mtd.Resolve().MetadataToken.ToUInt32()));
        }
        private static void Injection(RuntimeFieldHandle f)
        {
            var fld = System.Reflection.FieldInfo.GetFieldFromHandle(f);

            var asm=System.Reflection.Assembly.GetExecutingAssembly();
            if (fld.Name[0] != (char)1)
                asm = System.Reflection.Assembly.Load(asm.GetReferencedAssemblies()[(int)fld.Name[0] - 2]);

            var mtd = asm.GetModules()[0].ResolveMethod(BitConverter.ToInt32(Encoding.Unicode.GetBytes(fld.Name.ToCharArray(), 1, 2), 0)) as System.Reflection.ConstructorInfo;

            var args = mtd.GetParameters();
            Type[] arg = new Type[args.Length];
            for (int i = 0; i < args.Length; i++)
                arg[i] = args[i].ParameterType;

            var dm = new System.Reflection.Emit.DynamicMethod("", mtd.DeclaringType, arg, mtd.DeclaringType, true);
            var gen = dm.GetILGenerator();
            for (int i = 0; i < arg.Length; i++)
                gen.Emit(System.Reflection.Emit.OpCodes.Ldarg_S, i);
            gen.Emit(System.Reflection.Emit.OpCodes.Newobj, mtd);
            gen.Emit(System.Reflection.Emit.OpCodes.Ret);

            fld.SetValue(null, dm.CreateDelegate(fld.FieldType));
        }

        public override bool StandardCompatible
        {
            get { return true; }
        }

        public override string Description
        {
            get { return @"This confusion create proxies between references of constructors and methods code.
***This confusion could affect the startup performance***"; }
        }

        public override Target Target
        {
            get { return Target.Methods; }
        }
    }
}
