using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Confuser.Core.Poly.Expressions;

namespace Confuser.Core.Poly
{
    public static class ExpressionGenerator
    {
        public static Expression Generate(int lv, int seed)
        {
            Expression exp;
            bool hasVar = false;
            exp = Generate(null, lv, ref hasVar, new Random(seed));
            if (!exp.HasVariable)
                return null;
            else
                return exp;
        }

        public static Expression Generate(int lv, ref int seed)
        {
            Expression exp;
            Random rand = new Random();
            do
            {
                seed = rand.Next();
                bool hasVar = false;
                exp = Generate(null, lv, ref hasVar, new Random(seed));
            } while (!exp.HasVariable);
            return exp;
        }

        static Expression Generate(Expression par, int lv, ref bool hasVar, Random rand)
        {
            Expression ret = null;
            if (lv == 0)
            {
                if (!hasVar && rand.NextDouble() > 0.5)
                {
                    ret = new VariableExpression();
                    hasVar = true;
                }
                else
                {
                    ret = new ConstantExpression() { Value = rand.Next(-10, 10) };
                }
            }
            else
            {
                int a = rand.NextDouble() > 0.25 ? lv - 1 : 0;
                int b = rand.NextDouble() > 0.25 ? lv - 1 : 0;
                switch (rand.Next(0, 6))
                {
                    case 0:
                        ret = new AddExpression();
                        (ret as AddExpression).OperandA = Generate(ret, a, ref hasVar, rand);
                        (ret as AddExpression).OperandB = Generate(ret, b, ref hasVar, rand);
                        break;
                    case 1:
                        ret = new SubExpression();
                        (ret as SubExpression).OperandA = Generate(ret, a, ref hasVar, rand);
                        (ret as SubExpression).OperandB = Generate(ret, b, ref hasVar, rand);
                        break;
                    case 2:
                        ret = new MulExpression();
                        (ret as MulExpression).OperandA = Generate(ret, a, ref hasVar, rand);
                        (ret as MulExpression).OperandB = Generate(ret, b, ref hasVar, rand);
                        break;
                    case 3:
                        ret = new NegExpression();
                        (ret as NegExpression).Value = Generate(ret, a, ref hasVar, rand);
                        break;
                    case 4:
                        ret = new XorExpression();
                        (ret as XorExpression).OperandA = Generate(ret, a, ref hasVar, rand);
                        (ret as XorExpression).OperandB = Generate(ret, b, ref hasVar, rand);
                        break;
                    case 5:
                        ret = new NotExpression();
                        (ret as NotExpression).Value = Generate(ret, a, ref hasVar, rand);
                        break;
                }
            }
            ret.Parent = par;
            return ret;
        }
    }
}
