using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using System.Runtime.CompilerServices;

namespace Confuser.Core.Confusions
{
    public class AntiILDasmConfusion : StructureConfusion
    {
        public override void Confuse(int phase, Confuser cr, AssemblyDefinition asm, IMemberDefinition[] defs)
        {
            if (phase != 1) throw new InvalidOperationException();
            MethodReference ctor = asm.MainModule.Import(typeof(SuppressIldasmAttribute).GetConstructor(Type.EmptyTypes));
            bool has = false;
            foreach (CustomAttribute att in asm.MainModule.CustomAttributes)
                if (att.Constructor.ToString() == ctor.ToString())
                {
                    has = true;
                    break;
                }

            if (!has)
                asm.MainModule.CustomAttributes.Add(new CustomAttribute(ctor));
        }

        public override Priority Priority
        {
            get { return Priority.AssemblyLevel; }
        }

        public override string Name
        {
            get { return "Anti IL Dasm Confusion"; }
        }

        public override Phases Phases
        {
            get { return Phases.Phase1; }
        }

        public override bool StandardCompatible
        {
            get { return true; }
        }

        public override string Description
        {
            get { return "This confusion marked the assembly with a attribute and ILDasm would not disassemble the assemblies with this attribute."; }
        }

        public override Target Target
        {
            get { return Target.Whole; }
        }
    }
}
