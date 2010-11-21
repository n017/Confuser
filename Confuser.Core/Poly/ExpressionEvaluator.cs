using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Confuser.Core.Poly.Expressions;

namespace Confuser.Core.Poly
{
    public static class DoubleExpressionEvaluator
    {
        public static double Evaluate(Expression exp, double var)
        {
            if (exp is ConstantExpression)
            {
                ConstantExpression tExp = exp as ConstantExpression;
                return (exp as ConstantExpression).Value;
            }
            else if (exp is VariableExpression)
            {
                return var;
            }
            else if (exp is AddExpression)
            {
                AddExpression nExp = (AddExpression)exp;
                return Evaluate(nExp.OperandA, var) + Evaluate(nExp.OperandB, var);
            }
            else if (exp is SubExpression)
            {
                SubExpression nExp = (SubExpression)exp;
                return Evaluate(nExp.OperandA, var) - Evaluate(nExp.OperandB, var);
            }
            else if (exp is MulExpression)
            {
                MulExpression nExp = (MulExpression)exp;
                return Evaluate(nExp.OperandA, var) * Evaluate(nExp.OperandB, var);
            }
            else if (exp is NegExpression)
            {
                NegExpression nExp = (NegExpression)exp;
                return -Evaluate(nExp.Value, var);
            }
            else if (exp is DivExpression)
            {
                DivExpression nExp = (DivExpression)exp;
                return Evaluate(nExp.OperandA, var) / Evaluate(nExp.OperandB, var);
            }
            throw new NotSupportedException();
        }

        public static double ReverseEvaluate(Expression exp, double val)
        {
            if (exp is ConstantExpression)
            {
                ConstantExpression tExp = exp as ConstantExpression;
                return (exp as ConstantExpression).Value;
            }
            else if (exp is VariableExpression)
            {
                return val;
            }
            else if (exp is AddExpression)
            {
                AddExpression nExp = (AddExpression)exp;
                if (nExp.OperandB.HasVariable)
                    return ReverseEvaluate(nExp.OperandB, val - ReverseEvaluate(nExp.OperandA, val));
                else if (nExp.OperandA.HasVariable)
                    return ReverseEvaluate(nExp.OperandA, val - ReverseEvaluate(nExp.OperandB, val));
                else
                    return Evaluate(nExp, val);
            }
            else if (exp is SubExpression)
            {
                SubExpression nExp = (SubExpression)exp;
                if (nExp.OperandB.HasVariable)
                    return ReverseEvaluate(nExp.OperandB, ReverseEvaluate(nExp.OperandA, val) - val);
                else if (nExp.OperandA.HasVariable)
                    return ReverseEvaluate(nExp.OperandA, val + ReverseEvaluate(nExp.OperandB, val));
                else
                    return Evaluate(nExp, val);
            }
            else if (exp is MulExpression)
            {
                MulExpression nExp = (MulExpression)exp;
                if (nExp.OperandB.HasVariable)
                    return ReverseEvaluate(nExp.OperandB, val / ReverseEvaluate(nExp.OperandA, val));
                else if (nExp.OperandA.HasVariable)
                    return ReverseEvaluate(nExp.OperandA, val / ReverseEvaluate(nExp.OperandB, val));
                else
                    return Evaluate(nExp, val);
            }
            else if (exp is NegExpression)
            {
                NegExpression nExp = (NegExpression)exp;
                return -ReverseEvaluate(nExp.Value, val);
            }
            else if (exp is DivExpression)
            {
                DivExpression nExp = (DivExpression)exp;
                if (nExp.OperandB.HasVariable)
                    return ReverseEvaluate(nExp.OperandB, ReverseEvaluate(nExp.OperandA, val) / val);
                else if (nExp.OperandA.HasVariable)
                    return ReverseEvaluate(nExp.OperandA, val * ReverseEvaluate(nExp.OperandB, val));
                else
                    return Evaluate(nExp, val);
            }
            throw new NotSupportedException();
        }
    }

    public class LongExpressionEvaluator
    {
        public static long Evaluate(Expression exp, long var)
        {
            if (exp is ConstantExpression)
            {
                ConstantExpression tExp = exp as ConstantExpression;
                return (long)(exp as ConstantExpression).Value;
            }
            else if (exp is VariableExpression)
            {
                return var;
            }
            else if (exp is AddExpression)
            {
                AddExpression nExp = (AddExpression)exp;
                return Evaluate(nExp.OperandA, var) + Evaluate(nExp.OperandB, var);
            }
            else if (exp is SubExpression)
            {
                SubExpression nExp = (SubExpression)exp;
                return Evaluate(nExp.OperandA, var) - Evaluate(nExp.OperandB, var);
            }
            else if (exp is MulExpression)
            {
                MulExpression nExp = (MulExpression)exp;
                return Evaluate(nExp.OperandA, var) * Evaluate(nExp.OperandB, var);
            }
            else if (exp is NegExpression)
            {
                NegExpression nExp = (NegExpression)exp;
                return -Evaluate(nExp.Value, var);
            }
            else if (exp is DivExpression)
            {
                DivExpression nExp = (DivExpression)exp;
                return Evaluate(nExp.OperandA, var) / Evaluate(nExp.OperandB, var);
            }
            throw new NotSupportedException();
        }

        public static long ReverseEvaluate(Expression exp, long val)
        {
            if (exp is ConstantExpression)
            {
                ConstantExpression tExp = exp as ConstantExpression;
                return (long)(exp as ConstantExpression).Value;
            }
            else if (exp is VariableExpression)
            {
                return val;
            }
            else if (exp is AddExpression)
            {
                AddExpression nExp = (AddExpression)exp;
                if (nExp.OperandB.HasVariable)
                    return ReverseEvaluate(nExp.OperandB, val - ReverseEvaluate(nExp.OperandA, val));
                else if (nExp.OperandA.HasVariable)
                    return ReverseEvaluate(nExp.OperandA, val - ReverseEvaluate(nExp.OperandB, val));
                else
                    return Evaluate(nExp, val);
            }
            else if (exp is SubExpression)
            {
                SubExpression nExp = (SubExpression)exp;
                if (nExp.OperandB.HasVariable)
                    return ReverseEvaluate(nExp.OperandB, ReverseEvaluate(nExp.OperandA, val) - val);
                else if (nExp.OperandA.HasVariable)
                    return ReverseEvaluate(nExp.OperandA, val + ReverseEvaluate(nExp.OperandB, val));
                else
                    return Evaluate(nExp, val);
            }
            else if (exp is MulExpression)
            {
                MulExpression nExp = (MulExpression)exp;
                if (nExp.OperandB.HasVariable)
                    return ReverseEvaluate(nExp.OperandB, val / ReverseEvaluate(nExp.OperandA, val));
                else if (nExp.OperandA.HasVariable)
                    return ReverseEvaluate(nExp.OperandA, val / ReverseEvaluate(nExp.OperandB, val));
                else
                    return Evaluate(nExp, val);
            }
            else if (exp is NegExpression)
            {
                NegExpression nExp = (NegExpression)exp;
                return -ReverseEvaluate(nExp.Value, val);
            }
            else if (exp is DivExpression)
            {
                DivExpression nExp = (DivExpression)exp;
                if (nExp.OperandB.HasVariable)
                    return ReverseEvaluate(nExp.OperandB, ReverseEvaluate(nExp.OperandA, val) / val);
                else if (nExp.OperandA.HasVariable)
                    return ReverseEvaluate(nExp.OperandA, val * ReverseEvaluate(nExp.OperandB, val));
                else
                    return Evaluate(nExp, val);
            }
            throw new NotSupportedException();
        }
    }
}