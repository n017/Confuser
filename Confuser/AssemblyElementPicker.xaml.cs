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
using Confuser.Core;
using System.ComponentModel;

namespace Confuser
{
    /// <summary>
    /// Interaction logic for AssemblyElementPicker.xaml
    /// </summary>
    public partial class AssemblyElementPicker
    {
        internal class TreeNodeViewModel : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            void OnPropertyChanged(string prop)
            {
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs(prop));
            }

            object obj;
            public object Object
            {
                get { return obj; }
                set
                {
                    obj = value;
                    OnPropertyChanged("Object");
                }
            }

            Brush bg;
            public Brush Foreground
            {
                get { return bg; }
                set
                {
                    bg = value;
                    OnPropertyChanged("Foreground");
                }
            }

            ImageSource ico;
            public ImageSource Icon
            {
                get { return ico; }
                set
                {
                    ico = value;
                    OnPropertyChanged("Icon");
                }
            }

            string hdr;
            public string Header
            {
                get { return hdr; }
                set
                {
                    hdr = value;
                    OnPropertyChanged("Header");
                }
            }

            ContextMenu cMenu;
            public ContextMenu ContextMenu
            {
                get { return cMenu; }
                set
                {
                    cMenu = value;
                    OnPropertyChanged("ContextMenu");
                }
            }

            TreeNodeViewModel[] children;
            public TreeNodeViewModel[] Children
            {
                get { return children; }
                set
                {
                    children = value;
                    OnPropertyChanged("Children");
                }
            }

            bool isExpanded;
            public bool IsExpanded
            {
                get { return isExpanded; }
                set
                {
                    if (isExpanded != value)
                    {
                        isExpanded = value;
                        OnPropertyChanged("IsExpanded");
                        if (value && Children == Loading)
                            if (LoadChildren != null) LoadChildren(this);
                    }
                    else isExpanded = value;
                }
            }

            bool isLoaded = false;
            public event Action<TreeNodeViewModel> LoadChildren;

            static TreeNodeViewModel[] Loading = new[] {
                new TreeNodeViewModel()
                {
                    Children = new TreeNodeViewModel[0],
                    Header = "Loading...",
                    Foreground = Brushes.White
                }
            };
            static TreeNodeViewModel[] NoChildren = new TreeNodeViewModel[0];

