using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Confuser.Core.Poly.Expressions;

namespace Confuser.Core.Poly
{
    public interface IExpressionEvaluator
    {
        object Evaluate(Expression exp);
        object ReverseEvaluate(Expression exp);
    }

    public class DoubleExpressionEvaluator:IExpressionEvaluator
    {
        double var;
        public DoubleExpressionEvaluator(double var) { this.var = var; }

        public object Evaluate(Expression exp)
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
                return (double)nExp.OperandA.Evaluate(this) + (double)nExp.OperandB.Evaluate(this);
            }
            else if (exp is SubExpression)
            {
                SubExpression nExp = (SubExpression)exp;
                return (double)nExp.OperandA.Evaluate(this) - (double)nExp.OperandB.Evaluate(this);
            }
            else if (exp is MulExpression)
            {
                MulExpression nExp = (MulExpression)exp;
                return (double)nExp.OperandA.Evaluate(this) * (double)nExp.OperandB.Evaluate(this);
            }
            else if (exp is NegExpression)
            {
                NegExpression nExp = (NegExpression)exp;
                return -(double)nExp.Value.Evaluate(this);
            }
            else if (exp is DivExpression)
            {
                DivExpression nExp = (DivExpression)exp;
                return (double)nExp.OperandA.Evaluate(this) / (double)nExp.OperandB.Evaluate(this);
            }
            throw new NotSupportedException();
        }

        public object ReverseEvaluate(Expression exp, object now)
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
                if (nExp.OperandB.HasVariable)
                    return (double)now - (double)nExp.OperandA.Evaluate(this);
                else
                    return (double)now - (double)nExp.OperandB.Evaluate(this);
            }
            else if (exp is SubExpression)
            {
                SubExpression nExp = (SubExpression)exp;
                if (nExp.OperandB.HasVariable)
                    return (double)now - (double)nExp.OperandA.Evaluate(this);
                else
                    return (double)now + (double)nExp.OperandB.Evaluate(this);
            }
            else if (exp is MulExpression)
            {
                MulExpression nExp = (MulExpression)exp;
                if (nExp.OperandB.HasVariable)
                    return (double)now / (double)nExp.OperandA.Evaluate(this);
                else
                    return (double)now / (double)nExp.OperandB.Evaluate(this);
            }
            else if (exp is NegExpression)
            {
                NegExpression nExp = (NegExpression)exp;
                return -(double)nExp.Value.Evaluate(this);
            }
            else if (exp is DivExpression)
            {
                DivExpression nExp = (DivExpression)exp;
                if (nExp.OperandB.HasVariable)
                    return (double)nExp.OperandA.Evaluate(this) / (double)now;
                else
                    return (double)now * (double)nExp.OperandB.Evaluate(this);
            }
            throw new NotSupportedException();
        }
    }
}
