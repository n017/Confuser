using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace Confuser.Core.Confusions
{
    public class NameConfusion : StructureConfusion
    {
        public override string Name
        {
            get { return "Name Confusion"; }
        }

        public override Priority Priority
        {
            get { return Priority.Safe; }
        }

        public override Phases Phases
        {
            get { return Phases.Phase1 | Phases.Phase3; }            //Sorting of TypeDef cause MetadataTokens changed
        }

        Dictionary<TypeDefinition, Dictionary<string, string>> dict;
        Dictionary<string, string> tDict;
        void SetDictEntry(TypeDefinition type, string sig, string newName)
        {
            if (!dict.ContainsKey(type))
                dict[type] = new Dictionary<string, string>();
            dict[type][sig] = newName;
        }
        string GetDictEntry(TypeDefinition type, string sig)
        {
            if (!dict.ContainsKey(type)) return null;
            string ret;
            dict[type].TryGetValue(sig, out ret);
            return ret;
        }
        string GetSig(MethodReference mtd)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(mtd.ReturnType.FullName);
            sb.Append(" ");
            sb.Append(mtd.Name);
            sb.Append("(");
            if (mtd.HasParameters)
            {
                for (int i = 0; i < mtd.Parameters.Count; i++)
                {
                    ParameterDefinition param = mtd.Parameters[i];
                    if (i > 0)
                    {
                        sb.Append(",");
                    }
                    if (param.ParameterType.IsSentinel)
                    {
                        sb.Append("...,");
                    }
                    sb.Append(param.ParameterType.FullName);
                }
            }
            sb.Append(")");
            return sb.ToString();
        }
        string GetSig(FieldReference fld)
        {
            return fld.FieldType.FullName + " " + fld.Name;
        }

        public override void Confuse(int phase, Confuser cr, AssemblyDefinition asm, IMemberDefinition[] defs)
        {
            switch (phase)
            {
                case 1:
                    dict = new Dictionary<TypeDefinition, Dictionary<string, string>>();
                    tDict = new Dictionary<string, string>();
                    foreach (IMemberDefinition mem in defs)
                    {
                        if (mem is TypeDefinition)
                        {
                            TypeDefinition type = mem as TypeDefinition;
                            string o = type.FullName;
                            if (type.Name == "<Module>")
                                continue;
                            type.Name = GetNewName(type.Name);
                            type.Namespace = "";
                            tDict[o] = type.FullName;
                        }
                        else if (mem is MethodDefinition)
                        {
                            MethodDefinition mtd = mem as MethodDefinition;
                            PerformMethod(cr, asm, mtd);
                        }
                        else if (mem is FieldDefinition)
                        {
                            FieldDefinition fld = mem as FieldDefinition;
                            if (fld.IsRuntimeSpecialName)
                                continue;
                            string sig = GetSig(fld);
                            fld.Name = GetNewName(fld.Name);
                            SetDictEntry(fld.DeclaringType, sig, fld.Name);
                        }
                        else if (mem is PropertyDefinition)
                        {
                            PropertyDefinition prop = mem as PropertyDefinition;
                            if (prop.IsRuntimeSpecialName)
                                continue;
                            prop.Name = GetNewName(prop.Name);
                        }
                        else if (mem is EventDefinition)
                        {
                            EventDefinition evt = mem as EventDefinition;
                            if (evt.IsRuntimeSpecialName)
                                continue;
                            evt.Name = GetNewName(evt.Name);
                        }
                    }
                    break;
                case 3:
                    List<MemberReference> updated = new List<MemberReference>();
                    foreach (TypeDefinition type in asm.MainModule.Types)
                        UpdateType(type, updated);
                    foreach (Resource res in asm.MainModule.Resources)
                        if (res.Name.EndsWith(".resources"))
                            res.Name = tDict.ContainsKey(res.Name.Substring(0, res.Name.LastIndexOf(".resources"))) ? tDict[res.Name.Substring(0, res.Name.LastIndexOf(".resources"))] + ".resources" : res.Name;
                    break;
                default:
                    throw new InvalidOperationException();
            }
        }

        void PerformMethod(Confuser cr, AssemblyDefinition asm, MethodDefinition mtd)
        {
            if (!mtd.IsConstructor)
            {
                string sig;
                if (mtd.DeclaringType.BaseType != null && !(mtd.DeclaringType.BaseType.GetElementType() is TypeDefinition))
                {
                    TypeDefinition bType = mtd.DeclaringType.BaseType.Resolve();
                    MethodDefinition ovr = null;
                    foreach (MethodDefinition bMtd in bType.Methods)
                    {
                        if (bMtd.Name == mtd.Name &&
                            bMtd.ReturnType.FullName == mtd.ReturnType.FullName &&
                            bMtd.Parameters.Count == mtd.Parameters.Count)
                        {
                            bool f = true;
                            for (int i = 0; i < bMtd.Parameters.Count; i++)
                                if (bMtd.Parameters[i].ParameterType.FullName != mtd.Parameters[i].ParameterType.FullName)
                                {
                                    f = false;
                                    break;
                                }
                            if (f)
                            {
                                ovr = bMtd;
                                break;
                            }
                        }
                    }
                    if (ovr == null)
                    {
                        sig = GetSig(mtd);
                        mtd.Name = GetNewName(mtd.Name);
                        SetDictEntry(mtd.DeclaringType, sig, mtd.Name);
                    }
                }
                else
                {
                    sig = GetSig(mtd);
                    mtd.Name = GetNewName(mtd.Name);
                    SetDictEntry(mtd.DeclaringType, sig, mtd.Name);
                }
            }

            foreach (ParameterDefinition para in mtd.Parameters)
            {
                para.Name = GetNewName(para.Name);
            }

            if (mtd.HasBody)
            {
                foreach (VariableDefinition var in mtd.Body.Variables)
                {
                    var.Name = GetNewName(var.Name);
                }
            }
        }
        string GetNewName(string n)
        {
            MD5 md5 = MD5.Create();
            byte[] b = md5.ComputeHash(Encoding.UTF8.GetBytes(n));
            Random rand = new Random(n.GetHashCode());
            StringBuilder ret = new StringBuilder();
            for (int i = 0; i < b.Length; i += 2)
            {
                ret.Append((char)(((b[i] << 8) + b[i + 1]) ^ rand.Next()));
                if (rand.NextDouble() > 0.75)
                    ret.AppendLine();
            }
            return ret.ToString();
        }

        void UpdateType(TypeDefinition type, List<MemberReference> updated)
        {
            foreach (TypeDefinition nested in type.NestedTypes)
                UpdateType(nested, updated);

            foreach (MethodDefinition mtd in type.Methods)
                if (mtd.HasBody)
                    UpdateMethod(mtd, updated);
        }
        void UpdateMethod(MethodDefinition mtd, List<MemberReference> updated)
        {
            foreach (Instruction inst in mtd.Body.Instructions)
            {
                if ((inst.Operand is MethodReference ||
                    inst.Operand is FieldReference) &&
                    !updated.Contains(inst.Operand as MemberReference))
                {
                    TypeDefinition par;
                    MethodSpecification mSpec = inst.Operand as MethodSpecification;
                    if (mSpec != null && (par = mSpec.DeclaringType.GetElementType() as TypeDefinition) != null)
                    {
                        //mSpec.GetElementMethod().Name = GetDictEntry(par, GetSig(inst.Operand as MethodReference));
                        //updated.Add(mSpec);
                    }
                    else
                    {
                        TypeSpecification tSpec = (inst.Operand as MemberReference).DeclaringType as TypeSpecification;
                        if (tSpec != null && (par = tSpec.GetElementType() as TypeDefinition) != null)
                        {
                            if (inst.Operand is MethodReference)
                            {
                                (inst.Operand as MethodReference).Name = GetDictEntry(par, GetSig(inst.Operand as MethodReference)) ?? (inst.Operand as MethodReference).Name;
                            }
                            else
                            {
                                (inst.Operand as FieldReference).Name = GetDictEntry(par, GetSig(inst.Operand as FieldReference)) ?? (inst.Operand as FieldReference).Name;
                            }
                            updated.Add(inst.Operand as MemberReference);
                        }
                    }
                }
            }
        }

        public override bool StandardCompatible
        {
            get { return true; }
        }

        public override string Description
        {
            get { return "This confusion rename the members to unprintable name thus the decompiled source code can neither be compiled nor read."; }
        }

        public override Target Target
        {
            get { return Target.Types | Target.Fields | Target.Methods; }
        }
    }
}