            public TreeNodeViewModel()
            {
                if (Loading == null) isLoaded = true;
                else
                    Children = Loading;
            }
            public TreeNodeViewModel(bool hasChildren)
                : this()
            {
                if (!hasChildren) Children = new TreeNodeViewModel[0];
            }
        }

        public AssemblyElementPicker()
        {
            this.InitializeComponent();
        }

        ImageSource GetIcon(string imgId)
        {
            return FindResource(imgId) as ImageSource;
        }

        void SetCMenu(TreeNodeViewModel vm)
        {
            vm.ContextMenu = this.FindResource("selMenu") as ContextMenu;
        }


        public void ClearAssemblies()
        {
            asmViewer.Items.Clear();
        }

        public void LoadAssembly(AssemblyDefinition asm)
        {
            asmViewer.BeginInit();

            TreeNodeViewModel vm = new TreeNodeViewModel()
            {
                Icon = GetIcon("asm"),
                Foreground = Brushes.WhiteSmoke,
                Header = asm.Name.Name,
                Object = asm
            };
            SetCMenu(vm);
            vm.LoadChildren += _ => PopulateModules(_, (AssemblyDefinition)_.Object);
            asmViewer.Items.Add(vm);

            asmViewer.EndInit();
        }

        void PopulateModules(TreeNodeViewModel p, AssemblyDefinition asm)
        {
            List<TreeNodeViewModel> items = new List<TreeNodeViewModel>();
            foreach (var mod in asm.Modules)
            {
                TreeNodeViewModel vm = new TreeNodeViewModel()
                {
                    Icon = GetIcon("mod"),
                    Foreground = Brushes.WhiteSmoke,
                    Header = mod.Name,
                    Object = mod
                };
                SetCMenu(vm);
                vm.LoadChildren += _ => PopulateNamespaces(_, (ModuleDefinition)_.Object);
                items.Add(vm);
            }
            p.Children = items.ToArray();
        }

        void PopulateNamespaces(TreeNodeViewModel p, ModuleDefinition mod)
        {
            SortedDictionary<string, TreeNodeViewModel> nss = new SortedDictionary<string, TreeNodeViewModel>();
            Dictionary<string, List<TypeDefinition>> typeDefs = new Dictionary<string, List<TypeDefinition>>();
            foreach (TypeDefinition t in mod.Types)
            {
                if (!nss.ContainsKey(t.Namespace))
                {
                    typeDefs.Add(t.Namespace, new List<TypeDefinition>());
                    TreeNodeViewModel vm = new TreeNodeViewModel()
                    {
                        Icon = GetIcon("ns"),
                        Foreground = Brushes.WhiteSmoke,
                        Header = t.Namespace == "" ? "-" : t.Namespace,
                        Object = typeDefs[t.Namespace]
                    };
                    SetCMenu(vm);
                    vm.LoadChildren += _ => PopulateTypes(_, (List<TypeDefinition>)_.Object);
                    nss.Add(t.Namespace, vm);
                }
                typeDefs[t.Namespace].Add(t);
            }
            p.Children = nss.Values.ToArray();
        }

        void PopulateTypes(TreeNodeViewModel p, List<TypeDefinition> ns)
        {
            List<TreeNodeViewModel> items = new List<TreeNodeViewModel>();
            foreach (var type in ns)
            {
                string img = "";
                if (type.IsEnum) img = "enum";
                else if (type.IsInterface) img = "iface";
                else if (type.IsValueType) img = "vt";
                else if (type.BaseType != null && (type.BaseType.Name == "Delegate" || type.BaseType.Name == "MultiCastDelegate")) img = "dele";
                else img = "type";

                TreeNodeViewModel vm = new TreeNodeViewModel()
                {
                    Icon = GetIcon(img),
                    Foreground = type.IsPublic || type.IsNestedPublic ? Brushes.WhiteSmoke : Brushes.LightGray,
                    Header = type.Name,
                    Object = type
                };
                SetCMenu(vm);
                vm.LoadChildren += _ => PopulateMembers(_, (TypeDefinition)_.Object);
                items.Add(vm);
            }
            p.Children = items.ToArray();
        }

        void PopulateMembers(TreeNodeViewModel p, TypeDefinition type)
        {
            List<TreeNodeViewModel> items = new List<TreeNodeViewModel>();

            foreach (TypeDefinition nested in type.NestedTypes)
            {
                string img = "";
                if (nested.IsEnum) img = "enum";
                else if (nested.IsInterface) img = "iface";
                else if (nested.IsValueType) img = "vt";
                else if (nested.BaseType != null && (nested.BaseType.Name == "Delegate" || nested.BaseType.Name == "MulticastDelegate")) img = "dele";
                else img = "type";

                TreeNodeViewModel vm = new TreeNodeViewModel()
                {
                    Icon = GetIcon(img),
                    Foreground = nested.IsPublic || nested.IsNestedPublic ? Brushes.WhiteSmoke : Brushes.LightGray,
                    Header = nested.Name,
                    Object = nested
                };
                SetCMenu(vm);
                vm.LoadChildren += _ => PopulateMembers(_, (TypeDefinition)_.Object);
                items.Add(vm);
            }

            foreach (MethodDefinition mtd in from mtd in type.Methods orderby !mtd.IsConstructor select mtd)
            {
                StringBuilder mtdName = new StringBuilder();
                mtdName.Append(mtd.Name);
                mtdName.Append("(");
                for (int i = 0; i < mtd.Parameters.Count; i++)
                {
                    ParameterDefinition param = mtd.Parameters[i];
                    if (i > 0)
                    {
                        mtdName.Append(", ");
                    }
                    if (param.ParameterType.IsSentinel)
                    {
                        mtdName.Append("..., ");
                    }
                    mtdName.Append(param.ParameterType.Name);
                }
                mtdName.Append(") : ");
                mtdName.Append(mtd.ReturnType.Name);

                TreeNodeViewModel vm = new TreeNodeViewModel(false)
                {
                    Icon = GetIcon(mtd.IsConstructor ? "ctor" : "mtd"),
                    Foreground = mtd.IsPublic ? Brushes.WhiteSmoke : Brushes.LightGray,
                    Header = mtdName.ToString(),
                    Object = mtd
                };
                SetCMenu(vm);
                items.Add(vm);
            }

            foreach (PropertyDefinition prop in from prop in type.Properties orderby prop.Name select prop)
            {
                StringBuilder propName = new StringBuilder();
                propName.Append(prop.Name);
                propName.Append(" : ");
                propName.Append(prop.PropertyType.Name);

                TreeNodeViewModel vm = new TreeNodeViewModel(false)
                {
                    Icon = GetIcon("prop"),
                    Foreground = true ? Brushes.WhiteSmoke : Brushes.LightGray,
                    Header = propName.ToString(),
                    Object = prop
                };
                SetCMenu(vm);
                items.Add(vm);
            }

            foreach (EventDefinition evt in from evt in type.Events orderby evt.Name select evt)
            {
                StringBuilder evtName = new StringBuilder();
                evtName.Append(evt.Name);
                evtName.Append(" : ");
                evtName.Append(evt.EventType.Name);

                TreeNodeViewModel vm = new TreeNodeViewModel(false)
                {
                    Icon = GetIcon("evt"),
                    Foreground = true ? Brushes.WhiteSmoke : Brushes.LightGray,
                    Header = evtName.ToString(),
                    Object = evt
                };
                SetCMenu(vm);
                items.Add(vm);
            }

            foreach (FieldDefinition fld in from fld in type.Fields orderby fld.Name select fld)
            {
                StringBuilder fldName = new StringBuilder();
                fldName.Append(fld.Name);
                fldName.Append(" : ");
                fldName.Append(fld.FieldType.Name);

                TreeNodeViewModel vm = new TreeNodeViewModel(false)
                {
                    Icon = GetIcon("fld"),
                    Foreground = fld.IsPublic ? Brushes.WhiteSmoke : Brushes.LightGray,
                    Header = fldName.ToString(),
                    Object = fld
                };
                SetCMenu(vm);
                items.Add(vm);
            }

            p.Children = items.ToArray();
        }

        private void expAll_Click(object sender, RoutedEventArgs e)
        {
            TreeViewItem item = ((sender as MenuItem).Parent as ContextMenu).PlacementTarget as TreeViewItem;
            Expand(item.DataContext as TreeNodeViewModel, true);
        }

        private void colAll_Click(object sender, RoutedEventArgs e)
        {
            TreeViewItem item = ((sender as MenuItem).Parent as ContextMenu).PlacementTarget as TreeViewItem;
            Expand(item.DataContext as TreeNodeViewModel, false);
        }

        private void Expand(TreeNodeViewModel vm, bool val)
        {
            vm.IsExpanded = val;
            foreach (TreeNodeViewModel c in vm.Children)
                Expand(c, val);
        }
    }
}