using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Resources;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Mono.Collections.Generic;

namespace Confuser.Core.Engines
{
    struct ReflectionMethod
    {
        public string typeName;
        public string mtdName;
        public int[] paramLoc;
        public string[] paramType;
    }

    static class Database
    {
        static Database()
        {
            Reflections = new Dictionary<string, ReflectionMethod>();
            string type = null;
            using (StringReader rdr = new StringReader(db))
            {
                while (true)
                {
                    string line = rdr.ReadLine();
                    if (line == "=") break;
                    if (type != null)
                    {
                        if (line == "")
                        {
                            type = null; continue;
                        }
                        ReflectionMethod mtd = new ReflectionMethod();
                        mtd.typeName = type;
                        mtd.mtdName = line.Substring(0, line.IndexOf('['));
                        string param = line.Substring(line.IndexOf('[') + 1, line.IndexOf(']') - line.IndexOf('[') - 1);
                        string[] pars = param.Split(',');
                        mtd.paramLoc = new int[pars.Length];
                        mtd.paramType = new string[pars.Length];
                        for (int i = 0; i < pars.Length; i++)
                        {
                            mtd.paramLoc[i] = int.Parse(pars[i].Split(':')[0]);
                            mtd.paramType[i] = pars[i].Split(':')[1];
                        }
                        Reflections.Add(mtd.typeName + "::" + mtd.mtdName, mtd);
                    }
                    else
                    {
                        type = line;
                    }
                }
            }
        }

        public static readonly Dictionary<string, ReflectionMethod> Reflections;
        const string db =
@"Microsoft.VisualBasic.CompilerServices.LateBinding
LateCall[0:This,1:Type,2:Target]
LateGet[0:This,1:Type,2:Target]
LateSet[0:This,1:Type,2:Target]
LateSetComplex[0:This,1:Type,2:Target]

Microsoft.VisualBasic.CompilerServices.NewLateBinding
LateCall[0:This,1:Type,2:Target]
LateCanEvaluate[0:This,1:Type,2:Target]
LateGet[0:This,1:Type,2:Target]
LateSet[0:This,1:Type,2:Target]
LateSetComplex[0:This,1:Type,2:Target]

System.Type
GetEvent[0:Type,1:Target]
GetField[0:Type,1:Target]
GetMember[0:Type,1:Target]
GetMethod[0:Type,1:Target]
GetNestedType[0:Type,1:Target]
GetProperty[0:Type,1:Target]
GetType[0:TargetType]
InvokeMember[0:Type,1:Target]
ReflectionOnlyGetType[0:TargetType]

System.Reflection.Assembly
GetType[1:TargetType]

System.Reflection.Module
GetType[1:TargetType]

System.Activator
CreateInstance[1:TargetType]
CreateInstanceFrom[1:TargetType]

System.AppDomain
CreateInstance[2:TargetType]
CreateInstanceFrom[2:TargetType]

System.Resources.ResourceManager
.ctor[0:TargetResource]
=";
    }

    struct Identifier
    {
        public string scope;
        public string name;
        public int hash;
    }
    interface IReference
    {
        void UpdateReference(Identifier old, Identifier @new);
    }
    class RenameEngine : IEngine
    {
        class ResourceReference : IReference
        {
            public ResourceReference(Resource res) { this.res = res; }
            Resource res;
            public void UpdateReference(Identifier old, Identifier @new)
            {
                string oldN = string.IsNullOrEmpty(old.scope) ? old.name : old.scope + "." + old.name;
                string newN = string.IsNullOrEmpty(@new.scope) ? @new.name : @new.scope + "." + @new.name;
                res.Name = res.Name.Replace(oldN, newN);
            }
        }
        class ResourceNameReference : IReference
        {
            public ResourceNameReference(Instruction inst) { this.inst = inst; }
            Instruction inst;
            public void UpdateReference(Identifier old, Identifier @new)
            {
                inst.Operand = @new.scope;
            }
        }
        class SpecificationReference : IReference
        {
            public SpecificationReference(MemberReference refer) { this.refer = refer; }
            MemberReference refer;
            public void UpdateReference(Identifier old, Identifier @new)
            {
                MethodSpecification mSpec = refer as MethodSpecification;
                if (mSpec == null || !(mSpec.DeclaringType.GetElementType() is TypeDefinition))
                {
                    TypeSpecification tSpec = refer.DeclaringType as TypeSpecification;
                    TypeDefinition par = tSpec.GetElementType() as TypeDefinition;
                    if (tSpec != null && par != null)
                    {
                        refer.Name = @new.name;
                    }
                }
            }
        }
        class ReflectionReference : IReference
        {
            public ReflectionReference(Instruction ldstr) { this.ldstr = ldstr; }
            Instruction ldstr;
            public void UpdateReference(Identifier old, Identifier @new)
            {
                string op = (string)ldstr.Operand;
                if (op == old.name)
                    ldstr.Operand = @new.name;
                else if (op == old.scope)
                    ldstr.Operand = @new.scope;
            }
        }
        class BamlTypeReference : IReference
        {
            public BamlTypeReference(BamlDocument doc, TypeDeclaration type) { this.doc = doc; this.type = type; }

