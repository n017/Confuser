using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil.Cil;
using Confuser.Core.Poly.Expressions;

namespace Confuser.Core.Poly.Visitors
{
    public class CecilVisitor : ExpressionVisitor
    {
        List<Instruction> insts = new List<Instruction>();
        public CecilVisitor(Expression exp, bool isReverse)
        {
            if (isReverse)
                exp.GetVariableExpression().VisitReverse(this, null);
            else
                exp.Visit(this);
        }

        public Instruction[] GetInstructions(int var)
        {
            return insts.ToArray();
        }

        public override void Visit(Expression exp)
        {
            if (exp is ConstantExpression)
            {
                ConstantExpression tExp = exp as ConstantExpression;
                insts.Add(Instruction.Create(OpCodes.Ldc_I4, tExp.Value));
            }
            else if (exp is VariableExpression)
            {
                insts.Add(Instruction.Create(OpCodes.Ldarg_0));
            }
            else if (exp is AddExpression)
            {
                insts.Add(Instruction.Create(OpCodes.Add));
            }
            else if (exp is SubExpression)
            {
                insts.Add(Instruction.Create(OpCodes.Sub));
            }
            else if (exp is MulExpression)
            {
                insts.Add(Instruction.Create(OpCodes.Mul));
            }
            else if (exp is NegExpression)
            {
                insts.Add(Instruction.Create(OpCodes.Neg));
            }
            else if (exp is XorExpression)
            {
                insts.Add(Instruction.Create(OpCodes.Xor));
            }
            else if (exp is NotExpression)
            {
                insts.Add(Instruction.Create(OpCodes.Not));
            }
        }

        public override void VisitReverse(Expression exp)
        {
            if (exp is ConstantExpression)
            {
                ConstantExpression tExp = exp as ConstantExpression;
                insts.Add(Instruction.Create(OpCodes.Ldc_I4, tExp.Value));
            }
            else if (exp is VariableExpression)
            {
                insts.Add(Instruction.Create(OpCodes.Ldarg_0));
            }
            else if (exp is AddExpression)
            {
                insts.Add(Instruction.Create(OpCodes.Sub));
            }
            else if (exp is SubExpression)
            {
                insts.Add(Instruction.Create(OpCodes.Add));
            }
            else if (exp is MulExpression)
            {
                insts.Add(Instruction.Create(OpCodes.Div));
            }
            else if (exp is NegExpression)
            {
                insts.Add(Instruction.Create(OpCodes.Neg));
            }
            else if (exp is XorExpression)
            {
                insts.Add(Instruction.Create(OpCodes.Xor));
            }
            else if (exp is NotExpression)
            {
                insts.Add(Instruction.Create(OpCodes.Not));
            }
        }
    }
}
