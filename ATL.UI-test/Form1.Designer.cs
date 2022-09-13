namespace ATL.UI_test
{
    partial class Form1
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
            this.GoBtn = new System.Windows.Forms.Button();
            this.ProgressLbl = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // GoBtn
            // 
            this.GoBtn.Location = new System.Drawing.Point(12, 24);
            this.GoBtn.Name = "GoBtn";
            this.GoBtn.Size = new System.Drawing.Size(75, 23);
            this.GoBtn.TabIndex = 0;
            this.GoBtn.Text = "GO";
            this.GoBtn.UseVisualStyleBackColor = true;
            this.GoBtn.Click += new System.EventHandler(this.GoBtn_Click);
            // 
            // ProgressLbl
            // 
            this.ProgressLbl.AutoSize = true;
            this.ProgressLbl.Location = new System.Drawing.Point(93, 29);
            this.ProgressLbl.Name = "ProgressLbl";
            this.ProgressLbl.Size = new System.Drawing.Size(55, 13);
            this.ProgressLbl.TabIndex = 1;
            this.ProgressLbl.Text = "progress%";
            this.ProgressLbl.Visible = false;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(226, 69);
            this.Controls.Add(this.ProgressLbl);
            this.Controls.Add(this.GoBtn);
            this.Name = "Form1";
            this.Text = "Form1";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button GoBtn;
        private System.Windows.Forms.Label ProgressLbl;
    }
}

