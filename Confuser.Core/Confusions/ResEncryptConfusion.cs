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
                get { return Priority.MetadataLevel; }
            }
            public override bool WholeRun
            {
                get { return true; }
            }

            AssemblyDefinition asm;
            public override void Initialize(AssemblyDefinition asm)
            {
                this.asm = asm;
            }
            public override void DeInitialize()
            {
                //
            }
            public override void Process(ConfusionParameter parameter)
            {
                rc.dats = new List<KeyValuePair<string, byte[]>>();

                Random rand = new Random();
                TypeDefinition mod = asm.MainModule.GetType("<Module>");

                AssemblyDefinition i = AssemblyDefinition.ReadAssembly(typeof(StringConfusion).Assembly.Location);
                rc.reso = i.MainModule.GetType(typeof(ResEncryptConfusion).FullName).Methods.FirstOrDefault(mtd => mtd.Name == "Injection");
                rc.reso = CecilHelper.Inject(asm.MainModule, rc.reso);
                mod.Methods.Add(rc.reso);
                byte[] n = new byte[0x10]; rand.NextBytes(n);
                rc.reso.Name = Encoding.UTF8.GetString(n);
                rc.reso.IsAssembly = true;

                rc.key0 = (byte)(rand.NextDouble() * byte.MaxValue);
                rc.key1 = (byte)(rand.NextDouble() * byte.MaxValue);

                rand.NextBytes(n);

                rc.reso.Body.SimplifyMacros();
                foreach (Instruction inst in rc.reso.Body.Instructions)
                {
                    if ((inst.Operand as string) == "PADDINGPADDINGPADDING")
                        inst.Operand = Encoding.UTF8.GetString(n);
                    else if (inst.Operand is int && (int)inst.Operand == 0x111)
                        inst.Operand = rc.key0;
                    else if (inst.Operand is int && (int)inst.Operand == 0x222)
                        inst.Operand = rc.key1;
                }
                rc.reso.Body.OptimizeMacros();
                rc.reso.Body.ComputeOffsets();

                rc.resId = Encoding.UTF8.GetString(n);
            }
        }
        class MetadataPhase : AdvancedPhase
        {
            public MetadataPhase(ResEncryptConfusion rc) { this.rc = rc; }
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
            public override bool WholeRun
            {
                get { return true; }
            }
            public override void Initialize(AssemblyDefinition asm)
            {
                //
            }
            public override void DeInitialize()
            {
                //
            }
            public override void Process(MetadataProcessor.MetadataAccessor accessor)
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
                    Random rand = new Random();
                    MemoryStream ms = new MemoryStream();
                    using (BinaryWriter wtr = new BinaryWriter(new DeflateStream(ms, CompressionMode.Compress)))
                    {
                        byte[] key = new byte[0x10];
                        rand.NextBytes(key);
                        wtr.Write(key.Length); wtr.Write(key);
                        foreach (KeyValuePair<string, byte[]> i in rc.dats)
                        {
                            BitArray nArr = new BitArray(Encoding.UTF8.GetBytes(i.Key));
                            BitArray keyArr = new BitArray(key);
                            byte[] n = new byte[nArr.Length / 8];
                            nArr.Xor(keyArr).CopyTo(n, 0);
                            wtr.Write(n.Length); wtr.Write(n);
                            wtr.Write(i.Value.Length); wtr.Write(i.Value);
                        }
                        wtr.Write(0);
                    }
                    mod.Resources.Add(new EmbeddedResource(rc.resId, ManifestResourceAttributes.Private, ms.ToArray()));
                }
            }
        }

        Phase[] phases;
        public Phase[] Phases
        {
            get
            {
                if (phases == null) phases = new Phase[] { new Phase1(this), new MetadataPhase(this) };
                return phases;
            }
        }
        public string ID
        {
            get { return "resource encrypt"; }
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

        static System.Reflection.Assembly Injection(object sender, ResolveEventArgs args)
        {
            if (args == null)
            {
                AppDomain.CurrentDomain.ResourceResolve += Delegate.CreateDelegate(typeof(ResolveEventHandler), new StackFrame(0).GetMethod() as System.Reflection.MethodInfo) as ResolveEventHandler;
                return null;
            }

            System.Reflection.Assembly asm = System.Reflection.Assembly.GetCallingAssembly();
            Stream str = asm.GetManifestResourceStream("PADDINGPADDINGPADDING");
            using (BinaryReader rdr = new BinaryReader(new DeflateStream(str, CompressionMode.Decompress)))
            {
                int keyLen = rdr.ReadInt32();
                byte[] key = rdr.ReadBytes(keyLen);

                int nLen;
                while ((nLen = rdr.ReadInt32()) != 0)
                {
                    byte[] nDat = rdr.ReadBytes(nLen);
                    BitArray nArr = new BitArray(nDat);
                    BitArray keyArr = new BitArray(key);
                    byte[] n = new byte[nDat.Length];
                    nArr.Xor(keyArr).CopyTo(n, 0);
                    int datSize = rdr.ReadInt32();
                    byte[] dat = rdr.ReadBytes(datSize);

                    if (Encoding.UTF8.GetString(n) == args.Name)
                    {
                        int key0 = 0x111;
                        int key1 = 0x222;
                        /////////////////////////////////
                        byte[] final = new byte[dat.Length / 2];
                        for (int i = 0; i < dat.Length; i += 2)
                        {
                            final[i / 2] = (byte)((dat[i + 1] ^ key0) * key1 + (dat[i] ^ key0));
                        }
                        /////////////////////////////////
                        System.Reflection.Emit.AssemblyBuilder asmB = AppDomain.CurrentDomain.DefineDynamicAssembly(new System.Reflection.AssemblyName(Path.GetRandomFileName()), System.Reflection.Emit.AssemblyBuilderAccess.Run);
                        System.Reflection.Emit.ModuleBuilder modB = asmB.DefineDynamicModule(Path.GetRandomFileName());
                        modB.DefineManifestResource(args.Name, new MemoryStream(final), System.Reflection.ResourceAttributes.Public);
                        asm = asmB;
                        break;
                    }
                }
            }
            return asm;
        }
    }
}
