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
using Mono.Cecil;
using System.IO;
using System.Windows.Forms;
using System.Reflection;

namespace Confuser
{
    /// <summary>
    /// Interaction logic for Settings.xaml
    /// </summary>
    public partial class Settings : Page, IPage<AssemblyDefinition[]>
    {
        RelayCommand simpleCmd;
        RelayCommand advancedCmd;
        public Settings()
        {
            InitializeComponent();
            Style = FindResource(typeof(Page)) as Style;
            simple.Command = simpleCmd = new RelayCommand(CanNext, GoSimple);
            advanced.Command = advancedCmd = new RelayCommand(CanNext, GoAdvanced);
        }

        bool CanNext(object parameter)
        {
            return
                //!string.IsNullOrEmpty(sn.Text) &&
                !string.IsNullOrEmpty(output.Text);
        }

        class AsmData
        {
            public AssemblyDefinition Assembly { get; set; }
            public BitmapSource Icon { get; set; }
            public string Filename { get; set; }
            public string Fullname { get; set; }
        }

        IHost host;
        AssemblyDefinition[] parameter;
        public void Init(IHost host, AssemblyDefinition[] parameter)
        {
            this.host = host;
            this.parameter = parameter;
            List<AsmData> dat = new List<AsmData>();
            foreach (var i in parameter)
                dat.Add(new AsmData() { Assembly = i, Icon = Helper.GetIcon(i.MainModule.FullyQualifiedName), Filename = i.MainModule.FullyQualifiedName, Fullname = i.FullName });
            asmList.ItemsSource = dat;
            output.Text = Path.Combine(Path.GetDirectoryName(parameter[0].MainModule.FullyQualifiedName), "Confused\\");
        }

        private void GoSimple(object parameter)
        {
            host.Go<ConfuserDatas>(new Simple(), new ConfuserDatas()
            {
                Assemblies = this.parameter,
                StrongNameKey = sn.Text,
                OutputPath = output.Text
            });
        }

        private void GoAdvanced(object parameter)
        {
            host.Go<ConfuserDatas>(new AdvSelection(), new ConfuserDatas()
            {
                Assemblies = this.parameter,
                StrongNameKey = sn.Text,
                OutputPath = output.Text
            });
        }

        private void TextChanged(object sender, TextChangedEventArgs e)
        {
            simpleCmd.OnCanExecuteChanged();
        }

        private void OutputSel_Click(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog fd = new FolderBrowserDialog();
            if (fd.ShowDialog() != DialogResult.Cancel)
                output.Text = fd.SelectedPath;
        }
        private void SnSel_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Strong name key file (*.snk)|*.snk|All Files (*.*)|*.*";
            if (ofd.ShowDialog() != DialogResult.Cancel)
                sn.Text = ofd.FileName;
        }
        private void LoadPlugin_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Plugins (*.dll)|*.dll|All Files (*.*)|*.*";
            if (ofd.ShowDialog() != DialogResult.Cancel)
                ConfuserDatas.LoadAssembly(Assembly.LoadFile(ofd.FileName), true);
        }
    }
}
