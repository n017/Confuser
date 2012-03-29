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
using Confuser.Core.Analyzers.Baml;

namespace Confuser.Core.Analyzers
{
    partial class NameAnalyzer : Analyzer
    {
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

        Dictionary<AssemblyDefinition, List<string>> ivtMap;    //also act as assembly list
        Dictionary<MetadataToken, MemberReference> ivtRefs = new Dictionary<MetadataToken, MemberReference>();
        Dictionary<TypeDefinition, VTable> vTbls = new Dictionary<TypeDefinition, VTable>();

        public override void Analyze(IEnumerable<AssemblyDefinition> asms)
        {
            foreach (AssemblyDefinition asm in asms)
                foreach (ModuleDefinition mod in asm.Modules)
                    Init(mod);

            AnalyzeIvtMap(asms);
            foreach (AssemblyDefinition asm in asms)
            {
                try
                {
                    AnalyzeIvt(asm);
                }
                catch { }
                foreach (ModuleDefinition mod in asm.Modules)
                    Analyze(mod);
            }
            foreach (AssemblyDefinition asm in asms)
                foreach (ModuleDefinition mod in asm.Modules)
                    foreach (TypeDefinition type in mod.Types)
                        ConstructVTable(type);
        }
        void Init(ModuleDefinition mod)
        {
            (mod as IAnnotationProvider).Annotations["RenMode"] = NameMode.ASCII;
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

        void ConstructVTable(TypeDefinition typeDef)
        {
            foreach (var i in typeDef.NestedTypes)
                ConstructVTable(i);
            VTable.GetVTable(typeDef, vTbls);
        }

        void AnalyzeIvtMap(IEnumerable<AssemblyDefinition> asms)
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
                    Logger._Log("InternalsVisibleToAttribute found in " + asm.FullName + "!");

                    List<AssemblyDefinition> refAsms = new List<AssemblyDefinition>();
                    foreach (AssemblyDefinition asmm in asms)
                        if (internalVis.Contains(GetIva(asmm.Name)))
                            refAsms.Add(asmm);

                    if (refAsms.Count == 0)
                        Logger._Log("Internal assemblies NOT found!");
                    else
                        Logger._Log("Internal assemblies found!");
                    foreach (AssemblyDefinition i in refAsms)
                    {
                        if (!ivtMap.ContainsKey(i)) ivtMap.Add(i, new List<string>());
                        ivtMap[i].Add(asm.FullName);
                    }
                }
            }
        }
        void AnalyzeIvt(AssemblyDefinition asm)
        {
            List<string> ivts = ivtMap[asm];
            ivtRefs.Clear();
            AnalyzeCustomAttributes(asm);
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
        void Analyze(ModuleDefinition mod)
        {
            AnalyzeCustomAttributes(mod);
            for (int i = 0; i < mod.Resources.Count; i++)
                if (mod.Resources[i].Name.EndsWith(".g.resources") && mod.Resources[i] is EmbeddedResource)
                    AnalyzeResource(mod, i);
            foreach (TypeDefinition type in mod.Types)
                Analyze(type);
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

        Dictionary<AssemblyDefinition, Dictionary<string, List<PropertyDefinition>>> props = new Dictionary<AssemblyDefinition, Dictionary<string, List<PropertyDefinition>>>();
        void PopulateProperties(TypeDefinition typeDef, Dictionary<string, List<PropertyDefinition>> props)
        {
            foreach (var i in typeDef.NestedTypes)
                PopulateProperties(i, props);
            foreach (var i in typeDef.Properties)
            {
                List<PropertyDefinition> p;
                if (!props.TryGetValue(i.Name, out p))
                    p = props[i.Name] = new List<PropertyDefinition>();
                p.Add(i);
            }
        }
        void PopulateProperties(AssemblyDefinition asm)
        {
            Dictionary<string, List<PropertyDefinition>> p = new Dictionary<string, List<PropertyDefinition>>();
            foreach (var i in asm.Modules)
                foreach (var j in i.Types)
                    PopulateProperties(j, p);
            props[asm] = p;
        }
        List<PropertyDefinition> GetProperty(string name, out bool hasImport)
        {
            List<PropertyDefinition> ret = new List<PropertyDefinition>();
            hasImport = false;
            foreach (var i in props)
            {
                List<PropertyDefinition> p;
                if (i.Value.TryGetValue(name, out p))
                {
                    if (!ivtMap.ContainsKey(i.Key))
                        hasImport = true;
                    else
                        ret.AddRange(p);
                }
            }
            return ret.Count == 0 ? null : ret;
        }
        string GetBamlAssemblyFullName(AssemblyNameReference asmName)
        {
            if (asmName.PublicKeyToken == null || asmName.PublicKeyToken.Length == 0)
                return string.Format("{0}, Version={1}", asmName.Name, asmName.Version);
            else
            {
                string token = BitConverter.ToString(asmName.PublicKeyToken).Replace("-", "").ToLower();
                return string.Format("{0}, Version={1}, PublicKeyToken={2}", asmName.Name, asmName.Version, token);
            }
        }
        static void SplitClrNsUri(string xmlNamespace, out string clrNamespace, out string assembly)
        {
            clrNamespace = null;
            assembly = null;
            int index = xmlNamespace.IndexOf("clr-namespace:", StringComparison.Ordinal);
            if (index >= 0)
            {
                index += "clr-namespace:".Length;
                if (index < xmlNamespace.Length)
                {
                    int startIndex = xmlNamespace.IndexOf(";assembly=", StringComparison.Ordinal);
                    if (startIndex < index)
                    {
                        clrNamespace = xmlNamespace.Substring(index);
                    }
                    else
                    {
                        clrNamespace = xmlNamespace.Substring(index, startIndex - index);
                        startIndex += ";assembly=".Length;
                        if (startIndex < xmlNamespace.Length)
                        {
                            assembly = xmlNamespace.Substring(startIndex);
                        }
                    }
                }
            }
        }
        static TypeDefinition ResolveXmlns(string xmlNamespace, string typeName, AssemblyDefinition context, List<AssemblyDefinition> asms)
        {
            typeName = typeName.Replace('+', '/');
            if (xmlNamespace.StartsWith("clr-namespace:"))
            {
                string ns, asmName;
                SplitClrNsUri(xmlNamespace, out ns, out asmName);

                AssemblyDefinition asm;
                if (asmName == null)
                    asm = context;
                else
                {
                    asmName = AssemblyNameReference.Parse(asmName).Name;
                    if (asmName == context.Name.Name)
                        asm = context;
                    else
                        asm = asms.SingleOrDefault(_ => _.Name.Name == asmName);
                }

                if (asm != null)
                {
                    if (string.IsNullOrEmpty(ns))
                        return asm.MainModule.GetType(typeName);
                    else
                        return asm.MainModule.GetType(string.Format("{0}.{1}", ns, typeName));
                }
            }
            else
            {
                foreach (var i in asms)
                {
                    foreach (var attr in i.CustomAttributes.Where(_ => _.AttributeType.FullName == "System.Windows.Markup.XmlnsDefinitionAttribute"))
                    {
                        string uri = attr.ConstructorArguments[0].Value as string;
                        string clrNs = attr.ConstructorArguments[1].Value as string;
                        if (uri == xmlNamespace)
                        {
                            TypeDefinition typeDef;
                            if (string.IsNullOrEmpty(clrNs))
                                typeDef = i.MainModule.GetType(typeName);
                            else
                                typeDef = i.MainModule.GetType(string.Format("{0}.{1}", clrNs, typeName));
                            if (typeDef != null)
                                return typeDef;
                        }
                    }
                }
            }
            return null;
        }
        void ProcessProperty(BamlRecord rec, string path)
        {
            int idx = -1;
            List<BamlPathReference> refers = new List<BamlPathReference>();
            string prev = null;
            char prevSym = '.';
            for (int i = 0; i < path.Length; i++)
            {
                if (char.IsLetterOrDigit(path[i]))
                {
                    if (idx == -1)
                        idx = i;
                }
                else if (i - idx > 0 && idx != -1 && (path[i] == '.' || path[i] == ')'))
                {
                    string name = path.Substring(idx, i - idx);
                    bool hasImport;
                    var p = GetProperty(name, out hasImport);
                    if (p != null)
                        foreach (var prop in p)
                            (prop as IAnnotationProvider).Annotations["RenOk"] = false;
                    //if (p != null && prevSym == '.')
                    //{
                    //    var specProp = p.SingleOrDefault(_ => _.DeclaringType.Name == prev);
                    //    if (specProp != null)
                    //    {
                    //        BamlPathReference refer = new BamlPathReference(rec, idx, i - 1);
                    //        refers.Add(refer);
                    //        ((specProp as IAnnotationProvider).Annotations["RenRef"] as List<IReference>).Add(refer);
                    //    }
                    //    else
                    //        foreach (var prop in p)
                    //        {
                    //            BamlPathReference refer = new BamlPathReference(rec, idx, i - 1);
                    //            refers.Add(refer);
                    //            ((prop as IAnnotationProvider).Annotations["RenRef"] as List<IReference>).Add(refer);
                    //        }
                    //}

                    idx = -1;
                    prev = name;
                    prevSym = path[i];
                }
                else
                {
                    prevSym = path[i];
                    idx = -1;
                }
            }
            if (idx != -1)
            {
                string name = path.Substring(idx);
                bool hasImport;
                var p = GetProperty(name, out hasImport);
                //if (p != null)
                //    foreach (var prop in p)
                //    {
                //        BamlPathReference refer = new BamlPathReference(rec, idx, path.Length - 1);
                //        refers.Add(refer);
                //        ((prop as IAnnotationProvider).Annotations["RenRef"] as List<IReference>).Add(refer);
                //    }
                if (p != null)
                    foreach (var prop in p)
                        (prop as IAnnotationProvider).Annotations["RenOk"] = false;
            }
            else
            {
                string name = path;
                bool hasImport;
                var p = GetProperty(name, out hasImport);
                //if (p != null)
                //    foreach (var prop in p)
                //    {
                //        BamlPathReference refer = new BamlPathReference(rec, idx, path.Length - 1);
                //        refers.Add(refer);
                //        ((prop as IAnnotationProvider).Annotations["RenRef"] as List<IReference>).Add(refer);
                //    }
                if (p != null)
                    foreach (var prop in p)
                        (prop as IAnnotationProvider).Annotations["RenOk"] = false;
            }

            foreach (var i in refers)
                i.refers = refers;
        }
        void AnalyzeResource(ModuleDefinition mod, int resId)
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
                    cc++;
                    BamlDocument doc = BamlReader.ReadDocument(stream);
                    (mod as IAnnotationProvider).Annotations["RenMode"] = NameMode.Letters;

                    for (int i = 0; i < doc.Count; i++)
                    {
                        if (doc[i] is LineNumberAndPositionRecord || doc[i] is LinePositionRecord)
                        {
                            doc.RemoveAt(i); i--;
                        }
                    }

                    int asmId = -1;
                    Dictionary<ushort, string> asms = new Dictionary<ushort, string>();
                    List<AssemblyDefinition> assemblies = new List<AssemblyDefinition>();
                    props.Clear();
                    foreach (var rec in doc.OfType<AssemblyInfoRecord>())
                    {
                        AssemblyNameReference nameRef = AssemblyNameReference.Parse(rec.AssemblyFullName);
                        if (nameRef.Name == mod.Assembly.Name.Name)
                        {
                            asmId = rec.AssemblyId;
                            rec.AssemblyFullName = GetBamlAssemblyFullName(mod.Assembly.Name);
                            assemblies.Add(mod.Assembly);
                            PopulateProperties(mod.Assembly);
                            nameRef = null;
                        }
                        else
                        {
                            foreach (var i in ivtMap)
                                if (i.Key.Name.Name == nameRef.Name)
                                {
                                    rec.AssemblyFullName = GetBamlAssemblyFullName(i.Key.Name);
                                    assemblies.Add(i.Key);
                                    PopulateProperties(i.Key);
                                    nameRef = null;
                                    break;
                                }
                        }
                        asms.Add(rec.AssemblyId, rec.AssemblyFullName);
                        if (nameRef != null)
                            PopulateProperties(GlobalAssemblyResolver.Instance.Resolve(nameRef));
                    }

                    Dictionary<ushort, TypeDefinition> types = new Dictionary<ushort, TypeDefinition>();
                    foreach (var rec in doc.OfType<TypeInfoRecord>())
                        if ((rec.AssemblyId & 0xfff) == asmId)
                        {
                            TypeDefinition type = mod.GetType(rec.TypeFullName);
                            if (type != null)
                            {
                                types.Add(rec.TypeId, type);
                                ((type as IAnnotationProvider).Annotations["RenRef"] as List<IReference>).Add(new BamlTypeReference(rec));
                            }
                        }

                    Dictionary<string, string> xmlns = new Dictionary<string, string>();
                    foreach (var rec in doc.OfType<XmlnsPropertyRecord>())
                        xmlns[rec.Prefix] = rec.XmlNamespace;

                    Dictionary<ushort, string> ps = new Dictionary<ushort, string>();
                    foreach (var rec in doc.OfType<AttributeInfoRecord>())
                    {
                        if (types.ContainsKey(rec.OwnerTypeId))
                        {
                            PropertyDefinition prop = types[rec.OwnerTypeId].Properties.FirstOrDefault(p => p.Name == rec.Name);
                            if (prop != null)
                            {
                                ((prop as IAnnotationProvider).Annotations["RenRef"] as List<IReference>).Add(new BamlAttributeReference(rec));
                            }
                            FieldDefinition field = types[rec.OwnerTypeId].Fields.FirstOrDefault(p => p.Name == rec.Name);
                            if (field != null)
                            {
                                ((field as IAnnotationProvider).Annotations["RenRef"] as List<IReference>).Add(new BamlAttributeReference(rec));
                            }
                        }
                        ps.Add(rec.AttributeId, rec.Name);
                    }

                    foreach (var rec in doc.OfType<PropertyWithConverterRecord>())
                    {
                        if (rec.ConverterTypeId == 0xfd4c || ((short)rec.AttributeId > 0 && ps[rec.AttributeId] == "TypeName"))  //TypeExtension
                        {
                            string type = rec.Value;

                            string xmlNamespace;
                            if (type.IndexOf(':') != -1)
                            {
                                xmlNamespace = xmlns[type.Substring(0, type.IndexOf(':'))];
                                type = type.Substring(type.IndexOf(':') + 1, type.Length - type.IndexOf(':') - 1);
                            }
                            else
                            {
                                xmlNamespace = xmlns[""];
                            }
                            TypeDefinition typeDef;
                            if ((typeDef = ResolveXmlns(xmlNamespace, type, mod.Assembly, assemblies)) != null)
                            {
                                ((typeDef as IAnnotationProvider).Annotations["RenRef"] as List<IReference>).Add(new BamlTypeExtReference(rec, doc, typeDef.Module.Assembly.Name.Name));
                            }
                        }
                        if (rec.ConverterTypeId == 0xff77 || rec.ConverterTypeId == 0xfe14 || rec.ConverterTypeId == 0xfd99)
                        {
                            ProcessProperty(rec, rec.Value);
                        }
                    }

                    for (int i = 1; i < doc.Count; i++)
                    {
                        ElementStartRecord binding = doc[i - 1] as ElementStartRecord;
                        ConstructorParametersStartRecord param = doc[i] as ConstructorParametersStartRecord;
                        if (binding != null && param != null && binding.TypeId == 0xffec)//Binding
                        {
                            TextRecord path = doc[i + 1] as TextRecord;
                            ProcessProperty(path, path.Value);
                        }
                    }

                    var rootRec = doc.OfType<ElementStartRecord>().FirstOrDefault();
                    if (rootRec != null && types.ContainsKey(rootRec.TypeId))
                    {
                        TypeDefinition root = types[rootRec.TypeId];
                        Dictionary<string, IMemberDefinition> mems = new Dictionary<string, IMemberDefinition>();
                        foreach (PropertyDefinition prop in root.Properties)
                            mems.Add(prop.Name, prop);
                        foreach (EventDefinition evt in root.Events)
                            mems.Add(evt.Name, evt);
                        foreach (MethodDefinition mtd in root.Methods)
                            mems.Add(mtd.Name, mtd);

                        foreach (var rec in doc.OfType<PropertyRecord>())
                        {
                            if (!(rec.Value is string)) continue;
                            if (mems.ContainsKey((string)rec.Value))
                            {
                                ((mems[(string)rec.Value] as IAnnotationProvider).Annotations["RenRef"] as List<IReference>).Add(new BamlPropertyReference(rec));
                            }
                        }
                    }

                    bamls.Add(entry.Key as string, doc);
                }
            }
            if (cc != 0)
                ((res as IAnnotationProvider).Annotations["RenRef"] as List<IReference>).Add(new SaveBamlsReference(mod, resId));
            else
                System.Diagnostics.Debugger.Break();
        }

        void AnalyzeCodes(MethodDefinition mtd)
        {
            for (int i = 0; i < mtd.Body.Instructions.Count; i++)
            {
                Instruction inst = mtd.Body.Instructions[i];
                if (inst.OpCode.Code == Code.Ldtoken && inst.Operand is IMemberDefinition)
                    (inst.Operand as IAnnotationProvider).Annotations["RenOk"] = false;


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
                            if (target.Name == "GetTypeFromHandle" && target.DeclaringType.FullName == "System.Type")
                                break;
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
            int typeIdx;
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
                        {
                            type = param as TypeDefinition;
                            typeIdx = mtd.paramLoc[i];
                        }
                        break;
                    case "TargetType":
                        if (!(param is string)) return null;
                        type = scope.GetType(param as string);
                        typeIdx = mtd.paramLoc[i];
                        break;
                    case "TargetResource":
                        if (!(param is string)) return null;
                        res = scope.Resources.FirstOrDefault((r) => (r.Name == param as string + ".resources"));
                        memInst = StackTrace3(idx, c, insts, mtd.paramLoc[i]);
                        break;
                }
            }
            if (mem == null && type == null && res == null) return null;

            if (res != null)
            {
                ((res as IAnnotationProvider).Annotations["RenRef"] as List<IReference>).Add(new ResourceNameReference(memInst));
                return null;
            }

            if (mem != null && type != null)
            {
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
            }
            else if (type != null)
            {
                memInst = StackTrace3(idx, c, insts, mtd.paramLoc[Array.IndexOf(mtd.paramType, "TargetType")]);
                return type;
            }
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
