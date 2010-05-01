using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Security.Cryptography;
using System.IO;
using System.Globalization;

namespace Confuser.Core.Confusions
{
    class CtorProxyConfusion : StructureConfusion
    {
        public override Priority Priority
        {
            get { return Priority.TypeLevel; }
        }
        public override string Name
        {
            get { return "Constructor Proxy Confusion"; }
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
            foreach (TypeDefinition def in asm.MainModule.Types)
            {
                if (def.Name == "<Module>") continue;
                ProcessMethods(cr, def, asm.MainModule, new Processer(CreateDelegate));
            }
            cr.SubLv();
            cr.Log("</delegates>");

            TypeDefinition mod = asm.MainModule.Types["<Module>"];
            AssemblyDefinition i = AssemblyFactory.GetAssembly(typeof(MtdProxyConfusion).Assembly.Location);
            proxy = i.MainModule.Types["Confuser.Core.Confusions.CtorProxyConfusion"].Methods.GetMethod("Injection")[0];
            proxy = asm.MainModule.Inject(proxy, mod);
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

            cr.Log("<ctors>");
            cr.AddLv();
            CreateCtors(cr, asm.MainModule);
            cr.SubLv();
            cr.Log("</ctors>");

            FinalizeModuleCctor(asm.MainModule);
        }
        public override void PostConfuse(Confuser cr, AssemblyDefinition asm)
        {
            throw new InvalidOperationException();
        }


        private delegate void Processer(Confuser cr, MethodBody Bdy, Instruction Inst, MethodReference MtdRef, ModuleDefinition Mod);

