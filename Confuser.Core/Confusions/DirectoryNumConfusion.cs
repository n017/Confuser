using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Confuser.Core.Confusions
{
    public class DirectoryNumConfusion : AdvancedConfusion
    {
        public override void PreConfuse(Confuser cr, ConfusingWriter wtr)
        {
            wtr.Image.PEOptionalHeader.NTSpecificFields.NumberOfDataDir = 0xd;
        }

        public override void DoConfuse(Confuser cr, ConfusingWriter wtr)
        {
            throw new InvalidOperationException();
        }

        public override void PostConfuse(Confuser cr, ConfusingWriter wtr)
        {
            throw new InvalidOperationException();
        }

        public override Priority Priority
        {
            get { return Priority.PELevel; }
        }

        public override string Name
        {
            get { return "Data Directories Number Confusion"; }
        }

        public override ProcessType Process
        {
            get { return ProcessType.Pre; }
        }

        public override bool StandardCompatible
        {
            get { return false; }
        }
    }
}
