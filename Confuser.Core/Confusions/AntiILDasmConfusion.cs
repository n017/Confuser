using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using System.Runtime.CompilerServices;

namespace Confuser.Core.Confusions
{
    class AntiILDasmConfusion : StructureConfusion
    {
        public override void PreConfuse(Confuser cr, AssemblyDefinition asm)
        {
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

        public override void DoConfuse(Confuser cr, AssemblyDefinition asm)
        {
            throw new InvalidOperationException();
        }

        public override void PostConfuse(Confuser cr, AssemblyDefinition asm)
        {
            throw new InvalidOperationException();
        }

        public override Priority Priority
        {
            get { return Priority.AssemblyLevel; }
        }

        public override string Name
        {
            get { return "Anti IL Dasm Confusion"; }
        }

        public override ProcessType Process
        {
            get { return ProcessType.Pre; }
        }

        public override bool StandardCompatible
        {
            get { return true; }
        }
    }
}
