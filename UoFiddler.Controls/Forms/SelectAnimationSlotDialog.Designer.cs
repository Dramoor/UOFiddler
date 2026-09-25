namespace UoFiddler.Controls.Forms
{
    partial class SelectAnimationSlotDialog
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

        private void InitializeComponent()
        {
            BodyListBox = new System.Windows.Forms.ListBox();
            OkButton = new System.Windows.Forms.Button();
            CancelButton = new System.Windows.Forms.Button();
            LabelSelect = new System.Windows.Forms.Label();
            SuspendLayout();

            // BodyListBox
            BodyListBox.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            BodyListBox.FormattingEnabled = true;
            BodyListBox.ItemHeight = 15;
            BodyListBox.Location = new System.Drawing.Point(12, 32);
            BodyListBox.Name = "BodyListBox";
            BodyListBox.Size = new System.Drawing.Size(360, 274);
            BodyListBox.TabIndex = 0;

            // LabelSelect
            LabelSelect.AutoSize = true;
            LabelSelect.Location = new System.Drawing.Point(12, 9);
            LabelSelect.Name = "LabelSelect";
            LabelSelect.Size = new System.Drawing.Size(200, 15);
            LabelSelect.TabIndex = 1;
            LabelSelect.Text = "Select an empty animation slot:";

            // OkButton
            OkButton.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            OkButton.Location = new System.Drawing.Point(216, 314);
            OkButton.Name = "OkButton";
            OkButton.Size = new System.Drawing.Size(75, 23);
            OkButton.TabIndex = 2;
            OkButton.Text = "OK";
            OkButton.UseVisualStyleBackColor = true;
            OkButton.Click += OkButton_Click;

            // CancelButton
            CancelButton.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            CancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            CancelButton.Location = new System.Drawing.Point(297, 314);
            CancelButton.Name = "CancelButton";
            CancelButton.Size = new System.Drawing.Size(75, 23);
            CancelButton.TabIndex = 3;
            CancelButton.Text = "Cancel";
            CancelButton.UseVisualStyleBackColor = true;
            CancelButton.Click += CancelButton_Click;

            // SelectAnimationSlotDialog
            AcceptButton = OkButton;
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            CancelButton = CancelButton;
            ClientSize = new System.Drawing.Size(384, 349);
            Controls.Add(LabelSelect);
            Controls.Add(CancelButton);
            Controls.Add(OkButton);
            Controls.Add(BodyListBox);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "SelectAnimationSlotDialog";
            ShowInTaskbar = false;
            StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            Text = "Select Animation Slot";
            ResumeLayout(false);
            PerformLayout();
        }

        private System.Windows.Forms.ListBox BodyListBox;
        private System.Windows.Forms.Button OkButton;
        private System.Windows.Forms.Button CancelButton;
        private System.Windows.Forms.Label LabelSelect;
    }
}
