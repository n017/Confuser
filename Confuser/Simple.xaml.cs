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
using Confuser.Core;
using System.ComponentModel;
using Mono.Cecil;

namespace Confuser
{
    /// <summary>
    /// Interaction logic for Simple.xaml
    /// </summary>
    public partial class Simple : ConfuserTab, IPage
    {
        static Simple()
        {
            TitlePropertyKey.OverrideMetadata(typeof(Simple), new UIPropertyMetadata("Basic settings"));
        }
        public Simple()
        {
            InitializeComponent();
            Style = FindResource(typeof(ConfuserTab)) as Style;

        }
        CollectionViewSource src;

        IHost host;
        PrjPreset prevPreset;
        public override void Init(IHost host)
        {
            this.host = host;

            //=_=||
            preset.ApplyTemplate();
            TextBox tb = preset.Template.FindName("PART_EditableTextBox", preset) as TextBox;
            tb.IsEnabled = false;
            tb.IsHitTestVisible = false;
        }
        public override void InitProj()
        {
            src = new CollectionViewSource() { Source = host.Project.Confusions };
            src.Filter += ConfusionsFilter;
            src.SortDescriptions.Add(new SortDescription("Preset", ListSortDirection.Ascending));
            src.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));
            cnList.ItemsSource = src.View;
            prevPreset = host.Project.DefaultPreset;
            host.Project.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == "DefaultPreset")
                {
                    if (prevPreset == PrjPreset.Undefined && host.Project.DefaultPreset != PrjPreset.Undefined)
                    {
                        if (MessageBox.Show(
        @"Are you sure to change the default preset?
If you do so, you will lose the changes you did at Advanced tab!", "Confuser",
         MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                        {
                            Dispatcher.BeginInvoke(new Action(
                                () => host.Project.DefaultPreset = PrjPreset.Undefined));
                        }
                        else
                        {
                            foreach (var i in host.Project.Assemblies)
                            {
                                i.Settings = null;
                                foreach (var j in i.Assembly.Modules)
                                    foreach (var k in AsmSelector.Childer.GetChildren(j))
                                        k.Annotations.Clear();
                                i.Clear();
                            }
                            src.View.Refresh();
                            cnList.ItemsSource = src.View;
                        }
                    }
                    else
                    {
                        src.View.Refresh();
                        cnList.ItemsSource = src.View;
                    }
                    prevPreset = host.Project.DefaultPreset;
                }
            };

            this.DataContext = host.Project;
        }

        void ConfusionsFilter(object sender, FilterEventArgs e)
        {
            if (host.Project.DefaultPreset == PrjPreset.Undefined)
            {
                e.Accepted = false;
                return;
            }
            IConfusion item = e.Item as IConfusion;
            if (item.Preset <= (Preset)host.Project.DefaultPreset)
            {
                e.Accepted = true;
            }
            else
            {
                e.Accepted = false;
            }
        }
    }
}
