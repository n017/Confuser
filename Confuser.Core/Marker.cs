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
                if (inheritStack.Count != 0)
                    CurrentConfusions = new List<ConfusionSet>(inheritStack.Peek());
                else
                    CurrentConfusions = new List<ConfusionSet>();
                inheritStack.Push(CurrentConfusions);
            }
            public void LeaveLevel()
            {
                inheritStack.Pop();
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
                if (i.Preset <= preset)
                    cs.Add(new ConfusionSet() { Confusion = i });
        }
        private CustomAttribute GetAttribute(Collection<CustomAttribute> attributes)
        {
            return attributes.FirstOrDefault((att) => att.AttributeType.FullName == "ConfuseAttribute");
        }
        private void ProcessConfig(string cfg, List<ConfusionSet> cs)
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
            setting.StartLevel();
            FillPreset(preset, setting.CurrentConfusions);

            CustomAttribute att = GetAttribute(asm.CustomAttributes);
            if (att != null)
            {
                string cfg = att.Properties.FirstOrDefault(arg => arg.Name == "Configuration").Argument.Value as string;
                ProcessConfig(cfg, setting.CurrentConfusions);
            }

            List<ConfusionSet> now = new List<ConfusionSet>();
            foreach (ConfusionSet set in setting.CurrentConfusions)
                if ((set.Confusion.Target & Target.Assembly) == Target.Assembly)
                    now.Add(set);
            (asm as IAnnotationProvider).Annotations["ConfusionSets"] = now;

            foreach (ModuleDefinition mod in asm.Modules)
                MarkModule(mod, setting);

            setting.LeaveLevel();
        }

        private void MarkModule(ModuleDefinition mod, Settings setting)
        {
            setting.StartLevel();

            CustomAttribute att = GetAttribute(mod.CustomAttributes);
            if (att != null)
            {
                string cfg = att.Properties.FirstOrDefault(arg => arg.Name == "Configuration").Argument.Value as string;
                ProcessConfig(cfg, setting.CurrentConfusions);
            }

            foreach (TypeDefinition type in mod.Types)
                MarkType(type, setting);

            setting.LeaveLevel();
        }

        private void MarkType(TypeDefinition type, Settings setting)
        {
            setting.StartLevel();

            CustomAttribute att = GetAttribute(type.CustomAttributes);
            if (att != null)
            {
                string cfg = att.Properties.FirstOrDefault(arg => arg.Name == "Configuration").Argument.Value as string;
                ProcessConfig(cfg, setting.CurrentConfusions);
            }

            List<ConfusionSet> now = new List<ConfusionSet>();
            foreach (ConfusionSet set in setting.CurrentConfusions)
                if ((set.Confusion.Target & Target.Types) == Target.Types)
                    now.Add(set);
            (type as IAnnotationProvider).Annotations["ConfusionSets"] = now;

            foreach (MethodDefinition mtd in type.Methods)
                MarkMember(mtd, setting, Target.Methods);

            foreach (FieldDefinition fld in type.Fields)
                MarkMember(fld, setting, Target.Fields);

            foreach (PropertyDefinition prop in type.Properties)
                MarkMember(prop, setting, Target.Properties);

            foreach (EventDefinition evt in type.Events)
                MarkMember(evt, setting, Target.Events);

            setting.LeaveLevel();
        }

        private void MarkMember(IMemberDefinition mem, Settings setting, Target target)
        {
            setting.StartLevel();

            if (target == Target.Methods && (mem as MethodDefinition).SemanticsAttributes != MethodSemanticsAttributes.None)
            {
                return;
            }

            CustomAttribute att = GetAttribute(mem.CustomAttributes);
            if (att != null)
            {
                string cfg = att.Properties.FirstOrDefault(arg => arg.Name == "Configuration").Argument.Value as string;
                ProcessConfig(cfg, setting.CurrentConfusions);
            }

            List<ConfusionSet> now = new List<ConfusionSet>();
            foreach (ConfusionSet set in setting.CurrentConfusions)
                if ((set.Confusion.Target & target) == target)
                    now.Add(set);
            (mem as IAnnotationProvider).Annotations["ConfusionSets"] = now;

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

                    CustomAttribute att1 = GetAttribute(mtd.CustomAttributes);
                    if (att1 != null)
                    {
                        string cfg1 = att1.Properties.FirstOrDefault(arg => arg.Name == "Configuration").Argument.Value as string;
                        ProcessConfig(cfg1, setting.CurrentConfusions);
                    }

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
                    setting.StartLevel();

                    CustomAttribute att1 = GetAttribute(mtd.CustomAttributes);
                    if (att1 != null)
                    {
                        string cfg1 = att1.Properties.FirstOrDefault(arg => arg.Name == "Configuration").Argument.Value as string;
                        ProcessConfig(cfg1, setting.CurrentConfusions);
                    }

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
