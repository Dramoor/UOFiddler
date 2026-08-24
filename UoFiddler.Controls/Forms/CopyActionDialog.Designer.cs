namespace UoFiddler.Controls.Forms
{
    partial class CopyActionDialog
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
            ActionComboBox = new System.Windows.Forms.ComboBox();
            OkButton = new System.Windows.Forms.Button();
            CancelActionButton = new System.Windows.Forms.Button();
            ActionLabel = new System.Windows.Forms.Label();
            SuspendLayout();

            // 
            // ActionLabel
            // 
            ActionLabel.AutoSize = true;
            ActionLabel.Location = new System.Drawing.Point(12, 18);
            ActionLabel.Name = "ActionLabel";
            ActionLabel.Size = new System.Drawing.Size(82, 15);
            ActionLabel.TabIndex = 0;
            ActionLabel.Text = "To Action Slot:";

            // 
            // ActionComboBox
            // 
            ActionComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            ActionComboBox.FormattingEnabled = true;
            ActionComboBox.Location = new System.Drawing.Point(100, 15);
            ActionComboBox.Name = "ActionComboBox";
            ActionComboBox.Size = new System.Drawing.Size(248, 23);
            ActionComboBox.TabIndex = 1;

            // 
            // OkButton
            // 
            OkButton.Location = new System.Drawing.Point(192, 53);
            OkButton.Name = "OkButton";
            OkButton.Size = new System.Drawing.Size(75, 23);
            OkButton.TabIndex = 2;
            OkButton.Text = "OK";
            OkButton.UseVisualStyleBackColor = true;
            OkButton.Click += OkButton_Click;

            // 
            // CancelActionButton
            // 
            CancelActionButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            CancelActionButton.Location = new System.Drawing.Point(273, 53);
            CancelActionButton.Name = "CancelActionButton";
            CancelActionButton.Size = new System.Drawing.Size(75, 23);
            CancelActionButton.TabIndex = 3;
            CancelActionButton.Text = "Cancel";
            CancelActionButton.UseVisualStyleBackColor = true;
            CancelActionButton.Click += CancelActionButton_Click;

            // 
            // CopyActionDialog
            // 
            AcceptButton = OkButton;
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            CancelButton = CancelActionButton;
            ClientSize = new System.Drawing.Size(360, 90);
            Controls.Add(CancelActionButton);
            Controls.Add(OkButton);
            Controls.Add(ActionComboBox);
            Controls.Add(ActionLabel);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "CopyActionDialog";
            ShowInTaskbar = false;
            StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            Text = "Copy Action";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.ComboBox ActionComboBox;
        private System.Windows.Forms.Button OkButton;
        private System.Windows.Forms.Button CancelActionButton;
        private System.Windows.Forms.Label ActionLabel;
    }
}
