using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Rocks;

namespace Confuser.Core.Confusions
{
    public class MdReduceConfusion : StructureConfusion
    {
        public override Priority Priority
        {
            get { return Priority.TypeLevel; }
        }

        public override string Name
        {
            get { return "Reduce Metadata Confusion"; }
        }

        public override Phases Phases
        {
            get { return Phases.Phase1; }
        }

        public override bool StandardCompatible
        {
            get { return true; }
        }

        public override void Confuse(int phase, Confuser cr, AssemblyDefinition asm, IMemberDefinition[] defs)
        {
            if (phase != 1) throw new InvalidOperationException();
            foreach (IMemberDefinition def in defs)
            {
                if (def is TypeDefinition && (def as TypeDefinition).IsEnum)
                {
                    TypeDefinition t = def as TypeDefinition;
                    int idx = 0;
                    while (t.Fields.Count != 1)
                        if (t.Fields[idx].Name != "value__")
                            t.Fields.RemoveAt(idx);
                        else
                            idx++;
                }
                else if (def is EventDefinition)
                {
                    def.DeclaringType.Events.Remove(def as EventDefinition);
                }
                else if (def is PropertyDefinition)
                {
                    def.DeclaringType.Properties.Remove(def as PropertyDefinition);
                }
            }
        }

        public override string Description
        {
            get
            {
                return @"This confusion reduce the metadata carried by the assembly by removing unnecessary metadata.
***If your application relys on Reflection, you should not apply this confusion***"; }
        }

        public override Target Target
        {
            get { return Target.Events | Target.Properties | Target.Types; }
        }
    }
}
