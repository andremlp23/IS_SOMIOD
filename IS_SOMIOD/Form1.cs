using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace IS_SOMIOD
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();

            // Wire the +1 button click here so the handler is definitely attached
            this.btnMais1.Click += new EventHandler(this.btnMais1_Click);
        }


        private void btn5_Click(object sender, EventArgs e)
        {
            btn5.BackColor = Color.Red;
            //Send estou ocupado to SOMIOD

        }

        private void btn6_Click(object sender, EventArgs e)
        {
            btn6.BackColor = Color.Red;
            //Send estou ocupado to SOMIOD
        }

        private void btn7_Click(object sender, EventArgs e)
        {
            btn7.BackColor = Color.Red;
            //Send estou ocupado to SOMIOD
        }

        private void btn8_Click(object sender, EventArgs e)
        {
            btn8.BackColor = Color.Red;
            //Send estou ocupado to SOMIOD
        }

        // Handler for the "+1" button (control name: btnMais1)
        private void btnMais1_Click(object sender, EventArgs e)
        {
            // Color to mark occupied
            var occupiedColor = Color.Red;

            // Priority: try btn5..btn8 first — set first one with default color
            if (IsDefaultBackColor(btn5)) { btn5.BackColor = occupiedColor; return; }
            if (IsDefaultBackColor(btn6)) { btn6.BackColor = occupiedColor; return; }
            if (IsDefaultBackColor(btn7)) { btn7.BackColor = occupiedColor; return; }
            if (IsDefaultBackColor(btn8)) { btn8.BackColor = occupiedColor; return; }

            // Else: try btn1..btn4 — set first one with default color
            if (IsDefaultBackColor(btn1)) { btn1.BackColor = occupiedColor; return; }
            if (IsDefaultBackColor(btn2)) { btn2.BackColor = occupiedColor; return; }
            if (IsDefaultBackColor(btn3)) { btn3.BackColor = occupiedColor; return; }
            if (IsDefaultBackColor(btn4)) { btn4.BackColor = occupiedColor; return; }

            // If none of 1..8 are available, mark the +1 button itself red
            btnMais1.BackColor = occupiedColor;

            // Optionally: send "estou ocupado" to SOMIOD here
        }

        // Helper to decide if a button has the default WinForms background
        private bool IsDefaultBackColor(Control c)
        {
            // Consider common "default" cases: UseVisualStyleBackColor true, BackColor empty, or equals SystemColors.Control
            var btn = c as Button;
            if (btn != null && btn.UseVisualStyleBackColor) return true;
            if (c.BackColor.IsEmpty) return true;
            return c.BackColor.ToArgb() == SystemColors.Control.ToArgb();
        }
    }
}