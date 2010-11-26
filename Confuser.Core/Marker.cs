using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Mono.Cecil;
using Mono.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Xml;

namespace Confuser.Core
{
    public class Marker
    {
        public static readonly List<string> FrameworkAssemblies;
        static Marker()
        {
            FrameworkAssemblies = new List<string>();
            foreach (FileInfo file in Directory.GetParent(Environment.GetFolderPath(Environment.SpecialFolder.System)).GetDirectories("Microsoft.NET")[0].GetFiles("FrameworkList.xml", SearchOption.AllDirectories))
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(file.FullName);
                foreach (XmlNode xn in doc.SelectNodes("/FileList/File"))
                {
                    AssemblyNameReference an = new AssemblyNameReference(xn.Attributes["AssemblyName"].Value, new Version(xn.Attributes["Version"].Value));
                    byte[] tkn = new byte[8];
                    string tknStr = xn.Attributes["PublicKeyToken"].Value;
                    for (int i = 0; i < 8; i++)
                        tkn[i] = Convert.ToByte(tknStr.Substring(i * 2, 2), 16);
                    an.PublicKeyToken = tkn;
                    an.Culture = xn.Attributes["Culture"].Value;
                    FrameworkAssemblies.Add(an.FullName);
                }
            }
            foreach (string file in Directory.GetFiles(Directory.GetDirectories(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Reference Assemblies")[0], "FrameworkList.xml", SearchOption.AllDirectories))
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(file);
                foreach (XmlNode xn in doc.SelectNodes("/FileList/File"))
                {
                    AssemblyNameReference an = new AssemblyNameReference(xn.Attributes["AssemblyName"].Value, new Version(xn.Attributes["Version"].Value));
                    byte[] tkn = new byte[8];
                    string tknStr = xn.Attributes["PublicKeyToken"].Value;
                    for (int i = 0; i < 8; i++)
                        tkn[i] = Convert.ToByte(tknStr.Substring(i * 2, 2), 16);
                    an.PublicKeyToken = tkn;
                    an.Culture = xn.Attributes["Culture"].Value;
                    FrameworkAssemblies.Add(an.FullName);
                }
            }
        }

        class Settings
        {
            public Settings()
            {
                inheritStack = new Stack<Dictionary<IConfusion, NameValueCollection>>();
                StartLevel();
            }

            Stack<Dictionary<IConfusion, NameValueCollection>> inheritStack;
            public Dictionary<IConfusion, NameValueCollection> CurrentConfusions;

            public void StartLevel()
            {
                if (inheritStack.Count > 0)
                    CurrentConfusions = new Dictionary<IConfusion, NameValueCollection>(inheritStack.Peek());
                else
                    CurrentConfusions = new Dictionary<IConfusion, NameValueCollection>();
                inheritStack.Push(CurrentConfusions);
            }
            public void LeaveLevel()
            {
                inheritStack.Pop();
            }
            public void SkipLevel()
            {
                if (inheritStack.Count > 1)
                    CurrentConfusions = new Dictionary<IConfusion, NameValueCollection>(inheritStack.ToArray()[inheritStack.Count - 2]);
                else
                    CurrentConfusions = new Dictionary<IConfusion, NameValueCollection>();
                inheritStack.Push(CurrentConfusions);
            }
        }

        protected IDictionary<string, IConfusion> Confusions;
        protected IDictionary<string, Packer> Packers;
        public virtual void Initalize(IConfusion[] cions, Packer[] packs)
        {
            Confusions = new Dictionary<string, IConfusion>();
            foreach (IConfusion c in cions)
                Confusions.Add(c.ID, c);
            Packers = new Dictionary<string, Packer>();
            foreach (Packer pack in packs)
                Packers.Add(pack.ID, pack);
        }
        private void FillPreset(Preset preset, Dictionary<IConfusion, NameValueCollection> cs)
        {
            foreach (IConfusion i in Confusions.Values)
                if (i.Preset <= preset && !cs.ContainsKey(i))
                    cs.Add(i, new NameValueCollection());
        }

        Confuser cr;
        private bool ProcessAttribute(ICustomAttributeProvider provider, Settings setting)
        {
            CustomAttribute att = GetAttribute(provider.CustomAttributes, "ConfusingAttribute");
            if (att == null)
            {
                setting.StartLevel();
                return false;
            }

            CustomAttributeNamedArgument stripArg = att.Properties.FirstOrDefault(arg => arg.Name == "StripAfterObfuscation");
            bool strip = true;
            if (!stripArg.Equals(default(CustomAttributeNamedArgument)))
                strip = (bool)stripArg.Argument.Value;

            if (strip)
                provider.CustomAttributes.Remove(att);

            CustomAttributeNamedArgument excludeArg = att.Properties.FirstOrDefault(arg => arg.Name == "Exclude");
            bool exclude = false;
            if (!excludeArg.Equals(default(CustomAttributeNamedArgument)))
                exclude = (bool)excludeArg.Argument.Value;

            if (exclude)
                setting.CurrentConfusions.Clear();

            CustomAttributeNamedArgument applyToMembersArg = att.Properties.FirstOrDefault(arg => arg.Name == "ApplyToMembers");
            bool applyToMembers = true;
            if (!applyToMembersArg.Equals(default(CustomAttributeNamedArgument)))
                applyToMembers = (bool)applyToMembersArg.Argument.Value;

            if (applyToMembers)
                setting.StartLevel();
            else
                setting.SkipLevel();
            try
            {
                if (!exclude)
                {
                    CustomAttributeNamedArgument featureArg = att.Properties.FirstOrDefault(arg => arg.Name == "Config");
                    string feature = "all";
                    if (!featureArg.Equals(default(CustomAttributeNamedArgument)))
                        feature = (string)featureArg.Argument.Value;

                    if (string.Equals(feature, "all", StringComparison.OrdinalIgnoreCase))
                        FillPreset(Preset.Maximum, setting.CurrentConfusions);
                    else if (string.Equals(feature, "default", StringComparison.OrdinalIgnoreCase))
                        FillPreset(Preset.Normal, setting.CurrentConfusions);
                    else
                        ProcessConfig(feature, setting.CurrentConfusions);
                }

                return exclude && applyToMembers;
            }
            catch
            {
                cr.Log("Warning: Cannot process ConfusingAttribute at '" + provider.ToString() + "'. ConfusingAttribute ignored.");
                return false;
            }
        }
        private CustomAttribute GetAttribute(Collection<CustomAttribute> attributes, string name)
        {
            return attributes.FirstOrDefault((att) => att.AttributeType.FullName == name);
        }
        private void ProcessConfig(string cfg, Dictionary<IConfusion, NameValueCollection> cs)
        {
            if (string.Equals(cfg, "exclude", StringComparison.OrdinalIgnoreCase))
            {
                cs.Clear();
                return;
            }
            if (cfg.StartsWith("packer:")) return;
            MatchCollection matches = Regex.Matches(cfg, @"(\+|\-|)\[([^,\]]*)(?:,([^\]]*))?\]");
            foreach (Match match in matches)
            {
                string id = match.Groups[2].Value.ToLower();
                switch (match.Groups[1].Value)
                {
                    case null:
                    case "":
                    case "+":
                        if (id == "preset")
                        {
                            FillPreset((Preset)Enum.Parse(typeof(Preset), match.Groups[3].Value, true), cs);
                        }
                        else if (id == "new")
                        {
                            cs.Clear();
                        }
                        else
                        {
                            if (!Confusions.ContainsKey(id))
                            {
                                cr.Log("Warning: Cannot find confusion id '" + id + "'.");
                                break;
                            }
                            IConfusion now = (from i in cs.Keys where i.ID == id select i).FirstOrDefault() ?? Confusions[id];
                            if (!cs.ContainsKey(now)) cs[now] = new NameValueCollection();
                            NameValueCollection nv = cs[now];
                            if (!string.IsNullOrEmpty(match.Groups[3].Value))
                            {
                                foreach (string param in match.Groups[3].Value.Split(','))
                                {
                                    string[] p = param.Split('=');
                                    nv[p[0].ToLower()] = p[1];
                                }
                            }
                        }
                        break;
                    case "-":
                        cs.Remove((from i in cs.Keys where i.ID == id select i).FirstOrDefault());
                        break;
                }
            }
        }
        private void ProcessPackers(ICustomAttributeProvider provider, out NameValueCollection param, out Packer packer)
        {
            CustomAttribute attr = GetAttribute(provider.CustomAttributes, "PackerAttribute");

            if (attr == null) { param = null; packer = null; return; }
            CustomAttributeNamedArgument stripArg = attr.Properties.FirstOrDefault(arg => arg.Name == "StripAfterObfuscation");
            bool strip = true;
            if (!stripArg.Equals(default(CustomAttributeNamedArgument)))
                strip = (bool)stripArg.Argument.Value;

            if (strip)
                provider.CustomAttributes.Remove(attr);

            CustomAttributeNamedArgument cfgArg = attr.Properties.FirstOrDefault(arg => arg.Name == "Config");
            string cfg = "";
            if (!cfgArg.Equals(default(CustomAttributeNamedArgument)))
                cfg = (string)cfgArg.Argument.Value;
            if (string.IsNullOrEmpty(cfg)) { param = null; packer = null; return; }

            param = new NameValueCollection();

            Match match = Regex.Match(cfg, @"([^:]*):?(?:([^=]*=[^,]*),?)*");
            packer = Packers[match.Groups[1].Value];
            foreach (Capture arg in match.Groups[2].Captures)
            {
                string[] args = arg.Value.Split('=');
                param.Add(args[0], args[1]);
            }
        }

        public virtual void MarkAssembly(AssemblyDefinition asm, Preset preset, Confuser cr)
        {
            this.cr = cr;
            Settings setting = new Settings();
            FillPreset(preset, setting.CurrentConfusions);
            bool exclude = ProcessAttribute(asm, setting);

            (asm as IAnnotationProvider).Annotations["ConfusionSets"] = setting.CurrentConfusions;
            (asm as IAnnotationProvider).Annotations["GlobalParams"] = setting.CurrentConfusions;

            NameValueCollection param;
            Packer packer;
            ProcessPackers(asm, out param, out packer);
            (asm as IAnnotationProvider).Annotations["Packer"] = packer;
            (asm as IAnnotationProvider).Annotations["PackerParams"] = param;

            if (!exclude)
                foreach (ModuleDefinition mod in asm.Modules)
                    MarkModule(mod, setting);

            setting.LeaveLevel();
        }

        private void MarkModule(ModuleDefinition mod, Settings setting)
        {
            bool exclude = ProcessAttribute(mod, setting);
            MarkModule(mod, setting.CurrentConfusions, cr);

            (mod as IAnnotationProvider).Annotations["ConfusionSets"] = setting.CurrentConfusions;

            if (!exclude)
                foreach (TypeDefinition type in mod.Types)
                    MarkType(type, setting);

            setting.LeaveLevel();
        }
        protected virtual void MarkModule(ModuleDefinition mod, IDictionary<IConfusion, NameValueCollection> current, Confuser cr) { }

        private void MarkType(TypeDefinition type, Settings setting)
        {
            bool exclude = ProcessAttribute(type, setting);
            MarkType(type, setting.CurrentConfusions, cr);

            (type as IAnnotationProvider).Annotations["ConfusionSets"] = setting.CurrentConfusions;


            if (!exclude)
            {
                foreach (TypeDefinition nType in type.NestedTypes)
                    MarkType(nType, setting);

                foreach (MethodDefinition mtd in type.Methods)
                    MarkMember(mtd, setting, Target.Methods);

                foreach (FieldDefinition fld in type.Fields)
                    MarkMember(fld, setting, Target.Fields);

                foreach (PropertyDefinition prop in type.Properties)
                    MarkMember(prop, setting, Target.Properties);

                foreach (EventDefinition evt in type.Events)
                    MarkMember(evt, setting, Target.Events);
            }

            setting.LeaveLevel();
        }
        protected virtual void MarkType(TypeDefinition type, IDictionary<IConfusion, NameValueCollection> current, Confuser cr) { }

        private void MarkMember(IMemberDefinition mem, Settings setting, Target target)
        {
            if (target == Target.Methods && (mem as MethodDefinition).SemanticsAttributes != MethodSemanticsAttributes.None)
            {
                return;
            }

            bool exclude = ProcessAttribute(mem, setting);
            MarkMember(mem, setting.CurrentConfusions, cr);

            (mem as IAnnotationProvider).Annotations["ConfusionSets"] = setting.CurrentConfusions;

            if (!exclude)
                if (target == Target.Properties)
                {
                    PropertyDefinition prop = mem as PropertyDefinition;
                    List<MethodDefinition> sems = new List<MethodDefinition>();
                    if (prop.GetMethod != null)
                        sems.Add(prop.GetMethod);
                    if (prop.SetMethod != null)
                        sems.Add(prop.SetMethod);
                    if (prop.HasOtherMethods)
                        sems.AddRange(prop.OtherMethods);
                    foreach (MethodDefinition mtd in sems)
                    {
                        setting.StartLevel();

                        ProcessAttribute(mtd, setting);

                        (mtd as IAnnotationProvider).Annotations["ConfusionSets"] = setting.CurrentConfusions;

                        setting.LeaveLevel();
                    }
                }
                else if (target == Target.Events)
                {
                    EventDefinition evt = mem as EventDefinition;
                    List<MethodDefinition> sems = new List<MethodDefinition>();
                    if (evt.AddMethod != null)
                        sems.Add(evt.AddMethod);
                    if (evt.RemoveMethod != null)
                        sems.Add(evt.RemoveMethod);
                    if (evt.InvokeMethod != null)
                        sems.Add(evt.InvokeMethod);
                    if (evt.HasOtherMethods)
                        sems.AddRange(evt.OtherMethods);
                    foreach (MethodDefinition mtd in sems)
                    {
                        ProcessAttribute(mtd, setting);

                        (mtd as IAnnotationProvider).Annotations["ConfusionSets"] = setting.CurrentConfusions;

                        setting.LeaveLevel();
                    }
                }

            setting.LeaveLevel();
        }
        protected virtual void MarkMember(IMemberDefinition mem, IDictionary<IConfusion, NameValueCollection> current, Confuser cr) { }


        public virtual AssemblyDefinition[] ExtractDatas(string src)
        {
            Dictionary<string, AssemblyDefinition> ret = new Dictionary<string, AssemblyDefinition>();
            AssemblyDefinition asmDef = AssemblyDefinition.ReadAssembly(src);
            GlobalAssemblyResolver.Instance.AssemblyCache.Add(asmDef.FullName,asmDef);
            ret.Add(asmDef.FullName, asmDef);

            foreach (ModuleDefinition mod in asmDef.Modules)
            {
                mod.FullLoad();
                foreach (AssemblyNameReference refer in mod.AssemblyReferences)
                {
                    AssemblyDefinition asm = GlobalAssemblyResolver.Instance.Resolve(refer);
                    if (!FrameworkAssemblies.Contains(refer.FullName) && !ret.ContainsKey(asm.FullName))
                    {
                        ret.Add(asm.FullName, asm);
                        ExtractData(asm, ret);
                    }
                }
            }
            return ret.Values.ToArray();
        }
        void ExtractData(AssemblyDefinition asm, Dictionary<string, AssemblyDefinition> ret)
        {
            foreach (ModuleDefinition mod in asm.Modules)
            {
                mod.FullLoad();
                foreach (AssemblyNameReference refer in mod.AssemblyReferences)
                {
                    AssemblyDefinition asmDef = GlobalAssemblyResolver.Instance.Resolve(refer);
                    if (!FrameworkAssemblies.Contains(refer.FullName) && !ret.ContainsKey(asmDef.FullName))
                    {
                        ret.Add(asmDef.FullName, asmDef);
                        ExtractData(asmDef, ret);
                    }
                }
            }
        }

        public virtual string GetDestinationPath(ModuleDefinition mod, string dstPath)
        {
            return Path.Combine(dstPath, Path.GetFileName(mod.FullyQualifiedName));
        }
    }
}
