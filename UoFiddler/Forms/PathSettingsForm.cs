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

using System;
using System.Windows.Forms;
using Ultima;
using Ultima.Helpers;
using UoFiddler.Controls.Classes;

namespace UoFiddler.Forms
{
    public partial class PathSettingsForm : Form
    {
        public PathSettingsForm()
        {
            InitializeComponent();
            Icon = Options.GetFiddlerIcon();
            tsTbRootPath.Text = Files.RootDir;
        }

        private void OnClickManual(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select directory containing the client files";
                dialog.ShowNewFolderButton = false;
                if (dialog.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                Files.SetMulPath(dialog.SelectedPath);
                tsTbRootPath.Text = Files.RootDir;
                MapHelper.CheckForNewMapSize();
            }
        }

        private void OnKeyDownDir(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
            {
                return;
            }

            Files.SetMulPath(tsTbRootPath.Text);
            tsTbRootPath.Text = Files.RootDir;
            MapHelper.CheckForNewMapSize();
        }
    }
}