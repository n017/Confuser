using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Rocks;

namespace Confuser.Core.Analyzers
{
    partial class NameAnalyzer : Analyzer
    {
        Dictionary<TypeDefinition, VTable> vTbls = new Dictionary<TypeDefinition, VTable>();

        int modCount;
        int progressMod;
        public override void Analyze(IEnumerable<AssemblyDefinition> asms)
        {
            Logger._Log("> Initializing Name Analyzer...");
            modCount = asms.Sum(_ => _.Modules.Count);
            progressMod = 0;
            foreach (AssemblyDefinition asm in asms)
                foreach (ModuleDefinition mod in asm.Modules)
                {
                    Init(mod);
                    progressMod++;
                }

            Logger._Log("> Analyzing IVT attributes...");
            AnalyzeIvtMap(asms);
            foreach (AssemblyDefinition asm in asms)
            {
                try
                {
                    AnalyzeIvt(asm);
                }
                catch { }
                foreach (ModuleDefinition mod in asm.Modules)
                {
                    Logger._Log("> Analyzing " + mod.Name + "...");
                    Logger._Progress(0, 1);
                    Analyze(mod);
                }
            }

            Logger._Log("> Analyzing virtual inheritance...");
            progressMod = 0;
            foreach (AssemblyDefinition asm in asms)
                foreach (ModuleDefinition mod in asm.Modules)
                {
                    int p = 1;
                    foreach (TypeDefinition type in mod.Types)
                    {
                        ConstructVTable(type);
                        Logger._Progress(mod.Types.Count * progressMod + p, mod.Types.Count * (modCount + 1));
                        p++;
                    }
                    progressMod++;
                }
        }
        void Init(ModuleDefinition mod)
        {
            (mod as IAnnotationProvider).Annotations["RenMode"] = NameMode.Unreadable;
            int p = 1;
            foreach (TypeDefinition type in mod.Types)
            {
                Init(type);
                Logger._Progress(mod.Types.Count * progressMod + p, mod.Types.Count * (modCount + 1));
                p++;
            }
            foreach (Resource res in mod.Resources)
            {
                (res as IAnnotationProvider).Annotations["RenId"] = new Identifier() { scope = res.Name, hash = res.GetHashCode() };
                (res as IAnnotationProvider).Annotations["RenRef"] = new List<IReference>();
            }
        }
        void Init(TypeDefinition type)
        {
            foreach (TypeDefinition nType in type.NestedTypes)
                Init(nType);
            (type as IAnnotationProvider).Annotations["RenId"] = new Identifier() { scope = CecilHelper.GetNamespace(type), name = CecilHelper.GetName(type) };
            (type as IAnnotationProvider).Annotations["RenRef"] = new List<IReference>();
            (type as IAnnotationProvider).Annotations["RenOk"] = true;
            foreach (MethodDefinition mtd in type.Methods)
            {
                (mtd as IAnnotationProvider).Annotations["RenId"] = new Identifier() { scope = type.FullName, name = mtd.Name, hash = mtd.GetHashCode() };
                (mtd as IAnnotationProvider).Annotations["RenRef"] = new List<IReference>();
                (mtd as IAnnotationProvider).Annotations["RenOk"] = true;
            }
            foreach (FieldDefinition fld in type.Fields)
            {
                (fld as IAnnotationProvider).Annotations["RenId"] = new Identifier() { scope = type.FullName, name = fld.Name, hash = fld.GetHashCode() };
                (fld as IAnnotationProvider).Annotations["RenRef"] = new List<IReference>();
                (fld as IAnnotationProvider).Annotations["RenOk"] = true;
            }
            foreach (PropertyDefinition prop in type.Properties)
            {
                (prop as IAnnotationProvider).Annotations["RenId"] = new Identifier() { scope = type.FullName, name = prop.Name, hash = prop.GetHashCode() };
                (prop as IAnnotationProvider).Annotations["RenRef"] = new List<IReference>();
                (prop as IAnnotationProvider).Annotations["RenOk"] = true;
            }
            foreach (EventDefinition evt in type.Events)
            {
                (evt as IAnnotationProvider).Annotations["RenId"] = new Identifier() { scope = type.FullName, name = evt.Name, hash = evt.GetHashCode() };
                (evt as IAnnotationProvider).Annotations["RenRef"] = new List<IReference>();
                (evt as IAnnotationProvider).Annotations["RenOk"] = true;
            }
        }

        static bool IsTypePublic(TypeDefinition type)
        {
            if (type.Module.Kind == ModuleKind.Windows || type.Module.Kind == ModuleKind.Console)
                return false;
            do
            {
                if (!type.IsPublic && !type.IsNestedFamily && !type.IsNestedFamilyAndAssembly && !type.IsNestedFamilyOrAssembly && !type.IsNestedPublic && !type.IsPublic)
                    return false;
                type = type.DeclaringType;
            } while (type != null);
            return true;
        }

