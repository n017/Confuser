using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Mono.Cecil.PE;
using System.IO;
using Mono.Cecil;
using System.Collections.ObjectModel;

namespace Confuser
{
    /// <summary>
    /// Interaction logic for Start.xaml
    /// </summary>
    partial class Start : Page, IPage<object>
    {
        public Start()
        {
            InitializeComponent();
            Style = FindResource(typeof(Page)) as Style;
            this.view.ItemsSource = Asms;
            CheckCanNext();
        }

        class AsmDesc : Freezable
        {
            public string Path { get; set; }
            public Start Parent { get; set; }

            public bool IsExecutable
            {
                get { return (bool)GetValue(IsExecutableProperty); }
                set { SetValue(IsExecutableProperty, value); }
            }
            public static readonly DependencyProperty IsExecutableProperty =
                DependencyProperty.Register("IsExecutable", typeof(bool), typeof(AsmDesc), new UIPropertyMetadata(false));

            public bool IsMain
            {
                get { return (bool)GetValue(IsMainProperty); }
                set { SetValue(IsMainProperty, value); }
            }
            public static readonly DependencyProperty IsMainProperty =
                DependencyProperty.Register("IsMain", typeof(bool), typeof(AsmDesc), new UIPropertyMetadata(false, IsMainChanged));

            static void IsMainChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            {
                var parent = (d as AsmDesc).Parent;
                if (parent != null)
                    parent.CheckCanNext();
            }

            protected override Freezable CreateInstanceCore()
            {
                return new AsmDesc()
                {
                    Path = Path,
                    Parent = Parent,
                    IsExecutable = IsExecutable,
                    IsMain = IsMain
                };
            }
        }

        ObservableCollection<AsmDesc> asms = new ObservableCollection<AsmDesc>();
        ObservableCollection<AsmDesc> Asms { get { return asms; } }

        bool im = true;
        protected override void OnDrop(DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] file = e.Data.GetData(DataFormats.FileDrop, true) as string[];
                foreach (var i in file)
                {
                    if (asms.Any(_ => _.Path == i)) continue;
                    using (var str = File.OpenRead(i))
                    {
                        try
                        {
                            var img = ImageReader.ReadImageFrom(str);
                            var desc = new AsmDesc() { Path = i, Parent = this, IsExecutable = img.EntryPointToken != 0 };
                            if (desc.IsExecutable)
                            {
                                if (im)
                                {
                                    desc.IsMain = true;
                                    im = false;
                                }
                            }
                            asms.Add(desc);
                        }
                        catch
                        {
                            MessageBox.Show(string.Format(@"""{0}"" is not a valid assembly!", i), "Confuser", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
                CheckCanNext();
            }
        }

        private void view_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete && view.SelectedIndex != -1)
            {
                Asms.RemoveAt(view.SelectedIndex);
                CheckCanNext();
            }
        }

        void CheckCanNext()
        {
            bool hasExe = false, hasMain = false;
            foreach (var i in asms)
            {
                if (i.IsMain) hasMain = true;
                if (i.IsExecutable) hasExe = true;
            }
            next.IsEnabled = asms.Count > 0 && (!hasExe || (hasExe && hasMain));
        }

        AsmDesc[] l_asms;
        AssemblyDefinition[] Load()
        {
            List<AssemblyDefinition> ret = new List<AssemblyDefinition>();
            AssemblyDefinition main = null;
            foreach (var i in l_asms)
            {
                var asm = AssemblyDefinition.ReadAssembly(i.Path, new ReaderParameters(ReadingMode.Immediate));
                if (i.IsMain) main = asm;
                else ret.Add(asm);
            }
            if (main != null) ret.Insert(0, main);
            return ret.ToArray();
        }
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            l_asms = asms.Select(_ => _.GetAsFrozen() as AsmDesc).ToArray();
            host.Load<AssemblyDefinition[]>(Load, new Settings());
        }

        IHost host;
        public void Init(IHost host, object parameter)
        {
            this.host = host;
        }
    }
}
