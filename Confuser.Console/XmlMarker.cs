using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Confuser.Core;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using Mono.Cecil;
using System.IO;

namespace Confuser.Console
{
    class XmlMarker : Marker
    {
        XDocument xmlDoc;
        private XmlMarker(XDocument xmlDoc) { this.xmlDoc = xmlDoc; }
        public static bool Create(XDocument doc, out XmlMarker marker)
        {
            marker = new XmlMarker(doc);
            WriteWithColor(ConsoleColor.Yellow, "Validating...");
            List<string> errs = new List<string>();
            marker.Validate(errs);
            if (errs.Count != 0)
            {
                WriteLine();
                WriteLineWithColor(ConsoleColor.Red, "ERRORS!!!");
                foreach (string err in errs)
                    WriteLineWithColor(ConsoleColor.Red, err);
                return false;
            }
            else
            {
                WriteLineWithColor(ConsoleColor.Green, "   OK!!!");
                return true;
            }
        }

        static void WriteWithColor(ConsoleColor color, string txt)
        {
            ConsoleColor clr = System.Console.ForegroundColor;
            System.Console.ForegroundColor = color;
            System.Console.Write(txt);
            System.Console.ForegroundColor = clr;
        }
        static void WriteLineWithColor(ConsoleColor color, string txt)
        {
            ConsoleColor clr = System.Console.ForegroundColor;
            System.Console.ForegroundColor = color;
            System.Console.WriteLine(txt);
            System.Console.ForegroundColor = clr;
        }
        static void WriteLine(string txt)
        {
            System.Console.WriteLine(txt);
        }
        static void WriteLine()
        {
            System.Console.WriteLine();
        }

        void Validate(List<string> errs)
        {
            XElement root = xmlDoc.Element("configuration");
            if (root == null)
            {
                errs.Add(string.Format("Line {0:D3}: root element must be named 'configuration'.", ((IXmlLineInfo)xmlDoc).LinePosition));
                return;
            }
            foreach (XElement asm in root.Elements())
            {
                if (asm.Name != "assembly")
                    errs.Add(string.Format("Line {0:D3}: 'assembly' element required.", ((IXmlLineInfo)asm).LinePosition));
                if (asm.Attribute("path") == null)
                    errs.Add(string.Format("Line {0:D3}: path of assembly not specified.", ((IXmlLineInfo)asm).LineNumber));
                ValidateAssembly(asm, errs);
            }
        }
        void ValidateAssembly(XElement asm, List<string> errs)
        {
            foreach (XElement node in asm.Elements())
            {
                if (node.Name != "module" && node.Name != "settings" && node.Name != "packer")
                    errs.Add(string.Format("Line {0:D3}: 'module' or 'settings' or 'packer' element required.", ((IXmlLineInfo)node).LineNumber));
                if (node.Name == "module" && node.Attribute("name") == null)
                    errs.Add(string.Format("Line {0:D3}: name of module not specified.", ((IXmlLineInfo)node).LineNumber));

                if (node.Name == "module")
                    ValidateModule(node, errs);
                else if (node.Name == "settings")
                    ValidateSettings(node, errs);
                else if (node.Name == "packer")
                    ValidatePacker(node, errs);
            }
        }
        void ValidateModule(XElement mod, List<string> errs)
        {
            foreach (XElement node in mod.Elements())
            {
                if (node.Name != "type" && node.Name != "settings")
                    errs.Add(string.Format("Line {0:D3}: 'type' or 'settings' element required.", ((IXmlLineInfo)node).LineNumber));

                if (node.Name == "type")
                    ValidateType(node, errs);
                else
                    ValidateSettings(node, errs);
            }
        }
        void ValidateType(XElement type, List<string> errs)
        {
            if (type.Elements().Count() != 1 ||
                type.Element("settings") == null)
                errs.Add(string.Format("Line {0:D3}: exactly one 'settings' element required.", ((IXmlLineInfo)type).LineNumber));
            ValidateSettings(type.Element("settings"), errs);
        }
        void ValidateSettings(XElement settings, List<string> errs)
        {
            foreach (XAttribute attr in settings.Attributes())
            {
                if (attr.Name != "exclude" && attr.Name != "applytomembers")
                    errs.Add(string.Format("Line {0:D3}: 'exclude' or 'applytomembers' attribute required.", ((IXmlLineInfo)attr).LineNumber));
                if (attr.Value != "true" && attr.Value != "false")
                    errs.Add(string.Format("Line {0:D3}: boolean value required.", ((IXmlLineInfo)attr).LineNumber));
            }
            foreach (XElement cion in settings.Elements())
            {
                if (cion.Name != "confusion")
                    errs.Add(string.Format("Line {0:D3}: 'confusion' element required.", ((IXmlLineInfo)cion).LineNumber));
                if (cion.Attribute("id") == null)
                    errs.Add(string.Format("Line {0:D3}: id of confusion not specify.", ((IXmlLineInfo)cion).LineNumber));
                ValidateArguments(cion, errs);
            }
        }
        void ValidatePacker(XElement packer, List<string> errs)
        {
            if (packer.Attribute("id") == null)
                errs.Add(string.Format("Line {0:D3}: id of packer not specified.", ((IXmlLineInfo)packer).LineNumber));
            ValidateArguments(packer, errs);
        }
        void ValidateArguments(XElement parent, List<string> errs)
        {
            foreach (XElement arg in parent.Elements())
            {
                if (arg.Name != "argument")
                    errs.Add(string.Format("Line {0:D3}: 'argument' element required.", ((IXmlLineInfo)arg).LineNumber));
                if (arg.Attribute("name") == null ||
                    arg.Attribute("value") == null ||
                    arg.Attributes().Count() != 2)
                    errs.Add(string.Format("Line {0:D3}: exactly one 'name' and 'value' required.", ((IXmlLineInfo)arg).LineNumber));
            }
        }


