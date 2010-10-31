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
using System.Collections.Specialized;

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

            ModuleDefinition mod;
            public override void Initialize(ModuleDefinition mod)
            {
                this.mod = mod;

                mc.mcd = mod.Import(typeof(MulticastDelegate));
                mc.v = mod.Import(typeof(void));
                mc.obj = mod.Import(typeof(object));
                mc.ptr = mod.Import(typeof(IntPtr));

                mc.txts = new List<Context>();
                mc.delegates = new Dictionary<string, TypeDefinition>();
                mc.fields = new Dictionary<string, FieldDefinition>();
                mc.bridges = new Dictionary<string, MethodDefinition>();
            }
            public override void DeInitialize()
            {
                TypeDefinition modType = mod.GetType("<Module>");
                AssemblyDefinition i = AssemblyDefinition.ReadAssembly(typeof(Iid).Assembly.Location);
                mc.proxy = i.MainModule.GetType("Proxies").Methods.FirstOrDefault(mtd => mtd.Name == "MtdProxy");
                mc.proxy = CecilHelper.Inject(mod, mc.proxy);
                modType.Methods.Add(mc.proxy);
                mc.proxy.IsAssembly = true;
                mc.proxy.Name = ObfuscationHelper.GetNewName("Proxy" + Guid.NewGuid().ToString());
                AddHelper(mc.proxy, 0);
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
                            !((inst.Operand as MethodReference).DeclaringType is GenericInstanceType) &&
                            !(inst.Operand as MethodReference).DeclaringType.Resolve().IsInterface &&
                            (inst.Previous == null || inst.Previous.OpCode.OpCodeType != OpCodeType.Prefix))
                        {
                            CreateDelegate(mtd.Body, inst, inst.Operand as MethodReference, mod);
                        }
                    }
                    progresser.SetProgress((i + 1) / (double)targets.Count);
                }
                double total = mc.txts.Count;
                int interval = 1;
                if (total > 1000)
                    interval = (int)total / 100;
                for (int i = 0; i < mc.txts.Count; i++)
                {
                    CreateFieldBridge(mod, mc.txts[i]);
                    if (i % interval == 0 || i == mc.txts.Count - 1)
                        progresser.SetProgress((i + 1) / total);
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
                string sign = GetSignatureO(MtdRef);
                if (!mc.delegates.TryGetValue(sign, out txt.dele))
                {
                    txt.dele = new TypeDefinition("", sign, TypeAttributes.NotPublic | TypeAttributes.Sealed, mc.mcd);
                    Mod.Types.Add(txt.dele);

                    MethodDefinition cctor = new MethodDefinition(".cctor", MethodAttributes.Private | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName | MethodAttributes.Static, mc.v);
                    cctor.Body = new MethodBody(cctor);
                    txt.dele.Methods.Add(cctor);

                    MethodDefinition ctor = new MethodDefinition(".ctor", 0, mc.v);
                    ctor.IsRuntime = true;
                    ctor.HasThis = true;
                    ctor.IsHideBySig = true;
                    ctor.IsRuntimeSpecialName = true;
                    ctor.IsSpecialName = true;
                    ctor.IsPublic = true;
                    ctor.Parameters.Add(new ParameterDefinition(mc.obj));
                    ctor.Parameters.Add(new ParameterDefinition(mc.ptr));
                    txt.dele.Methods.Add(ctor);

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
                    mc.delegates.Add(sign, txt.dele);
                }
                mc.txts.Add(txt);
            }
            private void CreateFieldBridge(ModuleDefinition Mod, Context txt)
            {
                ////////////////Field
                string fldId = GetId(Mod, txt.inst.OpCode.Name == "callvirt", txt.mtdRef);
                if (!mc.fields.TryGetValue(fldId, out txt.fld))
                {
                    txt.fld = new FieldDefinition(fldId, FieldAttributes.Static | FieldAttributes.Assembly, txt.dele);
                    txt.dele.Fields.Add(txt.fld);
                    mc.fields.Add(fldId, txt.fld);
                }
                ////////////////Bridge
                string bridgeId = GetNameO(txt.inst.OpCode.Name == "callvirt", txt.mtdRef);
                MethodDefinition bdge;
                if (!mc.bridges.TryGetValue(bridgeId, out bdge))
                {
                    bdge = new MethodDefinition(bridgeId, MethodAttributes.Static | MethodAttributes.Assembly, txt.mtdRef.ReturnType);
                    if (txt.mtdRef.HasThis)
                    {
                        bdge.Parameters.Add(new ParameterDefinition(Mod.Import(mc.obj)));

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
                    txt.dele.Methods.Add(bdge);
                    mc.bridges.Add(bridgeId, bdge);
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

            ModuleDefinition mod;
            public override void Initialize(ModuleDefinition mod)
            {
                this.mod = mod;
            }
            public override void DeInitialize()
            {
                //
            }
            public override void Process(ConfusionParameter parameter)
            {
                double total = mc.txts.Count;
                int interval = 1;
                if (total > 1000)
                    interval = (int)total / 100;
                for (int i = 0; i < mc.txts.Count; i++)
                {
                    Context txt = mc.txts[i];
                    txt.fld.Name = GetId(txt.mtdRef.Module, txt.isVirt, txt.mtdRef);

                    if (!(txt.fld as IAnnotationProvider).Annotations.Contains("MtdProxyCtored"))
                    {
                        ILProcessor psr = txt.dele.GetStaticConstructor().Body.GetILProcessor();
                        psr.Emit(OpCodes.Ldtoken, txt.fld);
                        psr.Emit(OpCodes.Call, mc.proxy);
                        (txt.fld as IAnnotationProvider).Annotations["MtdProxyCtored"] = true;
                    }

                    if (i % interval == 0 || i == mc.txts.Count - 1)
                        progresser.SetProgress((i + 1) / total);
                }

                total = mc.delegates.Count;
                interval = 1;
                if (total > 1000)
                    interval = (int)total / 100;
                IEnumerator<TypeDefinition> etor = mc.delegates.Values.GetEnumerator();
                etor.MoveNext();
                for (int i = 0; i < mc.delegates.Count; i++)
                {
                    etor.Current.GetStaticConstructor().Body.GetILProcessor().Emit(OpCodes.Ret);
                    etor.MoveNext();
                    if (i % interval == 0 || i == mc.txts.Count - 1)
                        progresser.SetProgress((i + 1) / total);
                }
            }

            IProgresser progresser;
            void IProgressProvider.SetProgresser(IProgresser progresser)
            {
                this.progresser = progresser;
            }
        }
        class MdPhase : MetadataPhase
        {
            MtdProxyConfusion mc;
            public MdPhase(MtdProxyConfusion mc) { this.mc = mc; }
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

            public override void Process(NameValueCollection parameters, MetadataProcessor.MetadataAccessor accessor)
            {
                foreach (Context txt in mc.txts)
                {
                    MetadataToken tkn = accessor.LookupToken(txt.mtdRef);
                    txt.fld.Name = new string(new char[] { txt.fld.Name[0] , txt.fld.Name[1] }) + Encoding.Unicode.GetString(BitConverter.GetBytes(tkn.ToUInt32()));
                }
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
                return @"This confusion create proxies between references of methods and methods code.";
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
        public bool SupportLateAddition
        {
            get { return false; }
        }
        public Behaviour Behaviour
        {
            get { return Behaviour.Inject | Behaviour.AlterCode | Behaviour.AlterStructure; }
        }

        Phase[] ps;
        public Phase[] Phases
        {
            get
            {
                if (ps == null) ps = new Phase[] { new Phase1(this), new Phase2(this), new MdPhase(this) };
                return ps;
            }
        }

        Dictionary<string, TypeDefinition> delegates;
        Dictionary<string, FieldDefinition> fields;
        Dictionary<string, MethodDefinition> bridges;
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
            return asmRef + virt + mtd.ToString().GetHashCode();
        }
    }
}