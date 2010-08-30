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

namespace Confuser
{
	/// <summary>
	/// Interaction logic for MainUI.xaml
	/// </summary>
    public partial class MainUI : Window
    {
        public MainUI()
        {
            this.InitializeComponent();
            GIVEmeASSEMBLY();
            assemblyButton.IsChecked = false;
        }

        public static T FindChild<T>(DependencyObject parent, string childName)
            where T : DependencyObject
        {
            // Confirm parent and childName are valid. 
            if (parent == null) return null;

            T foundChild = null;

            int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                // If the child is not of the request child type child
                T childType = child as T;
                if (childType == null)
                {
                    // recursively drill down the tree
                    foundChild = FindChild<T>(child, childName);

                    // If the child is found, break so we do not overwrite the found child. 
                    if (foundChild != null) break;
                }
                else if (!string.IsNullOrEmpty(childName))
                {
                    var frameworkElement = child as FrameworkElement;
                    // If the child's name is set for search
                    if (frameworkElement != null && frameworkElement.Name == childName)
                    {
                        // if the child's name is of the request name
                        foundChild = (T)child;
                        break;
                    }
                    foundChild = FindChild<T>(child, childName);
                    if (foundChild != null) break;
                }
                else
                {
                    // child element found.
                    foundChild = (T)child;
                    break;
                }
            }

            return foundChild;
        }
        private void radioChecked(object sender, RoutedEventArgs e)
        {
            RadioButton radio = sender as RadioButton;
            UIElement element = FindChild<UIElement>(space, radio.Content.ToString().ToLower());
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
            UIElement element = FindChild<UIElement>(space, radio.Content.ToString().ToLower());
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
                    output.Text = System.IO.Path.GetDirectoryName(path) + "\\Confused\\" + System.IO.Path.GetFileName(path);
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

                                    BitmapImage ico = GetIcon(path);
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

        [DllImport("kernel32.dll")]
        private static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, int dwFlags);
        [DllImport("kernel32.dll")]
        private static extern IntPtr FindResource(IntPtr hModule, IntPtr lpName, int lpType);
        [DllImport("kernel32.dll")]
        private static extern IntPtr LoadResource(IntPtr hModule, IntPtr hResInfo);
        [DllImport("kernel32.dll")]
        private static extern IntPtr LockResource(IntPtr hResData);
        [DllImport("kernel32.dll")]
        private static extern int SizeofResource(IntPtr hModule, IntPtr hResInfo);
        [DllImport("kernel32.dll")]
        private static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool EnumResourceNames(IntPtr hModule, int lpszType, EnumResNameProc lpEnumFunc, IntPtr lParam);
        [UnmanagedFunctionPointer(CallingConvention.Winapi, CharSet = CharSet.Auto)]
        private delegate bool EnumResNameProc(IntPtr hModule, int lpszType, IntPtr lpszName, IntPtr lParam);

