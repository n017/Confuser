using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Mono.Cecil;
using Mono.Cecil.Rocks;
using Mono.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Xml;
using Confuser.Core.Project;
using System.Collections;

namespace Confuser.Core
{
    public class ObfuscationSettings : Dictionary<IConfusion, NameValueCollection>
    {
        public ObfuscationSettings() { }
        public ObfuscationSettings(ObfuscationSettings settings)
        {
            foreach (var i in settings) Add(i.Key, new NameValueCollection(i.Value));
        }

        public bool IsEmpty() { return this.Count == 0; }
    }
    public struct MarkerSetting
    {
        public Packer Packer;
        public NameValueCollection PackerParameters;
        public AssemblySetting[] Assemblies;
    }
    public struct AssemblySetting
    {
        public AssemblySetting(AssemblyDefinition asm)
        {
            this.Assembly = asm;
            GlobalParameters = null;
            Modules = Mono.Empty<ModuleSetting>.Array;
            ApplyToMember = IsMain = false;
        }

        public AssemblyDefinition Assembly;
        public ObfuscationSettings GlobalParameters;
        public ModuleSetting[] Modules;
        public bool IsMain;
        public bool ApplyToMember;
    }
    public struct ModuleSetting
    {
        public ModuleSetting(ModuleDefinition mod)
        {
            this.Module = mod;
            Parameters = null;
            Members = Mono.Empty<MemberSetting>.Array;
            ApplyToMember = false;
        }

        public ModuleDefinition Module;
        public ObfuscationSettings Parameters;
        public MemberSetting[] Members;
        public bool ApplyToMember;
    }
    public struct MemberSetting
    {
        public MemberSetting(IMemberDefinition obj)
        {
            this.Object = obj;
            Parameters = null;
            Members = Mono.Empty<MemberSetting>.Array;
            ApplyToMember = false;
        }
        public IMemberDefinition Object;
        public ObfuscationSettings Parameters;
        public MemberSetting[] Members;
        public bool ApplyToMember;
    }

    public class Marker
    {
        public class Marking
        {
            public Marking()
            {
                inheritStack = new Tuple<ObfuscationSettings, bool>[0x10];
                count = 0;
                CurrentConfusions = new ObfuscationSettings();
                ApplyToMember = false;
            }

            Tuple<ObfuscationSettings, bool>[] inheritStack;
            int count;
            public ObfuscationSettings CurrentConfusions { get; private set; }
            public bool ApplyToMember { get; set; }

            struct LevelHolder : IDisposable
            {
                Marking m;
                public LevelHolder(Marking m)
                {
                    m.inheritStack[m.count] = new Tuple<ObfuscationSettings, bool>(m.CurrentConfusions, m.ApplyToMember);
                    m.count++;
                    if (m.count > 0)
                    {
                        int i = m.count - 1;
                        while (i > 0)
                        {
                            if (m.inheritStack[i].Item2)    //ApplyToMember
                                break;
                            i--;
                        }
                        m.CurrentConfusions = new ObfuscationSettings(m.inheritStack[i].Item1);
                    }
                    else
                        m.CurrentConfusions = new ObfuscationSettings();
                    m.ApplyToMember = false;

                    this.m = m;
                }

                public void Dispose()
                {
                    m.count--;
                    m.inheritStack[m.count] = default(Tuple<ObfuscationSettings, bool>);
                    if (m.count > 0)
                    {
                        m.CurrentConfusions = m.inheritStack[m.count - 1].Item1;
                        m.ApplyToMember = m.inheritStack[m.count - 1].Item2;
                    }
                }
            }

            public IDisposable Level()
            {
                return new LevelHolder(this);
            }
        }

