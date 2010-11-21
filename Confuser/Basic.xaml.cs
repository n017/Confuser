using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Markup;
using System.Windows.Threading;
using System.Windows.Interop;

namespace Confuser
{
	/// <summary>
	/// Interaction logic for MainUI.xaml
	/// </summary>
    public partial class Basic : Window,System.Windows.Forms.IWin32Window
    {
        public Basic()
        {
            this.InitializeComponent();
            GIVEmeASSEMBLY();
            assemblyButton.IsChecked = false;
        }

        private void radioChecked(object sender, RoutedEventArgs e)
        {
            RadioButton radio = sender as RadioButton;
            UIElement element = Helper.FindChild<UIElement>(space, (radio.Name).Substring(0, radio.Name.Length - 6).ToLower());
            Storyboard sb = new Storyboard();
            DoubleAnimation ani = new DoubleAnimation(1, new Duration(TimeSpan.FromSeconds(0.25)));
            Storyboard.SetTarget(ani, element);
            Storyboard.SetTargetProperty(ani, new PropertyPath(UIElement.OpacityProperty));
            sb.Children.Add(ani);
            element.Visibility = Visibility.Visible;
            sb.Begin();
        }
        private void radioUnchecked(object sender, RoutedEventArgs e)
        {
            RadioButton radio = sender as RadioButton;
            UIElement element = Helper.FindChild<UIElement>(space, (radio.Name).Substring(0, radio.Name.Length - 6).ToLower());
            Storyboard sb = new Storyboard();
            DoubleAnimation ani = new DoubleAnimation(0, new Duration(TimeSpan.FromSeconds(0.25)));
            Storyboard.SetTarget(ani, element);
            Storyboard.SetTargetProperty(ani, new PropertyPath(UIElement.OpacityProperty));
            ani.Completed += new EventHandler(delegate { element.Visibility = Visibility.Hidden; });
            sb.Children.Add(ani);
            sb.Begin();
        }

        private void GIVEmeASSEMBLY()
        {
            confuseButton.IsEnabled = false;
            message.Visibility = Visibility.Visible;
            loading.Visibility = Visibility.Hidden;
			bar.IsIndeterminate = false;
            giveME.Visibility = Visibility.Visible;
            assemblyButton.IsChecked = true;
        }
        private void LOADINGassembly()
        {
            confuseButton.IsEnabled = false;
            message.Visibility = Visibility.Hidden;
			bar.IsIndeterminate = true;
            loading.Visibility = Visibility.Visible;
            giveME.Visibility = Visibility.Visible;
            assemblyButton.IsChecked = true;
        }
        private void IgotASSEMBLY()
        {
            giveME.Visibility = Visibility.Hidden;
			bar.IsIndeterminate = false;
            confuseButton.IsEnabled = true;
        }

        void CONFUSING()
        {
            options.IsEnabled = false;
            menu.IsEnabled = false;
            seltor.IsEnabled = false;
        }
        void CONFUSED()
        {
            options.IsEnabled = true;
            menu.IsEnabled = true;
            seltor.IsEnabled = true;
        }

