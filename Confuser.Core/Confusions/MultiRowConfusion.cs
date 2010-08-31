using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Metadata;

namespace Confuser.Core.Confusions
{
    public class MultiRowConfusion : AdvancedPhase, IConfusion
    {
        public string Name
        {
            get { return "Multiple Row Confusion"; }
        }
        public string Description
        {
            get { return "This confusion generate invalid metadata into the assembly and cause trouble to the decompilers."; }
        }
        public string ID
        {
            get { return "multi row"; }
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
        public Phase[] Phases
        {
            get { return new Phase[] { this }; }
        }


        public override Priority Priority
        {
            get { return Priority.MetadataLevel; }
        }
        public override IConfusion Confusion
        {
            get { return this; }
        }
        public override int PhaseID
        {
            get { return 1; }
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
            accessor.TableHeap.GetTable<ModuleTable>(Table.Module).AddRow(accessor.StringHeap.GetStringIndex(Guid.NewGuid().ToString()));

            accessor.TableHeap.GetTable<AssemblyTable>(Table.Assembly).AddRow(new Row<AssemblyHashAlgorithm, ushort, ushort, ushort, ushort, AssemblyAttributes, uint, uint, uint>(
                AssemblyHashAlgorithm.None, 0, 0, 0, 0, AssemblyAttributes.SideBySideCompatible, 0,
                accessor.StringHeap.GetStringIndex(Guid.NewGuid().ToString()), 0));
        }   
    }
}
