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

        public override ProcessType Process
        {
            get { return ProcessType.Pre; }            //Sorting of TypeDef cause MetadataTokens changed
        }

        public override void PreConfuse(Confuser cr, AssemblyDefinition asm)
        {
            foreach (TypeDefinition t in asm.MainModule.GetAllTypes())
            {
                if (t.Name == "<Module>")
                    continue;
                PerformType(cr, t);
            }
        }

        public override void DoConfuse(Confuser cr, AssemblyDefinition asm)
        {
            throw new InvalidOperationException();
        }

        public override void PostConfuse(Confuser cr, AssemblyDefinition asm)
        {
            throw new InvalidOperationException();
        }

        void PerformType(Confuser cr, TypeDefinition type)
        {
            if (!(type.IsRuntimeSpecialName || type.IsSpecialName || type.IsPublic || type.IsNestedPublic || type.IsNestedFamilyOrAssembly))
            {
                type.Name = GetNewName(type.Name);
                type.Namespace = "";
            }

            cr.AddLv();
            {
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
                    cr.Log("<field name='" + fld.Name + "'/>");
                    fld.Name = GetNewName(fld.Name);
                }
                foreach (PropertyDefinition pty in type.Properties)
                {
                    if (pty.IsSpecialName || pty.IsRuntimeSpecialName)
                        continue;
                    cr.Log("<property name='" + pty.Name + "'/>");
                    pty.Name = GetNewName(pty.Name);
                }
                foreach (EventDefinition evt in type.Events)
                {
                    if (evt.IsSpecialName || evt.IsRuntimeSpecialName)
                        continue;
                    cr.Log("<event name='" + evt.Name + "'/>");
                    evt.Name = GetNewName(evt.Name);
                }
            }
            cr.SubLv();
        }
        void PerformMethod(Confuser cr, MethodDefinition mtd)
        {
            cr.Log("<method name='" + mtd.Name + "'/>");
            mtd.Name = GetNewName(mtd.Name);
            cr.AddLv();

            cr.Log("<params>");
            cr.AddLv();
            foreach (ParameterDefinition para in mtd.Parameters)
            {
                cr.Log("<param name='" + para.Name + "' />");
                para.Name = GetNewName(para.Name);
            }
            cr.SubLv();
            cr.Log("</params>");

            if (mtd.HasBody)
            {
                cr.Log("<vars>");
                cr.AddLv();
                foreach (VariableDefinition var in mtd.Body.Variables)
                {
                    cr.Log("<var name='" + var.Name + "' />");
                    var.Name = GetNewName(var.Name);
                }
                cr.SubLv();
                cr.Log("</vars>");
            }

            cr.SubLv();
            cr.Log("</method>");
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
    }
}
