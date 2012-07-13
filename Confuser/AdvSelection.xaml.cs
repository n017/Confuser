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
                {
                    asmSel.ClearSelection();
                }
            };
        }

        static readonly object ADVSEL = new object();

        PrjAssembly GetAssembly(AssemblyDefinition asmDef)
        {
            var ret = host.Project.Assemblies.SingleOrDefault(_ => _.Assembly == asmDef);
            if (ret == null) throw new InvalidOperationException(); //...what? should already existed! or how can the user select it?
            return ret;
        }
        PrjModule GetModule(ModuleDefinition modDef, bool create)
        {
            var asm = GetAssembly(modDef.Assembly);
            var ret = asm.SingleOrDefault(_ => _.Name == modDef.Name);
            if (ret == null && create)
                asm.Add(ret = new PrjModule(asm) { Name = modDef.Name });
            return ret;
        }
        PrjNamespace GetNamespace(Namespace ns, bool create)
        {
            return GetNamespace(ns.Name, ns.Module, create);
        }
        PrjNamespace GetNamespace(string name, ModuleDefinition mod, bool create)
        {
            PrjModule parent = GetModule(mod, create);
            if (parent == null) return null;
            var ret = parent.SingleOrDefault(_ => _.Name == name);
            if (ret == null && create)
                parent.Add(ret = new PrjNamespace(parent) { Name = name });
            return ret;
        }
        PrjMember GetType(TypeDefinition type, bool create)
        {
            if (type.DeclaringType != null)
            {
                PrjMember parent = GetType(type.DeclaringType, create);
                if (parent == null) return null;
                var ret = parent.SingleOrDefault(_ => _.Member == type);
                if (ret == null && create)
                    parent.Add(ret = new PrjMember(parent) { Member = type });
                return ret;
            }
            else
            {
                PrjNamespace parent = GetNamespace(type.Namespace, type.Module, create);
                if (parent == null) return null;
                var ret = parent.SingleOrDefault(_ => _.Member == type);
                if (ret == null)
                    parent.Add(ret = new PrjMember(parent) { Member = type });
                return ret;
            }
        }
        PrjMember GetMember(IMemberDefinition member, bool create)
        {
            var mod = GetType(member.DeclaringType, create);
            if (mod == null) return null;
            var ret = mod.SingleOrDefault(_ => _.Member == member);
            if (ret == null && create)
                mod.Add(ret = new PrjMember(mod) { Member = member });
            return ret;
        }
        PrjSettings CreateSettings(object obj, IProjObject self)
        {
            object parent;
            if (obj is AssemblyDefinition)
                return new PrjSettings(self);
            else if (obj is ModuleDefinition)
                parent = (obj as ModuleDefinition).Assembly;
            else if (obj is TypeDefinition)
            {
                TypeDefinition typeDef = obj as TypeDefinition;
                if (typeDef.DeclaringType == null)
                    parent = Childer.GetNamespace(typeDef.Module, typeDef.Namespace);
                else
                    parent = typeDef.DeclaringType;
            }
            else if (obj is Namespace)
                parent = (obj as Namespace).Module;
            else if (obj is IMemberDefinition)
                parent = (obj as IMemberDefinition).DeclaringType;
            else
                throw new InvalidOperationException();


            IProjObject parentObj = GetObj(parent, false);

            if (parentObj != null && parentObj.Settings != null && parentObj.Settings.ApplyToMembers)
                return parentObj.Settings.Clone(self);
            else
                return CreateSettings(parent, self);
        }
        IProjObject GetObj(object obj, bool create)
        {
            if (obj is AssemblyDefinition)
            {
                var asm = GetAssembly(obj as AssemblyDefinition);
                return asm;
            }
            else if (obj is ModuleDefinition)
            {
                var mod = GetModule(obj as ModuleDefinition, create);
                return mod;
            }
            else if (obj is Namespace)
            {
                var ns = GetNamespace(obj as Namespace, create);
                return ns;
            }
            else if (obj is TypeDefinition)
            {
                var type = GetType(obj as TypeDefinition, create);
                return type;
            }
            else if (obj is IMemberDefinition)
            {
                var mem = GetMember(obj as IMemberDefinition, create);
                return mem;
            }
            else
                throw new InvalidOperationException();
        }

        PrjSettings settings;
        IProjObject obj;

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
            inChanging = true;
            IAnnotationProvider obj = asmSel.SelectedItem;
            if (obj == null)
            {
                panel.IsEnabled = false;

                settings = null;
                foreach (ConfusionListItem i in cnList.Items)
                    i.IsSelected = false;
            }
            else
            {
                this.obj = GetObj(obj, false);
                if (this.obj == null)
                    settings = CreateSettings(obj, null);
                else
                    settings = this.obj.Settings ?? CreateSettings(obj, null);


                @override.IsChecked = this.obj != null && this.obj.Settings != null;
                apply.IsChecked = settings.ApplyToMembers;
                inherit.IsChecked = settings.Inherit;

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

                panel.IsEnabled = true;
            }
            inChanging = false;
        }
        void asmSel_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            RefrSelection();

            if (e.NewValue != null)
            {
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
            }
        }

        bool inChanging = false;
        private void ConfusionsSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (inChanging) return;
            if (settings == null) return;
            foreach (ConfusionListItem i in e.RemovedItems)
                settings.Remove(settings.Single(_ => _.Object == i.Confusion));
            foreach (ConfusionListItem i in e.AddedItems)
                settings.Add(new PrjConfig<IConfusion>(i.Confusion, settings));
            RefrSelection();
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
                AdvSelection self = d as AdvSelection;
                foreach (ConfusionListItem i in self.cnList.Items)
                    i.IsSelected = i.Confusion.Preset <= p;
                self.RefrSelection();
            }
        }

        private void ApplyChecked(object sender, RoutedEventArgs e)
        {
            if (inChanging) return;
            obj.Settings.ApplyToMembers = true;
            RefrSelection();
        }
        private void ApplyUnchecked(object sender, RoutedEventArgs e)
        {
            if (inChanging) return;
            obj.Settings.ApplyToMembers = false;
            RefrSelection();
        }
        private void InheritChecked(object sender, RoutedEventArgs e)
        {
            if (inChanging) return;
            obj.Settings.Inherit = true;
            RefrSelection();
        }
        private void InheritUnchecked(object sender, RoutedEventArgs e)
        {
            if (inChanging) return;
            obj.Settings.Inherit = false;
            RefrSelection();
        }

        private void OverrideChecked(object sender, RoutedEventArgs e)
        {
            if (inChanging) return;

            var obj = GetObj(asmSel.SelectedItem, true);
            if (obj.Settings == null)
                obj.Settings = CreateSettings(asmSel.SelectedItem, this.obj);

            RefrSelection();
        }
        private void OverrideUnchecked(object sender, RoutedEventArgs e)
        {
            if (inChanging) return;

            var obj = GetObj(asmSel.SelectedItem, false);
            if (obj != null)
                obj.Settings = null;

            RefrSelection();
        }
    }
}
