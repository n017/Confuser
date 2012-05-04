using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Collections.Specialized;
using System.Xml.Schema;

namespace Confuser.Core.Project
{
    public enum SettingItemAction
    {
        Add,
        Remove
    }
    public class SettingItem<T> : NameValueCollection
    {
        public string Id { get; set; }
        public SettingItemAction Action { get; set; }

        public XmlElement Save(XmlDocument xmlDoc)
        {
            XmlElement elem = xmlDoc.CreateElement(typeof(T) == typeof(Packer) ? "packer" : "confusion", ConfuserProject.Namespace);

            XmlAttribute idAttr = xmlDoc.CreateAttribute("id");
            idAttr.Value = Id;
            elem.Attributes.Append(idAttr);

            if (Action != SettingItemAction.Add)
            {
                XmlAttribute pAttr = xmlDoc.CreateAttribute("action");
                pAttr.Value = Action.ToString().ToLower();
                elem.Attributes.Append(pAttr);
            }

            foreach (var i in this.AllKeys)
            {
                XmlElement arg = xmlDoc.CreateElement("argument", ConfuserProject.Namespace);

                XmlAttribute nameAttr = xmlDoc.CreateAttribute("name");
                nameAttr.Value = i;
                arg.Attributes.Append(nameAttr);
                XmlAttribute valAttr = xmlDoc.CreateAttribute("value");
                valAttr.Value = base[i];
                arg.Attributes.Append(valAttr);

                elem.AppendChild(arg);
            }

            return elem;
        }

        public void Load(XmlElement elem)
        {
            this.Id = elem.Attributes["id"].Value;
            if (elem.Attributes["action"] != null)
                this.Action = (SettingItemAction)Enum.Parse(typeof(SettingItemAction), elem.Attributes["action"].Value, true);
            foreach (XmlElement i in elem.ChildNodes.OfType<XmlElement>())
                this.Add(i.Attributes["name"].Value, i.Attributes["value"].Value);
        }
    }
    public class ObfSettings : List<SettingItem<IConfusion>>
    {
        public string Name { get; set; }
        public Preset Preset { get; set; }

        public XmlElement Save(XmlDocument xmlDoc)
        {
            XmlElement elem = xmlDoc.CreateElement("settings", ConfuserProject.Namespace);

            XmlAttribute nAttr = xmlDoc.CreateAttribute("name");
            nAttr.Value = Name;
            elem.Attributes.Append(nAttr);

            if (Preset != Preset.None)
            {
                XmlAttribute pAttr = xmlDoc.CreateAttribute("preset");
                pAttr.Value = Preset.ToString().ToLower();
                elem.Attributes.Append(pAttr);
            }

            foreach (var i in this)
                elem.AppendChild(i.Save(xmlDoc));

            return elem;
        }

        public void Load(XmlElement elem)
        {
            this.Name = elem.Attributes["name"].Value;
            if (elem.Attributes["preset"] != null)
                this.Preset = (Preset)Enum.Parse(typeof(Preset), elem.Attributes["preset"].Value, true);
            foreach (XmlElement i in elem.ChildNodes.OfType<XmlElement>())
            {
                var x = new SettingItem<IConfusion>();
                x.Load(i);
                this.Add(x);
            }
        }

        public ObfSettings Clone()
        {
            ObfSettings ret = new ObfSettings();
            ret.Name = this.Name;
            foreach (var i in this)
            {
                var item = new SettingItem<IConfusion>();
                item.Id = i.Id;
                foreach (var j in i.AllKeys)
                    item.Add(j, i[j]);
                ret.Add(item);
            }
            return ret;
        }
    }
    public class ObfConfig
    {
        public string Id { get; set; }
        public bool ApplyToMembers { get; set; }
        public bool Inherit { get; set; }