            BamlDocument doc;
            TypeDeclaration type;

            public void UpdateReference(Identifier old, Identifier @new)
            {
                type.Name = @new.name;
                type.Namespace = @new.scope;
            }
        }
        class BamlMemberDeclReference : IReference
        {
            public BamlMemberDeclReference(BamlDocument doc, PropertyDeclaration prop) { this.doc = doc; this.prop = prop; }

            BamlDocument doc;
            PropertyDeclaration prop;

            public void UpdateReference(Identifier old, Identifier @new)
            {
                prop.Name = @new.name;
            }
        }
        class BamlMemberReference : IReference
        {
            public BamlMemberReference(BamlDocument doc, Property prop) { this.doc = doc; this.prop = prop; }

            BamlDocument doc;
            Property prop;

            public void UpdateReference(Identifier old, Identifier @new)
            {
                prop.Value = @new.name;
            }
        }
        class SaveBamlsReference : IReference
        {
            public SaveBamlsReference(ModuleDefinition mod, int resId) { this.mod = mod; this.resId = resId; }

            ModuleDefinition mod;
            int resId;

            public void UpdateReference(Identifier old, Identifier @new)
            {
                EmbeddedResource res = mod.Resources[resId] as EmbeddedResource;
                foreach (KeyValuePair<string, BamlDocument> pair in (res as IAnnotationProvider).Annotations["Gbamls"] as Dictionary<string, BamlDocument>)
                {
                    Stream src = ((res as IAnnotationProvider).Annotations["Gresources"] as Dictionary<string, object>)[pair.Key] as Stream;
                    src.Position = 0;
                    Stream dst = new MemoryStream();
                    new BamlCopyer(src, dst, pair.Value);
                    ((res as IAnnotationProvider).Annotations["Gresources"] as Dictionary<string, object>)[pair.Key] = dst;
                }
                MemoryStream newRes = new MemoryStream();
                ResourceWriter wtr = new ResourceWriter(newRes);
                foreach (KeyValuePair<string, object> pair in (res as IAnnotationProvider).Annotations["Gresources"] as Dictionary<string, object>)
                    wtr.AddResource(pair.Key, pair.Value);
                wtr.Generate();
                mod.Resources[resId] = new EmbeddedResource(res.Name, res.Attributes, newRes.ToArray());
            }
        }
        class IvtMemberReference : IReference
        {
            public IvtMemberReference(MemberReference memRef) { this.memRef = memRef; }

            MemberReference memRef;

            public void UpdateReference(Identifier old, Identifier @new)
            {
                if (memRef is MethodReference || memRef is FieldReference)
                {
                    memRef.Name = @new.name;
                }
                else if (memRef is TypeReference)
                {
                    memRef.Name = @new.name;
                    ((TypeReference)memRef).Namespace = @new.scope;
                }
            }
        }

