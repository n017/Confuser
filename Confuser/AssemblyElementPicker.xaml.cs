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

namespace Confuser
{
    /// <summary>
    /// Interaction logic for AssemblyElementPicker.xaml
    /// </summary>
    public partial class AssemblyElementPicker
    {
        public AssemblyElementPicker()
        {
            this.InitializeComponent();
        }

        StackPanel AddIcon(ImageSource img, UIElement hdr)
        {
            StackPanel ret = new StackPanel() { Orientation = Orientation.Horizontal };
            Image image = new Image() { Source = img, Width = 16, Height = 16 };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
            ret.Children.Add(image);
            ret.Children.Add(hdr);
            return ret;
        }

        UIElement GetHeader(string imgId, string txt, bool isPublic)
        {
            UIElement txtEle;
            txtEle = new TextBlock() { Text = txt };
            if (!isPublic)
                (txtEle as TextBlock).Foreground = Brushes.DimGray;

            return AddIcon(FindResource(imgId) as ImageSource, txtEle);
        }

        void SetCMenu(TreeViewItem item)
        {
            item.ContextMenu = this.FindResource("selMenu") as ContextMenu;
            item.AddHandler(TreeViewItem.MouseRightButtonUpEvent, new RoutedEventHandler(menu_Click));
        }

        Dictionary<IMemberDefinition, TreeViewItem> dict;

        public void LoadAssembly(AssemblyDefinition asm)
        {
            dict = new Dictionary<IMemberDefinition, TreeViewItem>();

            asmViewer.BeginInit();

            asmViewer.Items.Clear();
            TreeViewItem item = new TreeViewItem();
            item.Header = GetHeader("asm", asm.Name.Name, true);
            item.Tag = asm;
            SetCMenu(item);
            PopulateModule(item, asm.MainModule);
            asmViewer.Items.Add(item);

            asmViewer.EndInit();
        }

        void PopulateModule(TreeViewItem asm, ModuleDefinition mod)
        {
            TreeViewItem item = new TreeViewItem();
            item.Header = GetHeader("mod", mod.Name, true);
            item.Tag = mod;
            SetCMenu(item);

            SortedDictionary<string, TreeViewItem> nss = new SortedDictionary<string, TreeViewItem>();
            foreach (TypeDefinition t in mod.Types)
            {
                if (!nss.ContainsKey(t.Namespace))
                {
                    nss.Add(t.Namespace, new TreeViewItem() { Header = AddIcon(FindResource("ns") as ImageSource, new TextBlock() { Text = t.Namespace == "" ? "-" : t.Namespace }) });
                }
            }

            foreach (TypeDefinition type in mod.Types)
            {
                PopulateType(nss[type.Namespace], type);
            }

            foreach (TreeViewItem ns in nss.Values)
            {
                SetCMenu(ns);
                item.Items.Add(ns);
            }

            asm.Items.Add(item);
        }

        void PopulateType(TreeViewItem par, TypeDefinition type)
        {
            TreeViewItem item = new TreeViewItem();

            string img = "";
            if (type.IsEnum) img = "enum";
            else if (type.IsInterface) img = "iface";
            else if (type.IsValueType) img = "vt";
            else if (type.BaseType != null && (type.BaseType.Name == "Delegate" || type.BaseType.Name == "MultiCastDelegate")) img = "dele";
            else img = "type";
            item.Header = GetHeader(img, type.Name, type.IsPublic || type.IsNestedPublic);

            item.Tag = type;
            SetCMenu(item);

            foreach (TypeDefinition nested in type.NestedTypes)
            {
                PopulateType(item, nested);
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
                TreeViewItem mtdItem = new TreeViewItem();
                mtdItem.Header = GetHeader(mtd.IsConstructor ? "ctor" : "mtd", mtdName.ToString(), mtd.IsPublic);
                mtdItem.Tag = mtd;
                SetCMenu(mtdItem);
                item.Items.Add(mtdItem);
                dict[mtd] = mtdItem;
            }

            foreach (PropertyDefinition prop in from prop in type.Properties orderby prop.Name select prop)
            {
                StringBuilder propName = new StringBuilder();
                propName.Append(prop.Name);
                propName.Append(" : ");
                propName.Append(prop.PropertyType.Name);
                TreeViewItem propItem = new TreeViewItem();
                propItem.Header = GetHeader("prop", propName.ToString(), true);
                propItem.Tag = prop;
                SetCMenu(propItem);
                item.Items.Add(propItem);
                dict[prop] = propItem;
            }

            foreach (EventDefinition evt in from evt in type.Events orderby evt.Name select evt)
            {
                StringBuilder evtName = new StringBuilder();
                evtName.Append(evt.Name);
                evtName.Append(" : ");
                evtName.Append(evt.EventType.Name);
                TreeViewItem evtItem = new TreeViewItem();
                evtItem.Header = GetHeader("evt", evtName.ToString(), true);
                evtItem.Tag = evt;
                SetCMenu(evtItem);
                item.Items.Add(evtItem);
                dict[evt] = evtItem;
            }

            foreach (FieldDefinition fld in from fld in type.Fields orderby fld.Name select fld)
            {
                StringBuilder fldName = new StringBuilder();
                fldName.Append(fld.Name);
                fldName.Append(" : ");
                fldName.Append(fld.FieldType.Name);
                TreeViewItem fldItem = new TreeViewItem();
                fldItem.Header = GetHeader("fld", fldName.ToString(), fld.IsPublic);
                fldItem.Tag = fld;
                SetCMenu(fldItem);
                item.Items.Add(fldItem);
                dict[fld] = fldItem;
            }

            par.Items.Add(item);
            dict[type] = item;
        }

        void menu_Click(object sender, RoutedEventArgs e)
        {
            (sender as TreeViewItem).ContextMenu.Tag = sender;
            (sender as TreeViewItem).ContextMenu.IsOpen = true;
            e.Handled = true;
        }

        private void expAll_Click(object sender, RoutedEventArgs e)
        {
            TreeViewItem item = ((sender as MenuItem).Parent as ContextMenu).Tag as TreeViewItem;
            Expand(item, true);
        }

        private void colAll_Click(object sender, RoutedEventArgs e)
        {
            TreeViewItem item = ((sender as MenuItem).Parent as ContextMenu).Tag as TreeViewItem;
            Expand(item, false);
        }

        private void Expand(TreeViewItem item, bool val)
        {
            item.IsExpanded = val;
            foreach (TreeViewItem c in item.Items)
                Expand(c, val);
        }
    }
}