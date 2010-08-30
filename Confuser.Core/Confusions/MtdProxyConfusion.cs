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
    public class MtdProxyConfusion : IConfusion
    {
        class Phase1 : StructurePhase, IProgressProvider
        {
            MtdProxyConfusion mc;
            public Phase1(MtdProxyConfusion mc) { this.mc = mc; }
            public override IConfusion Confusion
            {
                get { return mc; }
            }

            public override int PhaseID
            {
                get { return 1; }
            }

            public override Priority Priority
            {
                get { return Priority.TypeLevel; }
            }

            public override bool WholeRun
            {
                get { return false; }
            }

            AssemblyDefinition asm;
            public override void Initialize(AssemblyDefinition asm)
            {
                this.asm = asm;

                mc.mcd = asm.MainModule.Import(typeof(MulticastDelegate));
                mc.v = asm.MainModule.Import(typeof(void));
                mc.obj = asm.MainModule.Import(typeof(object));
                mc.ptr = asm.MainModule.Import(typeof(IntPtr));

                mc.txts = new List<Context>();
            }
            public override void DeInitialize()
            {
                TypeDefinition mod = asm.MainModule.GetType("<Module>");
                AssemblyDefinition i = AssemblyDefinition.ReadAssembly(typeof(MtdProxyConfusion).Assembly.Location);
                mc.proxy = i.MainModule.GetType("Confuser.Core.Confusions.MtdProxyConfusion").Methods.FirstOrDefault(mtd => mtd.Name == "Injection");
                mc.proxy = CecilHelper.Inject(asm.MainModule, mc.proxy);
                mod.Methods.Add(mc.proxy);
                mc.proxy.IsAssembly = true;
                mc.proxy.Name = ObfuscationHelper.GetNewName("Proxy" + Guid.NewGuid().ToString());
            }

            public override void Process(ConfusionParameter parameter)
            {
                List<object> targets = parameter.Target as List<object>;
                for (int i = 0; i < targets.Count; i++)
                {
                    MethodDefinition mtd = targets[i] as MethodDefinition;
                    if (!mtd.HasBody || mtd.DeclaringType.FullName == "<Module>") continue;

                    MethodBody bdy = mtd.Body;
                    foreach (Instruction inst in bdy.Instructions)
                    {
                        if ((inst.OpCode.Code == Code.Call || inst.OpCode.Code == Code.Callvirt) &&
                            (inst.Operand as MethodReference).Name != ".ctor" && (inst.Operand as MethodReference).Name != ".cctor" &&
                            !(inst.Operand as MethodReference).DeclaringType.Resolve().IsInterface &&
                            !((inst.Operand as MethodReference).DeclaringType is GenericInstanceType) &&
                            (inst.Previous == null || inst.Previous.OpCode.OpCodeType != OpCodeType.Prefix))
                        {
                            CreateDelegate(mtd.Body, inst, inst.Operand as MethodReference, asm.MainModule);
                        }
                    }
                    progresser.SetProgress((i + 1) / (double)targets.Count);
                }
                for (int i = 0; i < mc.txts.Count; i++)
                {
                    CreateFieldBridge(asm.MainModule, mc.txts[i]);
                    if (i % 10 == 0 || i == mc.txts.Count - 1)
                        progresser.SetProgress((i + 1) / (double)mc.txts.Count);
                }
            }

            private void CreateDelegate(MethodBody Bdy, Instruction Inst, MethodReference MtdRef, ModuleDefinition Mod)
            {
                //Limitation
                if ((MtdRef.HasThis && MtdRef.DeclaringType.IsValueType) ||
                    MtdRef is GenericInstanceMethod ||
                    MtdRef.DeclaringType.FullName == "<Module>") return;

                Context txt = new Context();
                txt.inst = Inst;
                txt.bdy = Bdy;
                txt.mtdRef = MtdRef;
                if ((txt.dele = Mod.GetType(GetSignatureO(MtdRef))) == null)
                {
                    txt.dele = new TypeDefinition("", GetSignatureO(MtdRef), TypeAttributes.NotPublic | TypeAttributes.Sealed, mc.mcd);
                    Mod.Types.Add(txt.dele);

                    MethodDefinition cctor = new MethodDefinition(".ctor", 0, mc.v);
                    cctor.IsRuntime = true;
                    cctor.HasThis = true;
                    cctor.IsHideBySig = true;
                    cctor.IsRuntimeSpecialName = true;
                    cctor.IsSpecialName = true;
                    cctor.IsPublic = true;
                    cctor.Parameters.Add(new ParameterDefinition(mc.obj));
                    cctor.Parameters.Add(new ParameterDefinition(mc.ptr));
                    txt.dele.Methods.Add(cctor);

                    MethodDefinition invoke = new MethodDefinition("Invoke", 0, MtdRef.ReturnType);
                    invoke.IsRuntime = true;
                    invoke.HasThis = true;
                    invoke.IsHideBySig = true;
                    invoke.IsVirtual = true;
                    invoke.IsPublic = true;

                    if (MtdRef.HasThis)
                    {
                        invoke.Parameters.Add(new ParameterDefinition(mc.obj));
                        for (int i = 0; i < MtdRef.Parameters.Count; i++)
                        {
                            invoke.Parameters.Add(Clone(GetNameO(MtdRef.Parameters[i]), MtdRef.Parameters[i]));
                        }
                    }
                    else
                    {
                        for (int i = 0; i < MtdRef.Parameters.Count; i++)
                        {
                            invoke.Parameters.Add(Clone(GetNameO(MtdRef.Parameters[i]), MtdRef.Parameters[i]));
                        }
                    }
                    txt.dele.Methods.Add(invoke);

                }
                mc.txts.Add(txt);
            }
            private void CreateFieldBridge(ModuleDefinition Mod, Context txt)
            {
                ////////////////Field
                if ((txt.fld = Mod.GetType("<Module>").Fields.FirstOrDefault(fld => fld.Name == GetId(Mod, txt.inst.OpCode.Name == "callvirt", txt.mtdRef))) == null)
                {
                    txt.fld = new FieldDefinition(GetId(Mod, txt.inst.OpCode.Name == "callvirt", txt.mtdRef), FieldAttributes.Static | FieldAttributes.Assembly, txt.dele);
                    Mod.GetType("<Module>").Fields.Add(txt.fld);
                }
                ////////////////Bridge
                MethodDefinition bdge;
                if ((bdge = Mod.GetType("<Module>").Methods.FirstOrDefault(mtd => mtd.Name == GetNameO(txt.inst.OpCode.Name == "callvirt", txt.mtdRef))) == null)
                {
                    bdge = new MethodDefinition(GetNameO(txt.inst.OpCode.Name == "callvirt", txt.mtdRef), MethodAttributes.Static | MethodAttributes.Assem, txt.mtdRef.ReturnType);
                    if (txt.mtdRef.HasThis)
                    {
                        bdge.Parameters.Add(new ParameterDefinition(Mod.Import(typeof(object))));

                        for (int i = 0; i < txt.mtdRef.Parameters.Count; i++)
                        {
                            bdge.Parameters.Add(Clone(GetNameO(txt.mtdRef.Parameters[i]), txt.mtdRef.Parameters[i]));
                        }
                    }
                    else
                    {
                        for (int i = 0; i < txt.mtdRef.Parameters.Count; i++)
                        {
                            bdge.Parameters.Add(Clone(GetNameO(txt.mtdRef.Parameters[i]), txt.mtdRef.Parameters[i]));
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
            }

            IProgresser progresser;
            void IProgressProvider.SetProgresser(IProgresser progresser)
            {
                this.progresser = progresser;
            }
        }
        class Phase2 : StructurePhase, IProgressProvider
        {
            MtdProxyConfusion mc;
            public Phase2(MtdProxyConfusion mc) { this.mc = mc; }
            public override IConfusion Confusion
            {
                get { return mc; }
            }

            public override int PhaseID
            {
                get { return 2; }
            }

            public override Priority Priority
            {
                get { return Priority.TypeLevel; }
            }

            public override bool WholeRun
            {
                get { return false; }
            }

            AssemblyDefinition asm;
            public override void Initialize(AssemblyDefinition asm)
            {
                this.asm = asm;

                MethodDefinition cctor = asm.MainModule.GetType("<Module>").GetStaticConstructor();
                MethodBody bdy = cctor.Body as MethodBody;
                bdy.Instructions.RemoveAt(bdy.Instructions.Count - 1);
            }
            public override void DeInitialize()
            {
                MethodDefinition cctor = asm.MainModule.GetType("<Module>").GetStaticConstructor();
                cctor.Body.GetILProcessor().Emit(OpCodes.Ret);
            }
            public override void Process(ConfusionParameter parameter)
            {
                MethodDefinition cctor = asm.MainModule.GetType("<Module>").GetStaticConstructor();
                ILProcessor wkr = cctor.Body.GetILProcessor();

                for (int i = 0; i < mc.txts.Count; i++)
                {
                    ////////////////Cctor
                    mc.txts[i].fld.Name = GetId(asm.MainModule, mc.txts[i].isVirt, mc.txts[i].mtdRef);
                    wkr.Emit(OpCodes.Ldtoken, mc.txts[i].fld);
                    wkr.Emit(OpCodes.Call, mc.proxy);
                    progresser.SetProgress((i + 1) / (double)mc.txts.Count);
                }
            }

            IProgresser progresser;
            void IProgressProvider.SetProgresser(IProgresser progresser)
            {
                this.progresser = progresser;
            }
        }


        public string ID
        {
            get { return "mtd proxy"; }
        }
        public string Name
        {
            get { return "Method Proxy Confusion"; }
        }
        public string Description
        {
            get
            {
                return @"This confusion create proxies between references of methods and methods code.
***This confusion could affect the startup performance***";
            }
        }
        public Target Target
        {
            get { return Target.Methods; }
        }
        public Preset Preset
        {
            get { return Preset.Normal; }
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
                if (ps == null) ps = new Phase[] { new Phase1(this), new Phase2(this) };
                return ps;
            }
        }

        MethodDefinition proxy;
        private class Context { public MethodBody bdy; public bool isVirt; public Instruction inst; public FieldDefinition fld; public TypeDefinition dele; public MethodReference mtdRef;}
        List<Context> txts;
        TypeReference mcd;
        TypeReference v;
        TypeReference obj;
        TypeReference ptr;

        static ParameterDefinition Clone(string n, ParameterDefinition param)
        {
            ParameterDefinition ret = new ParameterDefinition(n, param.Attributes, param.ParameterType);
            if (param.HasConstant)
            {
                ret.Constant = param.Constant;
                ret.HasConstant = true;
            }
            else
                ret.HasConstant = false;

            if (param.HasMarshalInfo)
            {
                ret.MarshalInfo = param.MarshalInfo;
            }
            return ret;
        }
        static string GetNameO(bool isVirt, MethodReference mbr)
        {
            return ObfuscationHelper.GetNewName((isVirt ? "V>." : "") + mbr.ToString());
        }
        static string GetNameO(ParameterDefinition arg)
        {
            return ObfuscationHelper.GetNewName(arg.Name);
        }
        static string GetSignatureO(MethodReference mbr)
        {
            return ObfuscationHelper.GetNewName(GetSignature(mbr));
        }
        static string GetSignature(MethodReference mbr)
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

        private static string GetId(ModuleDefinition mod, bool isVirt, MethodReference mtd)
        {
            string virt = isVirt ? "\r" : "\n";
            char asmRef = (char)(mod.AssemblyReferences.IndexOf(mtd.DeclaringType.Scope as AssemblyNameReference) + 2);
            return asmRef + virt + Encoding.Unicode.GetString(BitConverter.GetBytes(mtd.Resolve().MetadataToken.ToUInt32()));
        }
        private static void Injection(RuntimeFieldHandle f)
        {
            var fld = System.Reflection.FieldInfo.GetFieldFromHandle(f);

            string n = fld.Name;

            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            if (n[0] != (char)1)
                asm = System.Reflection.Assembly.Load(asm.GetReferencedAssemblies()[(int)n[0] - 2]);
            var mtd = asm.GetModules()[0].ResolveMethod(BitConverter.ToInt32(Encoding.Unicode.GetBytes(n.ToCharArray(), 2, 2), 0)) as System.Reflection.MethodInfo;

            Console.WriteLine(asm.GetName().ToString());
            Console.WriteLine(mtd.DeclaringType.FullName);
            Console.WriteLine(mtd.ToString());

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
                //gen.Emit(System.Reflection.Emit.OpCodes.Castclass, mtd.DeclaringType);
                for (int i = 1; i < arg.Length; i++)
                    gen.Emit(System.Reflection.Emit.OpCodes.Ldarg_S, i);
                gen.Emit((n[1] == '\r') ? System.Reflection.Emit.OpCodes.Callvirt : System.Reflection.Emit.OpCodes.Call, mtd);
                gen.Emit(System.Reflection.Emit.OpCodes.Ret);

                fld.SetValue(null, dm.CreateDelegate(fld.FieldType));
            }
        }
    }
}