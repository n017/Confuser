using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using System.IO;
using System.Xml;

namespace Confuser.Core.Project
{
    public interface IProjectObject
    {
        ObfConfig Config { get; set; }
    }
    public class ProjectAssembly : List<ProjectModule>, IProjectObject
    {
        public ObfConfig Config { get; set; }
        public string Path { get; set; }
        public bool IsMain { get; set; }

        public void Import(AssemblyDefinition assembly)
        {
            this.Path = assembly.MainModule.FullyQualifiedName;
        }
        public AssemblyDefinition Resolve()
        {
            return AssemblyDefinition.ReadAssembly(Path, new ReaderParameters(ReadingMode.Immediate));
        }

        public XmlElement Save(XmlDocument xmlDoc)
        {
            XmlElement elem = xmlDoc.CreateElement("assembly", ConfuserProject.Namespace);

            XmlAttribute nameAttr = xmlDoc.CreateAttribute("path");
            nameAttr.Value = Path;
            elem.Attributes.Append(nameAttr);

            if (IsMain != false)
            {
                XmlAttribute mainAttr = xmlDoc.CreateAttribute("isMain");
                mainAttr.Value = IsMain.ToString().ToLower();
                elem.Attributes.Append(mainAttr);
            }

            if (Config != null)
                elem.AppendChild(Config.Save(xmlDoc));

            foreach (var i in this)
                elem.AppendChild(i.Save(xmlDoc));

            return elem;
        }
        public void Load(XmlElement elem)
        {
            this.Path = elem.Attributes["path"].Value;
            if (elem.Attributes["isMain"] != null)
                this.IsMain = bool.Parse(elem.Attributes["isMain"].Value);
            foreach (XmlElement i in elem.ChildNodes.OfType<XmlElement>())
            {
                if (i.Name == "config")
                {
                    Config = new ObfConfig();
                    Config.Load(i);
                }
                else
                {
                    ProjectModule mod = new ProjectModule();
                    mod.Load(i);
                    this.Add(mod);
                }
            }
        }
    }
    public class ProjectModule : List<ProjectType>, IProjectObject
    {
        public ObfConfig Config { get; set; }
        public string Name { get; set; }

        public void Import(ModuleDefinition module)
        {
            this.Name = module.Name;
        }
        public ModuleDefinition Resolve(AssemblyDefinition assembly)
        {
            return assembly.Modules.Single(_ => _.Name == Name);
        }

        public XmlElement Save(XmlDocument xmlDoc)
        {
            XmlElement elem = xmlDoc.CreateElement("module", ConfuserProject.Namespace);

            XmlAttribute nameAttr = xmlDoc.CreateAttribute("name");
            nameAttr.Value = Name;
            elem.Attributes.Append(nameAttr);

            if (Config != null)
                elem.AppendChild(Config.Save(xmlDoc));

            foreach (var i in this)
                elem.AppendChild(i.Save(xmlDoc));

            return elem;
        }
        public void Load(XmlElement elem)
        {
            this.Name = elem.Attributes["name"].Value;
            foreach (XmlElement i in elem.ChildNodes.OfType<XmlElement>())
            {
                if (i.Name == "config")
                {
                    Config = new ObfConfig();
                    Config.Load(i);
                }
                else
                {
                    ProjectType type = new ProjectType();
                    type.Load(i);
                    this.Add(type);
                }
            }
        }
    }
    public class ProjectType : List<ProjectMember>, IProjectObject
    {
        public ObfConfig Config { get; set; }
        public string FullName { get; set; }

        public void Import(TypeDefinition type)
        {
            this.FullName = type.FullName;
        }
        public TypeDefinition Resolve(ModuleDefinition module)
        {
            TypeDefinition ret = module.GetType(FullName);
            if (ret == null)
                throw new Exception("Failed to resolve " + FullName + "!!!");
            else
                return ret;
        }

        public XmlElement Save(XmlDocument xmlDoc)
        {
            XmlElement elem = xmlDoc.CreateElement("type", ConfuserProject.Namespace);

            XmlAttribute fnAttr = xmlDoc.CreateAttribute("fullname");
            fnAttr.Value = FullName;
            elem.Attributes.Append(fnAttr);

            if (Config != null)
                elem.AppendChild(Config.Save(xmlDoc));

            foreach (var i in this)
                elem.AppendChild(i.Save(xmlDoc));

            return elem;
        }
        public void Load(XmlElement elem)
        {
            this.FullName = elem.Attributes["fullname"].Value;
            foreach (XmlElement i in elem.ChildNodes.OfType<XmlElement>())
            {
                if (i.Name == "config")
                {
                    Config = new ObfConfig();
                    Config.Load(i);
                }
                else
                {
                    ProjectMember member = new ProjectMember();
                    member.Load(i);
                    this.Add(member);
                }
            }
        }
    }
    public enum ProjectMemberType
    {
        Method,
        Field,
        Property,
        Event
    }
    public class ProjectMember : IProjectObject
    {
        public ObfConfig Config { get; set; }
        public string Signature { get; set; }
        public ProjectMemberType Type { get; set; }

