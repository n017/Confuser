using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Confuser.Core.Poly.Expressions
{
    public class VariableExpression : Expression
    {
        public override Expression GetVariableExpression()
        {
            return this;
        }

        public override void Visit(ExpressionVisitor visitor)
        {
            visitor.Visit(this);
        }

        public override void VisitReverse(ExpressionVisitor visitor, Expression child)
        {
            visitor.VisitReverse(this);
            if (Parent != null)
                Parent.VisitReverse(visitor, this);
        }

        public override bool HasVariable
        {
            get { return true; }
        }

        public override string ToString()
        {
            return "Var";
        }
    }
}
