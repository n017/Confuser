using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Mono.Cecil;

namespace Confuser.Core.Analyzers
{
    partial class NameAnalyzer
    {
        struct XmlNsDef
        {
            public string ClrNamespace;
            public AssemblyNameReference AssemblyName;
        }

        class XamlContext
        {
            public Dictionary<string, List<XmlNsDef>> namespaces = new Dictionary<string, List<XmlNsDef>>();
            public Dictionary<string, List<XmlNsDef>> uri2nsDef = new Dictionary<string, List<XmlNsDef>>();
        }

        void AnalyzeXaml(XDocument doc)
        {
            XamlContext txt = new XamlContext();
            foreach (var i in Assemblies)
            {
                foreach (var attr in i.CustomAttributes.Where(_ =>
                    _.AttributeType.FullName == "System.Windows.Markup.XmlnsDefinitionAttribute"
                    ))
                {
                    List<XmlNsDef> map;
                    if (!txt.uri2nsDef.TryGetValue((string)attr.ConstructorArguments[0].Value, out map))
                        map = txt.uri2nsDef[(string)attr.ConstructorArguments[0].Value] = new List<XmlNsDef>();

                    var asmNameProp = attr.Properties
                        .Cast<CustomAttributeNamedArgument?>()
                        .FirstOrDefault(_ => _.Value.Name == "AssemblyName");
                    map.Add(new XmlNsDef()
                    {
                        ClrNamespace = (string)attr.ConstructorArguments[1].Value,
                        AssemblyName = asmNameProp == null ? i.Name : AssemblyNameReference.Parse((string)asmNameProp.Value.Argument.Value)
                    });
                }
            }

            foreach (var i in doc.Elements())
                AnalyzeElement(i);
        }

        void AnalyzePropertyAttr(XAttribute attr)
        {
            //attr.lin
        }

        void AnalyzePropertyElem(XElement elem)
        {
        }

        void AnalyzeElement(XElement elem)
        {
            string xmlNs = elem.Name.NamespaceName;
            TypeDefinition typeDef;

            foreach (var i in elem.Attributes())
                AnalyzePropertyAttr(i);
            foreach (var i in elem.Elements())
            {
                if (i.Name.LocalName.Contains("."))
                    AnalyzePropertyElem(elem);
                else
                    AnalyzeElement(elem);
            }
        }
    }
}
