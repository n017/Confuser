using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Metadata;

namespace Confuser.Core.Confusions
{
    public class AntiCFFConfusion:AdvancedConfusion
    {
        public override void PreConfuse(Confuser cr, MetadataProcessor.MetadataAccessor accessor)
        {
            throw new InvalidOperationException();
        }

        public override void DoConfuse(Confuser cr, MetadataProcessor.MetadataAccessor accessor)
        {
            foreach (Row<ParameterAttributes, ushort, uint> r in accessor.TableHeap.GetTable<ParamTable>(Table.Param))
                if (r != null)
                    r.Col3 = 0x7fff7fff;
        }

        public override void PostConfuse(Confuser cr, MetadataProcessor.MetadataAccessor accessor)
        {
            throw new InvalidOperationException();
        }

        public override Priority Priority
        {
            get { return Priority.MetadataLevel; }
        }

        public override string Name
        {
            get { return "Anti CFF Explorer Confusion"; }
        }

        public override ProcessType Process
        {
            get { return ProcessType.Real; }
        }

        public override bool StandardCompatible
        {
            get { return false; }
        }
    }
}
