using Microsoft.Win32;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Confuser.Core;
using System.IO;

namespace Confuser
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window    
	{
		public MainWindow()
		{
			this.InitializeComponent();
		}
		
        void ShowNotice(string txt)
        {
            noticeText.Content = txt;
            mainTabCtrl.Effect = new BlurEffect() { Radius = 2 };
            notice.Visibility = Visibility.Visible;
        }
        void HideNotice()
        {
            mainTabCtrl.Effect = null;
            notice.Visibility = Visibility.Hidden;
            noticeText.Content = "";
        }

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			LoadPluginAssembly(typeof(Confusion).Assembly);
			ShowNotice("Load a assembly to continue...");
		}
		private void About_Loaded(object sender, RoutedEventArgs e)
		{
			ver.Text = "v" + typeof(Confuser.Core.Confuser).Assembly.GetName().Version.ToString();
        }

        AssemblyDefinition asm;
        string path;

        private void load_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog of = new OpenFileDialog();
            of.Filter = "Assembly(*.exe, *.dll)|*.exe;*.dll|All Files|*.*";
            if (of.ShowDialog().GetValueOrDefault())
            {
                ShowNotice("Loading...");
                plugin.IsEnabled = false;
                path = of.FileName;
                output.Text = System.IO.Path.GetDirectoryName(path) + "\\Confused\\" + System.IO.Path.GetFileName(path);
                new Thread(delegate()
                {
                    try
                    {
                        asm = AssemblyDefinition.ReadAssembly(of.OpenFile(), new ReaderParameters(ReadingMode.Immediate));
                    }
                    catch
                    {
                        MessageBox.Show("Not a valid managed assembly!", "Confuser", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    if ((asm.MainModule.Attributes & ModuleAttributes.ILOnly) == 0)
                    {
                        MessageBox.Show("Mixed mode assembly not supported!");
                        return;
                    }

                    this.Dispatcher.Invoke(new Action<AssemblyDefinition>(InitalizeAssembly), asm);
                }).Start();
            }
        }
		private void close_Click(object sender, RoutedEventArgs e)
        {
            asmTab.IsSelected = true;
            CloseAssembly();
            ShowNotice("Load a assembly to continue...");
        }
        private void exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] file = e.Data.GetData(DataFormats.FileDrop, true) as string[];
                if (file.Length == 1)
                {
                    asmTab.IsSelected = true;
                    CloseAssembly();

                    ShowNotice("Loading...");
                    plugin.IsEnabled = false;
                    path = file[0];
                    output.Text = System.IO.Path.GetDirectoryName(path) + "\\Confused\\" + System.IO.Path.GetFileName(path);
                    new Thread(delegate()
                    {
                        try
                        {
                            asm = AssemblyDefinition.ReadAssembly(path, new ReaderParameters(ReadingMode.Immediate));
                        }
                        catch
                        {
                            MessageBox.Show("Not a valid managed assembly!", "Confuser", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                        if ((asm.MainModule.Attributes & ModuleAttributes.ILOnly) == 0)
                        {
                            MessageBox.Show("Mixed mode assembly not supported!");
                            return;
                        }

                        this.Dispatcher.Invoke(new Action<AssemblyDefinition>(InitalizeAssembly), asm);
                    }).Start();
                }
            }
        }

        private void output_sel_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog sf = new SaveFileDialog();
            sf.Filter = "Assembly(*.exe, *.dll)|*.exe;*.dll|All Files|*.*";
            sf.FileName = path;
            if (sf.ShowDialog().GetValueOrDefault())
                output.Text = sf.FileName;
        }
        private void doConfuse_Click(object sender, RoutedEventArgs e)
        {
            MemoryStream tempCfg = new MemoryStream();
            SaveConfig(tempCfg);

            FileStream dst;
            try
            {
                if (!Directory.Exists(System.IO.Path.GetDirectoryName(output.Text)))
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(output.Text));
                dst = File.Create(output.Text);
            }
            catch
            {
                MessageBox.Show("Cannot access the output path!", "Confuser", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            FileStream src;
            try
            {
                src = File.OpenRead(path);
            }
            catch
            {
                MessageBox.Show("Cannot read the input assembly!", "Confuser", MessageBoxButton.OK, MessageBoxImage.Error);
                dst.Dispose();
                return;
            }

            Confuser.Core.Confuser cr = new Confuser.Core.Confuser();
            List<ConfusionParameter> parameters = new List<ConfusionParameter>();
            for (int i = 1; i < mainTabCtrl.Items.Count - 2; i++)
            {
                Grid grid = (mainTabCtrl.Items[i] as TabItem).Content as Grid;
                CheckBox enable = LogicalTreeHelper.FindLogicalNode(grid, "enable") as CheckBox;
                if (!enable.IsChecked.GetValueOrDefault()) continue;

                Confusion cion = grid.DataContext as Confusion;
                Border border = LogicalTreeHelper.FindLogicalNode(grid, "setting") as Border;

                IMemberDefinition[] defs;
                if (border.Child is AssemblyElementPicker)
                    defs = (border.Child as AssemblyElementPicker).GetSelections();
                else
                    defs = null;

                ConfusionParameter para = new ConfusionParameter();
                para.Confusion = cion;
                para.Targets = defs;
                parameters.Add(para);
            }
            cr.CompressOutput = compress.IsChecked.GetValueOrDefault();

            cr.Fault += new ExceptionEventHandler(delegate
            {
                this.Dispatcher.Invoke(new Action(delegate
                {
                    MessageBox.Show("Fault error occured!\r\nSee log for details.", "Confuser", MessageBoxButton.OK, MessageBoxImage.Error);
                }), null);
            });

            cr.Log += new LogEventHandler(delegate(object sdr, LogEventArgs ee)
            {
                this.Dispatcher.BeginInvoke(new Action(delegate
                {
                    log.AppendText(ee.Message + "\r\n");
                }), DispatcherPriority.Render, null);
            });

            cr.Finish += new EventHandler(delegate
            {
                src.Dispose(); dst.Dispose();
                this.Dispatcher.Invoke(new Action(delegate
                {
                    try
                    {
                        mainTabCtrl.IsHitTestVisible = true;
                        tempCfg.Position = 0;
                        CloseAssembly();
                        LoadConfig(tempCfg);
                    }
                    catch { }
                }), null);
            });

            mainTabCtrl.IsHitTestVisible = false;
            Confuse.IsHitTestVisible = true;
            log.Clear();

            cr.ConfuseAsync(asm, dst, parameters.ToArray());
        }

        internal List<System.Reflection.Assembly> Plugs = new List<System.Reflection.Assembly>();
        internal void LoadPluginAssembly(System.Reflection.Assembly asm)
        {
            if (Plugs.Contains(asm)) return;
            Plugs.Add(asm);
            foreach (Type i in asm.GetTypes())
            {
                if (i.IsSubclassOf(typeof(Confusion)) && !i.IsAbstract)
                {
                    Confusion c = Activator.CreateInstance(i) as Confusion;
                    TabItem tab = new TabItem();
                    tab.Header = c.Name;

                    Grid pnl = new Grid();

                    TextBlock tmp = new TextBlock() { FontSize = 14, FontWeight = FontWeights.Bold, Margin = new Thickness(5, 5, 0, 0), Height = 20, VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Stretch, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.White };
                    tmp.SetBinding(TextBlock.TextProperty, new Binding("Name"));
                    pnl.Children.Add(tmp);

                    tmp = new TextBlock() { FontSize = 12, Margin = new Thickness(10, 30, 0, 0), VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Stretch, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.White };
                    tmp.SetBinding(TextBlock.TextProperty, new Binding("Description"));
                    pnl.Children.Add(tmp);

                    CheckBox enable = new CheckBox() { Name = "enable", Margin = new Thickness(5, 80, 0, 0), Content = "Enabled", Height = 20, VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Left };
                    pnl.Children.Add(enable);
                    Border setting = new Border() { Name = "setting", BorderThickness = new Thickness(2), BorderBrush = Brushes.White, CornerRadius = new CornerRadius(5), Margin = new Thickness(5, 100, 5, 35) };
                    setting.SetBinding(Border.IsEnabledProperty, new Binding() { Path = new PropertyPath(CheckBox.IsCheckedProperty), Source = enable });
                    pnl.Children.Add(setting);
                    pnl.Children.Add(new Label() { Name = "warn", FontSize = 12, FontWeight = FontWeights.Bold, Margin = new Thickness(5, 0, 0, 5), Foreground = Brushes.Red, Height = 20, VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Left });

                    pnl.DataContext = c;
                    tab.Content = pnl;
                    mainTabCtrl.Items.Insert(mainTabCtrl.Items.Count - 2, tab);
                }
            }
        }
        void InitalizeAssembly(AssemblyDefinition asm)
        {
            string kind;
            switch (asm.MainModule.Kind)
            {
                case ModuleKind.Console:
                    kind = "Assembly Kind : Console Application"; break;
                case ModuleKind.Dll:
                    kind = "Assembly Kind : Library"; break;
                case ModuleKind.Windows:
                    kind = "Assembly Kind : Windows Application"; break;
                default:
                    kind = "Assembly Kind : Unknown"; break;
            }
            string rt;
            switch (asm.MainModule.Runtime)
            {
                case TargetRuntime.Net_1_0:
                    rt = "Assembly Runtime : .NET Framework v1.0"; break;
                case TargetRuntime.Net_1_1:
                    rt = "Assembly Runtime : .NET Framework v1.1"; break;
                case TargetRuntime.Net_2_0:
                    rt = "Assembly Runtime : .NET Framework v2.0"; break;
                case TargetRuntime.Net_4_0:
                    rt = "Assembly Runtime : .NET Framework v4.0"; break;
                default:
                    rt = "Assembly Runtime : Unknown"; break;
            }

            this.Dispatcher.Invoke(new Action(delegate
            {
                ShowNotice("Loading...");
                asmInfo.Visibility = Visibility.Visible;
                asmName.Text = asm.Name.ToString();
                asmKind.Content = kind;
                asmRt.Content = rt;
            }), null);

            for (int i = 1; i < mainTabCtrl.Items.Count - 2; i++)
            {
                int ii = i;
                this.Dispatcher.BeginInvoke(new Action(delegate
                {
                    Grid panel = (mainTabCtrl.Items[ii] as TabItem).Content as Grid;

                    Confusion c = panel.DataContext as Confusion;
                    Label lbl = LogicalTreeHelper.FindLogicalNode(panel, "warn") as Label;
                    Border border = LogicalTreeHelper.FindLogicalNode(panel, "setting") as Border;

                    if (!c.StandardCompatible)
                    {
                        lbl.Content = "***WARNING: Assembly will become unverifiable after apply this confusion.***";
                    }
                    if (c.Target != Target.Whole)
                    {
                        AssemblyElementPicker picker = new AssemblyElementPicker();
                        picker.LoadAssembly(asm, c.Target);
                        picker.Margin = new Thickness(0);
                        picker.VerticalAlignment = VerticalAlignment.Stretch;
                        picker.HorizontalAlignment = HorizontalAlignment.Stretch;
                        border.Child = picker;
                    }
                    else
                    {
                        Label n = new Label();
                        n.FontSize = 10;
                        n.Margin = new Thickness(0);
                        n.VerticalAlignment = VerticalAlignment.Stretch;
                        n.HorizontalAlignment = HorizontalAlignment.Stretch;
                        n.VerticalContentAlignment = VerticalAlignment.Center;
                        n.HorizontalContentAlignment = HorizontalAlignment.Center;
                        n.Content = "This confusion is apply to the whole assembly therefore no settings are available.";
                        border.Child = n;
                    }
                }), System.Windows.Threading.DispatcherPriority.Render, null);
            }
            this.Dispatcher.BeginInvoke(new Action(delegate
            {
                load.IsEnabled = false;
                open.IsEnabled = false;
                close.IsEnabled = true;
                save.IsEnabled = true;
                plugin.IsEnabled = true;
                HideNotice();
            }), System.Windows.Threading.DispatcherPriority.Render, null);
        }
        void CloseAssembly()
        {
            this.Dispatcher.Invoke(new Action(delegate
            {
                asm = null;
                load.IsEnabled = true;
                open.IsEnabled = true;
                close.IsEnabled = false;
                save.IsEnabled = false;
                plugin.IsEnabled = true;
                asmInfo.Visibility = Visibility.Hidden;
            }), null);
        }
        void LoadConfig(Stream str)
        {
            Configuration cfg = new Configuration(this);
            cfg.Load(str);
            asm = cfg.Assembly;
            InitalizeAssembly(asm);
            path = cfg.Path;
            compress.IsChecked = cfg.Compress;

            this.Dispatcher.BeginInvoke(new Action(delegate
            {
                for (int i = 1; i < mainTabCtrl.Items.Count - 2; i++)
                {
                    Grid grid = (mainTabCtrl.Items[i] as TabItem).Content as Grid;

                    Confusion cion = grid.DataContext as Confusion;
                    (LogicalTreeHelper.FindLogicalNode(grid, "enable") as CheckBox).IsChecked = cfg.Parameters.ContainsKey(cion.GetType().FullName);
                    if (!cfg.Parameters.ContainsKey(cion.GetType().FullName)) continue;

                    Border border = LogicalTreeHelper.FindLogicalNode(grid, "setting") as Border;
                    if (border.Child is AssemblyElementPicker)
                        (border.Child as AssemblyElementPicker).SetSelection(cfg.Parameters[cion.GetType().FullName]);
                }
            }), System.Windows.Threading.DispatcherPriority.Render, null);
        }
        void SaveConfig(Stream str)
        {
            Configuration cfg = new Configuration(this);
            cfg.Assembly = asm;
            cfg.Path = path;
            cfg.Compress = compress.IsChecked.GetValueOrDefault();

            for (int i = 1; i < mainTabCtrl.Items.Count - 2; i++)
            {
                Grid grid = (mainTabCtrl.Items[i] as TabItem).Content as Grid;
                CheckBox enable = LogicalTreeHelper.FindLogicalNode(grid, "enable") as CheckBox;
                if (!enable.IsChecked.GetValueOrDefault()) continue;

                Confusion cion = grid.DataContext as Confusion;
                Border border = LogicalTreeHelper.FindLogicalNode(grid, "setting") as Border;

                if (border.Child is AssemblyElementPicker)
                {
                    IMemberDefinition[] defs = (border.Child as AssemblyElementPicker).GetSelections();
                    cfg.Parameters.Add(cion.GetType().FullName, defs);
                }
                else
                {
                    cfg.Parameters.Add(cion.GetType().FullName, new IMemberDefinition[0]);
                }
            }
            cfg.Save(str);
        }

        private void plugin_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog of = new OpenFileDialog();
            of.Filter = "Assembly(*.exe, *.dll)|*.exe;*.dll";
            if (of.ShowDialog().GetValueOrDefault())
            {
                try
                {
                    var plug = System.Reflection.Assembly.LoadFile(of.FileName);
                    LoadPluginAssembly(plug);
                }
                catch
                {
                    MessageBox.Show("Cannot load the plugin!", "Confuser", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        private void open_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog of = new OpenFileDialog();
            of.Filter = "Configuration(*.kfg)|*.kfg";
            if (of.ShowDialog().GetValueOrDefault())
            {
                try
                {
                    LoadConfig(of.OpenFile());
                }
                catch
                {
                    MessageBox.Show("Cannot load the configration!", "Confuser", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        private void save_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog sf = new SaveFileDialog();
            sf.Filter = "Configuration(*.kfg)|*.kfg";
            if (sf.ShowDialog().GetValueOrDefault())
            {
                try
                {
                    SaveConfig(sf.OpenFile());
                }
                catch
                {
                    MessageBox.Show("Cannot save the configration!", "Confuser", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}