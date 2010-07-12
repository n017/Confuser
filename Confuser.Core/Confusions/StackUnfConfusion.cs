using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace Confuser.Core.Confusions
{
    public class StackUnfConfusion : StructureConfusion
    {
        Random rad;
        public override void Confuse(int phase, Confuser cr, AssemblyDefinition asm, IMemberDefinition[] defs)
        {
            if (phase != 3) throw new InvalidOperationException();
            rad = new Random();
            foreach (MethodDefinition mtd in defs)
                ProcessMethod(cr, mtd);
        }

        private void ProcessMethod(Confuser cr, MethodDefinition mtd)
        {
            if (!mtd.HasBody) return;
            MethodBody bdy = mtd.Body;
            ILProcessor wkr = bdy.GetILProcessor();

            Instruction original = bdy.Instructions[0];
            Instruction jmp = wkr.Create(OpCodes.Br_S, original);

            Instruction stackundering = wkr.Create(OpCodes.Pop);
            Instruction stackrecovering;
            switch (rad.Next(0, 4))
            {
                case 0:
                    stackrecovering = wkr.Create(OpCodes.Ldnull); break;
                case 1:
                    stackrecovering = wkr.Create(OpCodes.Ldc_I4_0); break;
                case 2:
                    stackrecovering = wkr.Create(OpCodes.Ldstr, ""); break;
                default:
                    stackrecovering = wkr.Create(OpCodes.Ldc_I8, (long)rad.Next()); break;
            }
            wkr.InsertBefore(original, stackrecovering);
            wkr.InsertBefore(stackrecovering, stackundering);
            wkr.InsertBefore(stackundering, jmp);
        }


        public override Priority Priority
        {
            get { return Priority.CodeLevel; }
        }

        public override string Name
        {
            get { return "Stack Underflow Confusion"; }
        }

        public override Phases Phases
        {
            get { return Phases.Phase3; }
        }

        public override bool StandardCompatible
        {
            get { return false; }
        }

        public override string Description
        {
            get { return "This confusion will add a piece of code in the front of the methods and cause decompilers to crash."; }
        }

        public override Target Target
        {
            get { return Target.Methods; }
        }
    }
}