        protected IDictionary<string, IConfusion> Confusions;
        protected IDictionary<string, Packer> Packers;
        public virtual void Initalize(IList<IConfusion> cions, IList<Packer> packs)
        {
            Confusions = new Dictionary<string, IConfusion>();
            foreach (IConfusion c in cions)
                Confusions.Add(c.ID, c);
            Packers = new Dictionary<string, Packer>();
            foreach (Packer pack in packs)
                Packers.Add(pack.ID, pack);
        }
        private void FillPreset(Preset preset, ObfuscationSettings cs)
        {
            foreach (IConfusion i in Confusions.Values)
                if (i.Preset <= preset && !cs.ContainsKey(i))
                    cs.Add(i, new NameValueCollection());
        }
        static NameValueCollection Clone(NameValueCollection src)
        {
            NameValueCollection ret = new NameValueCollection();
            foreach (var i in src.AllKeys)
                ret.Add(i.ToLowerInvariant(), src[i]);
            return ret;
        }

        Confuser cr;
        protected Confuser Confuser { get { return cr; } set { cr = value; } }
        ConfuserProject proj;
        public virtual MarkerSetting MarkAssemblies(Confuser cr, EventHandler<LogEventArgs> err)
        {
            this.cr = cr;
            this.proj = cr.param.Project;
            MarkerSetting ret = new MarkerSetting();
            ret.Assemblies = new AssemblySetting[proj.Count];

            Marking setting = new Marking();
            FillPreset(proj.DefaultPreset, setting.CurrentConfusions);
            setting.ApplyToMember = true;
            using (setting.Level())
            {
                for (int i = 0; i < proj.Count; i++)
                    using (setting.Level())
                        ret.Assemblies[i] = MarkAssembly(proj[i], setting);
                if (proj.Packer != null)
                {
                    ret.Packer = Packers[proj.Packer.Id];
                    ret.PackerParameters = Clone(proj.Packer);
                }
            }

            return ret;
        }

        internal void MarkHelperAssembly(AssemblyDefinition asm, ObfuscationSettings settings, Confuser cr)
        {
            AssemblySetting ret = new AssemblySetting(asm);
            ret.GlobalParameters = new ObfuscationSettings(settings);
            ret.ApplyToMember = true;

            cr.settings.Add(ret);
        }
        private AssemblySetting MarkAssembly(ProjectAssembly asm, Marking mark)
        {
            bool applyToMember;
            AssemblySetting ret = MarkAssembly(asm, mark, out applyToMember);
            ret.GlobalParameters = mark.CurrentConfusions;
            mark.ApplyToMember = applyToMember;

            using (mark.Level())
            {
                List<ModuleSetting> modSettings = new List<ModuleSetting>();
                foreach (var m in ret.Assembly.Modules)
                    using (mark.Level())
                    {
                        ProjectModule mod = asm.SingleOrDefault(_ => _.Name == m.Name) ??
                                            new ProjectModule() { Name = m.Name };
                        MarkModule(ret, mod, mark, modSettings);
                    }
                ret.Modules = modSettings.ToArray();
                return ret;
            }
        }
        protected virtual AssemblySetting MarkAssembly(ProjectAssembly asm, Marking mark, out bool applyToMember)
        {
            AssemblySetting ret = new AssemblySetting(asm.Resolve());
            ret.ApplyToMember = applyToMember = false;
            ret.IsMain = asm.IsMain;
            if (asm.Config != null)
            {
                ret.ApplyToMember = applyToMember = asm.Config.ApplyToMembers;
                mark.CurrentConfusions.Clear();
                foreach (var i in proj.Settings.Single(_ => _.Name == asm.Config.Id))
                    mark.CurrentConfusions[Confusions[i.Id]] = Clone(i);
            }
            return ret;
        }

