using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Metadata;

namespace Confuser.Core.Confusions
{
    public class AntiCFFConfusion : AdvancedConfusion
    {
        public override void Confuse(int phase, Confuser cr, MetadataProcessor.MetadataAccessor accessor)
        {
            if (phase != 2) throw new InvalidOperationException();
            foreach (Row<ParameterAttributes, ushort, uint> r in accessor.TableHeap.GetTable<ParamTable>(Table.Param))
                if (r != null)
                    r.Col3 = 0x7fff7fff;
        }

        public override Priority Priority
        {
            get { return Priority.MetadataLevel; }
        }

        public override string Name
        {
            get { return "Anti CFF Explorer Confusion"; }
        }

        public override Phases Phases
        {
            get { return Phases.Phase2; }
        }

        public override bool StandardCompatible
        {
            get { return false; }
        }

        public override string Description
        {
            get { return "This confusion prevent CFF Explorer to view the metadata of the assembly."; }
        }
    }
}
