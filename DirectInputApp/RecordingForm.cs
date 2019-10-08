using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WindowsFormsApp27
{
    public partial class RecordingForm : Form
    {

        private static readonly RecordingForm Form = new RecordingForm();
        private bool _flag = true;

        public RecordingForm()
        {
            InitializeComponent();
        }

        public static void StartForm()
        {
            Form.timer1.Start();
            Form.Show();
        }

        public static void StopForm()
        {
            Form.timer1.Stop();
            Form.Hide();
        }

        /// <summary>
        /// Makes the form fade in and out
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void timer1_Tick(object sender, EventArgs e)
        {
            if (_flag)
            {
                Opacity -= 0.1;
                if (Opacity == 0.0)
                    _flag = false;
            }
            else
            {
                Opacity += 0.1;
                if (Opacity == 1.0)
                    _flag = true;
            }
        }
    }
}
