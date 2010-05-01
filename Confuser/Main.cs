using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Reflection;
using Confuser.Core;
using System.IO;

namespace Confuser
{
    public partial class Main : Form
    {
        public Main()
        {
            InitializeComponent();
            Control.CheckForIllegalCrossThreadCalls = false;
        }

        private void Main_Load(object sender, EventArgs e)
        {
            Assembly asm = typeof(Confusion).Assembly;
            foreach (Type i in asm.GetTypes())
            {
                if (i.IsSubclassOf(typeof(AdvancedConfusion)) || i.IsSubclassOf(typeof(StructureConfusion)))
                {
                    Confusion c = Activator.CreateInstance(i) as Confusion;
                    if (c.StandardCompatible)
                        listView1.Items.Add(new ListViewItem(c.Name) { Tag = c });
                    else
                        listView1.Items.Add(new ListViewItem(c.Name) { ForeColor = Color.Red, Tag = c });
                }
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Confuser.Core.Confuser cr = new Confuser.Core.Confuser();
            wtr = new StreamWriter(textBox3.Text + ".log", false, Encoding.Unicode);
            cr.Logging += new LoggingEventHandler(logfile);
            cr.ScreenLogging += new LoggingEventHandler(log);
            cr.Finish += new EventHandler(fin);
            cr.Fault += new ExceptionEventHandler(fault);

            button1.Enabled = false;
            loggingBox1.Reset();
            foreach (ListViewItem i in listView1.CheckedItems)
            {
                cr.Confusions.Add(i.Tag as Confusion);
            }
            cr.CompressOutput = checkBox1.Checked;
            cr.ConfuseAsync(textBox1.Text, textBox3.Text);
        }

        void fault(object sender, ExceptionEventArgs e)
        {
            MessageBox.Show(e.Exception.Message + "\r\n" + e.Exception.StackTrace);
        }

        void fin(object sender, EventArgs e)
        {
            button1.Enabled = true;
            loggingBox1.Log("");
            wtr.Dispose();
        }

        void log(object sender, LoggingEventArgs e)
        {
            loggingBox1.Log(e.Message);
        }

        StreamWriter wtr;
        void logfile(object sender, LoggingEventArgs e)
        {
            wtr.WriteLine(e.Message.Replace("\n", "").Replace("\r", ""));
        }
    }
}
