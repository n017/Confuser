using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Confuser.Core.Poly.Expressions
{
    public class ConstantExpression : Expression
    {
        int val;
        public int Value { get { return val; } set { val = value; } }

        public override Expression GetVariableExpression()
        {
            return null;
        }

        public override void Visit(ExpressionVisitor visitor)
        {
            visitor.Visit(this);
        }

        public override void VisitReverse(ExpressionVisitor visitor, Expression child)
        {
            visitor.VisitReverse(this);
        }

        public override bool HasVariable
        {
            get { return false; }
        }

        public override string ToString()
        {
            return val.ToString();
        }
    }
}
