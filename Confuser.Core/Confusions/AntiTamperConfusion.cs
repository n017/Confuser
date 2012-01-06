using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Specialized;
using Mono.Cecil;
using Mono.Cecil.Metadata;
using Mono.Cecil.Rocks;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using Mono.Cecil.Cil;
using Mono.Cecil.PE;

//TODO: Implement better version by JIT hooking
namespace Confuser.Core.Confusions
{
    public class AntiTamperConfusion : IConfusion
    {
        class Phase1 : StructurePhase
        {
            AntiTamperConfusion cion;
            public Phase1(AntiTamperConfusion cion) { this.cion = cion; }
            public override Priority Priority
            {
                get { return Priority.AssemblyLevel; }
            }
            public override IConfusion Confusion
            {
                get { return cion; }
            }
            public override int PhaseID
            {
                get { return 1; }
            }
            public override bool WholeRun
            {
                get { return true; }
            }

            public override void Initialize(ModuleDefinition mod)
            {
                this.mod = mod;
                Random rand = new Random();
                byte[] dat = new byte[25];
                rand.NextBytes(dat);
                cion.key0 = BitConverter.ToInt32(dat, 0);
                cion.key1 = BitConverter.ToInt64(dat, 4);
                cion.key2 = BitConverter.ToInt32(dat, 12);
                cion.key3 = BitConverter.ToInt32(dat, 16);
                cion.key4 = BitConverter.ToInt32(dat, 20);
                cion.key5 = dat[24];
            }
            public override void DeInitialize()
            {
                //
            }

            ModuleDefinition mod;
            public override void Process(ConfusionParameter parameter)
            {
                AssemblyDefinition i = AssemblyDefinition.ReadAssembly(typeof(Iid).Assembly.Location);
                cion.root = CecilHelper.Inject(mod, i.MainModule.GetType("AntiTamper"));
                mod.Types.Add(cion.root);
                MethodDefinition cctor = mod.GetType("<Module>").GetStaticConstructor();
                cctor.Body.GetILProcessor().InsertBefore(0, Instruction.Create(OpCodes.Call, cion.root.Methods.FirstOrDefault(mtd => mtd.Name == "Initalize")));

                MethodDefinition init = cion.root.Methods.FirstOrDefault(mtd => mtd.Name == "Initalize");
                foreach (Instruction inst in init.Body.Instructions)
                {
                    if (inst.Operand is int)
                    {
                        switch ((int)inst.Operand)
                        {
                            case 0x11111111:
                                inst.Operand = cion.key0; break;
                            case 0x33333333:
                                inst.Operand = cion.key2; break;
                            case 0x44444444:
                                inst.Operand = cion.key3; break;
                            case 0x55555555:
                                inst.Operand = cion.key4; break;
                        }
                    }
                    else if (inst.Operand is long && (long)inst.Operand == 0x2222222222222222)
                        inst.Operand = cion.key1;
                }
                MethodDefinition dec = cion.root.Methods.FirstOrDefault(mtd => mtd.Name == "Decrypt");
                foreach (Instruction inst in dec.Body.Instructions)
                    if (inst.Operand is int && (int)inst.Operand == 0x11111111)
                        inst.Operand = (int)cion.key5;

                cion.root.Name = ObfuscationHelper.GetNewName("AntiTamperModule" + Guid.NewGuid().ToString());
                cion.root.Namespace = "";
                AddHelper(cion.root, HelperAttribute.NoInjection);
                foreach (MethodDefinition mtdDef in cion.root.Methods)
                {
                    mtdDef.Name = ObfuscationHelper.GetNewName(mtdDef.Name + Guid.NewGuid().ToString());
                    AddHelper(mtdDef, HelperAttribute.NoInjection);
                }
            }
        }
        class Phase2 : StructurePhase
        {
            AntiTamperConfusion cion;
            public Phase2(AntiTamperConfusion cion) { this.cion = cion; }
            public override Priority Priority
            {
                get { return Priority.AssemblyLevel; }
            }
            public override IConfusion Confusion
            {
                get { return cion; }
            }
            public override int PhaseID
            {
                get { return 2; }
            }
            public override bool WholeRun
            {
                get { return true; }
            }

            public override void Initialize(ModuleDefinition mod)
            {
                this.mod = mod;
            }
            public override void DeInitialize()
            {
                //
            }

