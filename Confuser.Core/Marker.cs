using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Mono.Cecil;
using Mono.Collections.Generic;
using System.Collections.Specialized;
using System.IO;

namespace Confuser.Core
{
    public struct AssemblyData
    {
        public AssemblyDefinition Assembly;
        public string TargetPath;
    }

    public interface IMarker
    {
        void Initalize(IConfusion[] cions);
        void MarkAssembly(AssemblyDefinition asm, Preset preset);
        AssemblyData[] ExtractDatas(string src, string dst);
    }

    class DefaultMarker : IMarker
    {
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

        Dictionary<string, IConfusion> cns;
        public void Initalize(IConfusion[] cions)
        {
            cns = new Dictionary<string, IConfusion>();
            foreach (IConfusion c in cions)
                cns.Add(c.ID, c);
        }

        private void FillPreset(Preset preset, Dictionary<IConfusion, NameValueCollection> cs)
        {
            foreach (IConfusion i in cns.Values)
                if (i.Preset <= preset && !cs.ContainsKey(i))
                    cs.Add(i, new NameValueCollection());
        }

        private bool ProcessAttribute(ICustomAttributeProvider provider, Settings setting)
        {
            CustomAttribute att = GetAttribute(provider.CustomAttributes);
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
            bool exclude = true;
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

            if (!exclude)
            {
                CustomAttributeNamedArgument featureArg = att.Properties.FirstOrDefault(arg => arg.Name == "Feature");
                string feature = "all";
                if (!featureArg.Equals(default(CustomAttributeNamedArgument)))
                    feature = (string)featureArg.Argument.Value;

                if (string.Equals(feature, "all", StringComparison.OrdinalIgnoreCase))
                    FillPreset(Preset.Maximum, setting.CurrentConfusions);
                else if (string.Equals(feature, "default", StringComparison.OrdinalIgnoreCase))
                    FillPreset(Preset.Normal, setting.CurrentConfusions);
                else
                    ProcessFeature(feature, setting.CurrentConfusions);
            }

            return exclude && applyToMembers;
        }
        private CustomAttribute GetAttribute(Collection<CustomAttribute> attributes)
        {
            return attributes.FirstOrDefault((att) => att.AttributeType.FullName == "System.Reflection.ObfuscationAttribute");
        }
        private void ProcessFeature(string cfg, Dictionary<IConfusion, NameValueCollection> cs)
        {
            if (string.Equals(cfg, "exclude", StringComparison.OrdinalIgnoreCase))
            {
                cs.Clear();
                return;
            }
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
                        if (id == "new")
                        {
                            cs.Clear();
                        }
                        else
                        {
                            IConfusion now = (from i in cs.Keys where i.ID == id select i).FirstOrDefault() ?? cns[id];
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

        public void MarkAssembly(AssemblyDefinition asm, Preset preset)
        {
            Settings setting = new Settings();
            bool exclude = ProcessAttribute(asm, setting);

            FillPreset(preset, setting.CurrentConfusions);

            (asm as IAnnotationProvider).Annotations["ConfusionSets"] = setting.CurrentConfusions;
            (asm as IAnnotationProvider).Annotations["GlobalParams"] = setting.CurrentConfusions;

            if (!exclude)
                foreach (ModuleDefinition mod in asm.Modules)
                    MarkModule(mod, setting);

            setting.LeaveLevel();
        }

        private void MarkModule(ModuleDefinition mod, Settings setting)
        {
            bool exclude = ProcessAttribute(mod, setting);

            if (!exclude)
                foreach (TypeDefinition type in mod.Types)
                    MarkType(type, setting);

            setting.LeaveLevel();
        }

        private void MarkType(TypeDefinition type, Settings setting)
        {
            bool exclude = ProcessAttribute(type, setting);

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

        private void MarkMember(IMemberDefinition mem, Settings setting, Target target)
        {
            if (target == Target.Methods && (mem as MethodDefinition).SemanticsAttributes != MethodSemanticsAttributes.None)
            {
                return;
            }

            bool exclude = ProcessAttribute(mem, setting);

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

        public AssemblyData[] ExtractDatas(string src, string dst)
        {
            AssemblyData ret = new AssemblyData();
            ret.Assembly = AssemblyDefinition.ReadAssembly(src, new ReaderParameters(ReadingMode.Immediate));
            ret.TargetPath = dst;
            return new AssemblyData[] { ret };
        }
    }
}
