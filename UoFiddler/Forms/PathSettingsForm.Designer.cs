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
            this.tsPathSettingsMenu = new System.Windows.Forms.ToolStrip();
            this.tsBtnSetPathManual = new System.Windows.Forms.ToolStripButton();
            this.tsTbRootPath = new System.Windows.Forms.ToolStripTextBox();
            this.lblRootPath = new System.Windows.Forms.ToolStripLabel();
            this.tsPathSettingsMenu.SuspendLayout();
            this.SuspendLayout();
            // 
            // tsPathSettingsMenu
            // 
            this.tsPathSettingsMenu.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.tsPathSettingsMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.lblRootPath,
            this.tsTbRootPath,
            this.tsBtnSetPathManual});
            this.tsPathSettingsMenu.Location = new System.Drawing.Point(0, 0);
            this.tsPathSettingsMenu.Name = "tsPathSettingsMenu";
           // this.tsPathSettingsMenu.RenderMode = System.Windows.Forms.ToolStripRenderMode.System;
            this.tsPathSettingsMenu.Size = new System.Drawing.Size(744, 25);
            this.tsPathSettingsMenu.TabIndex = 1;
            this.tsPathSettingsMenu.Text = "toolStrip1";
            // 
            // lblRootPath
            // 
            this.lblRootPath.Name = "lblRootPath";
            this.lblRootPath.Size = new System.Drawing.Size(72, 22);
            this.lblRootPath.Text = "Data Path:";
            // 
            // tsTbRootPath
            // 
            this.tsTbRootPath.Name = "tsTbRootPath";
            this.tsTbRootPath.Size = new System.Drawing.Size(500, 25);
            this.tsTbRootPath.KeyDown += new System.Windows.Forms.KeyEventHandler(this.OnKeyDownDir);
            // 
            // tsBtnSetPathManual
            // 
            this.tsBtnSetPathManual.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.tsBtnSetPathManual.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.tsBtnSetPathManual.Name = "tsBtnSetPathManual";
            this.tsBtnSetPathManual.Size = new System.Drawing.Size(35, 22);
            this.tsBtnSetPathManual.Text = "Set Path";
            this.tsBtnSetPathManual.ToolTipText = "Browse for data folder";
            this.tsBtnSetPathManual.Click += new System.EventHandler(this.OnClickManual);
            // 
            // PathSettingsForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(744, 60);
            this.Controls.Add(this.tsPathSettingsMenu);
            this.DoubleBuffered = true;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.SizableToolWindow;
            this.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.MaximumSize = new System.Drawing.Size(1200, 90);
            this.MinimumSize = new System.Drawing.Size(744, 60);
            this.Name = "PathSettingsForm";
            this.Text = "Path Settings";
            this.tsPathSettingsMenu.ResumeLayout(false);
            this.tsPathSettingsMenu.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ToolStripButton tsBtnSetPathManual;
        private System.Windows.Forms.ToolStrip tsPathSettingsMenu;
        private System.Windows.Forms.ToolStripTextBox tsTbRootPath;
        private System.Windows.Forms.ToolStripLabel lblRootPath;
    }
}