        string ReadUntilToken(StringReader reader, params char[] token)
        {
            StringBuilder ret = new StringBuilder();
            int c = reader.Read();
            while (c != -1 && Array.IndexOf(token, (char)c) == -1)
            {
                ret.Append((char)c);
                c = reader.Read();
            }
            return ret.ToString().Trim();
        }
        string ReadUntilToken(StringReader reader, out char t, params char[] token)
        {
            StringBuilder ret = new StringBuilder();
            int c = reader.Read();
            while (c != -1 && Array.IndexOf(token, (char)c) == -1)
            {
                ret.Append((char)c);
                c = reader.Read();
            }
            t = (char)c;
            return ret.ToString().Trim();
        }

        static string GetTypeRefName(TypeReference typeRef, bool full)
        {
            StringBuilder sb = new StringBuilder();
            WriteTypeReference(sb, typeRef, full);
            return sb.ToString();
        }
        static void WriteTypeReference(StringBuilder sb, TypeReference typeRef, bool full)
        {
            WriteTypeReference(sb, typeRef, false, full);
        }
        static void WriteTypeReference(StringBuilder sb, TypeReference typeRef, bool isGenericInstance, bool full)
        {
            if (typeRef is TypeSpecification)
            {
                TypeSpecification typeSpec = typeRef as TypeSpecification;
                if (typeSpec is ArrayType)
                {
                    WriteTypeReference(sb, typeSpec.ElementType, full);
                    sb.Append("[");
                    var dims = (typeSpec as ArrayType).Dimensions;
                    for (int i = 0; i < dims.Count; i++)
                    {
                        if (i != 0) sb.Append(", ");
                        if (dims[i].IsSized)
                        {
                            sb.Append(dims[i].LowerBound.HasValue ?
                                            dims[i].LowerBound.ToString() : ".");
                            sb.Append("..");
                            sb.Append(dims[i].UpperBound.HasValue ?
                                            dims[i].UpperBound.ToString() : ".");
                        }
                    }
                    sb.Append("]");
                }
                else if (typeSpec is ByReferenceType)
                {
                    WriteTypeReference(sb, typeSpec.ElementType, full);
                    sb.Append("&");
                }
                else if (typeSpec is PointerType)
                {
                    WriteTypeReference(sb, typeSpec.ElementType, full);
                    sb.Append("*");
                }
                else if (typeSpec is OptionalModifierType)
                {
                    WriteTypeReference(sb, typeSpec.ElementType, full);
                    sb.Append(" ");
                    sb.Append("modopt");
                    sb.Append("(");
                    WriteTypeReference(sb, (typeSpec as OptionalModifierType).ModifierType, full);
                    sb.Append(")");
                }
                else if (typeSpec is RequiredModifierType)
                {
                    WriteTypeReference(sb, typeSpec.ElementType, full);
                    sb.Append(" ");
                    sb.Append("modreq");
                    sb.Append("(");
                    WriteTypeReference(sb, (typeSpec as RequiredModifierType).ModifierType, full);
                    sb.Append(")");
                }
                else if (typeSpec is FunctionPointerType)
                {
                    FunctionPointerType funcPtr = typeSpec as FunctionPointerType;
                    WriteTypeReference(sb, funcPtr.ReturnType, full);
                    sb.Append(" *(");
                    for (int i = 0; i < funcPtr.Parameters.Count; i++)
                    {
                        if (i != 0) sb.Append(", ");
                        WriteTypeReference(sb, funcPtr.Parameters[i].ParameterType, full);
                    }
                    sb.Append(")");
                }
                else if (typeSpec is SentinelType)
                {
                    sb.Append("...");
                }
                else if (typeSpec is GenericInstanceType)
                {
                    WriteTypeReference(sb, typeSpec.ElementType, true);
                    sb.Append("<");
                    var args = (typeSpec as GenericInstanceType).GenericArguments;
                    for (int i = 0; i < args.Count; i++)
                    {
                        if (i != 0) sb.Append(", ");
                        WriteTypeReference(sb, args[i], full);
                    }
                    sb.Append(">");
                }
            }
            else if (typeRef is GenericParameter)
            {
                sb.Append((typeRef as GenericParameter).Name);
            }
            else
            {
                string name = typeRef.Name;
                var genParamsCount = 0;
                if (typeRef.HasGenericParameters)
                {
                    genParamsCount = typeRef.GenericParameters.Count - (typeRef.DeclaringType == null ? 0 : typeRef.DeclaringType.GenericParameters.Count);
                    string str = "`" + genParamsCount.ToString();
                    if (typeRef.Name.EndsWith(str)) name = typeRef.Name.Substring(0, typeRef.Name.Length - str.Length);
                }

                if (typeRef.IsNested)
                {
                    WriteTypeReference(sb, typeRef.DeclaringType, full);
                    sb.Append(".");
                    sb.Append(name);
                }
                else
                {
                    if (full)
                    {
                        sb.Append(typeRef.Namespace);
                        if (!string.IsNullOrEmpty(typeRef.Namespace)) sb.Append(".");
                    }
                    sb.Append(name);
                }
                if (typeRef.HasGenericParameters && genParamsCount != 0 && !isGenericInstance)
                {
                    sb.Append("<");
                    for (int i = typeRef.GenericParameters.Count - genParamsCount; i < typeRef.GenericParameters.Count; i++)
                    {
                        if (i != 0) sb.Append(", ");
                        WriteTypeReference(sb, typeRef.GenericParameters[i], full);
                    }
                    sb.Append(">");
                }
            }
        }

