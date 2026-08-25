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
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Ultima;
using UoFiddler.Classes;
using UoFiddler.Controls.Classes;

namespace UoFiddler.Forms
{
    public partial class OptionsForm : Form
    {
        private readonly Action _updateAllTileViewsAction;
        private readonly Action _updateMapTabAction;
        private readonly Action _updateItemsTabAction;
        private readonly Action _updateSoundTabAction;

        public OptionsForm(Action updateAllTileViewsAction,
            Action updateItemsTabAction,
            Action updateSoundTabAction,
            Action updateMapTabAction)
        {
            InitializeComponent();

            radioExportFilenameHex.Checked = AppSettings.ExportFilenameInHex;
            radioExportFilenameDec.Checked = !AppSettings.ExportFilenameInHex;
            checkBoxExportFilenameDecPad.Checked = AppSettings.ExportFilenameDecimalPadded;
            checkBoxExportFilenameDecPad.Enabled = !AppSettings.ExportFilenameInHex;

            Icon = Options.GetFiddlerIcon();

            _updateAllTileViewsAction = updateAllTileViewsAction;
            _updateItemsTabAction = updateItemsTabAction;
            _updateSoundTabAction = updateSoundTabAction;
            _updateMapTabAction = updateMapTabAction;

            TileFocusColorComboBox.MaxDropDownItems = 14;
            TileFocusColorComboBox.IntegralHeight = false;
            TileFocusColorComboBox.DrawMode = DrawMode.OwnerDrawFixed;
            TileFocusColorComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            TileFocusColorComboBox.DrawItem += TileFocusColorComboBoxDrawItem;

            TileFocusColorComboBox.DataSource = typeof(Color).GetProperties()
                .Where(x => x.PropertyType == typeof(Color))
                .Select(x => x.GetValue(null)).ToList();

            TileFocusColorComboBox.SelectedItem = Options.TileFocusColor;

            TileSelectionColorComboBox.MaxDropDownItems = 14;
            TileSelectionColorComboBox.IntegralHeight = false;
            TileSelectionColorComboBox.DrawMode = DrawMode.OwnerDrawFixed;
            TileSelectionColorComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            TileSelectionColorComboBox.DrawItem += TileSelectionColorComboBoxDrawItem;

            TileSelectionColorComboBox.DataSource = typeof(Color).GetProperties()
                .Where(x => x.PropertyType == typeof(Color))
                .Select(x => x.GetValue(null)).ToList();

            checkboxRemoveTileBorder.Checked = Options.RemoveTileBorder;

            TileSelectionColorComboBox.SelectedItem = Options.TileSelectionColor;

            PreviewBackgroundColorButton.BackColor = Options.PreviewBackgroundColor;

            checkBoxCacheData.Checked = Files.CacheData;
            checkBoxNewMapSize.Checked = Map.Felucca.Width == 7168;
            checkBoxuseDiff.Checked = Map.UseDiff;
            checkBoxPolSoundIdOffset.Checked = Options.PolSoundIdOffset;
            numericUpDownItemSizeWidth.Value = Options.ArtItemSizeWidth;
            numericUpDownItemSizeHeight.Value = Options.ArtItemSizeHeight;
            checkBoxItemClip.Checked = Options.ArtItemClip;

            // Build dynamic map name controls instead of hardcoded ones
            BuildDynamicMapPanel();

            cmdtext.Text = Options.MapCmd;
            argstext.Text = Options.MapArgs;
            textBoxOutputPath.Text = Options.OutputPath;
        }