            ModuleDefinition mod;
            public override void Process(ConfusionParameter parameter)
            {
                Queue<TypeDefinition> q = new Queue<TypeDefinition>();
                cion.excludes = new List<int>();
                cion.excludes.Add((int)mod.GetType("<Module>").GetStaticConstructor().MetadataToken.RID - 1);
                q.Enqueue(cion.root);
                while (q.Count != 0)
                {
                    TypeDefinition typeDef = q.Dequeue();
                    foreach (MethodDefinition mtd in typeDef.Methods)
                        cion.excludes.Add((int)mtd.MetadataToken.RID - 1);
                    foreach (TypeDefinition t in typeDef.NestedTypes)
                        q.Enqueue(t);
                }
            }
        }
        class Phase3 : MetadataPhase
        {
            AntiTamperConfusion cion;
            public Phase3(AntiTamperConfusion cion) { this.cion = cion; }
            public override Priority Priority
            {
                get { return Priority.MetadataLevel; }
            }
            public override IConfusion Confusion
            {
                get { return cion; }
            }
            public override int PhaseID
            {
                get { return 3; }
            }

            public override void Process(NameValueCollection parameters, MetadataProcessor.MetadataAccessor accessor)
            {
                MethodTable tbl = accessor.TableHeap.GetTable<MethodTable>(Table.Method);
                cion.rvas = new uint[tbl.Length];
                cion.ptrs = new uint[tbl.Length];
                cion.codes = new byte[tbl.Length][];
                for (int i = 0; i < tbl.Length; i++)
                {
                    if (cion.excludes.Contains(i)) continue;
                    cion.rvas[i] = tbl[i].Col1;
                }
            }
        }
        class Phase4 : ImagePhase
        {
            AntiTamperConfusion cion;
            public Phase4(AntiTamperConfusion cion) { this.cion = cion; }
            public override Priority Priority
            {
                get { return Priority.MetadataLevel; }
            }
            public override IConfusion Confusion
            {
                get { return cion; }
            }
            public override int PhaseID
            {
                get { return 4; }
            }

            uint EstimateSize(MetadataProcessor.ImageAccessor accessor)
            {
                uint size = accessor.Sections.Single(_ => _.Name == ".text").SizeOfRawData;
                size += 24 + 16;
                return size;
            }
            public override void Process(NameValueCollection parameters, MetadataProcessor.ImageAccessor accessor)
            {
                uint size = EstimateSize(accessor);
                Section prev = accessor.Sections[accessor.Sections.Count - 1];
                Section sect = accessor.CreateSection(".confuse", size, 0x40000040, prev);
                accessor.Sections.Add(sect);
            }
        }
        class Phase5 : PePhase
        {
            AntiTamperConfusion cion;
            public Phase5(AntiTamperConfusion cion) { this.cion = cion; }
            public override Priority Priority
            {
                get { return Priority.PELevel; }
            }
            public override IConfusion Confusion
            {
                get { return cion; }
            }
            public override int PhaseID
            {
                get { return 3; }
            }

