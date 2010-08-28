using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Confuser.Core.Poly;
using Confuser.Core.Poly.Visitors;

namespace Confuser.Core.Confusions
{
    public class DisConstConfusion : StructurePhase, IConfusion
    {
        public string Name
        {
            get { return "Constant Disintegration Confusion"; }
        }
        public string Description
        {
            get
            {
                return @"This confusion disintegrate the constants in the code into expression.
***This confusion could affect the performance if your application uses constant frequently***";
            }
        }
        public string ID
        {
            get { return "disintegrate const"; }
        }
        public bool StandardCompatible
        {
            get { return true; }
        }
        public Target Target
        {
            get { return Target.Methods; }
        }
        public Preset Preset
        {
            get { return Preset.Maximum; }
        }
        public Phase[] Phases
        {
            get { return new Phase[] { this }; }
        }

        public override Priority Priority
        {
            get { return Priority.CodeLevel; }
        }
        public override IConfusion Confusion
        {
            get { return this; }
        }
        public override int PhaseID
        {
            get { return 3; }
        }
        public override bool WholeRun
        {
            get { return false; }
        }
        public override void Initialize(AssemblyDefinition asm)
        {
            //
        }
        public override void DeInitialize()
        {
            //
        }

        public override void Process(ConfusionParameter parameter)
        {
            MethodDefinition mtd = parameter.Target as MethodDefinition;

            int lv = 5;
            if (Array.IndexOf(parameter.Parameters.AllKeys, "level") != -1)
            {
                if (!int.TryParse(parameter.Parameters["level"], out lv) && (lv <= 0 || lv > 10))
                {
                    Log("Invaild level, 5 will be used.");
                    lv = 5;
                }
            }

            if (!mtd.HasBody) return;
            MethodBody body = mtd.Body;
            body.SimplifyMacros();
            ILProcessor psr = body.GetILProcessor();
            for (int i = 0; i < body.Instructions.Count; i++)
            {
                if (body.Instructions[i].OpCode.Name == "ldc.i4" ||
                    body.Instructions[i].OpCode.Name == "ldc.i8" ||
                    body.Instructions[i].OpCode.Name == "ldc.r4" ||
                    body.Instructions[i].OpCode.Name == "ldc.r8")
                {
                    double val = Convert.ToDouble(body.Instructions[i].Operand);
                    int seed;

                    Expression exp;
                    double eval = 0;
                    double tmp = 0;
                    do
                    {
                        exp = ExpressionGenerator.Generate(lv, out seed);
                        eval = (double)new ReflectionVisitor(exp, false, true).Eval(val);
                        try
                        {
                            tmp = (double)new ReflectionVisitor(exp, true, true).Eval(eval);
                        }
                        catch { continue; }
                    } while (tmp != val);

                    Instruction[] expInsts = new CecilVisitor(exp, true, new Instruction[] { Instruction.Create(OpCodes.Ldc_R8, eval) }, true).GetInstructions();
                    if (expInsts.Length == 0) continue;
                    string op = body.Instructions[i].OpCode.Name;
                    psr.Replace(body.Instructions[i], expInsts[0]);
                    for (int ii = 1; ii < expInsts.Length; ii++)
                    {
                        psr.InsertAfter(expInsts[ii - 1], expInsts[ii]);
                        i++;
                    }
                    switch (op)
                    {
                        case "ldc.i4":
                            psr.InsertAfter(expInsts[expInsts.Length - 1], Instruction.Create(OpCodes.Conv_I4)); break;
                        case "ldc.i8":
                            psr.InsertAfter(expInsts[expInsts.Length - 1], Instruction.Create(OpCodes.Conv_I8)); break;
                        case "ldc.r4":
                            psr.InsertAfter(expInsts[expInsts.Length - 1], Instruction.Create(OpCodes.Conv_R4)); break;
                        case "ldc.r8":
                            psr.InsertAfter(expInsts[expInsts.Length - 1], Instruction.Create(OpCodes.Conv_R8)); break;
                    }
                }
            }
        }
    }
}