namespace IS_SOMIOD
{
    partial class Form2
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.lblVagasX = new System.Windows.Forms.Label();
            this.lblEsperaY = new System.Windows.Forms.Label();
            this.lblSemaforo = new System.Windows.Forms.Label();
            this.txtLog = new System.Windows.Forms.RichTextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // lblVagasX
            // 
            this.lblVagasX.AutoSize = true;
            this.lblVagasX.Location = new System.Drawing.Point(32, 41);
            this.lblVagasX.Name = "lblVagasX";
            this.lblVagasX.Size = new System.Drawing.Size(103, 13);
            this.lblVagasX.TabIndex = 0;
            this.lblVagasX.Text = "Vagas Descargas: 0";
            // 
            // lblEsperaY
            // 
            this.lblEsperaY.AutoSize = true;
            this.lblEsperaY.Location = new System.Drawing.Point(32, 126);
            this.lblEsperaY.Name = "lblEsperaY";
            this.lblEsperaY.Size = new System.Drawing.Size(88, 13);
            this.lblEsperaY.TabIndex = 1;
            this.lblEsperaY.Text = "Fila em Espera: 0";
            this.lblEsperaY.Click += new System.EventHandler(this.lblEsperaY_Click);
            // 
            // lblSemaforo
            // 
            this.lblSemaforo.AutoSize = true;
            this.lblSemaforo.BackColor = System.Drawing.SystemColors.ActiveBorder;
            this.lblSemaforo.Location = new System.Drawing.Point(35, 190);
            this.lblSemaforo.Name = "lblSemaforo";
            this.lblSemaforo.Size = new System.Drawing.Size(67, 13);
            this.lblSemaforo.TabIndex = 2;
            this.lblSemaforo.Text = "SEMÁFORO";
            // 
            // txtLog
            // 
            this.txtLog.Location = new System.Drawing.Point(355, 38);
            this.txtLog.Name = "txtLog";
            this.txtLog.Size = new System.Drawing.Size(433, 395);
            this.txtLog.TabIndex = 3;
            this.txtLog.Text = "";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(352, 22);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(66, 13);
            this.label1.TabIndex = 4;
            this.label1.Text = "MESSAGES";
            // 
            // Form2
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.txtLog);
            this.Controls.Add(this.lblSemaforo);
            this.Controls.Add(this.lblEsperaY);
            this.Controls.Add(this.lblVagasX);
            this.Name = "Form2";
            this.Text = "Form2";
            this.Load += new System.EventHandler(this.Form2_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblVagasX;
        private System.Windows.Forms.Label lblEsperaY;
        private System.Windows.Forms.Label lblSemaforo;
        private System.Windows.Forms.RichTextBox txtLog;
        private System.Windows.Forms.Label label1;
    }
}