        string NormalizeIva(string asmname)
        {
            string[] names = asmname.Split(',');
            if (names.Length == 1) return asmname;
            else return names[0].Trim() + "," + names[1].Trim().Substring(10);
        }
        string GetIva(AssemblyNameDefinition name)
        {
            if (name.PublicKey != null) return name.Name + "," + BitConverter.ToString(name.PublicKey).Replace("-", "").ToLower();
            else return name.Name;
        }
        Dictionary<AssemblyDefinition, List<string>> ivtMap;
        Dictionary<MetadataToken, MemberReference> ivtRefs = new Dictionary<MetadataToken, MemberReference>();
        public void Analysis(Logger logger, IEnumerable<AssemblyDefinition> asms)
        {
            foreach (AssemblyDefinition asm in asms)
                foreach (ModuleDefinition mod in asm.Modules)
                    Init(mod);

            AnalysisIvtMap(logger, asms);
            foreach (AssemblyDefinition asm in asms)
            {
                try
                {
                    AnalysisIvt(asm);
                }
                catch { }
                foreach (ModuleDefinition mod in asm.Modules)
                    Analysis(mod);
            }
        }
        void Init(ModuleDefinition mod)
        {
            foreach (TypeDefinition type in mod.Types)
                Init(type);
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
            (type as IAnnotationProvider).Annotations["RenId"] = new Identifier() { scope = type.Namespace, name = type.Name };
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

        void AnalysisIvtMap(Logger logger, IEnumerable<AssemblyDefinition> asms)
        {
            ivtMap = new Dictionary<AssemblyDefinition, List<string>>();
            foreach (AssemblyDefinition asm in asms)
            {
                if (!ivtMap.ContainsKey(asm)) ivtMap.Add(asm, new List<string>());
                List<string> internalVis = new List<string>();
                foreach (CustomAttribute attr in asm.CustomAttributes)
                    if (attr.AttributeType.FullName == "System.Runtime.CompilerServices.InternalsVisibleToAttribute")
                        internalVis.Add(NormalizeIva((string)attr.ConstructorArguments[0].Value));
                if (internalVis.Count != 0)
                {
                    logger.Log("InternalsVisibleToAttribute found in " + asm.FullName + "!");

                    List<AssemblyDefinition> refAsms = new List<AssemblyDefinition>();
                    foreach (AssemblyDefinition asmm in asms)
                        if (internalVis.Contains(GetIva(asmm.Name)))
                            refAsms.Add(asmm);

                    if (refAsms.Count == 0)
                        logger.Log("Internal assemblies NOT found!");
                    else
                        logger.Log("Internal assemblies found!");
                    foreach (AssemblyDefinition i in refAsms)
                    {
                        if (!ivtMap.ContainsKey(i)) ivtMap.Add(i, new List<string>());
                        ivtMap[i].Add(asm.FullName);
                    }
                }
            }
        }
        void AnalysisIvt(AssemblyDefinition asm)
        {
            List<string> ivts = ivtMap[asm];
            ivtRefs.Clear();
            foreach (ModuleDefinition mod in asm.Modules)
            {
                foreach (TypeReference typeRef in mod.GetTypeReferences())
                {
                    TypeDefinition typeDef = typeRef.Resolve();
                    if (typeDef != null && ivts.Contains(typeDef.Module.Assembly.FullName))
                    {
                        ivtRefs.Add(typeRef.MetadataToken, typeRef);
                        ((typeDef as IAnnotationProvider).Annotations["RenRef"] as List<IReference>).Add(new IvtMemberReference(typeRef));
                    }
                }
                foreach (MemberReference memRef in mod.GetMemberReferences())
                {
                    IMemberDefinition memDef;
                    if (memRef is MethodReference && (memDef = ((MethodReference)memRef).Resolve()) != null && ivts.Contains(((MethodDefinition)memDef).Module.Assembly.FullName))
                    {
                        ivtRefs.Add(memRef.MetadataToken, memRef);
                        if (mod.LookupToken(memRef.MetadataToken) != memRef)
                            ((memDef as IAnnotationProvider).Annotations["RenRef"] as List<IReference>).Add(new IvtMemberReference(memRef));
                    }
                    if (memRef is FieldReference && (memDef = ((FieldReference)memRef).Resolve()) != null && ivts.Contains(((FieldDefinition)memDef).Module.Assembly.FullName))
                    {
                        ivtRefs.Add(memRef.MetadataToken, memRef);
                        if (mod.LookupToken(memRef.MetadataToken) != memRef)
                            ((memDef as IAnnotationProvider).Annotations["RenRef"] as List<IReference>).Add(new IvtMemberReference(memRef));
                    }
                }
            }
        }
        void Analysis(ModuleDefinition mod)
        {
            for (int i = 0; i < mod.Resources.Count; i++)
                if (mod.Resources[i].Name.EndsWith(".g.resources") && mod.Resources[i] is EmbeddedResource)
                    AnalysisResource(mod, i);
            foreach (TypeDefinition type in mod.Types)
                Analysis(type);
        }
        void Analysis(TypeDefinition type)
        {
            if (type.Name == "<Module>" || IsTypePublic(type))
                (type as IAnnotationProvider).Annotations["RenOk"] = false;
            foreach (Resource res in (type.Scope as ModuleDefinition).Resources)
                if (res.Name == type.FullName + ".resources")
                    ((type as IAnnotationProvider).Annotations["RenRef"] as List<IReference>).Add(new ResourceReference(res));

            foreach (TypeDefinition nType in type.NestedTypes)
                Analysis(nType);
            foreach (MethodDefinition mtd in type.Methods)
                Analysis(mtd);
            foreach (FieldDefinition fld in type.Fields)
                Analysis(fld);
            foreach (PropertyDefinition prop in type.Properties)
                Analysis(prop);
            foreach (EventDefinition evt in type.Events)
                Analysis(evt);

        }
        void Analysis(MethodDefinition mtd)
        {
            if (mtd.DeclaringType.IsInterface || mtd.IsConstructor || (IsTypePublic(mtd.DeclaringType) &&
                (mtd.IsFamily || mtd.IsAssembly || mtd.IsFamilyAndAssembly || mtd.IsFamilyOrAssembly || mtd.IsPublic)))
                (mtd as IAnnotationProvider).Annotations["RenOk"] = false;
            else if (mtd.DeclaringType.BaseType != null && mtd.DeclaringType.BaseType.Resolve() != null)
            {
                TypeDefinition bType = mtd.DeclaringType.BaseType.Resolve();
                if (bType.FullName == "System.Delegate" ||
                    bType.FullName == "System.MulticastDelegate")
                {
                    (mtd as IAnnotationProvider).Annotations["RenOk"] = false;
                }
                else
                {
                    TypeDefinition now = bType;
                    MethodDefinition ovr = null;
                    do
                    {
                        foreach (MethodDefinition bMtd in now.Methods)
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
                        if (now.BaseType != null)
                            now = now.BaseType.Resolve();
                        else
                            now = null;
                    } while (now != null);
                    if (ovr != null && (ovr.Module != mtd.Module || IsTypePublic(ovr.DeclaringType) || !(ovr.IsFamily || ovr.IsAssembly || ovr.IsFamilyAndAssembly || ovr.IsFamilyOrAssembly || ovr.IsPublic)))
                    {
                        (mtd as IAnnotationProvider).Annotations["RenOk"] = false;
                    }
                }


                Queue<TypeDefinition> q = new Queue<TypeDefinition>();
                q.Enqueue(bType);
                if (mtd.DeclaringType.HasInterfaces)
                    foreach (TypeReference i in mtd.DeclaringType.Interfaces)
                        q.Enqueue(i.Resolve());
                do
                {
                    TypeDefinition now = q.Dequeue();
                    if (now == null) continue;
                    if (now.HasGenericParameters && now.IsInterface)
                    {
                        (mtd as IAnnotationProvider).Annotations["RenOk"] = false;
                        break;
                    }
                    else if (now.IsInterface)
                    {
                        MethodDefinition imple = null;
                        foreach (MethodDefinition bMtd in now.Methods)
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
                            MethodReference refer = mtd.Module.Import(imple);
                            bool ok = true;
                            foreach (MethodReference over in mtd.Overrides)
                                if (over.FullName == refer.FullName)
                                {
                                    ok = false;
                                    break;
                                }
                            if (ok)
                                mtd.Overrides.Add(refer);
                        }
                        if (now.HasInterfaces)
                            foreach (TypeReference i in now.Interfaces)
                                q.Enqueue(i.Resolve());
                    }
                    else
                    {
                        if (now.BaseType != null)
                            q.Enqueue(now.BaseType.Resolve());
                        if (now.HasInterfaces)
                            foreach (TypeReference i in now.Interfaces)
                                q.Enqueue(i.Resolve());
                    }
                } while (q.Count != 0);
            }

            if (mtd.HasBody)
            {
                mtd.Body.SimplifyMacros();
                AnalysisCodes(mtd);
                mtd.Body.OptimizeMacros();
            }
        }
        void Analysis(FieldDefinition fld)
        {
            if (fld.IsRuntimeSpecialName || (IsTypePublic(fld.DeclaringType) &&
                (fld.IsFamily || fld.IsFamilyAndAssembly || fld.IsFamilyOrAssembly || fld.IsPublic)))
                (fld as IAnnotationProvider).Annotations["RenOk"] = false;
        }
        void Analysis(PropertyDefinition prop)
        {
            if (prop.IsRuntimeSpecialName || IsTypePublic(prop.DeclaringType))
                (prop as IAnnotationProvider).Annotations["RenOk"] = false;
        }
        void Analysis(EventDefinition evt)
        {
            if (evt.IsRuntimeSpecialName || IsTypePublic(evt.DeclaringType))
                (evt as IAnnotationProvider).Annotations["RenOk"] = false;
        }
        void AnalysisResource(ModuleDefinition mod, int resId)
        {
            EmbeddedResource res = mod.Resources[resId] as EmbeddedResource;
            ResourceReader resRdr = new ResourceReader(res.GetResourceStream());
            Dictionary<string, object> ress;
            Dictionary<string, BamlDocument> bamls;
            (res as IAnnotationProvider).Annotations["Gresources"] = ress = new Dictionary<string, object>();
            (res as IAnnotationProvider).Annotations["Gbamls"] = bamls = new Dictionary<string, BamlDocument>();
            int cc = 0;
            foreach (DictionaryEntry entry in resRdr)
            {
                Stream stream = null;
                if (entry.Value is Stream)
                {
                    byte[] buff = new byte[(entry.Value as Stream).Length];
                    (entry.Value as Stream).Position = 0;
                    (entry.Value as Stream).Read(buff, 0, buff.Length);
                    ress.Add(entry.Key as string, stream = new MemoryStream(buff));
                }
                else
                    ress.Add(entry.Key as string, entry.Value);
                if (stream != null && (entry.Key as string).EndsWith(".baml"))
                {
                    BamlDocument doc = new BamlReader(stream).Document;
                    int c = 0;

                    Dictionary<TypeDeclaration, TypeDefinition> ts = new Dictionary<TypeDeclaration, TypeDefinition>();
                    foreach (TypeDeclaration decl in doc.TypeTable.Values)
                        if (decl.Assembly == mod.Assembly.Name.FullName)
                        {
                            TypeDefinition type = mod.GetType(decl.Namespace, decl.Name);
                            if (type != null)
                            {
                                ts.Add(decl, type);
                                ((type as IAnnotationProvider).Annotations["RenRef"] as List<IReference>).Add(new BamlTypeReference(doc, decl));
                                c++; cc++;
                            }
                        }

                    Dictionary<PropertyDeclaration, PropertyDefinition> ps = new Dictionary<PropertyDeclaration, PropertyDefinition>();
                    foreach (PropertyDeclaration decl in doc.PropertyTable.Values)
                        if (ts.ContainsKey(decl.DeclaringType))
                        {
                            PropertyDefinition prop = ts[decl.DeclaringType].Properties.FirstOrDefault(p => p.Name == decl.Name);
                            if (prop != null)
                            {
                                ((prop as IAnnotationProvider).Annotations["RenRef"] as List<IReference>).Add(new BamlMemberDeclReference(doc, decl));
                                c++; cc++;
                            }
                        }
                    if (ts.ContainsKey(doc.RootElement.TypeDeclaration))
                    {
                        TypeDefinition root = mod.GetType(doc.RootElement.TypeDeclaration.Namespace, doc.RootElement.TypeDeclaration.Name);
                        Dictionary<string, IMemberDefinition> mems = new Dictionary<string, IMemberDefinition>();
                        foreach (PropertyDefinition prop in root.Properties)
                            mems.Add(prop.Name, prop);
                        foreach (EventDefinition evt in root.Events)
                            mems.Add(evt.Name, evt);
                        foreach (MethodDefinition mtd in root.Methods)
                            mems.Add(mtd.Name, mtd);

                        foreach (Property prop in doc.Properties)
                        {
                            if (!(prop.Value is string)) continue;
                            if (mems.ContainsKey((string)prop.Value))
                            {
                                ((mems[(string)prop.Value] as IAnnotationProvider).Annotations["RenRef"] as List<IReference>).Add(new BamlMemberReference(doc, prop));
                                c++; cc++;
                            }
                        }
                    }

                    if (c != 0)
                        bamls.Add(entry.Key as string, doc);
                }
            }
            if (cc != 0)
                ((res as IAnnotationProvider).Annotations["RenRef"] as List<IReference>).Add(new SaveBamlsReference(mod, resId));
        }
        void AnalysisCodes(MethodDefinition mtd)
        {
            for (int i = 0; i < mtd.Body.Instructions.Count; i++)
            {
                Instruction inst = mtd.Body.Instructions[i];
                if (inst.Operand is MethodReference ||
                    inst.Operand is FieldReference)
                {
                    if ((inst.Operand as MemberReference).DeclaringType is TypeSpecification && ((inst.Operand as MemberReference).DeclaringType as TypeSpecification).GetElementType() is TypeDefinition)
                    {
                        IMemberDefinition memDef;
                        if (inst.Operand is MethodReference && (memDef = (inst.Operand as MethodReference).GetElementMethod().Resolve()) != null)
                            ((memDef as IAnnotationProvider).Annotations["RenRef"] as List<IReference>).Add(new SpecificationReference(inst.Operand as MemberReference));
                        else if (inst.Operand is FieldReference && (memDef = (inst.Operand as FieldReference).Resolve()) != null)
                            ((memDef as IAnnotationProvider).Annotations["RenRef"] as List<IReference>).Add(new SpecificationReference(inst.Operand as MemberReference));
                    }
                    else if (inst.Operand is MethodReference)
                    {
                        MethodReference refer = inst.Operand as MethodReference;
                        string id = refer.DeclaringType.FullName + "::" + refer.Name;
                        if (Database.Reflections.ContainsKey(id))
                        {
                            ReflectionMethod Rmtd = Database.Reflections[id];
                            Instruction memInst;
                            MemberReference mem = StackTrace(i, mtd.Body.Instructions, Rmtd, mtd.Module, out memInst);
                            if (mem != null)
                                ((mem as IAnnotationProvider).Annotations["RenRef"] as List<IReference>).Add(new ReflectionReference(memInst));
                        }
                    }
                    if (ivtRefs.ContainsKey((inst.Operand as MemberReference).MetadataToken))
                    {
                        IMemberDefinition memDef;
                        if (inst.Operand is TypeReference && (memDef = (inst.Operand as TypeReference).Resolve()) != null)
                            ((memDef as IAnnotationProvider).Annotations["RenRef"] as List<IReference>).Add(new IvtMemberReference(inst.Operand as MemberReference));
                        else if (inst.Operand is MethodReference && (memDef = (inst.Operand as MethodReference).Resolve()) != null)
                            ((memDef as IAnnotationProvider).Annotations["RenRef"] as List<IReference>).Add(new IvtMemberReference(inst.Operand as MemberReference));
                        else if (inst.Operand is FieldReference && (memDef = (inst.Operand as FieldReference).Resolve()) != null)
                            ((memDef as IAnnotationProvider).Annotations["RenRef"] as List<IReference>).Add(new IvtMemberReference(inst.Operand as MemberReference));
                    }
                }
            }
        }

