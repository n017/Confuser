using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Confuser.Core.Poly.Expressions
{
    public class AddExpression : Expression
    {
        Expression a;
        public Expression OperandA { get { return a; } set { a = value; } }
        Expression b;
        public Expression OperandB { get { return b; } set { b = value; } }

        public override Expression GetVariableExpression()
        {
            if (a.HasVariable) return a.GetVariableExpression();
            if (b.HasVariable) return b.GetVariableExpression();
            return null;
        }

        public override void Visit(ExpressionVisitor visitor)
        {
            a.Visit(visitor);
            b.Visit(visitor);
            visitor.Visit(this);
        }

        public override void VisitReverse(ExpressionVisitor visitor, Expression child)
        {
            if (child != null && Parent != null)
                Parent.VisitReverse(visitor, this);

            if (child == a)
                b.Visit(visitor);
            else if (child == b)
                a.Visit(visitor);
            visitor.VisitReverse(this);
        }

        public override bool HasVariable
        {
            get { return a.HasVariable || b.HasVariable; }
        }

        public override string ToString()
        {
            return string.Format("({0}+{1})", a, b);
        }
    }
}