        string GetSig(IMemberDefinition member, out ProjectMemberType type)
        {
            StringBuilder sig = new StringBuilder();
            if (member is MethodReference)
            {
                type = ProjectMemberType.Method;
                MethodReference method = member as MethodReference;

                WriteTypeReference(sig, method.ReturnType, false);
                sig.Append(" ");


                string name = method.Name;
                var genParamsCount = 0;
                if (method.HasGenericParameters)
                {
                    genParamsCount = method.GenericParameters.Count;
                    string str = "`" + genParamsCount.ToString();
                    if (method.Name.EndsWith(str)) name = method.Name.Substring(0, method.Name.Length - str.Length);
                }
                sig.Append(name);
                if (method.HasGenericParameters)
                {
                    sig.Append("<");
                    sig.Append(method.GenericParameters.Count.ToString());
                    sig.Append(">");
                }

                sig.Append("(");
                for (int i = 0; i < method.Parameters.Count; i++)
                {
                    if (i != 0) sig.Append(", ");
                    WriteTypeReference(sig, method.Parameters[i].ParameterType, false);
                }
                sig.Append(")");
            }
            else if (member is FieldReference)
            {
                type = ProjectMemberType.Field;
                FieldReference field = member as FieldReference;

                WriteTypeReference(sig, field.FieldType, false);
                sig.Append(" ");
                sig.Append(field.Name);
            }
            else if (member is PropertyReference)
            {
                type = ProjectMemberType.Property;
                PropertyReference prop = member as PropertyReference;

                WriteTypeReference(sig, prop.PropertyType, false);
                sig.Append(" ");
                sig.Append(prop.Name);
            }
            else if (member is EventReference)
            {
                type = ProjectMemberType.Event;
                EventReference evt = member as EventReference;

                WriteTypeReference(sig, evt.EventType, false);
                sig.Append(" ");
                sig.Append(evt.Name);
            }
            else
                throw new NotSupportedException();
            return sig.ToString();
        }
        public void Import(IMemberDefinition member)
        {
            ProjectMemberType t;
            Signature = GetSig(member, out t);
            Type = t;
        }
        public IMemberDefinition Resolve(TypeDefinition type)
        {
            StringReader reader = new StringReader(Signature);
            ProjectMemberType x;
            switch (Type)
            {
                case ProjectMemberType.Method:
                    {
                        foreach (var i in type.Methods)
                            if (GetSig(i, out x) == Signature && x == Type)
                                return i;
                    } break;
                case ProjectMemberType.Field:
                    {
                        foreach (var i in type.Fields)
                            if (GetSig(i, out x) == Signature && x == Type)
                                return i;
                    } break;
                case ProjectMemberType.Property:
                    {
                        foreach (var i in type.Properties)
                            if (GetSig(i, out x) == Signature && x == Type)
                                return i;
                    } break;
                case ProjectMemberType.Event:
                    {
                        foreach (var i in type.Events)
                            if (GetSig(i, out x) == Signature && x == Type)
                                return i;
                    } break;
            }
            throw new Exception("Failed to resolve " + Signature + "!!!");
        }

        public XmlElement Save(XmlDocument xmlDoc)
        {
            XmlElement elem = xmlDoc.CreateElement("member", ConfuserProject.Namespace);

            XmlAttribute sigAttr = xmlDoc.CreateAttribute("sig");
            sigAttr.Value = Signature;
            elem.Attributes.Append(sigAttr);
            XmlAttribute typeAttr = xmlDoc.CreateAttribute("type");
            typeAttr.Value = Type.ToString().ToLower();
            elem.Attributes.Append(typeAttr);

            if (Config != null)
                elem.AppendChild(Config.Save(xmlDoc));

            return elem;
        }
        public void Load(XmlElement elem)
        {
            this.Signature = elem.Attributes["sig"].Value;
            this.Type = (ProjectMemberType)Enum.Parse(typeof(ProjectMemberType), elem.Attributes["type"].Value, true);
            foreach (XmlElement i in elem.ChildNodes.OfType<XmlElement>())
            {
                if (i.Name == "config")
                {
                    Config = new ObfConfig();
                    Config.Load(i);
                }
            }
        }
    }
}
