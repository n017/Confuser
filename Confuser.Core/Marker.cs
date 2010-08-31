using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Mono.Cecil;
using Mono.Collections.Generic;
using System.Collections.Specialized;

namespace Confuser.Core
{
    class ConfusionSet
    {
        IConfusion confusion;
        NameValueCollection parameter = new NameValueCollection();
        public IConfusion Confusion { get { return confusion; } set { confusion = value; } }
        public NameValueCollection Parameters { get { return parameter; } }
    }
    class Marker
    {
        class Settings
        {
            public Settings()
            {
                inheritStack = new Stack<List<ConfusionSet>>();
                StartLevel();
            }

            Stack<List<ConfusionSet>> inheritStack;
            public List<ConfusionSet> CurrentConfusions;

            public void StartLevel()
            {
                if (inheritStack.Count > 0)
                    CurrentConfusions = new List<ConfusionSet>(inheritStack.Peek());
                else
                    CurrentConfusions = new List<ConfusionSet>();
                inheritStack.Push(CurrentConfusions);
            }
            public void LeaveLevel()
            {
                inheritStack.Pop();
            }
            public void SkipLevel()
            {
                if (inheritStack.Count > 1)
                    CurrentConfusions = new List<ConfusionSet>(inheritStack.ToArray()[inheritStack.Count - 2]);
                else
                    CurrentConfusions = new List<ConfusionSet>();
                inheritStack.Push(CurrentConfusions);
            }
        }

        Dictionary<string, IConfusion> cns;
        public Marker(IConfusion[] cions)
        {
            cns = new Dictionary<string, IConfusion>();
            foreach (IConfusion c in cions)
                cns.Add(c.ID, c);
        }

        private void FillPreset(Preset preset, List<ConfusionSet> cs)
        {
            foreach (IConfusion i in cns.Values)
                if (i.Preset <= preset && (from ii in cs where ii.Confusion == i select ii).Count() == 0)
                    cs.Add(new ConfusionSet() { Confusion = i });
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

                if (feature == "all")
                    FillPreset(Preset.Maximum, setting.CurrentConfusions);
                else if (feature == "default")
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
        private void ProcessFeature(string cfg, List<ConfusionSet> cs)
        {
            if (cfg == "exclude")
            {
                cs.Clear();
                return;
            }
            MatchCollection matches = Regex.Matches(cfg, @"(\+|\-|)\[([^,\]]*)(?:,([^\]]*))?\]");
            foreach (Match match in matches)
            {
                string id = match.Groups[2].Value;
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
                            ConfusionSet now = new ConfusionSet();
                            foreach (ConfusionSet i in cs)
                                if (i.Confusion.ID == id)
                                {
                                    now = i;
                                    break;
                                }
                            if (now.Confusion == null) now.Confusion = cns[id];
                            if (!string.IsNullOrEmpty(match.Groups[3].Value))
                            {
                                foreach (string param in match.Groups[3].Value.Split(','))
                                {
                                    string[] p = param.Split('=');
                                    now.Parameters[p[0]] = p[1];
                                }
                            }
                            if (!cs.Contains(now)) cs.Add(now);
                        }
                        break;
                    case "-":
                        foreach (ConfusionSet i in cs)
                            if (i.Confusion.ID == id)
                            {
                                cs.Remove(i); 
                                break;
                            }
                        break;
                }
            }
        }

        public void MarkAssembly(AssemblyDefinition asm, Preset preset)
        {
            Settings setting = new Settings();
            bool exclude = ProcessAttribute(asm, setting);

            FillPreset(preset, setting.CurrentConfusions);

            List<ConfusionSet> now = new List<ConfusionSet>();
            foreach (ConfusionSet set in setting.CurrentConfusions)
                if ((set.Confusion.Target & Target.Assembly) == Target.Assembly)
                    now.Add(set);
            (asm as IAnnotationProvider).Annotations["ConfusionSets"] = now;

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

            List<ConfusionSet> now = new List<ConfusionSet>();
            foreach (ConfusionSet set in setting.CurrentConfusions)
                if ((set.Confusion.Target & Target.Types) == Target.Types)
                    now.Add(set);
            (type as IAnnotationProvider).Annotations["ConfusionSets"] = now;


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

            List<ConfusionSet> now = new List<ConfusionSet>();
            foreach (ConfusionSet set in setting.CurrentConfusions)
                if ((set.Confusion.Target & target) == target)
                    now.Add(set);
            (mem as IAnnotationProvider).Annotations["ConfusionSets"] = now;

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

                        List<ConfusionSet> now1 = new List<ConfusionSet>();
                        foreach (ConfusionSet set in setting.CurrentConfusions)
                            if ((set.Confusion.Target & Target.Methods) == Target.Methods)
                                now1.Add(set);
                        (mtd as IAnnotationProvider).Annotations["ConfusionSets"] = now1;

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

                        List<ConfusionSet> now1 = new List<ConfusionSet>();
                        foreach (ConfusionSet set in setting.CurrentConfusions)
                            if ((set.Confusion.Target & Target.Methods) == Target.Methods)
                                now1.Add(set);
                        (mtd as IAnnotationProvider).Annotations["ConfusionSets"] = now1;

                        setting.LeaveLevel();
                    }
                }

            setting.LeaveLevel();
        }
    }
}
