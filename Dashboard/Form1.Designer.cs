namespace IS_SOMIOD
{
    partial class Form1
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
            this.label1 = new System.Windows.Forms.Label();
            this.txtLog = new System.Windows.Forms.RichTextBox();
            this.lblSemaforo = new System.Windows.Forms.Label();
            this.lblEsperaY = new System.Windows.Forms.Label();
            this.lblVagasX = new System.Windows.Forms.Label();
            this.btnLuzes = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(486, 60);
            this.label1.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(82, 16);
            this.label1.TabIndex = 9;
            this.label1.Text = "MESSAGES";
            // 
            // txtLog
            // 
            this.txtLog.Location = new System.Drawing.Point(490, 80);
            this.txtLog.Margin = new System.Windows.Forms.Padding(4);
            this.txtLog.Name = "txtLog";
            this.txtLog.Size = new System.Drawing.Size(576, 485);
            this.txtLog.TabIndex = 8;
            this.txtLog.Text = "";
            this.txtLog.TextChanged += new System.EventHandler(this.txtLog_TextChanged);
            // 
            // lblSemaforo
            // 
            this.lblSemaforo.AutoSize = true;
            this.lblSemaforo.BackColor = System.Drawing.SystemColors.ActiveBorder;
            this.lblSemaforo.Location = new System.Drawing.Point(73, 264);
            this.lblSemaforo.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblSemaforo.Name = "lblSemaforo";
            this.lblSemaforo.Size = new System.Drawing.Size(83, 16);
            this.lblSemaforo.TabIndex = 7;
            this.lblSemaforo.Text = "SEMÁFORO";
            // 
            // lblEsperaY
            // 
            this.lblEsperaY.AutoSize = true;
            this.lblEsperaY.Location = new System.Drawing.Point(69, 185);
            this.lblEsperaY.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblEsperaY.Name = "lblEsperaY";
            this.lblEsperaY.Size = new System.Drawing.Size(111, 16);
            this.lblEsperaY.TabIndex = 6;
            this.lblEsperaY.Text = "Fila em Espera: 0";
            this.lblEsperaY.Click += new System.EventHandler(this.lblEsperaY_Click);
            // 
            // lblVagasX
            // 
            this.lblVagasX.AutoSize = true;
            this.lblVagasX.Location = new System.Drawing.Point(69, 80);
            this.lblVagasX.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblVagasX.Name = "lblVagasX";
            this.lblVagasX.Size = new System.Drawing.Size(130, 16);
            this.lblVagasX.TabIndex = 5;
            this.lblVagasX.Text = "Vagas Descargas: 0";
            this.lblVagasX.Click += new System.EventHandler(this.lblVagasX_Click);
            // 
            // btnLuzes
            // 
            this.btnLuzes.Location = new System.Drawing.Point(72, 325);
            this.btnLuzes.Name = "btnLuzes";
            this.btnLuzes.Size = new System.Drawing.Size(84, 29);
            this.btnLuzes.TabIndex = 10;
            this.btnLuzes.UseVisualStyleBackColor = true;
            this.btnLuzes.Click += new System.EventHandler(this.btnLuzes_Click);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1165, 625);
            this.Controls.Add(this.btnLuzes);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.txtLog);
            this.Controls.Add(this.lblSemaforo);
            this.Controls.Add(this.lblEsperaY);
            this.Controls.Add(this.lblVagasX);
            this.Name = "Form1";
            this.Text = "Dashboard (Form2)";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form2_FormClosing);
            this.Load += new System.EventHandler(this.Form2_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.RichTextBox txtLog;
        private System.Windows.Forms.Label lblSemaforo;
        private System.Windows.Forms.Label lblEsperaY;
        private System.Windows.Forms.Label lblVagasX;
        private System.Windows.Forms.Button btnLuzes;
    }
}