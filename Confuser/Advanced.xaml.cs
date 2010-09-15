using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
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
using System.ComponentModel;

namespace Confuser
{
	/// <summary>
	/// Interaction logic for Advanced.xaml
	/// </summary>
	public partial class Advanced : Window
	{
		public Advanced()
		{
            this.InitializeComponent();
            GIVEmeASSEMBLY();
            assemblyButton.IsChecked = false;
        }

        private void radioChecked(object sender, RoutedEventArgs e)
        {
            RadioButton radio = sender as RadioButton;
            UIElement element = Helper.FindChild<UIElement>(space, radio.Content.ToString().ToLower());
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
            UIElement element = Helper.FindChild<UIElement>(space, radio.Content.ToString().ToLower());
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
            settingsButton.IsEnabled = false;
            message.Visibility = Visibility.Visible;
            loading.Visibility = Visibility.Hidden;
            bar.IsIndeterminate = false;
            giveME.Visibility = Visibility.Visible;
            assemblyButton.IsChecked = true;
        }
        private void LOADINGassembly()
        {
            confuseButton.IsEnabled = false;
            settingsButton.IsEnabled = false;
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
            settingsButton.IsEnabled = true;
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

                                    BitmapImage ico = Helper.GetIcon(path);
                                    if (ico != null)
                                    {
                                        if (ico.Height > 64 && ico.Width > 64) icon.Stretch = Stretch.Uniform; else icon.Stretch = Stretch.None;
                                        this.icon.Source = ico;
                                    }
									
									elements.LoadAssembly(asm);
                                }
                                IgotASSEMBLY();
                            }
                        }), null);
                    }).Start();
                }
            }
        }

        private void browseClick(object sender, RoutedEventArgs e)
        {
            string id = (sender as Button).Name.Substring(6).ToLower();
            if (id == "sn")
            {
                Microsoft.Win32.OpenFileDialog open = new Microsoft.Win32.OpenFileDialog();
                open.Filter = "Strong Name Key(*.snk)|*.snk";
                if (open.ShowDialog().GetValueOrDefault())
                    sn.Text = open.FileName;
            }
            else if (id == "output")
            {
                Microsoft.Win32.SaveFileDialog save = new Microsoft.Win32.SaveFileDialog();
                save.Filter = "Assembly(*.exe;*.dll)|**.exe;*.dll";
                if (save.ShowDialog().GetValueOrDefault())
                    output.Text = save.FileName;
            }
        }
        private void exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
        private void bas_Click(object sender, RoutedEventArgs e)
        {
            Basic bas = new Basic();
            Application.Current.MainWindow = bas;
            bas.Left = this.Left; bas.Top = this.Top;
            bas.Visibility = Visibility.Visible;
            this.Close();
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
            setConfusions.ItemsSource = ldConfusions.Values;
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
                src = new FileStream(path, FileMode.Open, FileAccess.Read);
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
            param.ReferencesPath = System.IO.Path.GetDirectoryName(path);
            param.Confusions = ldConfusions.Values.ToArray();
            param.DefaultPreset = Core.Preset.None;
            param.CompressOutput = compress.IsChecked.GetValueOrDefault();
            param.StrongNameKeyPath = sn.Text;
            param.Marker = new MarkingCopyer(asm);
            param.Logger.BeginPhase += new EventHandler<Core.PhaseEventArgs>((sender1, e1) =>
            {
                this.Dispatcher.Invoke(new Action(delegate
                {
                    Monitor.Enter(this);
                    Border phase = Helper.FindChild<Border>(progress, "phase" + e1.Phase);
                    log = Helper.FindChild<TextBox>(phase, null);
                    if (log != null) log.Clear();
                    if (pBar != null) pBar.Value = 1;
                    pBar = Helper.FindChild<ProgressBar>(phase, null);
                    if (pBar != null) pBar.Value = 0;
                    Storyboard sb = this.Resources["showPhase"] as Storyboard;
                    Storyboard.SetTarget(sb.Children[0], phase);
                    Storyboard.SetTarget(sb.Children[1], phase);
                    sb.Completed += delegate
                    {
                        Monitor.Exit(this);
                        progress.ScrollToEnd();
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
            (this.Resources["showProgress"] as Storyboard).Begin();
            Dispatcher.Invoke(new Action(delegate { Thread.Sleep(100); }), DispatcherPriority.Render);
            MoniterValue();
            CONFUSING();
            doConfuse.Content = "Cancel";
            confuser = cr.ConfuseAsync(src, dst, param);
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


        private void AboutLoaded(object sender, RoutedEventArgs e)
        {
            ver.Text = "v" + typeof(Core.Confuser).Assembly.GetName().Version.ToString();
        }

        private void elements_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (elements.SelectedItem == null || (elements.SelectedItem as TreeViewItem).Tag == null)
            {
                confusionList.ItemsSource = null;
                elementSet.IsEnabled = false;
                return;
            }
            else elementSet.IsEnabled = true;

            IAnnotationProvider provider = (elements.SelectedItem as TreeViewItem).Tag as IAnnotationProvider;
            IDictionary<Core.IConfusion, NameValueCollection> dict;
            if (!provider.Annotations.Contains("ConfusionSets"))
                provider.Annotations["ConfusionSets"] = dict = new ConfusionSets();
            else
                dict = provider.Annotations["ConfusionSets"] as IDictionary<Core.IConfusion, NameValueCollection>;

            confusionList.ItemsSource = dict;
        }

        private void addClick(object sender, RoutedEventArgs e)
        {
            IDictionary<Core.IConfusion, NameValueCollection> dict = confusionList.ItemsSource as IDictionary<Core.IConfusion, NameValueCollection>;
            if (setConfusions.SelectedItem != null && !dict.ContainsKey(setConfusions.SelectedItem as Core.IConfusion))
                dict.Add(setConfusions.SelectedItem as Core.IConfusion, new NameValueCollection());
        }
        private void removeClick(object sender, RoutedEventArgs e)
        {
            IDictionary<Core.IConfusion, NameValueCollection> dict = confusionList.ItemsSource as IDictionary<Core.IConfusion, NameValueCollection>;
            if (confusionList.SelectedItem != null)
            {
                int idx = confusionList.SelectedIndex - 1;
                dict.Remove(((KeyValuePair<Core.IConfusion, NameValueCollection>)confusionList.SelectedItem).Key);
                if (confusionList.Items.Count != 0 && idx == -1) idx = 0;
                confusionList.SelectedIndex = idx;
            }
        }
        private void clearClick(object sender, RoutedEventArgs e)
        {
            (confusionList.ItemsSource as IDictionary<Core.IConfusion, NameValueCollection>).Clear();
        }
        private void addPresetClick(object sender, RoutedEventArgs e)
        {
            IDictionary<Core.IConfusion, NameValueCollection> dict = confusionList.ItemsSource as IDictionary<Core.IConfusion, NameValueCollection>;
            Core.Preset preset = (Core.Preset)Enum.Parse(typeof(Core.Preset), (setPreset.SelectedItem as TextBlock).Text);
            foreach (Core.IConfusion i in ldConfusions.Values)
                if (i.Preset <= preset && !dict.ContainsKey(i))
                    dict.Add(i, new NameValueCollection());
        }
        private void applyChildClick(object sender, RoutedEventArgs e)
        {
            IDictionary<Core.IConfusion, NameValueCollection> dict = confusionList.ItemsSource as IDictionary<Core.IConfusion, NameValueCollection>;
            SetChilds((elements.SelectedItem as TreeViewItem).Tag as IAnnotationProvider, dict);
        }

        void SetChilds(IAnnotationProvider obj, IDictionary<Core.IConfusion, NameValueCollection> dict)
        {
            ConfusionSets s = new ConfusionSets();
            foreach (KeyValuePair<Core.IConfusion, NameValueCollection> pair in dict)
                s.Add(pair.Key, pair.Value);
            obj.Annotations["ConfusionSets"] = s;

            System.Collections.IEnumerable e;
            if (obj is AssemblyDefinition)
                e = (obj as AssemblyDefinition).Modules;
            else if (obj is ModuleDefinition)
                e = (obj as ModuleDefinition).Types;
            else if (obj is TypeDefinition)
            {
                TypeDefinition type = obj as TypeDefinition;
                System.Collections.ArrayList anno = new System.Collections.ArrayList();
                anno.AddRange(type.NestedTypes);
                anno.AddRange(type.Methods);
                anno.AddRange(type.Fields);
                anno.AddRange(type.Properties);
                anno.AddRange(type.Events);
                e = anno;
            }
            else
                e = Enumerable.Empty<object>();

            foreach (IAnnotationProvider provider in e)
                SetChilds(provider, dict);
        }
    }

    class ConfusionSets : IDictionary<Core.IConfusion, NameValueCollection>, ICollection<KeyValuePair<Core.IConfusion, NameValueCollection>>, IEnumerable<KeyValuePair<Core.IConfusion, NameValueCollection>>, INotifyCollectionChanged,INotifyPropertyChanged
    {
        List<Core.IConfusion> keys = new List<Confuser.Core.IConfusion>();
        Dictionary<Core.IConfusion, NameValueCollection> _internal = new Dictionary<Confuser.Core.IConfusion, NameValueCollection>();
        public event NotifyCollectionChangedEventHandler CollectionChanged;
        public event PropertyChangedEventHandler PropertyChanged;
        void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs("Count"));
                PropertyChanged(this, new PropertyChangedEventArgs("Item[]"));
                PropertyChanged(this, new PropertyChangedEventArgs("Keys"));
                PropertyChanged(this, new PropertyChangedEventArgs("Values"));
            }
            if (CollectionChanged != null)
            {
                CollectionChanged(this, e);
            }
        }

        public void Add(Core.IConfusion key, NameValueCollection value)
        {
            _internal.Add(key, value);
            keys.Add(key);
            if (CollectionChanged != null)
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, new KeyValuePair<Core.IConfusion, NameValueCollection>(key, value)));
        }

        public bool ContainsKey(Core.IConfusion key)
        {
            return _internal.ContainsKey(key);
        }

        public ICollection<Core.IConfusion> Keys
        {
            get { return _internal.Keys; }
        }

        public bool Remove(Core.IConfusion key)
        {
            NameValueCollection value = _internal[key];
            int idx = keys.IndexOf(key);
            bool ret = _internal.Remove(key);
            if (ret)
            {
                keys.RemoveAt(idx);
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, new KeyValuePair<Core.IConfusion, NameValueCollection>(key, value), idx));
            } 
            return ret;
        }

        public bool TryGetValue(Core.IConfusion key, out NameValueCollection value)
        {
            return _internal.TryGetValue(key, out value);
        }

        public ICollection<NameValueCollection> Values
        {
            get { return _internal.Values; }
        }

        public NameValueCollection this[Core.IConfusion key]
        {
            get { return _internal[key]; }
            set
            {
                _internal[key] = value;
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
        }

        void ICollection<KeyValuePair<Core.IConfusion, NameValueCollection>>.Add(KeyValuePair<Confuser.Core.IConfusion, NameValueCollection> item)
        {
            Add(item.Key, item.Value);
        }

        public void Clear()
        {
            _internal.Clear(); keys.Clear();
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        bool ICollection<KeyValuePair<Core.IConfusion, NameValueCollection>>.Contains(KeyValuePair<Confuser.Core.IConfusion, NameValueCollection> item)
        {
            return _internal.ContainsKey(item.Key) && _internal[item.Key] == item.Value;
        }

        void ICollection<KeyValuePair<Core.IConfusion, NameValueCollection>>.CopyTo(KeyValuePair<Confuser.Core.IConfusion, NameValueCollection>[] array, int arrayIndex)
        {
            (_internal as ICollection<KeyValuePair<Core.IConfusion, NameValueCollection>>).CopyTo(array, arrayIndex);
        }

        public int Count
        {
            get { return _internal.Count; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        bool ICollection<KeyValuePair<Core.IConfusion, NameValueCollection>>.Remove(KeyValuePair<Confuser.Core.IConfusion, NameValueCollection> item)
        {
            if (!_internal.ContainsKey(item.Key) || _internal[item.Key] != item.Value)
                return false;
            return Remove(item.Key);
        }

        private class CSEnumerator : IEnumerator<KeyValuePair<Confuser.Core.IConfusion, NameValueCollection>>
        {
            ConfusionSets sets;
            int idx;
            public CSEnumerator(ConfusionSets sets)
            {
                this.sets = sets; this.idx = -1;
            }

            public KeyValuePair<Confuser.Core.IConfusion, NameValueCollection> Current
            {
                get
                {
                    return new KeyValuePair<Confuser.Core.IConfusion, NameValueCollection>(sets.keys[idx], sets._internal[sets.keys[idx]]);
                }
            }

            public void Dispose()
            {
                //
            }

            object System.Collections.IEnumerator.Current
            {
                get { return Current; }
            }

            public bool MoveNext()
            {
                if (idx == sets.keys.Count - 1) return false;
                idx++; return true;
            }

            public void Reset()
            {
                throw new NotSupportedException();
            }
        }

        public IEnumerator<KeyValuePair<Confuser.Core.IConfusion, NameValueCollection>> GetEnumerator()
        {
            return new CSEnumerator(this);
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return _internal.GetEnumerator();
        }
    }

    class MarkingCopyer : Core.IMarker
    {
        AssemblyDefinition src;
        public MarkingCopyer(AssemblyDefinition asm)
        {
            src = asm;
        }
        public void Initalize(Core.IConfusion[] cions)
        {
            //
        }
        void Copy(IAnnotationProvider src, IAnnotationProvider dst)
        {
            Core.Target target;
            if (src is AssemblyDefinition) target = Core.Target.Assembly;
            else if (src is ModuleDefinition) target = (Core.Target)0xff;
            else if (src is TypeDefinition) target = Core.Target.Types;
            else if (src is MethodDefinition) target = Core.Target.Methods;
            else if (src is FieldDefinition) target = Core.Target.Fields;
            else if (src is PropertyDefinition) target = Core.Target.Properties;
            else if (src is EventDefinition) target = Core.Target.Events;
            else throw new InvalidOperationException();
            Dictionary<Core.IConfusion, NameValueCollection> now = new Dictionary<Core.IConfusion, NameValueCollection>();
            if (src.Annotations.Contains("ConfusionSets"))
            {
                foreach (KeyValuePair<Core.IConfusion, NameValueCollection> set in src.Annotations["ConfusionSets"] as IDictionary<Core.IConfusion, NameValueCollection>)
                    if ((set.Key.Target & target) == target)
                        now.Add(set.Key, set.Value);
            }
            dst.Annotations["ConfusionSets"] = now;
            dst.Annotations["GlobalParams"] = new Dictionary<Core.IConfusion, NameValueCollection>();
        }

        public void MarkAssembly(AssemblyDefinition asm, Core.Preset preset)
        {
            Copy(src, asm);
            for (int i = 0; i < asm.Modules.Count; i++)
            {
                MarkModule(src.Modules[i], asm.Modules[i]);
            }
        }
        void MarkModule(ModuleDefinition src, ModuleDefinition dst)
        {
            Copy(src, dst);
            for (int i = 0; i < dst.Types.Count; i++)
            {
                MarkType(src.Types[i], dst.Types[i]);
            }
        }
        void MarkType(TypeDefinition src, TypeDefinition dst)
        {
            Copy(src, dst);
            for (int i = 0; i < dst.NestedTypes.Count; i++)
            {
                MarkType(src.NestedTypes[i], dst.NestedTypes[i]);
            }
            for (int i = 0; i < dst.Methods.Count; i++)
            {
                Copy(src.Methods[i], dst.Methods[i]);
            }
            for (int i = 0; i < dst.Fields.Count; i++)
            {
                Copy(src.Fields[i], dst.Fields[i]);
            }
            for (int i = 0; i < dst.Properties.Count; i++)
            {
                Copy(src.Properties[i], dst.Properties[i]);
            }
            for (int i = 0; i < dst.Events.Count; i++)
            {
                Copy(src.Events[i], dst.Events[i]);
            }
        }
    }
}