        public XmlElement Save(XmlDocument xmlDoc)
        {
            XmlElement elem = xmlDoc.CreateElement("config", ConfuserProject.Namespace);

            XmlAttribute idAttr = xmlDoc.CreateAttribute("id");
            idAttr.Value = Id;
            elem.Attributes.Append(idAttr);

            if (ApplyToMembers != false)
            {
                XmlAttribute attr = xmlDoc.CreateAttribute("applytomembers");
                attr.Value = ApplyToMembers.ToString().ToLower();
                elem.Attributes.Append(attr);
            }

            if (Inherit != false)
            {
                XmlAttribute attr = xmlDoc.CreateAttribute("inherit");
                attr.Value = Inherit.ToString().ToLower();
                elem.Attributes.Append(attr);
            }

            return elem;
        }

        public void Load(XmlElement elem)
        {
            this.Id = elem.Attributes["id"].Value;
            if (elem.Attributes["applytomembers"] != null)
                this.ApplyToMembers = bool.Parse(elem.Attributes["applytomembers"].Value);
            if (elem.Attributes["inherit"] != null)
                this.Inherit = bool.Parse(elem.Attributes["inherit"].Value);
        }
    }

    public class ConfuserProject : List<ProjectAssembly>
    {
        public ConfuserProject()
        {
            Plugins = new List<string>();
            Settings = new List<ObfSettings>();
        }
        public IList<string> Plugins { get; private set; }
        public IList<ObfSettings> Settings { get; private set; }
        public string OutputPath { get; set; }
        public string SNKeyPath { get; set; }
        public Preset DefaultPreset { get; set; }
        public SettingItem<Packer> Packer { get; set; }

        public static readonly XmlSchema Schema = XmlSchema.Read(typeof(ConfuserProject).Assembly.GetManifestResourceStream("Confuser.Core.ConfuserPrj.xsd"), null);
        public const string Namespace = "http://confuser.codeplex.com";
        public XmlDocument Save()
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Schemas.Add(Schema);

            XmlElement elem = xmlDoc.CreateElement("project", Namespace);

            XmlAttribute outputAttr = xmlDoc.CreateAttribute("outputDir");
            outputAttr.Value = OutputPath;
            elem.Attributes.Append(outputAttr);

            XmlAttribute snAttr = xmlDoc.CreateAttribute("snKey");
            snAttr.Value = SNKeyPath;
            elem.Attributes.Append(snAttr);

            if (DefaultPreset != Preset.None)
            {
                XmlAttribute presetAttr = xmlDoc.CreateAttribute("preset");
                presetAttr.Value = DefaultPreset.ToString().ToLower();
                elem.Attributes.Append(presetAttr);
            }

            foreach (var i in Plugins)
            {
                XmlElement plug = xmlDoc.CreateElement("plugin", Namespace);

                XmlAttribute pathAttr = xmlDoc.CreateAttribute("path");
                pathAttr.Value = i;
                plug.Attributes.Append(pathAttr);

                elem.AppendChild(plug);
            }

            foreach (var i in Settings)
                elem.AppendChild(i.Save(xmlDoc));

            if (Packer != null)
                elem.AppendChild(Packer.Save(xmlDoc));

            foreach (var i in this)
                elem.AppendChild(i.Save(xmlDoc));

            xmlDoc.AppendChild(elem);
            return xmlDoc;
        }
        public void Load(XmlDocument doc)
        {
            doc.Schemas.Add(Schema);
            doc.Validate(null);

            XmlElement docElem = doc.DocumentElement;

            this.OutputPath = docElem.Attributes["outputDir"].Value;
            this.SNKeyPath = docElem.Attributes["snKey"].Value;
            if (docElem.Attributes["preset"] != null)
                this.DefaultPreset = (Preset)Enum.Parse(typeof(Preset), docElem.Attributes["preset"].Value, true);
            foreach (XmlElement i in docElem.ChildNodes)
            {
                if (i.Name == "plugin")
                {
                    Plugins.Add(i.Attributes["path"].Value);
                }
                else if (i.Name == "settings")
                {
                    ObfSettings settings = new ObfSettings();
                    settings.Load(i);
                    Settings.Add(settings);
                }
                else if (i.Name == "packer")
                {
                    Packer = new SettingItem<Packer>();
                    Packer.Load(i);
                }
                else
                {
                    ProjectAssembly asm = new ProjectAssembly();
                    asm.Load(i);
                    this.Add(asm);
                }
            }
        }
    }
}
