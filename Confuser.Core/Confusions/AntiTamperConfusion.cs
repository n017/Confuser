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
                cion.root.Name = "AntiTemperingModule"; cion.root.Namespace = "";
                mod.Types.Add(cion.root);
                MethodDefinition cctor = mod.GetType("<Module>").GetStaticConstructor();
                cctor.Body.GetILProcessor().InsertBefore(0, Instruction.Create(OpCodes.Call, cion.root.Methods.FirstOrDefault(mtd => mtd.Name == "Initalize")));
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
                cion.codes = new byte[tbl.Length][];
                for (int i = 0; i < tbl.Length; i++)
                {
                    if (cion.excludes.Contains(i)) continue;
                    cion.rvas[i] = tbl[i].Col1;
                }
            }
        }
        class Phase4 : PePhase
        {
            AntiTamperConfusion cion;
            public Phase4(AntiTamperConfusion cion) { this.cion = cion; }
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

            void ExtractCodes(Stream stream, out uint csOffset)
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
                for (int i = 0; i < sections; i++)
                {
                    string name = Encoding.ASCII.GetString(rdr.ReadBytes(8)).Trim('\0');
                    if (name != ".text") { stream.Seek(0x20, SeekOrigin.Current); continue; }
                    uint vSize = rdr.ReadUInt32();
                    uint vAdr = rdr.ReadUInt32();
                    uint dSize = rdr.ReadUInt32();
                    uint dAdr = rdr.ReadUInt32();
                    stream.Seek(0x10, SeekOrigin.Current);
                    rvaOffset = (int)dAdr - (int)vAdr;
                    break;
                }

                for (int i = 0; i < cion.rvas.Length; i++)
                {
                    if (cion.rvas[i] == 0) continue;
                    long ptr = cion.rvas[i] + rvaOffset;
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
                    rand.NextBytes(bs);
                    stream.Write(bs, 0, (int)len);
                }
            }
            static byte[] Encrypt(byte[] file, byte[] dat, out byte[] iv)
            {
                dat = (byte[])dat.Clone();
                SHA512 sha = SHA512.Create();
                byte[] c = sha.ComputeHash(file);
                for (int i = 0; i < dat.Length; i += 64)
                {
                    byte[] o = new byte[64];
                    int len = dat.Length <= i + 64 ? dat.Length : i + 64;
                    Buffer.BlockCopy(dat, i, o, 0, len - i);
                    for (int j = i; j < len; j++)
                        dat[j] ^= c[j - i];
                    c = sha.ComputeHash(o);
                }

                Rijndael ri = Rijndael.Create();
                ri.GenerateIV(); iv = ri.IV;
                MemoryStream ret = new MemoryStream();
                using (CryptoStream cStr = new CryptoStream(ret, ri.CreateEncryptor(SHA256.Create().ComputeHash(file), iv), CryptoStreamMode.Write))
                    cStr.Write(dat, 0, dat.Length);
                return ret.ToArray();
            }
            static byte[] Decrypt(byte[] file, byte[] iv, byte[] dat)
            {
                Rijndael ri = Rijndael.Create();
                byte[] ret = new byte[dat.Length];
                MemoryStream ms = new MemoryStream(dat);
                using (CryptoStream cStr = new CryptoStream(ms, ri.CreateDecryptor(SHA256.Create().ComputeHash(file), iv), CryptoStreamMode.Read))
                { cStr.Read(ret, 0, dat.Length); }

                SHA512 sha = SHA512.Create();
                byte[] c = sha.ComputeHash(file);
                for (int i = 0; i < ret.Length; i += 64)
                {
                    int len = ret.Length <= i + 64 ? ret.Length : i + 64;
                    for (int j = i; j < len; j++)
                        ret[j] ^= c[j - i];
                    c = sha.ComputeHash(ret, i, len - i);
                }
                return ret;
            }
            public override void Process(NameValueCollection parameters, Stream stream)
            {
                stream.Seek(0, SeekOrigin.Begin);
                uint csOffset;
                ExtractCodes(stream, out csOffset);

                MemoryStream ms = new MemoryStream();
                ms.WriteByte(0xd6);
                ms.WriteByte(0x6f);
                BinaryWriter wtr = new BinaryWriter(ms);
                wtr.Write((uint)cion.codes.Length);
                for (int i = 0; i < cion.codes.Length; i++)
                {
                    wtr.Write(cion.rvas[i]);
                    if (cion.rvas[i] == 0) continue;
                    wtr.Write(cion.codes[i].Length);
                    wtr.Write(cion.codes[i]);
                }
                byte[] file = new byte[stream.Length];
                stream.Seek(0, SeekOrigin.Begin);
                stream.Read(file, 0, (int)stream.Length);
                byte[] iv;
                byte[] dat = Encrypt(file, ms.ToArray(), out iv);

                byte[] md5 = MD5.Create().ComputeHash(file);
                ulong checkSum = BitConverter.ToUInt64(md5, 0) ^ BitConverter.ToUInt64(md5, 8);
                wtr = new BinaryWriter(stream);
                stream.Seek(csOffset, SeekOrigin.Begin);
                wtr.Write(file.Length);
                stream.Seek(0, SeekOrigin.End);
                wtr.Write(checkSum);
                wtr.Write(iv.Length);
                wtr.Write(iv);
                wtr.Write(dat.Length);
                wtr.Write(dat);
            }
        }

        TypeDefinition root;
        List<int> excludes;
        uint[] rvas;
        byte[][] codes;

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
            get { return Target.Assembly; }
        }
        public Preset Preset
        {
            get { return Preset.Maximum; }
        }

        Phase[] phases;
        public Phase[] Phases
        {
            get { if (phases == null)phases = new Phase[] { new Phase1(this), new Phase2(this), new Phase3(this), new Phase4(this) }; return phases; }
        }
    }
}
