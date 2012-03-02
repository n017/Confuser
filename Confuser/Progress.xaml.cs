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
using Confuser.Core;
using System.Threading;
using Mono.Cecil;
using System.Windows.Media.Imaging;

namespace Confuser
{
    /// <summary>
    /// Interaction logic for Progress.xaml
    /// </summary>
    public partial class Progress : Page, IPage<ConfuserParameter>
    {
        public Progress()
        {
            InitializeComponent();
            Style = FindResource(typeof(Page)) as Style;
        }

        Confuser.Core.Confuser cr;
        Thread thread;

        IHost host;
        ConfuserParameter parameter;
        public void Init(IHost host, ConfuserParameter parameter)
        {
            this.host = host;
            this.parameter = parameter;
            parameter.Confusions = ConfuserDatas.Confusions.ToArray();
            parameter.Packers = ConfuserDatas.Packers.ToArray();
            parameter.Logger.BeginAssembly += Logger_BeginAssembly;
            parameter.Logger.EndAssembly += Logger_EndAssembly;
            parameter.Logger.Phase += Logger_Phase;
            parameter.Logger.Log += Logger_Log;
            parameter.Logger.Progress += Logger_Progress;
            parameter.Logger.Fault += Logger_Fault;
            parameter.Logger.End += Logger_End;

            cr = new Confuser.Core.Confuser();
            thread = cr.ConfuseAsync(parameter);
            btn.Content = "Cancel";
            host.DisabledNavigation = true;
        }

        class AsmData
        {
            public AssemblyDefinition Assembly { get; set; }
            public BitmapSource Icon { get; set; }
            public string Filename { get; set; }
            public string Fullname { get; set; }
        }

        void Logger_End(object sender, LogEventArgs e)
        {
            if (!CheckAccess())
            {
                Dispatcher.Invoke(new EventHandler<LogEventArgs>(Logger_End), sender, e);
                return;
            }
            asmLbl.DataContext = new AsmData()
            {
                Assembly = null,
                Icon = (BitmapSource)FindResource("ok"),
                Filename = "Success!",
                Fullname = e.Message
            };
            log.AppendText(e.Message + "\r\n");

            progress.Value = 10000;

            thread = null;
            ex = null;
            btn.Content = "Next";
            host.DisabledNavigation = false;
        }
        Exception ex = null;
        void Logger_Fault(object sender, ExceptionEventArgs e)
        {
            if (!CheckAccess())
            {
                Dispatcher.Invoke(new EventHandler<ExceptionEventArgs>(Logger_Fault), sender, e);
                return;
            }
            asmLbl.DataContext = new AsmData()
            {
                Assembly = null,
                Icon = (BitmapSource)FindResource("error"),
                Filename = "Failure!",
                Fullname = e.Exception is ThreadAbortException ? "Cancelled." : e.Exception.Message
            };
            if (e.Exception is ThreadAbortException)
            {
                log.AppendText("Cancelled!\r\n");
            }
            else
            {
                log.AppendText("Failure!\r\n");
                log.AppendText("Message : " + e.Exception.Message + "\r\n");
            }

            thread = null;
            ex = e.Exception;
            btn.Content = "Next";
            host.DisabledNavigation = false;
        }
        void Logger_Progress(object sender, ProgressEventArgs e)
        {
            if (!CheckAccess())
            {
                Dispatcher.Invoke(new EventHandler<ProgressEventArgs>(Logger_Progress), sender, e);
                return;
            }
            progress.Value = e.Progress * 10000 / e.Total;
        }
        void Logger_Log(object sender, LogEventArgs e)
        {
            if (!CheckAccess())
            {
                Dispatcher.Invoke(new EventHandler<LogEventArgs>(Logger_Log), sender, e);
                return;
            }
            log.AppendText(e.Message + "\r\n");
            log.ScrollToEnd();
        }
        void Logger_Phase(object sender, LogEventArgs e)
        {
            if (!CheckAccess())
            {
                Dispatcher.Invoke(new EventHandler<LogEventArgs>(Logger_Phase), sender, e);
                return;
            }
            asmLbl.DataContext = new AsmData()
            {
                Assembly = null,
                Icon = (BitmapSource)FindResource("loading"),
                Filename = e.Message,
                Fullname = e.Message
            };
            log.AppendText("\r\n");
        }
        void Logger_EndAssembly(object sender, AssemblyEventArgs e)
        {
            if (!CheckAccess())
            {
                Dispatcher.Invoke(new EventHandler<AssemblyEventArgs>(Logger_EndAssembly), sender, e);
                return;
            }
        }
        void Logger_BeginAssembly(object sender, AssemblyEventArgs e)
        {
            if (!CheckAccess())
            {
                Dispatcher.Invoke(new EventHandler<AssemblyEventArgs>(Logger_BeginAssembly), sender, e);
                return;
            }
            asmLbl.DataContext = new AsmData()
            {
                Assembly = e.Assembly,
                Icon = Helper.GetIcon(e.Assembly.MainModule.FullyQualifiedName),
                Filename = e.Assembly.MainModule.FullyQualifiedName,
                Fullname = e.Assembly.FullName
            };
            log.AppendText("\r\n");
            log.ScrollToEnd();
        }


        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (thread != null)
                thread.Abort();
            else
            {
                if (ex == null)
                {
                    host.Go<string>(new Success(), parameter.DestinationPath);
                }
                else
                {
                    if (ex is ThreadAbortException)
                        host.Go<string>(new Failure(), "Operation cancelled.");
                    else
                    {
                        StringBuilder sb = new StringBuilder();
                        sb.AppendLine("Oops... Confuser crushed...");
                        sb.AppendLine();
                        sb.AppendLine(ex.GetType().FullName);
                        sb.AppendLine("Message : " + ex.Message);
                        sb.AppendLine("Stack Trace :");
                        sb.AppendLine(ex.StackTrace);
                        sb.AppendLine();
                        sb.AppendLine("Please report it!!!");
                        host.Go<string>(new Failure(), sb.ToString());
                    }
                }
            }
        }
    }
}
