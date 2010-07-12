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
		
		UIElement GetHeader(string imgId, string txt, bool isCb, bool isPublic)
		{
			UIElement txtEle;
			if(isCb)
			{
				txtEle = new CheckBox() { Content = new TextBlock() { Text = txt } };
				if(!isPublic)
					(txtEle as CheckBox).Foreground = Brushes.DimGray;
			}
			else
			{
				txtEle = new TextBlock() { Text = txt };
				if(!isPublic)
					(txtEle as TextBlock).Foreground = Brushes.DimGray;
			}
			return AddIcon(FindResource(imgId) as ImageSource, txtEle);
		}
		
		public void LoadAssembly(AssemblyDefinition asm, Target display)
        {
            asmViewer.BeginInit();

			asmViewer.Items.Clear();
			TreeViewItem item = new TreeViewItem();
			item.Focusable = false;
			item.Header = GetHeader("asm", asm.Name.Name, false, true);
			item.Tag = asm;
			PopulateModule(item, asm.MainModule, display);
            asmViewer.Items.Add(item);

            asmViewer.EndInit();
		}

        void PopulateModule(TreeViewItem asm, ModuleDefinition mod, Target display)
		{
			TreeViewItem item = new TreeViewItem();
			item.Focusable = false;
			item.Header = GetHeader("mod", mod.Name, false, true);
			item.Tag = mod;
			
			Dictionary<string, TreeViewItem> nss = new Dictionary<string, TreeViewItem>();
			foreach(TypeDefinition t in mod.Types)
			{
				if(!nss.ContainsKey(t.Namespace))
				{
					nss.Add(t.Namespace, new TreeViewItem() { Focusable = false, Header = AddIcon(FindResource("ns") as ImageSource, new TextBlock() { Text = t.Namespace == "" ? "-" : t.Namespace } )} );
				}
			}
			
			foreach(TypeDefinition type in mod.Types)
			{
				PopulateType(nss[type.Namespace], type, display);
			}
			
			foreach(TreeViewItem ns in nss.Values)
			{
				item.Items.Add(ns);
			}
			
			asm.Items.Add(item);
		}

        void PopulateType(TreeViewItem par, TypeDefinition type, Target display)
		{
			TreeViewItem item = new TreeViewItem();
			item.Focusable = false;
			
			string img = "";
			if(type.IsEnum) img = "enum";
			else if(type.IsInterface) img = "iface";
			else if(type.IsValueType) img = "vt";
			else if(type.BaseType != null && (type.BaseType.Name == "Delegate" || type.BaseType.Name == "MultiCastDelegate")) img = "dele";
			else img = "type";
            item.Header = GetHeader(img, type.Name, ((display & Target.Types) == Target.Types || (display & Target.All) == Target.All), type.IsPublic || type.IsNestedPublic);
			
			item.Tag = type;

			foreach(TypeDefinition nested in type.NestedTypes)
			{
				PopulateType(item, nested, display);
			}

            if ((display & Target.Methods) == Target.Methods || (display & Target.All) == Target.All)
			{
				foreach(MethodDefinition mtd in from mtd in type.Methods orderby !mtd.IsConstructor select mtd)
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
					mtdItem.Header = GetHeader(mtd.IsConstructor ? "ctor" : "mtd", mtdName.ToString(), true, mtd.IsPublic);
					mtdItem.Tag = mtd;
					mtdItem.Focusable = false;
					item.Items.Add(mtdItem);
				}
			}

            if ((display & Target.Properties) == Target.Properties || (display & Target.All) == Target.All)
			{
				foreach(PropertyDefinition prop in from prop in type.Properties orderby prop.Name select prop)
				{
					StringBuilder propName = new StringBuilder();
					propName.Append(prop.Name);
					propName.Append(" : ");
					propName.Append(prop.PropertyType.Name);
					TreeViewItem propItem = new TreeViewItem();
					propItem.Header = GetHeader("prop", propName.ToString(), true, true);
					propItem.Tag = prop;
					propItem.Focusable = false;
					item.Items.Add(propItem);
				}
			}

            if ((display & Target.Events) == Target.Events || (display & Target.All) == Target.All)
			{
				foreach(EventDefinition evt in from evt in type.Events orderby evt.Name select evt)
				{
					StringBuilder evtName = new StringBuilder();
					evtName.Append(evt.Name);
					evtName.Append(" : ");
					evtName.Append(evt.EventType.Name);
					TreeViewItem evtItem = new TreeViewItem();
					evtItem.Header = GetHeader("evt", evtName.ToString(), true, true);
					evtItem.Tag = evt;
					evtItem.Focusable = false;
					item.Items.Add(evtItem);
				}
			}

            if ((display & Target.Fields) == Target.Fields || (display & Target.All) == Target.All)
			{
				foreach(FieldDefinition fld in from fld in type.Fields orderby fld.Name select fld)
				{
					StringBuilder fldName = new StringBuilder();
					fldName.Append(fld.Name);
					fldName.Append(" : ");
					fldName.Append(fld.FieldType.Name);
					TreeViewItem fldItem = new TreeViewItem();
					fldItem.Header = GetHeader("fld", fldName.ToString(), true, fld.IsPublic);
					fldItem.Tag = fld;
					fldItem.Focusable = false;
					item.Items.Add(fldItem);
				}
			}
			
			par.Items.Add(item);
        }

        private void selAll_Click(object sender, RoutedEventArgs e)
        {
            selAll.ContextMenu.IsOpen = true;
        }

        private void selMem_Click(object sender, RoutedEventArgs e)
        {
            Check(asmViewer.Items[0] as TreeViewItem, true, 0);
        }

        private void selPub_Click(object sender, RoutedEventArgs e)
        {
            Check(asmViewer.Items[0] as TreeViewItem, true, 1);
        }

        private void selInt_Click(object sender, RoutedEventArgs e)
        {
            Check(asmViewer.Items[0] as TreeViewItem, true, 2);
        }

        private void unsel_Click(object sender, RoutedEventArgs e)
        {
            Check(asmViewer.Items[0] as TreeViewItem, false, 0);
        }

        private void Check(TreeViewItem item, bool val, int type)
        {
            CheckBox bx;
            if ((bx = (item.Header as StackPanel).Children[1] as CheckBox) != null)
            {
                if (type == 0)
                    bx.IsChecked = val;
                else if (type == 1 && bx.Foreground != Brushes.DimGray)
                    bx.IsChecked = val;
                else if (type == 2 && bx.Foreground == Brushes.DimGray)
                    bx.IsChecked = val;
            }
            foreach (TreeViewItem c in item.Items)
                Check(c, val, type);
        }

        private void Expand(TreeViewItem item, bool val)
        {
            item.IsExpanded = val;
            foreach (TreeViewItem c in item.Items)
                Expand(c, val);
        }

        public IMemberDefinition[] GetSelections()
        {
            if (asmViewer.Items.Count == 0) return null;
            List<IMemberDefinition> sels = new List<IMemberDefinition>();
            GetSelections(asmViewer.Items[0] as TreeViewItem, sels);
            return sels.ToArray();
        }

        private void GetSelections(TreeViewItem item, List<IMemberDefinition> sels)
        {
            CheckBox bx;
            if ((bx = (item.Header as StackPanel).Children[1] as CheckBox) != null)
                if (bx.IsChecked.GetValueOrDefault())
                    sels.Add(item.Tag as IMemberDefinition);
            foreach (TreeViewItem child in item.Items)
                GetSelections(child, sels);
        }

        private void expAll_Click(object sender, RoutedEventArgs e)
        {
            Expand(asmViewer.Items[0] as TreeViewItem, true);
        }

        private void colAll_Click(object sender, RoutedEventArgs e)
        {
            Expand(asmViewer.Items[0] as TreeViewItem, false);
        }
	}
}