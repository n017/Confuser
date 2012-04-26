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
using System.Windows.Media.Animation;
using System.ComponentModel;

namespace Confuser
{
    /// <summary>
    /// Interaction logic for AdvSelection.xaml
    /// </summary>
    public partial class AdvSelection : ConfuserTab, IPage
    {
        static AdvSelection()
        {
            TitlePropertyKey.OverrideMetadata(typeof(AdvSelection), new UIPropertyMetadata("Advanced Settings"));
        }
        public AdvSelection()
        {
            InitializeComponent();
            Style = FindResource(typeof(ConfuserTab)) as Style;
        }

        class ProjToTree : IEnumerable<AsmTreeModel>, INotifyCollectionChanged
        {
            public ProjToTree(IHost host)
            {
                this.host = host;
                enumerable = host.Project.Assemblies.Select(_ => new AsmTreeModel(_.Assembly));
                host.Project.Assemblies.CollectionChanged += (sender, e) =>
                    {
                        if (CollectionChanged != null)
                            CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                    };
            }
            IHost host;
            IEnumerable<AsmTreeModel> enumerable;

            public IEnumerator<AsmTreeModel> GetEnumerator()
            {
                return enumerable.GetEnumerator();
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return enumerable.GetEnumerator();
            }

            public event NotifyCollectionChangedEventHandler CollectionChanged;
        }
        class ProjToCns : IEnumerable<ConfusionListItem>, INotifyCollectionChanged
        {
            public ProjToCns(IHost host)
            {
                this.host = host;
                enumerable = host.Project.Confusions.Select(_ => new ConfusionListItem(_));
                host.Project.Confusions.CollectionChanged += (sender, e) =>
                {
                    if (CollectionChanged != null)
                        CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                };
            }
            IHost host;
            IEnumerable<ConfusionListItem> enumerable;

            public IEnumerator<ConfusionListItem> GetEnumerator()
            {
                return enumerable.GetEnumerator();
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return enumerable.GetEnumerator();
            }

            public event NotifyCollectionChangedEventHandler CollectionChanged;
        }

