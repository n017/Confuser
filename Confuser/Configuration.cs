using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using Mono.Cecil;
using Confuser.Core;
using System.Reflection;

namespace Confuser
{
    class Configuration
    {
        MainWindow win;
        public Configuration(MainWindow win) { this.win = win; }

        AssemblyDefinition asm;
        string pth;
        Dictionary<string, IMemberDefinition[]> paras = new Dictionary<string, IMemberDefinition[]>();
        bool compress;
        public AssemblyDefinition Assembly { get { return asm; } set { asm = value; } }
        public string Path { get { return pth; } set { pth = value; } }
        public Dictionary<string, IMemberDefinition[]> Parameters { get { return paras; } }
        public bool Compress { get { return compress; } set { compress = value; } }

        public void Load(Stream str)
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(str);

            pth = doc.SelectSingleNode("/configuration/assembly/text()").Value;
            asm = AssemblyDefinition.ReadAssembly(pth, new ReaderParameters(ReadingMode.Immediate));

            foreach (XmlNode plug in doc.SelectNodes("/configuration/plugins/plugin"))
            {
                AssemblyName asmn = new AssemblyName(plug.Attributes["fullname"].Value);
                asmn.CodeBase = plug.Attributes["uri"].Value;
                var plugAsm = System.Reflection.Assembly.Load(asmn);
                win.LoadPluginAssembly(plugAsm);
            }

            compress = bool.Parse(doc.SelectSingleNode("/configuration/compress/text()").Value);

            foreach (XmlNode para in doc.SelectNodes("/configuration/parameter"))
            {
                string name = para.Attributes["name"].Value;
                List<IMemberDefinition> defs = new List<IMemberDefinition>();
                foreach (XmlElement element in para.ChildNodes)
                {
                    switch (element.Name)
                    {
                        case "type":
                            defs.Add(asm.MainModule.GetType(element.Attributes["fullname"].Value));
                            break;
                        case "method":
                            {
                                TypeDefinition type = asm.MainModule.GetType(element.Attributes["declaringType"].Value);
                                string mtdSig = element.Attributes["sig"].Value;
                                foreach (MethodDefinition mtd in type.Methods)
                                {
                                    if (mtd.FullName == mtdSig)
                                    {
                                        defs.Add(mtd); break;
                                    }
                                }
                            }
                            break;
                        case "field":
                            {
                                TypeDefinition type = asm.MainModule.GetType(element.Attributes["declaringType"].Value);
                                string fldType = element.Attributes["type"].Value;
                                foreach (FieldDefinition fld in type.Fields)
                                {
                                    if (fld.FieldType.FullName == fldType)
                                    {
                                        defs.Add(fld); break;
                                    }
                                }
                            }
                            break;
                        case "property":
                            {
                                TypeDefinition type = asm.MainModule.GetType(element.Attributes["declaringType"].Value);
                                string propType = element.Attributes["type"].Value;
                                foreach (PropertyDefinition prop in type.Properties)
                                {
                                    if (prop.PropertyType.FullName == propType)
                                    {
                                        defs.Add(prop); break;
                                    }
                                }
                            }
                            break;
                        case "event":
                            {
                                TypeDefinition type = asm.MainModule.GetType(element.Attributes["declaringType"].Value);
                                string evtType = element.Attributes["type"].Value;
                                foreach (EventDefinition evt in type.Events)
                                {
                                    if (evt.EventType.FullName == evtType)
                                    {
                                        defs.Add(evt); break;
                                    }
                                }
                            }
                            break;
                    }
                }
                paras.Add(name, defs.ToArray());
            }
        }
        public void Save(Stream str)
        {
            XmlDocument doc = new XmlDocument();
            XmlElement root = doc.CreateElement("configuration");
            doc.AppendChild(root);

            XmlElement asmPath = doc.CreateElement("assembly");
            asmPath.InnerText = pth;
            root.AppendChild(asmPath);

            XmlElement compressEle = doc.CreateElement("compress");
            compressEle.InnerText = compress.ToString();
            root.AppendChild(compressEle);

            XmlElement plugs = doc.CreateElement("plugins");
            foreach (Assembly plugAsm in win.Plugs)
            {
                XmlElement plug = doc.CreateElement("plugin");

                XmlAttribute plugName = doc.CreateAttribute("fullname");
                plugName.Value = plugAsm.FullName;
                plug.Attributes.Append(plugName);

                XmlAttribute plugPath = doc.CreateAttribute("uri");
                plugPath.Value = plugAsm.CodeBase;
                plug.Attributes.Append(plugPath);

                plugs.AppendChild(plug);
            }
            root.AppendChild(plugs);

            foreach (KeyValuePair<string, IMemberDefinition[]> param in paras)
            {
                XmlElement paramEle = doc.CreateElement("parameter");

                XmlAttribute name = doc.CreateAttribute("name");
                name.Value = param.Key;
                paramEle.Attributes.Append(name);

                foreach (IMemberDefinition mem in param.Value)
                {
                    XmlElement memEle;
                    if (mem is TypeDefinition)
                    {
                        memEle = doc.CreateElement("type");

                        XmlAttribute fullname = doc.CreateAttribute("fullname");
                        fullname.Value = (mem as TypeDefinition).FullName;
                        memEle.Attributes.Append(fullname);
                    }
                    else if (mem is MethodDefinition)
                    {
                        memEle = doc.CreateElement("method");

                        XmlAttribute declaringType = doc.CreateAttribute("declaringType");
                        declaringType.Value = mem.DeclaringType.FullName;
                        memEle.Attributes.Append(declaringType);

                        XmlAttribute sig = doc.CreateAttribute("sig");
                        sig.Value = mem.FullName;
                        memEle.Attributes.Append(sig);
                    }
                    else if (mem is FieldDefinition)
                    {
                        memEle = doc.CreateElement("field");

                        XmlAttribute declaringType = doc.CreateAttribute("declaringType");
                        declaringType.Value = mem.DeclaringType.FullName;
                        memEle.Attributes.Append(declaringType);

                        XmlAttribute type = doc.CreateAttribute("type");
                        type.Value = (mem as FieldDefinition).FieldType.FullName;
                        memEle.Attributes.Append(type);
                    }
                    else if (mem is PropertyDefinition)
                    {
                        memEle = doc.CreateElement("property");

                        XmlAttribute declaringType = doc.CreateAttribute("declaringType");
                        declaringType.Value = mem.DeclaringType.FullName;
                        memEle.Attributes.Append(declaringType);

                        XmlAttribute type = doc.CreateAttribute("type");
                        type.Value = (mem as PropertyDefinition).PropertyType.FullName;
                        memEle.Attributes.Append(type);
                    }
                    else if (mem is EventDefinition)
                    {
                        memEle = doc.CreateElement("field");

                        XmlAttribute declaringType = doc.CreateAttribute("declaringType");
                        declaringType.Value = mem.DeclaringType.FullName;
                        memEle.Attributes.Append(declaringType);

                        XmlAttribute type = doc.CreateAttribute("type");
                        type.Value = (mem as EventDefinition).EventType.FullName;
                        memEle.Attributes.Append(type);
                    }
                    else
                        throw new InvalidOperationException();
                    paramEle.AppendChild(memEle);
                }
                root.AppendChild(paramEle);
            }
            doc.Save(str);
        }
    }
}
