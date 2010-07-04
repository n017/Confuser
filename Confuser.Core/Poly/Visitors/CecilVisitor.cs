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
        Instruction[] arg;
        bool useDouble;
        public CecilVisitor(Expression exp, bool isReverse, Instruction[] arg, bool useDouble)
        {
            this.arg = arg;
            this.useDouble = useDouble;
            if (isReverse)
                exp.GetVariableExpression().VisitReverse(this, null);
            else
                exp.Visit(this);
        }

        public Instruction[] GetInstructions()
        {
            return insts.ToArray();
        }

        public override void Visit(Expression exp)
        {
            if (exp is ConstantExpression)
            {
                ConstantExpression tExp = exp as ConstantExpression;
                if (useDouble)
                    insts.Add(Instruction.Create(OpCodes.Ldc_R8, tExp.Value));
                else
                    insts.Add(Instruction.Create(OpCodes.Ldc_I8, (long)tExp.Value));
            }
            else if (exp is VariableExpression)
            {
                insts.AddRange(arg);
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
            else if (exp is DivExpression)
            {
                insts.Add(Instruction.Create(OpCodes.Div));
            }
        }

        public override void VisitReverse(Expression exp)
        {
            if (exp is ConstantExpression)
            {
                ConstantExpression tExp = exp as ConstantExpression;
                if (useDouble)
                    insts.Add(Instruction.Create(OpCodes.Ldc_R8, tExp.Value));
                else
                    insts.Add(Instruction.Create(OpCodes.Ldc_I8, (long)tExp.Value));
            }
            else if (exp is VariableExpression)
            {
                insts.AddRange(arg);
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
            else if (exp is DivExpression)
            {
                insts.Add(Instruction.Create(OpCodes.Mul));
            }
        }
    }
}
