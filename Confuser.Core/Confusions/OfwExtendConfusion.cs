using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil.Metadata;
using Mono.Cecil;

namespace Confuser.Core.Confusions
{
    class OfwExtendConfusion : AdvancedConfusion
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
            TypeDefTable typeTbl = wtr.GetTable<TypeDefTable>();
            FieldTable fldTbl = wtr.GetTable<FieldTable>();
            MethodTable mtdTbl = wtr.GetTable<MethodTable>();
            List<uint> ns = new List<uint>();
            foreach (TypeDefRow r in typeTbl.Rows)
                if (!ns.Contains(r.Namespace))
                    ns.Add(r.Namespace);

            foreach (uint i in ns)
            {
                MethodRow mtd = wtr.CreateRow<MethodRow>();
                mtd.Flags = MethodAttributes.Abstract | MethodAttributes.PInvokeImpl;

                TypeDefRow t = wtr.CreateRow<TypeDefRow>();
                t.Extends = new MetadataToken(TokenType.TypeDef, 0x00ffffff);
                t.FieldList = (uint)fldTbl.Rows.Count + 1;
                t.MethodList = (uint)mtdTbl.Rows.Count + 1;
                t.Namespace = i;
                t.Name = 0x7fff7fff;
                typeTbl.Rows.Add(t);
                cr.Log("<ns id='" + i.ToString("X") + "'/>");
            }
        }

        public override Priority Priority
        {
            get { return Priority.MetadataLevel; }
        }

        public override string Name
        {
            get { return "Overflow Extend Confusion"; }
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
