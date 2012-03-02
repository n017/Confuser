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
using System.Windows.Shapes;
using System.Windows.Navigation;
using WPF.JoshSmith.Adorners;
using System.Windows.Interop;
using System.Runtime.InteropServices;

namespace Confuser
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, IHost
    {
        UIElementAdorner adorner;

        public MainWindow()
        {
            InitializeComponent();
            this.Width = 800; this.Height = 600;
            Go<object>(new Start(), null);
            back.CommandTarget = frame;
            this.DataContext = frame;
            frame.CommandBindings.Add(new CommandBinding(NavigationCommands.BrowseForward, (sender, e) =>
            {
                e.Handled = true;
            }, (sender, e) =>
            {
                e.CanExecute = false;
                e.Handled = true;
            }));
            frame.CommandBindings.Add(new CommandBinding(NavigationCommands.BrowseBack, (sender, e) => { }, (sender, e) =>
            {
                if (DisabledNavigation)
                {
                    e.CanExecute = false;
                    e.Handled = true;
                }
            }));
        }

        public bool DisabledNavigation
        {
            get { return (bool)GetValue(DisabledNavigationProperty); }
            set { SetValue(DisabledNavigationProperty, value); }
        }
        public static readonly DependencyProperty DisabledNavigationProperty =
            DependencyProperty.Register("DisabledNavigation", typeof(bool), typeof(MainWindow), new UIPropertyMetadata(false, DisabledNavigationChanged));

        static void DisabledNavigationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            CommandManager.InvalidateRequerySuggested();
        }

        public void Go<T>(IPage<T> page, T parameter) where T : class
        {
            page.Init(this, parameter);
            frame.Navigate(page);
        }

        public void Load<T>(Func<T> load, IPage<T> page) where T : class
        {
            adorner = new UIElementAdorner(Root, new Loading());
            adorner.Width = this.ActualWidth;
            adorner.Height = this.ActualHeight;
            AdornerLayer.GetAdornerLayer(Root).Add(adorner);

            load.BeginInvoke(Complete<T>, new LoadData<T>() { load = load, page = page });
        }
        struct LoadData<T> where T : class
        {
            public Func<T> load;
            public IPage<T> page;
        }
        void Complete<T>(IAsyncResult ar) where T : class
        {
            if (!CheckAccess())
            {
                Dispatcher.Invoke(new AsyncCallback(Complete<T>), ar);
                return;
            }
            AdornerLayer.GetAdornerLayer(Root).Remove(adorner);
            adorner = null;

            LoadData<T> dat = (LoadData<T>)ar.AsyncState;
            dat.page.Init(this, dat.load.EndInvoke(ar));
            frame.Navigate(dat.page);
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Bar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void Bar_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            switch (this.WindowState)
            {
                case WindowState.Maximized:
                    this.WindowState = WindowState.Normal; break;
                case WindowState.Normal:
                    this.WindowState = WindowState.Maximized; break;
            }
        }
        public override void OnApplyTemplate()
        {
            System.IntPtr handle = (new WindowInteropHelper(this)).Handle;
            HwndSource.FromHwnd(handle).AddHook(new HwndSourceHook(WindowProc));
        }

        private static System.IntPtr WindowProc(
              System.IntPtr hwnd,
              int msg,
              System.IntPtr wParam,
              System.IntPtr lParam,
              ref bool handled)
        {
            switch (msg)
            {
                case 0x0024:/* WM_GETMINMAXINFO */
                    WmGetMinMaxInfo(hwnd, lParam);
                    handled = true;
                    break;
            }

            return (System.IntPtr)0;
        }

        private static void WmGetMinMaxInfo(System.IntPtr hwnd, System.IntPtr lParam)
        {

            MINMAXINFO mmi = (MINMAXINFO)Marshal.PtrToStructure(lParam, typeof(MINMAXINFO));

            // Adjust the maximized size and position to fit the work area of the correct monitor
            int MONITOR_DEFAULTTONEAREST = 0x00000002;
            System.IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);

            if (monitor != System.IntPtr.Zero)
            {

                MONITORINFO monitorInfo = new MONITORINFO();
                GetMonitorInfo(monitor, monitorInfo);
                RECT rcWorkArea = monitorInfo.rcWork;
                RECT rcMonitorArea = monitorInfo.rcMonitor;
                mmi.ptMaxPosition.x = Math.Abs(rcWorkArea.left - rcMonitorArea.left);
                mmi.ptMaxPosition.y = Math.Abs(rcWorkArea.top - rcMonitorArea.top);
                mmi.ptMaxSize.x = Math.Abs(rcWorkArea.right - rcWorkArea.left);
                mmi.ptMaxSize.y = Math.Abs(rcWorkArea.bottom - rcWorkArea.top);
            }

            Marshal.StructureToPtr(mmi, lParam, true);
        }


        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int x;
            public int y;

            public POINT(int x, int y)
            {
                this.x = x;
                this.y = y;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        };

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public class MONITORINFO
        {
            public int cbSize = Marshal.SizeOf(typeof(MONITORINFO));

            public RECT rcMonitor = new RECT();

            public RECT rcWork = new RECT();

            public int dwFlags = 0;
        }


        [StructLayout(LayoutKind.Sequential, Pack = 0)]
        public struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;

            public static readonly RECT Empty = new RECT();

            public int Width
            {
                get { return Math.Abs(right - left); }
            }
            public int Height
            {
                get { return bottom - top; }
            }

            public RECT(int left, int top, int right, int bottom)
            {
                this.left = left;
                this.top = top;
                this.right = right;
                this.bottom = bottom;
            }


            public RECT(RECT rcSrc)
            {
                this.left = rcSrc.left;
                this.top = rcSrc.top;
                this.right = rcSrc.right;
                this.bottom = rcSrc.bottom;
            }

            public bool IsEmpty
            {
                get
                {
                    return left >= right || top >= bottom;
                }
            }
            public override string ToString()
            {
                if (this == RECT.Empty) { return "RECT {Empty}"; }
                return "RECT { left : " + left + " / top : " + top + " / right : " + right + " / bottom : " + bottom + " }";
            }

            public override bool Equals(object obj)
            {
                if (!(obj is Rect)) { return false; }
                return (this == (RECT)obj);
            }

            public override int GetHashCode()
            {
                return left.GetHashCode() + top.GetHashCode() + right.GetHashCode() + bottom.GetHashCode();
            }


            public static bool operator ==(RECT rect1, RECT rect2)
            {
                return (rect1.left == rect2.left && rect1.top == rect2.top && rect1.right == rect2.right && rect1.bottom == rect2.bottom);
            }

            public static bool operator !=(RECT rect1, RECT rect2)
            {
                return !(rect1 == rect2);
            }


        }

        [DllImport("user32")]
        internal static extern bool GetMonitorInfo(IntPtr hMonitor, MONITORINFO lpmi);
        [DllImport("User32")]
        internal static extern IntPtr MonitorFromWindow(IntPtr handle, int flags);
    }

    public interface IHost
    {
        void Go<T>(IPage<T> page, T parameter) where T : class;
        void Load<T>(Func<T> load, IPage<T> page) where T : class;
        bool DisabledNavigation { get; set; }
    }

    public interface IPage<T> where T : class
    {
        void Init(IHost host, T parameter);
    }
}