        private void OnClickApply(object sender, EventArgs e)
        {
            if (checkBoxPolSoundIdOffset.Checked != Options.PolSoundIdOffset)
            {
                Options.PolSoundIdOffset = checkBoxPolSoundIdOffset.Checked;

                _updateSoundTabAction();
            }

            Files.CacheData = checkBoxCacheData.Checked;

            if (checkBoxNewMapSize.Checked != (Map.Felucca.Width == 7168))
            {
                if (checkBoxNewMapSize.Checked)
                {
                    Map.Felucca.Width = 7168;
                    Map.Trammel.Width = 7168;
                }
                else
                {
                    Map.Felucca.Width = 6144;
                    Map.Trammel.Width = 6144;
                }

                _updateMapTabAction();
            }

            if (checkBoxuseDiff.Checked != Map.UseDiff)
            {
                Map.UseDiff = checkBoxuseDiff.Checked;
                ControlEvents.FireMapDiffChangeEvent();
            }

            if (numericUpDownItemSizeWidth.Value != Options.ArtItemSizeWidth
                || numericUpDownItemSizeHeight.Value != Options.ArtItemSizeHeight)
            {
                Options.ArtItemSizeWidth = (int)numericUpDownItemSizeWidth.Value;
                Options.ArtItemSizeHeight = (int)numericUpDownItemSizeHeight.Value;

                _updateItemsTabAction();
            }

            if (checkBoxItemClip.Checked != Options.ArtItemClip)
            {
                Options.ArtItemClip = checkBoxItemClip.Checked;

                _updateItemsTabAction();
            }

            if ((Color)TileFocusColorComboBox.SelectedItem != Options.TileFocusColor)
            {
                Options.TileFocusColor = (Color)TileFocusColorComboBox.SelectedItem;

                _updateAllTileViewsAction();
            }

            if ((Color)TileSelectionColorComboBox.SelectedItem != Options.TileSelectionColor)
            {
                Options.TileSelectionColor = (Color)TileSelectionColorComboBox.SelectedItem;

                _updateAllTileViewsAction();
            }

            if (checkboxRemoveTileBorder.Checked != Options.RemoveTileBorder)
            {
                Options.RemoveTileBorder = checkboxRemoveTileBorder.Checked;

                _updateAllTileViewsAction();
            }

            if (PreviewBackgroundColorButton.BackColor != Options.PreviewBackgroundColor)
            {
                Options.PreviewBackgroundColor = PreviewBackgroundColorButton.BackColor;
                ControlEvents.FirePreviewBackgroundColorChangeEvent();
            }

            // Save all dynamic map names
            SaveDynamicMapNames();

            Options.MapCmd = cmdtext.Text;
            Options.MapArgs = argstext.Text;

            if (Directory.Exists(textBoxOutputPath.Text))
            {
                Options.OutputPath = textBoxOutputPath.Text;
            }

            bool newHex = radioExportFilenameHex.Checked;
            bool newPad = checkBoxExportFilenameDecPad.Checked;
            if (newHex != AppSettings.ExportFilenameInHex || newPad != AppSettings.ExportFilenameDecimalPadded)
            {
                AppSettings.ExportFilenameInHex = newHex;
                AppSettings.ExportFilenameDecimalPadded = newPad;
                Options.ExportFilenameInHex = newHex;
                Options.ExportFilenameDecimalPadded = newPad;
                AppSettings.Save();
            }
        }

        private void OnExportFilenameFormatChanged(object sender, EventArgs e)
        {
            checkBoxExportFilenameDecPad.Enabled = !radioExportFilenameHex.Checked;
        }

