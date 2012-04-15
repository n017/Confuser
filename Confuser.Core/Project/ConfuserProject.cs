using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

namespace Confuser.Core.Project
{
    class Settings<T> : List<KeyValuePair<string, string>>
    {
        public string Id { get; set; }

        public XmlElement Save(XmlDocument xmlDoc)
        {
            XmlElement elem = xmlDoc.CreateElement(typeof(T) == typeof(Packer) ? "packer" : "confusion");

            XmlAttribute idAttr = xmlDoc.CreateAttribute("id");
            idAttr.Value = Id;
            elem.Attributes.Append(idAttr);

            foreach (var i in this)
            {
                XmlElement arg = xmlDoc.CreateElement("argument");

                XmlAttribute nameAttr = xmlDoc.CreateAttribute("name");
                nameAttr.Value = i.Key;
                arg.Attributes.Append(nameAttr);
                XmlAttribute valAttr = xmlDoc.CreateAttribute("value");
                valAttr.Value = i.Value;
                arg.Attributes.Append(valAttr);

                elem.AppendChild(arg);
            }

            return elem;
        }
    }
    class ObfuscationSettings : List<Settings<IConfusion>>
    {
        public bool Exclude { get; set; }
        public bool Inherit { get; set; }

        public XmlElement Save(XmlDocument xmlDoc)
        {
            XmlElement elem = xmlDoc.CreateElement("settings");

            if (Exclude != false)
            {
                XmlAttribute attr = xmlDoc.CreateAttribute("exclude");
                attr.Value = Exclude.ToString().ToLower();
                elem.Attributes.Append(attr);
            }
            if (Inherit != true)
            {
                XmlAttribute attr = xmlDoc.CreateAttribute("inherit");
                attr.Value = Inherit.ToString().ToLower();
                elem.Attributes.Append(attr);
            }

            foreach (var i in this)
                elem.AppendChild(i.Save(xmlDoc));

            return elem;
        }
    }

    class ConfuserProject : List<ProjectAssembly>
    {
        public ConfuserProject()
        {
            Plugins = new List<string>();
        }
        public IList<string> Plugins { get; private set; }

        public XmlDocument Save()
        {
            XmlDocument xmlDoc = new XmlDocument();

            XmlElement elem = xmlDoc.CreateElement("project");

            foreach (var i in Plugins)
            {
                XmlElement plug = xmlDoc.CreateElement("plugin");

                XmlAttribute pathAttr = xmlDoc.CreateAttribute("path");
                pathAttr.Value = i;
                plug.Attributes.Append(pathAttr);

                elem.AppendChild(plug);
            }

            foreach (var i in this)
                elem.AppendChild(i.Save(xmlDoc));

            xmlDoc.AppendChild(elem);
            return xmlDoc;
        }
    }
}
