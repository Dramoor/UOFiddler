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
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Windows.Forms;
using UoFiddler.Controls.Classes;
using UoFiddler.Controls.Helpers;

namespace UoFiddler.Controls.UserControls
{
    public partial class MultiMapControl : UserControl
    {
        public MultiMapControl()
        {
            InitializeComponent();
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);

            multiMapToolStripMenuItem.Tag = -1;
            // dynamically populate facet entries found in Files.RootDir
            PopulateFacetMenuItems();
        }

        private bool _moving;
        private Point _movingPoint;

        private bool _loaded;

        /// <summary>
        /// ReLoads if loaded
        /// </summary>
        private void Reload()
        {
            if (IsAncestorSiteInDesignMode || FormsDesignerHelper.IsInDesignMode())
            {
                return;
            }

            if (!_loaded)
            {
                return;
            }

            _moving = false;
            ToolStripMenuItem strip = GetCheckedFacetMenuItem();
            if (strip == null) return;

            strip.Checked = false;
            ShowImage(strip, EventArgs.Empty);
        }

        private void OnResize(object sender, EventArgs e)
        {
            if (pictureBox.Image == null)
            {
                return;
            }

            DisplayScrollBars();
            SetScrollBarValues();
            Refresh();
        }

        private void HandleScroll(object sender, ScrollEventArgs e)
        {
            pictureBox.Invalidate();
        }

        private void DisplayScrollBars()
        {
            hScrollBar.Enabled = pictureBox.Width <= pictureBox.Image.Width - vScrollBar.Width;
            vScrollBar.Enabled = pictureBox.Height <= pictureBox.Image.Height - hScrollBar.Height;
        }

        private void SetScrollBarValues()
        {
            vScrollBar.Minimum = 0;
            hScrollBar.Minimum = 0;
            if (pictureBox.Image.Size.Width - pictureBox.ClientSize.Width > 0)
            {
                hScrollBar.Maximum = pictureBox.Image.Size.Width - pictureBox.ClientSize.Width;
            }

            hScrollBar.LargeChange = hScrollBar.Maximum / 10;
            hScrollBar.SmallChange = hScrollBar.Maximum / 20;

            hScrollBar.Maximum += hScrollBar.LargeChange;

            if (pictureBox.Image.Size.Height - pictureBox.ClientSize.Height > 0)
            {
                vScrollBar.Maximum = pictureBox.Image.Size.Height - pictureBox.ClientSize.Height;
            }

            vScrollBar.LargeChange = vScrollBar.Maximum / 10;
            vScrollBar.SmallChange = vScrollBar.Maximum / 20;

            vScrollBar.Maximum += vScrollBar.LargeChange;
        }

