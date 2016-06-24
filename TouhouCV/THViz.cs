using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Gma.System.MouseKeyHook;
using Microsoft.Win32;

namespace TouhouCV
{
    public partial class THViz : Form
    {
        private TouhouCV _tcv;

        public THViz()
        {
            InitializeComponent();
            _tcv = TouhouCV.Instance;

        }

        private void THViz_Load(object sender, EventArgs e)
        {
            Subscribe();
        }

        private IKeyboardMouseEvents m_GlobalHook;

        public void Subscribe()
        {
            // Note: for the application hook, use the Hook.AppEvents() instead
            m_GlobalHook = Hook.GlobalEvents();

            m_GlobalHook.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                switch (e.KeyCode)
                {
                    case Keys.T:
                        _tcv.DoMovement = !_tcv.DoMovement;
                        break;
                    case Keys.Y:
                        _tcv.ShouldAssist = !_tcv.ShouldAssist;
                        break;
                }
            };
            m_GlobalHook.MouseMove += delegate(object sender, MouseEventArgs args)
            {
                Point p = args.Location;
                Win32.POINT lp = new Win32.POINT();
                Win32.ClientToScreen(_tcv._hWnd, ref lp);
                Point sp = new Point(p.X - lp.X - TouhouCV.BOX_SIZE.X, p.Y - lp.Y - TouhouCV.BOX_SIZE.Y);
                _tcv.AssistPoint = sp;
            };
        }
    }
}
