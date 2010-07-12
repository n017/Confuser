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
            get { return Phases.Phase1; }            //Sorting of TypeDef cause MetadataTokens changed
        }

        public override void Confuse(int phase, Confuser cr, AssemblyDefinition asm, IMemberDefinition[] defs)
        {
            if (phase != 1) throw new InvalidOperationException();
            foreach (IMemberDefinition mem in defs)
            {
                if (mem is TypeDefinition)
                {
                    TypeDefinition type = mem as TypeDefinition;
                    if (!(type.IsRuntimeSpecialName || type.IsSpecialName || type.IsPublic || type.IsNestedPublic || type.IsNestedFamilyOrAssembly))
                    {
                        type.Name = GetNewName(type.Name);
                        type.Namespace = "";
                    }

                    foreach (MethodDefinition mtd in type.Methods)
                    {
                        if (mtd.IsConstructor || mtd.IsPublic || mtd.IsFamilyOrAssembly || mtd.IsSpecialName || mtd.IsFamily)
                            continue;
                        PerformMethod(cr, mtd);
                    }
                    foreach (FieldDefinition fld in type.Fields)
                    {
                        if (fld.IsPublic || fld.IsFamilyOrAssembly || fld.IsSpecialName || fld.IsRuntimeSpecialName || fld.IsPublic || fld.IsFamilyOrAssembly || fld.IsFamily)
                            continue;
                        fld.Name = GetNewName(fld.Name);
                    }
                    foreach (PropertyDefinition pty in type.Properties)
                    {
                        if (pty.IsSpecialName || pty.IsRuntimeSpecialName)
                            continue;
                        pty.Name = GetNewName(pty.Name);
                    }
                    foreach (EventDefinition evt in type.Events)
                    {
                        if (evt.IsSpecialName || evt.IsRuntimeSpecialName)
                            continue;
                        evt.Name = GetNewName(evt.Name);
                    }
                }
                else if (mem is MethodDefinition)
                {
                    MethodDefinition mtd = mem as MethodDefinition;
                    if (mtd.IsConstructor || mtd.IsPublic || mtd.IsFamilyOrAssembly || mtd.IsSpecialName || mtd.IsFamily)
                        continue;
                    PerformMethod(cr, mtd);
                }
                else if (mem is FieldDefinition)
                {
                    FieldDefinition fld = mem as FieldDefinition;
                    if (fld.IsPublic || fld.IsFamilyOrAssembly || fld.IsSpecialName || fld.IsRuntimeSpecialName || fld.IsPublic || fld.IsFamilyOrAssembly || fld.IsFamily)
                        continue;
                    fld.Name = GetNewName(fld.Name);
                }
                else if (mem is PropertyDefinition)
                {
                    PropertyDefinition prop = mem as PropertyDefinition;
                    if (prop.IsSpecialName || prop.IsRuntimeSpecialName)
                        continue;
                    prop.Name = GetNewName(prop.Name);
                }
                else if (mem is EventDefinition)
                {
                    EventDefinition evt = mem as EventDefinition;
                    if (evt.IsSpecialName || evt.IsRuntimeSpecialName)
                        continue;
                    evt.Name = GetNewName(evt.Name);
                }
            }
        }

        void PerformMethod(Confuser cr, MethodDefinition mtd)
        {
            mtd.Name = GetNewName(mtd.Name);

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
            get { return Target.All; }
        }
    }
}
