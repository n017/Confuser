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
                cion.txts[mod] = new _Context();
                Random rand = new Random();
                byte[] dat = new byte[25];
                rand.NextBytes(dat);
                var txt = cion.txts[mod];
                txt.key0 = BitConverter.ToInt32(dat, 0);
                txt.key1 = BitConverter.ToInt64(dat, 4);
                txt.key2 = BitConverter.ToInt32(dat, 12);
                txt.key3 = BitConverter.ToInt32(dat, 16);
                txt.key4 = dat[24];
                txt.sectName = Convert.ToBase64String(MD5.Create().ComputeHash(dat)).Substring(0, 8);
            }
            public override void DeInitialize()
            {
                //
            }

            ModuleDefinition mod;
            public override void Process(ConfusionParameter parameter)
            {
                var txt = cion.txts[mod];
                AssemblyDefinition i = AssemblyDefinition.ReadAssembly(typeof(Iid).Assembly.Location);
                txt.root = CecilHelper.Inject(mod, i.MainModule.GetType("AntiTamper"));
                mod.Types.Add(txt.root);
                MethodDefinition cctor = mod.GetType("<Module>").GetStaticConstructor();
                cctor.Body.GetILProcessor().InsertBefore(0, Instruction.Create(OpCodes.Call, txt.root.Methods.FirstOrDefault(mtd => mtd.Name == "Initalize")));

                MethodDefinition init = txt.root.Methods.FirstOrDefault(mtd => mtd.Name == "Initalize");
                foreach (Instruction inst in init.Body.Instructions)
                {
                    if (inst.Operand is int)
                    {
                        switch ((int)inst.Operand)
                        {
                            case 0x11111111:
                                inst.Operand = txt.key0; break;
                            case 0x33333333:
                                inst.Operand = txt.key2; break;
                            case 0x44444444:
                                inst.Operand = txt.key3; break;
                            case 0x55555555:
                                inst.Operand = (int)txt.sectName.ToCharArray().Sum(_ => (int)_); break;
                        }
                    }
                    else if (inst.Operand is long && (long)inst.Operand == 0x2222222222222222)
                        inst.Operand = txt.key1;
                }
                MethodDefinition dec = txt.root.Methods.FirstOrDefault(mtd => mtd.Name == "Decrypt");
                foreach (Instruction inst in dec.Body.Instructions)
                    if (inst.Operand is int && (int)inst.Operand == 0x11111111)
                        inst.Operand = (int)txt.key4;

                //txt.root.Name = ObfuscationHelper.GetNewName("AntiTamperModule" + Guid.NewGuid().ToString());
                txt.root.Namespace = "";
                AddHelper(txt.root, HelperAttribute.NoInjection);
                foreach (MethodDefinition mtdDef in txt.root.Methods)
                {
                    //mtdDef.Name = ObfuscationHelper.GetNewName(mtdDef.Name + Guid.NewGuid().ToString());
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
                var txt = cion.txts[mod];
                Queue<TypeDefinition> q = new Queue<TypeDefinition>();
                txt.excludes = new List<int>();
                txt.excludes.Add((int)mod.GetType("<Module>").GetStaticConstructor().MetadataToken.RID - 1);
                q.Enqueue(txt.root);
                while (q.Count != 0)
                {
                    TypeDefinition typeDef = q.Dequeue();
                    foreach (MethodDefinition mtd in typeDef.Methods)
                        txt.excludes.Add((int)mtd.MetadataToken.RID - 1);
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
                get { return 2; }
            }

            static void Crypt(byte[] buff, uint idx, uint len, uint key)
            {
                byte[] keyBuff = BitConverter.GetBytes(key);
                for (uint i = idx; i < idx + len; i++)
                    buff[i] ^= keyBuff[(i - idx) % 4];
            }
            public override void Process(NameValueCollection parameters, MetadataProcessor.MetadataAccessor accessor)
            {
                var txt = cion.txts[accessor.Module];
                MethodTable tbl = accessor.TableHeap.GetTable<MethodTable>(Table.Method);

                accessor.Codes.Position = 0;
                txt.codes = accessor.Codes.ReadBytes(accessor.Codes.Length);
                accessor.Codes.Reset(null);
                accessor.Codes.Position = 0;

                Random rand = new Random();
                byte[] randBuff = new byte[4];
                uint bas = accessor.Codebase;
                for (int i = 0; i < tbl.Length; i++)
                {
                    tbl[i].Col2 |= MethodImplAttributes.NoInlining;
                    if (tbl[i].Col1 == 0) continue;
                    if (txt.excludes.Contains(i))
                    {
                        tbl[i].Col1 = (uint)accessor.Codes.Position + bas;

                        Range range = accessor.BodyRanges[new MetadataToken(TokenType.Method, i + 1)];
                        byte[] buff = new byte[range.Length];
                        Buffer.BlockCopy(txt.codes, (int)(range.Start - bas), buff, 0, buff.Length);
                        accessor.Codes.WriteBytes(buff);
                        accessor.Codes.WriteBytes(((accessor.Codes.Position + 3) & ~3) - accessor.Codes.Position);
                    }
                    else
                    {
                        uint ptr = tbl[i].Col1 - bas;
                        rand.NextBytes(randBuff);
                        uint key = BitConverter.ToUInt32(randBuff, 0);
                        tbl[i].Col1 = (uint)accessor.Codes.Position + bas;

                        Range range = accessor.BodyRanges[new MetadataToken(TokenType.Method, i + 1)];
                        Crypt(txt.codes, range.Start - bas, range.Length, key);

                        accessor.Codes.WriteByte(0x46); //flags
                        accessor.Codes.WriteByte(0x21); //ldc.i8
                        accessor.Codes.WriteUInt64(((ulong)key << 32) | (ptr ^ key));
                        accessor.Codes.WriteByte(0x20); //ldc.i4
                        accessor.Codes.WriteUInt32(~range.Length ^ key);
                        accessor.Codes.WriteByte(0x26);

                        accessor.BlobHeap.Position = (int)tbl[i].Col5;
                        accessor.BlobHeap.ReadCompressedUInt32();
                        byte flags = accessor.BlobHeap.ReadByte();
                        if ((flags & 0x10) != 0) accessor.BlobHeap.ReadCompressedUInt32();
                        accessor.BlobHeap.ReadCompressedUInt32();
                        bool hasRet = false;
                        do
                        {
                            byte t = accessor.BlobHeap.ReadByte();
                            if (t == 0x1f || t == 0x20) continue;
                            hasRet = t != 0x01;
                        } while (false);

                        accessor.Codes.WriteByte(hasRet ? (byte)0x00 : (byte)0x26);
                        accessor.Codes.WriteByte(0x2a); //ret
                        accessor.Codes.WriteBytes(((accessor.Codes.Position + 3) & ~3) - accessor.Codes.Position);
                    }
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

            public override void Process(NameValueCollection parameters, MetadataProcessor.ImageAccessor accessor)
            {
                var txt = cion.txts[accessor.Module];
                uint size = (((uint)txt.codes.Length + 2 + 0x7f) & ~0x7fu) + 0x28;
                Section prev = accessor.Sections[accessor.Sections.Count - 1];
                Section sect = accessor.CreateSection(txt.sectName, size, 0x40000040, prev);
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
                get { return 5; }
            }

            void ExtractOffsets(_Context txt, Stream stream, out uint csOffset, out uint sn, out uint snLen)
            {
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
            public override void Process(NameValueCollection parameters, Stream stream, ModuleDefinition mod)
            {
                var txt = cion.txts[mod];
                stream.Seek(0, SeekOrigin.Begin);
                uint csOffset;
                uint sn;
                uint snLen;
                ExtractOffsets(txt, stream, out csOffset, out sn, out snLen);
                stream.Position = 0;
                Image img = ImageReader.ReadImageFrom(stream);

                MemoryStream ms = new MemoryStream();
                ms.WriteByte(0xd6);
                ms.WriteByte(0x6f);
                BinaryWriter wtr = new BinaryWriter(ms);
                wtr.Write(txt.codes);

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
                byte[] dat = Encrypt(buff, ms.ToArray(), out iv, txt.key4);

                byte[] md5 = MD5.Create().ComputeHash(buff);
                long checkSum = BitConverter.ToInt64(md5, 0) ^ BitConverter.ToInt64(md5, 8);
                wtr = new BinaryWriter(stream);
                stream.Seek(csOffset, SeekOrigin.Begin);
                wtr.Write(img.Metadata.VirtualAddress ^ (uint)txt.key0);
                stream.Seek(img.GetSection(txt.sectName).PointerToRawData, SeekOrigin.Begin);
                wtr.Write(checkSum ^ txt.key1);
                wtr.Write(sn);
                wtr.Write(snLen);
                wtr.Write(iv.Length ^ txt.key2);
                wtr.Write(iv);
                wtr.Write(dat.Length ^ txt.key3);
                wtr.Write(dat);
            }
        }

        class _Context
        {
            public TypeDefinition root;
            public int key0;
            public long key1;
            public int key2;
            public int key3;
            public byte key4;
            public string sectName;
            public List<int> excludes;
            public byte[] codes;
        }
        Dictionary<ModuleDefinition, _Context> txts = new Dictionary<ModuleDefinition, _Context>();

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

        public void Init() { txts.Clear(); }
        public void Deinit() { txts.Clear(); }
    }
}