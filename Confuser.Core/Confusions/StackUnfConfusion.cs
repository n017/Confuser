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

        private void ProcessMethods(Confuser cr, TypeDefinition def)
        {
            foreach (MethodDefinition mtd in def.Methods)
            {
                ProcessMethod(cr, mtd);
            }
        }
        private void ProcessMethod(Confuser cr, MethodDefinition mtd)
        {
            if (!mtd.HasBody) return;
            MethodBody bdy = mtd.Body ;
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
            cr.Log("<method name='" + mtd.ToString() + "'/>");
        }

        public override void PreConfuse(Confuser cr, AssemblyDefinition asm)
        {
            throw new InvalidOperationException();
        }
        public override void DoConfuse(Confuser cr, AssemblyDefinition asm)
        {
            throw new InvalidOperationException();
        }
        public override void PostConfuse(Confuser cr, AssemblyDefinition asm)
        {
            rad = new Random();
            foreach (TypeDefinition def in asm.MainModule.GetAllTypes())
            {
                ProcessMethods(cr, def);
            }
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
            get { return ProcessType.Post; }
        }

        public override bool StandardCompatible
        {
            get { return false; }
        }
    }
}
