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
    public class CtorProxyConfusion : IConfusion
    {
        class Phase1 : StructurePhase, IProgressProvider
        {
            CtorProxyConfusion cc;
            public Phase1(CtorProxyConfusion cc) { this.cc = cc; }
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

            public override bool WholeRun
            {
                get { return false; }
            }

            AssemblyDefinition asm;
            public override void Initialize(AssemblyDefinition asm)
            {
                this.asm = asm;

                cc.mcd = asm.MainModule.Import(typeof(MulticastDelegate));
                cc.v = asm.MainModule.Import(typeof(void));
                cc.obj = asm.MainModule.Import(typeof(object));
                cc.ptr = asm.MainModule.Import(typeof(IntPtr));

                cc.txts = new List<Context>();
                cc.delegates = new Dictionary<string, TypeDefinition>();
                cc.fields = new Dictionary<string, FieldDefinition>();
                cc.bridges = new Dictionary<string, MethodDefinition>();
            }
            public override void DeInitialize()
            {
                TypeDefinition mod = asm.MainModule.GetType("<Module>");
                AssemblyDefinition i = AssemblyDefinition.ReadAssembly(typeof(CtorProxyConfusion).Assembly.Location);
                cc.proxy = i.MainModule.GetType("Confuser.Core.Confusions.CtorProxyConfusion").Methods.FirstOrDefault(mtd => mtd.Name == "Injection");
                cc.proxy = CecilHelper.Inject(asm.MainModule, cc.proxy);
                mod.Methods.Add(cc.proxy);
                cc.proxy.IsAssembly = true;
                cc.proxy.Name = ObfuscationHelper.GetNewName("Proxy" + Guid.NewGuid().ToString());
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
                        if (inst.OpCode.Code == Code.Newobj &&
                            !((inst.Operand as MethodReference).DeclaringType is GenericInstanceType) &&
                            !(inst.Operand is GenericInstanceMethod))
                        {
                            CreateDelegate(mtd.Body, inst, inst.Operand as MethodReference, asm.MainModule);
                        }
                    }
                    progresser.SetProgress((i + 1) / (double)targets.Count);
                }
                double total = cc.txts.Count;
                int interval = 1;
                if (total > 1000)
                    interval = (int)total / 100;
                for (int i = 0; i < cc.txts.Count; i++)
                {
                    CreateFieldBridge(asm.MainModule, cc.txts[i]);
                    if (i % interval == 0 || i == cc.txts.Count - 1)
                        progresser.SetProgress((i + 1) / total);
                }
            }

            private void CreateDelegate(MethodBody Bdy, Instruction Inst, MethodReference MtdRef, ModuleDefinition Mod)
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
                string sign = GetSignatureO(MtdRef);
                if (!cc.delegates.TryGetValue(sign, out txt.dele))
                {
                    txt.dele = new TypeDefinition("", sign, TypeAttributes.NotPublic | TypeAttributes.Sealed, cc.mcd);
                    Mod.Types.Add(txt.dele);

                    MethodDefinition ctor = new MethodDefinition(".ctor", 0, cc.v);
                    ctor.IsRuntime = true;
                    ctor.HasThis = true;
                    ctor.IsHideBySig = true;
                    ctor.IsRuntimeSpecialName = true;
                    ctor.IsSpecialName = true;
                    ctor.IsPublic = true;
                    ctor.Parameters.Add(new ParameterDefinition(cc.obj));
                    ctor.Parameters.Add(new ParameterDefinition(cc.ptr));
                    txt.dele.Methods.Add(ctor);

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
                    cc.delegates.Add(sign, txt.dele);
                }
                cc.txts.Add(txt);
            }
            private void CreateFieldBridge(ModuleDefinition Mod, Context txt)
            {
                ////////////////Field
                string fldId = GetId(Mod, txt.mtdRef);
                if (!cc.fields.TryGetValue(fldId, out txt.fld))
                {
                    txt.fld = new FieldDefinition(fldId, FieldAttributes.Static | FieldAttributes.Assembly, txt.dele);
                    Mod.GetType("<Module>").Fields.Add(txt.fld);
                    cc.fields.Add(fldId, txt.fld);
                }
                ////////////////Bridge
                string bridgeId = GetNameO(txt.mtdRef);
                MethodDefinition bdge;
                if (!cc.bridges.TryGetValue(bridgeId, out bdge))
                {
                    bdge = new MethodDefinition(bridgeId, MethodAttributes.Static | MethodAttributes.Assem, txt.mtdRef.DeclaringType);
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
                    cc.bridges.Add(bridgeId, bdge);
                }

                ////////////////Replace
                txt.inst.OpCode = OpCodes.Call;
                txt.inst.Operand = bdge;
            }

            IProgresser progresser;
            public void SetProgresser(IProgresser progresser)
            {
                this.progresser = progresser;
            }
        }
        class Phase2 : StructurePhase, IProgressProvider
        {
            CtorProxyConfusion cc;
            public Phase2(CtorProxyConfusion cc) { this.cc = cc; }
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

                double total = cc.txts.Count;
                int interval = 1;
                if (total > 1000)
                    interval = (int)total / 100;
                for (int i = 0; i < cc.txts.Count; i++)
                {
                    ////////////////Cctor
                    cc.txts[i].fld.Name = GetId(asm.MainModule, cc.txts[i].mtdRef);
                    wkr.Emit(OpCodes.Ldtoken, cc.txts[i].fld);
                    wkr.Emit(OpCodes.Call, cc.proxy);
                    if (i % interval == 0 || i == cc.txts.Count - 1)
                        progresser.SetProgress((i + 1) / total);
                }
            }

            IProgresser progresser;
            public void SetProgresser(IProgresser progresser)
            {
                this.progresser = progresser;
            }
        }


        public string ID
        {
            get { return "ctor proxy"; }
        }
        public string Name
        {
            get { return "Constructor Proxy Confusion"; }
        }
        public string Description
        {
            get
            {
                return @"This confusion create proxies between references of constructors and methods code.
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

        Dictionary<string, TypeDefinition> delegates;
        Dictionary<string, FieldDefinition> fields;
        Dictionary<string, MethodDefinition> bridges;
        MethodDefinition proxy;
        private class Context { public MethodBody bdy; public Instruction inst; public FieldDefinition fld; public TypeDefinition dele; public MethodReference mtdRef;}
        List<Context> txts;
        TypeReference mcd;
        TypeReference v;
        TypeReference obj;
        TypeReference ptr;

        static string GetNameO(MethodReference mbr)
        {
            return ObfuscationHelper.GetNewName(mbr.ToString());
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

        private static string GetId(ModuleDefinition mod, MethodReference mtd)
        {
            char asmRef = (char)(mod.AssemblyReferences.IndexOf(mtd.DeclaringType.Scope as AssemblyNameReference) + 2);
            return asmRef + Encoding.Unicode.GetString(BitConverter.GetBytes(mtd.Resolve().MetadataToken.ToUInt32()));
        }
        private static void Injection(RuntimeFieldHandle f)
        {
            var fld = System.Reflection.FieldInfo.GetFieldFromHandle(f);

            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            if (fld.Name[0] != (char)1)
                asm = System.Reflection.Assembly.Load(asm.GetReferencedAssemblies()[(int)fld.Name[0] - 2]);

            Console.WriteLine(asm.FullName);
            Console.WriteLine(BitConverter.ToInt32(Encoding.Unicode.GetBytes(fld.Name.ToCharArray(), 1, 2), 0));
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
    }
}
