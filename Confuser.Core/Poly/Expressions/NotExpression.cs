using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Confuser.Core.Poly.Expressions
{
    public class NotExpression : Expression
    {
        Expression val;
        public Expression Value { get { return val; } set { val = value; } }

        public override Expression GetVariableExpression()
        {
            return val.GetVariableExpression();
        }

        public override void Visit(ExpressionVisitor visitor)
        {
            val.Visit(visitor);
            visitor.Visit(this);
        }

        public override void VisitReverse(ExpressionVisitor visitor, Expression child)
        {
            val.Visit(visitor);
            visitor.VisitReverse(this);
            if (Parent != null && child != null)
                Parent.VisitReverse(visitor, this);
        }

        public override bool HasVariable
        {
            get { return false; }
        }

        public override string ToString()
        {
            return string.Format("~{0}", val);
        }
    }
}