            void ExtractCodes(Stream stream, out uint csOffset, out uint sn, out uint snLen)
            {
                Random rand = new Random();
                int rvaOffset = -1;
                BinaryReader rdr = new BinaryReader(stream);
                stream.Seek(0x3c, SeekOrigin.Begin);
                uint offset = rdr.ReadUInt32();
                stream.Seek(offset, SeekOrigin.Begin);
                stream.Seek(0x6, SeekOrigin.Current);
                uint sections = rdr.ReadUInt16();
                stream.Seek(offset = offset + 0x18, SeekOrigin.Begin);  //Optional hdr
                bool pe32 = (rdr.ReadUInt16() == 0x010b);
                csOffset = offset + 0x40;
                stream.Seek(offset = offset + (pe32 ? 0xE0U : 0xF0U), SeekOrigin.Begin);   //sections
                uint sampleRva = 0xffffffff;
                foreach (uint i in cion.rvas)
                    if (i != 0) { sampleRva = i; break; }
                uint[] vAdrs = new uint[sections];
                uint[] vSizes = new uint[sections];
                uint[] dAdrs = new uint[sections];
                for (int i = 0; i < sections; i++)
                {
                    string name = Encoding.ASCII.GetString(rdr.ReadBytes(8)).Trim('\0');
                    uint vSize = vSizes[i] = rdr.ReadUInt32();
                    uint vAdr = vAdrs[i] = rdr.ReadUInt32();
                    uint dSize = rdr.ReadUInt32();
                    uint dAdr = dAdrs[i] = rdr.ReadUInt32();
                    stream.Seek(0x10, SeekOrigin.Current);
                    if (sampleRva > vAdr && sampleRva < (vAdr + vSize))
                    {
                        rvaOffset = (int)dAdr - (int)vAdr;
                        break;
                    }
                }

                for (int i = 0; i < cion.rvas.Length; i++)
                {
                    if (cion.rvas[i] == 0) continue;
                    long ptr = cion.rvas[i] + rvaOffset;
                    cion.ptrs[i] = (uint)ptr;
                    stream.Seek(ptr, SeekOrigin.Begin);
                    byte b = rdr.ReadByte();
                    if ((b & 0x3) == 0x2)
                    {
                        stream.Seek((uint)b >> 2, SeekOrigin.Current);
                    }
                    else
                    {
                        stream.Seek(-1, SeekOrigin.Current);
                        ushort f = rdr.ReadUInt16();
                        stream.Seek(2, SeekOrigin.Current);
                        uint size = rdr.ReadUInt32();
                        stream.Seek(4 + size, SeekOrigin.Current);
                        if ((f & 0x80) != 0)
                        {
                            stream.Seek((stream.Position + 3) & ~3, SeekOrigin.Begin);
                            bool more;
                            do
                            {
                                byte fl = rdr.ReadByte();
                                more = ((fl & 0x80) != 0);
                                if ((fl & 0x40) != 0)
                                {
                                    stream.Seek(-1, SeekOrigin.Current);
                                    uint sectLen = rdr.ReadUInt32() >> 8;
                                    stream.Seek(-4 + sectLen, SeekOrigin.Current);
                                }
                                else
                                {
                                    byte sectLen = rdr.ReadByte();
                                    stream.Seek(-1 + sectLen, SeekOrigin.Current);
                                }
                            } while (more);
                        }
                    }
                    long len = stream.Position - ptr;
                    stream.Seek(ptr, SeekOrigin.Begin);
                    cion.codes[i] = rdr.ReadBytes((int)len);
                    stream.Seek(ptr, SeekOrigin.Begin);
                    byte[] bs = new byte[len];
                    //rand.NextBytes(bs);
                    stream.Write(bs, 0, (int)len);
                }

                stream.Seek(offset - 16, SeekOrigin.Begin);
                uint mdDir = rdr.ReadUInt32();
                for (int i = 0; i < sections; i++)
                    if (mdDir > vAdrs[i] && mdDir < vAdrs[i] + vSizes[i])
                    {
                        mdDir = mdDir - vAdrs[i] + dAdrs[i];
                        break;
                    }
                stream.Seek(mdDir + 0x20, SeekOrigin.Begin);
                sn = rdr.ReadUInt32();
                for (int i = 0; i < sections; i++)
                    if (sn > vAdrs[i] && sn < vAdrs[i] + vSizes[i])
                    {
                        sn = sn - vAdrs[i] + dAdrs[i];
                        break;
                    }
                snLen = rdr.ReadUInt32();
            }
            static byte[] Encrypt(byte[] buff, byte[] dat, out byte[] iv, byte key)
            {
                dat = (byte[])dat.Clone();
                SHA512 sha = SHA512.Create();
                byte[] c = sha.ComputeHash(buff);
                for (int i = 0; i < dat.Length; i += 64)
                {
                    byte[] o = new byte[64];
                    int len = dat.Length <= i + 64 ? dat.Length : i + 64;
                    Buffer.BlockCopy(dat, i, o, 0, len - i);
                    for (int j = i; j < len; j++)
                        dat[j] ^= (byte)(c[j - i] ^ key);
                    c = sha.ComputeHash(o);
                }

                Rijndael ri = Rijndael.Create();
                ri.GenerateIV(); iv = ri.IV;
                MemoryStream ret = new MemoryStream();
                using (CryptoStream cStr = new CryptoStream(ret, ri.CreateEncryptor(SHA256.Create().ComputeHash(buff), iv), CryptoStreamMode.Write))
                    cStr.Write(dat, 0, dat.Length);
                return ret.ToArray();
            }
            static byte[] Decrypt(byte[] buff, byte[] iv, byte[] dat, byte key)
            {
                Rijndael ri = Rijndael.Create();
                byte[] ret = new byte[dat.Length];
                MemoryStream ms = new MemoryStream(dat);
                using (CryptoStream cStr = new CryptoStream(ms, ri.CreateDecryptor(SHA256.Create().ComputeHash(buff), iv), CryptoStreamMode.Read))
                { cStr.Read(ret, 0, dat.Length); }

                SHA512 sha = SHA512.Create();
                byte[] c = sha.ComputeHash(buff);
                for (int i = 0; i < ret.Length; i += 64)
                {
                    int len = ret.Length <= i + 64 ? ret.Length : i + 64;
                    for (int j = i; j < len; j++)
                        ret[j] ^= (byte)(c[j - i] ^ key);
                    c = sha.ComputeHash(ret, i, len - i);
                }
                return ret;
            }
            public override void Process(NameValueCollection parameters, Stream stream)
            {
                stream.Seek(0, SeekOrigin.Begin);
                uint csOffset;
                uint sn;
                uint snLen;
                ExtractCodes(stream, out csOffset, out sn, out snLen);
                stream.Position = 0;
                Image img = ImageReader.ReadImageFrom(stream);

                MemoryStream ms = new MemoryStream();
                ms.WriteByte(0xd6);
                ms.WriteByte(0x6f);
                BinaryWriter wtr = new BinaryWriter(ms);
                wtr.Write((uint)cion.codes.Length);
                for (int i = 0; i < cion.codes.Length; i++)
                {
                    wtr.Write((int)(cion.ptrs[i] ^ cion.key4));
                    if (cion.ptrs[i] == 0) continue;
                    wtr.Write((int)(cion.rvas[i] ^ cion.key4));
                    wtr.Write(cion.codes[i].Length);
                    wtr.Write(cion.codes[i]);
                }

                byte[] buff;
                BinaryReader sReader = new BinaryReader(stream);
                using (MemoryStream str = new MemoryStream())
                {
                    stream.Position = img.ResolveVirtualAddress(img.Metadata.VirtualAddress) + 12;
                    stream.Position += sReader.ReadUInt32() + 4;
                    stream.Position += 2;

                    ushort streams = sReader.ReadUInt16();

                    for (int i = 0; i < streams; i++)
                    {
                        uint offset = img.ResolveVirtualAddress(img.Metadata.VirtualAddress + sReader.ReadUInt32());
                        uint size = sReader.ReadUInt32();

                        int c = 0;
                        while (sReader.ReadByte() != 0) c++;
                        long ori = stream.Position += (((c + 1) + 3) & ~3) - (c + 1);

                        stream.Position = offset;
                        str.Write(sReader.ReadBytes((int)size), 0, (int)size);
                        stream.Position = ori;
                    }

                    buff = str.ToArray();
                }

                byte[] iv;
                byte[] dat = Encrypt(buff, ms.ToArray(), out iv, cion.key5);

                byte[] md5 = MD5.Create().ComputeHash(buff);
                long checkSum = BitConverter.ToInt64(md5, 0) ^ BitConverter.ToInt64(md5, 8);
                wtr = new BinaryWriter(stream);
                stream.Seek(csOffset, SeekOrigin.Begin);
                wtr.Write(img.Metadata.VirtualAddress ^ (uint)cion.key0);
                stream.Seek(img.GetSection(".confuse").PointerToRawData, SeekOrigin.Begin);
                wtr.Write(checkSum ^ cion.key1);
                wtr.Write(sn);
                wtr.Write(snLen);
                wtr.Write(iv.Length ^ cion.key2);
                wtr.Write(iv);
                wtr.Write(dat.Length ^ cion.key3);
                wtr.Write(dat);
            }
        }

        TypeDefinition root;
        int key0;
        long key1;
        int key2;
        int key3;
        int key4;
        byte key5;
        List<int> excludes;
        uint[] rvas;
        uint[] ptrs;
        byte[][] codes;
        uint sectRaw;

        public string Name
        {
            get { return "Anti Tampering Confusion"; }
        }
        public string Description
        {
            get { return "This confusion provides a better protection than strong name for maintain integration."; }
        }
        public string ID
        {
            get { return "anti tamper"; }
        }
        public bool StandardCompatible
        {
            get { return false; }
        }
        public Target Target
        {
            get { return Target.Module; }
        }
        public Preset Preset
        {
            get { return Preset.Maximum; }
        }
        public bool SupportLateAddition
        {
            get { return true; }
        }
        public Behaviour Behaviour
        {
            get { return Behaviour.Inject | Behaviour.AlterCode | Behaviour.Encrypt; }
        }

        Phase[] phases;
        public Phase[] Phases
        {
            get { if (phases == null)phases = new Phase[] { new Phase1(this), new Phase2(this), new Phase3(this), new Phase4(this), new Phase5(this) }; return phases; }
        }
    }
}