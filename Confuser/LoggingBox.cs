using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Drawing;

namespace Confuser
{
    class LoggingBox : RichTextBox
    {
        public LoggingBox()
        {
            this.WordWrap = false;
            this.ReadOnly = true;
            this.Multiline = true;
            this.BackColor = Color.FromKnownColor(KnownColor.Window);
        }

        public void Log(string mess)
        {
            this.AppendText(mess.Replace("\n", "").Replace("\r", "") + "\r\n");
            this.ScrollToCaret();
        }

        public void Reset()
        {
            this.Text = "";
        }
    }
}
