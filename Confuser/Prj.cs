using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Confuser.Core;
using System.Reflection;
using Mono.Cecil;
using System.Windows;
using System.ComponentModel;
using Confuser.Core.Project;

namespace Confuser
{
    public interface INotifyChildrenChanged : INotifyPropertyChanged
    {
        void OnChildChanged();
    }

    public class PrjArgument
    {
        string name;
        public string Name
        {
            get { return name; }
            set
            {
                if (name != value)
                {
                    name = value;
                    if (parent != null) parent.OnChildChanged();
                }
            }
        }
        string val;
        public string Value
        {
            get { return val; }
            set
            {
                if (val != value)
                {
                    val = value;
                    if (parent != null) parent.OnChildChanged();
                }
            }
        }

        INotifyChildrenChanged parent;
        public PrjArgument(INotifyChildrenChanged parent)
        {
            this.parent = parent;
        }
    }

    public class PrjConfig<T> : ObservableCollection<PrjArgument>, INotifyChildrenChanged
    {
        INotifyChildrenChanged parent;
        public PrjConfig(T obj, INotifyChildrenChanged parent)
        {
            this.Object = obj;
            this.parent = parent;
        }

        public T Object { get; private set; }

        SettingItemAction action;
        public SettingItemAction Action
        {
            get { return action; }
            set
            {
                if (action != value)
                {
                    action = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("Action"));
                }
            }
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (parent != null) parent.OnChildChanged();
            base.OnPropertyChanged(e);
        }
        public void OnChildChanged()
        {
            if (parent != null) parent.OnChildChanged();
        }

        public SettingItem<T> ToCrConfig()
        {
            SettingItem<T> ret = new SettingItem<T>();
            if (Object is Packer)
                ret.Id = (Object as Packer).ID;
            else
                ret.Id = (Object as IConfusion).ID;
            ret.Action = action;
            foreach (var i in this)
                ret.Add(i.Name, i.Value);
            return ret;
        }

        public override bool Equals(object obj)
        {
            PrjConfig<T> cfg = obj as PrjConfig<T>;
            if (cfg == null) return false;
            if (!cfg.Object.Equals(this.Object)) return false;
            if (cfg.Count != this.Count) return false;
            if (cfg.action != this.action) return false;
            for (int i = 0; i < this.Count; i++)
                if (cfg[i].Name != this[i].Name ||
                    cfg[i].Value != this[i].Value) return false;
            return true;
        }

