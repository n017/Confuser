using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Metadata;
using System.Text;

namespace Confuser.Core.Confusions
{
    class InvalidMdConfusion :  IConfusion
    {
        class Phase1 : AdvancedPhase
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
            public override bool WholeRun
            {
                get { return true; }
            }
            public override void Initialize(ModuleDefinition mod)
            {
                //
            }
            public override void DeInitialize()
            {
                //
            }


            public override void Process(MetadataProcessor.MetadataAccessor accessor)
            {
                accessor.TableHeap.GetTable<DeclSecurityTable>(Table.DeclSecurity).AddRow(new Row<SecurityAction, uint, uint>((SecurityAction)0xffff, 0xffffffff, 0xffffffff));
                accessor.TableHeap.GetTable<ManifestResourceTable>(Table.ManifestResource).AddRow(new Row<uint, ManifestResourceAttributes, uint, uint>(0xffffffff, ManifestResourceAttributes.Private, 0xffffffff, 2));
            }
        }
        class Phase2 : AdvancedPhase
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
            public override bool WholeRun
            {
                get { return true; }
            }
            public override void Initialize(ModuleDefinition mod)
            {
                //
            }
            public override void DeInitialize()
            {
                //
            }


            public override void Process(MetadataProcessor.MetadataAccessor accessor)
            {
                uint mtdLen = (uint)accessor.TableHeap.GetTable<MethodTable>(Table.Method).Length + 1;
                uint fldLen = (uint)accessor.TableHeap.GetTable<FieldTable>(Table.Field).Length + 1;
                List<uint> nss = new List<uint>();
                foreach (Row<TypeAttributes, uint, uint, uint, uint, uint> i in accessor.TableHeap.GetTable<TypeDefTable>(Table.TypeDef))
                    if (i == null) break; else if (!nss.Contains(i.Col3)) nss.Add(i.Col3);
                uint nested = (uint)accessor.TableHeap.GetTable<TypeDefTable>(Table.TypeDef).AddRow(new Row<TypeAttributes, uint, uint, uint, uint, uint>(0, 0xffffffff, 0, 0x3FFFD, fldLen, mtdLen));
                accessor.TableHeap.GetTable<NestedClassTable>(Table.NestedClass).AddRow(new Row<uint, uint>(nested, nested));
                foreach (uint i in nss)
                {
                    uint type = (uint)accessor.TableHeap.GetTable<TypeDefTable>(Table.TypeDef).AddRow(new Row<TypeAttributes, uint, uint, uint, uint, uint>(0, 0xffffffff, i, 0x3FFFD, fldLen, mtdLen));
                    accessor.TableHeap.GetTable<NestedClassTable>(Table.NestedClass).AddRow(new Row<uint, uint>(nested, type));
                }
                foreach (Row<ParameterAttributes, ushort, uint> r in accessor.TableHeap.GetTable<ParamTable>(Table.Param))
                    if (r != null)
                        r.Col3 = 0xffffffff;

                accessor.TableHeap.GetTable<ModuleTable>(Table.Module).AddRow(accessor.StringHeap.GetStringIndex(Guid.NewGuid().ToString()));

                accessor.TableHeap.GetTable<AssemblyTable>(Table.Assembly).AddRow(new Row<AssemblyHashAlgorithm, ushort, ushort, ushort, ushort, AssemblyAttributes, uint, uint, uint>(
                    AssemblyHashAlgorithm.None, 0, 0, 0, 0, AssemblyAttributes.SideBySideCompatible, 0,
                    accessor.StringHeap.GetStringIndex(Guid.NewGuid().ToString()), 0));
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
            get { return Target.Assembly; }
        }
        public Preset Preset
        {
            get { return Preset.Maximum; }
        }

        Phase[] phases;
        public Phase[] Phases
        {
            get { if (phases == null)phases = new Phase[] { new Phase1(this), new Phase2(this) }; return phases; }
        }
    }
}
