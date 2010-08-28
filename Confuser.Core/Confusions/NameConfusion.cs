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
    public class NameConfusion : IConfusion
    {
        class Phase1 : StructurePhase
        {
            public Phase1(NameConfusion nc) { this.nc = nc; }
            NameConfusion nc;
            public override IConfusion Confusion
            {
                get { return nc; }
            }

            public override int PhaseID
            {
                get { return 1; }
            }

            public override Priority Priority
            {
                get { return Priority.Safe; }
            }

            public override bool WholeRun
            {
                get { return false; }
            }

            public override void Initialize(AssemblyDefinition asm)
            {
                nc.dict = new Dictionary<TypeDefinition, Dictionary<string, string>>();
                nc.tDict = new Dictionary<string, string>();
            }

            public override void DeInitialize()
            {
                //
            }

            public override void Process(ConfusionParameter parameter)
            {
                IMemberDefinition mem = parameter.Target as IMemberDefinition;
                if (mem is TypeDefinition)
                {
                    TypeDefinition type = mem as TypeDefinition;
                    string o = type.FullName;
                    if (type.Name == "<Module>")
                        return;
                    type.Name = ObfuscationHelper.GetNewName(type.Name);
                    type.Namespace = "";
                    nc.tDict[o] = type.FullName;
                }
                else if (mem is MethodDefinition)
                {
                    MethodDefinition mtd = mem as MethodDefinition;
                    PerformMethod(mtd);
                }
                else if (mem is FieldDefinition)
                {
                    FieldDefinition fld = mem as FieldDefinition;
                    if (fld.IsRuntimeSpecialName)
                        return;
                    string sig = nc.GetSig(fld);
                    fld.Name = ObfuscationHelper.GetNewName(fld.Name);
                    nc.SetDictEntry(fld.DeclaringType, sig, fld.Name);
                }
                else if (mem is PropertyDefinition)
                {
                    PropertyDefinition prop = mem as PropertyDefinition;
                    if (prop.IsRuntimeSpecialName)
                        return;
                    prop.Name = ObfuscationHelper.GetNewName(prop.Name);
                }
                else if (mem is EventDefinition)
                {
                    EventDefinition evt = mem as EventDefinition;
                    if (evt.IsRuntimeSpecialName)
                        return;
                    evt.Name = ObfuscationHelper.GetNewName(evt.Name);
                }
            }
            void PerformMethod(MethodDefinition mtd)
            {
                if (!mtd.IsConstructor)
                {
                    string sig;
                    if (mtd.DeclaringType.BaseType != null && !(mtd.DeclaringType.BaseType.GetElementType() is TypeDefinition))
                    {
                        TypeDefinition bType = mtd.DeclaringType.BaseType.Resolve();
                        if (bType.FullName == "System.Delegate" ||
                            bType.FullName == "System.MulticastDelegate")
                        {
                            //NOT TO RENAME
                        }
                        else if (bType.IsInterface)
                        {
                            TypeDefinition now = bType;
                            do
                            {
                                MethodDefinition imple = null;
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
                                            imple = bMtd;
                                            break;
                                        }
                                    }
                                }
                                if (imple != null)
                                {
                                    mtd.Overrides.Add(imple);
                                }
                                now = now.BaseType.Resolve();
                            } while (now != null);

                            sig = nc.GetSig(mtd);
                            mtd.Name = ObfuscationHelper.GetNewName(mtd.Name);
                            nc.SetDictEntry(mtd.DeclaringType, sig, mtd.Name);
                        }
                        else
                        {
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
                                sig = nc.GetSig(mtd);
                                mtd.Name = ObfuscationHelper.GetNewName(mtd.Name);
                                nc.SetDictEntry(mtd.DeclaringType, sig, mtd.Name);
                            }
                        }
                    }
                    else
                    {
                        sig = nc.GetSig(mtd);
                        mtd.Name = ObfuscationHelper.GetNewName(mtd.Name);
                        nc.SetDictEntry(mtd.DeclaringType, sig, mtd.Name);
                    }
                }

                foreach (ParameterDefinition para in mtd.Parameters)
                {
                    para.Name = ObfuscationHelper.GetNewName(para.Name);
                }

                if (mtd.HasBody)
                {
                    foreach (VariableDefinition var in mtd.Body.Variables)
                    {
                        var.Name = ObfuscationHelper.GetNewName(var.Name);
                    }
                }
            }
        }

        class Phase3 : StructurePhase
        {
            public Phase3(NameConfusion nc) { this.nc = nc; }
            NameConfusion nc;
            public override IConfusion Confusion
            {
                get { return nc; }
            }

            public override int PhaseID
            {
                get { return 3; }
            }

            public override Priority Priority
            {
                get { return Priority.Safe; }
            }

            public override bool WholeRun
            {
                get { return true; }
            }

            public override void Initialize(AssemblyDefinition asm)
            {
                this.asm = asm;
            }

            public override void DeInitialize()
            {
                //
            }

            AssemblyDefinition asm;
            public override void Process(ConfusionParameter parameter)
            {
                List<MemberReference> updated = new List<MemberReference>();
                foreach (ModuleDefinition mod in asm.Modules)
                {
                    foreach (TypeDefinition type in mod.Types)
                        UpdateType(type, updated);
                    foreach (Resource res in mod.Resources)
                        if (res.Name.EndsWith(".resources"))
                            res.Name = nc.tDict.ContainsKey(res.Name.Substring(0, res.Name.LastIndexOf(".resources"))) ? nc.tDict[res.Name.Substring(0, res.Name.LastIndexOf(".resources"))] + ".resources" : res.Name;
                }
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
                        }
                        else
                        {
                            TypeSpecification tSpec = (inst.Operand as MemberReference).DeclaringType as TypeSpecification;
                            if (tSpec != null && (par = tSpec.GetElementType() as TypeDefinition) != null)
                            {
                                if (inst.Operand is MethodReference)
                                {
                                    (inst.Operand as MethodReference).Name = nc.GetDictEntry(par, nc.GetSig(inst.Operand as MethodReference)) ?? (inst.Operand as MethodReference).Name;
                                }
                                else
                                {
                                    (inst.Operand as FieldReference).Name = nc.GetDictEntry(par, nc.GetSig(inst.Operand as FieldReference)) ?? (inst.Operand as FieldReference).Name;
                                }
                                updated.Add(inst.Operand as MemberReference);
                            }
                        }
                    }
                }
            }
        }


        public string ID
        {
            get { return "rename"; }
        }
        public string Name
        {
            get { return "Name Confusion"; }
        }
        public string Description
        {
            get { return "This confusion rename the members to unprintable name thus the decompiled source code can neither be compiled nor read."; }
        }
        public Target Target
        {
            get { return Target.Types | Target.Fields | Target.Methods; }
        }
        public Preset Preset
        {
            get { return Preset.Minimum; }
        }
        public bool StandardCompatible
        {
            get { return true; }
        }

        Phase[] ps;
        public Phase[] Phases
        {
            get
            {
                if (ps == null)
                    ps = new Phase[] { new Phase1(this), new Phase3(this) };
                return ps;
            }
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


    }
}
