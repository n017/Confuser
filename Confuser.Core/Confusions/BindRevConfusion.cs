using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Rocks;

namespace Confuser.Core.Confusions
{
    public class BindRevConfusion : StructureConfusion
    {
        public override Priority Priority
        {
            get { return Priority.TypeLevel; }
        }

        public override string Name
        {
            get { return "Binding Removal Confusion"; }
        }

        public override ProcessType Process
        {
            get { return ProcessType.Pre; }
        }

        public override bool StandardCompatible
        {
            get { return true; }
        }

        public override void PreConfuse(Confuser cr, AssemblyDefinition asm)
        {
            foreach (TypeDefinition t in asm.MainModule.GetAllTypes())
            {
                ConfuseType(cr, t);
            }
        }

        public override void PostConfuse(Confuser cr, AssemblyDefinition asm)
        {
            throw new InvalidOperationException();
        }

        public override void DoConfuse(Confuser cr, AssemblyDefinition asm)
        {
            throw new InvalidOperationException();
        }

        private void ConfuseType(Confuser cr, TypeDefinition def)
        {
            if (def.IsEnum)
            {
                int idx=0;
                while (def.Fields.Count != 1)
                    if (def.Fields[idx].Name != "value__")
                        def.Fields.RemoveAt(idx);
                    else
                        idx++;
            }
            def.Properties.Clear();
            def.Events.Clear();
        }
    }
}