        private void OnMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _moving = true;
                _movingPoint.X = e.X;
                _movingPoint.Y = e.Y;
                Cursor = Cursors.Hand;
            }
            else
            {
                _moving = false;
                Cursor = Cursors.Default;
            }
        }

        private void PopulateFacetMenuItems()
        {
            // remove existing dynamic facet items (keep MultiMap as first)
            var items = toolStripDropDownButton1.DropDownItems;
            // clear all except first (MultiMap)
            for (int i = items.Count - 1; i >= 0; --i)
            {
                if (items[i] != multiMapToolStripMenuItem)
                {
                    items.RemoveAt(i);
                }
            }

            // find facet*.mul files in RootDir
            if (string.IsNullOrEmpty(Ultima.Files.RootDir)) return;
            try
            {
                var files = Directory.GetFiles(Ultima.Files.RootDir, "facet*.mul");
                foreach (var f in files)
                {
                    string name = Path.GetFileNameWithoutExtension(f); // e.g. facet00
                    if (!name.StartsWith("facet", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string numPart = name.Substring(5); // after 'facet'
                    if (!int.TryParse(numPart, out int id))
                        continue;

                    var menu = new ToolStripMenuItem
                    {
                        Text = name.Replace("facet", "Facet"),
                        Tag = id
                    };
                    menu.Click += ShowImage;
                    toolStripDropDownButton1.DropDownItems.Add(menu);
                }
            }
            catch
            {
                // ignore errors
            }
        }

        private ToolStripMenuItem GetCheckedFacetMenuItem()
        {
            foreach (ToolStripItem item in toolStripDropDownButton1.DropDownItems)
            {
                if (item is ToolStripMenuItem m && m.Checked)
                    return m;
            }

            return null;
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!_moving)
            {
                return;
            }

            if (pictureBox.Image == null)
            {
                return;
            }

            int deltaX = -1 * (e.X - _movingPoint.X);
            int deltaY = -1 * (e.Y - _movingPoint.Y);

            _movingPoint.X = e.X;
            _movingPoint.Y = e.Y;

            hScrollBar.Value = Math.Max(0, Math.Min(hScrollBar.Maximum, hScrollBar.Value + deltaX));
            vScrollBar.Value = Math.Max(0, Math.Min(vScrollBar.Maximum, vScrollBar.Value + deltaY));

            pictureBox.Invalidate();
        }

        private void OnMouseUp(object sender, MouseEventArgs e)
        {
            _moving = false;
            Cursor = Cursors.Default;
        }

        private void OnFilePathChangeEvent()
        {
            Reload();
        }

        private void OnClickExportBmp(object sender, EventArgs e)
        {
            ExportMultiMapImage(ImageFormat.Bmp);
        }

        private void OnClickExportTiff(object sender, EventArgs e)
        {
            ExportMultiMapImage(ImageFormat.Tiff);
        }

        private void OnClickExportJpg(object sender, EventArgs e)
        {
            ExportMultiMapImage(ImageFormat.Jpeg);
        }

        private void OnClickExportPng(object sender, EventArgs e)
        {
            ExportMultiMapImage(ImageFormat.Png);
        }

        private void ExportMultiMapImage(ImageFormat imageFormat)
        {
            string fileExtension = Utils.GetFileExtensionFor(imageFormat);
            string fileName = Path.Combine(Options.OutputPath, $"{CheckedToString()}.{fileExtension}");

            pictureBox.Image.Save(fileName, imageFormat);

            MessageBox.Show($"{CheckedToString()} saved to {fileName}", "Export", MessageBoxButtons.OK,
                MessageBoxIcon.Information, MessageBoxDefaultButton.Button1);
        }

        private string CheckedToString()
        {
            if (multiMapToolStripMenuItem.Checked)
            {
                return "MultiMap";
            }
            var item = GetCheckedFacetMenuItem();
            if (item == null) return "Unk";
            return item.Text.Replace(" ", "");
        }

        private void ShowImage(object sender, EventArgs e)
        {
            if (!(sender is ToolStripMenuItem strip))
            {
                return;
            }

            if (strip.Checked)
            {
                return;
            }

            Cursor.Current = Cursors.WaitCursor;


            // uncheck all items in the first dropdown (Load..)
            foreach (ToolStripItem item in toolStripDropDownButton1.DropDownItems)
            {
                if (item is ToolStripMenuItem menuItem)
                    menuItem.Checked = false;
            }

            strip.Checked = true;

            int tag = strip.Tag is int ? (int)strip.Tag : -1;
            pictureBox.Image = tag == -1 ? Ultima.MultiMap.GetMultiMap() : Ultima.MultiMap.GetFacetImage(tag);

            if (pictureBox.Image != null)
            {
                DisplayScrollBars();
                SetScrollBarValues();
            }
            Cursor.Current = Cursors.Default;
        }

        private void OnClickGenerateRLE(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Select Image to convert";
                if (dialog.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                try
                {
                    Cursor.Current = Cursors.WaitCursor;

                    Bitmap image = new Bitmap(dialog.FileName);

                    if (image.Height != 2048 || image.Width != 2560)
                    {
                        MessageBox.Show("Invalid image height or width", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
                        return;
                    }

                    string path = Options.OutputPath;
                    string fileName = Path.Combine(path, "MultiMap.rle");
                    using (FileStream fs = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.Write))
                    {
                        BinaryWriter bin = new BinaryWriter(fs, Encoding.Unicode);
                        Ultima.MultiMap.SaveMultiMap(image, bin);
                    }

                    Cursor.Current = Cursors.Default;

                    MessageBox.Show($"MultiMap saved to {fileName}", "Convert",
                        MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1);
                }
                catch (FileNotFoundException)
                {
                    MessageBox.Show("No image found", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error,
                        MessageBoxDefaultButton.Button1);
                }
                finally
                {
                    Cursor.Current = Cursors.Default;
                }
            }
        }

        private void OnClickGenerateFacetFromImage(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "Select Image to convert";
                if (dialog.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                try
                {
                    Cursor.Current = Cursors.WaitCursor;

                    Bitmap image = new Bitmap(dialog.FileName);
                    string path = Options.OutputPath;
                    string fileName = Path.Combine(path, "facet.mul");
                    Ultima.MultiMap.SaveFacetImage(fileName, image);

                    Cursor.Current = Cursors.Default;

                    MessageBox.Show($"Facet saved to {fileName}", "Convert", MessageBoxButtons.OK,
                        MessageBoxIcon.Information, MessageBoxDefaultButton.Button1);
                }
                catch (FileNotFoundException)
                {
                    MessageBox.Show("No image found", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error,
                        MessageBoxDefaultButton.Button1);
                }
                finally
                {
                    Cursor.Current = Cursors.Default;
                }
            }
        }

        private void OnLoad(object sender, EventArgs e)
        {
            if (IsAncestorSiteInDesignMode || FormsDesignerHelper.IsInDesignMode())
            {
                return;
            }

            if (_loaded)
            {
                return;
            }

            multiMapToolStripMenuItem.Checked = true;
            pictureBox.Image = Ultima.MultiMap.GetMultiMap();
            if (pictureBox.Image != null)
            {
                DisplayScrollBars();
                SetScrollBarValues();
            }
            ControlEvents.FilePathChangeEvent += OnFilePathChangeEvent;
            _loaded = true;
        }

        private void OnPaint(object sender, PaintEventArgs e)
        {
            e.Graphics.Clear(Color.White);
            if (pictureBox.Image != null)
            {
                e.Graphics.DrawImage(pictureBox.Image,
                    e.ClipRectangle,
                    hScrollBar.Value, vScrollBar.Value, e.ClipRectangle.Width, e.ClipRectangle.Height,
                    GraphicsUnit.Pixel);
            }
        }
    }
}
