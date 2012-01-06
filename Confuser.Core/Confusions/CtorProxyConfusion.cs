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
using System.Collections.Specialized;

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

            ModuleDefinition mod;
            public override void Initialize(ModuleDefinition mod)
            {
                this.mod = mod;

                cc.mcd = mod.Import(typeof(MulticastDelegate));
                cc.v = mod.TypeSystem.Void;
                cc.obj = mod.TypeSystem.Object;
                cc.ptr = mod.TypeSystem.IntPtr;

                cc.txts = new List<Context>();
                cc.delegates = new Dictionary<string, TypeDefinition>();
                cc.fields = new Dictionary<string, FieldDefinition>();
                cc.bridges = new Dictionary<string, MethodDefinition>();
            }
            public override void DeInitialize()
            {
                TypeDefinition modType = mod.GetType("<Module>");
                AssemblyDefinition i = AssemblyDefinition.ReadAssembly(typeof(Iid).Assembly.Location);
                cc.proxy = i.MainModule.GetType("Proxies").Methods.FirstOrDefault(mtd => mtd.Name == "CtorProxy");
                cc.proxy = CecilHelper.Inject(mod, cc.proxy);
                modType.Methods.Add(cc.proxy);
                cc.proxy.IsAssembly = true;
                cc.proxy.Name = ObfuscationHelper.GetNewName("Proxy" + Guid.NewGuid().ToString());
                AddHelper(cc.proxy, 0);

                cc.key = new Random().Next();
                foreach (Instruction inst in cc.proxy.Body.Instructions)
                    if (inst.Operand is int && (int)inst.Operand == 0x12345678)
                    { inst.Operand = cc.key; break; }
            }

            public override void Process(ConfusionParameter parameter)
            {
                IList<IAnnotationProvider> targets = parameter.Target as IList<IAnnotationProvider>;
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
                            CreateDelegate(mtd.Body, inst, inst.Operand as MethodReference, mod);
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
                    CreateFieldBridge(mod, cc.txts[i]);
                    if (i % interval == 0 || i == cc.txts.Count - 1)
                        progresser.SetProgress((i + 1) / total);
                }
            }

            private void CreateDelegate(MethodBody Bdy, Instruction Inst, MethodReference MtdRef, ModuleDefinition Mod)
            {
                //Limitation
                TypeDefinition tmp = MtdRef.DeclaringType.Resolve();
                if (tmp != null && tmp.BaseType != null &&
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

                    MethodDefinition cctor = new MethodDefinition(".cctor", MethodAttributes.Private | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName | MethodAttributes.Static, cc.v);
                    cctor.Body = new MethodBody(cctor);
                    txt.dele.Methods.Add(cctor);

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
                    txt.dele.Fields.Add(txt.fld);
                    cc.fields.Add(fldId, txt.fld);
                }
                ////////////////Bridge
                string bridgeId = GetNameO(txt.mtdRef);
                MethodDefinition bdge;
                if (!cc.bridges.TryGetValue(bridgeId, out bdge))
                {
                    bdge = new MethodDefinition(bridgeId, MethodAttributes.Static | MethodAttributes.Assembly, txt.mtdRef.DeclaringType);
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
                    txt.dele.Methods.Add(bdge);
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
                double total = cc.txts.Count;
                int interval = 1;
                if (total > 1000)
                    interval = (int)total / 100;
                for (int i = 0; i < cc.txts.Count; i++)
                {
                    Context txt = cc.txts[i];
                    txt.fld.Name = GetId(txt.mtdRef.Module, txt.mtdRef);

                    if (!(txt.fld as IAnnotationProvider).Annotations.Contains("CtorProxyCtored"))
                    {
                        ILProcessor psr = txt.dele.GetStaticConstructor().Body.GetILProcessor();
                        psr.Emit(OpCodes.Ldtoken, txt.fld);
                        psr.Emit(OpCodes.Call, cc.proxy);
                        (txt.fld as IAnnotationProvider).Annotations["CtorProxyCtored"] = true;
                    }

                    if (i % interval == 0 || i == cc.txts.Count - 1)
                        progresser.SetProgress((i + 1) / total);
                }

                total = cc.delegates.Count;
                interval = 1;
                if (total > 1000)
                    interval = (int)total / 100;
                IEnumerator<TypeDefinition> etor = cc.delegates.Values.GetEnumerator();
                etor.MoveNext();
                for (int i = 0; i < cc.delegates.Count; i++)
                {
                    etor.Current.GetStaticConstructor().Body.GetILProcessor().Emit(OpCodes.Ret);
                    etor.MoveNext();
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
        class MdPhase : MetadataPhase
        {
            CtorProxyConfusion cc;
            public MdPhase(CtorProxyConfusion cc) { this.cc = cc; }
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
                foreach (Context txt in cc.txts)
                {
                    if (txt.fld.Name[0] != '\0') continue;
                    MetadataToken tkn = accessor.LookupToken(txt.mtdRef);
                    string str = Convert.ToBase64String(BitConverter.GetBytes(tkn.ToInt32() ^ cc.key));
                    StringBuilder sb = new StringBuilder(str.Length);
                    for (int i = 0; i < str.Length; i++)
                        sb.Append((char)((byte)str[i] ^ i));
                    txt.fld.Name = sb.ToString();
                }
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
            get { return "This confusion create proxies between references of constructors and methods code."; }
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
        int key;
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
            if (mbr.Resolve() != null && mbr.Resolve().IsVirtual)
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

        static string GetId(ModuleDefinition mod, MethodReference mtd)
        {
            char asmRef = (char)(mod.AssemblyReferences.IndexOf(mtd.DeclaringType.Scope as AssemblyNameReference) + 2);
            return "\0" + asmRef + mtd.ToString();
        }
    }
}
