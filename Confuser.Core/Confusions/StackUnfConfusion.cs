using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Confuser.Core.Confusions
{
    class StackUnfConfusion : StructureConfusion
    {
        Random rad = new Random();

        public override void DoConfuse(Confuser cr, AssemblyDefinition asm)
        {
            foreach (TypeDefinition def in asm.MainModule.Types)
            {
                ProcessMethods(cr, def);
            }
        }
        private void ProcessMethods(Confuser cr, TypeDefinition def)
        {
            foreach (TypeDefinition t in def.NestedTypes)
            {
                ProcessMethods(cr, t);
            }
            foreach (MethodDefinition mtd in def.Constructors)
            {
                ProcessMethod(cr, mtd);
            }
            foreach (MethodDefinition mtd in def.Methods)
            {
                ProcessMethod(cr, mtd);
            }
        }
        private void ProcessMethod(Confuser cr, MethodDefinition mtd)
        {
            if (!mtd.HasBody) return;
            CilWorker wkr = mtd.Body.CilWorker;

            Instruction original = mtd.Body.Instructions[0];
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
            wkr.InsertBefore(original, stackundering);
            wkr.InsertBefore(stackundering, stackrecovering);
            wkr.InsertBefore(stackrecovering, jmp);
            cr.Log("<method name='" + mtd.ToString() + "'/>");
        }

        public override void PreConfuse(Confuser cr, AssemblyDefinition asm)
        {
            throw new InvalidOperationException();
        }
        public override void PostConfuse(Confuser cr, AssemblyDefinition asm)
        {
            throw new InvalidOperationException();
        }

        public override Priority Priority
        {
            get { return Priority.CodeLevel; }
        }

        public override string Name
        {
            get { return "Stack Underflow Confusion"; }
        }

        public override ProcessType Process
        {
            get { return ProcessType.Real; }
        }

        public override bool StandardCompatible
        {
            get { return false; }
        }
    }
}