        bool IsTypePublic(TypeDefinition type)
        {
            do
            {
                if (!type.IsPublic && !type.IsNestedFamily && !type.IsNestedFamilyAndAssembly && !type.IsNestedFamilyOrAssembly && !type.IsNestedPublic && !type.IsPublic)
                    return false;
                type = type.DeclaringType;
            } while (type != null);
            return true;
        }

        MemberReference StackTrace(int idx, Collection<Instruction> insts, ReflectionMethod mtd, ModuleDefinition scope, out Instruction memInst)
        {
            memInst = null;
            int count = ((insts[idx].Operand as MethodReference).HasThis ? 1 : 0) + (insts[idx].Operand as MethodReference).Parameters.Count;
            if (insts[idx].OpCode.Code == Code.Newobj)
                count--;
            int c = 0;
            for (idx--; idx >= 0; idx--)
            {
                if (count == c) break;
                Instruction inst = insts[idx];
                switch (inst.OpCode.Code)
                {
                    case Code.Ldc_I4:
                    case Code.Ldc_I8:
                    case Code.Ldc_R4:
                    case Code.Ldc_R8:
                    case Code.Ldstr:
                        c++; break;
                    case Code.Call:
                    case Code.Callvirt:
                        {
                            MethodReference target = (inst.Operand as MethodReference);
                            c -= (target.HasThis ? 1 : 0) + target.Parameters.Count;
                            if (target.ReturnType.FullName != "System.Void")
                                c++;
                            break;
                        }
                    case Code.Newobj:
                        {
                            MethodReference target = (inst.Operand as MethodReference);
                            c -= target.Parameters.Count - 1;
                            break;
                        }
                    case Code.Pop:
                        c--; break;
                    case Code.Ldarg:
                        c++; break;
                    case Code.Ldfld:
                        c++; break;
                    case Code.Ldloc:
                        c++; break;
                    case Code.Ldnull:
                        c++; break;
                    case Code.Starg:
                    case Code.Stfld:
                    case Code.Stloc:
                        c--; break;
                    case Code.Ldtoken:
                        c++; break;
                    default:
                        FollowStack(inst.OpCode, ref c); break;
                }
            }

            return StackTrace2(idx + 1, count, insts, mtd, scope, out memInst);
        }
        MemberReference StackTrace2(int idx, int c, Collection<Instruction> insts, ReflectionMethod mtd, ModuleDefinition scope, out Instruction memInst)
        {
            memInst = null;
            int count = c;
            Stack<object> stack = new Stack<object>();
            for (int i = idx; ; i++)
            {
                if (stack.Count == count) break;
                Instruction inst = insts[i];
                switch (inst.OpCode.Code)
                {
                    case Code.Ldc_I4:
                    case Code.Ldc_I8:
                    case Code.Ldc_R4:
                    case Code.Ldc_R8:
                    case Code.Ldstr:
                        stack.Push(inst.Operand); break;
                    case Code.Call:
                    case Code.Callvirt:
                        {
                            MethodReference target = (inst.Operand as MethodReference);
                            int cc = -(target.HasThis ? 1 : 0) - target.Parameters.Count;
                            for (int ii = cc; ii != 0; ii++)
                                stack.Pop();
                            if (target.ReturnType.FullName != "System.Void")
                                stack.Push(target.ReturnType);
                            break;
                        }
                    case Code.Newobj:
                        {
                            MethodReference target = (inst.Operand as MethodReference);
                            for (int ii = -target.Parameters.Count; ii != 0; ii++)
                                stack.Pop();
                            stack.Push(target.DeclaringType);
                            break;
                        }
                    case Code.Pop:
                        stack.Pop(); break;
                    case Code.Ldarg:
                        stack.Push((inst.Operand as ParameterReference).ParameterType); break;
                    case Code.Ldfld:
                        stack.Push((inst.Operand as FieldReference).FieldType); break;
                    case Code.Ldloc:
                        stack.Push((inst.Operand as VariableReference).VariableType); break;
                    case Code.Ldnull:
                        stack.Push(null); break;
                    case Code.Starg:
                    case Code.Stfld:
                    case Code.Stloc:
                        stack.Pop(); break;
                    case Code.Ldtoken:
                        stack.Push(inst.Operand); break;
                    default:
                        FollowStack(inst.OpCode, stack); break;
                }
            }

            object[] objs = stack.ToArray();
            Array.Reverse(objs);

            string mem = null;
            TypeDefinition type = null;
            Resource res = null;
            for (int i = 0; i < mtd.paramLoc.Length; i++)
            {
                if (mtd.paramLoc[i] >= objs.Length) return null;
                object param = objs[mtd.paramLoc[i]];
                switch (mtd.paramType[i])
                {
                    case "Target":
                        if ((mem = param as string) == null) return null;
                        memInst = StackTrace3(idx, c, insts, mtd.paramLoc[i]);
                        break;
                    case "Type":
                    case "This":
                        if (param as TypeDefinition != null)
                            type = param as TypeDefinition;
                        break;
                    case "TargetType":
                        if (!(param is string)) return null;
                        type = scope.GetType(param as string);
                        break;
                    case "TargetResource":
                        if (!(param is string)) return null;
                        res = scope.Resources.FirstOrDefault((r) => (r.Name == param as string + ".resources"));
                        memInst = StackTrace3(idx, c, insts, mtd.paramLoc[i]);
                        break;
                }
            }
            if ((mem == null || type == null) && res == null) return null;

            if (res != null)
            {
                ((res as IAnnotationProvider).Annotations["RenRef"] as List<IReference>).Add(new ResourceNameReference(memInst));
                return null;
            }

            foreach (FieldDefinition fld in type.Fields)
                if (fld.Name == mem)
                    return fld;
            foreach (MethodDefinition mtd1 in type.Methods)
                if (mtd1.Name == mem)
                    return mtd1;
            foreach (PropertyDefinition prop in type.Properties)
                if (prop.Name == mem)
                    return prop;
            foreach (EventDefinition evt in type.Events)
                if (evt.Name == mem)
                    return evt;
            return null;
        }
        Instruction StackTrace3(int idx, int count, Collection<Instruction> insts, int c)
        {
            c = count - c;
            for (; ; idx++)
            {
                if (count < c) break;
                Instruction inst = insts[idx];
                switch (inst.OpCode.Code)
                {
                    case Code.Ldc_I4:
                    case Code.Ldc_I8:
                    case Code.Ldc_R4:
                    case Code.Ldc_R8:
                    case Code.Ldstr:
                        count--; break;
                    case Code.Call:
                    case Code.Callvirt:
                        {
                            MethodReference target = (inst.Operand as MethodReference);
                            count += (target.HasThis ? 1 : 0) + target.Parameters.Count;
                            if (target.ReturnType.FullName != "System.Void")
                                count--;
                            break;
                        }
                    case Code.Newobj:
                        {
                            MethodReference target = (inst.Operand as MethodReference);
                            c += target.Parameters.Count - 1;
                            break;
                        }
                    case Code.Pop:
                        count++; break;
                    case Code.Ldarg:
                        count--; break;
                    case Code.Ldfld:
                        count--; break;
                    case Code.Ldloc:
                        count--; break;
                    case Code.Ldnull:
                        count--; break;
                    case Code.Starg:
                    case Code.Stfld:
                    case Code.Stloc:
                        count++; break;
                    case Code.Ldtoken:
                        count--; break;
                    default:
                        int cc = count;
                        FollowStack(inst.OpCode, ref count);
                        count -= count - cc;
                        break;
                }
            }
            return insts[idx - 1];
        }

