using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Metadata;

namespace Confuser.Core.Confusions
{
    public class MultiRowConfusion : AdvancedConfusion
    {
        public override void Confuse(int phase, Confuser cr, MetadataProcessor.MetadataAccessor accessor)
        {
            if (phase != 1) return;
            accessor.TableHeap.GetTable<ModuleTable>(Table.Module).AddRow(accessor.StringHeap.GetStringIndex(Guid.NewGuid().ToString()));

            accessor.TableHeap.GetTable<AssemblyTable>(Table.Assembly).AddRow(new Row<AssemblyHashAlgorithm, ushort, ushort, ushort, ushort, AssemblyAttributes, uint, uint, uint>(
                AssemblyHashAlgorithm.None, 0, 0, 0, 0, AssemblyAttributes.SideBySideCompatible, 0,
                accessor.StringHeap.GetStringIndex(Guid.NewGuid().ToString()), 0));
        }

        public override Priority Priority
        {
            get { return Priority.MetadataLevel; }
        }

        public override string Name
        {
            get { return "Multiple Row Confusion"; }
        }

        public override Phases Phases
        {
            get { return Phases.Phase1; }
        }

        public override bool StandardCompatible
        {
            get { return false; }
        }

        public override string Description
        {
            get { return "This confusion generate invalid metadata into the assembly and cause trouble to the decompilers."; }
        }
    }
}
