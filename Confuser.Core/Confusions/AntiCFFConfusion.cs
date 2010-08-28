using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Metadata;

namespace Confuser.Core.Confusions
{
    public class AntiCFFConfusion : AdvancedPhase, IConfusion
    {
        public string Name
        {
            get { return "Anti CFF Explorer Confusion"; }
        }
        public string Description
        {
            get { return "This confusion prevent CFF Explorer to view the metadata of the assembly."; }
        }
        public string ID
        {
            get { return "anti cff"; }
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
            get { return Preset.Aggressive; }
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
            get { return 2; }
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
            foreach (Row<ParameterAttributes, ushort, uint> r in accessor.TableHeap.GetTable<ParamTable>(Table.Param))
                if (r != null)
                    r.Col3 = 0x7fff7fff;
        }
    }
}
