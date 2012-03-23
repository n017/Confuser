using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Metadata;
using System.Text;
using System.Collections.Specialized;
using System.IO;

namespace Confuser.Core.Confusions
{
    public class InvalidMdConfusion : IConfusion
    {
        class Phase1 : MetadataPhase
        {
            InvalidMdConfusion cion;
            public Phase1(InvalidMdConfusion cion) { this.cion = cion; }
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
                get { return 1; }
            }


            public override void Process(NameValueCollection parameters, MetadataProcessor.MetadataAccessor accessor)
            {
                accessor.TableHeap.GetTable<DeclSecurityTable>(Table.DeclSecurity).AddRow(new Row<SecurityAction, uint, uint>((SecurityAction)0xffff, 0xffffffff, 0xffffffff));
                char[] pad = new char[0x10000];
                int len = 0;
                Random rand = new Random();
                while (accessor.StringHeap.Length + len < 0x10000)
                {
                    for (int i = 0; i < 0x1000; i++)
                        while ((pad[len + i] = (char)rand.Next(0, 0xff)) == '\0') ;
                    len += 0x1000;
                }
                uint idx = accessor.StringHeap.GetStringIndex(new string(pad, 0, len));
                if (Array.IndexOf(parameters.AllKeys, "hasReflection") == -1)
                    accessor.TableHeap.GetTable<ManifestResourceTable>(Table.ManifestResource).AddRow(new Row<uint, ManifestResourceAttributes, uint, uint>(0xffffffff, ManifestResourceAttributes.Private, idx, 2));
            }
        }
        class Phase2 : MetadataPhase
        {
            InvalidMdConfusion cion;
            public Phase2(InvalidMdConfusion cion) { this.cion = cion; }
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


            public override void Process(NameValueCollection parameters, MetadataProcessor.MetadataAccessor accessor)
            {
                if (Array.IndexOf(parameters.AllKeys, "hasreflection") == -1)
                {
                    if (accessor.Module.Runtime != TargetRuntime.Net_4_0)
                    {
                        uint mtdLen = (uint)accessor.TableHeap.GetTable<MethodTable>(Table.Method).Length + 1;
                        uint fldLen = (uint)accessor.TableHeap.GetTable<FieldTable>(Table.Field).Length + 1;
                        List<uint> nss = new List<uint>();
                        foreach (Row<TypeAttributes, uint, uint, uint, uint, uint> i in accessor.TableHeap.GetTable<TypeDefTable>(Table.TypeDef))
                            if (i == null) break; else if (!nss.Contains(i.Col3)) nss.Add(i.Col3);
                        uint nested = (uint)accessor.TableHeap.GetTable<TypeDefTable>(Table.TypeDef).AddRow(new Row<TypeAttributes, uint, uint, uint, uint, uint>(0, 0x7fffffff, 0, 0x3FFFD, fldLen, mtdLen));
                        accessor.TableHeap.GetTable<NestedClassTable>(Table.NestedClass).AddRow(new Row<uint, uint>(nested, nested));
                        foreach (uint i in nss)
                        {
                            uint type = (uint)accessor.TableHeap.GetTable<TypeDefTable>(Table.TypeDef).AddRow(new Row<TypeAttributes, uint, uint, uint, uint, uint>(0, 0x7fffffff, i, 0x3FFFD, fldLen, mtdLen));
                            accessor.TableHeap.GetTable<NestedClassTable>(Table.NestedClass).AddRow(new Row<uint, uint>(nested, type));
                        }
                        foreach (Row<ParameterAttributes, ushort, uint> r in accessor.TableHeap.GetTable<ParamTable>(Table.Param))
                            if (r != null)
                                r.Col3 = 0x7fffffff;
                    }
                }
                accessor.TableHeap.GetTable<ModuleTable>(Table.Module).AddRow(accessor.StringHeap.GetStringIndex(Guid.NewGuid().ToString()));

                accessor.TableHeap.GetTable<AssemblyTable>(Table.Assembly).AddRow(new Row<AssemblyHashAlgorithm, ushort, ushort, ushort, ushort, AssemblyAttributes, uint, uint, uint>(
                    AssemblyHashAlgorithm.None, 0, 0, 0, 0, AssemblyAttributes.SideBySideCompatible, 0,
                    accessor.StringHeap.GetStringIndex(Guid.NewGuid().ToString()), 0));
            }
        }
        class Phase3 : PePhase
        {
            InvalidMdConfusion cion;
            public Phase3(InvalidMdConfusion cion) { this.cion = cion; }
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
                get { return 2; }
            }

            public override void Process(NameValueCollection parameters, Stream stream, ModuleDefinition mod)
            {
                Random rand = new Random();
                BinaryReader rdr = new BinaryReader(stream);
                stream.Seek(0x3c, SeekOrigin.Begin);
                uint offset = rdr.ReadUInt32();
                stream.Seek(offset, SeekOrigin.Begin);
                stream.Seek(0x6, SeekOrigin.Current);
                uint sections = rdr.ReadUInt16();
                stream.Seek(offset = offset + 0x18, SeekOrigin.Begin);  //Optional hdr
                bool pe32 = (rdr.ReadUInt16() == 0x010b);
                //stream.Seek(offset + (pe32 ? 0x5c : 0x6c), SeekOrigin.Begin);
                //stream.Write(new byte[] { 0x06, 0x00, 0x00, 0x00 }, 0, 4);
                stream.Seek(offset + 0x10, SeekOrigin.Begin);
                uint entryPt = rdr.ReadUInt32(); bool ok = false;
                stream.Seek(offset = offset + (pe32 ? 0xE0U : 0xF0U), SeekOrigin.Begin);   //sections
                for (int i = 0; i < sections; i++)
                {
                    bool seen = false;
                    for (int j = 0; j < 8; j++)
                    {
                        byte b = rdr.ReadByte();
                        if (b == 0 & !seen)
                        {
                            seen = true;
                            stream.Seek(-1, SeekOrigin.Current);
                            stream.WriteByte(0x20);
                        }
                    }
                    uint vSize = rdr.ReadUInt32();
                    uint vLoc = rdr.ReadUInt32();
                    uint rSize = rdr.ReadUInt32();
                    uint rLoc = rdr.ReadUInt32();
                    if (!ok && entryPt > vLoc && entryPt < (vLoc + vSize))
                    { entryPt = entryPt - vLoc + rLoc; ok = true; }
                    stream.Seek(0x10, SeekOrigin.Current);
                }
                //stream.Seek(entryPt, SeekOrigin.Begin);
                //byte[] fake = new byte[]{0xff,0x25,0x00,0x20,0x40,0x00,
                //                         0xBE,0x05,0x29,0x0E,0x31,0x1B};
                //stream.Write(fake, 0, fake.Length);
            }
        }

        public string Name
        {
            get { return "Invalid Metadata Confusion"; }
        }
        public string Description
        {
            get { return "This confusion adds invalid metadata into assembly to prevent disassembler/decompiler to open the assembly."; }
        }
        public string ID
        {
            get { return "invalid md"; }
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
            get { return Behaviour.AlterStructure; }
        }

        Phase[] phases;
        public Phase[] Phases
        {
            get { if (phases == null)phases = new Phase[] { new Phase1(this), new Phase2(this), new Phase3(this) }; return phases; }
        }

        public void Init() { }
        public void Deinit() { }
    }
}
