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

        }

        private void timer_Tick(object sender, EventArgs e)
        {

        }
    }
}
