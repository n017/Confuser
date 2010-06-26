using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil.Binary;
using Mono.Cecil.Metadata;
using Mono.Cecil;

namespace Confuser.Core.Confusions
{
    public class MultiRowConfusion : AdvancedConfusion
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
            ModuleTable modTbl = wtr.GetTable<ModuleTable>();
            ModuleRow modRow = wtr.CreateRow<ModuleRow>();
            modRow.EncBaseId = 0;
            modRow.EncId = 0;
            modRow.Generation = 0;
            Guid g = Guid.NewGuid();
            modRow.Mvid = wtr.AddGuid(g);
            modRow.Name = wtr.AddString(g.ToString());
            modTbl.Rows.Add(modRow);
            cr.Log("<mod/>");

            AssemblyTable asmTbl = wtr.GetTable<AssemblyTable>();
            AssemblyRow asmRow = wtr.CreateRow<AssemblyRow>();
            asmRow.BuildNumber = 0;
            asmRow.Culture = 0;
            asmRow.Flags = AssemblyFlags.SideBySideCompatible;
            asmRow.HashAlgId = AssemblyHashAlgorithm.None;
            asmRow.MajorVersion = 0;
            asmRow.MinorVersion = 0;
            asmRow.Name = wtr.AddString(g.ToString());
            asmRow.PublicKey = 0;
            asmRow.RevisionNumber = 0;
            asmTbl.Rows.Add(asmRow);
            cr.Log("<asm/>");
        }

        public override Priority Priority
        {
            get { return Priority.MetadataLevel; }
        }

        public override string Name
        {
            get { return "Multiple Row Confusion"; }
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
