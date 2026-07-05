/***************************************************************************
 *
 * $Author: Turley
 * 
 * "THE BEER-WARE LICENSE"
 * As long as you retain this notice you can do whatever you want with 
 * this stuff. If we meet some day, and you think this stuff is worth it,
 * you can buy me a beer in return.
 *
 ***************************************************************************/

namespace UoFiddler.Forms
{
    partial class PathSettingsForm
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
            tsPathSettingsMenu = new System.Windows.Forms.ToolStrip();
            tsBtnSetPathManual = new System.Windows.Forms.ToolStripButton();
            tsTbRootPath = new System.Windows.Forms.ToolStripTextBox();
            tsPathSettingsMenu.SuspendLayout();
            SuspendLayout();
            // 
            // tsPathSettingsMenu
            // 
            tsPathSettingsMenu.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            tsPathSettingsMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { tsBtnSetPathManual, tsTbRootPath });
            tsPathSettingsMenu.Location = new System.Drawing.Point(0, 0);
            tsPathSettingsMenu.Name = "tsPathSettingsMenu";
            tsPathSettingsMenu.RenderMode = System.Windows.Forms.ToolStripRenderMode.System;
            tsPathSettingsMenu.Size = new System.Drawing.Size(520, 25);
            tsPathSettingsMenu.TabIndex = 1;
            tsPathSettingsMenu.Text = "toolStrip1";
            // 
            // tsBtnSetPathManual
            // 
            tsBtnSetPathManual.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            tsBtnSetPathManual.ImageTransparentColor = System.Drawing.Color.Magenta;
            tsBtnSetPathManual.Name = "tsBtnSetPathManual";
            tsBtnSetPathManual.Size = new System.Drawing.Size(54, 22);
            tsBtnSetPathManual.Text = "Set path";
            tsBtnSetPathManual.Click += OnClickManual;
            // 
            // tsTbRootPath
            // 
            tsTbRootPath.Name = "tsTbRootPath";
            tsTbRootPath.Size = new System.Drawing.Size(380, 25);
            tsTbRootPath.KeyDown += OnKeyDownDir;
            // 
            // PathSettingsForm
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(520, 48);
            Controls.Add(tsPathSettingsMenu);
            // add padding around the toolbar to give 10px spacing
            this.Padding = new System.Windows.Forms.Padding(10);
            DoubleBuffered = true;
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.SizableToolWindow;
            Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            MaximumSize = new System.Drawing.Size(800, 120);
            MinimumSize = new System.Drawing.Size(520, 48);
            Name = "PathSettingsForm";
            Text = "Path Settings";
            tsPathSettingsMenu.ResumeLayout(false);
            tsPathSettingsMenu.PerformLayout();
            ResumeLayout(false);
            PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ToolStripButton tsBtnSetPathManual;
        private System.Windows.Forms.ToolStrip tsPathSettingsMenu;
        private System.Windows.Forms.ToolStripTextBox tsTbRootPath;
    }
}