        MethodDefinition proxy;
        private class Context { public MethodBody bdy; public Instruction inst; public FieldDefinition fld; public TypeDefinition dele; public MethodReference mtdRef;}
        List<Context> txts;
        private void ProcessMethods(Confuser cr, TypeDefinition def, ModuleDefinition mod, Processer d)
        {
            foreach (TypeDefinition t in def.NestedTypes)
            {
                ProcessMethods(cr, t, mod, d);
            }
            foreach (MethodDefinition mtd in def.Constructors)
            {
                ProcessMethod(cr, mtd, mod, d);
            }
            foreach (MethodDefinition mtd in def.Methods)
            {
                ProcessMethod(cr, mtd, mod, d);
            }
        }
        private void ProcessMethod(Confuser cr, MethodDefinition mtd, ModuleDefinition mod, Processer d)
        {
            if (!mtd.HasBody || mtd.DeclaringType.FullName == "<Module>") return;

            InstructionCollection insts = mtd.Body.Instructions;
            CilWorker wkr = mtd.Body.CilWorker;
            for (int i = 0; i < insts.Count; i++)
            {
                if (insts[i].OpCode.Code == Code.Newobj &&
                    !(insts[i].Operand as MethodReference).DeclaringType.Resolve().IsInterface &&
                    !(insts[i].Operand as MethodReference).DeclaringType.Resolve().HasGenericParameters)
                {
                    d(cr, mtd.Body, insts[i], insts[i].Operand as MethodReference, mod);
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
            if (MtdRef.DeclaringType.Resolve().BaseType.FullName == "System.MulticastDelegate" ||
                MtdRef.DeclaringType.Resolve().BaseType.FullName == "System.Delegate")
                return;

            Context txt = new Context();
            txt.inst = Inst;
            txt.bdy = Bdy;
            txt.mtdRef = MtdRef;
            if (Mod.Types[GetSignatureO(MtdRef)] == null)
            {
                txt.dele = new TypeDefinition(GetSignatureO(MtdRef), "", TypeAttributes.NotPublic | TypeAttributes.Sealed, mcd);
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
                txt.dele.Constructors.Add(cctor);

                MethodDefinition invoke = new MethodDefinition("Invoke", 0, MtdRef.DeclaringType);
                invoke.IsRuntime = true;
                invoke.HasThis = true;
                invoke.IsHideBySig = true;
                invoke.IsVirtual = true;
                invoke.IsPublic = true;

                for (int i = 0; i < MtdRef.Parameters.Count; i++)
                {
                    invoke.Parameters.Add(new ParameterDefinition(GetNameO(MtdRef.Parameters[i]), i, MtdRef.Parameters[i].Attributes, MtdRef.Parameters[i].ParameterType));
                }
                txt.dele.Methods.Add(invoke);

                cr.Log("<delegate sig='" + GetSignature(MtdRef) + "'/>");
            }
            else
            {
                txt.dele = Mod.Types[GetSignatureO(MtdRef)];
            }
            txts.Add(txt);
        }
        private void CreateFieldBridges(Confuser cr, ModuleDefinition Mod)
        {
            foreach (Context txt in txts)
            {
                ////////////////Field
                if ((txt.fld = Mod.Types["<Module>"].Fields.GetField(" " + GetNameO(txt.mtdRef))) == null)
                {
                    txt.fld = new FieldDefinition(" " + GetNameO(txt.mtdRef), txt.dele, FieldAttributes.Static | FieldAttributes.Assembly);
                    Mod.Types["<Module>"].Fields.Add(txt.fld);
                }
                ////////////////Bridge
                MethodDefinition bdge;
                if (Mod.Types["<Module>"].Methods.GetMethod(GetNameO(txt.mtdRef)).Length == 0)
                {
                    bdge = new MethodDefinition(GetNameO(txt.mtdRef), MethodAttributes.Static | MethodAttributes.Assem, txt.mtdRef.DeclaringType);
                    for (int i = 0; i < txt.mtdRef.Parameters.Count; i++)
                    {
                        bdge.Parameters.Add(new ParameterDefinition(GetNameO(txt.mtdRef.Parameters[i]), i + 1, txt.mtdRef.Parameters[i].Attributes, txt.mtdRef.Parameters[i].ParameterType));
                    }
                    {
                        CilWorker wkr = bdge.Body.CilWorker;
                        wkr.Emit(OpCodes.Ldsfld, txt.fld);
                        for (int i = 0; i < bdge.Parameters.Count; i++)
                        {
                            wkr.Emit(OpCodes.Ldarg, bdge.Parameters[i]);
                        }
                        wkr.Emit(OpCodes.Call, txt.dele.Methods.GetMethod("Invoke")[0]);
                        wkr.Emit(OpCodes.Ret);
                    }
                    Mod.Types["<Module>"].Methods.Add(bdge);
                }
                else
                {
                    bdge = Mod.Types["<Module>"].Methods.GetMethod(GetNameO(txt.mtdRef))[0];
                }

                ////////////////Replace
                txt.inst.OpCode = OpCodes.Call;
                txt.inst.Operand = bdge;

                cr.Log("<ref sig='" + txt.mtdRef.ToString() + "'/>");
            }
        }
        private void CreateCtors(Confuser cr, ModuleDefinition Mod)
        {
            foreach (Context txt in txts)
            {
                string id = GetId(txt.mtdRef.Resolve());
                ////////////////Cctor
                MethodDefinition cctor = Mod.Types["<Module>"].Constructors[0];
                {
                    CilWorker wkr = cctor.Body.CilWorker;
                    wkr.Emit(OpCodes.Ldstr, id);
                    wkr.Emit(OpCodes.Ldtoken, txt.fld);
                    wkr.Emit(OpCodes.Call, proxy);
                }
                cr.Log("<dat id='" + id + "'/>");
            }
        }

        private void InitModuleCctor(ModuleDefinition mod)
        {
            MethodDefinition cctor = mod.Types["<Module>"].Constructors[0];
            cctor.Body.Instructions.RemoveAt(cctor.Body.Instructions.Count - 1);
        }
        private void FinalizeModuleCctor(ModuleDefinition mod)
        {
            MethodDefinition cctor = mod.Types["<Module>"].Constructors[0];
            cctor.Body.CilWorker.Emit(OpCodes.Ret);
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
            Random rand = new Random(arg.Name.GetHashCode());
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
            int sen = mbr.GetSentinel();
            StringBuilder sig = new StringBuilder();
            sig.Append(mbr.ReturnType.ReturnType.FullName);
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
                    if (i == sen)
                    {
                        sig.Append("...,");
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

        private string GetId(MethodDefinition mtd)
        {
            MemoryStream ret = new MemoryStream();
            using (BinaryWriter wtr = new BinaryWriter(ret))
            {
                byte[] bs;
                AssemblyNameReference asm;
                if (mtd.DeclaringType.Scope is AssemblyNameReference)
                    asm = mtd.DeclaringType.Scope as AssemblyNameReference;
                else if (mtd.DeclaringType.Scope is ModuleDefinition)
                    asm = (mtd.DeclaringType.Scope as ModuleDefinition).Assembly.Name;
                else
                    throw new InvalidOperationException();

                bs = Encoding.UTF8.GetBytes(asm.Name);
                wtr.Write((byte)bs.Length);
                for (int i = 0; i < bs.Length; i++)
                {
                    wtr.Write((byte)(bs[i] ^ bs.Length));
                }

                wtr.Write((ushort)asm.Version.Major);
                wtr.Write((ushort)asm.Version.Minor);
                wtr.Write((ushort)asm.Version.Build);
                wtr.Write((ushort)asm.Version.Revision);

                bs = Encoding.UTF8.GetBytes(asm.Culture);
                wtr.Write((byte)bs.Length);
                for (int i = 0; i < bs.Length; i++)
                {
                    wtr.Write((byte)(bs[i] ^ bs.Length));
                }

                if (asm.PublicKeyToken == null)
                    wtr.Write(false);
                else
                {
                    wtr.Write(true);
                    for (int i = 0; i < 8; i++)
                        wtr.Write(asm.PublicKeyToken[i]);
                }
                wtr.Write((int)mtd.MetadataToken.ToUInt());
            }
            return Convert.ToBase64String(ret.ToArray());
        }
        private static void Injection(string id, RuntimeFieldHandle f)
        {
            System.Reflection.FieldInfo fld = System.Reflection.FieldInfo.GetFieldFromHandle(f);

            string name = "";
            Version ver;
            string cult = "";
            byte[] pkt;
            int tkn;
            using (BinaryReader rdr = new BinaryReader(new MemoryStream(Convert.FromBase64String(id))))
            {
                byte b = rdr.ReadByte();
                byte[] tmp = new byte[b];
                for (int i = 0; i < b; i++)
                {
                    tmp[i] = (byte)(rdr.ReadByte() ^ b);
                }
                name = Encoding.UTF8.GetString(tmp);

                ver = new Version(rdr.ReadUInt16(), rdr.ReadUInt16(), rdr.ReadUInt16(), rdr.ReadUInt16());

                b = rdr.ReadByte();
                tmp = new byte[b];
                for (int i = 0; i < b; i++)
                {
                    tmp[i] = (byte)(rdr.ReadByte() ^ b);
                }
                cult = Encoding.UTF8.GetString(tmp);

                if (rdr.ReadBoolean())
                    pkt = rdr.ReadBytes(8);
                else
                    pkt = null;

                tkn = rdr.ReadInt32();
            }

            var n = new System.Reflection.AssemblyName();
            n.Name = name;
            n.Version = ver;
            n.CultureInfo = CultureInfo.GetCultureInfo(cult);
            n.SetPublicKeyToken(pkt);
            var asm = System.Reflection.Assembly.Load(n);
            var mtd = asm.GetModules()[0].ResolveMethod(tkn) as System.Reflection.ConstructorInfo;

            var args = mtd.GetParameters();
            Type[] arg = new Type[args.Length];
            for (int i = 0; i < args.Length; i++)
                arg[i] = args[i].ParameterType;

            System.Reflection.Emit.DynamicMethod dm = new System.Reflection.Emit.DynamicMethod("", mtd.DeclaringType, arg, mtd.DeclaringType, true);
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
    }
}