        AssemblyDefinition asm = null;
        string path;
        private void space_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] file = e.Data.GetData(DataFormats.FileDrop, true) as string[];
                if (file.Length == 1)
                {
                    LOADINGassembly();
                    path = file[0];
                    output.Text = System.IO.Path.GetDirectoryName(path) + "\\Confused";
                    new Thread(delegate()
                    {
                        bool ok = true;
                        try
                        {
                            asm = AssemblyDefinition.ReadAssembly(path, new ReaderParameters(ReadingMode.Immediate));
                            if ((asm.MainModule.Attributes & ModuleAttributes.ILOnly) == 0)
                            {
                                MessageBox.Show("Mixed mode assembly not supported!", "Confuser", MessageBoxButton.OK, MessageBoxImage.Error);
                                ok = false;
                            }
                        }
                        catch
                        {
                            MessageBox.Show("Not a valid managed assembly!", "Confuser", MessageBoxButton.OK, MessageBoxImage.Error);
                            ok = false;
                        }
                        this.Dispatcher.BeginInvoke(new Action(delegate
                        {
                            if (asm == null && !ok)
                                GIVEmeASSEMBLY();  //Give me an GOOD assembly!!
                            else
                            {
                                if (ok)
                                {
                                    assembly.DataContext = asm;
                                    asmPath.Text = "Location : " + path;

                                    BitmapImage ico = Helper.GetIcon(path);
									if(ico != null)
									{
										if (ico.Height > 64 && ico.Width > 64) icon.Stretch = Stretch.Uniform; else icon.Stretch = Stretch.None;
										this.icon.Source = ico;
									}
                                }
                                IgotASSEMBLY();
                            }
                        }), null);
                    }).Start();
                }
            }
        }


        public IntPtr Handle
        {
            get { return new WindowInteropHelper(this).Handle; }
        }
        private void browseClick(object sender, RoutedEventArgs e)
        {
        	string id = (sender as Button).Name.Substring(6).ToLower();
			if(id == "sn")
			{
				Microsoft.Win32.OpenFileDialog open = new Microsoft.Win32.OpenFileDialog();
				open.Filter = "Strong Name Key(*.snk)|*.snk";
				if(open.ShowDialog().GetValueOrDefault())
					sn.Text = open.FileName;
			}
            else if (id == "output")
            {
                System.Windows.Forms.FolderBrowserDialog brow = new System.Windows.Forms.FolderBrowserDialog();
                if (brow.ShowDialog(this) == System.Windows.Forms.DialogResult.OK)
                    output.Text = brow.SelectedPath;
            }
        }
        private void exit_Click(object sender, RoutedEventArgs e)
        {   
            Application.Current.Shutdown();
        }
        private void adv_Click(object sender, RoutedEventArgs e)
        {
            Advanced adv = new Advanced();
            Application.Current.MainWindow = adv;
            adv.Left = this.Left; adv.Top = this.Top;
            adv.Visibility = Visibility.Visible;
            this.Close();
        }


        Dictionary<string, Core.IConfusion> ldConfusions = new Dictionary<string, Core.IConfusion>();
        Dictionary<string, Core.Packer> ldPackers = new Dictionary<string, Core.Packer>();
        private void LoadAssembly(Assembly asm)
        {
            foreach (Type type in asm.GetTypes()){
                if (typeof(Core.IConfusion).IsAssignableFrom(type) && type != typeof(Core.IConfusion))
                    ldConfusions.Add(type.FullName, Activator.CreateInstance(type) as Core.IConfusion);
                if (typeof(Core.Packer).IsAssignableFrom(type) && type != typeof(Core.Packer))
                    ldPackers.Add(type.FullName, Activator.CreateInstance(type) as Core.Packer);
            }
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadAssembly(typeof(Core.IConfusion).Assembly);
			confusionList.ItemsSource = ldConfusions.Values;
			packersList.ItemsSource = ldPackers.Values;
        }

        Thread confuser;
        TextBox log;
        ProgressBar pBar;
        private void DoConfuse(object sender, RoutedEventArgs e)
        {
            if ((string)doConfuse.Content == "Cancel")
            {
                confuser.Abort();
                return;
            }

            var param = new Core.ConfuserParameter();
            param.SourceAssembly = path;
            param.DestinationPath = output.Text;
            param.ReferencesPath = System.IO.Path.GetDirectoryName(path);
            param.Confusions = ldConfusions.Values.ToArray();
            param.Packers = ldPackers.Values.ToArray();
            param.DefaultPreset = (Core.Preset)Enum.Parse(typeof(Core.Preset), (preset.SelectedItem as TextBlock).Text);
            param.StrongNameKeyPath = sn.Text;
            param.Logger.BeginPhase += new EventHandler<Core.PhaseEventArgs>((sender1, e1) =>
            {
                this.Dispatcher.Invoke(new Action(delegate
                {
                    Monitor.Enter(this);
                    Border phase = Helper.FindChild<Border>(progress, "phase" + e1.Phase);
                    log = Helper.FindChild<TextBox>(phase, null);
                    if (pBar != null) pBar.Value = 1;
                    pBar = Helper.FindChild<ProgressBar>(phase, null);
                    if (pBar != null) pBar.Value = 0;
                    Storyboard sb = this.Resources["showPhase"] as Storyboard;
                    Storyboard.SetTarget(sb.Children[0], phase);
                    Storyboard.SetTarget(sb.Children[1], phase);
                    sb.Completed += delegate
                    {
                        Monitor.Exit(this);
                        progress.ScrollTo(e1.Phase / 5.0);
                    };
                    sb.Begin();
                }), null);
                lock (this) { }
                Thread.Sleep(150);
            });
            param.Logger.Logging += new EventHandler<Core.LogEventArgs>((sender1, e1) =>
            {
                this.Dispatcher.Invoke(new Action(delegate
                {
                    if (log != null)
                    {
                        log.AppendText(e1.Message + Environment.NewLine);
                        log.ScrollToEnd();
                    }
                }), null);
            });
            param.Logger.Progressing += new EventHandler<Core.ProgressEventArgs>((sender1, e1) =>
            {
                value = e1.Progress;
            });
            param.Logger.Fault += new EventHandler<Core.ExceptionEventArgs>((sender1, e1) =>
            {
                this.Dispatcher.Invoke(new Action(delegate
                {
                    Monitor.Enter(this);
                    Border result = Helper.FindChild<Border>(progress, "result");
                    Helper.FindChild<UIElement>(result, "resultFail").Visibility = Visibility.Visible;
                    Helper.FindChild<UIElement>(result, "resultOk").Visibility = Visibility.Hidden;
                    Helper.FindChild<TextBox>(result, null).Text = string.Format("Failed!\nException details : \n" + e1.Exception.ToString());
                    Storyboard sb = this.Resources["showPhase"] as Storyboard;
                    progress.ScrollToEnd();
                    Storyboard.SetTarget(sb, result);
                    sb.Completed += delegate
                    {
                        Monitor.Exit(this);
                    };
                    sb.Begin();
                    value = -1;
                    doConfuse.Content = "Confuse!";
                    CONFUSED();
                }), null);
                lock (this) { }
            });
            param.Logger.End += new EventHandler<Core.LogEventArgs>((sender1, e1) =>
            {
                this.Dispatcher.Invoke(new Action(delegate
                {
                    Monitor.Enter(this);
                    if (pBar != null) pBar.Value = 1;
                    Border result = Helper.FindChild<Border>(progress, "result");
                    Helper.FindChild<UIElement>(result, "resultOk").Visibility = Visibility.Visible;
                    Helper.FindChild<UIElement>(result, "resultFail").Visibility = Visibility.Hidden;
                    Helper.FindChild<TextBox>(result, null).Text = string.Format("Succeeded!\n" + e1.Message.ToString());
                    Storyboard sb = this.Resources["showPhase"] as Storyboard;
                    Storyboard.SetTarget(sb, result);
                    sb.Completed += delegate
                    {
                        Monitor.Exit(this);
                        progress.ScrollToEnd();
                    };
                    sb.Begin();
                    value = -1;
                    doConfuse.Content = "Confuse!";
                    CONFUSED();
                }), null);
                lock (this) { }
            });
            var cr = new Core.Confuser();
            progress.ScrollToBeginning();
            (this.Resources["resetProgress"] as Storyboard).Begin();
            Helper.FindChild<TextBox>(phase1, null).Text = "";
            Helper.FindChild<TextBox>(phase2, null).Text = "";
            Helper.FindChild<TextBox>(phase3, null).Text = "";
            Helper.FindChild<TextBox>(phase4, null).Text = "";
            (this.Resources["showProgress"] as Storyboard).Begin();
            Dispatcher.Invoke(new Action(delegate { Thread.Sleep(100); }), DispatcherPriority.SystemIdle);
            MoniterValue();
            CONFUSING();
            doConfuse.Content = "Cancel";
            confuser = cr.ConfuseAsync(param);
        }

        double value = 0;
        void MoniterValue()
        {
            if (value == -1)
            {
                value = 0;
                return;
            }
            if (pBar != null)
                pBar.Value = value;
            this.Dispatcher.BeginInvoke(new Action(MoniterValue), DispatcherPriority.Background, null);
        }

        private void list_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
			Storyboard sb = new Storyboard();
			
			DoubleAnimation fadeOut = new DoubleAnimation(0, new Duration(TimeSpan.FromSeconds(0.25)));
			Storyboard.SetTargetProperty(fadeOut, new PropertyPath(UIElement.OpacityProperty));
            Storyboard.SetTarget(fadeOut, Helper.FindChild<Grid>((sender as ListBox).Parent, ((Grid)(sender as ListBox).Parent).Name + "Detail"));
			sb.Children.Add(fadeOut);
			
			var obj = new ObjectAnimationUsingKeyFrames();
            obj.KeyFrames.Add(new DiscreteObjectKeyFrame((sender as ListBox).SelectedItem, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.25))));
			Storyboard.SetTargetProperty(obj, new PropertyPath(FrameworkElement.DataContextProperty));
            Storyboard.SetTarget(obj, Helper.FindChild<Grid>((sender as ListBox).Parent, ((Grid)(sender as ListBox).Parent).Name + "Detail"));
			sb.Children.Add(obj);
			
			DoubleAnimation fadeIn = new DoubleAnimation(1, new Duration(TimeSpan.FromSeconds(0.25)));
			fadeIn.BeginTime = TimeSpan.FromSeconds(0.25);
			Storyboard.SetTargetProperty(fadeIn, new PropertyPath(UIElement.OpacityProperty));
            Storyboard.SetTarget(fadeIn, Helper.FindChild<Grid>((sender as ListBox).Parent, ((Grid)(sender as ListBox).Parent).Name + "Detail"));
			sb.Children.Add(fadeIn);
			
        	sb.Begin();
        }

        private void AboutLoaded(object sender, RoutedEventArgs e)
        {
        	ver.Text = "v" + typeof(Core.Confuser).Assembly.GetName().Version.ToString();
        }
    }
}