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
            XmlElement elem = xmlDoc.CreateElement("assembly");

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
            foreach (XmlElement i in elem.ChildNodes)
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
            XmlElement elem = xmlDoc.CreateElement("module");

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
            foreach (XmlElement i in elem.ChildNodes)
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
            return module.GetType(FullName);
        }

        public XmlElement Save(XmlDocument xmlDoc)
        {
            XmlElement elem = xmlDoc.CreateElement("type");

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
            foreach (XmlElement i in elem.ChildNodes)
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

        public void Import(IMemberDefinition member)
        {
            StringBuilder sig = new StringBuilder();
            if (member is MethodReference)
            {
                Type = ProjectMemberType.Method;
                MethodReference method = member as MethodReference;

                sig.Append(method.ReturnType.Name);
                sig.Append(" ");
                sig.Append(method.Name);
                sig.Append("(");
                for (int i = 0; i < method.Parameters.Count; i++)
                {
                    if (i != 0) sig.Append(", ");
                    sig.Append(method.Parameters[i].ParameterType.Name);
                }
                sig.Append(")");
            }
            else if (member is FieldReference)
            {
                Type = ProjectMemberType.Field;
                FieldReference field = member as FieldReference;

                sig.Append(field.FieldType.Name);
                sig.Append(" ");
                sig.Append(field.Name);
            }
            else if (member is PropertyReference)
            {
                Type = ProjectMemberType.Property;
                PropertyReference prop = member as PropertyReference;

                sig.Append(prop.PropertyType.Name);
                sig.Append(" ");
                sig.Append(prop.Name);
            }
            else if (member is EventReference)
            {
                Type = ProjectMemberType.Event;
                EventReference evt = member as EventReference;

                sig.Append(evt.EventType.Name);
                sig.Append(" ");
                sig.Append(evt.Name);
            }
            Signature = sig.ToString();
        }
        public IMemberDefinition Resolve(TypeDefinition type)
        {
            StringReader reader = new StringReader(Signature);
            switch (Type)
            {
                case ProjectMemberType.Method:
                    {
                        string retType = ReadUntilToken(reader, ' ');
                        string name = ReadUntilToken(reader, '(');
                        List<string> argTypes = new List<string>();
                        string s = ReadUntilToken(reader, ',', ')');
                        while (!string.IsNullOrEmpty(s))
                        {
                            argTypes.Add(s);
                            s = ReadUntilToken(reader, ',', ')');
                        }

                        foreach (var i in type.Methods)
                        {
                            if (i.Name != name || i.ReturnType.Name != retType) continue;
                            if (i.Parameters.Count != argTypes.Count) continue;
                            bool yes = true;
                            for (int j = 0; j < argTypes.Count; j++)
                                if (i.Parameters[j].ParameterType.Name != argTypes[j])
                                {
                                    yes = false;
                                    break;
                                }
                            if (yes)
                                return i;
                        }
                        return null;
                    }
                case ProjectMemberType.Field:
                    {
                        string fieldType = ReadUntilToken(reader, ' ');
                        string name = ReadUntilToken(reader, '\0');

                        foreach (var i in type.Fields)
                        {
                            if (i.Name == name && i.FieldType.Name == fieldType)
                                return i;
                        }
                        return null;
                    }
                case ProjectMemberType.Property:
                    {
                        string propType = ReadUntilToken(reader, ' ');
                        string name = ReadUntilToken(reader, '\0');

                        foreach (var i in type.Properties)
                        {
                            if (i.Name == name && i.PropertyType.Name == propType)
                                return i;
                        }
                        return null;
                    }
                case ProjectMemberType.Event:
                    {
                        string evtType = ReadUntilToken(reader, ' ');
                        string name = ReadUntilToken(reader, '\0');

                        foreach (var i in type.Events)
                        {
                            if (i.Name == name && i.EventType.Name == evtType)
                                return i;
                        }
                        return null;
                    }
            }
            return null;
        }

        public XmlElement Save(XmlDocument xmlDoc)
        {
            XmlElement elem = xmlDoc.CreateElement("member");

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
            foreach (XmlElement i in elem.ChildNodes)
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
