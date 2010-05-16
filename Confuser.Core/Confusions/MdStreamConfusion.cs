using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil.Metadata;
using Mono.Cecil.Binary;

namespace Confuser.Core.Confusions
{
    public class MdStreamConfusion : AdvancedConfusion
    {
        public override Priority Priority
        {
            get { return Priority.MetadataLevel; }
        }

        public override string Name
        {
            get { return "Metadata Stream Confusion"; }
        }

        public override ProcessType Process
        {
            get { return ProcessType.Real; }
        }

        public override void PreConfuse(Confuser cr, ConfusingWriter wtr)
        {
            throw new InvalidOperationException();
        }

        public override void DoConfuse(Confuser cr, ConfusingWriter wtr)
        {
            MetadataStream str = new MetadataStream();
            str.Header.Name = "#~\"Have Confused?\"~#";
            str.Heap = new RawHeap(str) { Data = Encoding.UTF8.GetBytes("Confused by Confuser!") };

            wtr.Image.MetadataRoot.Streams.Add(str);
            cr.Log("<stream name='" + str.Header.Name + "'/>");
        }

        public override void PostConfuse(Confuser cr, ConfusingWriter wtr)
        {
            throw new InvalidOperationException();
        }

        public override bool StandardCompatible
        {
            get { return true; }
        }
    }
}
