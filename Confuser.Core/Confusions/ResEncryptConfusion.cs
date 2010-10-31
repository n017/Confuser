using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using System.IO.Compression;
using System.Collections.Specialized;

namespace Confuser.Core.Confusions
{
    public class ResEncryptConfusion : IConfusion
    {
        class Phase1 : StructurePhase
        {
            public Phase1(ResEncryptConfusion rc) { this.rc = rc; }
            ResEncryptConfusion rc;
            public override IConfusion Confusion
            {
                get { return rc; }
            }
            public override int PhaseID
            {
                get { return 1; }
            }
            public override Priority Priority
            {
                get { return Priority.AssemblyLevel; }
            }
            public override bool WholeRun
            {
                get { return true; }
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
                rc.dats = new List<KeyValuePair<string, byte[]>>();

                TypeDefinition modType = mod.GetType("<Module>");

                AssemblyDefinition i = AssemblyDefinition.ReadAssembly(typeof(Iid).Assembly.Location);
                rc.reso = i.MainModule.GetType("Encryptions").Methods.FirstOrDefault(mtd => mtd.Name == "Resources");
                rc.reso = CecilHelper.Inject(mod, rc.reso);
                modType.Methods.Add(rc.reso);
                byte[] n = Guid.NewGuid().ToByteArray();
                rc.reso.Name = Encoding.UTF8.GetString(n);
                rc.reso.IsAssembly = true;
                AddHelper(rc.reso, HelperAttribute.NoInjection);

                n = Guid.NewGuid().ToByteArray();
                rc.key0 = n[0];
                rc.key1 = n[1];

                n = Guid.NewGuid().ToByteArray();

                rc.reso.Body.SimplifyMacros();
                foreach (Instruction inst in rc.reso.Body.Instructions)
                {
                    if ((inst.Operand as string) == "PADDINGPADDINGPADDING")
                        inst.Operand = Encoding.UTF8.GetString(n);
                    else if (inst.Operand is int && (int)inst.Operand == 0x11)
                        inst.Operand = (int)rc.key0;
                    else if (inst.Operand is int && (int)inst.Operand == 0x22)
                        inst.Operand = (int)rc.key1;
                    else if (inst.Operand is TypeReference && (inst.Operand as TypeReference).FullName == "System.Exception")
                        inst.Operand = modType;
                }
                rc.reso.Body.OptimizeMacros();
                rc.reso.Body.ComputeOffsets();

                rc.resId = Encoding.UTF8.GetString(n);

                MethodDefinition cctor = mod.GetType("<Module>").GetStaticConstructor();
                MethodBody bdy = cctor.Body as MethodBody;
                bdy.Instructions.RemoveAt(bdy.Instructions.Count - 1);
                ILProcessor psr = bdy.GetILProcessor();
                psr.Emit(OpCodes.Call, mod.Import(typeof(AppDomain).GetProperty("CurrentDomain").GetGetMethod()));
                psr.Emit(OpCodes.Ldnull);
                psr.Emit(OpCodes.Ldftn, rc.reso);
                psr.Emit(OpCodes.Newobj, mod.Import(typeof(ResolveEventHandler).GetConstructor(new Type[] { typeof(object), typeof(IntPtr) })));
                psr.Emit(OpCodes.Callvirt, mod.Import(typeof(AppDomain).GetEvent("ResourceResolve").GetAddMethod()));
                psr.Emit(OpCodes.Ret);
            }
        }
        class MdPhase : MetadataPhase
        {
            public MdPhase(ResEncryptConfusion rc) { this.rc = rc; }
            ResEncryptConfusion rc;
            public override IConfusion Confusion
            {
                get { return rc; }
            }
            public override int PhaseID
            {
                get { return 1; }
            }
            public override Priority Priority
            {
                get { return Priority.MetadataLevel; }
            }
            public override void Process(NameValueCollection parameters, MetadataProcessor.MetadataAccessor accessor)
            {
                ModuleDefinition mod = accessor.Module;
                for (int i = 0; i < mod.Resources.Count; i++)
                    if (mod.Resources[i] is EmbeddedResource)
                    {
                        rc.dats.Add(new KeyValuePair<string, byte[]>(mod.Resources[i].Name, (mod.Resources[i] as EmbeddedResource).GetResourceData()));
                        mod.Resources.RemoveAt(i);
                        i--;
                    }

                if (rc.dats.Count > 0)
                {
                    MemoryStream ms = new MemoryStream();
                    BinaryWriter wtr = new BinaryWriter(new DeflateStream(ms, CompressionMode.Compress, true));

                    MemoryStream ms1 = new MemoryStream();
                    BinaryWriter wtr1 = new BinaryWriter(new DeflateStream(ms1, CompressionMode.Compress, true));
                    byte[] asm = GetAsm();
                    wtr1.Write(asm.Length);
                    wtr1.Write(asm);
                    wtr1.BaseStream.Dispose();

                    byte[] dat = Encrypt(ms1.ToArray(), rc.key0, rc.key1);
                    wtr.Write(dat.Length);
                    wtr.Write(dat);
                    wtr.BaseStream.Dispose();

                    mod.Resources.Add(new EmbeddedResource(rc.resId, ManifestResourceAttributes.Private, ms.ToArray()));
                }
            }
            byte[] GetAsm()
            {
                AssemblyDefinition asm = AssemblyDefinition.CreateAssembly(new AssemblyNameDefinition(ObfuscationHelper.GetNewName(Guid.NewGuid().ToString()), new Version()), ObfuscationHelper.GetNewName(Guid.NewGuid().ToString()), ModuleKind.Dll);
                foreach(KeyValuePair<string, byte[]> i in rc.dats)
                asm.MainModule.Resources.Add(new EmbeddedResource(i.Key, ManifestResourceAttributes.Public, i.Value));
                MemoryStream ms = new MemoryStream();
                asm.Write(ms);
                return ms.ToArray();
            }
        }

        Phase[] phases;
        public Phase[] Phases
        {
            get
            {
                if (phases == null) phases = new Phase[] { new Phase1(this), new MdPhase(this) };
                return phases;
            }
        }
        public string ID
        {
            get { return "res encrypt"; }
        }
        public string Name
        {
            get { return "Resource Encryption Confusion"; }
        }
        public string Description
        {
            get { return "This will encrypt the embededd resources in the assembly and dynamically decrypt in runtime."; }
        }
        public Target Target
        {
            get { return Target.Assembly; }
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
            get { return true; }
        }
        public Behaviour Behaviour
        {
            get { return Behaviour.Inject | Behaviour.AlterCode | Behaviour.Encrypt; }
        }


        List<KeyValuePair<string, byte[]>> dats;

        string resId;
        byte key0;
        byte key1;
        MethodDefinition reso;

        static byte[] Encrypt(byte[] res, byte key0, byte key1)
        {
            byte[] ret = new byte[res.Length * 2];
            for (int i = 0; i < res.Length; i++)
            {
                ret[i * 2] = (byte)((res[i] % key1) ^ key0);
                ret[i * 2 + 1] = (byte)((res[i] / key1) ^ key0);
            }
            return ret;
        }
        static byte[] Decrypt(byte[] res, byte key0, byte key1)
        {
            byte[] ret = new byte[res.Length / 2];
            for (int i = 0; i < res.Length; i += 2)
            {
                ret[i / 2] = (byte)((res[i + 1] ^ key0) * key1 + (res[i] ^ key0));
            }
            return ret;
        }
    }
}