        public override int GetHashCode()
        {
            int ret = Object.GetHashCode() ^ (int)action;
            for (int i = 0; i < this.Count; i++)
                ret ^= this[i].Name.GetHashCode() ^ this[i].Value.GetHashCode();
            return ret;
        }
    }
    public class PrjSettings : ObservableCollection<PrjConfig<IConfusion>>, INotifyChildrenChanged
    {
        INotifyChildrenChanged parent;
        public PrjSettings(INotifyChildrenChanged parent)
        {
            this.parent = parent;
        }

        Preset preset;
        public Preset Preset
        {
            get { return preset; }
            set
            {
                if (preset != value)
                {
                    preset = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("Preset"));
                }
            }
        }

        bool apply;
        public bool ApplyToMembers
        {
            get { return apply; }
            set
            {
                if (apply != value)
                {
                    apply = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("ApplyToMembers"));
                }
            }
        }

        bool inherit;
        public bool Inherit
        {
            get { return inherit; }
            set
            {
                if (inherit != value)
                {
                    inherit = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("Inherit"));
                }
            }
        }

        internal string name;

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (parent != null) parent.OnChildChanged();
            base.OnPropertyChanged(e);
        }
        public void OnChildChanged()
        {
            parent.OnChildChanged();
        }

        public PrjSettings Clone(INotifyChildrenChanged parent)
        {
            PrjSettings ret = new PrjSettings(parent);
            ret.apply = apply;
            ret.inherit = inherit;
            ret.preset = preset;
            foreach (PrjConfig<IConfusion> i in this)
            {
                PrjConfig<IConfusion> n = new PrjConfig<IConfusion>(i.Object, ret);
                n.Action = i.Action;
                foreach (var j in i)
                    n.Add(new PrjArgument(n) { Name = j.Name, Value = j.Value });
                ret.Add(n);
            }
            return ret;
        }

        public ObfSettings ToCrSettings()
        {
            ObfSettings ret = new ObfSettings();
            ret.Name = name;
            ret.Preset = Preset;
            foreach (var i in this)
                ret.Add(i.ToCrConfig());
            return ret;
        }
        public void FromCrSettings(Prj prj, ObfSettings settings)
        {
            name = settings.Name;
            preset = settings.Preset;
            foreach (var i in settings)
            {
                PrjConfig<IConfusion> cfg = new PrjConfig<IConfusion>(prj.Confusions.Single(_ => _.ID == i.Id), this);
                cfg.Action = i.Action;
                foreach (var j in i.AllKeys)
                    cfg.Add(new PrjArgument(this) { Name = j, Value = i[j] });
                this.Add(cfg);
            }
        }

        public ObfConfig ToCrConfig()
        {
            ObfConfig ret = new ObfConfig();
            ret.ApplyToMembers = apply;
            ret.Inherit = inherit;
            ret.Id = name;
            return ret;
        }

        public override bool Equals(object obj)
        {
            PrjSettings settings = obj as PrjSettings;
            if (settings == null) return false; //Don't compare ApplyToMember/Inherit
            if (settings.Count != this.Count) return false;
            if (settings.Preset != this.Preset) return false;
            for (int i = 0; i < this.Count; i++)
                if (!settings[i].Equals(this[i])) return false;
            return true;
        }

        public override int GetHashCode()
        {
            int ret = (int)Preset;
            for (int i = 0; i < this.Count; i++)
                ret ^= this[i].GetHashCode();
            return ret;
        }
    }
    public class PrjAssembly : ObservableCollection<PrjModule>, INotifyChildrenChanged
    {
        INotifyChildrenChanged parent;
        public PrjAssembly(INotifyChildrenChanged parent)
        {
            this.parent = parent;
        }

        AssemblyDefinition asmDef;
        public AssemblyDefinition Assembly
        {
            get { return asmDef; }
            set
            {
                if (asmDef != value)
                {
                    asmDef = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("Assembly"));
                }
            }
        }
        string path;
        public string Path
        {
            get { return path; }
            set
            {
                if (path != value)
                {
                    path = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("Path"));
                }
            }
        }
        bool isMain;
        public bool IsMain
        {
            get { return isMain; }
            set
            {
                if (isMain != value)
                {
                    isMain = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("IsMain"));
                }
            }
        }

        public bool IsExecutable { get; set; }

        PrjSettings settings;
        public PrjSettings Settings
        {
            get { return settings; }
            set
            {
                if (settings != value)
                {
                    settings = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("Settings"));
                }
            }
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (parent != null) parent.OnChildChanged();
            base.OnPropertyChanged(e);
        }
        public void OnChildChanged()
        {
            parent.OnChildChanged();
        }

        public ProjectAssembly ToCrAssembly()
        {
            ProjectAssembly ret = new ProjectAssembly();
            ret.Path = path;
            ret.IsMain = isMain;
            if (settings != null)
                ret.Config = settings.ToCrConfig();
            foreach (var i in this)
                ret.Add(i.ToCrModule());
            return ret;
        }
        public void FromCrAssembly(Prj prj, Dictionary<string, PrjSettings> settings, ProjectAssembly asm)
        {
            this.path = asm.Path;
            this.asmDef = AssemblyDefinition.ReadAssembly(this.path);
            this.IsExecutable = this.asmDef.MainModule.EntryPoint != null;
            this.isMain = asm.IsMain;
            if (asm.Config != null)
            {
                this.settings = settings[asm.Config.Id].Clone(this);
                this.settings.ApplyToMembers = asm.Config.ApplyToMembers;
                this.settings.Inherit = asm.Config.Inherit;
            }
            foreach (var i in asm)
            {
                PrjModule mod = new PrjModule(this);
                mod.FromCrModule(prj, settings, i);
                this.Add(mod);
            }
        }

        internal void GetSettings(List<PrjSettings> x)
        {
            if (settings != null)
                x.Add(settings);
            foreach (var i in this)
                i.GetSettings(x);
        }
    }
    public class PrjModule : ObservableCollection<PrjMember>, INotifyChildrenChanged
    {
        INotifyChildrenChanged parent;
        public PrjModule(INotifyChildrenChanged parent)
        {
            this.parent = parent;
        }

        string name;
        public string Name
        {
            get { return name; }
            set
            {
                if (name != value)
                {
                    name = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("Name"));
                }
            }
        }
        PrjSettings settings;
        public PrjSettings Settings
        {
            get { return settings; }
            set
            {
                if (settings != value)
                {
                    settings = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("Settings"));
                }
            }
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (parent != null) parent.OnChildChanged();
            base.OnPropertyChanged(e);
        }
        public void OnChildChanged()
        {
            parent.OnChildChanged();
        }

        public ProjectModule ToCrModule()
        {
            ProjectModule ret = new ProjectModule();
            ret.Name = name;
            if (settings != null)
                ret.Config = settings.ToCrConfig();
            PrjSettings current = settings ?? (parent as PrjAssembly).Settings;

            Dictionary<TypeDefinition, ProjectType> map = new Dictionary<TypeDefinition, ProjectType>();
            Dictionary<TypeDefinition, PrjSettings> typeSettings = new Dictionary<TypeDefinition, PrjSettings>();
            foreach (var i in this.Where(_ => _.Member is TypeDefinition))
            {
                ProjectType t = new ProjectType();
                TypeDefinition typeDef = i.Member as TypeDefinition;
                t.Import(typeDef);
                if (i.Settings != null)
                {
                    if (i.Settings.Equals(current))
                        current.ApplyToMembers = true;
                    else
                        t.Config = i.Settings.ToCrConfig();
                }
                typeSettings[typeDef] = i.Settings ?? current;
                ret.Add(map[typeDef] = t);
            }
            foreach (var i in this.Where(_ => !(_.Member is TypeDefinition)))
            {
                if (i.Settings != null)
                {
                    var typeS = typeSettings[i.Member.DeclaringType];
                    if (i.Settings.Equals(typeS))
                        typeS.ApplyToMembers = true;
                    else
                    {
                        ProjectMember m = new ProjectMember();
                        m.Import(i.Member);
                        m.Config = i.Settings.ToCrConfig();
                        map[i.Member.DeclaringType].Add(m);
                    }
                }
            }
            foreach (var i in map.Keys)
            {
                if (typeSettings[i] != current)
                    map[i].Config = typeSettings[i].ToCrConfig();
                if (map[i].Count == 0 && map[i].Config == null)
                    ret.Remove(map[i]);
            }
            return ret;
        }
        public void FromCrModule(Prj prj, Dictionary<string, PrjSettings> settings, ProjectModule mod)
        {
            this.name = mod.Name;
            if (mod.Config != null)
            {
                this.settings = settings[mod.Config.Id].Clone(this);
                this.settings.ApplyToMembers = mod.Config.ApplyToMembers;
                this.settings.Inherit = mod.Config.Inherit;
            }
            else if ((parent as PrjAssembly).Settings != null && (parent as PrjAssembly).Settings.ApplyToMembers)
                this.settings = (parent as PrjAssembly).Settings.Clone(this);
            var modDef = (parent as PrjAssembly).Assembly.Modules.Single(_ => _.Name == mod.Name);
            foreach (var i in mod)
            {
                PrjMember type = new PrjMember(this);
                type.Member = i.Resolve(modDef);
                if (type.Member == null) continue;  //Ignore cannot resolve
                if (i.Config != null)
                {
                    type.Settings = settings[i.Config.Id].Clone(type);
                    type.Settings.ApplyToMembers = i.Config.ApplyToMembers;
                    type.Settings.Inherit = i.Config.Inherit;
                    prj.DefaultPreset = PrjPreset.Undefined;
                }
                else if (this.settings != null && this.settings.ApplyToMembers)
                    type.Settings = this.settings.Clone(type);
                this.Add(type);

                foreach (var j in i)
                {
                    PrjMember mem = new PrjMember(this);
                    mem.Member = j.Resolve(type.Member as TypeDefinition);
                    if (mem.Member == null) continue;  //Ignore cannot resolve
                    if (j.Config != null)
                    {
                        mem.Settings = settings[j.Config.Id].Clone(mem);
                        mem.Settings.ApplyToMembers = j.Config.ApplyToMembers;
                        mem.Settings.Inherit = j.Config.Inherit;
                        prj.DefaultPreset = PrjPreset.Undefined;
                    }
                    else if (type.Settings != null && type.Settings.ApplyToMembers)
                        mem.Settings = type.Settings.Clone(type);
                    this.Add(mem);
                }
            }
        }

        internal void GetSettings(List<PrjSettings> x)
        {
            if (settings != null)
                x.Add(settings);
            foreach (var i in this)
                i.GetSettings(x);
        }
    }
    public class PrjMember : INotifyChildrenChanged
    {
        INotifyChildrenChanged parent;
        public PrjMember(INotifyChildrenChanged parent)
        {
            this.parent = parent;
        }

        IMemberDefinition mem;
        public IMemberDefinition Member
        {
            get { return mem; }
            set
            {
                if (mem != value)
                {
                    mem = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("Member"));
                }
            }
        }
        PrjSettings settings;
        public PrjSettings Settings
        {
            get { return settings; }
            set
            {
                if (settings != value)
                {
                    settings = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("Settings"));
                }
            }
        }

        public void OnChildChanged()
        {
            if (parent != null) parent.OnChildChanged();
        }
        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, e);
        }

        internal void GetSettings(List<PrjSettings> x)
        {
            if (settings != null)
                x.Add(settings);
        }
    }

    public enum PrjPreset
    {
        None,
        Minimum,
        Normal,
        Aggressive,
        Maximum,
        Undefined
    }
    public class Prj : INotifyChildrenChanged
    {
        static Prj()
        {
            foreach (Type type in typeof(IConfusion).Assembly.GetTypes())
            {
                if (typeof(IConfusion).IsAssignableFrom(type) && type != typeof(IConfusion))
                    DefaultConfusions.Add(Activator.CreateInstance(type) as IConfusion);
                if (typeof(Packer).IsAssignableFrom(type) && type != typeof(Packer))
                    DefaultPackers.Add(Activator.CreateInstance(type) as Packer);
            }
            for (int i = 0; i < DefaultConfusions.Count; i++)
                for (int j = i; j < DefaultConfusions.Count; j++)
                    if (Comparer<string>.Default.Compare(DefaultConfusions[i].Name, DefaultConfusions[j].Name) > 0)
                    {
                        var tmp = DefaultConfusions[i];
                        DefaultConfusions[i] = DefaultConfusions[j];
                        DefaultConfusions[j] = tmp;
                    }
            for (int i = 0; i < DefaultPackers.Count; i++)
                for (int j = i; j < DefaultPackers.Count; j++)
                    if (Comparer<string>.Default.Compare(DefaultPackers[i].Name, DefaultPackers[j].Name) > 0)
                    {
                        var tmp = DefaultPackers[i];
                        DefaultPackers[i] = DefaultPackers[j];
                        DefaultPackers[j] = tmp;
                    }
        }

        public static readonly ObservableCollection<IConfusion> DefaultConfusions = new ObservableCollection<IConfusion>();
        public static readonly ObservableCollection<Packer> DefaultPackers = new ObservableCollection<Packer>();

        public Prj()
        {
            Confusions = new ObservableCollection<IConfusion>(DefaultConfusions);
            Confusions.CollectionChanged += (sender, e) => OnChildChanged();
            Packers = new ObservableCollection<Packer>(DefaultPackers);
            Packers.CollectionChanged += (sender, e) => OnChildChanged();
            Plugins = new ObservableCollection<string>();
            Plugins.CollectionChanged += (sender, e) => OnChildChanged();
            Assemblies = new ObservableCollection<PrjAssembly>();
            Assemblies.CollectionChanged += (sender, e) => OnChildChanged();
        }

        public ObservableCollection<IConfusion> Confusions { get; private set; }
        public ObservableCollection<Packer> Packers { get; private set; }

        string snKey;
        public string StrongNameKey
        {
            get { return snKey; }
            set
            {
                if (snKey != value)
                {
                    snKey = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("StrongNameKey"));
                }
            }
        }
        string output;
        public string OutputPath
        {
            get { return output; }
            set
            {
                if (output != value)
                {
                    output = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("OutputPath"));
                }
            }
        }
        string seed;
        public string Seed
        {
            get { return seed; }
            set
            {
                if (seed != value)
                {
                    seed = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("Seed"));
                }
            }
        }

        string file;
        public string FileName
        {
            get { return file; }
            set
            {
                if (file != value)
                {
                    file = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("FileName"));
                }
            }
        }
        bool modified;
        public bool IsModified
        {
            get { return modified; }
            set
            {
                if (modified != value)
                {
                    modified = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("IsModified"));
                }
            }
        }

        public void LoadAssembly(Assembly asm, bool interact)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Loaded type :");
            bool h = false;
            foreach (Type type in asm.GetTypes())
            {
                if (typeof(Core.IConfusion).IsAssignableFrom(type) && type != typeof(Core.IConfusion))
                {
                    Confusions.Add(Activator.CreateInstance(type) as Core.IConfusion);
                    sb.AppendLine(type.FullName);
                    h = true;
                }
                if (typeof(Core.Packer).IsAssignableFrom(type) && type != typeof(Core.Packer))
                {
                    Packers.Add(Activator.CreateInstance(type) as Core.Packer);
                    sb.AppendLine(type.FullName);
                    h = true;
                }
            }
            if (!h) sb.AppendLine("NONE!");
            else
            {
                Plugins.Add(asm.Location);
                Sort();
            }
            if (interact)
                MessageBox.Show(sb.ToString(), "Confuser", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        void Sort()
        {
            for (int i = 0; i < Confusions.Count; i++)
                for (int j = i; j < Confusions.Count; j++)
                    if (Comparer<string>.Default.Compare(Confusions[i].Name, Confusions[j].Name) > 0)
                    {
                        var tmp = Confusions[i];
                        Confusions[i] = Confusions[j];
                        Confusions[j] = tmp;
                    }
            for (int i = 0; i < Packers.Count; i++)
                for (int j = i; j < Packers.Count; j++)
                    if (Comparer<string>.Default.Compare(Packers[i].Name, Packers[j].Name) > 0)
                    {
                        var tmp = Packers[i];
                        Packers[i] = Packers[j];
                        Packers[j] = tmp;
                    }
        }

        PrjPreset preset = PrjPreset.Normal;
        public PrjPreset DefaultPreset
        {
            get { return preset; }
            set
            {
                if (preset != value)
                {
                    preset = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("DefaultPreset"));
                }
            }
        }
        PrjConfig<Packer> packer;
        public PrjConfig<Packer> Packer
        {
            get { return packer; }
            set
            {
                if (packer != value)
                {
                    packer = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("Packer"));
                }
            }
        }
        public ObservableCollection<PrjAssembly> Assemblies { get; private set; }
        public ObservableCollection<string> Plugins { get; private set; }

        void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (e.PropertyName != "FileName" && e.PropertyName != "IsModified")
                OnChildChanged();
            if (PropertyChanged != null)
                PropertyChanged(this, e);
        }
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnChildChanged()
        {
            IsModified = true;
        }

        public ConfuserProject ToCrProj()
        {
            ConfuserProject ret = new ConfuserProject();
            ret.OutputPath = output;
            ret.SNKeyPath = snKey;
            ret.Seed = seed;


            List<PrjSettings> s = new List<PrjSettings>();
            foreach (var i in Assemblies)
                i.GetSettings(s);
            int idx = 0;
            Dictionary<PrjSettings, string> settingsName = new Dictionary<PrjSettings, string>();
            foreach (var i in s.Distinct())
            {
                idx++;
                settingsName[i] = i.name = "settings" + idx;
                ret.Settings.Add(i.ToCrSettings());
            }
            foreach (var i in s)
            {
                i.name = settingsName[i];
            }

            if (packer != null)
                ret.Packer = packer.ToCrConfig();
            if (preset != PrjPreset.Undefined)
                ret.DefaultPreset = (Preset)preset;
            foreach (string i in Plugins)
                ret.Plugins.Add(i);

            foreach (var i in Assemblies)
                ret.Add(i.ToCrAssembly());
            return ret;
        }
        public void FromConfuserProject(ConfuserProject prj)
        {
            output = prj.OutputPath;
            snKey = prj.SNKeyPath;
            seed = prj.Seed;
            preset = (PrjPreset)prj.DefaultPreset;
            foreach (var i in prj.Plugins)
                LoadAssembly(Assembly.LoadFrom(i), false);
            Dictionary<string, PrjSettings> map = new Dictionary<string, PrjSettings>();
            foreach (var i in prj.Settings)
            {
                PrjSettings x = new PrjSettings(this);
                x.FromCrSettings(this, i);
                map[x.name] = x;
            }
            if (prj.Packer != null)
            {
                this.packer = new PrjConfig<Packer>(Packers.Single(_ => _.ID == prj.Packer.Id), this);
                foreach (var j in prj.Packer.AllKeys)
                    this.packer.Add(new PrjArgument(this) { Name = j, Value = prj.Packer[j] });
            }
            foreach (var i in prj)
            {
                PrjAssembly asm = new PrjAssembly(this);
                asm.FromCrAssembly(this, map, i);
                if (asm.Count != 0 || asm.Settings != null)
                    preset = PrjPreset.Undefined;
                Assemblies.Add(asm);
            }
        }
    }
}
