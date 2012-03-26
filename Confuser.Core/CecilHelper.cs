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

        static TypeReference ImportType(TypeReference typeRef, ModuleDefinition mod, Dictionary<MetadataToken, IMemberDefinition> mems)
        {
            TypeReference ret = typeRef;
            if (typeRef is TypeSpecification)
            {
                if (typeRef is ArrayType)
                {
                    ArrayType _spec = typeRef as ArrayType;
                    ret = new ArrayType(ImportType(_spec.ElementType, mod, mems));
                    (ret as ArrayType).Dimensions.Clear();
                    foreach (var i in _spec.Dimensions) (ret as ArrayType).Dimensions.Add(i);
                }
                else if (typeRef is GenericInstanceType)
                {
                    GenericInstanceType _spec = typeRef as GenericInstanceType;
                    ret = new GenericInstanceType(ImportType(_spec.ElementType, mod, mems));
                    foreach (var i in _spec.GenericArguments) (ret as GenericInstanceType).GenericArguments.Add(ImportType(i, mod, mems));
                }
                else if (typeRef is OptionalModifierType)
                {
                    ret = new OptionalModifierType(ImportType((typeRef as OptionalModifierType).ModifierType, mod, mems),
                      ImportType((typeRef as TypeSpecification).ElementType, mod, mems));
                }
                else if (typeRef is RequiredModifierType)
                {
                    ret = new RequiredModifierType(ImportType((typeRef as RequiredModifierType).ModifierType, mod, mems),
                        ImportType((typeRef as TypeSpecification).ElementType, mod, mems));
                }
                else if (typeRef is ByReferenceType)
                    ret = new ByReferenceType(ImportType((typeRef as TypeSpecification).ElementType, mod, mems));
                else if (typeRef is PointerType)
                    ret = new PointerType(ImportType((typeRef as TypeSpecification).ElementType, mod, mems));
                else if (typeRef is PinnedType)
                    ret = new PinnedType(ImportType((typeRef as TypeSpecification).ElementType, mod, mems));
                else if (typeRef is SentinelType)
                    ret = new SentinelType(ImportType((typeRef as TypeSpecification).ElementType, mod, mems));
                else
                    throw new NotSupportedException();
            }
            else if (typeRef is GenericParameter)
                return ret;
            else
            {
                if (mems.ContainsKey(typeRef.MetadataToken))
                    ret = mems[typeRef.MetadataToken] as TypeReference;
                else if (!(ret is TypeDefinition))
                    ret = mod.Import(ret);
            }
            return ret;
        }
        static MethodReference ImportMethod(MethodReference mtdRef, ModuleDefinition mod, Dictionary<MetadataToken, IMemberDefinition> mems)
        {
            MethodReference ret = mtdRef;
            if (mtdRef is GenericInstanceMethod)
            {
                GenericInstanceMethod _spec = mtdRef as GenericInstanceMethod;
                ret = new GenericInstanceMethod(ImportMethod(_spec.ElementMethod, mod, mems));
                foreach (var i in _spec.GenericArguments) (ret as GenericInstanceMethod).GenericArguments.Add(ImportType(i, mod, mems));

                ret.ReturnType = ImportType(ret.ReturnType, mod, mems);
                foreach (var i in ret.Parameters)
                    i.ParameterType = ImportType(i.ParameterType, mod, mems);
            }
            else
            {
                if (mems.ContainsKey(mtdRef.MetadataToken))
                    ret = mems[mtdRef.MetadataToken] as MethodReference;
                else
                {
                    ret = mod.Import(ret);
                    ret.ReturnType = ImportType(ret.ReturnType, mod, mems);
                    foreach (var i in ret.Parameters)
                        i.ParameterType = ImportType(i.ParameterType, mod, mems);
                }
            }
            if (!(mtdRef is MethodDefinition))
                ret.DeclaringType = ImportType(mtdRef.DeclaringType, mod, mems);
            return ret;
        }

        public static TypeDefinition Inject(ModuleDefinition mod, TypeDefinition type)
        {
            type.Module.FullLoad();
            var dict = new Dictionary<MetadataToken, IMemberDefinition>();
            TypeDefinition ret = _Inject(mod, type, dict);
            PopulateDatas(mod, type, dict);
            return ret;
        }
        static TypeDefinition _Inject(ModuleDefinition mod, TypeDefinition type, Dictionary<MetadataToken, IMemberDefinition> mems)
        {
            TypeDefinition ret = new TypeDefinition(type.Namespace, type.Name, type.Attributes);
            ret.Scope = mod;
            ret.ClassSize = type.ClassSize;
            ret.PackingSize = type.PackingSize;

            if (type.BaseType != null)
                ret.BaseType = mod.Import(type.BaseType);

            mems.Add(type.MetadataToken, ret);
            foreach (TypeDefinition ty in type.NestedTypes)
            {
                TypeDefinition t = _Inject(mod, ty, mems);
                ret.NestedTypes.Add(t);
            }
            foreach (FieldDefinition fld in type.Fields)
            {
                if (fld.IsLiteral) continue;
                FieldDefinition n = new FieldDefinition(fld.Name, fld.Attributes, mod.TypeSystem.Void);
                mems.Add(fld.MetadataToken, n);
                ret.Fields.Add(n);
            }
            foreach (MethodDefinition mtd in type.Methods)
            {
                MethodDefinition n = new MethodDefinition(mtd.Name, mtd.Attributes, mtd.ReturnType);
                mems.Add(mtd.MetadataToken, n);
                ret.Methods.Add(n);
            }

            return ret;
        }
        static void PopulateDatas(ModuleDefinition mod, TypeDefinition type, Dictionary<MetadataToken, IMemberDefinition> mems)
        {
            TypeDefinition newType = mems[type.MetadataToken] as TypeDefinition;

            if (type.BaseType != null)
                newType.BaseType = ImportType(type.BaseType, mod, mems);

            foreach (TypeDefinition ty in type.NestedTypes)
            {
                PopulateDatas(mod, ty, mems);
            }
            foreach (FieldDefinition fld in type.Fields)
            {
                if (fld.IsLiteral) continue;
                (mems[fld.MetadataToken] as FieldDefinition).FieldType = ImportType(fld.FieldType, mod, mems);
            }
            foreach (MethodDefinition mtd in type.Methods)
            {
                PopulateMethod(mod, mtd, mems[mtd.MetadataToken] as MethodDefinition, mems);
            }
        }
        public static void PopulateMethod(ModuleDefinition mod, MethodDefinition mtd, MethodDefinition newMtd, Dictionary<MetadataToken, IMemberDefinition> mems)
        {
            mtd.Module.FullLoad();

            newMtd.Attributes = mtd.Attributes;
            newMtd.ImplAttributes = mtd.ImplAttributes;
            newMtd.ReturnType = ImportType(mtd.ReturnType, mod, mems);
            if (mtd.IsPInvokeImpl)
            {
                newMtd.PInvokeInfo = mtd.PInvokeInfo;
                bool has = false;
                foreach (ModuleReference modRef in mod.ModuleReferences)
                    if (modRef.Name == newMtd.PInvokeInfo.Module.Name)
                    {
                        has = true;
                        newMtd.PInvokeInfo.Module = modRef;
                        break;
                    }
                if (!has)
                    mod.ModuleReferences.Add(newMtd.PInvokeInfo.Module);
            }
            if (mtd.HasCustomAttributes)
            {
                foreach (CustomAttribute attr in mtd.CustomAttributes)
                {
                    CustomAttribute nAttr = new CustomAttribute(ImportMethod(attr.Constructor, mod, mems), attr.GetBlob());
                    newMtd.CustomAttributes.Add(nAttr);
                }
            }

            foreach (ParameterDefinition param in mtd.Parameters)
                newMtd.Parameters.Add(new ParameterDefinition(param.Name, param.Attributes, ImportType(param.ParameterType, mod, mems)));

            if (mtd.HasBody)
            {
                MethodBody old = mtd.Body;
                MethodBody bdy = new MethodBody(newMtd);
                bdy.MaxStackSize = old.MaxStackSize;
                bdy.InitLocals = old.InitLocals;

                ILProcessor psr = bdy.GetILProcessor();

                foreach (VariableDefinition var in old.Variables)
                    bdy.Variables.Add(new VariableDefinition(var.Name, ImportType(var.VariableType, mod, mems)));

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
                                psr.Emit(inst.OpCode, newMtd.Parameters[param]);
                            }
                            break;
                        case OperandType.InlineVar:
                        case OperandType.ShortInlineVar:
                            int var = old.Variables.IndexOf(inst.Operand as VariableDefinition);
                            psr.Emit(inst.OpCode, bdy.Variables[var]);
                            break;
                        case OperandType.InlineField:
                            if (mems.ContainsKey((inst.Operand as FieldReference).MetadataToken))
                                psr.Emit(inst.OpCode, mems[(inst.Operand as FieldReference).MetadataToken] as FieldReference);
                            else
                                psr.Emit(inst.OpCode, mod.Import(inst.Operand as FieldReference));
                            break;
                        case OperandType.InlineMethod:
                            psr.Emit(inst.OpCode, ImportMethod(inst.Operand as MethodReference, mod, mems));
                            break;
                        case OperandType.InlineType:
                            psr.Emit(inst.OpCode, ImportType(inst.Operand as TypeReference, mod, mems));
                            break;
                        case OperandType.InlineTok:
                            if (mems.ContainsKey((inst.Operand as TypeReference).MetadataToken))
                            {
                                if (inst.Operand is FieldReference)
                                    psr.Emit(inst.OpCode, mems[(inst.Operand as FieldReference).MetadataToken] as FieldReference);
                                else if (inst.Operand is MethodReference)
                                    psr.Emit(inst.OpCode, ImportMethod(inst.Operand as MethodReference, mod, mems));
                                else if (inst.Operand is TypeReference)
                                    psr.Emit(inst.OpCode, ImportType(inst.Operand as TypeReference, mod, mems));
                            }
                            else
                            {
                                if (inst.Operand is FieldReference)
                                    psr.Emit(inst.OpCode, mod.Import(inst.Operand as FieldReference));
                                else if (inst.Operand is MethodReference)
                                    psr.Emit(inst.OpCode, ImportMethod(inst.Operand as MethodReference, mod, mems));
                                else if (inst.Operand is TypeReference)
                                    psr.Emit(inst.OpCode, ImportType(inst.Operand as TypeReference, mod, mems));
                            }
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
                            neh.CatchType = ImportType(eh.CatchType, mod, mems);
                            break;
                        case ExceptionHandlerType.Filter:
                            neh.FilterStart = bdy.Instructions[old.Instructions.IndexOf(eh.FilterStart)];
                            //neh.FilterEnd = bdy.Instructions[old.Instructions.IndexOf(eh.FilterEnd)];
                            break;
                    }

                    bdy.ExceptionHandlers.Add(neh);
                }

                newMtd.Body = bdy;
            }
        }

        public static MethodDefinition Inject(ModuleDefinition mod, MethodDefinition mtd)
        {
            MethodDefinition ret = new MethodDefinition(mtd.Name, mtd.Attributes, mod.Import(mtd.ReturnType));
            if (mtd.IsPInvokeImpl)
            {
                ret.PInvokeInfo = mtd.PInvokeInfo;
                bool has = false;
                foreach (ModuleReference modRef in mod.ModuleReferences)
                    if (modRef.Name == ret.PInvokeInfo.Module.Name)
                    {
                        has = true;
                        ret.PInvokeInfo.Module = modRef;
                        break;
                    }
                if (!has)
                    mod.ModuleReferences.Add(ret.PInvokeInfo.Module);
            }
            if (mtd.HasCustomAttributes)
            {
                foreach (CustomAttribute attr in mtd.CustomAttributes)
                {
                    CustomAttribute nAttr = new CustomAttribute(mod.Import(attr.Constructor), attr.GetBlob());
                    ret.CustomAttributes.Add(nAttr);
                }
            }

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
                            if (((FieldReference)inst.Operand).DeclaringType != mtd.DeclaringType)
                                psr.Emit(inst.OpCode, mod.Import(inst.Operand as FieldReference));
                            else
                                psr.Emit(inst.OpCode, (FieldReference)inst.Operand);
                            break;
                        case OperandType.InlineMethod:
                            if (inst.Operand == mtd)
                                psr.Emit(inst.OpCode, ret);
                            else if (((MethodReference)inst.Operand).DeclaringType != mtd.DeclaringType)
                                psr.Emit(inst.OpCode, mod.Import(inst.Operand as MethodReference));
                            else
                                psr.Emit(inst.OpCode, (MethodReference)inst.Operand);
                            break;
                        case OperandType.InlineType:
                            psr.Emit(inst.OpCode, mod.Import(inst.Operand as TypeReference));
                            break;
                        case OperandType.InlineTok:
                            if (((MemberReference)inst.Operand).DeclaringType != mtd.DeclaringType &&
                                ((MemberReference)inst.Operand) != mtd.DeclaringType)
                            {
                                if (inst.Operand is TypeReference)
                                    psr.Emit(inst.OpCode, mod.Import(inst.Operand as TypeReference));
                                else if (inst.Operand is FieldReference)
                                    psr.Emit(inst.OpCode, mod.Import(inst.Operand as FieldReference));
                                else if (inst.Operand is MethodReference)
                                    psr.Emit(inst.OpCode, mod.Import(inst.Operand as MethodReference));
                            }
                            else
                            {
                                if (inst.Operand is TypeReference)
                                    psr.Emit(inst.OpCode, inst.Operand as TypeReference);
                                else if (inst.Operand is FieldReference)
                                    psr.Emit(inst.OpCode, inst.Operand as FieldReference);
                                else if (inst.Operand is MethodReference)
                                    psr.Emit(inst.OpCode, inst.Operand as MethodReference);
                            }
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
                            //neh.FilterEnd = bdy.Instructions[old.Instructions.IndexOf(eh.FilterEnd)];
                            break;
                    }

                    bdy.ExceptionHandlers.Add(neh);
                }

                ret.Body = bdy;
            }

            return ret;
        }

        public static string GetNamespace(TypeDefinition typeDef)
        {
            while (typeDef.DeclaringType != null) typeDef = typeDef.DeclaringType;
            return typeDef.Namespace;
        }
        public static string GetName(TypeDefinition typeDef)
        {
            if (typeDef.DeclaringType == null) return typeDef.Name;

            StringBuilder ret = new StringBuilder();
            ret.Append(typeDef.Name);
            while (typeDef.DeclaringType != null)
            {
                typeDef = typeDef.DeclaringType;
                ret.Insert(0, typeDef.Name + "/");
            }
            return ret.ToString();
        }
    }
}
