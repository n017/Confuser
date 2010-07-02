using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Confuser.Core.Poly
{
    public abstract class ExpressionVisitor
    {
        public abstract void Visit(Expression exp);
        public abstract void VisitReverse(Expression exp);
    }
}
