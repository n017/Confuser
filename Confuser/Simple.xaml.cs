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
    public partial class Simple : Page, IPage<ConfuserDatas>
    {
        public Simple()
        {
            InitializeComponent();
            Style = FindResource(typeof(Page)) as Style;
            src = new CollectionViewSource() { Source = ConfuserDatas.Confusions };
            src.Filter += ConfusionsFilter;
            src.SortDescriptions.Add(new SortDescription("Preset", ListSortDirection.Ascending));
            src.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));
            cnList.ItemsSource = src.View;
            preset.SelectionChanged += (sender, e) =>
            {
                src.View.Refresh();
                cnList.ItemsSource = src.View;
            };
        }
        CollectionViewSource src;

        IHost host;
        ConfuserDatas parameter;
        public void Init(IHost host, ConfuserDatas parameter)
        {
            this.host = host;
            this.parameter = parameter;
        }

        void ConfusionsFilter(object sender, FilterEventArgs e)
        {
            IConfusion item = e.Item as IConfusion;
            if (item.Preset <= (Preset)preset.SelectedValue)
            {
                e.Accepted = true;
            }
            else
            {
                e.Accepted = false;
            }
        }

        Preset _preset;
        Packer _packer;
        ConfuserDatas LoadSummary()
        {
            StringBuilder summary = new StringBuilder();
            summary.AppendLine(string.Format("Output path: {0}", parameter.OutputPath));
            if (string.IsNullOrEmpty(parameter.StrongNameKey))
                summary.AppendLine("No strong name key specified.");
            else
                summary.AppendLine(string.Format("Strong name key: {0}", parameter.StrongNameKey));
            summary.AppendLine();

            parameter.Parameter = new ConfuserParameter();
            summary.AppendLine(string.Format("Protection preset: {0}", parameter.Parameter.DefaultPreset = _preset));
            SimpleMarker mkr = new SimpleMarker();
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
            _preset = (Preset)preset.SelectedValue;
            if (usePacker.IsChecked.GetValueOrDefault())
                _packer = (Packer)packer.SelectedValue;
            else
                _packer = null;
            host.Load<ConfuserDatas>(LoadSummary, new Summary());
        }
    }

    class SimpleMarker : Marker
    {
        public Packer packer;
        public override MarkerSetting MarkAssemblies(IList<AssemblyDefinition> asms, Preset preset, Confuser.Core.Confuser cr, EventHandler<LogEventArgs> err)
        {
            var ret = base.MarkAssemblies(asms, preset, cr, err);
            ret.Packer = packer;
            return ret;
        }
    }
}