        void Analyze(ModuleDefinition mod)
        {
            AnalyzeCustomAttributes(mod);
            for (int i = 0; i < mod.Resources.Count; i++)
                if (mod.Resources[i].Name.EndsWith(".g.resources") && mod.Resources[i] is EmbeddedResource)
                    AnalyzeResource(mod, i);
            int p = 1;
            foreach (TypeDefinition type in mod.Types)
            {
                Analyze(type);
                Logger._Progress(p, mod.Types.Count);
                p++;
            }
        }
        void Analyze(TypeDefinition type)
        {
            if (type.Name == "<Module>" || IsTypePublic(type))
                (type as IAnnotationProvider).Annotations["RenOk"] = false;
            foreach (Resource res in (type.Scope as ModuleDefinition).Resources)
                if (res.Name == type.FullName + ".resources")
                    ((type as IAnnotationProvider).Annotations["RenRef"] as List<IReference>).Add(new ResourceReference(res));

            AnalyzeCustomAttributes(type);
            if (type.HasGenericParameters)
                foreach (var i in type.GenericParameters)
                    AnalyzeCustomAttributes(i);
            foreach (TypeDefinition nType in type.NestedTypes)
                Analyze(nType);
            foreach (MethodDefinition mtd in type.Methods)
                Analyze(mtd);
            foreach (FieldDefinition fld in type.Fields)
                Analyze(fld);
            foreach (PropertyDefinition prop in type.Properties)
                Analyze(prop);
            foreach (EventDefinition evt in type.Events)
                Analyze(evt);
        }
        void Analyze(MethodDefinition mtd)
        {
            if (mtd.IsConstructor || (IsTypePublic(mtd.DeclaringType) &&
                (mtd.IsFamily || mtd.IsAssembly || mtd.IsFamilyAndAssembly || mtd.IsFamilyOrAssembly || mtd.IsPublic)))
                (mtd as IAnnotationProvider).Annotations["RenOk"] = false;
            else if (mtd.DeclaringType.BaseType != null && mtd.DeclaringType.BaseType.Resolve() != null)
            {
                TypeReference bType = mtd.DeclaringType.BaseType;
                if (bType.FullName == "System.Delegate" ||
                    bType.FullName == "System.MulticastDelegate")
                {
                    (mtd as IAnnotationProvider).Annotations["RenOk"] = false;
                }
            }

            AnalyzeCustomAttributes(mtd);
            if (mtd.HasParameters)
                foreach (var i in mtd.Parameters)
                    AnalyzeCustomAttributes(i);
            AnalyzeCustomAttributes(mtd.MethodReturnType);
            if (mtd.HasGenericParameters)
                foreach (var i in mtd.GenericParameters)
                    AnalyzeCustomAttributes(i);
            if (mtd.HasBody)
            {
                mtd.Body.SimplifyMacros();
                AnalyzeCodes(mtd);
                mtd.Body.OptimizeMacros();
            }
        }
        void Analyze(FieldDefinition fld)
        {
            AnalyzeCustomAttributes(fld);
            if (fld.IsRuntimeSpecialName || fld.DeclaringType.IsEnum || (IsTypePublic(fld.DeclaringType) &&
                (fld.IsFamily || fld.IsFamilyAndAssembly || fld.IsFamilyOrAssembly || fld.IsPublic)))
                (fld as IAnnotationProvider).Annotations["RenOk"] = false;
        }
        void Analyze(PropertyDefinition prop)
        {
            AnalyzeCustomAttributes(prop);
            if (prop.IsRuntimeSpecialName || IsTypePublic(prop.DeclaringType))
                (prop as IAnnotationProvider).Annotations["RenOk"] = false;
        }
        void Analyze(EventDefinition evt)
        {
            AnalyzeCustomAttributes(evt);
            if (evt.IsRuntimeSpecialName || IsTypePublic(evt.DeclaringType))
                (evt as IAnnotationProvider).Annotations["RenOk"] = false;
        }

        void AnalyzeCustomAttributes(ICustomAttributeProvider ca)
        {
            if (!ca.HasCustomAttributes) return;
            foreach (var i in ca.CustomAttributes)
            {
                foreach (var arg in i.ConstructorArguments)
                    AnalyzeCustomAttributeArgs(arg);
                foreach (var arg in i.Fields)
                    AnalyzeCustomAttributeArgs(arg.Argument);
                foreach (var arg in i.Properties)
                    AnalyzeCustomAttributeArgs(arg.Argument);

                if (Database.ExcludeAttributes.Contains(i.AttributeType.FullName) && ca is IAnnotationProvider)
                    (ca as IAnnotationProvider).Annotations["RenOk"] = false;
            }
        }
        void AnalyzeCustomAttributeArgs(CustomAttributeArgument arg)
        {
            if (arg.Value is TypeReference)
            {
                TypeReference typeRef = arg.Value as TypeReference;
                bool has = false;
                foreach (var i in ivtMap)
                    if (i.Key.Name.Name == typeRef.Scope.Name)
                    {
                        has = true;
                        break;
                    }
                if (has)
                    (((arg.Value as TypeReference).Resolve() as IAnnotationProvider).Annotations["RenRef"] as List<IReference>).Add(new CustomAttributeReference(arg.Value as TypeReference));
            }
            else if (arg.Value is CustomAttributeArgument[])
                foreach (var i in arg.Value as CustomAttributeArgument[])
                    AnalyzeCustomAttributeArgs(i);
        }
    }
}