        void FollowStack(OpCode op, Stack<object> stack)
        {
            switch (op.StackBehaviourPop)
            {
                case StackBehaviour.Pop1:
                case StackBehaviour.Pop1_pop1:
                case StackBehaviour.Popref:
                case StackBehaviour.Popi:
                    stack.Pop(); break;
                case StackBehaviour.Popi_pop1:
                case StackBehaviour.Popi_popi:
                case StackBehaviour.Popi_popi8:
                case StackBehaviour.Popi_popr4:
                case StackBehaviour.Popi_popr8:
                case StackBehaviour.Popref_pop1:
                case StackBehaviour.Popref_popi:
                    stack.Pop(); stack.Pop(); break;
                case StackBehaviour.Popi_popi_popi:
                case StackBehaviour.Popref_popi_popi:
                case StackBehaviour.Popref_popi_popi8:
                case StackBehaviour.Popref_popi_popr4:
                case StackBehaviour.Popref_popi_popr8:
                case StackBehaviour.Popref_popi_popref:
                    stack.Pop(); stack.Pop(); stack.Pop(); break;
                case StackBehaviour.PopAll:
                    stack.Clear(); break;
                case StackBehaviour.Varpop:
                    throw new InvalidOperationException();
            }
            switch (op.StackBehaviourPush)
            {
                case StackBehaviour.Push1:
                case StackBehaviour.Pushi:
                case StackBehaviour.Pushi8:
                case StackBehaviour.Pushr4:
                case StackBehaviour.Pushr8:
                case StackBehaviour.Pushref:
                    stack.Push(null); break;
                case StackBehaviour.Push1_push1:
                    stack.Push(null); stack.Push(null); break;
                case StackBehaviour.Varpush:
                    throw new InvalidOperationException();
            }
        }
        void FollowStack(OpCode op, ref int stack)
        {
            switch (op.StackBehaviourPop)
            {
                case StackBehaviour.Pop1:
                case StackBehaviour.Pop1_pop1:
                case StackBehaviour.Popref:
                case StackBehaviour.Popi:
                    stack--; break;
                case StackBehaviour.Popi_pop1:
                case StackBehaviour.Popi_popi:
                case StackBehaviour.Popi_popi8:
                case StackBehaviour.Popi_popr4:
                case StackBehaviour.Popi_popr8:
                case StackBehaviour.Popref_pop1:
                case StackBehaviour.Popref_popi:
                    stack -= 2; break;
                case StackBehaviour.Popi_popi_popi:
                case StackBehaviour.Popref_popi_popi:
                case StackBehaviour.Popref_popi_popi8:
                case StackBehaviour.Popref_popi_popr4:
                case StackBehaviour.Popref_popi_popr8:
                case StackBehaviour.Popref_popi_popref:
                    stack -= 3; break;
                case StackBehaviour.PopAll:
                    stack = 0; break;
                case StackBehaviour.Varpop:
                    throw new InvalidOperationException();
            }
            switch (op.StackBehaviourPush)
            {
                case StackBehaviour.Push1:
                case StackBehaviour.Pushi:
                case StackBehaviour.Pushi8:
                case StackBehaviour.Pushr4:
                case StackBehaviour.Pushr8:
                case StackBehaviour.Pushref:
                    stack++; break;
                case StackBehaviour.Push1_push1:
                    stack += 2; break;
                case StackBehaviour.Varpush:
                    throw new InvalidOperationException();
            }
        }
    }
}