        IHost host;
        public override void Init(IHost host)
        {
            this.host = host;

            asmSel.SelectedItemChanged += asmSel_SelectedItemChanged;
            asmSel.MouseDown += (sender, e) =>
            {
                asmSel.ClearSelection();
            };

            this.DataContext = this;

            //=_=||
            preset.ApplyTemplate();
            TextBox tb = preset.Template.FindName("PART_EditableTextBox", preset) as TextBox;
            tb.IsEnabled = false;
            tb.IsHitTestVisible = false;
        }
        public override void InitProj()
        {
            this.asmSel.ItemsSource = new ProjToTree(host);
            this.cnList.ItemsSource = new ProjToCns(host);

            host.Project.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == "DefaultPreset" && host.Project.DefaultPreset != PrjPreset.Undefined)
                    RefrSelection();
            };
        }

        static readonly object ADVSEL = new object();

        PrjAssembly GetAssembly(AssemblyDefinition asmDef)
        {
            var ret = host.Project.Assemblies.SingleOrDefault(_ => _.Assembly == asmDef);
            if (ret == null) throw new InvalidOperationException(); //...what? should already existed! or how can the user select it?
            return ret;
        }
        PrjModule GetModule(ModuleDefinition modDef)
        {
            var asm = GetAssembly(modDef.Assembly);
            var ret = asm.SingleOrDefault(_ => _.Name == modDef.Name);
            if (ret == null)
                asm.Add(ret = new PrjModule(asm) { Name = modDef.Name });
            return ret;
        }
        PrjMember GetMember(IMemberDefinition member)
        {
            var mod = GetModule(member.Module);
            var ret = mod.SingleOrDefault(_ => _.Member == member);
            if (ret == null)
                mod.Add(ret = new PrjMember(mod) { Member = member });
            return ret;
        }
        PrjSettings CreateSettings(object obj, INotifyChildrenChanged self)
        {
            PrjSettings parentSettings;
            INotifyChildrenChanged x;
            if (obj is AssemblyDefinition)
                parentSettings = null;
            else if (obj is ModuleDefinition)
                parentSettings = GetSettings((obj as ModuleDefinition).Assembly, out x);
            else if (obj is TypeDefinition)
                parentSettings = GetSettings((obj as TypeDefinition).Module, out x);
            else if (obj is IMemberDefinition)
                parentSettings = GetSettings((obj as IMemberDefinition).DeclaringType, out x);
            else
                throw new InvalidOperationException();
            if (parentSettings != null && parentSettings.ApplyToMembers)
                return parentSettings.Clone(self);
            else
                return new PrjSettings(self);
        }
        PrjSettings GetSettings(object obj, out INotifyChildrenChanged model)
        {
            if (obj is AssemblyDefinition)
            {
                var asm = GetAssembly(obj as AssemblyDefinition);
                model = asm;
                if (asm.Settings == null)
                    asm.Settings = CreateSettings(obj, asm);
                return asm.Settings;
            }
            else if (obj is ModuleDefinition)
            {
                var mod = GetModule(obj as ModuleDefinition);
                model = mod;
                if (mod.Settings == null)
                    mod.Settings = CreateSettings(obj, mod);
                return mod.Settings;
            }
            else if (obj is IMemberDefinition)
            {
                var mem = GetMember(obj as IMemberDefinition);
                model = mem;
                if (mem.Settings == null)
                    mem.Settings = CreateSettings(obj, mem);
                return mem.Settings;
            }
            else
                throw new InvalidOperationException();
        }

        PrjSettings settings;
        INotifyChildrenChanged obj;

        class ConfusionListItem : INotifyPropertyChanged
        {
            public ConfusionListItem(IConfusion cn) { Confusion = cn; }
            public IConfusion Confusion { get; private set; }
            bool sel;
            public bool IsSelected
            {
                get { return sel; }
                set
                {
                    if (sel != value)
                    {
                        sel = value;
                        if (PropertyChanged != null)
                            PropertyChanged(this, new PropertyChangedEventArgs("IsSelected"));
                    }
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;
        }


        void RefrSelection()
        {
            IAnnotationProvider obj = asmSel.SelectedItem;
            if (obj == null)
            {
                panel.IsEnabled = false;
            }
            else
            {
                if (obj is Namespace)
                {
                    if (!obj.Annotations.Contains(ADVSEL))
                        obj.Annotations[ADVSEL] = settings = new PrjSettings(null);
                    else
                        settings = obj.Annotations[ADVSEL] as PrjSettings;
                }
                else
                {
                    object r = asmSel.SelectedItem;
                    settings = GetSettings(r, out this.obj);
                }

                inChanging = true;
                foreach (ConfusionListItem i in cnList.Items)
                    i.IsSelected = settings.Any(_ => _.Object == i.Confusion);
                Preset p = Preset.None;
                foreach (ConfusionListItem i in cnList.SelectedItems)
                    if (i.Confusion.Preset > p) p = i.Confusion.Preset;

                PrjPreset active = (PrjPreset)p;
                foreach (ConfusionListItem i in cnList.Items)
                    if (i.Confusion.Preset <= p && !i.IsSelected)
                    {
                        active = PrjPreset.Undefined;
                        break;
                    }
                ActivePreset = active;
                inChanging = false;

                panel.IsEnabled = true;
            }
        }
        void asmSel_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            RefrSelection();
        }

        private void ApplyToMembers_Click(object sender, RoutedEventArgs e)
        {
            Action<IAnnotationProvider> apply = null;
            apply = _ =>
            {
                foreach (var i in Childer.GetChildren(_))
                {
                    if (i is Namespace)
                    {
                        if (!i.Annotations.Contains(ADVSEL))
                            i.Annotations[ADVSEL] = new PrjSettings(null);
                        var settings = i.Annotations[ADVSEL] as PrjSettings;
                        settings.Clear();
                        foreach (var j in this.settings) settings.Add(j);
                    }
                    else
                    {
                        object r = i;
                        INotifyChildrenChanged x;
                        var settings = GetSettings(r, out x);
                        settings.Clear();
                        foreach (var j in this.settings) settings.Add(j);
                    }
                    apply(i);
                }
            };
            apply(asmSel.SelectedItem);
        }

        bool inChanging = false;
        private void ConfusionsSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (inChanging) return;

            host.Project.DefaultPreset = PrjPreset.Undefined;

            Preset p = Preset.None;
            foreach (ConfusionListItem i in cnList.SelectedItems)
                if (i.Confusion.Preset > p) p = i.Confusion.Preset;

            PrjPreset active = (PrjPreset)p;
            foreach (ConfusionListItem i in cnList.Items)
                if (i.Confusion.Preset <= p && !i.IsSelected)
                {
                    active = PrjPreset.Undefined;
                    break;
                }
            ActivePreset = active;

            foreach (ConfusionListItem i in e.RemovedItems)
                settings.Remove(settings.Single(_ => _.Object == i.Confusion));
            foreach (ConfusionListItem i in e.AddedItems)
                settings.Add(new PrjConfig<IConfusion>(i.Confusion, settings));
        }

        PrjPreset ActivePreset
        {
            get { return (PrjPreset)GetValue(ActivePresetProperty); }
            set { SetValue(ActivePresetProperty, value); }
        }
        static readonly DependencyProperty ActivePresetProperty = DependencyProperty.Register("ActivePreset", typeof(PrjPreset), typeof(AdvSelection), new UIPropertyMetadata(PrjPreset.None, ActivePresetChanged));

        static void ActivePresetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if ((PrjPreset)e.NewValue != PrjPreset.Undefined)
            {
                Preset p = (Preset)e.NewValue;
                foreach (ConfusionListItem i in (d as AdvSelection).cnList.Items)
                    i.IsSelected = i.Confusion.Preset <= p;
            }
        }
    }
}
