using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil.Metadata;

namespace Confuser.Core.Confusions
{
    public class AntiCFFConfusion:AdvancedConfusion
    {
        public override void PreConfuse(Confuser cr, ConfusingWriter wtr)
        {
            throw new InvalidOperationException();
        }

        public override void DoConfuse(Confuser cr, ConfusingWriter wtr)
        {
            throw new InvalidOperationException();
        }

        public override void PostConfuse(Confuser cr, ConfusingWriter wtr)
        {
            if (wtr.Tables[GenericParamTable.RId] != null)
                foreach (GenericParamRow r in wtr.Tables[GenericParamTable.RId].Rows)
                    r.Name = 0x7fff7fff;
            if (wtr.Tables[ParamTable.RId] != null)
                foreach (ParamRow r in wtr.Tables[ParamTable.RId].Rows)
                    r.Name = 0x7fff7fff;
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
            get { return ProcessType.Post; }
        }

        public override bool StandardCompatible
        {
            get { return false; }
        }
    }
}
