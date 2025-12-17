namespace IS_SOMIOD
{
    partial class Form2
    {

        private System.ComponentModel.IContainer components = null;

      protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code


        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form2));
            this.label1 = new System.Windows.Forms.Label();
            this.ParkTextBox = new System.Windows.Forms.RichTextBox();
            this.lblSemaforo = new System.Windows.Forms.Label();
            this.lblEsperaY = new System.Windows.Forms.Label();
            this.lblVagasX = new System.Windows.Forms.Label();
            this.btnLuzes = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.LuzesTextBox = new System.Windows.Forms.RichTextBox();
            this.lblLuzesStatus = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(364, 49);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(80, 13);
            this.label1.TabIndex = 9;
            this.label1.Text = "Park Messages";
            // 
            // ParkTextBox
            // 
            this.ParkTextBox.Location = new System.Drawing.Point(368, 65);
            this.ParkTextBox.Name = "ParkTextBox";
            this.ParkTextBox.Size = new System.Drawing.Size(428, 163);
            this.ParkTextBox.TabIndex = 8;
            this.ParkTextBox.Text = "";
            this.ParkTextBox.TextChanged += new System.EventHandler(this.txtLog_TextChanged);
            // 
            // lblSemaforo
            // 
            this.lblSemaforo.AutoSize = true;
            this.lblSemaforo.BackColor = System.Drawing.SystemColors.ActiveBorder;
            this.lblSemaforo.Location = new System.Drawing.Point(55, 214);
            this.lblSemaforo.Name = "lblSemaforo";
            this.lblSemaforo.Size = new System.Drawing.Size(67, 13);
            this.lblSemaforo.TabIndex = 7;
            this.lblSemaforo.Text = "SEMÁFORO";
            // 
            // lblEsperaY
            // 
            this.lblEsperaY.AutoSize = true;
            this.lblEsperaY.Location = new System.Drawing.Point(52, 150);
            this.lblEsperaY.Name = "lblEsperaY";
            this.lblEsperaY.Size = new System.Drawing.Size(88, 13);
            this.lblEsperaY.TabIndex = 6;
            this.lblEsperaY.Text = "Fila em Espera: 0";
            this.lblEsperaY.Click += new System.EventHandler(this.lblEsperaY_Click);
            // 
            // lblVagasX
            // 
            this.lblVagasX.AutoSize = true;
            this.lblVagasX.Location = new System.Drawing.Point(52, 65);
            this.lblVagasX.Name = "lblVagasX";
            this.lblVagasX.Size = new System.Drawing.Size(103, 13);
            this.lblVagasX.TabIndex = 5;
            this.lblVagasX.Text = "Vagas Descargas: 0";
            this.lblVagasX.Click += new System.EventHandler(this.lblVagasX_Click);
            // 
            // btnLuzes
            // 
            this.btnLuzes.Location = new System.Drawing.Point(52, 284);
            this.btnLuzes.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.btnLuzes.Name = "btnLuzes";
            this.btnLuzes.Size = new System.Drawing.Size(154, 89);
            this.btnLuzes.TabIndex = 10;
            this.btnLuzes.Text = "Desligar Luzes";
            this.btnLuzes.UseVisualStyleBackColor = true;
            this.btnLuzes.Click += new System.EventHandler(this.btnLuzes_Click);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(364, 240);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(86, 13);
            this.label2.TabIndex = 12;
            this.label2.Text = "Luzes Messages";
            // 
            // LuzesTextBox
            // 
            this.LuzesTextBox.Location = new System.Drawing.Point(367, 257);
            this.LuzesTextBox.Name = "LuzesTextBox";
            this.LuzesTextBox.Size = new System.Drawing.Size(428, 163);
            this.LuzesTextBox.TabIndex = 11;
            this.LuzesTextBox.Text = "";
            // 
            // lblLuzesStatus
            // 
            this.lblLuzesStatus.AutoSize = true;
            this.lblLuzesStatus.Location = new System.Drawing.Point(148, 288);
            this.lblLuzesStatus.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblLuzesStatus.Name = "lblLuzesStatus";
            this.lblLuzesStatus.Size = new System.Drawing.Size(0, 13);
            this.lblLuzesStatus.TabIndex = 13;
            // 
            // Form2
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(874, 508);
            this.Controls.Add(this.lblLuzesStatus);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.LuzesTextBox);
            this.Controls.Add(this.btnLuzes);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.ParkTextBox);
            this.Controls.Add(this.lblSemaforo);
            this.Controls.Add(this.lblEsperaY);
            this.Controls.Add(this.lblVagasX);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.MaximizeBox = false;
            this.Name = "Form2";
            this.Text = "Dashboard";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form2_FormClosing);
            this.Load += new System.EventHandler(this.Form2_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.RichTextBox ParkTextBox;
        private System.Windows.Forms.Label lblSemaforo;
        private System.Windows.Forms.Label lblEsperaY;
        private System.Windows.Forms.Label lblVagasX;
        private System.Windows.Forms.Button btnLuzes;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.RichTextBox LuzesTextBox;
        private System.Windows.Forms.Label lblLuzesStatus;
    }
}