        private void OnClickBrowseOutputPath(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select directory";
                dialog.ShowNewFolderButton = true;

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    textBoxOutputPath.Text = dialog.SelectedPath;
                }
            }
        }

        private void TileFocusColorComboBoxDrawItem(object sender, DrawItemEventArgs e)
        {
            e.DrawBackground();

            if (e.Index < 0)
            {
                return;
            }

            var itemText = TileFocusColorComboBox.GetItemText(TileFocusColorComboBox.Items[e.Index]);
            var color = (Color)TileFocusColorComboBox.Items[e.Index];

            var rectangle = new Rectangle(e.Bounds.Left + 1, e.Bounds.Top + 1, 2 * (e.Bounds.Height - 2), e.Bounds.Height - 2);
            var textRectangle = Rectangle.FromLTRB(rectangle.Right + 2, e.Bounds.Top, e.Bounds.Right, e.Bounds.Bottom);

            using (var b = new SolidBrush(color))
            {
                e.Graphics.FillRectangle(b, rectangle);
            }

            e.Graphics.DrawRectangle(Pens.Black, rectangle);

            TextRenderer.DrawText(e.Graphics, itemText, TileFocusColorComboBox.Font, textRectangle, TileFocusColorComboBox.ForeColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        }

        private void TileSelectionColorComboBoxDrawItem(object sender, DrawItemEventArgs e)
        {
            e.DrawBackground();

            if (e.Index < 0)
            {
                return;
            }

            var itemText = TileSelectionColorComboBox.GetItemText(TileSelectionColorComboBox.Items[e.Index]);
            var color = (Color)TileSelectionColorComboBox.Items[e.Index];

            var rectangle = new Rectangle(e.Bounds.Left + 1, e.Bounds.Top + 1, 2 * (e.Bounds.Height - 2), e.Bounds.Height - 2);
            var textRectangle = Rectangle.FromLTRB(rectangle.Right + 2, e.Bounds.Top, e.Bounds.Right, e.Bounds.Bottom);

            using (var b = new SolidBrush(color))
            {
                e.Graphics.FillRectangle(b, rectangle);
            }

            e.Graphics.DrawRectangle(Pens.Black, rectangle);

            TextRenderer.DrawText(e.Graphics, itemText, TileSelectionColorComboBox.Font, textRectangle, TileSelectionColorComboBox.ForeColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        }

        private void RestoreDefaultsButton_Click(object sender, EventArgs e)
        {
            const string title = "Restore defaults";
            const string message = "Do you want to reset tile views settings to default?";

            if (MessageBox.Show(message, title, MessageBoxButtons.YesNo) == DialogResult.No)
            {
                return;
            }

            checkboxRemoveTileBorder.Checked = false;

            if (AppSettings.DarkMode)
            {
                TileFocusColorComboBox.SelectedItem = Color.Red;
                TileSelectionColorComboBox.SelectedItem = Color.MediumTurquoise;
                PreviewBackgroundColorButton.BackColor = Color.FromArgb(32, 32, 32);
            }
            else
            {
                TileFocusColorComboBox.SelectedItem = Color.DarkRed;
                TileSelectionColorComboBox.SelectedItem = Color.DodgerBlue;
                PreviewBackgroundColorButton.BackColor = Color.White;
            }
        }

        private void PreviewBackgroundColorButton_Click(object sender, EventArgs e)
        {
            using var dlg = new ColorDialog { Color = PreviewBackgroundColorButton.BackColor };
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                PreviewBackgroundColorButton.BackColor = dlg.Color;
            }
        }

        private void OnClickClose(object sender, EventArgs e)
        {
            Close();
        }

        private Panel _mapNamesScrollPanel;
        private TextBox[] _mapNameTextBoxes;

        private void BuildDynamicMapPanel()
        {
            // Store all map textboxes in an array for easy access
            int mapCount = Options.MapNames.Length;
            _mapNameTextBoxes = new TextBox[mapCount];

            // Remove all old hardcoded map controls (map0Nametext through map5Nametext, and their labels)
            Control[] controlsToRemove = new Control[12];
            int removeIndex = 0;

            // Labels
            foreach (Control ctrl in groupBox3.Controls)
            {
                if (ctrl is Label label && (label.Text.StartsWith("map") && label.Text.EndsWith("Name")))
                {
                    controlsToRemove[removeIndex++] = ctrl;
                }
            }

            // TextBoxes (map0Nametext through map5Nametext)
            if (map0Nametext != null && groupBox3.Controls.Contains(map0Nametext)) controlsToRemove[removeIndex++] = map0Nametext;
            if (map1Nametext != null && groupBox3.Controls.Contains(map1Nametext)) controlsToRemove[removeIndex++] = map1Nametext;
            if (map2Nametext != null && groupBox3.Controls.Contains(map2Nametext)) controlsToRemove[removeIndex++] = map2Nametext;
            if (map3Nametext != null && groupBox3.Controls.Contains(map3Nametext)) controlsToRemove[removeIndex++] = map3Nametext;
            if (map4Nametext != null && groupBox3.Controls.Contains(map4Nametext)) controlsToRemove[removeIndex++] = map4Nametext;
            if (map5Nametext != null && groupBox3.Controls.Contains(map5Nametext)) controlsToRemove[removeIndex++] = map5Nametext;

            // Remove them
            for (int i = 0; i < removeIndex; i++)
            {
                if (controlsToRemove[i] != null)
                    groupBox3.Controls.Remove(controlsToRemove[i]);
            }

            // Create a scrollable panel for all maps
            _mapNamesScrollPanel = new Panel
            {
                AutoScroll = true,
                Location = new Point(4, 15),
                Size = new Size(groupBox3.Width - 20, 180),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            // Add label + textbox for each map
            for (int i = 0; i < mapCount; i++)
            {
                int yPosition = i * 30;

                // Create label
                var label = new Label
                {
                    Text = $"map{i} Name",
                    AutoSize = true,
                    Location = new Point(7, yPosition + 5)
                };
                _mapNamesScrollPanel.Controls.Add(label);

                // Create textbox
                var textBox = new TextBox
                {
                    Text = Options.MapNames[i],
                    Location = new Point(90, yPosition + 2),
                    Width = _mapNamesScrollPanel.Width - 110,
                    Height = 23
                };

                _mapNameTextBoxes[i] = textBox;
                _mapNamesScrollPanel.Controls.Add(textBox);
            }

            // Add the scrollable panel to groupBox3
            groupBox3.Controls.Add(_mapNamesScrollPanel);
        }

        private void SaveDynamicMapNames()
        {
            if (_mapNameTextBoxes == null)
                return;

            bool changed = false;
            for (int i = 0; i < _mapNameTextBoxes.Length && i < Options.MapNames.Length; i++)
            {
                if (_mapNameTextBoxes[i] != null && _mapNameTextBoxes[i].Text != Options.MapNames[i])
                {
                    Options.MapNames[i] = _mapNameTextBoxes[i].Text;
                    changed = true;
                }
            }

            if (changed)
            {
                ControlEvents.FireMapNameChangeEvent();
            }
        }
    }
}
