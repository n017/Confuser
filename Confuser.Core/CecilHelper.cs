using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Confuser.Core
{
    public static class CecilHelper
    {
        public static void RefreshTokens(ModuleDefinition mod)
        {
            int t = 1;
            int f = 1;
            int m = 1;
            foreach (TypeDefinition type in mod.Types)
            {
                RefreshType(ref t, ref f, ref m, type);
            }
        }
        static void RefreshType(ref int type, ref int fld, ref int mtd, TypeDefinition typeDef)
        {
            typeDef.MetadataToken = new MetadataToken(TokenType.TypeDef, type);
            type++;
            foreach (FieldDefinition fldDef in typeDef.Fields)
            {
                fldDef.MetadataToken = new MetadataToken(TokenType.Field, fld);
                fld++;
            }
            foreach (MethodDefinition mtdDef in typeDef.Methods)
            {
                mtdDef.MetadataToken = new MetadataToken(TokenType.Method, mtd);
                mtd++;
            }
            foreach (TypeDefinition nestedDef in typeDef.NestedTypes)
            {
                RefreshType(ref type, ref fld, ref mtd, nestedDef);
            }
        }

        public static TypeDefinition Inject(ModuleDefinition mod, TypeDefinition type)
        {
            TypeDefinition ret = new TypeDefinition(type.Name, type.Namespace, type.Attributes);

            if (type.BaseType != null)
                ret.BaseType = mod.Import(type.BaseType);
            foreach (FieldDefinition fld in type.Fields)
                ret.Fields.Add(new FieldDefinition(fld.Name, fld.Attributes, mod.Import(fld.FieldType)));
            foreach (MethodDefinition mtd in type.Methods)
                ret.Methods.Add(Inject(mod, mtd));


            return ret;
        }
        public static MethodDefinition Inject(ModuleDefinition mod, MethodDefinition mtd)
        {
            MethodDefinition ret = new MethodDefinition(mtd.Name, mtd.Attributes, mod.Import(mtd.ReturnType));

            foreach (ParameterDefinition param in mtd.Parameters)
                ret.Parameters.Add(new ParameterDefinition(param.Name, param.Attributes, mod.Import(param.ParameterType)));

            if (mtd.HasBody)
            {
                MethodBody old = mtd.Body;
                MethodBody bdy = new MethodBody(ret);
                bdy.MaxStackSize = old.MaxStackSize;
                bdy.InitLocals = old.InitLocals;

                ILProcessor psr = bdy.GetILProcessor();

                foreach (VariableDefinition var in old.Variables)
                    bdy.Variables.Add(new VariableDefinition(var.Name, mod.Import(var.VariableType)));

                foreach (Instruction inst in old.Instructions)
                {
                    switch (inst.OpCode.OperandType)
                    {
                        case OperandType.InlineArg:
                        case OperandType.ShortInlineArg:
                            if (inst.Operand == old.ThisParameter)
                                psr.Emit(inst.OpCode, bdy.ThisParameter);
                            else
                            {
                                int param = mtd.Parameters.IndexOf(inst.Operand as ParameterDefinition);
                                psr.Emit(inst.OpCode, ret.Parameters[param]);
                            }
                            break;
                        case OperandType.InlineVar:
                        case OperandType.ShortInlineVar:
                            int var = old.Variables.IndexOf(inst.Operand as VariableDefinition);
                            psr.Emit(inst.OpCode, bdy.Variables[var]);
                            break;
                        case OperandType.InlineField:
                            psr.Emit(inst.OpCode, mod.Import(inst.Operand as FieldReference));
                            break;
                        case OperandType.InlineMethod:
                            psr.Emit(inst.OpCode, mod.Import(inst.Operand as MethodReference));
                            break;
                        case OperandType.InlineType:
                            psr.Emit(inst.OpCode, mod.Import(inst.Operand as TypeReference));
                            break;
                        case OperandType.InlineTok:
                            if (inst.Operand is TypeReference)
                                psr.Emit(inst.OpCode, mod.Import(inst.Operand as TypeReference));
                            else if (inst.Operand is FieldReference)
                                psr.Emit(inst.OpCode, mod.Import(inst.Operand as FieldReference));
                            else if (inst.Operand is MethodReference)
                                psr.Emit(inst.OpCode, mod.Import(inst.Operand as MethodReference));
                            break;
                        default:
                            psr.Append(inst);
                            break;
                    }
                }

                for (int i = 0; i < bdy.Instructions.Count; i++)
                {
                    Instruction inst = bdy.Instructions[i];
                    Instruction o = old.Instructions[i];

                    if (inst.OpCode.OperandType == OperandType.InlineSwitch)
                    {
                        Instruction[] olds = (Instruction[])o.Operand;
                        Instruction[] news = new Instruction[olds.Length];

                        for (int ii = 0; ii < news.Length; ii++)
                            news[ii] = bdy.Instructions[old.Instructions.IndexOf(olds[ii])];

                        inst.Operand = news;
                    }
                    else if (inst.OpCode.OperandType == OperandType.ShortInlineBrTarget || inst.OpCode.OperandType == OperandType.InlineBrTarget)
                        inst.Operand = bdy.Instructions[old.Instructions.IndexOf(inst.Operand as Instruction)];
                }

                foreach (ExceptionHandler eh in old.ExceptionHandlers)
                {
                    ExceptionHandler neh = new ExceptionHandler(eh.HandlerType);
                    if (old.Instructions.IndexOf(eh.TryStart) != -1)
                        neh.TryStart = bdy.Instructions[old.Instructions.IndexOf(eh.TryStart)];
                    if (old.Instructions.IndexOf(eh.TryEnd) != -1)
                        neh.TryEnd = bdy.Instructions[old.Instructions.IndexOf(eh.TryEnd)];
                    if (old.Instructions.IndexOf(eh.HandlerStart) != -1)
                        neh.HandlerStart = bdy.Instructions[old.Instructions.IndexOf(eh.HandlerStart)];
                    if (old.Instructions.IndexOf(eh.HandlerEnd) != -1)
                        neh.HandlerEnd = bdy.Instructions[old.Instructions.IndexOf(eh.HandlerEnd)];

                    switch (eh.HandlerType)
                    {
                        case ExceptionHandlerType.Catch:
                            neh.CatchType = mod.Import(eh.CatchType);
                            break;
                        case ExceptionHandlerType.Filter:
                            neh.FilterStart = bdy.Instructions[old.Instructions.IndexOf(eh.FilterStart)];
                            neh.FilterEnd = bdy.Instructions[old.Instructions.IndexOf(eh.FilterEnd)];
                            break;
                    }

                    bdy.ExceptionHandlers.Add(neh);
                }

                ret.Body = bdy;
            }

            return ret;
        }
    }
}
