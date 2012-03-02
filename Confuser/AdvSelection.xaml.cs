using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Mono.Cecil;
using System.Collections.ObjectModel;
using Confuser.Core;
using Confuser.AsmSelector;
using System.Collections.Specialized;

namespace Confuser
{
    /// <summary>
    /// Interaction logic for AdvSelection.xaml
    /// </summary>
    public partial class AdvSelection : Page, IPage<ConfuserDatas>
    {
        public AdvSelection()
        {
            InitializeComponent();
            Style = FindResource(typeof(Page)) as Style;
        }

        IHost host;
        ConfuserDatas parameter;
        public void Init(IHost host, ConfuserDatas parameter)
        {
            this.host = host;
            this.parameter = parameter;

            foreach (var i in parameter.Assemblies)
                asmSel.AddAssembly(i);

            asmSel.SelectedItemChanged += asmSel_SelectedItemChanged;
            asmSel.MouseDown += (sender, e) =>
            {
                asmSel.ClearSelection();
            };
        }

        static readonly object ADVSEL = new object();

        IList<IConfusion> cions;
        void asmSel_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            IAnnotationProvider obj = asmSel.SelectedItem;
            if (obj == null)
            {
                cnList.ItemsSource = cions = null;
                panel.IsEnabled = false;
            }
            else
            {
                if (!obj.Annotations.Contains(ADVSEL))
                    obj.Annotations[ADVSEL] = new ObservableCollection<IConfusion>();
                cions = obj.Annotations[ADVSEL] as ObservableCollection<IConfusion>;
                cnList.ItemsSource = cions;
                panel.IsEnabled = true;
            }
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            if (!cions.Contains(cnSel.SelectedItem as IConfusion))
                cions.Add(cnSel.SelectedItem as IConfusion);
        }
        private void Remove_Click(object sender, RoutedEventArgs e)
        {
            if (cions.Contains(cnSel.SelectedItem as IConfusion))
                cions.Remove(cnSel.SelectedItem as IConfusion);
        }
        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            cions.Clear();
        }
        private void ApplyToMembers_Click(object sender, RoutedEventArgs e)
        {
            Action<IAnnotationProvider> apply = null;
            apply = _ =>
            {
                foreach (var i in Childer.GetChildren(_))
                {
                    if (i.Annotations.Contains(ADVSEL))
                    {
                        var list = i.Annotations[ADVSEL] as IList<IConfusion>;
                        list.Clear();
                        foreach (var j in cions) list.Add(j);
                    }
                    else
                        i.Annotations[ADVSEL] = new ObservableCollection<IConfusion>(cions);
                    apply(i);
                }
            };
            apply(asmSel.SelectedItem);
        }
        private void ApplyPreset_Click(object sender, RoutedEventArgs e)
        {
            Preset preset = (Preset)this.preset.SelectedItem;
            foreach (var i in ConfuserDatas.Confusions)
                if (i.Preset <= preset && !cions.Contains(i))
                    cions.Add(i);
        }

        Dictionary<string, AdvAssembly> SerializeAsm()
        {
            var ret = new Dictionary<string, AdvAssembly>();
            foreach (var i in parameter.Assemblies)
            {
                AdvAssembly asm = new AdvAssembly() { FullName = i.FullName };
                if ((i as IAnnotationProvider).Annotations.Contains(ADVSEL))
                    asm.Confusions = (i as IAnnotationProvider).Annotations[ADVSEL] as IList<IConfusion>;

                asm.Modules = new Dictionary<string, AdvModule>();
                foreach (var j in i.Modules)
                {
                    AdvModule mod = new AdvModule() { Name = j.Name };
                    if ((j as IAnnotationProvider).Annotations.Contains(ADVSEL))
                        mod.Confusions = (j as IAnnotationProvider).Annotations[ADVSEL] as IList<IConfusion>;

                    List<AdvMember> mems = new List<AdvMember>();
                    foreach (var k in j.Types)
                        SerializeMember(k, mems);
                    mod.Members = mems.ToArray();
                    asm.Modules.Add(j.Name, mod);
                }
                ret.Add(i.FullName, asm);
            }
            return ret;
        }
        void SerializeMember(TypeDefinition typeDef, List<AdvMember> mems)
        {
            if ((typeDef as IAnnotationProvider).Annotations.Contains(ADVSEL))
            {
                AdvMember type = new AdvMember() { DeclaringType = MetadataToken.Zero, MemberToken = typeDef.MetadataToken };
                type.Confusions = (typeDef as IAnnotationProvider).Annotations[ADVSEL] as IList<IConfusion>;
                mems.Add(type);
            }

            foreach (var i in typeDef.NestedTypes)
                SerializeMember(i, mems);
            foreach (var i in typeDef.Methods.Where(_ => (_ as IAnnotationProvider).Annotations.Contains(ADVSEL)))
            {
                AdvMember mem = new AdvMember() { DeclaringType = typeDef.MetadataToken, MemberToken = i.MetadataToken };
                mem.Confusions = (i as IAnnotationProvider).Annotations[ADVSEL] as IList<IConfusion>;
                mems.Add(mem);
            }
            foreach (var i in typeDef.Fields.Where(_ => (_ as IAnnotationProvider).Annotations.Contains(ADVSEL)))
            {
                AdvMember mem = new AdvMember() { DeclaringType = typeDef.MetadataToken, MemberToken = i.MetadataToken };
                mem.Confusions = (i as IAnnotationProvider).Annotations[ADVSEL] as IList<IConfusion>;
                mems.Add(mem);
            }
            foreach (var i in typeDef.Properties.Where(_ => (_ as IAnnotationProvider).Annotations.Contains(ADVSEL)))
            {
                AdvMember mem = new AdvMember() { DeclaringType = typeDef.MetadataToken, MemberToken = i.MetadataToken };
                mem.Confusions = (i as IAnnotationProvider).Annotations[ADVSEL] as IList<IConfusion>;
                mems.Add(mem);
            }
            foreach (var i in typeDef.Events.Where(_ => (_ as IAnnotationProvider).Annotations.Contains(ADVSEL)))
            {
                AdvMember mem = new AdvMember() { DeclaringType = typeDef.MetadataToken, MemberToken = i.MetadataToken };
                mem.Confusions = (i as IAnnotationProvider).Annotations[ADVSEL] as IList<IConfusion>;
                mems.Add(mem);
            }
        }

        Packer _packer;
        ConfuserDatas Load()
        {
            var dat = SerializeAsm();

            StringBuilder summary = new StringBuilder();
            summary.AppendLine(string.Format("Output path: {0}", parameter.OutputPath));
            if (string.IsNullOrEmpty(parameter.StrongNameKey))
                summary.AppendLine("No strong name key specified.");
            else
                summary.AppendLine(string.Format("Strong name key: {0}", parameter.StrongNameKey));
            summary.AppendLine();

            parameter.Parameter = new ConfuserParameter();
            AdvMarker mkr = new AdvMarker() { Data = dat };
            parameter.Parameter.Marker = mkr;
            if (_packer != null)
                summary.AppendLine(string.Format("Packer: {0}", (mkr.packer = _packer).Name));
            else
                summary.AppendLine("No packer specified.");
            summary.AppendLine();

            parameter.Summary = summary.ToString();
            return parameter;
        }
        private void next_Click(object sender, RoutedEventArgs e)
        {
            if (usePacker.IsChecked.GetValueOrDefault())
                _packer = (Packer)packer.SelectedValue;
            else
                _packer = null;
            host.Load<ConfuserDatas>(Load, new Summary());
        }
    }

    struct AdvAssembly
    {
        public string FullName;
        public IList<IConfusion> Confusions;
        public Dictionary<string, AdvModule> Modules;
    }
    struct AdvModule
    {
        public string Name;
        public IList<IConfusion> Confusions;
        public AdvMember[] Members;
    }
    struct AdvMember
    {
        public MetadataToken DeclaringType;
        public MetadataToken MemberToken;
        public IList<IConfusion> Confusions;
    }
    class AdvMarker : Marker
    {
        public Dictionary<string, AdvAssembly> Data { get; set; }

        public Packer packer;
        public override MarkerSetting MarkAssemblies(IList<AssemblyDefinition> asms, Preset preset, Confuser.Core.Confuser cr, EventHandler<LogEventArgs> err)
        {
            var ret = base.MarkAssemblies(asms, preset, cr, err);
            ret.Packer = packer;
            return ret;
        }

        protected override AssemblySetting MarkAssembly(AssemblyDefinition asm, Marking mark, out bool exclude)
        {
            mark.CurrentConfusions.Clear();
            AdvAssembly advAsm = Data[asm.FullName];
            if (advAsm.Confusions != null)
                foreach (var i in advAsm.Confusions)
                    mark.CurrentConfusions.Add(i, new NameValueCollection());
            exclude = false;
            mark.StartLevel();
            return new AssemblySetting(asm);
        }

        protected override ModuleSetting MarkModule(ModuleDefinition mod, Marker.Marking mark, out bool exclude)
        {
            mark.CurrentConfusions.Clear();
            AdvModule advMod = Data[mod.Assembly.FullName].Modules[mod.Name];
            if (advMod.Confusions != null)
                foreach (var i in advMod.Confusions)
                    mark.CurrentConfusions.Add(i, new NameValueCollection());
            exclude = true;
            mark.StartLevel();
            var ret = new ModuleSetting(mod);

            List<MemberSetting> mems = new List<MemberSetting>();
            foreach (var i in advMod.Members)
            {
                MemberSetting mem = new MemberSetting();
                if (i.DeclaringType == MetadataToken.Zero)
                {
                    mem = new MemberSetting(mod.LookupToken(i.MemberToken) as TypeDefinition);
                }
                else
                {
                    TypeDefinition declType = mod.LookupToken(i.DeclaringType) as TypeDefinition;
                    if (i.MemberToken.TokenType == TokenType.Method)
                        mem = new MemberSetting(declType.Methods.Single(_ => _.MetadataToken == i.MemberToken));
                    else if (i.MemberToken.TokenType == TokenType.Field)
                        mem = new MemberSetting(declType.Fields.Single(_ => _.MetadataToken == i.MemberToken));
                    else if (i.MemberToken.TokenType == TokenType.Property)
                        mem = new MemberSetting(declType.Properties.Single(_ => _.MetadataToken == i.MemberToken));
                    else if (i.MemberToken.TokenType == TokenType.Event)
                        mem = new MemberSetting(declType.Events.Single(_ => _.MetadataToken == i.MemberToken));
                }
                mem.Parameters = new ObfuscationSettings();
                foreach (var j in i.Confusions)
                    mem.Parameters.Add(j, new NameValueCollection());
                mems.Add(mem);
            }
            ret.Members = mems.ToArray();
            return ret;
        }
    }
}
