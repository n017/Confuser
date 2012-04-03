using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Confuser.Core.Poly.Expressions;

namespace Confuser.Core.Poly
{
    public class ExpressionGenerator
    {
        public int Seed { get; private set; }
        public Type[] ExpressionTypes { get; set; }

        public ExpressionGenerator()
            : this(BitConverter.ToInt32(Guid.NewGuid().ToByteArray(), 0))
        {
        }
        public ExpressionGenerator(int seed)
        {
            Seed = seed;
            ExpressionTypes = new Type[]
            {
                typeof(AddExpression),
                typeof(SubExpression),
                //typeof(MulExpression),
                //typeof(DivExpression),
                typeof(NegExpression),
                typeof(InvExpression),
                typeof(XorExpression)
            };
        }


        public Expression Generate(int lv)
        {
            g = false;
            return Generate(null, lv, new Random(Seed));
        }

        bool g = false;
        internal Expression Generate(Expression parent, int level, Random rand)
        {
            Expression ret = null;
            if (level == 0)
            {
                if (!g)
                {
                    ret = new VariableExpression();
                    g = true;
                }
                else
                {
                    ret = new ConstantExpression();
                    ret.Generate(this, level, rand);
                }
            }
            else
            {
                ret = (Expression)Activator.CreateInstance(ExpressionTypes[rand.Next(0, ExpressionTypes.Length - 1)]);
                ret.Generate(this, level - 1, rand);
            }
            ret.Parent = parent;
            return ret;
        }
    }
}