        private void MarkModule(AssemblySetting parent, ProjectModule mod, Marking mark, List<ModuleSetting> settings)
        {
            bool applyToMember;
            ModuleSetting ret = MarkModule(parent, mod, mark, out applyToMember);
            ret.Parameters = mark.CurrentConfusions;
            mark.ApplyToMember = applyToMember;

            using (mark.Level())
            {
                List<MemberSetting> memSettings = new List<MemberSetting>();
                foreach (var t in ret.Module.GetAllTypes())
                    using (mark.Level())
                    {
                        ProjectType type = mod.SingleOrDefault(_ => _.FullName == t.FullName) ??
                                            new ProjectType() { FullName = t.FullName };
                        MarkType(ret, type, mark, memSettings);
                    }
                ret.Members = memSettings.ToArray();
            }
            settings.Add(ret);
        }
        protected virtual ModuleSetting MarkModule(AssemblySetting parent, ProjectModule mod, Marking mark, out bool applyToMember)
        {
            ModuleSetting ret = new ModuleSetting(mod.Resolve(parent.Assembly));
            ret.ApplyToMember = applyToMember = false;
            if (mod.Config != null)
            {
                ret.ApplyToMember = applyToMember = mod.Config.ApplyToMembers;
                mark.CurrentConfusions.Clear();
                foreach (var i in proj.Settings.Single(_ => _.Name == mod.Config.Id))
                    mark.CurrentConfusions[Confusions[i.Id]] = Clone(i);
            }
            return ret;
        }

        private void MarkType(ModuleSetting parent, ProjectType type, Marking mark, List<MemberSetting> settings)
        {
            bool applyToMember;
            MemberSetting ret = MarkType(parent, type, mark, out applyToMember);
            ret.Parameters = mark.CurrentConfusions;
            mark.ApplyToMember = applyToMember;

            using (mark.Level())
            {
                List<MemberSetting> memSettings = new List<MemberSetting>();
                TypeDefinition typeDef = ret.Object as TypeDefinition;
                List<IMemberDefinition> mems = new List<IMemberDefinition>(typeDef.Methods.OfType<IMemberDefinition>().Concat(
                                  typeDef.Fields.OfType<IMemberDefinition>()).Concat(
                                  typeDef.Properties.OfType<IMemberDefinition>()).Concat(
                                  typeDef.Events.OfType<IMemberDefinition>()));
                foreach (ProjectMember mem in type)
                    using (mark.Level())
                        MarkMember(ret, mem, mark, memSettings);
                foreach (var i in memSettings)
                    mems.Remove(i.Object);
                foreach (var i in mems)
                    using (mark.Level())
                    {
                        ProjectMember mem = new ProjectMember();
                        mem.Import(i);
                        MarkMember(ret, mem, mark, memSettings);
                    }
                ret.Members = memSettings.ToArray();
            }
            settings.Add(ret);
        }
        protected virtual MemberSetting MarkType(ModuleSetting parent, ProjectType type, Marking mark, out bool applyToMember)
        {
            MemberSetting ret = new MemberSetting(type.Resolve(parent.Module));
            ret.ApplyToMember = applyToMember = false;
            if (type.Config != null)
            {
                ret.ApplyToMember = applyToMember = type.Config.ApplyToMembers;
                mark.CurrentConfusions.Clear();
                foreach (var i in proj.Settings.Single(_ => _.Name == type.Config.Id))
                    mark.CurrentConfusions[Confusions[i.Id]] = Clone(i);
            }
            return ret;
        }

        private void MarkMember(MemberSetting parent, ProjectMember mem, Marking mark, List<MemberSetting> settings)
        {
            bool applyToMember;
            MemberSetting ret = MarkMember(parent, mem, mark, out applyToMember);
            ret.Parameters = mark.CurrentConfusions;
            mark.ApplyToMember = applyToMember;
            settings.Add(ret);
        }
        protected virtual MemberSetting MarkMember(MemberSetting parent, ProjectMember mem, Marking mark, out bool applyToMember)
        {
            MemberSetting ret = new MemberSetting(mem.Resolve(parent.Object as TypeDefinition));
            ret.ApplyToMember = applyToMember = false;
            if (mem.Config != null)
            {
                ret.ApplyToMember = applyToMember = mem.Config.ApplyToMembers;
                mark.CurrentConfusions.Clear();
                foreach (var i in proj.Settings.Single(_ => _.Name == mem.Config.Id))
                    mark.CurrentConfusions[Confusions[i.Id]] = Clone(i);
            }
            return ret;
        }

    }
}
