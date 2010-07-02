using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection.Emit;
using Confuser.Core.Poly.Expressions;

namespace Confuser.Core.Poly.Visitors
{
    public class ReflectionVisitor : ExpressionVisitor
    {
        DynamicMethod dm;
        ILGenerator gen;
        public ReflectionVisitor(Expression exp, bool isReverse)
        {
            dm = new DynamicMethod("", typeof(int), new Type[] { typeof(int) });
            gen = dm.GetILGenerator();
            if (isReverse)
                exp.GetVariableExpression().VisitReverse(this, null);
            else
                exp.Visit(this);
            gen.Emit(OpCodes.Ret);
        }

        public int Eval(int var)
        {
            return (int)dm.Invoke(null, new object[] { var });
        }

        public override void Visit(Expression exp)
        {
            if (exp is ConstantExpression)
            {
                ConstantExpression tExp = exp as ConstantExpression;
                gen.Emit(OpCodes.Ldc_I4, tExp.Value);
            }
            else if (exp is VariableExpression)
            {
                gen.Emit(OpCodes.Ldarg_0);
            }
            else if (exp is AddExpression)
            {
                gen.Emit(OpCodes.Add);
            }
            else if (exp is SubExpression)
            {
                gen.Emit(OpCodes.Sub);
            }
            else if (exp is MulExpression)
            {
                gen.Emit(OpCodes.Mul);
            }
            else if (exp is NegExpression)
            {
                gen.Emit(OpCodes.Neg);
            }
            else if (exp is XorExpression)
            {
                gen.Emit(OpCodes.Xor);
            }
            else if (exp is NotExpression)
            {
                gen.Emit(OpCodes.Not);
            }
        }

        public override void VisitReverse(Expression exp)
        {
            if (exp is ConstantExpression)
            {
                ConstantExpression tExp = exp as ConstantExpression;
                gen.Emit(OpCodes.Ldc_I4, tExp.Value);
            }
            else if (exp is VariableExpression)
            {
                gen.Emit(OpCodes.Ldarg_0);
            }
            else if (exp is AddExpression)
            {
                gen.Emit(OpCodes.Sub);
            }
            else if (exp is SubExpression)
            {
                gen.Emit(OpCodes.Add);
            }
            else if (exp is MulExpression)
            {
                gen.Emit(OpCodes.Div);
            }
            else if (exp is NegExpression)
            {
                gen.Emit(OpCodes.Neg);
            }
            else if (exp is XorExpression)
            {
                gen.Emit(OpCodes.Xor);
            }
            else if (exp is NotExpression)
            {
                gen.Emit(OpCodes.Not);
            }
        }
    }
}