        BitmapImage GetIcon(string path)
        {
            IntPtr hMod = LoadLibraryEx(path, IntPtr.Zero, 0x00000002);
			MemoryStream mem = null;
            EnumResourceNames(hMod, 3 + 11, new EnumResNameProc(delegate(IntPtr hModule, int lpszType, IntPtr lpszName, IntPtr lParam)
            {
                if (lpszType == 3 + 11)
                {
                    IntPtr res = FindResource(hMod, lpszName, 3 + 11);
                    IntPtr dat = LoadResource(hMod, res);
                    IntPtr ptr = LockResource(dat);
                    int size = SizeofResource(hMod, res);
                    Console.WriteLine(ptr.ToString("X8"));
                    Console.WriteLine(size.ToString("X8"));
                    Console.WriteLine();
                    byte[] byteArr = new byte[size];
                    Marshal.Copy(ptr, byteArr, 0, size);

                    mem = new MemoryStream();
                    BinaryWriter wtr = new BinaryWriter(mem);
                    int count = BitConverter.ToUInt16(byteArr, 4);
                    int offset = 6 + (0x10 * count);
                    wtr.Write(byteArr, 0, 6);
                    for (int i = 0; i < count; i++)
                    {
                        wtr.BaseStream.Seek(6 + (0x10 * i), SeekOrigin.Begin);
                        wtr.Write(byteArr, 6 + (14 * i), 12);
                        wtr.Write(offset);
                        IntPtr id = (IntPtr)BitConverter.ToUInt16(byteArr, (6 + (14 * i)) + 12);

                        IntPtr icoRes = FindResource(hMod, id, 3);
                        IntPtr icoDat = LoadResource(hMod, icoRes);
                        IntPtr icoPtr = LockResource(icoDat);
                        int icoSize = SizeofResource(hMod, icoRes);
                        byte[] img = new byte[icoSize];
                        Marshal.Copy(icoPtr, img, 0, icoSize);

                        wtr.BaseStream.Seek(offset, SeekOrigin.Begin);
                        wtr.Write(img, 0, img.Length);
                        offset += img.Length;
                    }
                    return false;
                }
                return true;
            }), IntPtr.Zero);
            FreeLibrary(hMod);
			if(mem == null) return null;
            BitmapImage ret = new BitmapImage();
            ret.BeginInit();
			ret.StreamSource = mem;
            ret.EndInit();
            return ret;
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
			else if(id == "output")
			{
				Microsoft.Win32.SaveFileDialog save = new Microsoft.Win32.SaveFileDialog();
				save.Filter = "Assembly(*.exe;*.dll)|**.exe;*.dll";
				if(save.ShowDialog().GetValueOrDefault())
					output.Text = save.FileName;
			}
        }
        private void exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
        private void adv_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Notsupport");
        }


        Dictionary<string, Core.IConfusion> ldConfusions = new Dictionary<string, Confuser.Core.IConfusion>();
        private void LoadAssembly(Assembly asm)
        {
            foreach (Type type in asm.GetTypes())
                if (typeof(Core.IConfusion).IsAssignableFrom(type) && type != typeof(Core.IConfusion))
                    ldConfusions.Add(type.FullName, Activator.CreateInstance(type) as Core.IConfusion);
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadAssembly(typeof(Core.IConfusion).Assembly);
			confusionList.ItemsSource = ldConfusions.Values;
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

            Stream src;
            try
            {
                src = new FileStream(path, FileMode.Open);
            }
            catch
            {
                MessageBox.Show("Cannot access source assembly", "Confuser", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Stream dst;
            try
            {
                if (!Directory.Exists(System.IO.Path.GetDirectoryName(output.Text)))
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(output.Text));
                dst = new FileStream(output.Text, FileMode.Create);
            }
            catch
            {
                MessageBox.Show("Cannot access destination path", "Confuser", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            var param = new Core.ConfuserParameter();
            param.Confusions = ldConfusions.Values.ToArray();
            param.DefaultPreset = (Core.Preset)Enum.Parse(typeof(Core.Preset), (preset.SelectedItem as TextBlock).Text);
            param.CompressOutput = compress.IsChecked.GetValueOrDefault();
            param.StrongNameKeyPath = sn.Text;
            param.Logger.BeginPhase += new EventHandler<Core.PhaseEventArgs>((sender1, e1) =>
            {
                this.Dispatcher.Invoke(new Action(delegate
                {
                    Monitor.Enter(this);
                    Border phase = FindChild<Border>(progress, "phase" + e1.Phase);
                    log = FindChild<TextBox>(phase, null);
                    if (log != null) log.Clear();
                    pBar = FindChild<ProgressBar>(phase, null);
                    if (pBar != null) pBar.Value = 0;
                    Storyboard sb = this.Resources["showPhase"] as Storyboard;
                    progress.ScrollToEnd();
                    Storyboard.SetTarget(sb.Children[0], phase);
                    Storyboard.SetTarget(sb.Children[1], phase);
                    sb.Completed += delegate
                    {
                        Monitor.Exit(this);
                    };
                    sb.Begin();
                }), null);
                lock (this) { }
                Thread.Sleep(150);
            });
            param.Logger.Logging += new EventHandler<Core.LogEventArgs>((sender1, e1) =>
            {
                this.Dispatcher.BeginInvoke(new Action(delegate
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
                this.Dispatcher.BeginInvoke(new Action(delegate
                {
                    if (pBar != null)
                        pBar.Value = e1.Progress;
                }), null);
            });
            param.Logger.Fault += new EventHandler<Core.ExceptionEventArgs>((sender1, e1) =>
            {
                this.Dispatcher.Invoke(new Action(delegate
                {
                    Monitor.Enter(this);
                    Border result = FindChild<Border>(progress, "result");
                    FindChild<UIElement>(result, "resultFail").Visibility = Visibility.Visible;
                    FindChild<UIElement>(result, "resultOk").Visibility = Visibility.Hidden;
                    FindChild<TextBox>(result, null).Text = string.Format("Failed!\nException details : \n" + e1.Exception.ToString());
                    Storyboard sb = this.Resources["showPhase"] as Storyboard;
                    progress.ScrollToEnd();
                    Storyboard.SetTarget(sb, result);
                    sb.Completed += delegate
                    {
                        Monitor.Exit(this);
                    };
                    sb.Begin();
                    doConfuse.Content = "Confuse!";
                }), null);
                lock (this) { }
            });
            param.Logger.End += new EventHandler<Core.LogEventArgs>((sender1, e1) =>
            {
                this.Dispatcher.Invoke(new Action(delegate
                {
                    Monitor.Enter(this);
                    Border result = FindChild<Border>(progress, "result");
                    FindChild<UIElement>(result, "resultOk").Visibility = Visibility.Visible;
                    FindChild<UIElement>(result, "resultFail").Visibility = Visibility.Hidden;
                    FindChild<TextBox>(result, null).Text = string.Format("Succeeded!\n" + e1.Message.ToString());
                    Storyboard sb = this.Resources["showPhase"] as Storyboard;
                    progress.ScrollToEnd();
                    Storyboard.SetTarget(sb, result);
                    sb.Completed += delegate
                    {
                        Monitor.Exit(this);
                    };
                    sb.Begin();
                    doConfuse.Content = "Confuse!";
                }), null);
                lock (this) { }
            });
            var cr = new Core.Confuser();
            (this.Resources["resetProgress"] as Storyboard).Begin();
            (this.Resources["showProgress"] as Storyboard).Begin();
            progress.ScrollToBeginning();
            confuser = cr.ConfuseAsync(src, dst, param);
            doConfuse.Content = "Cancel";
        }

        private void confusionList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
			Storyboard sb = new Storyboard();
			
			DoubleAnimation fadeOut = new DoubleAnimation(0, new Duration(TimeSpan.FromSeconds(0.25)));
			Storyboard.SetTargetProperty(fadeOut, new PropertyPath(UIElement.OpacityProperty));
			Storyboard.SetTarget(fadeOut, confusionDetail);
			sb.Children.Add(fadeOut);
			
			var obj = new ObjectAnimationUsingKeyFrames();
			obj.KeyFrames.Add(new DiscreteObjectKeyFrame(confusionList.SelectedItem, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.25))));
			Storyboard.SetTargetProperty(obj, new PropertyPath(FrameworkElement.DataContextProperty));
			Storyboard.SetTarget(obj, confusionDetail);
			sb.Children.Add(obj);
			
			DoubleAnimation fadeIn = new DoubleAnimation(1, new Duration(TimeSpan.FromSeconds(0.25)));
			fadeIn.BeginTime = TimeSpan.FromSeconds(0.25);
			Storyboard.SetTargetProperty(fadeIn, new PropertyPath(UIElement.OpacityProperty));
			Storyboard.SetTarget(fadeIn, confusionDetail);
			sb.Children.Add(fadeIn);
			
        	sb.Begin();
        }
    }
    class CultureConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if ((string)value == string.Empty) return "null";
            else return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value.ToString() == "null") return string.Empty;
            else return value;
        }
    }
    class ByteArrConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null || !(value is byte[]) || (value as byte[]).Length == 0)
                return "null";
            StringBuilder sb = new StringBuilder();
            foreach (byte i in value as byte[])
                sb.Append(i.ToString("x2"));
            return sb.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value.ToString() == "null" || value == null) return new byte[0];
            List<byte> ret = new List<byte>();
            string str = value.ToString();
            for (int i = 0; i < str.Length; i += 2)
                ret.Add(System.Convert.ToByte(str.Substring(i, 2), 16));
            return ret.ToArray();
        }
    }
    class KindConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (!(value is ModuleKind))
                return "Unknown";
            switch ((ModuleKind)value)
            {
                case ModuleKind.Console:
                    return "Console Application";
                case ModuleKind.Dll:
                    return "Class Library";
                case ModuleKind.NetModule:
                    return "Net Module(???)";
                case ModuleKind.Windows:
                    return "Windows Application";
                default:
                    return "Unknown";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            switch (value.ToString())
            {
                case "Console Application":
                    return ModuleKind.Console;
                case "Class Library":
                    return ModuleKind.Dll;
                case "Net Module(???)":
                    return ModuleKind.NetModule;
                case "Windows Application":
                    return ModuleKind.Windows;
                default:
                    return (ModuleKind)0;
            }
        }
    }
}