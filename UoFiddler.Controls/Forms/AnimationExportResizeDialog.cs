using System;
using System.Drawing;
using System.Windows.Forms;

namespace UoFiddler.Controls.Forms
{
    public class AnimationExportResizeDialog : Form
    {
        private Label labelPercentage;
        private NumericUpDown numericUpDownPercentage;
        private Button buttonOK;
        private Button buttonCancel;

        public int ResizePercentage { get; private set; }

        public AnimationExportResizeDialog()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.labelPercentage = new Label();
            this.numericUpDownPercentage = new NumericUpDown();
            this.buttonOK = new Button();
            this.buttonCancel = new Button();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownPercentage)).BeginInit();
            this.SuspendLayout();

            this.labelPercentage.AutoSize = true;
            this.labelPercentage.Location = new Point(12, 15);
            this.labelPercentage.Name = "labelPercentage";
            this.labelPercentage.Size = new Size(213, 15);
            this.labelPercentage.TabIndex = 0;
            this.labelPercentage.Text = "Resize Percentage (10% - 100%):";

            this.numericUpDownPercentage.Location = new Point(230, 12);
            this.numericUpDownPercentage.Name = "numericUpDownPercentage";
            this.numericUpDownPercentage.Size = new Size(50, 23);
            this.numericUpDownPercentage.TabIndex = 1;
            this.numericUpDownPercentage.Value = 100;
            this.numericUpDownPercentage.Minimum = 10;
            this.numericUpDownPercentage.Maximum = 100;

            this.buttonOK.DialogResult = DialogResult.OK;
            this.buttonOK.Location = new Point(124, 50);
            this.buttonOK.Name = "buttonOK";
            this.buttonOK.Size = new Size(75, 23);
            this.buttonOK.TabIndex = 2;
            this.buttonOK.Text = "OK";
            this.buttonOK.UseVisualStyleBackColor = true;
            this.buttonOK.Click += ButtonOK_Click;

            this.buttonCancel.DialogResult = DialogResult.Cancel;
            this.buttonCancel.Location = new Point(205, 50);
            this.buttonCancel.Name = "buttonCancel";
            this.buttonCancel.Size = new Size(75, 23);
            this.buttonCancel.TabIndex = 3;
            this.buttonCancel.Text = "Cancel";
            this.buttonCancel.UseVisualStyleBackColor = true;

            this.AcceptButton = this.buttonOK;
            this.CancelButton = this.buttonCancel;
            this.ClientSize = new Size(292, 85);
            this.Controls.Add(this.buttonCancel);
            this.Controls.Add(this.buttonOK);
            this.Controls.Add(this.numericUpDownPercentage);
            this.Controls.Add(this.labelPercentage);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "AnimationExportResizeDialog";
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Text = "Export Animation - Resize";
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownPercentage)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private void ButtonOK_Click(object sender, EventArgs e)
        {
            ResizePercentage = (int)this.numericUpDownPercentage.Value;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