        public override AssemblyDefinition[] ExtractDatas(string src)
        {
            List<AssemblyDefinition> ret = new List<AssemblyDefinition>();
            foreach (XElement element in xmlDoc.XPathSelectElements("configuration/assembly"))
            {
                string path = element.Attribute("path").Value;
                if (!Path.IsPathRooted(path))
                    path = new Uri(Path.Combine(Path.GetDirectoryName(xmlDoc.BaseUri), path)).LocalPath;
                AssemblyDefinition asmDef = AssemblyDefinition.ReadAssembly(path, new ReaderParameters(ReadingMode.Immediate));
                ((IAnnotationProvider)asmDef).Annotations.Add("Xml_Mark", element);
                ret.Add(asmDef);
                GlobalAssemblyResolver.Instance.AssemblyCache.Add(asmDef.FullName, asmDef);
            }
            return ret.ToArray();
        }
        public override void MarkAssembly(AssemblyDefinition asm, Preset preset, Core.Confuser cr)
        {
            XElement xAsm = (XElement)((IAnnotationProvider)asm).Annotations["Xml_Mark"];
            MarkSettings(xAsm.Element("settings"), asm);
            MarkPacker(xAsm.Element("packer"), asm);
            foreach (ModuleDefinition mod in asm.Modules)
            {
                XElement xMod = xAsm.XPathSelectElement("module[@name='" + mod.Name + "']");
                if (xMod != null)
                    MarkModule(xMod, mod);
            }

            base.MarkAssembly(asm, preset, cr);
        }
        void MarkModule(XElement xMod, ModuleDefinition mod)
        {
            MarkSettings(xMod.Element("settings"), mod);
            foreach (XElement xType in xMod.XPathSelectElements("type"))
            {
                TypeDefinition type = mod.GetType(xType.Attribute("name").Value);
                if (type == null)
                    WriteLineWithColor(ConsoleColor.Yellow, "Warning: Cannot find type '" + xType.Attribute("name").Value + "'.");
                else
                    MarkType(xType, type);
            }
        }
        void MarkType(XElement xType, TypeDefinition type)
        {
            MarkSettings(xType.Element("settings"), type);
        }
        void MarkSettings(XElement setting, ICustomAttributeProvider target)
        {
            if (target.HasCustomAttributes)
                for (int i = 0; i < target.CustomAttributes.Count; i++)
                    if (target.CustomAttributes[i].Constructor.DeclaringType.FullName == "ConfusingAttribute")
                    {
                        target.CustomAttributes.RemoveAt(i);
                        i--;
                    }
            if (setting == null) return;
            CustomAttribute attr = new CustomAttribute(new MethodReference(".ctor", new TypeReference("System", "Void", null), new TypeReference("", "ConfusingAttribute", null)));

            XAttribute xAttr;
            attr.Properties.Add(new CustomAttributeNamedArgument("StripAfterObfuscation", new CustomAttributeArgument(new TypeReference("System", "Boolean", null), true)));
            if ((xAttr = setting.Attribute("exclude")) != null)
                attr.Properties.Add(new CustomAttributeNamedArgument("Exclude", new CustomAttributeArgument(new TypeReference("System", "Boolean", null), bool.Parse(xAttr.Value))));
            if ((xAttr = setting.Attribute("applytomembers")) != null)
                attr.Properties.Add(new CustomAttributeNamedArgument("ApplyToMembers", new CustomAttributeArgument(new TypeReference("System", "Boolean", null), bool.Parse(xAttr.Value))));

            if (setting.HasElements)
            {
                StringBuilder cfg = new StringBuilder();
                foreach (XElement element in setting.Elements("confusion"))
                {
                    if (!Confusions.ContainsKey(element.Attribute("id").Value))
                        WriteLineWithColor(ConsoleColor.Yellow, string.Format("Warning: cannot find confusion with id '{0}'.", element.Attribute("id").Value));
                    else
                    {
                        if (element.PreviousNode == null)
                            cfg.Append("[");
                        else
                            cfg.Append("+[");
                        cfg.Append(element.Attribute("id").Value);
                        if (element.HasElements)
                        {
                            foreach (XElement arg in element.Elements("argument"))
                            {
                                cfg.Append(",");
                                cfg.Append(arg.Attribute("name").Value + "=" + arg.Attribute("value").Value);
                            }
                        }
                        cfg.Append("]");
                    }
                }
                attr.Properties.Add(new CustomAttributeNamedArgument("Config", new CustomAttributeArgument(new TypeReference("System", "String", null), cfg.ToString())));
            }

            target.CustomAttributes.Add(attr);
        }
        void MarkPacker(XElement packer, AssemblyDefinition asm)
        {
            if (asm.HasCustomAttributes)
                for (int i = 0; i < asm.CustomAttributes.Count; i++)
                    if (asm.CustomAttributes[i].Constructor.DeclaringType.FullName == "PackerAttribute")
                    {
                        asm.CustomAttributes.RemoveAt(i);
                        i--;
                    }
            if (packer == null) return;
            CustomAttribute attr = new CustomAttribute(new MethodReference(".ctor", new TypeReference("System", "Void", null), new TypeReference("", "PackerAttribute", null)));
            attr.Properties.Add(new CustomAttributeNamedArgument("StripAfterObfuscation", new CustomAttributeArgument(new TypeReference("System", "Boolean", null), true)));

            StringBuilder cfg = new StringBuilder();
            if (!Packers.ContainsKey(packer.Attribute("id").Value))
                WriteLineWithColor(ConsoleColor.Yellow, string.Format("Warning: cannot find packer with id '{0}'.", packer.Attribute("id").Value));
            else
            {
                cfg.Append(packer.Attribute("id").Value);
                if (packer.HasElements)
                {
                    cfg.Append(":");
                    foreach (XElement arg in packer.Elements("argument"))
                    {
                        if (arg.PreviousNode != null)
                            cfg.Append(",");
                        cfg.Append(arg.Attribute("name").Value + "=" + arg.Attribute("value").Value);
                    }
                }
                attr.Properties.Add(new CustomAttributeNamedArgument("Config", new CustomAttributeArgument(new TypeReference("System", "String", null), cfg.ToString())));
            }

            asm.CustomAttributes.Add(attr);
        }

    }
}
