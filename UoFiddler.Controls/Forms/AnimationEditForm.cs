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
using System.Linq;
using System.Windows.Forms;
using System.Windows.Media.Imaging;
using Ultima;
using UoFiddler.Controls.Classes;

namespace UoFiddler.Controls.Forms
{
    public partial class AnimationEditForm : Form
    {
        private class MapItem
        {
            public int Index { get; }
            public string Text { get; }
            public MapItem(int index, string text) { Index = index; Text = text; }
            public override string ToString() => Text;
        }
        private static readonly int[] _animCx = new int[5];
        private static readonly int[] _animCy = new int[5];
        private bool _loaded;
        private int _fileType;
        private int _currentAction;
        private int _currentBody;
        private int _currentDir;
        private Point _framePoint;
        private bool _showOnlyValid;
        private static bool _drawEmpty;
        private static bool _drawFull;
        private static readonly Color _whiteConvert = Color.FromArgb(255, 255, 255, 255);

        private static readonly Pen _blackUnDrawTransparent = new Pen(Color.FromArgb(0, 0, 0, 0), 1);
        private static readonly Pen _blackUnDrawOpaque = new Pen(Color.FromArgb(255, 0, 0, 0), 1);
        private static Pen _blackUndraw = _blackUnDrawOpaque;

        private static readonly SolidBrush _whiteUnDrawTransparent = new SolidBrush(Color.FromArgb(0, 255, 255, 255));
        private static readonly SolidBrush _whiteUnDrawOpaque = new SolidBrush(Color.FromArgb(255, 255, 255, 255));
        private static SolidBrush _whiteUnDraw = _whiteUnDrawOpaque;

        public AnimationEditForm()
        {
            InitializeComponent();
            Icon = Options.GetFiddlerIcon();

            SelectFileToolStripComboBox.SelectedIndex = 0;
            FramesListView.MultiSelect = true;

            _fileType = 0;
            _currentDir = 0;
            _framePoint = new Point(AnimationPictureBox.Width / 2, AnimationPictureBox.Height / 2);
            _showOnlyValid = false;
            _loaded = false;
        }

        private static int GetOppositeDirection(int dir)
        {
            // DirectionTrackBar uses 0..4 mapping for directions: 0 = down, 1 = down-left, 2 = left, 3 = up-left, 4 = up
            // Opposite is simply rotated 180 degrees; map to the same 0..4 space by reversing order
            // 0 <-> 4, 1 <-> 3, 2 stays 2
            switch (dir)
            {
                case 0: return 4;
                case 1: return 3;
                case 2: return 2;
                case 3: return 1;
                case 4: return 0;
                default: return dir;
            }
        }

        private static Point GetTilePixelOffsetForDir(int dir, int dx, int dy)
        {
            // Return pixel offset (x,y) for one isometric tile in given direction.
            // Using common UO offsets: moving up subtracts dy vertically, moving left/right adjust by dx.
            switch (dir)
            {
                case 0: // down
                    return new Point(-4, dy);
                case 1: // down-left
                    return new Point(-dx, dy/2);
                case 2: // left
                    return new Point(-dx*2, 0);
                case 3: // up-left
                    return new Point(-dx, -dy/2);
                case 4: // up
                    return new Point(0, -dy);
                default:
                    return Point.Empty;
            }
        }

        private void CbDrawMounted_CheckedChanged(object sender, EventArgs e)
        {
            AnimationPictureBox.Invalidate();
        }

        private void CbDrawOppositeHuman_CheckedChanged(object sender, EventArgs e)
        {
            AnimationPictureBox.Invalidate();
        }

        private readonly string[][] _animNames =
        {
            new string[]
            {
                "Walk",
                "Run",
                "Idle",
                "Eat",
                "Alert",
                "Attack1",
                "Attack2",
                "GetHit",
                "Die1",
                "Idle",
                "Fidget",
                "LieDown",
                "Die2"
            }, //animal
            new string[]
            {
                "Walk",
                "Idle",
                "Die1",
                "Die2",
                "Attack1",
                "Attack2",
                "Attack3",
                "AttackBow",
                "AttackCrossBow",
                "AttackThrow",
                "GetHit",
                "Pillage",
                "Stomp",
                "Cast2",
                "Cast3",
                "BlockRight",
                "BlockLeft",
                "Idle",
                "Fidget",
                "Fly",
                "TakeOff",
                "GetHitInAir"
            }, //Monster
            new string[]
            {
                "Walk_01",
                "WalkStaff_01",
                "Run_01",
                "RunStaff_01",
                "Idle_01",
                "Idle_01",
                "Fidget_Yawn_Stretch_01",
                "CombatIdle1H_01",
                "CombatIdle1H_01",
                "AttackSlash1H_01",
                "AttackPierce1H_01",
                "AttackBash1H_01",
                "AttackBash2H_01",
                "AttackSlash2H_01",
                "AttackPierce2H_01",
                "CombatAdvance_1H_01",
                "Spell1",
                "Spell2",
                "AttackBow_01",
                "AttackCrossbow_01",
                "GetHit_Fr_Hi_01",
                "Die_Hard_Fwd_01",
                "Die_Hard_Back_01",
                "Horse_Walk_01",
                "Horse_Run_01",
                "Horse_Idle_01",
                "Horse_Attack1H_SlashRight_01",
                "Horse_AttackBow_01",
                "Horse_AttackCrossbow_01",
                "Horse_Attack2H_SlashRight_01",
                "Block_Shield_Hard_01",
                "Punch_Punch_Jab_01",
                "Bow_Lesser_01",
                "Salute_Armed1h_01",
                "Ingest_Eat_01"
            } //human
        };

        private void OnLoad(object sender, EventArgs e)
        {
            Options.LoadedUltimaClass["AnimationEdit"] = true;

            AnimationListTreeView.BeginUpdate();
            try
            {
                AnimationListTreeView.Nodes.Clear();
                if (_fileType != 0)
                {
                    int count = Animations.GetAnimCount(_fileType);
                    TreeNode[] nodes = new TreeNode[count];
                    for (int i = 0; i < count; ++i)
                    {
                        int animLength = Animations.GetAnimLength(i, _fileType);
                        string type = animLength == 22 ? "H" : animLength == 13 ? "L" : "P";
                        TreeNode node = new TreeNode
                        {
                            Tag = i,
                            Text = $"{type}: {i} ({BodyConverter.GetTrueBody(_fileType, i)})"
                        };

                        bool valid = false;
                        for (int j = 0; j < animLength; ++j)
                        {
                            TreeNode treeNode = new TreeNode
                            {
                                Tag = j,
                                Text = string.Format("{0:D2} {1}", j, _animNames[animLength == 22 ? 1 : animLength == 13 ? 0 : 2][j])
                            };

                            if (AnimationEdit.IsActionDefined(_fileType, i, j))
                            {
                                valid = true;
                            }
                            else
                            {
                                treeNode.ForeColor = Color.Red;
                            }

                            node.Nodes.Add(treeNode);
                        }

                        if (!valid)
                        {
                            if (_showOnlyValid)
                            {
                                continue;
                            }

                            node.ForeColor = Color.Red;
                        }

                        nodes[i] = node;
                    }

                    AnimationListTreeView.Nodes.AddRange(nodes.Where(n => n != null).ToArray());
                }
            }
            finally
            {
                AnimationListTreeView.EndUpdate();
            }

            if (AnimationListTreeView.Nodes.Count > 0)
            {
                AnimationListTreeView.SelectedNode = AnimationListTreeView.Nodes[0];
            }

            if (!_loaded)
            {
                ControlEvents.FilePathChangeEvent += OnFilePathChangeEvent;
            }

            _loaded = true;
        }

        private void OnFilePathChangeEvent()
        {
            if (!_loaded)
            {
                return;
            }

            _fileType = 0;
            _currentDir = 0;
            _currentAction = 0;
            _currentBody = 0;
            SelectFileToolStripComboBox.SelectedIndex = 0;
            _framePoint = new Point(AnimationPictureBox.Width / 2, AnimationPictureBox.Height / 2);
            _showOnlyValid = false;
            ShowOnlyValidToolStripMenuItem.Checked = false;
            OnLoad(null);
        }

        private TreeNode GetNode(int tag)
        {
            return _showOnlyValid
                ? AnimationListTreeView.Nodes.Cast<TreeNode>().FirstOrDefault(node => (int)node.Tag == tag)
                : AnimationListTreeView.Nodes[tag];
        }

        private unsafe void SetPaletteBox()
        {
            if (_fileType == 0)
            {
                return;
            }

            // TODO: why is bitmapWidth constant and height is taken from picturebox?
            // TODO: looks like the value is the same as array size for pallete in AnimIdx
            const int bitmapWidth = 256;
            int bitmapHeight = PalettePictureBox.Height;

            AnimIdx edit = AnimationEdit.GetAnimation(_fileType, _currentBody, _currentAction, _currentDir);
            Bitmap bmp = new Bitmap(bitmapWidth, bitmapHeight, PixelFormat.Format16bppArgb1555);
            if (edit != null)
            {
                BitmapData bd = bmp.LockBits(new Rectangle(0, 0, bitmapWidth, bitmapHeight), ImageLockMode.WriteOnly, PixelFormat.Format16bppArgb1555);
                ushort* line = (ushort*)bd.Scan0;
                int delta = bd.Stride >> 1;
                for (int y = 0; y < bd.Height; ++y, line += delta)
                {
                    ushort* cur = line;
                    for (int i = 0; i < bitmapWidth; ++i)
                    {
                        *cur++ = edit.Palette[i];
                    }
                }

                bmp.UnlockBits(bd);
            }

            PalettePictureBox.Image?.Dispose();
            PalettePictureBox.Image = bmp;
        }

        private void AfterSelectTreeView(object sender, TreeViewEventArgs e)
        {
            if (AnimationListTreeView.SelectedNode == null)
            {
                return;
            }

            if (AnimationListTreeView.SelectedNode.Parent == null)
            {
                if (AnimationListTreeView.SelectedNode.Tag != null)
                {
                    _currentBody = (int)AnimationListTreeView.SelectedNode.Tag;
                }

                _currentAction = 0;
            }
            else
            {
                if (AnimationListTreeView.SelectedNode.Parent.Tag != null)
                {
                    _currentBody = (int)AnimationListTreeView.SelectedNode.Parent.Tag;
                }

                _currentAction = (int)AnimationListTreeView.SelectedNode.Tag;
            }

            FramesListView.BeginUpdate();
            try
            {
                FramesListView.Clear();
                AnimIdx edit = AnimationEdit.GetAnimation(_fileType, _currentBody, _currentAction, _currentDir);
                if (edit != null)
                {
                    int width = 80;
                    int height = 110;
                    Bitmap[] currentBits = edit.GetFrames();
                    if (currentBits != null)
                    {
                        for (int i = 0; i < currentBits.Length; ++i)
                        {
                            if (currentBits[i] == null)
                            {
                                continue;
                            }

                            ListViewItem item = new ListViewItem(i.ToString(), 0)
                            {
                                Tag = i
                            };
                            FramesListView.Items.Add(item);

                            if (currentBits[i].Width > width)
                            {
                                width = currentBits[i].Width;
                            }

                            if (currentBits[i].Height > height)
                            {
                                height = currentBits[i].Height;
                            }
                        }
                        FramesListView.TileSize = new Size(width + 5, height + 5);

                        FramesTrackBar.Maximum = currentBits.Length - 1;
                        FramesTrackBar.Value = 0;
                        FramesTrackBar.Invalidate();

                        CenterXNumericUpDown.Value = edit.Frames[FramesTrackBar.Value].Center.X;
                        CenterYNumericUpDown.Value = edit.Frames[FramesTrackBar.Value].Center.Y;
                    }
                    //Soulblighter Modification
                    else
                    {
                        FramesTrackBar.Maximum = 0;
                        FramesTrackBar.Value = 0;
                        FramesTrackBar.Invalidate();
                    }
                    //End of Soulblighter Modification
                }
            }
            finally
            {
                FramesListView.EndUpdate();
            }

            AnimationPictureBox.Invalidate();
            SetPaletteBox();
        }

        private void DrawFrameItem(object sender, DrawListViewItemEventArgs e)
        {
            AnimIdx edit = AnimationEdit.GetAnimation(_fileType, _currentBody, _currentAction, _currentDir);
            Bitmap[] currentBits = edit.GetFrames();
            Bitmap bmp = currentBits[(int)e.Item.Tag];
            var penColor = FramesListView.SelectedItems.Contains(e.Item) ? Color.Red : Color.Gray;
            e.Graphics.DrawRectangle(new Pen(penColor), e.Bounds.X, e.Bounds.Y, e.Bounds.Width, e.Bounds.Height);
            e.Graphics.DrawImage(bmp, e.Bounds.X, e.Bounds.Y, bmp.Width,  bmp.Height);
            e.DrawText(TextFormatFlags.Bottom | TextFormatFlags.HorizontalCenter);
        }

        private void OnAnimChanged(object sender, EventArgs e)
        {
            if (SelectFileToolStripComboBox.SelectedIndex == _fileType)
            {
                return;
            }

            _fileType = SelectFileToolStripComboBox.SelectedIndex;
            OnLoad(this, EventArgs.Empty);
        }

        private void OnDirectionChanged(object sender, EventArgs e)
        {
            _currentDir = DirectionTrackBar.Value;
            AfterSelectTreeView(null, null);
        }

        private void AnimationPictureBox_OnSizeChanged(object sender, EventArgs e)
        {
            _framePoint = new Point(AnimationPictureBox.Width / 2, AnimationPictureBox.Height / 2);
            AnimationPictureBox.Invalidate();
        }
        //Soulblighter Modification

        private void AnimationPictureBox_OnPaintFrame(object sender, PaintEventArgs e)
        {
            AnimIdx edit = AnimationEdit.GetAnimation(_fileType, _currentBody, _currentAction, _currentDir);
            if (edit == null)
            {
                return;
            }

            Bitmap[] currentBits = edit.GetFrames();

            using (var off = new Bitmap(AnimationPictureBox.Width, AnimationPictureBox.Height))
            using (var offG = Graphics.FromImage(off))
            {
                // draw background and axes into offscreen first
                offG.Clear(Color.LightGray);
                offG.DrawLine(Pens.Black, new Point(_framePoint.X, 0), new Point(_framePoint.X, AnimationPictureBox.Height));
                offG.DrawLine(Pens.Black, new Point(0, _framePoint.Y), new Point(AnimationPictureBox.Width, _framePoint.Y));

                // If opposite-human should be behind main sprite for directions 2,3,4, draw it first
                bool overlayDrawnBefore = false;
                if (DrawOppositeHumanCheckBox.Checked && (_currentDir == 2 || _currentDir == 3 || _currentDir == 4))
                {
                    overlayDrawnBefore = DrawOppositeHumanOverlay(offG, edit);
                }

                if (currentBits?.Length > 0 && currentBits[FramesTrackBar.Value] != null)
            {
                int varW;
                int varH;
                if (!_drawEmpty)
                {
                    varW = 0;
                    varH = 0;
                }
                else
                {
                    varW = currentBits[FramesTrackBar.Value].Width;
                    varH = currentBits[FramesTrackBar.Value].Height;
                }

                int varFw;
                int varFh;
                if (!_drawFull)
                {
                    varFw = 0;
                    varFh = 0;
                }
                else
                {
                    varFw = currentBits[FramesTrackBar.Value].Width;
                    varFh = currentBits[FramesTrackBar.Value].Height;
                }

                int x = _framePoint.X - edit.Frames[FramesTrackBar.Value].Center.X;
                int y = _framePoint.Y - edit.Frames[FramesTrackBar.Value].Center.Y - currentBits[FramesTrackBar.Value].Height;

                using (var whiteTransparent = new SolidBrush(Color.FromArgb(160, 255, 255, 255)))
                {
                    offG.FillRectangle(whiteTransparent, new Rectangle(x, y, varFw, varFh));
                }

                offG.DrawRectangle(Pens.Red, new Rectangle(x, y, varW, varH));
                offG.DrawImage(currentBits[FramesTrackBar.Value], x, y);

                // If overlay was drawn before the main sprite for behind-directions, redraw the main sprite to ensure it appears on top
                if (overlayDrawnBefore)
                {
                    offG.DrawImage(currentBits[FramesTrackBar.Value], x, y);
                }

                // --- Overlay logic for human animation (body 400) ---
                int animLength = Ultima.Animations.GetAnimLength(_currentBody, _fileType);
                if (animLength == 13) // Animal
                {
                    int overlayAction = -1;
                    if (_currentAction == 0) overlayAction = 23; // Walk
                    else if (_currentAction == 1) overlayAction = 24; // Run
                    else if (_currentAction == 2) overlayAction = 25; // Idle

                    if (overlayAction != -1 && DrawMountedCheckBox.Checked)
                    {
                        int overlayBody = 400;
                        int overlayFileType = 1; // Always use anim.mul
                        int overlayDir = _currentDir;
                        var overlayEdit = Ultima.AnimationEdit.GetAnimation(overlayFileType, overlayBody, overlayAction, overlayDir);
                        if (overlayEdit != null)
                        {
                            var overlayFrames = overlayEdit.GetFrames();
                            if (overlayFrames != null && overlayFrames.Length > 0)
                            {
                                int overlayIndex = Math.Min(FramesTrackBar.Value, overlayFrames.Length - 1);
                                if (overlayFrames[overlayIndex] != null && overlayEdit.Frames.Count > overlayIndex)
                                {
                                    var overlayFrame = overlayEdit.Frames[overlayIndex];
                                    int overlayW = overlayFrames[overlayIndex].Width;
                                    int overlayH = overlayFrames[overlayIndex].Height;
                                    int ox = _framePoint.X - overlayFrame.Center.X;
                                    int oy = _framePoint.Y - overlayFrame.Center.Y - overlayH;
                                    offG.DrawImage(overlayFrames[overlayIndex], ox, oy);
                                }
                                else
                                {
                                    // Draw a magenta rectangle if the overlay frame is missing
                                    offG.DrawRectangle(Pens.Magenta, 10, 10, 40, 40);
                                }
                            }
                            else
                            {
                                // Draw a yellow rectangle if overlayFrames is null or empty
                                offG.DrawRectangle(Pens.Yellow, 10, 10, 40, 40);
                                MessageBox.Show($"No overlay frames for body 400, action {overlayAction}, dir {overlayDir}");
                            }
                        }
                        else
                        {
                            // Draw a red rectangle if overlayEdit is null
                            offG.DrawRectangle(Pens.Red, 10, 10, 40, 40);
                        }
                    }
                }
                // --- End overlay logic ---
                if (DrawOppositeHumanCheckBox.Checked && !overlayDrawnBefore)
                {
                    DrawOppositeHumanOverlay(offG, edit);
                }
            }

            // Draw Reference Point Arrow
            Point[] arrayPoints = {
        new Point(418 - (int)RefXNumericUpDown.Value, 335 - (int)RefYNumericUpDown.Value),
        new Point(418 - (int)RefXNumericUpDown.Value, 352 - (int)RefYNumericUpDown.Value),
        new Point(422 - (int)RefXNumericUpDown.Value, 348 - (int)RefYNumericUpDown.Value),
        new Point(425 - (int)RefXNumericUpDown.Value, 353 - (int)RefYNumericUpDown.Value),
        new Point(427 - (int)RefXNumericUpDown.Value, 352 - (int)RefYNumericUpDown.Value),
        new Point(425 - (int)RefXNumericUpDown.Value, 347 - (int)RefYNumericUpDown.Value),
        new Point(430 - (int)RefXNumericUpDown.Value, 347 - (int)RefYNumericUpDown.Value)
    };

            offG.FillPolygon(_whiteUnDraw, arrayPoints);
            offG.DrawPolygon(_blackUndraw, arrayPoints);

            e.Graphics.DrawImage(off, 0, 0);
        }
        }

        private bool DrawOppositeHumanOverlay(Graphics g, AnimIdx edit)
        {
            if (!DrawOppositeHumanCheckBox.Checked)
            {
                return false;
            }

            // idle action for humans is usually 4 (Idle_01)
            int humanIdleAction = 4;
            int humanFile = 1; // anim.mul

            int dx = 22;
            int dy = 44;

            // compute facing and offset
            int humanFacingDir = (_currentDir + 4) % 8;
            Point tileOffset = GetTilePixelOffsetForDir(_currentDir, dx, dy);

            int[] humanBodies = { 744, 401, 605, 606, 666, 667 };

            // Protect against missing base animation frames (can happen for some directions/actions)
            if (edit == null || edit.Frames == null || edit.Frames.Count == 0)
            {
                return false;
            }

            foreach (int humanBody in humanBodies)
            {
                var overlayEdit = Ultima.AnimationEdit.GetAnimation(humanFile, humanBody, humanIdleAction, humanFacingDir);
                if (overlayEdit == null)
                {
                    continue;
                }

                var overlayFrames = overlayEdit.GetFrames();
                if (overlayFrames == null || overlayFrames.Length == 0)
                {
                    continue;
                }

                int baseCount = edit.Frames.Count;
                int trackIndex = FramesTrackBar.Value;
                int overlayIndex;
                if (baseCount <= 1)
                {
                    overlayIndex = 0;
                }
                else
                {
                    overlayIndex = (int)Math.Round(trackIndex / (double)(baseCount - 1) * (overlayFrames.Length - 1));
                }

                overlayIndex = Math.Max(0, Math.Min(overlayIndex, overlayFrames.Length - 1));

                if (overlayFrames[overlayIndex] == null || overlayEdit.Frames.Count <= overlayIndex)
                {
                    continue;
                }

                var overlayFrame = overlayEdit.Frames[overlayIndex];
                int overlayW = overlayFrames[overlayIndex].Width;
                int overlayH = overlayFrames[overlayIndex].Height;

                bool shouldFlipFacingOnly = (_currentDir == 1 || _currentDir == 2 || _currentDir == 3);
                if (shouldFlipFacingOnly)
                {
                    using (Bitmap flipped = (Bitmap)overlayFrames[overlayIndex].Clone())
                    {
                        flipped.RotateFlip(RotateFlipType.RotateNoneFlipX);
                        int flippedCenterX = overlayW - overlayFrame.Center.X;
                        int fox = _framePoint.X - flippedCenterX + tileOffset.X;
                        int foy = _framePoint.Y - overlayFrame.Center.Y - overlayH + tileOffset.Y;
                        g.DrawImage(flipped, fox, foy);
                    }
                }
                else
                {
                    int ox = _framePoint.X - overlayFrame.Center.X + tileOffset.X;
                    int oy = _framePoint.Y - overlayFrame.Center.Y - overlayH + tileOffset.Y;
                    g.DrawImage(overlayFrames[overlayIndex], ox, oy);
                }

                return true;
            }

            // nothing found
            g.DrawRectangle(Pens.Magenta, 10, 10, 40, 40);
            return true;
        }
        /*
        private void AnimationPictureBox_OnPaintFrame(object sender, PaintEventArgs e)
        {
            AnimIdx edit = AnimationEdit.GetAnimation(_fileType, _currentBody, _currentAction, _currentDir);
            if (edit == null)
            {
                return;
            }

            Bitmap[] currentBits = edit.GetFrames();

            e.Graphics.Clear(Color.LightGray);
            e.Graphics.DrawLine(Pens.Black, new Point(_framePoint.X, 0), new Point(_framePoint.X, AnimationPictureBox.Height));
            e.Graphics.DrawLine(Pens.Black, new Point(0, _framePoint.Y), new Point(AnimationPictureBox.Width, _framePoint.Y));

            if (currentBits?.Length > 0 && currentBits[FramesTrackBar.Value] != null)
            {
                int varW;
                int varH;
                if (!_drawEmpty)
                {
                    varW = 0;
                    varH = 0;
                }
                else
                {
                    varW = currentBits[FramesTrackBar.Value].Width;
                    varH = currentBits[FramesTrackBar.Value].Height;
                }

                int varFw;
                int varFh;
                if (!_drawFull)
                {
                    varFw = 0;
                    varFh = 0;
                }
                else
                {
                    varFw = currentBits[FramesTrackBar.Value].Width;
                    varFh = currentBits[FramesTrackBar.Value].Height;
                }

                int x = _framePoint.X - edit.Frames[FramesTrackBar.Value].Center.X;
                int y = _framePoint.Y - edit.Frames[FramesTrackBar.Value].Center.Y - currentBits[FramesTrackBar.Value].Height;

                using (var whiteTransparent = new SolidBrush(Color.FromArgb(160, 255, 255, 255)))
                {
                    e.Graphics.FillRectangle(whiteTransparent, new Rectangle(x, y, varFw, varFh));
                }

                e.Graphics.DrawRectangle(Pens.Red, new Rectangle(x, y, varW, varH));
                e.Graphics.DrawImage(currentBits[FramesTrackBar.Value], x, y);

                //e.Graphics.DrawLine(Pens.Red, new Point(0, 335-(int)numericUpDown1.Value), new Point(animationPictureBox.Width, 335-(int)numericUpDown1.Value));
            }

            // Draw Reference Point Arrow
            Point[] arrayPoints = {
                new Point(418 - (int)RefXNumericUpDown.Value, 335 - (int)RefYNumericUpDown.Value),
                new Point(418 - (int)RefXNumericUpDown.Value, 352 - (int)RefYNumericUpDown.Value),
                new Point(422 - (int)RefXNumericUpDown.Value, 348 - (int)RefYNumericUpDown.Value),
                new Point(425 - (int)RefXNumericUpDown.Value, 353 - (int)RefYNumericUpDown.Value),
                new Point(427 - (int)RefXNumericUpDown.Value, 352 - (int)RefYNumericUpDown.Value),
                new Point(425 - (int)RefXNumericUpDown.Value, 347 - (int)RefYNumericUpDown.Value),
                new Point(430 - (int)RefXNumericUpDown.Value, 347 - (int)RefYNumericUpDown.Value)
            };

            e.Graphics.FillPolygon(_whiteUnDraw, arrayPoints);
            e.Graphics.DrawPolygon(_blackUndraw, arrayPoints);
        }
      */


        //End of Soulblighter Modification

        //Soulblighter Modification
        private void OnFrameCountBarChanged(object sender, EventArgs e)
        {
            if (_fileType == 0)
            {
                return;
            }

            AnimIdx edit = AnimationEdit.GetAnimation(_fileType, _currentBody, _currentAction, _currentDir);
            if (edit != null && edit.Frames.Count >= FramesTrackBar.Value)
            {
                CenterXNumericUpDown.Value = edit.Frames[FramesTrackBar.Value].Center.X;
                CenterYNumericUpDown.Value = edit.Frames[FramesTrackBar.Value].Center.Y;
            }

            AnimationPictureBox.Invalidate();
        }
        //End of Soulblighter Modification

        private void OnCenterXValueChanged(object sender, EventArgs e)
        {
            try
            {
                if (_fileType == 0)
                {
                    return;
                }

                AnimIdx edit = AnimationEdit.GetAnimation(_fileType, _currentBody, _currentAction, _currentDir);
                if (edit == null || edit.Frames.Count < FramesTrackBar.Value)
                {
                    return;
                }

                FrameEdit frame = edit.Frames[FramesTrackBar.Value];
                if (CenterXNumericUpDown.Value == frame.Center.X)
                {
                    return;
                }

                frame.ChangeCenter((int)CenterXNumericUpDown.Value, frame.Center.Y);
                Options.ChangedUltimaClass["Animations"] = true;
                AnimationPictureBox.Invalidate();
            }
            catch (NullReferenceException)
            {
                // TODO: add logging or fix?
                // ignored
            }
        }

        private void OnCenterYValueChanged(object sender, EventArgs e)
        {
            try
            {
                if (_fileType == 0)
                {
                    return;
                }

                AnimIdx edit = AnimationEdit.GetAnimation(_fileType, _currentBody, _currentAction, _currentDir);
                if (edit == null || edit.Frames.Count < FramesTrackBar.Value)
                {
                    return;
                }

                FrameEdit frame = edit.Frames[FramesTrackBar.Value];
                if (CenterYNumericUpDown.Value == frame.Center.Y)
                {
                    return;
                }

                frame.ChangeCenter(frame.Center.X, (int)CenterYNumericUpDown.Value);
                Options.ChangedUltimaClass["Animations"] = true;
                AnimationPictureBox.Invalidate();
            }
            catch (NullReferenceException)
            {
                // TODO: add logging or fix?
                // ignored
            }
        }

        private void OnClickExtractImages(object sender, EventArgs e)
        {
            if (_fileType == 0)
            {
                return;
            }

            ToolStripMenuItem menu = (ToolStripMenuItem)sender;

            ImageFormat format;

            switch ((string)menu.Tag)
            {
                case ".tiff":
                    format = ImageFormat.Tiff;
                    break;
                case ".png":
                    format = ImageFormat.Png;
                    break;
                case ".jpg":
                    format = ImageFormat.Jpeg;
                    break;
                default:
                    format = ImageFormat.Bmp;
                    break;
            }

            string path = Options.OutputPath;

            int body;
            int action;

            if (AnimationListTreeView.SelectedNode.Parent == null)
            {
                body = (int)AnimationListTreeView.SelectedNode.Tag;
                action = -1;
            }
            else
            {
                body = (int)AnimationListTreeView.SelectedNode.Parent.Tag;
                action = (int)AnimationListTreeView.SelectedNode.Tag;
            }

            if (action == -1)
            {
                for (int a = 0; a < Animations.GetAnimLength(body, _fileType); ++a)
                {
                    for (int i = 0; i < 5; ++i)
                    {
                        AnimIdx edit = AnimationEdit.GetAnimation(_fileType, body, a, i);
                        Bitmap[] bits = edit?.GetFrames();
                        if (bits == null)
                        {
                            continue;
                        }

                        for (int j = 0; j < bits.Length; ++j)
                        {
                            if (bits[j] is null)
                            {
                                continue;
                            }

                            string filename = string.Format("anim{5}_{0}_{1}_{2}_{3}{4}", body, a, i, j, menu.Tag, _fileType);
                            string file = Path.Combine(path, filename);

                            using (Bitmap bit = new Bitmap(bits[j]))
                            {
                                bit.Save(file, format);
                            }
                        }
                    }
                }
            }
            else
            {
                for (int i = 0; i < 5; ++i)
                {
                    AnimIdx edit = AnimationEdit.GetAnimation(_fileType, body, action, i);
                    Bitmap[] bits = edit?.GetFrames();
                    if (bits == null)
                    {
                        continue;
                    }

                    for (int j = 0; j < bits.Length; ++j)
                    {
                        if (bits[j] is null)
                        {
                            continue;
                        }

                        string filename = string.Format("anim{5}_{0}_{1}_{2}_{3}{4}", body, action, i, j, menu.Tag, _fileType);
                        string file = Path.Combine(path, filename);

                        using (Bitmap bit = new Bitmap(bits[j]))
                        {
                            bit.Save(file, format);
                        }
                    }
                }
            }

            MessageBox.Show($"Frames saved to {path}", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button1);
        }

        private void OnClickRemoveAction(object sender, EventArgs e)
        {
            if (_fileType == 0)
            {
                return;
            }

            if (AnimationListTreeView.SelectedNode.Parent == null)
            {
                DialogResult result = MessageBox.Show($"Are you sure to remove animation {_currentBody}", "Remove",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
                if (result != DialogResult.Yes)
                {
                    return;
                }

                AnimationListTreeView.SelectedNode.ForeColor = Color.Red;
                for (int i = 0; i < AnimationListTreeView.SelectedNode.Nodes.Count; ++i)
                {
                    AnimationListTreeView.SelectedNode.Nodes[i].ForeColor = Color.Red;
                    for (int d = 0; d < 5; ++d)
                    {
                        AnimIdx edit = AnimationEdit.GetAnimation(_fileType, _currentBody, i, d);
                        edit?.ClearFrames();
                    }
                }

                if (_showOnlyValid)
                {
                    AnimationListTreeView.SelectedNode.Remove();
                }

                Options.ChangedUltimaClass["Animations"] = true;
                AfterSelectTreeView(this, null);
            }
            else
            {
                DialogResult result = MessageBox.Show($"Are you sure to remove action {_currentAction}", "Remove",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
                if (result != DialogResult.Yes)
                {
                    return;
                }

                for (int i = 0; i < 5; ++i)
                {
                    AnimIdx edit = AnimationEdit.GetAnimation(_fileType, _currentBody, _currentAction, i);
                    edit?.ClearFrames();
                }

                AnimationListTreeView.SelectedNode.Parent.Nodes[_currentAction].ForeColor = Color.Red;
                bool valid = false;
                foreach (TreeNode node in AnimationListTreeView.SelectedNode.Parent.Nodes)
                {
                    if (node.ForeColor == Color.Red)
                    {
                        continue;
                    }

                    valid = true;
                    break;
                }

                if (!valid)
                {
                    if (_showOnlyValid)
                    {
                        AnimationListTreeView.SelectedNode.Parent.Remove();
                    }
                    else
                    {
                        AnimationListTreeView.SelectedNode.Parent.ForeColor = Color.Red;
                    }
                }

                Options.ChangedUltimaClass["Animations"] = true;
                AfterSelectTreeView(this, null);
            }
        }

        private void OnClickSave(object sender, EventArgs e)
        {
            if (_fileType == 0)
            {
                return;
            }

            AnimationEdit.Save(_fileType, Options.OutputPath);
            Options.ChangedUltimaClass["Animations"] = false;

            MessageBox.Show($"AnimationFile saved to {Options.OutputPath}", "Saved", MessageBoxButtons.OK,
                MessageBoxIcon.Information, MessageBoxDefaultButton.Button1);
        }

        //My Soulblighter Modification
        private void OnClickRemoveFrame(object sender, EventArgs e)
        {
            if (FramesListView.SelectedItems.Count <= 0)
            {
                return;
            }

            int corrector = 0;
            int[] frameIndex = new int[FramesListView.SelectedItems.Count];
            for (int i = 0; i < FramesListView.SelectedItems.Count; i++)
            {
                frameIndex[i] = FramesListView.SelectedIndices[i] - corrector;
                corrector++;
            }

            foreach (var index in frameIndex)
            {
                AnimIdx edit = AnimationEdit.GetAnimation(_fileType, _currentBody, _currentAction, _currentDir);
                if (edit == null)
                {
                    continue;
                }

                edit.RemoveFrame(index);
                FramesListView.Items.RemoveAt(FramesListView.Items.Count - 1);
                FramesTrackBar.Maximum = edit.Frames.Count != 0 ? edit.Frames.Count - 1 : 0;
                FramesListView.Invalidate();
                Options.ChangedUltimaClass["Animations"] = true;
            }
        }
        //End of Soulblighter Modification

        private void OnClickReplace(object sender, EventArgs e)
        {
            if (FramesListView.SelectedItems.Count <= 0)
            {
                return;
            }

            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                int frameIndex = (int)FramesListView.SelectedItems[0].Tag;

                dialog.Multiselect = false;
                dialog.Title = $"Choose image file to replace at {frameIndex}";
                dialog.CheckFileExists = true;
                dialog.Filter = "Image files (*.tif;*.tiff;*.bmp;*.png)|*.tif;*.tiff;*.bmp;*.png";

                if (dialog.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                using (var bmpTemp = new Bitmap(dialog.FileName))
                {
                    Bitmap bitmap = new Bitmap(bmpTemp);

                    if (dialog.FileName.Contains(".bmp"))
                    {
                        bitmap = ConvertBmpAnim(bitmap, (int)numericUpDownRed.Value, (int)numericUpDownGreen.Value, (int)numericUpDownBlue.Value);
                    }

                    AnimIdx edit = AnimationEdit.GetAnimation(_fileType, _currentBody, _currentAction, _currentDir);
                    if (edit == null)
                    {
                        return;
                    }

                    edit.ReplaceFrame(bitmap, frameIndex);

                    FramesListView.Invalidate();

                    Options.ChangedUltimaClass["Animations"] = true;
                }
            }
        }

        private void OnClickAdd(object sender, EventArgs e)
        {
            if (_fileType != 0)
            {
                using (OpenFileDialog dialog = new OpenFileDialog())
                {
                    dialog.Multiselect = true;
                    dialog.Title = "Choose image file to add";
                    dialog.CheckFileExists = true;
                    dialog.Filter = "Gif files (*.gif;)|*.gif; |Bitmap files (*.bmp;)|*.bmp; |Tiff files (*.tif;*.tiff)|*.tif;*.tiff; |Png files (*.png;)|*.png; |Jpeg files (*.jpeg;*.jpg;)|*.jpeg;*.jpg;";

                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        FramesListView.BeginUpdate();
                        try
                        {
                            //My Soulblighter Modifications
                            foreach (var fileName in dialog.FileNames)
                            {
                                using (var bmpTemp = new Bitmap(fileName))
                                {
                                    Bitmap bitmap = new Bitmap(bmpTemp);

                                    if (dialog.FileName.Contains(".bmp") || dialog.FileName.Contains(".tiff") ||
                                        dialog.FileName.Contains(".png") || dialog.FileName.Contains(".jpeg") ||
                                        dialog.FileName.Contains(".jpg"))
                                    {
                                        bitmap = ConvertBmpAnim(bitmap, (int)numericUpDownRed.Value, (int)numericUpDownGreen.Value, (int)numericUpDownBlue.Value);

                                        //edit.GetImagePalette(bitmap);
                                    }

                                    AnimIdx edit = AnimationEdit.GetAnimation(_fileType, _currentBody, _currentAction, _currentDir);
                                    if (edit == null)
                                    {
                                        continue;
                                    }

                                    //Gif Especial Properties
                                    if (dialog.FileName.Contains(".gif"))
                                    {
                                        FrameDimension dimension = new FrameDimension(bitmap.FrameDimensionsList[0]);

                                        // Number of frames
                                        int frameCount = bitmap.GetFrameCount(dimension);

                                        Bitmap[] bitBmp = new Bitmap[frameCount];

                                        bitmap.SelectActiveFrame(dimension, 0);
                                        UpdateGifPalette(bitmap, edit);

                                        ProgressBar.Maximum = frameCount;

                                        AddImageAtCertainIndex(frameCount, bitBmp, bitmap, dimension, edit);

                                        ProgressBar.Value = 0;
                                        ProgressBar.Invalidate();

                                        SetPaletteBox();

                                        CenterXNumericUpDown.Value = edit.Frames[FramesTrackBar.Value].Center.X;
                                        CenterYNumericUpDown.Value = edit.Frames[FramesTrackBar.Value].Center.Y;

                                        Options.ChangedUltimaClass["Animations"] = true;
                                    }
                                    //End of Soulblighter Modifications
                                    else
                                    {
                                        edit.AddFrame(bitmap);

                                        TreeNode node = GetNode(_currentBody);
                                        if (node != null)
                                        {
                                            node.ForeColor = Color.Black;
                                            node.Nodes[_currentAction].ForeColor = Color.Black;
                                        }

                                        int i = edit.Frames.Count - 1;
                                        var item = new ListViewItem(i.ToString(), 0)
                                        {
                                            Tag = i
                                        };

                                        FramesListView.Items.Add(item);

                                        int width = FramesListView.TileSize.Width - 5;
                                        if (bitmap.Width > FramesListView.TileSize.Width)
                                        {
                                            width = bitmap.Width;
                                        }

                                        int height = FramesListView.TileSize.Height - 5;
                                        if (bitmap.Height > FramesListView.TileSize.Height)
                                        {
                                            height = bitmap.Height;
                                        }

                                        FramesListView.TileSize = new Size(width + 5, height + 5);
                                        FramesTrackBar.Maximum = i;

                                        CenterXNumericUpDown.Value = edit.Frames[FramesTrackBar.Value].Center.X;
                                        CenterYNumericUpDown.Value = edit.Frames[FramesTrackBar.Value].Center.Y;

                                        Options.ChangedUltimaClass["Animations"] = true;
                                    }
                                }
                            }
                        }
                        finally
                        {
                            FramesListView.EndUpdate();
                        }

                        FramesListView.Invalidate();
                    }
                }
            }

            // Refresh List
            _currentDir = DirectionTrackBar.Value;
            AfterSelectTreeView(null, null);
        }

        private void AddImageAtCertainIndex(int frameCount, Bitmap[] bitBmp, Bitmap bmp, FrameDimension dimension, AnimIdx edit)
        {
            // Return an Image at a certain index
            for (int index = 0; index < frameCount; index++)
            {
                bitBmp[index] = new Bitmap(bmp.Width, bmp.Height, PixelFormat.Format16bppArgb1555);
                bmp.SelectActiveFrame(dimension, index);
                bitBmp[index] = ConvertBmpAnim(bitBmp[index], (int) numericUpDownRed.Value,
                    (int) numericUpDownGreen.Value, (int) numericUpDownBlue.Value);
                edit.AddFrame(bitBmp[index], bitBmp[index].Width / 2);
                TreeNode node = GetNode(_currentBody);
                if (node != null)
                {
                    node.ForeColor = Color.Black;
                    node.Nodes[_currentAction].ForeColor = Color.Black;
                }

                int i = edit.Frames.Count - 1;
                var item = new ListViewItem(i.ToString(), 0)
                {
                    Tag = i
                };
                FramesListView.Items.Add(item);
                int width = FramesListView.TileSize.Width - 5;
                if (bmp.Width > FramesListView.TileSize.Width)
                {
                    width = bmp.Width;
                }

                int height = FramesListView.TileSize.Height - 5;
                if (bmp.Height > FramesListView.TileSize.Height)
                {
                    height = bmp.Height;
                }

                FramesListView.TileSize = new Size(width + 5, height + 5);
                FramesTrackBar.Maximum = i;
                Options.ChangedUltimaClass["Animations"] = true;
                if (ProgressBar.Value < ProgressBar.Maximum)
                {
                    ProgressBar.Value++;
                    ProgressBar.Invalidate();
                }
            }
        }

        private void OnClickExtractPalette(object sender, EventArgs e)
        {
            if (_fileType == 0)
            {
                return;
            }

            ToolStripMenuItem menu = (ToolStripMenuItem)sender;
            AnimIdx edit = AnimationEdit.GetAnimation(_fileType, _currentBody, _currentAction, _currentDir);
            if (edit == null)
            {
                return;
            }

            string name = $"palette_anim{_fileType}_{_currentBody}_{_currentAction}_{_currentDir}";
            if ((string)menu.Tag == "txt")
            {
                string path = Path.Combine(Options.OutputPath, name + ".txt");
                edit.ExportPalette(path, 0);
            }
            else
            {
                string path = Path.Combine(Options.OutputPath, name + "." + (string)menu.Tag);
                edit.ExportPalette(path, (string)menu.Tag == "bmp" ? 1 : 2);
            }

            MessageBox.Show($"Palette saved to {Options.OutputPath}", "Saved", MessageBoxButtons.OK,
                MessageBoxIcon.Information, MessageBoxDefaultButton.Button1);
        }

        private void OnClickImportPalette(object sender, EventArgs e)
        {
            if (_fileType == 0)
            {
                return;
            }

            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Multiselect = false;
                dialog.Title = "Choose palette file";
                dialog.CheckFileExists = true;
                dialog.Filter = "txt files (*.txt)|*.txt";
                if (dialog.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                AnimIdx edit = AnimationEdit.GetAnimation(_fileType, _currentBody, _currentAction, _currentDir);
                if (edit == null)
                {
                    return;
                }

                using (StreamReader sr = new StreamReader(dialog.FileName))
                {
                    ushort[] palette = new ushort[Animations.PaletteCapacity];

                    int i = 0;
                    while (sr.ReadLine() is { } line)
                    {
                        if ((line = line.Trim()).Length == 0 || line.StartsWith('#'))
                        {
                            continue;
                        }

                        i++;

                        if (i >= Animations.PaletteCapacity)
                        {
                            break;
                        }

                        palette[i] = ushort.Parse(line);

                        // My Soulblighter Modification
                        // Convert color 0,0,0 to 0,0,8
                        // TODO: find out why do we need this replacement
                        if (palette[i] == 32768)
                        {
                            palette[i] = 32769;
                        }
                        // End of Soulblighter Modification
                    }

                    edit.ReplacePalette(palette);
                }

                SetPaletteBox();

                FramesListView.Invalidate();

                Options.ChangedUltimaClass["Animations"] = true;
            }
        }

        private void OnClickImportFromVD(object sender, EventArgs e)
        {
            if (_fileType == 0)
            {
                return;
            }

            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Multiselect = false;
                dialog.Title = "Choose palette file";
                dialog.CheckFileExists = true;
                dialog.Filter = "vd files (*.vd)|*.vd";
                if (dialog.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                int animLength = Animations.GetAnimLength(_currentBody, _fileType);
                int currentType;
                if (animLength == 22)
                {
                    currentType = 0;
                }
                else
                {
                    currentType = animLength == 13 ? 1 : 2;
                }

                using (FileStream fs = new FileStream(dialog.FileName, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    using (BinaryReader bin = new BinaryReader(fs))
                    {
                        int fileType = bin.ReadInt16();
                        int animType = bin.ReadInt16();
                        if (fileType != 6)
                        {
                            MessageBox.Show("Not an Anim File.", "Import", MessageBoxButtons.OK,
                                MessageBoxIcon.Information, MessageBoxDefaultButton.Button1);
                            return;
                        }

                        if (animType != currentType)
                        {
                            MessageBox.Show("Wrong Anim Id ( Type )", "Import", MessageBoxButtons.OK,
                                MessageBoxIcon.Information, MessageBoxDefaultButton.Button1);
                            return;
                        }
                        AnimationEdit.LoadFromVD(_fileType, _currentBody, bin);
                    }
                }

                bool valid = false;
                TreeNode node = GetNode(_currentBody);
                if (node != null)
                {
                    for (int j = 0; j < animLength; ++j)
                    {
                        if (AnimationEdit.IsActionDefined(_fileType, _currentBody, j))
                        {
                            node.Nodes[j].ForeColor = Color.Black;
                            valid = true;
                        }
                        else
                        {
                            node.Nodes[j].ForeColor = Color.Red;
                        }
                    }
                    node.ForeColor = valid ? Color.Black : Color.Red;
                }

                Options.ChangedUltimaClass["Animations"] = true;
                AfterSelectTreeView(this, null);

                MessageBox.Show("Finished", "Import", MessageBoxButtons.OK, MessageBoxIcon.Information,
                    MessageBoxDefaultButton.Button1);
            }
        }

        private void OnClickExportToVD(object sender, EventArgs e)
        {
            if (_fileType == 0)
            {
                return;
            }

            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Title = "Export to .vd";
                dialog.InitialDirectory = Options.OutputPath;
                dialog.Filter = "vd files (*.vd)|*.vd";
                dialog.FileName = $"anim{_fileType}_0x{_currentBody:X}.vd";

                // Build a simple option dialog embedded in the SaveFileDialog's owner sequence
                if (dialog.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                // Create small configuration dialog for VD type and frame selection
                using (Form cfg = new Form())
                {
                    cfg.StartPosition = FormStartPosition.CenterParent;
                    cfg.FormBorderStyle = FormBorderStyle.FixedDialog;
                    cfg.MinimizeBox = false;
                    cfg.MaximizeBox = false;
                    cfg.ShowInTaskbar = false;
                    cfg.Text = "Export VD Remapper";
                    int animLength = Animations.GetAnimLength(_currentBody, _fileType);
                    // Determine default type from animation length (same logic used elsewhere)
                    int defaultType = animLength == 22 ? 0 : animLength == 13 ? 1 : 2; // 0=High,1=Low,2=People
                    // enlarge dialog when source animation is High (more rows)
                    if (defaultType == 0)
                        cfg.ClientSize = new System.Drawing.Size(420, 760);
                    else
                        cfg.ClientSize = new System.Drawing.Size(390, 450);

                    var lblType = new Label { Left = 8, Top = 12, Width = 120, Text = "Export VD Type:" };
                    // Use radio buttons for H / L / P selection
                    var rbHigh = new RadioButton { Left = 130, Top = 10, Width = 80, Text = "High (H)" };
                    var rbLow = new RadioButton { Left = 210, Top = 10, Width = 80, Text = "Low (L)" };
                    var rbPeople = new RadioButton { Left = 290, Top = 10, Width = 80, Text = "People (P)" };

                    rbHigh.Checked = defaultType == 0;
                    rbLow.Checked = defaultType == 1;
                    rbPeople.Checked = defaultType == 2;

                    // high dropdown labels (used to label source frames when source animation is High)
                    string[] highdropdownLabels = new string[]
                    {
                        "00 (Walk)", "01 (Idle1)", "02 (Die1)", "03 (Die2)", "04 (Attack1)", "05 (Attack2)",
                        "06 (Attack3)", "07 (AttackBow)", "08 (AttackCrossbow)", "09 (AttackThrow)", "10 (GetHit)", "11 (Pillage)",
                        "12 (Stomp)", "13 (Cast2)", "14 (Cast3)", "15 (BlockRight)", "16 (BlockLeft)", "17 (Idle2)",
                        "18 (Fidget)", "19 (Fly)", "20 (Takeoff)", "21 (GetHitInAir)"
                    };

                    // also used for target labels for High mapping panel
                    string[] highLabels = new string[]
                    {
                        "00 Walk", "01 Idle1", "02 Die1", "03 Die2", "04 Attack1", "05 Attack2",
                        "06 Attack3", "07 AttackBow", "08 AttackCrossbow", "09 AttackThrow", "10 GetHit", "11 Pillage",
                        "12 Stomp", "13 Cast2", "14 Cast3", "15 BlockRight", "16 BlockLeft", "17 Idle2",
                        "18 Fidget", "19 Fly", "20 Takeoff", "21 GetHitInAir"
                    };

                    // low dropdown labels (used to label source frames when source animation is Low)
                    string[] lowdropdownLabels = new string[]
                    {
                        "00 (Walk)", "01 (Run)", "02 (Idle)", "03 (Eat)", "04 (Alert)", "05 (Attack1)",
                        "06 (Attack2)", "07 (GetHit)", "08 (Die1)", "09 (Idle)", "10 (Fidget)", "11 (LieDown)", "12 (Die2)"
                    };

                    // also used for target labels for Low mapping panel
                    string[] lowLabels = new string[]
                    {
                        "00 Walk", "01 Run", "02 Idle", "03 Eat", "04 Alert", "05 Attack1",
                        "06 Attack2", "07 GetHit", "08 Die1", "09 Idle", "10 Fidget", "11 LieDown", "12 Die2"
                    };

                    // people dropdown labels (used to label source frames when source animation is People)
                    string[] peopledropdownLabels = new string[]
                    {
                        "00 Walk", "01 WalkStaff", "02 Run", "03 RunStaff", "04 Idle1", "05 Idle2", "06 Fidget_Yawn_Stretch",
                        "07 CombatIdle1Hand01", "08 CombatIdle1Hand02", "09 AttackSlash1Hand", "10 AttackPierce1Hand", "11 AttackBash1Hand",
                        "12 AttackBash2Hand", "13 AttackSlash2Hand", "14 AttackPierce2Hand", "15 CombatAdvance1Hand", "16 Spell1", "17 Spell2",
                        "18 AttackBow", "19 AttackCrossbow", "20 GetHit Fr Hi", "21 DieForward", "22 DieBackward", "23 MountWalk",
                        "24 MountRun", "25 MountIdle", "26 MountAttack1HandSlash", "27 MountAttackBow", "28 MountAttackCrossbow", "29 MountAttack2HandSlash",
                        "30 BlockShield", "31 PunchJab", "32 BowLesser", "33 SaluateArmed1Hand", "34 Eat"
                    };

                    // also used for target labels for People mapping panel
                    string[] peopleLabels = new string[]
                    {
                        "00 Walk", "01 WalkStaff", "02 Run", "03 RunStaff", "04 Idle1", "05 Idle2", "06 Fidget_Yawn_Stretch",
                        "07 CombatIdle1Hand01", "08 CombatIdle1Hand02", "09 AttackSlash1Hand", "10 AttackPierce1Hand", "11 AttackBash1Hand",
                        "12 AttackBash2Hand", "13 AttackSlash2Hand", "14 AttackPierce2Hand", "15 CombatAdvance1Hand", "16 Spell1", "17 Spell2",
                        "18 AttackBow", "19 AttackCrossbow", "20 GetHit Fr Hi", "21 DieForward", "22 DieBackward", "23 MountWalk",
                        "24 MountRun", "25 MountIdle", "26 MountAttack1HandSlash", "27 MountAttackBow", "28 MountAttackCrossbow", "29 MountAttack2HandSlash",
                        "30 BlockShield", "31 PunchJab", "32 BowLesser", "33 SaluateArmed1Hand", "34 Eat"
                    };

                    // Determine source animation type from animLength: 0=High,1=Low,2=People
                    int sourceType = animLength == 22 ? 0 : animLength == 13 ? 1 : 2;

                    // Remapping UI: prepare map lists for each target type
                    var mapHigh = new System.Collections.Generic.List<ComboBox>();
                    var mapLow = new System.Collections.Generic.List<ComboBox>();
                    var mapPeople = new System.Collections.Generic.List<ComboBox>();

                    // Panel sizes per export target: fixed values
                    const int panelHeightHigh = 621;
                    const int panelHeightLow = 370;
                    const int panelHeightPeople = 621;
                    const int panelWidthHigh = 360;
                    const int panelWidthLow = 360;
                    // wider panel for People so labels and comboboxes don't require horizontal scroll
                    const int panelWidthPeople = 570;
                    // preferred combobox width for People (narrower than full panel)
                    const int cbWidthPeople = 300;
                    const int labelWidthHigh = 110;
                    const int labelWidthLow = 110;
                    const int labelWidthPeople = 240;

                    // helper to build mapping panel
                    Panel BuildMapPanel(System.Collections.Generic.List<ComboBox> mapList, string[] targetLabels = null, int srcType = -1, int panelHeight = 370, int panelWidth = 360, int lblWidth = 110, int cbWidth = -1)
                    {
                        var pnl = new Panel { AutoScroll = true };
                        // fixed layout values
                        int y = 4;
                        int count = targetLabels != null ? targetLabels.Length : animLength;
                        int lblLeft = 4;
                        // lblWidth parameter allows wider labels for People
                        // cbLeft and cbWidth adapt to provided panelWidth
                        int cbLeft = lblLeft + lblWidth + 8;
                        int cbWidthLocal = cbWidth > 0 ? cbWidth : panelWidth - cbLeft - 8;

                        for (int tgt = 0; tgt < count; tgt++)
                        {
                            string lblText = targetLabels != null ? targetLabels[tgt] : tgt.ToString();
                            var lbl = new Label { Left = lblLeft, Top = y + 6, Width = lblWidth, Text = lblText, AutoSize = false };
                            var cb = new ComboBox { Left = cbLeft, Top = y + 2, Width = cbWidthLocal, DropDownStyle = ComboBoxStyle.DropDownList };
                            // Only add valid source actions to the dropdown
                            var items = new System.Collections.Generic.List<MapItem>();
                            items.Add(new MapItem(-1, "None"));
                            for (int src = 0; src < animLength; src++)
                            {
                                if (!AnimationEdit.IsActionDefined(_fileType, _currentBody, src))
                                    continue;

                                // Determine base label from the SOURCE animation type (animLength of the currently loaded animation)
                                int effectiveSourceType = Animations.GetAnimLength(_currentBody, _fileType) == 22 ? 0 : Animations.GetAnimLength(_currentBody, _fileType) == 13 ? 1 : 2;
                                string baseLabel;
                                if (effectiveSourceType == 1 && src < lowdropdownLabels.Length)
                                {
                                    baseLabel = lowdropdownLabels[src];
                                }
                                else if (effectiveSourceType == 0 && src < highdropdownLabels.Length)
                                {
                                    baseLabel = highdropdownLabels[src];
                                }
                                else if (effectiveSourceType == 2 && src < peopledropdownLabels.Length)
                                {
                                    baseLabel = peopledropdownLabels[src];
                                }
                                else
                                {
                                    baseLabel = src.ToString();
                                }

                                items.Add(new MapItem(src, baseLabel));
                            }

                            foreach (var it in items) cb.Items.Add(it);
                            // default mapping: identity if defined, otherwise None
                            MapItem selItem = items.FirstOrDefault(x => x.Index == tgt);
                            if (selItem != null) cb.SelectedItem = selItem; else cb.SelectedIndex = 0;
                            pnl.Controls.Add(lbl);
                            pnl.Controls.Add(cb);
                            mapList.Add(cb);
                            y += 28;
                        }
                        // set a reasonable size; caller will position panel
                        pnl.Size = new System.Drawing.Size(panelWidth, panelHeight);
                        return pnl;
                    }

                    // Panels will be created once below and shown/hidden based on selection

                    var panelHigh = BuildMapPanel(mapHigh, highLabels, sourceType, panelHeightHigh, panelWidthHigh, labelWidthHigh);
                    var panelLow = BuildMapPanel(mapLow, lowLabels, sourceType, panelHeightLow, panelWidthLow, labelWidthLow);
                    var panelPeople = BuildMapPanel(mapPeople, peopleLabels, sourceType, panelHeightPeople, panelWidthPeople, labelWidthPeople, cbWidthPeople);

                    // position panels below radio buttons
                    panelHigh.Location = new System.Drawing.Point(8, 40);
                    panelLow.Location = new System.Drawing.Point(8, 40);
                    panelPeople.Location = new System.Drawing.Point(8, 40);
                    panelHigh.Visible = rbHigh.Checked;
                    panelLow.Visible = rbLow.Checked;
                    panelPeople.Visible = rbPeople.Checked;

                    // declare buttons so the rbChanged handler can reference them
                    Button btnOk = null;
                    Button btnCancel = null;

                    EventHandler rbChanged = (s, ev) =>
                    {
                        panelHigh.Visible = rbHigh.Checked;
                        panelLow.Visible = rbLow.Checked;
                        panelPeople.Visible = rbPeople.Checked;

                        // adjust dialog size based on selected target panel
                        int newPanelH = rbHigh.Checked ? panelHeightHigh : rbLow.Checked ? panelHeightLow : panelHeightPeople;
                        int newPanelW = rbHigh.Checked ? panelWidthHigh : rbLow.Checked ? panelWidthLow : panelWidthPeople;
                        // keep a small margin around the panel
                        cfg.ClientSize = new System.Drawing.Size(newPanelW + 60, newPanelH + 120);

                        // reposition OK/Cancel so they stay a fixed distance from bottom
                        btnOk.Top = cfg.ClientSize.Height - 45;
                        btnCancel.Top = cfg.ClientSize.Height - 45;
                    };

                    rbHigh.CheckedChanged += rbChanged;
                    rbLow.CheckedChanged += rbChanged;
                    rbPeople.CheckedChanged += rbChanged;

                    // set initial dialog size based on defaultType and panel heights
                    int initialPanelHeight = defaultType == 0 ? panelHeightHigh : defaultType == 1 ? panelHeightLow : panelHeightPeople;
                    int initialPanelWidth = defaultType == 0 ? panelWidthHigh : defaultType == 1 ? panelWidthLow : panelWidthPeople;
                    cfg.ClientSize = new System.Drawing.Size(initialPanelWidth + 60, initialPanelHeight + 120);

                    btnOk = new Button { Text = "OK", Left = 240, Width = 60, Top = cfg.ClientSize.Height - 45, DialogResult = DialogResult.OK };
                    btnCancel = new Button { Text = "Cancel", Left = 310, Width = 60, Top = cfg.ClientSize.Height - 45, DialogResult = DialogResult.Cancel };

                    cfg.Controls.AddRange(new Control[] { lblType, rbHigh, rbLow, rbPeople, panelHigh, panelLow, panelPeople, btnOk, btnCancel });
                    cfg.AcceptButton = btnOk;
                    cfg.CancelButton = btnCancel;

                    if (cfg.ShowDialog(this) != DialogResult.OK)
                    {
                        return;
                    }


                    int selectedType = rbHigh.Checked ? 0 : rbLow.Checked ? 1 : 2; // 0=High,1=Low,2=People

                    // Collect checked actions
                    int[] actionsToInclude = null;
                    // Build target->source mapping from the selected tab's ComboBoxes
                    int[] targetToSourceMap = null;
                    System.Collections.Generic.List<ComboBox> chosenMap = null;
                    if (selectedType == 0) chosenMap = mapHigh;
                    else if (selectedType == 1) chosenMap = mapLow;
                    else if (selectedType == 2) chosenMap = mapPeople;
                    else chosenMap = mapHigh;

                    if (chosenMap != null)
                    {
                        targetToSourceMap = new int[chosenMap.Count];
                        for (int t = 0; t < chosenMap.Count; t++)
                        {
                            var selObj = chosenMap[t].SelectedItem as MapItem;
                            if (selObj == null || selObj.Index < 0) targetToSourceMap[t] = -1; else targetToSourceMap[t] = selObj.Index;
                        }
                    }

                    string filePath = dialog.FileName;
                    // if exactly one target mapped to a single source, append that to filename
                    if (targetToSourceMap != null)
                    {
                        int single = -1;
                        for (int i = 0; i < targetToSourceMap.Length; i++)
                        {
                            if (targetToSourceMap[i] >= 0)
                            {
                                if (single == -1) single = i; else { single = -2; break; }
                            }
                        }
                        if (single >= 0)
                        {
                            string dir = Path.GetDirectoryName(filePath);
                            string baseName = Path.GetFileNameWithoutExtension(filePath);
                            string ext = Path.GetExtension(filePath);
                            filePath = Path.Combine(dir, baseName + $"_t{single}_s{targetToSourceMap[single]}" + ext);
                        }
                    }

                    if (selectedType >= 0)
                    {
                        AnimationEdit.ExportToVDRemap(_fileType, _currentBody, filePath, selectedType, targetToSourceMap);
                    }
                    else
                    {
                        AnimationEdit.ExportToVDRemap(_fileType, _currentBody, filePath, -1, targetToSourceMap);
                    }

                    MessageBox.Show($"Animation saved to {Path.GetDirectoryName(filePath)}", "Export", MessageBoxButtons.OK,
                        MessageBoxIcon.Information, MessageBoxDefaultButton.Button1);
                }
            }
        }

        private void OnClickShowOnlyValid(object sender, EventArgs e)
        {
            _showOnlyValid = !_showOnlyValid;

            if (_showOnlyValid)
            {
                AnimationListTreeView.BeginUpdate();
                try
                {
                    for (int i = AnimationListTreeView.Nodes.Count - 1; i >= 0; --i)
                    {
                        if (AnimationListTreeView.Nodes[i].ForeColor == Color.Red)
                        {
                            AnimationListTreeView.Nodes[i].Remove();
                        }
                    }
                }
                finally
                {
                    AnimationListTreeView.EndUpdate();
                }
            }
            else
            {
                OnLoad(null);
            }
        }

        //My Soulblighter Modification
        private void SameCenterButton_Click(object sender, EventArgs e)
        {
            // TODO: there is no undo for same center button
            try
            {
                if (_fileType == 0)
                {
                    return;
                }

                AnimIdx edit = AnimationEdit.GetAnimation(_fileType, _currentBody, _currentAction, _currentDir);
                if (edit == null || edit.Frames.Count < FramesTrackBar.Value)
                {
                    return;
                }

                FrameEdit[] frame = new FrameEdit[edit.Frames.Count];
                for (int index = 0; index < edit.Frames.Count; index++)
                {
                    frame[index] = edit.Frames[index];
                    frame[index].ChangeCenter((int)CenterXNumericUpDown.Value, (int)CenterYNumericUpDown.Value);
                    Options.ChangedUltimaClass["Animations"] = true;
                    AnimationPictureBox.Invalidate();
                }
            }
            catch (NullReferenceException)
            {
                // TODO: add logging or fix?
                // ignored
            }
        }
        //End of Soulblighter Modification

        //My Soulblighter Modification
        private void FromGifToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_fileType == 0)
            {
                return;
            }

            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Multiselect = false;
                dialog.Title = "Choose palette file";
                dialog.CheckFileExists = true;
                dialog.Filter = "Gif files (*.gif)|*.gif";
                if (dialog.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                Bitmap bit = new Bitmap(dialog.FileName);
                AnimIdx edit = AnimationEdit.GetAnimation(_fileType, _currentBody, _currentAction, _currentDir);
                if (edit == null)
                {
                    return;
                }

                FrameDimension dimension = new FrameDimension(bit.FrameDimensionsList[0]);
                // Number of frames
                //int frameCount = bit.GetFrameCount(dimension); // TODO: unused variable?
                bit.SelectActiveFrame(dimension, 0);
                UpdateGifPalette(bit, edit);
                SetPaletteBox();
                FramesListView.Invalidate();
                Options.ChangedUltimaClass["Animations"] = true;
            }
        }

        private void ReferencePointX(object sender, EventArgs e)
        {
            AnimationPictureBox.Invalidate();
        }

        private void ReferencePointY(object sender, EventArgs e)
        {
            AnimationPictureBox.Invalidate();
        }

        private static bool _lockButton;

        private void AnimationPictureBox_MouseClick(object sender, MouseEventArgs e)
        {
            if (_lockButton || !ToolStripLockButton.Enabled)
            {
                return;
            }

            RefXNumericUpDown.Value = 418 - e.X;
            RefYNumericUpDown.Value = 335 - e.Y;

            AnimationPictureBox.Invalidate();
        }

        // Change center of frame on key press
        private void TxtSendData_KeyDown(object sender, KeyEventArgs e)
        {
            if (AnimationTimer.Enabled)
            {
                return;
            }

            switch (e.KeyCode)
            {
                case Keys.Right:
                    {
                        CenterXNumericUpDown.Value--;
                        CenterXNumericUpDown.Invalidate();
                        break;
                    }
                case Keys.Left:
                    {
                        CenterXNumericUpDown.Value++;
                        CenterXNumericUpDown.Invalidate();
                        break;
                    }
                case Keys.Up:
                    {
                        CenterYNumericUpDown.Value++;
                        CenterYNumericUpDown.Invalidate();
                        break;
                    }
                case Keys.Down:
                    {
                        CenterYNumericUpDown.Value--;
                        CenterYNumericUpDown.Invalidate();
                        break;
                    }
            }
            AnimationPictureBox.Invalidate();
        }

        // Change center of Reference Point on key press
        private void TxtSendData_KeyDown2(object sender, KeyEventArgs e)
        {
            if (_lockButton || !ToolStripLockButton.Enabled)
            {
                return;
            }

            switch (e.KeyCode)
            {
                case Keys.Right:
                    {
                        RefXNumericUpDown.Value--;
                        RefXNumericUpDown.Invalidate();
                        break;
                    }
                case Keys.Left:
                    {
                        RefXNumericUpDown.Value++;
                        RefXNumericUpDown.Invalidate();
                        break;
                    }
                case Keys.Up:
                    {
                        RefYNumericUpDown.Value++;
                        RefYNumericUpDown.Invalidate();
                        break;
                    }
                case Keys.Down:
                    {
                        RefYNumericUpDown.Value--;
                        RefYNumericUpDown.Invalidate();
                        break;
                    }
            }
            AnimationPictureBox.Invalidate();
        }

        private void ToolStripLockButton_Click(object sender, EventArgs e)
        {
            _lockButton = !_lockButton;
            RefXNumericUpDown.Enabled = !_lockButton;
            RefYNumericUpDown.Enabled = !_lockButton;
        }

        // Add in all Directions
        private void AllDirectionsAddToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_fileType != 0)
            {
                using (OpenFileDialog dialog = new OpenFileDialog())
                {
                    dialog.Multiselect = true;
                    dialog.Title = "Choose 5 GIFs to add";
                    dialog.CheckFileExists = true;
                    dialog.Filter = "Gif files (*.gif;)|*.gif;";
                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        DirectionTrackBar.Enabled = false;
                        if (dialog.FileNames.Length == 5)
                        {
                            DirectionTrackBar.Value = 0;

                            AddFilesAllDirections(dialog);
                        }

                        // Looping if dialog.FileNames.Length != 5
                        while (dialog.FileNames.Length != 5)
                        {
                            if (dialog.ShowDialog() == DialogResult.Cancel)
                            {
                                break;
                            }

                            if (dialog.FileNames.Length != 5)
                            {
                                dialog.ShowDialog();
                            }

                            if (dialog.FileNames.Length != 5)
                            {
                                continue;
                            }

                            DirectionTrackBar.Value = 0;

                            AddFilesAllDirections(dialog);
                        }
                        DirectionTrackBar.Enabled = true;
                    }
                }
            }

            // Refresh List
            _currentDir = DirectionTrackBar.Value;
            AfterSelectTreeView(null, null);
        }

        private void AddFilesAllDirections(OpenFileDialog dialog)
        {
            for (int w = 0; w < dialog.FileNames.Length; w++)
            {
                if (w >= 5)
                {
                    continue;
                }

                AnimIdx edit = AnimationEdit.GetAnimation(_fileType, _currentBody, _currentAction, _currentDir);

                if (edit != null)
                {
                    // Gif Especial Properties
                    if (dialog.FileNames[w].Contains(".gif"))
                    {
                        // dialog.Filename replaced by dialog.FileNames[w]
                        using (Bitmap bmpTemp = new Bitmap(dialog.FileNames[w]))
                        {
                            Bitmap bitmap = new Bitmap(bmpTemp);

                            FrameDimension dimension = new FrameDimension(bitmap.FrameDimensionsList[0]);

                            // Number of frames
                            int frameCount = bitmap.GetFrameCount(dimension);

                            ProgressBar.Maximum = frameCount;

                            bitmap.SelectActiveFrame(dimension, 0);

                            UpdateGifPalette(bitmap, edit);

                            Bitmap[] bitBmp = new Bitmap[frameCount];

                            AddImageAtCertainIndex(frameCount, bitBmp, bitmap, dimension, edit);

                            ProgressBar.Value = 0;
                            ProgressBar.Invalidate();

                            SetPaletteBox();

                            FramesListView.Invalidate();

                            CenterXNumericUpDown.Value = edit.Frames[FramesTrackBar.Value].Center.X;
                            CenterYNumericUpDown.Value = edit.Frames[FramesTrackBar.Value].Center.Y;

                            Options.ChangedUltimaClass["Animations"] = true;
                        }
                    }
                }

                if ((w < 4) && (w < dialog.FileNames.Length - 1))
                {
                    DirectionTrackBar.Value++;
                }
            }
        }

        private void DrawEmptyRectangleToolStripButton_Click(object sender, EventArgs e)
        {
            _drawEmpty = !_drawEmpty;
            AnimationPictureBox.Invalidate();
        }

        private void DrawFullRectangleToolStripButton_Click(object sender, EventArgs e)
        {
            _drawFull = !_drawFull;
            AnimationPictureBox.Invalidate();
        }

        private void AnimationEdit_FormClosing(object sender, FormClosingEventArgs e)
        {
            AnimationTimer.Enabled = false;
            _drawFull = false;
            _drawEmpty = false;
            _lockButton = false;
            _loaded = false;

            ControlEvents.FilePathChangeEvent -= OnFilePathChangeEvent;
        }

        private void AnimationTimer_Tick(object sender, EventArgs e)
        {
            if (FramesTrackBar.Value < FramesTrackBar.Maximum)
            {
                FramesTrackBar.Value++;
            }
            else
            {
                FramesTrackBar.Value = 0;
            }

            AnimationPictureBox.Invalidate();
        }

        private void ToolStripButtonPlayAnimation_Click(object sender, EventArgs e)
        {
            if (AnimationTimer.Enabled)
            {
                AnimationTimer.Enabled = false;
                FramesTrackBar.Enabled = true;
                SameCenterButton.Enabled = true;
                CenterXNumericUpDown.Enabled = true;
                CenterYNumericUpDown.Enabled = true;

                if (DrawReferencialPointToolStripButton.Checked)
                {
                    ToolStripLockButton.Enabled = false;
                    _blackUndraw = _blackUnDrawTransparent;
                    _whiteUnDraw = _whiteUnDrawTransparent;
                }
                else
                {
                    ToolStripLockButton.Enabled = true;
                    _blackUndraw = _blackUnDrawOpaque;
                    _whiteUnDraw = _whiteUnDrawOpaque;
                }

                if (ToolStripLockButton.Checked || DrawReferencialPointToolStripButton.Checked)
                {
                    RefXNumericUpDown.Enabled = false;
                    RefYNumericUpDown.Enabled = false;
                }
                else
                {
                    RefXNumericUpDown.Enabled = true;
                    RefYNumericUpDown.Enabled = true;
                }
            }
            else
            {
                AnimationTimer.Enabled = true;
                FramesTrackBar.Enabled = false;
                SameCenterButton.Enabled = false;

                CenterXNumericUpDown.Enabled = false;
                CenterYNumericUpDown.Enabled = false;
            }

            AnimationPictureBox.Invalidate();
        }

        private void AnimationSpeedTrackBar_ValueChanged(object sender, EventArgs e)
        {
            AnimationTimer.Interval = 50 + (AnimationSpeedTrackBar.Value * 30);
        }

        private void DrawReferencialPointToolStripButton_Click(object sender, EventArgs e)
        {
            if (!DrawReferencialPointToolStripButton.Checked)
            {
                _blackUndraw = _blackUnDrawOpaque;
                _whiteUnDraw = _whiteUnDrawOpaque;

                ToolStripLockButton.Enabled = true;
                if (ToolStripLockButton.Checked)
                {
                    RefXNumericUpDown.Enabled = false;
                    RefYNumericUpDown.Enabled = false;
                }
                else
                {
                    RefXNumericUpDown.Enabled = true;
                    RefYNumericUpDown.Enabled = true;
                }
            }
            else
            {
                _blackUndraw = _blackUnDrawTransparent;
                _whiteUnDraw = _whiteUnDrawTransparent;
                ToolStripLockButton.Enabled = false;
                RefXNumericUpDown.Enabled = false;
                RefYNumericUpDown.Enabled = false;
            }
            AnimationPictureBox.Invalidate();
        }

        // All Directions with Canvas
        private void AllDirectionsAddWithCanvasToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                if (_fileType != 0)
                {
                    using (OpenFileDialog dialog = new OpenFileDialog())
                    {
                        dialog.Multiselect = true;
                        dialog.Title = "Choose 5 GIFs to add";
                        dialog.CheckFileExists = true;
                        dialog.Filter = "Gif files (*.gif;)|*.gif;";
                        if (dialog.ShowDialog() == DialogResult.OK)
                        {
                            Color customConvert = Color.FromArgb(255, (int)numericUpDownRed.Value, (int)numericUpDownGreen.Value, (int)numericUpDownBlue.Value);
                            DirectionTrackBar.Enabled = false;
                            if (dialog.FileNames.Length == 5)
                            {
                                DirectionTrackBar.Value = 0;
                                AddSelectedFiles(dialog, customConvert);
                            }

                            // Looping if dialog.FileNames.Length != 5
                            while (dialog.FileNames.Length != 5)
                            {
                                if (dialog.ShowDialog() == DialogResult.Cancel)
                                {
                                    break;
                                }

                                if (dialog.FileNames.Length != 5)
                                {
                                    dialog.ShowDialog();
                                }

                                if (dialog.FileNames.Length != 5)
                                {
                                    continue;
                                }

                                DirectionTrackBar.Value = 0;
                                AddSelectedFiles(dialog, customConvert);
                            }

                            DirectionTrackBar.Enabled = true;
                        }
                    }
                }

                // Refresh List after Canvas reduction
                _currentDir = DirectionTrackBar.Value;
                AfterSelectTreeView(null, null);
            }
            catch (OutOfMemoryException)
            {
                // TODO: add logging or fix?
                // ignored
            }
        }

        private void AddSelectedFiles(OpenFileDialog dialog, Color customConvert)
        {
            for (int w = 0; w < dialog.FileNames.Length; w++)
            {
                if (w >= 5)
                {
                    continue;
                }

                // dialog.Filename replaced by dialog.FileNames[w]
                Bitmap bmp = new Bitmap(dialog.FileNames[w]);

                // TODO: fix checking file extension
                // Gif Especial Properties
                if (!dialog.FileNames[w].Contains(".gif"))
                {
                    continue;
                }

                AddAnimationX1(customConvert, bmp);

                if ((w < 4) && (w < dialog.FileNames.Length - 1))
                {
                    DirectionTrackBar.Value++;
                }
            }
        }

        private void AddAnimationX1(Color customConvert, Bitmap bmp)
        {
            AnimIdx edit = AnimationEdit.GetAnimation(_fileType, _currentBody, _currentAction, _currentDir);
            if (edit == null)
            {
                return;
            }

            FrameDimension dimension = new FrameDimension(bmp.FrameDimensionsList[0]);

            // Number of frames
            int frameCount = bmp.GetFrameCount(dimension);
            ProgressBar.Maximum = frameCount;
            bmp.SelectActiveFrame(dimension, 0);
            UpdateGifPalette(bmp, edit);

            // Return an Image at a certain index
            Bitmap[] bitBmp = new Bitmap[frameCount];
            for (int index = 0; index < frameCount; index++)
            {
                bitBmp[index] = new Bitmap(bmp.Width, bmp.Height, PixelFormat.Format16bppArgb1555);
                bmp.SelectActiveFrame(dimension, index);
                bitBmp[index] = bmp;
            }

            // Canvas algorithm
            int top = 0;
            int bottom = 0;
            int left = 0;
            int right = 0;

            int regressT = -1;
            int regressB = -1;
            int regressL = -1;
            int regressR = -1;

            bool var = true;
            bool breakOk = false;

            // Top
            for (int yf = 0; yf < bitBmp[0].Height; yf++)
            {
                for (int frameIdx = 0; frameIdx < frameCount; frameIdx++)
                {
                    bitBmp[frameIdx].SelectActiveFrame(dimension, frameIdx);
                    for (int xf = 0; xf < bitBmp[0].Width; xf++)
                    {
                        Color pixel = bitBmp[frameIdx].GetPixel(xf, yf);
                        if (pixel == _whiteConvert || pixel == customConvert || pixel.A == 0)
                        {
                            var = true;
                        }

                        if (pixel != _whiteConvert && pixel != customConvert && pixel.A != 0)
                        {
                            var = false;
                            if (yf != 0)
                            {
                                regressT++;
                                yf--;
                                xf = -1;
                                frameIdx = 0;
                            }
                            else
                            {
                                breakOk = true;
                                break;
                            }
                        }

                        if (var && xf == bitBmp[frameIdx].Width - 1 && frameIdx == frameCount - 1 && regressT == -1 &&
                            yf < bitBmp[0].Height - 9)
                        {
                            top += 10;
                        }

                        if (var && xf == bitBmp[frameIdx].Width - 1 && frameIdx == frameCount - 1 && regressT == -1 &&
                            yf >= bitBmp[0].Height - 9)
                        {
                            top++;
                        }

                        if (var && xf == bitBmp[frameIdx].Width - 1 && frameIdx == frameCount - 1 && regressT != -1)
                        {
                            top -= regressT;
                            breakOk = true;
                            break;
                        }
                    }

                    if (breakOk)
                    {
                        break;
                    }
                }

                if (yf < bitBmp[0].Height - 9)
                {
                    yf += 9; // 1 of for + 9
                }

                if (breakOk)
                {
                    breakOk = false;
                    break;
                }
            }

            // Bottom
            for (int yf = bitBmp[0].Height - 1; yf > 0; yf--)
            {
                for (int frameIdx = 0; frameIdx < frameCount; frameIdx++)
                {
                    bitBmp[frameIdx].SelectActiveFrame(dimension, frameIdx);
                    for (int xf = 0; xf < bitBmp[0].Width; xf++)
                    {
                        Color pixel = bitBmp[frameIdx].GetPixel(xf, yf);
                        if (pixel == _whiteConvert || pixel == customConvert || pixel.A == 0)
                        {
                            var = true;
                        }

                        if (pixel != _whiteConvert && pixel != customConvert && pixel.A != 0)
                        {
                            var = false;
                            if (yf != bitBmp[0].Height - 1)
                            {
                                regressB++;
                                yf++;
                                xf = -1;
                                frameIdx = 0;
                            }
                            else
                            {
                                breakOk = true;
                                break;
                            }
                        }

                        if (var && xf == bitBmp[frameIdx].Width - 1 && frameIdx == frameCount - 1 && regressB == -1 &&
                            yf > 9)
                        {
                            bottom += 10;
                        }

                        if (var && xf == bitBmp[frameIdx].Width - 1 && frameIdx == frameCount - 1 && regressB == -1 &&
                            yf <= 9)
                        {
                            bottom++;
                        }

                        if (var && xf == bitBmp[frameIdx].Width - 1 && frameIdx == frameCount - 1 && regressB != -1)
                        {
                            bottom -= regressB;
                            breakOk = true;
                            break;
                        }
                    }

                    if (breakOk)
                    {
                        break;
                    }
                }

                if (yf > 9)
                {
                    yf -= 9; // 1 of for + 9
                }

                if (breakOk)
                {
                    breakOk = false;
                    break;
                }
            }

            // Left
            for (int xf = 0; xf < bitBmp[0].Width; xf++)
            {
                for (int frameIdx = 0; frameIdx < frameCount; frameIdx++)
                {
                    bitBmp[frameIdx].SelectActiveFrame(dimension, frameIdx);
                    for (int yf = 0; yf < bitBmp[0].Height; yf++)
                    {
                        Color pixel = bitBmp[frameIdx].GetPixel(xf, yf);
                        if (pixel == _whiteConvert || pixel == customConvert || pixel.A == 0)
                        {
                            var = true;
                        }

                        if (pixel != _whiteConvert && pixel != customConvert && pixel.A != 0)
                        {
                            var = false;
                            if (xf != 0)
                            {
                                regressL++;
                                xf--;
                                yf = -1;
                                frameIdx = 0;
                            }
                            else
                            {
                                breakOk = true;
                                break;
                            }
                        }

                        if (var && yf == bitBmp[frameIdx].Height - 1 && frameIdx == frameCount - 1 && regressL == -1 &&
                            xf < bitBmp[0].Width - 9)
                        {
                            left += 10;
                        }

                        if (var && yf == bitBmp[frameIdx].Height - 1 && frameIdx == frameCount - 1 && regressL == -1 &&
                            xf >= bitBmp[0].Width - 9)
                        {
                            left++;
                        }

                        if (var && yf == bitBmp[frameIdx].Height - 1 && frameIdx == frameCount - 1 && regressL != -1)
                        {
                            left -= regressL;
                            breakOk = true;
                            break;
                        }
                    }

                    if (breakOk)
                    {
                        break;
                    }
                }

                if (xf < bitBmp[0].Width - 9)
                {
                    xf += 9; // 1 of for + 9
                }

                if (breakOk)
                {
                    breakOk = false;
                    break;
                }
            }

            // Right
            for (int xf = bitBmp[0].Width - 1; xf > 0; xf--)
            {
                for (int frameIdx = 0; frameIdx < frameCount; frameIdx++)
                {
                    bitBmp[frameIdx].SelectActiveFrame(dimension, frameIdx);
                    for (int yf = 0; yf < bitBmp[0].Height; yf++)
                    {
                        Color pixel = bitBmp[frameIdx].GetPixel(xf, yf);
                        if (pixel == _whiteConvert || pixel == customConvert || pixel.A == 0)
                        {
                            var = true;
                        }

                        if (pixel != _whiteConvert && pixel != customConvert && pixel.A != 0)
                        {
                            var = false;
                            if (xf != bitBmp[0].Width - 1)
                            {
                                regressR++;
                                xf++;
                                yf = -1;
                                frameIdx = 0;
                            }
                            else
                            {
                                breakOk = true;
                                break;
                            }
                        }

                        if (var && yf == bitBmp[frameIdx].Height - 1 && frameIdx == frameCount - 1 && regressR == -1 &&
                            xf > 9)
                        {
                            right += 10;
                        }

                        if (var && yf == bitBmp[frameIdx].Height - 1 && frameIdx == frameCount - 1 && regressR == -1 &&
                            xf <= 9)
                        {
                            right++;
                        }

                        if (var && yf == bitBmp[frameIdx].Height - 1 && frameIdx == frameCount - 1 && regressR != -1)
                        {
                            right -= regressR;
                            breakOk = true;
                            break;
                        }
                    }

                    if (breakOk)
                    {
                        break;
                    }
                }

                if (xf > 9)
                {
                    xf -= 9; // 1 of for + 9
                }

                if (breakOk)
                {
                    //breakOk = false;
                    break;
                }
            }

            for (int index = 0; index < frameCount; index++)
            {
                Rectangle rect = new Rectangle(left, top, bitBmp[index].Width - left - right, bitBmp[index].Height - top - bottom);
                bitBmp[index].SelectActiveFrame(dimension, index);
                bitBmp[index] = bitBmp[index].Clone(rect, PixelFormat.Format16bppArgb1555);
            }

            // End of Canvas algorithm

            for (int index = 0; index < frameCount; index++)
            {
                bitBmp[index].SelectActiveFrame(dimension, index);
                bitBmp[index] = ConvertBmpAnim(bitBmp[index], (int) numericUpDownRed.Value,
                    (int) numericUpDownGreen.Value, (int) numericUpDownBlue.Value);
                edit.AddFrame(bitBmp[index], bitBmp[index].Width/2);
                TreeNode node = GetNode(_currentBody);
                if (node != null)
                {
                    node.ForeColor = Color.Black;
                    node.Nodes[_currentAction].ForeColor = Color.Black;
                }

                int i = edit.Frames.Count - 1;
                var item = new ListViewItem(i.ToString(), 0)
                {
                    Tag = i
                };
                FramesListView.Items.Add(item);
                int width = FramesListView.TileSize.Width - 5;
                if (bmp.Width > FramesListView.TileSize.Width)
                {
                    width = bmp.Width;
                }

                int height = FramesListView.TileSize.Height - 5;
                if (bmp.Height > FramesListView.TileSize.Height)
                {
                    height = bmp.Height;
                }

                FramesListView.TileSize = new Size(width + 5, height + 5);
                FramesTrackBar.Maximum = i;
                Options.ChangedUltimaClass["Animations"] = true;
                if (ProgressBar.Value < ProgressBar.Maximum)
                {
                    ProgressBar.Value++;
                    ProgressBar.Invalidate();
                }
            }

            ProgressBar.Value = 0;
            ProgressBar.Invalidate();
            SetPaletteBox();
            FramesListView.Invalidate();
            CenterXNumericUpDown.Value = edit.Frames[FramesTrackBar.Value].Center.X;
            CenterYNumericUpDown.Value = edit.Frames[FramesTrackBar.Value].Center.Y;
            Options.ChangedUltimaClass["Animations"] = true;
        }

        //Add with Canvas
        private void AddWithCanvasToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                if (_fileType != 0)
                {
                    using (OpenFileDialog dialog = new OpenFileDialog())
                    {
                        dialog.Multiselect = false;
                        dialog.Title = "Choose image file to add";
                        dialog.CheckFileExists = true;
                        dialog.Filter = "Gif files (*.gif;)|*.gif;";
                        if (dialog.ShowDialog() == DialogResult.OK)
                        {
                            Color customConvert = Color.FromArgb(255, (int)numericUpDownRed.Value,
                                (int)numericUpDownGreen.Value, (int)numericUpDownBlue.Value);
                            //My Soulblighter Modifications
                            for (int w = 0; w < dialog.FileNames.Length; w++)
                            {
                                // dialog.Filename replaced by dialog.FileNames[w]
                                Bitmap bmp = new Bitmap(dialog.FileNames[w]);

                                // TODO: fix checking file extension
                                // Gif Especial Properties
                                if (!dialog.FileNames[w].Contains(".gif"))
                                {
                                    continue;
                                }

                                AddAnimationX1(customConvert, bmp);
                            }
                        }
                    }
                }

                // Refresh List after Canvas reduction
                _currentDir = DirectionTrackBar.Value;
                AfterSelectTreeView(null, null);
            }
            catch (OutOfMemoryException)
            {
                // TODO: add logging or fix?
                // ignored
            }
        }

        private void OnClickGeneratePalette(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Multiselect = true;
                dialog.Title = "Choose images to generate from";
                dialog.CheckFileExists = true;
                dialog.Filter = "Image files (*.tif;*.tiff;*.bmp;*.png;*.jpg;*.jpeg)|*.tif;*.tiff;*.bmp;*.png;*.jpg;*.jpeg";
                if (dialog.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                foreach (string filename in dialog.FileNames)
                {
                    Bitmap bit = new Bitmap(filename);
                    AnimIdx edit = AnimationEdit.GetAnimation(_fileType, _currentBody, _currentAction, _currentDir);
                    if (edit != null)
                    {
                        bit = ConvertBmpAnim(bit, (int)numericUpDownRed.Value, (int)numericUpDownGreen.Value, (int)numericUpDownBlue.Value);
                        UpdateImagePalette(bit, edit);
                    }
                    SetPaletteBox();
                    FramesListView.Invalidate();
                    Options.ChangedUltimaClass["Animations"] = true;
                    SetPaletteBox();
                }
            }
        }
        //End of Soulblighter Modification

        private static unsafe Bitmap ConvertBmpAnim(Bitmap bmp, int red, int green, int blue)
        {
            //Extra background
            int extraBack = (red / 8 * 1024) + (green / 8 * 32) + (blue / 8) + 32768;

            BitmapData bd = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format16bppArgb1555);
            ushort* line = (ushort*)bd.Scan0;
            int delta = bd.Stride >> 1;

            Bitmap bmpNew = new Bitmap(bmp.Width, bmp.Height, PixelFormat.Format16bppArgb1555);
            BitmapData bdNew = bmpNew.LockBits(new Rectangle(0, 0, bmpNew.Width, bmpNew.Height), ImageLockMode.WriteOnly, PixelFormat.Format16bppArgb1555);

            ushort* lineNew = (ushort*)bdNew.Scan0;
            int deltaNew = bdNew.Stride >> 1;

            for (int y = 0; y < bmp.Height; ++y, line += delta, lineNew += deltaNew)
            {
                ushort* cur = line;
                ushort* curNew = lineNew;
                for (int x = 0; x < bmp.Width; ++x)
                {
                    //My Soulblighter Modification
                    // Convert color 0,0,0 to 0,0,8
                    if (cur[x] == 32768)
                    {
                        curNew[x] = 32769;
                    }

                    if (cur[x] != 65535 && cur[x] != extraBack && cur[x] > 32768) //True White == BackGround
                    {
                        curNew[x] = cur[x];
                    }
                    //End of Soulblighter Modification
                }
            }
            bmp.UnlockBits(bd);
            bmpNew.UnlockBits(bdNew);
            return bmpNew;
        }

        private void OnClickExportAllToVD(object sender, EventArgs e)
        {
            if (_fileType == 0)
            {
                return;
            }

            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select directory";
                dialog.ShowNewFolderButton = true;
                if (dialog.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                for (int i = 0; i < AnimationListTreeView.Nodes.Count; ++i)
                {
                    int index = (int)AnimationListTreeView.Nodes[i].Tag;
                    if (index < 0 || AnimationListTreeView.Nodes[i].Parent != null ||
                        AnimationListTreeView.Nodes[i].ForeColor == Color.Red)
                    {
                        continue;
                    }

                    string fileName = Path.Combine(dialog.SelectedPath, $"anim{_fileType}_0x{index:X}.vd");
                    AnimationEdit.ExportToVD(_fileType, index, fileName);
                }

                MessageBox.Show($"All Animations saved to {dialog.SelectedPath}",
                    "Export", MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1);
            }
        }

        private void CbSaveCoordinates_CheckedChanged(object sender, EventArgs e)
        {
            // Get position of all animations in array
            if (SaveCoordinatesCheckBox.Checked)
            {
                DirectionTrackBar.Enabled = false;
                FramesTrackBar.Value = 0;
                SetCoordinatesButton.Enabled = true;
                for (int count = 0; count < 5;)
                {
                    if (DirectionTrackBar.Value < 4)
                    {
                        _animCx[DirectionTrackBar.Value] = (int)CenterXNumericUpDown.Value;
                        _animCy[DirectionTrackBar.Value] = (int)CenterYNumericUpDown.Value;
                        DirectionTrackBar.Value++;
                        count++;
                    }
                    else
                    {
                        _animCx[DirectionTrackBar.Value] = (int)CenterXNumericUpDown.Value;
                        _animCy[DirectionTrackBar.Value] = (int)CenterYNumericUpDown.Value;
                        DirectionTrackBar.Value = 0;
                        count++;
                    }
                }

                SaveCoordinatesLabel1.Text = $"1: {_animCx[0]}/{_animCy[0]}";
                SaveCoordinatesLabel2.Text = $"2: {_animCx[1]}/{_animCy[1]}";
                SaveCoordinatesLabel3.Text = $"3: {_animCx[2]}/{_animCy[2]}";
                SaveCoordinatesLabel4.Text = $"4: {_animCx[3]}/{_animCy[3]}";
                SaveCoordinatesLabel5.Text = $"5: {_animCx[4]}/{_animCy[4]}";

                DirectionTrackBar.Enabled = true;
            }
            else
            {
                SaveCoordinatesLabel1.Text = "1:    /    ";
                SaveCoordinatesLabel2.Text = "2:    /    ";
                SaveCoordinatesLabel3.Text = "3:    /    ";
                SaveCoordinatesLabel4.Text = "4:    /    ";
                SaveCoordinatesLabel5.Text = "5:    /    ";
                SetCoordinatesButton.Enabled = false;
            }
        }

        private void SetButton_Click(object sender, EventArgs e)
        {
            DirectionTrackBar.Value = 0;
            DirectionTrackBar.Enabled = false;
            for (int i = 0; i <= DirectionTrackBar.Maximum; i++)
            {
                try
                {
                    if (_fileType != 0)
                    {
                        AnimIdx edit = AnimationEdit.GetAnimation(_fileType, _currentBody, _currentAction, _currentDir);
                        if (edit != null && edit.Frames.Count >= FramesTrackBar.Value)
                        {
                            foreach (var editFrame in edit.Frames)
                            {
                                editFrame.ChangeCenter(_animCx[i], _animCy[i]);
                                // TODO: check if we can invalidate pictture box only after the loop
                                Options.ChangedUltimaClass["Animations"] = true;
                                AnimationPictureBox.Invalidate();
                            }
                        }
                    }
                }
                catch (NullReferenceException)
                {
                    // TODO: add logging or fix?
                    // ignored
                }

                if (DirectionTrackBar.Value < DirectionTrackBar.Maximum)
                {
                    DirectionTrackBar.Value++;
                }
                else
                {
                    DirectionTrackBar.Value = 0;
                }
            }
            DirectionTrackBar.Enabled = true;
        }

        // Add Directions with Canvas ( CV5 style GIF )
        private void AddDirectionsAddWithCanvasUniqueImageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                if (_fileType != 0)
                {
                    using (OpenFileDialog dialog = new OpenFileDialog())
                    {
                        dialog.Multiselect = false;
                        dialog.Title = "Choose 1 Gif ( with all directions in CV5 Style ) to add";
                        dialog.CheckFileExists = true;
                        dialog.Filter = "Gif files (*.gif;)|*.gif;";

                        if (dialog.ShowDialog() == DialogResult.OK)
                        {
                            Color customConvert = Color.FromArgb(255, (int)numericUpDownRed.Value, (int)numericUpDownGreen.Value, (int)numericUpDownBlue.Value);

                            DirectionTrackBar.Enabled = false;
                            DirectionTrackBar.Value = 0;

                            AnimIdx edit = AnimationEdit.GetAnimation(_fileType, _currentBody, _currentAction, _currentDir);

                            if (edit != null)
                            {
                                // Gif Especial Properties
                                if (dialog.FileName.Contains(".gif"))
                                {
                                    using (Bitmap bmpTemp = new Bitmap(dialog.FileName))
                                    using (MemoryStream ms = new MemoryStream())
                                    {
                                        bmpTemp.Save(ms, ImageFormat.Gif);
                                        Bitmap bitmap = new Bitmap(ms);

                                        FrameDimension dimension = new FrameDimension(bitmap.FrameDimensionsList[0]);

                                        // Number of frames
                                        int frameCount = bitmap.GetFrameCount(dimension);

                                        ProgressBar.Maximum = frameCount;

                                        bitmap.SelectActiveFrame(dimension, 0);

                                        UpdateGifPalette(bitmap, edit);

                                        Bitmap[] bitBmp = new Bitmap[frameCount];

                                        // Return an Image at a certain index
                                        for (int index = 0; index < frameCount; index++)
                                        {
                                            bitBmp[index] = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format16bppArgb1555);
                                            bitmap.SelectActiveFrame(dimension, index);
                                            bitBmp[index] = bitmap;
                                        }

                                        Cv5CanvasAlgorithm(bitBmp, frameCount, dimension, customConvert);

                                        edit = Cv5AnimIdxPositions(frameCount, bitBmp, dimension, edit, bitmap);

                                        ProgressBar.Value = 0;
                                        ProgressBar.Invalidate();

                                        SetPaletteBox();
                                        FramesListView.Invalidate();

                                        CenterXNumericUpDown.Value = edit.Frames[FramesTrackBar.Value].Center.X;
                                        CenterYNumericUpDown.Value = edit.Frames[FramesTrackBar.Value].Center.Y;

                                        Options.ChangedUltimaClass["Animations"] = true;
                                    }
                                }
                            }

                            DirectionTrackBar.Enabled = true;
                        }
                    }
                }

                // Refresh List after Canvas reduction
                _currentDir = DirectionTrackBar.Value;
                AfterSelectTreeView(null, null);
            }
            catch (NullReferenceException)
            {
                // TODO: add logging or fix?
                DirectionTrackBar.Enabled = true;
            }
        }

        private AnimIdx Cv5AnimIdxPositions(int frameCount, Bitmap[] bitBmp, FrameDimension dimension, AnimIdx edit, Bitmap bmp)
        {
            // position 0
            for (int index = frameCount / 8 * 4; index < frameCount / 8 * 5; index++)
            {
                bitBmp[index].SelectActiveFrame(dimension, index);
                bitBmp[index] = ConvertBmpAnimCv5(bitBmp[index], (int) numericUpDownRed.Value, (int) numericUpDownGreen.Value,
                    (int) numericUpDownBlue.Value);
                edit.AddFrame(bitBmp[index], bitBmp[index].Width/2);
                TreeNode node = GetNode(_currentBody);
                if (node != null)
                {
                    node.ForeColor = Color.Black;
                    node.Nodes[_currentAction].ForeColor = Color.Black;
                }

                int i = edit.Frames.Count - 1;
                var item = new ListViewItem(i.ToString(), 0)
                {
                    Tag = i
                };
                FramesListView.Items.Add(item);
                int width = FramesListView.TileSize.Width - 5;
                if (bmp.Width > FramesListView.TileSize.Width)
                {
                    width = bmp.Width;
                }

                int height = FramesListView.TileSize.Height - 5;
                if (bmp.Height > FramesListView.TileSize.Height)
                {
                    height = bmp.Height;
                }

                FramesListView.TileSize = new Size(width + 5, height + 5);
                FramesTrackBar.Maximum = i;
                Options.ChangedUltimaClass["Animations"] = true;
                if (ProgressBar.Value < ProgressBar.Maximum)
                {
                    ProgressBar.Value++;
                    ProgressBar.Invalidate();
                }

                if (index == (frameCount / 8 * 5) - 1)
                {
                    DirectionTrackBar.Value++;
                }
            }

            // position 1
            edit = AnimationEdit.GetAnimation(_fileType, _currentBody, _currentAction, _currentDir);
            bmp.SelectActiveFrame(dimension, 0);
            UpdateGifPalette(bmp, edit);
            for (int index = 0; index < frameCount / 8; index++)
            {
                bitBmp[index].SelectActiveFrame(dimension, index);
                bitBmp[index] = ConvertBmpAnimCv5(bitBmp[index], (int) numericUpDownRed.Value, (int) numericUpDownGreen.Value,
                    (int) numericUpDownBlue.Value);
                edit.AddFrame(bitBmp[index], bitBmp[index].Width/2);
                TreeNode node = GetNode(_currentBody);
                if (node != null)
                {
                    node.ForeColor = Color.Black;
                    node.Nodes[_currentAction].ForeColor = Color.Black;
                }

                int i = edit.Frames.Count - 1;
                ListViewItem item = new ListViewItem(i.ToString(), 0)
                {
                    Tag = i
                };
                FramesListView.Items.Add(item);
                int width = FramesListView.TileSize.Width - 5;
                if (bmp.Width > FramesListView.TileSize.Width)
                {
                    width = bmp.Width;
                }

                int height = FramesListView.TileSize.Height - 5;
                if (bmp.Height > FramesListView.TileSize.Height)
                {
                    height = bmp.Height;
                }

                FramesListView.TileSize = new Size(width + 5, height + 5);
                FramesTrackBar.Maximum = i;
                Options.ChangedUltimaClass["Animations"] = true;
                if (ProgressBar.Value < ProgressBar.Maximum)
                {
                    ProgressBar.Value++;
                    ProgressBar.Invalidate();
                }

                if (index == (frameCount / 8) - 1)
                {
                    DirectionTrackBar.Value++;
                }
            }

            // position 2
            edit = AnimationEdit.GetAnimation(_fileType, _currentBody, _currentAction, _currentDir);
            bmp.SelectActiveFrame(dimension, 0);
            UpdateGifPalette(bmp, edit);
            for (int index = frameCount / 8 * 5; index < frameCount / 8 * 6; index++)
            {
                bitBmp[index].SelectActiveFrame(dimension, index);
                bitBmp[index] = ConvertBmpAnimCv5(bitBmp[index], (int) numericUpDownRed.Value, (int) numericUpDownGreen.Value,
                    (int) numericUpDownBlue.Value);
                edit.AddFrame(bitBmp[index], bitBmp[index].Width/2);
                TreeNode node = GetNode(_currentBody);
                if (node != null)
                {
                    node.ForeColor = Color.Black;
                    node.Nodes[_currentAction].ForeColor = Color.Black;
                }

                int i = edit.Frames.Count - 1;
                var item = new ListViewItem(i.ToString(), 0)
                {
                    Tag = i
                };
                FramesListView.Items.Add(item);
                int width = FramesListView.TileSize.Width - 5;
                if (bmp.Width > FramesListView.TileSize.Width)
                {
                    width = bmp.Width;
                }

                int height = FramesListView.TileSize.Height - 5;
                if (bmp.Height > FramesListView.TileSize.Height)
                {
                    height = bmp.Height;
                }

                FramesListView.TileSize = new Size(width + 5, height + 5);
                FramesTrackBar.Maximum = i;
                Options.ChangedUltimaClass["Animations"] = true;
                if (ProgressBar.Value < ProgressBar.Maximum)
                {
                    ProgressBar.Value++;
                    ProgressBar.Invalidate();
                }

                if (index == (frameCount / 8 * 6) - 1)
                {
                    DirectionTrackBar.Value++;
                }
            }

            // position 3
            edit = AnimationEdit.GetAnimation(_fileType, _currentBody, _currentAction, _currentDir);
            bmp.SelectActiveFrame(dimension, 0);
            UpdateGifPalette(bmp, edit);
            for (int index = frameCount / 8 * 1; index < frameCount / 8 * 2; index++)
            {
                bitBmp[index].SelectActiveFrame(dimension, index);
                bitBmp[index] = ConvertBmpAnimCv5(bitBmp[index], (int) numericUpDownRed.Value, (int) numericUpDownGreen.Value,
                    (int) numericUpDownBlue.Value);
                edit.AddFrame(bitBmp[index], bitBmp[index].Width/2);
                TreeNode node = GetNode(_currentBody);
                if (node != null)
                {
                    node.ForeColor = Color.Black;
                    node.Nodes[_currentAction].ForeColor = Color.Black;
                }

                int i = edit.Frames.Count - 1;
                var item = new ListViewItem(i.ToString(), 0)
                {
                    Tag = i
                };
                FramesListView.Items.Add(item);
                int width = FramesListView.TileSize.Width - 5;
                if (bmp.Width > FramesListView.TileSize.Width)
                {
                    width = bmp.Width;
                }

                int height = FramesListView.TileSize.Height - 5;
                if (bmp.Height > FramesListView.TileSize.Height)
                {
                    height = bmp.Height;
                }

                FramesListView.TileSize = new Size(width + 5, height + 5);
                FramesTrackBar.Maximum = i;
                Options.ChangedUltimaClass["Animations"] = true;
                if (ProgressBar.Value < ProgressBar.Maximum)
                {
                    ProgressBar.Value++;
                    ProgressBar.Invalidate();
                }

                if (index == (frameCount / 8 * 2) - 1)
                {
                    DirectionTrackBar.Value++;
                }
            }

            // position 4
            edit = AnimationEdit.GetAnimation(_fileType, _currentBody, _currentAction, _currentDir);
            bmp.SelectActiveFrame(dimension, 0);
            UpdateGifPalette(bmp, edit);
            for (int index = frameCount / 8 * 6; index < frameCount / 8 * 7; index++)
            {
                bitBmp[index].SelectActiveFrame(dimension, index);
                bitBmp[index] = ConvertBmpAnimCv5(bitBmp[index], (int) numericUpDownRed.Value, (int) numericUpDownGreen.Value,
                    (int) numericUpDownBlue.Value);
                edit.AddFrame(bitBmp[index], bitBmp[index].Width/2);
                TreeNode node = GetNode(_currentBody);
                if (node != null)
                {
                    node.ForeColor = Color.Black;
                    node.Nodes[_currentAction].ForeColor = Color.Black;
                }

                int i = edit.Frames.Count - 1;
                var item = new ListViewItem(i.ToString(), 0)
                {
                    Tag = i
                };
                FramesListView.Items.Add(item);
                int width = FramesListView.TileSize.Width - 5;
                if (bmp.Width > FramesListView.TileSize.Width)
                {
                    width = bmp.Width;
                }

                int height = FramesListView.TileSize.Height - 5;
                if (bmp.Height > FramesListView.TileSize.Height)
                {
                    height = bmp.Height;
                }

                FramesListView.TileSize = new Size(width + 5, height + 5);
                FramesTrackBar.Maximum = i;
                Options.ChangedUltimaClass["Animations"] = true;
                if (ProgressBar.Value < ProgressBar.Maximum)
                {
                    ProgressBar.Value++;
                    ProgressBar.Invalidate();
                }
            }

            return edit;
        }

        private static unsafe Bitmap ConvertBmpAnimCv5(Bitmap bmp, int red, int green, int blue)
        {
            //Extra background
            int extraBack = (red / 8 * 1024) + (green / 8 * 32) + (blue / 8) + 32768;

            BitmapData bd = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format16bppArgb1555);
            ushort* line = (ushort*)bd.Scan0;
            int delta = bd.Stride >> 1;

            Bitmap bmpNew = new Bitmap(bmp.Width, bmp.Height, PixelFormat.Format16bppArgb1555);
            BitmapData bdNew = bmpNew.LockBits(new Rectangle(0, 0, bmpNew.Width, bmpNew.Height), ImageLockMode.WriteOnly, PixelFormat.Format16bppArgb1555);

            ushort* lineNew = (ushort*)bdNew.Scan0;
            int deltaNew = bdNew.Stride >> 1;

            for (int y = 0; y < bmp.Height; ++y, line += delta, lineNew += deltaNew)
            {
                ushort* cur = line;
                ushort* curNew = lineNew;
                for (int x = 0; x < bmp.Width; ++x)
                {
                    //My Soulblighter Modification
                    // Convert color 0,0,0 to 0,0,8
                    if (cur[x] == 32768)
                    {
                        curNew[x] = 32769;
                    }

                    if (cur[x] != 65535 && cur[x] != 54965 && cur[x] != extraBack && cur[x] > 32768) //True White == BackGround
                    {
                        curNew[x] = cur[x];
                    }
                    //End of Soulblighter Modification
                }
            }
            bmp.UnlockBits(bd);
            bmpNew.UnlockBits(bdNew);
            return bmpNew;
        }

        private static readonly Color _greyConvert = Color.FromArgb(255, 170, 170, 170);

        private static void Cv5CanvasAlgorithm(Bitmap[] bitBmp, int frameCount, FrameDimension dimension, Color customConvert)
        {
            // TODO: Needs better names
            // TODO: This code needs documentation. This algorithm is not really readable

            // Order of calls looks important
            // Looks like it is import for Gif/bmps from Diablo cv5 format
            // Some materials about Diablo formats:
            // - https://d2mods.info/resources/infinitum/tut_files/dcc_tutorial/
            // - https://d2mods.info/resources/infinitum/tut_files/dcc_tutorial/chapter4.html
            //
            const int frameDivider = 8;

            // position 0
            Cv5ProcessFrames(bitBmp, dimension, customConvert, GetInitialFrameIndex(frameCount, frameDivider, 4), GetMaximumFrameIndex(frameCount, frameDivider, 4));
            // position 1
            Cv5ProcessFrames(bitBmp, dimension, customConvert, GetInitialFrameIndex(frameCount, frameDivider, 0), GetMaximumFrameIndex(frameCount, frameDivider, 0));
            // position 2
            Cv5ProcessFrames(bitBmp, dimension, customConvert, GetInitialFrameIndex(frameCount, frameDivider, 5), GetMaximumFrameIndex(frameCount, frameDivider, 5));
            // position 3
            Cv5ProcessFrames(bitBmp, dimension, customConvert, GetInitialFrameIndex(frameCount, frameDivider, 1), GetMaximumFrameIndex(frameCount, frameDivider, 1));
            // position 4
            Cv5ProcessFrames(bitBmp, dimension, customConvert, GetInitialFrameIndex(frameCount, frameDivider, 6), GetMaximumFrameIndex(frameCount, frameDivider, 6));
        }

        private static int GetInitialFrameIndex(int frameCount, int frameDivider, int position)
        {
            return frameCount / frameDivider * position;
        }

        private static int GetMaximumFrameIndex(int frameCount, int frameDivider, int position)
        {
            return frameCount / frameDivider * (position + 1);
        }

        private static void Cv5ProcessFrames(Bitmap[] bitBmp, FrameDimension dimension, Color customConvert, int initialFrameIndex, int maximumFrameIndex)
        {
            int top = 0;
            int bottom = 0;
            int left = 0;
            int right = 0;

            int regressT = -1;
            int regressB = -1;
            int regressL = -1;
            int regressR = -1;

            bool var = true;
            bool breakOk = false;

            // Top
            for (int yf = 0; yf < bitBmp[initialFrameIndex].Height; yf++)
            {
                for (int frameIdx = initialFrameIndex; frameIdx < maximumFrameIndex; frameIdx++)
                {
                    bitBmp[frameIdx].SelectActiveFrame(dimension, frameIdx);
                    for (int xf = 0; xf < bitBmp[initialFrameIndex].Width; xf++)
                    {
                        Color pixel = bitBmp[frameIdx].GetPixel(xf, yf);
                        if (pixel == _whiteConvert || pixel == _greyConvert || pixel == customConvert || pixel.A == 0)
                        {
                            var = true;
                        }

                        if (pixel != _whiteConvert && pixel != _greyConvert && pixel != customConvert && pixel.A != 0)
                        {
                            var = false;
                            if (yf != 0)
                            {
                                regressT++;
                                yf--;
                                xf = -1;
                                frameIdx = initialFrameIndex;
                            }
                            else
                            {
                                breakOk = true;
                                break;
                            }
                        }

                        if (var && xf == bitBmp[frameIdx].Width - 1 && frameIdx == maximumFrameIndex - 1 && regressT == -1 &&
                            yf < bitBmp[0].Height - 9)
                        {
                            top += 10;
                        }

                        if (var && xf == bitBmp[frameIdx].Width - 1 && frameIdx == maximumFrameIndex - 1 && regressT == -1 &&
                            yf >= bitBmp[0].Height - 9)
                        {
                            top++;
                        }

                        if (var && xf == bitBmp[frameIdx].Width - 1 && frameIdx == maximumFrameIndex - 1 && regressT != -1)
                        {
                            top -= regressT;
                            breakOk = true;
                            break;
                        }
                    }

                    if (breakOk)
                    {
                        break;
                    }
                }

                if (yf < bitBmp[initialFrameIndex].Height - 9)
                {
                    yf += 9; // 1 of for + 9
                }

                if (breakOk)
                {
                    breakOk = false;
                    break;
                }
            }

            // Bottom
            for (int yf = bitBmp[initialFrameIndex].Height - 1; yf > 0; yf--)
            {
                for (int frameIdx = initialFrameIndex; frameIdx < maximumFrameIndex; frameIdx++)
                {
                    bitBmp[frameIdx].SelectActiveFrame(dimension, frameIdx);
                    for (int xf = 0; xf < bitBmp[initialFrameIndex].Width; xf++)
                    {
                        Color pixel = bitBmp[frameIdx].GetPixel(xf, yf);
                        if (pixel == _whiteConvert || pixel == _greyConvert || pixel == customConvert || pixel.A == 0)
                        {
                            var = true;
                        }

                        if (pixel != _whiteConvert && pixel != _greyConvert && pixel != customConvert && pixel.A != 0)
                        {
                            var = false;
                            if (yf != bitBmp[initialFrameIndex].Height - 1)
                            {
                                regressB++;
                                yf++;
                                xf = -1;
                                frameIdx = initialFrameIndex;
                            }
                            else
                            {
                                breakOk = true;
                                break;
                            }
                        }

                        if (var && xf == bitBmp[frameIdx].Width - 1 && frameIdx == maximumFrameIndex - 1 && regressB == -1 &&
                            yf > 9)
                        {
                            bottom += 10;
                        }

                        if (var && xf == bitBmp[frameIdx].Width - 1 && frameIdx == maximumFrameIndex - 1 && regressB == -1 &&
                            yf <= 9)
                        {
                            bottom++;
                        }

                        if (var && xf == bitBmp[frameIdx].Width - 1 && frameIdx == maximumFrameIndex - 1 && regressB != -1)
                        {
                            bottom -= regressB;
                            breakOk = true;
                            break;
                        }
                    }

                    if (breakOk)
                    {
                        break;
                    }
                }

                if (yf > 9)
                {
                    yf -= 9; // 1 of for + 9
                }

                if (breakOk)
                {
                    breakOk = false;
                    break;
                }
            }

            // Left
            for (int xf = 0; xf < bitBmp[initialFrameIndex].Width; xf++)
            {
                for (int frameIdx = initialFrameIndex; frameIdx < maximumFrameIndex; frameIdx++)
                {
                    bitBmp[frameIdx].SelectActiveFrame(dimension, frameIdx);
                    for (int yf = 0; yf < bitBmp[initialFrameIndex].Height; yf++)
                    {
                        Color pixel = bitBmp[frameIdx].GetPixel(xf, yf);
                        if (pixel == _whiteConvert || pixel == _greyConvert || pixel == customConvert || pixel.A == 0)
                        {
                            var = true;
                        }

                        if (pixel != _whiteConvert && pixel != _greyConvert && pixel != customConvert && pixel.A != 0)
                        {
                            var = false;
                            if (xf != 0)
                            {
                                regressL++;
                                xf--;
                                yf = -1;
                                frameIdx = initialFrameIndex;
                            }
                            else
                            {
                                breakOk = true;
                                break;
                            }
                        }

                        if (var && yf == bitBmp[frameIdx].Height - 1 && frameIdx == maximumFrameIndex - 1 &&
                            regressL == -1 &&
                            xf < bitBmp[0].Width - 9)
                        {
                            left += 10;
                        }

                        if (var && yf == bitBmp[frameIdx].Height - 1 && frameIdx == maximumFrameIndex - 1 &&
                            regressL == -1 &&
                            xf >= bitBmp[0].Width - 9)
                        {
                            left++;
                        }

                        if (var && yf == bitBmp[frameIdx].Height - 1 && frameIdx == maximumFrameIndex - 1 && regressL != -1)
                        {
                            left -= regressL;
                            breakOk = true;
                            break;
                        }
                    }

                    if (breakOk)
                    {
                        break;
                    }
                }

                if (xf < bitBmp[initialFrameIndex].Width - 9)
                {
                    xf += 9; // 1 of for + 9
                }

                if (breakOk)
                {
                    breakOk = false;
                    break;
                }
            }

            // Right
            for (int xf = bitBmp[initialFrameIndex].Width - 1; xf > 0; xf--)
            {
                for (int frameIdx = initialFrameIndex; frameIdx < maximumFrameIndex; frameIdx++)
                {
                    bitBmp[frameIdx].SelectActiveFrame(dimension, frameIdx);
                    for (int yf = 0; yf < bitBmp[initialFrameIndex].Height; yf++)
                    {
                        Color pixel = bitBmp[frameIdx].GetPixel(xf, yf);
                        if (pixel == _whiteConvert || pixel == _greyConvert || pixel == customConvert || pixel.A == 0)
                        {
                            var = true;
                        }

                        if (pixel != _whiteConvert && pixel != _greyConvert && pixel != customConvert && pixel.A != 0)
                        {
                            var = false;
                            if (xf != bitBmp[initialFrameIndex].Width - 1)
                            {
                                regressR++;
                                xf++;
                                yf = -1;
                                frameIdx = initialFrameIndex;
                            }
                            else
                            {
                                breakOk = true;
                                break;
                            }
                        }

                        if (var && yf == bitBmp[frameIdx].Height - 1 && frameIdx == maximumFrameIndex - 1 &&
                            regressR == -1 &&
                            xf > 9)
                        {
                            right += 10;
                        }

                        if (var && yf == bitBmp[frameIdx].Height - 1 && frameIdx == maximumFrameIndex - 1 &&
                            regressR == -1 &&
                            xf <= 9)
                        {
                            right++;
                        }

                        if (var && yf == bitBmp[frameIdx].Height - 1 && frameIdx == maximumFrameIndex - 1 && regressR != -1)
                        {
                            right -= regressR;
                            breakOk = true;
                            break;
                        }
                    }

                    if (breakOk)
                    {
                        break;
                    }
                }

                if (xf > 9)
                {
                    xf -= 9; // 1 of for + 9
                }

                if (breakOk)
                {
                    //breakOk = false;
                    break;
                }
            }

            for (int index = initialFrameIndex; index < maximumFrameIndex; index++)
            {
                Rectangle rect = new Rectangle(left, top, bitBmp[index].Width - left - right, bitBmp[index].Height - top - bottom);
                bitBmp[index].SelectActiveFrame(dimension, index);
                bitBmp[index] = bitBmp[index].Clone(rect, PixelFormat.Format16bppArgb1555);
            }
        }

        private void CbLockColorControls_CheckedChanged(object sender, EventArgs e)
        {
            if (!LockColorControlsCheckBox.Checked)
            {
                numericUpDownRed.Enabled = true;
                numericUpDownGreen.Enabled = true;
                numericUpDownBlue.Enabled = true;
            }
            else
            {
                numericUpDownRed.Enabled = false;
                numericUpDownGreen.Enabled = false;
                numericUpDownBlue.Enabled = false;

                numericUpDownRed.Value = 255;
                numericUpDownGreen.Value = 255;
                numericUpDownBlue.Value = 255;
            }
        }

        // All directions Add KRFrameViewer
        private void AllDirectionsAddWithCanvasKRFrameEditorColorCorrectorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                if (_fileType != 0)
                {
                    using (OpenFileDialog dialog = new OpenFileDialog())
                    {
                        dialog.Multiselect = false;
                        dialog.Title = "Choose 1 Gif ( with all directions in KRFrameViewer Style ) to add";
                        dialog.CheckFileExists = true;
                        dialog.Filter = "Gif files (*.gif;)|*.gif;";

                        if (dialog.ShowDialog() == DialogResult.OK)
                        {
                            Color customConvert = Color.FromArgb(255, (int)numericUpDownRed.Value, (int)numericUpDownGreen.Value, (int)numericUpDownBlue.Value);

                            DirectionTrackBar.Enabled = false;
                            DirectionTrackBar.Value = 0;

                            AnimIdx edit = AnimationEdit.GetAnimation(_fileType, _currentBody, _currentAction, _currentDir);

                            if (edit != null)
                            {
                                //Gif Especial Properties
                                if (dialog.FileName.Contains(".gif"))
                                {
                                    using (Bitmap bmpTemp = new Bitmap(dialog.FileName))
                                    using (MemoryStream ms = new MemoryStream())
                                    {
                                        bmpTemp.Save(ms, ImageFormat.Gif);
                                        Bitmap bitmap = new Bitmap(ms);

                                        FrameDimension dimension = new FrameDimension(bitmap.FrameDimensionsList[0]);

                                        // Number of frames
                                        int frameCount = bitmap.GetFrameCount(dimension);

                                        ProgressBar.Maximum = frameCount;

                                        bitmap.SelectActiveFrame(dimension, 0);

                                        UpdateGifPalette(bitmap, edit);

                                        Bitmap[] bitBmp = new Bitmap[frameCount];

                                        // Return an Image at a certain index
                                        for (int index = 0; index < frameCount; index++)
                                        {
                                            bitBmp[index] = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format16bppArgb1555);
                                            bitmap.SelectActiveFrame(dimension, index);
                                            bitBmp[index] = bitmap;
                                        }

                                        KrCanvasAlgorithm(bitBmp, frameCount, dimension, customConvert);

                                        edit = KrAnimIdxPositions(frameCount, bitBmp, dimension, edit, bitmap);

                                        ProgressBar.Value = 0;
                                        ProgressBar.Invalidate();

                                        SetPaletteBox();
                                        FramesListView.Invalidate();

                                        CenterXNumericUpDown.Value = edit.Frames[FramesTrackBar.Value].Center.X;
                                        CenterYNumericUpDown.Value = edit.Frames[FramesTrackBar.Value].Center.Y;

                                        Options.ChangedUltimaClass["Animations"] = true;
                                    }
                                }
                            }

                            DirectionTrackBar.Enabled = true;
                        }
                    }
                }

                // Refresh List after Canvas reduction
                _currentDir = DirectionTrackBar.Value;
                AfterSelectTreeView(null, null);
            }
            catch (NullReferenceException)
            {
                // TODO: add logging or fix?
                DirectionTrackBar.Enabled = true;
            }
        }

        private AnimIdx KrAnimIdxPositions(int frameCount, Bitmap[] bitBmp, FrameDimension dimension, AnimIdx edit, Bitmap bmp)
        {
            // position 0
            for (int index = frameCount / 5 * 0; index < frameCount / 5 * 1; index++)
            {
                bitBmp[index].SelectActiveFrame(dimension, index);
                bitBmp[index] = ConvertBmpAnimKr(bitBmp[index], (int) numericUpDownRed.Value, (int) numericUpDownGreen.Value,
                    (int) numericUpDownBlue.Value);
                edit.AddFrame(bitBmp[index], bitBmp[index].Width/2);
                TreeNode node = GetNode(_currentBody);
                if (node != null)
                {
                    node.ForeColor = Color.Black;
                    node.Nodes[_currentAction].ForeColor = Color.Black;
                }

                int i = edit.Frames.Count - 1;
                var item = new ListViewItem(i.ToString(), 0)
                {
                    Tag = i
                };
                FramesListView.Items.Add(item);
                int width = FramesListView.TileSize.Width - 5;
                if (bmp.Width > FramesListView.TileSize.Width)
                {
                    width = bmp.Width;
                }

                int height = FramesListView.TileSize.Height - 5;
                if (bmp.Height > FramesListView.TileSize.Height)
                {
                    height = bmp.Height;
                }

                FramesListView.TileSize = new Size(width + 5, height + 5);
                FramesTrackBar.Maximum = i;
                Options.ChangedUltimaClass["Animations"] = true;
                if (ProgressBar.Value < ProgressBar.Maximum)
                {
                    ProgressBar.Value++;
                    ProgressBar.Invalidate();
                }

                if (index == (frameCount / 5 * 1) - 1)
                {
                    DirectionTrackBar.Value++;
                }
            }

            // position 1
            edit = AnimationEdit.GetAnimation(_fileType, _currentBody, _currentAction, _currentDir);
            bmp.SelectActiveFrame(dimension, 0);
            UpdateGifPalette(bmp, edit);
            for (int index = frameCount / 5 * 1; index < frameCount / 5 * 2; index++)
            {
                bitBmp[index].SelectActiveFrame(dimension, index);
                bitBmp[index] = ConvertBmpAnimKr(bitBmp[index], (int) numericUpDownRed.Value, (int) numericUpDownGreen.Value,
                    (int) numericUpDownBlue.Value);
                edit.AddFrame(bitBmp[index], bitBmp[index].Width/2);
                TreeNode node = GetNode(_currentBody);
                if (node != null)
                {
                    node.ForeColor = Color.Black;
                    node.Nodes[_currentAction].ForeColor = Color.Black;
                }

                int i = edit.Frames.Count - 1;
                var item = new ListViewItem(i.ToString(), 0)
                {
                    Tag = i
                };
                FramesListView.Items.Add(item);
                int width = FramesListView.TileSize.Width - 5;
                if (bmp.Width > FramesListView.TileSize.Width)
                {
                    width = bmp.Width;
                }

                int height = FramesListView.TileSize.Height - 5;
                if (bmp.Height > FramesListView.TileSize.Height)
                {
                    height = bmp.Height;
                }

                FramesListView.TileSize = new Size(width + 5, height + 5);
                FramesTrackBar.Maximum = i;
                Options.ChangedUltimaClass["Animations"] = true;
                if (ProgressBar.Value < ProgressBar.Maximum)
                {
                    ProgressBar.Value++;
                    ProgressBar.Invalidate();
                }

                if (index == (frameCount / 5 * 2) - 1)
                {
                    DirectionTrackBar.Value++;
                }
            }

            // position 2
            edit = AnimationEdit.GetAnimation(_fileType, _currentBody, _currentAction, _currentDir);
            bmp.SelectActiveFrame(dimension, 0);
            UpdateGifPalette(bmp, edit);
            for (int index = frameCount / 5 * 2; index < frameCount / 5 * 3; index++)
            {
                bitBmp[index].SelectActiveFrame(dimension, index);
                bitBmp[index] = ConvertBmpAnimKr(bitBmp[index], (int) numericUpDownRed.Value, (int) numericUpDownGreen.Value,
                    (int) numericUpDownBlue.Value);
                edit.AddFrame(bitBmp[index], bitBmp[index].Width/2);
                TreeNode node = GetNode(_currentBody);
                if (node != null)
                {
                    node.ForeColor = Color.Black;
                    node.Nodes[_currentAction].ForeColor = Color.Black;
                }

                int i = edit.Frames.Count - 1;
                var item = new ListViewItem(i.ToString(), 0)
                {
                    Tag = i
                };
                FramesListView.Items.Add(item);
                int width = FramesListView.TileSize.Width - 5;
                if (bmp.Width > FramesListView.TileSize.Width)
                {
                    width = bmp.Width;
                }

                int height = FramesListView.TileSize.Height - 5;
                if (bmp.Height > FramesListView.TileSize.Height)
                {
                    height = bmp.Height;
                }

                FramesListView.TileSize = new Size(width + 5, height + 5);
                FramesTrackBar.Maximum = i;
                Options.ChangedUltimaClass["Animations"] = true;
                if (ProgressBar.Value < ProgressBar.Maximum)
                {
                    ProgressBar.Value++;
                    ProgressBar.Invalidate();
                }

                if (index == (frameCount / 5 * 3) - 1)
                {
                    DirectionTrackBar.Value++;
                }
            }

            // position 3
            edit = AnimationEdit.GetAnimation(_fileType, _currentBody, _currentAction, _currentDir);
            bmp.SelectActiveFrame(dimension, 0);
            UpdateGifPalette(bmp, edit);
            for (int index = frameCount / 5 * 3; index < frameCount / 5 * 4; index++)
            {
                bitBmp[index].SelectActiveFrame(dimension, index);
                bitBmp[index] = ConvertBmpAnimKr(bitBmp[index], (int) numericUpDownRed.Value, (int) numericUpDownGreen.Value,
                    (int) numericUpDownBlue.Value);
                edit.AddFrame(bitBmp[index], bitBmp[index].Width/2);
                TreeNode node = GetNode(_currentBody);
                if (node != null)
                {
                    node.ForeColor = Color.Black;
                    node.Nodes[_currentAction].ForeColor = Color.Black;
                }

                int i = edit.Frames.Count - 1;
                var item = new ListViewItem(i.ToString(), 0)
                {
                    Tag = i
                };
                FramesListView.Items.Add(item);
                int width = FramesListView.TileSize.Width - 5;
                if (bmp.Width > FramesListView.TileSize.Width)
                {
                    width = bmp.Width;
                }

                int height = FramesListView.TileSize.Height - 5;
                if (bmp.Height > FramesListView.TileSize.Height)
                {
                    height = bmp.Height;
                }

                FramesListView.TileSize = new Size(width + 5, height + 5);
                FramesTrackBar.Maximum = i;
                Options.ChangedUltimaClass["Animations"] = true;
                if (ProgressBar.Value < ProgressBar.Maximum)
                {
                    ProgressBar.Value++;
                    ProgressBar.Invalidate();
                }

                if (index == (frameCount / 5 * 4) - 1)
                {
                    DirectionTrackBar.Value++;
                }
            }

            // position 4
            edit = AnimationEdit.GetAnimation(_fileType, _currentBody, _currentAction, _currentDir);
            bmp.SelectActiveFrame(dimension, 0);
            UpdateGifPalette(bmp, edit);
            for (int index = frameCount / 5 * 4; index < frameCount / 5 * 5; index++)
            {
                bitBmp[index].SelectActiveFrame(dimension, index);
                bitBmp[index] = ConvertBmpAnimKr(bitBmp[index], (int) numericUpDownRed.Value, (int) numericUpDownGreen.Value,
                    (int) numericUpDownBlue.Value);
                edit.AddFrame(bitBmp[index], bitBmp[index].Width/2);
                TreeNode node = GetNode(_currentBody);
                if (node != null)
                {
                    node.ForeColor = Color.Black;
                    node.Nodes[_currentAction].ForeColor = Color.Black;
                }

                int i = edit.Frames.Count - 1;
                var item = new ListViewItem(i.ToString(), 0)
                {
                    Tag = i
                };
                FramesListView.Items.Add(item);
                int width = FramesListView.TileSize.Width - 5;
                if (bmp.Width > FramesListView.TileSize.Width)
                {
                    width = bmp.Width;
                }

                int height = FramesListView.TileSize.Height - 5;
                if (bmp.Height > FramesListView.TileSize.Height)
                {
                    height = bmp.Height;
                }

                FramesListView.TileSize = new Size(width + 5, height + 5);
                FramesTrackBar.Maximum = i;
                Options.ChangedUltimaClass["Animations"] = true;
                if (ProgressBar.Value < ProgressBar.Maximum)
                {
                    ProgressBar.Value++;
                    ProgressBar.Invalidate();
                }
            }

            return edit;
        }

        private static unsafe Bitmap ConvertBmpAnimKr(Bitmap bmp, int red, int green, int blue)
        {
            // Extra background
            int extraBack = (red / 8 * 1024) + (green / 8 * 32) + (blue / 8) + 32768;

            BitmapData bd = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format16bppArgb1555);
            ushort* line = (ushort*)bd.Scan0;
            int delta = bd.Stride >> 1;

            Bitmap bmpNew = new Bitmap(bmp.Width, bmp.Height, PixelFormat.Format16bppArgb1555);
            BitmapData bdNew = bmpNew.LockBits(new Rectangle(0, 0, bmpNew.Width, bmpNew.Height), ImageLockMode.WriteOnly, PixelFormat.Format16bppArgb1555);

            ushort* lineNew = (ushort*)bdNew.Scan0;
            int deltaNew = bdNew.Stride >> 1;

            for (int y = 0; y < bmp.Height; ++y, line += delta, lineNew += deltaNew)
            {
                ushort* cur = line;
                ushort* curNew = lineNew;
                for (int x = 0; x < bmp.Width; ++x)
                {
                    //if (cur[X] != 53235)
                    //{
                    // Convert back to RGB
                    int blueTemp = (cur[x] - 32768) / 32;
                    blueTemp *= 32;
                    blueTemp = cur[x] - 32768 - blueTemp;

                    int greenTemp = (cur[x] - 32768) / 1024;
                    greenTemp *= 1024;
                    greenTemp = cur[x] - 32768 - greenTemp - blueTemp;
                    greenTemp /= 32;

                    int redTemp = (cur[x] - 32768) / 1024;

                    // remove green colors
                    if (greenTemp > blueTemp && greenTemp > redTemp && greenTemp > 10)
                    {
                        cur[x] = 65535;
                    }
                    //}

                    //My Soulblighter Modification
                    // Convert color 0,0,0 to 0,0,8
                    if (cur[x] == 32768)
                    {
                        curNew[x] = 32769;
                    }

                    if (cur[x] != 65535 && cur[x] != 54965 && cur[x] != extraBack && cur[x] > 32768) //True White == BackGround
                    {
                        curNew[x] = cur[x];
                    }
                    //End of Soulblighter Modification
                }
            }
            bmp.UnlockBits(bd);
            bmpNew.UnlockBits(bdNew);
            return bmpNew;
        }

        private static void KrCanvasAlgorithm(Bitmap[] bitBmp, int frameCount, FrameDimension dimension, Color customConvert)
        {
            /*
             * TODO: both methods needs better names.
             *
             *      Duplication here was huge. Now it is reduced to one method with parameter.
             *      It still needs further reducing.
             *      It may be possible to merge code with CV5 routines.
             */
            // TODO: Needs better names
            // TODO: This code needs documentation. This algorithm is not really readable

            // Order of calls looks important
            // Looks like it is import for Gif/bmps from KR client format
            const int frameDivider = 5;

            KrProcessFrames(bitBmp, dimension, customConvert, GetInitialFrameIndex(frameCount, frameDivider, 0), GetMaximumFrameIndex(frameCount, frameDivider, 0));
            KrProcessFrames(bitBmp, dimension, customConvert, GetInitialFrameIndex(frameCount, frameDivider, 1), GetMaximumFrameIndex(frameCount, frameDivider, 1));
            KrProcessFrames(bitBmp, dimension, customConvert, GetInitialFrameIndex(frameCount, frameDivider, 2), GetMaximumFrameIndex(frameCount, frameDivider, 2));
            KrProcessFrames(bitBmp, dimension, customConvert, GetInitialFrameIndex(frameCount, frameDivider, 3), GetMaximumFrameIndex(frameCount, frameDivider, 3));
            KrProcessFrames(bitBmp, dimension, customConvert, GetInitialFrameIndex(frameCount, frameDivider, 4), GetMaximumFrameIndex(frameCount, frameDivider, 4));
        }

        private static void KrProcessFrames(Bitmap[] bitBmp, FrameDimension dimension, Color customConvert, int initialFrameIndex, int maximumFrameIndex)
        {
            int top = 0;
            int bottom = 0;
            int left = 0;
            int right = 0;

            int regressT = -1;
            int regressB = -1;
            int regressL = -1;
            int regressR = -1;

            bool var = true;
            bool breakOk = false;

            // Top
            for (int yf = 0; yf < bitBmp[initialFrameIndex].Height; yf++)
            {
                for (int frameIdx = initialFrameIndex; frameIdx < maximumFrameIndex; frameIdx++)
                {
                    bitBmp[frameIdx].SelectActiveFrame(dimension, frameIdx);
                    for (int xf = 0; xf < bitBmp[initialFrameIndex].Width; xf++)
                    {
                        Color pixel = bitBmp[frameIdx].GetPixel(xf, yf);
                        if (pixel == _whiteConvert || pixel == customConvert || pixel.A == 0)
                        {
                            var = true;
                        }

                        if (pixel != _whiteConvert && pixel != customConvert && pixel.A != 0)
                        {
                            var = false;
                            if (yf != 0)
                            {
                                regressT++;
                                yf--;
                                xf = -1;
                                frameIdx = initialFrameIndex;
                            }
                            else
                            {
                                breakOk = true;
                                break;
                            }
                        }

                        if (var && xf == bitBmp[frameIdx].Width - 1 && frameIdx == maximumFrameIndex - 1 && regressT == -1 &&
                            yf < bitBmp[0].Height - 9)
                        {
                            top += 10;
                        }

                        if (var && xf == bitBmp[frameIdx].Width - 1 && frameIdx == maximumFrameIndex - 1 && regressT == -1 &&
                            yf >= bitBmp[0].Height - 9)
                        {
                            top++;
                        }

                        if (var && xf == bitBmp[frameIdx].Width - 1 && frameIdx == maximumFrameIndex - 1 && regressT != -1)
                        {
                            top -= regressT;
                            breakOk = true;
                            break;
                        }
                    }

                    if (breakOk)
                    {
                        break;
                    }
                }

                if (yf < bitBmp[initialFrameIndex].Height - 9)
                {
                    yf += 9; // 1 of for + 9
                }

                if (breakOk)
                {
                    breakOk = false;
                    break;
                }
            }

            // Bottom
            for (int yf = bitBmp[initialFrameIndex].Height - 1; yf > 0; yf--)
            {
                for (int frameIdx = initialFrameIndex; frameIdx < maximumFrameIndex; frameIdx++)
                {
                    bitBmp[frameIdx].SelectActiveFrame(dimension, frameIdx);
                    for (int xf = 0; xf < bitBmp[initialFrameIndex].Width; xf++)
                    {
                        Color pixel = bitBmp[frameIdx].GetPixel(xf, yf);
                        if (pixel == _whiteConvert || pixel == customConvert || pixel.A == 0)
                        {
                            var = true;
                        }

                        if (pixel != _whiteConvert && pixel != customConvert && pixel.A != 0)
                        {
                            var = false;
                            if (yf != bitBmp[initialFrameIndex].Height - 1)
                            {
                                regressB++;
                                yf++;
                                xf = -1;
                                frameIdx = initialFrameIndex;
                            }
                            else
                            {
                                breakOk = true;
                                break;
                            }
                        }

                        if (var && xf == bitBmp[frameIdx].Width - 1 && frameIdx == maximumFrameIndex - 1 && regressB == -1 &&
                            yf > 9)
                        {
                            bottom += 10;
                        }

                        if (var && xf == bitBmp[frameIdx].Width - 1 && frameIdx == maximumFrameIndex - 1 && regressB == -1 &&
                            yf <= 9)
                        {
                            bottom++;
                        }

                        if (var && xf == bitBmp[frameIdx].Width - 1 && frameIdx == maximumFrameIndex - 1 && regressB != -1)
                        {
                            bottom -= regressB;
                            breakOk = true;
                            break;
                        }
                    }

                    if (breakOk)
                    {
                        break;
                    }
                }

                if (yf > 9)
                {
                    yf -= 9; // 1 of for + 9
                }

                if (breakOk)
                {
                    breakOk = false;
                    break;
                }
            }

            // Left
            for (int xf = 0; xf < bitBmp[initialFrameIndex].Width; xf++)
            {
                for (int frameIdx = initialFrameIndex; frameIdx < maximumFrameIndex; frameIdx++)
                {
                    bitBmp[frameIdx].SelectActiveFrame(dimension, frameIdx);
                    for (int yf = 0; yf < bitBmp[initialFrameIndex].Height; yf++)
                    {
                        Color pixel = bitBmp[frameIdx].GetPixel(xf, yf);
                        if (pixel == _whiteConvert || pixel == customConvert || pixel.A == 0)
                        {
                            var = true;
                        }

                        if (pixel != _whiteConvert && pixel != customConvert && pixel.A != 0)
                        {
                            var = false;
                            if (xf != 0)
                            {
                                regressL++;
                                xf--;
                                yf = -1;
                                frameIdx = initialFrameIndex;
                            }
                            else
                            {
                                breakOk = true;
                                break;
                            }
                        }

                        if (var && yf == bitBmp[frameIdx].Height - 1 && frameIdx == maximumFrameIndex - 1 &&
                            regressL == -1 &&
                            xf < bitBmp[0].Width - 9)
                        {
                            left += 10;
                        }

                        if (var && yf == bitBmp[frameIdx].Height - 1 && frameIdx == maximumFrameIndex - 1 &&
                            regressL == -1 &&
                            xf >= bitBmp[0].Width - 9)
                        {
                            left++;
                        }

                        if (var && yf == bitBmp[frameIdx].Height - 1 && frameIdx == maximumFrameIndex - 1 && regressL != -1)
                        {
                            left -= regressL;
                            breakOk = true;
                            break;
                        }
                    }

                    if (breakOk)
                    {
                        break;
                    }
                }

                if (xf < bitBmp[initialFrameIndex].Width - 9)
                {
                    xf += 9; // 1 of for + 9
                }

                if (breakOk)
                {
                    breakOk = false;
                    break;
                }
            }

            // Right
            for (int xf = bitBmp[initialFrameIndex].Width - 1; xf > 0; xf--)
            {
                for (int frameIdx = initialFrameIndex; frameIdx < maximumFrameIndex; frameIdx++)
                {
                    bitBmp[frameIdx].SelectActiveFrame(dimension, frameIdx);
                    for (int yf = 0; yf < bitBmp[initialFrameIndex].Height; yf++)
                    {
                        Color pixel = bitBmp[frameIdx].GetPixel(xf, yf);
                        if (pixel == _whiteConvert || pixel == customConvert || pixel.A == 0)
                        {
                            var = true;
                        }

                        if (pixel != _whiteConvert && pixel != customConvert && pixel.A != 0)
                        {
                            var = false;
                            if (xf != bitBmp[initialFrameIndex].Width - 1)
                            {
                                regressR++;
                                xf++;
                                yf = -1;
                                frameIdx = initialFrameIndex;
                            }
                            else
                            {
                                breakOk = true;
                                break;
                            }
                        }

                        if (var && yf == bitBmp[frameIdx].Height - 1 && frameIdx == maximumFrameIndex - 1 &&
                            regressR == -1 &&
                            xf > 9)
                        {
                            right += 10;
                        }

                        if (var && yf == bitBmp[frameIdx].Height - 1 && frameIdx == maximumFrameIndex - 1 &&
                            regressR == -1 &&
                            xf <= 9)
                        {
                            right++;
                        }

                        if (var && yf == bitBmp[frameIdx].Height - 1 && frameIdx == maximumFrameIndex - 1 && regressR != -1)
                        {
                            right -= regressR;
                            breakOk = true;
                            break;
                        }
                    }

                    if (breakOk)
                    {
                        break;
                    }
                }

                if (xf > 9)
                {
                    xf -= 9; // 1 of for + 9
                }

                if (breakOk)
                {
                    //breakOk = false;
                    break;
                }
            }

            for (int index = initialFrameIndex; index < maximumFrameIndex; index++)
            {
                Rectangle rect = new Rectangle(left, top, bitBmp[index].Width - left - right, bitBmp[index].Height - top - bottom);
                bitBmp[index].SelectActiveFrame(dimension, index);
                bitBmp[index] = bitBmp[index].Clone(rect, PixelFormat.Format16bppArgb1555);
            }
        }

        private void SetPaletteButton_Click(object sender, EventArgs e)
        {
            for (int x = 0; x < 5; x++)
            {
                // RGB
                if (rbRGB.Checked)
                {
                    AnimIdx edit = AnimationEdit.GetAnimation(_fileType, _currentBody, _currentAction, _currentDir);
                    PaletteConverter(1, edit);
                }

                // RBG
                if (rbRBG.Checked)
                {
                    AnimIdx edit = AnimationEdit.GetAnimation(_fileType, _currentBody, _currentAction, _currentDir);
                    PaletteConverter(2, edit);
                }

                // GRB
                if (rbGRB.Checked)
                {
                    AnimIdx edit = AnimationEdit.GetAnimation(_fileType, _currentBody, _currentAction, _currentDir);
                    PaletteConverter(3, edit);
                }

                // GBR
                if (rbGBR.Checked)
                {
                    AnimIdx edit = AnimationEdit.GetAnimation(_fileType, _currentBody, _currentAction, _currentDir);
                    PaletteConverter(4, edit);
                }

                // BGR
                if (rbBGR.Checked)
                {
                    AnimIdx edit = AnimationEdit.GetAnimation(_fileType, _currentBody, _currentAction, _currentDir);
                    PaletteConverter(5, edit);
                }

                // BRG
                if (rbBRG.Checked)
                {
                    AnimIdx edit = AnimationEdit.GetAnimation(_fileType, _currentBody, _currentAction, _currentDir);
                    PaletteConverter(6, edit);
                }

                SetPaletteBox();
                FramesListView.Invalidate();
                Options.ChangedUltimaClass["Animations"] = true;
                SetPaletteBox();
                if (DirectionTrackBar.Value != DirectionTrackBar.Maximum)
                {
                    DirectionTrackBar.Value++;
                }
                else
                {
                    DirectionTrackBar.Value = 0;
                }
            }
        }

        // TODO: check why there is no RadioButton1_CheckedChanged event for selector 1?

        private void RadioButton2_CheckedChanged(object sender, EventArgs e)
        {
            ConvertAndSetPalette(2);
        }

        private void RadioButton3_CheckedChanged(object sender, EventArgs e)
        {
            ConvertAndSetPalette(3);
        }

        private void RadioButton4_CheckedChanged(object sender, EventArgs e)
        {
            ConvertAndSetPalette(4);
        }

        private void RadioButton5_CheckedChanged(object sender, EventArgs e)
        {
            ConvertAndSetPalette(5);
        }

        private void RadioButton6_CheckedChanged(object sender, EventArgs e)
        {
            ConvertAndSetPalette(6);
        }

        private void ApplyButton_Click(object sender, EventArgs e)
        {
            ConvertAndSetPaletteWithReducer();
        }

        private void ConvertAndSetPaletteWithReducer()
        {
            // TODO: except calling reducer here the whole logic is the same as in ConvertAndSetPalette()
            for (int x = 0; x < 5; x++)
            {
                AnimIdx edit = AnimationEdit.GetAnimation(_fileType, _currentBody, _currentAction, _currentDir);
                PaletteReducer((int) numericUpDown6.Value, (int) numericUpDown7.Value, (int) numericUpDown8.Value, edit);
                SetPaletteBox();
                FramesListView.Invalidate();
                Options.ChangedUltimaClass["Animations"] = true;
                SetPaletteBox();
                if (DirectionTrackBar.Value != DirectionTrackBar.Maximum)
                {
                    DirectionTrackBar.Value++;
                }
                else
                {
                    DirectionTrackBar.Value = 0;
                }
            }
        }

        private void ConvertAndSetPalette(int selector)
        {
            for (int x = 0; x < 5; x++)
            {
                AnimIdx edit = AnimationEdit.GetAnimation(_fileType, _currentBody, _currentAction, _currentDir);
                PaletteConverter(selector, edit);
                SetPaletteBox();
                FramesListView.Invalidate();
                Options.ChangedUltimaClass["Animations"] = true;
                SetPaletteBox();
                if (DirectionTrackBar.Value != DirectionTrackBar.Maximum)
                {
                    DirectionTrackBar.Value++;
                }
                else
                {
                    DirectionTrackBar.Value = 0;
                }
            }
        }

        public void UpdateGifPalette(Bitmap bit, AnimIdx animIdx)
        {
            using (MemoryStream imageStreamSource = new MemoryStream())
            {
                ImageConverter ic = new ImageConverter();
                byte[] btImage = (byte[])ic.ConvertTo(bit, typeof(byte[]));
                imageStreamSource.Write(btImage, 0, btImage.Length);
                GifBitmapDecoder decoder = new GifBitmapDecoder(imageStreamSource, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default);
                BitmapPalette pal = decoder.Palette;
                int i;
                for (i = 0; i < Animations.PaletteCapacity; i++)
                {
                    animIdx.Palette[i] = 0;
                }

                try
                {
                    i = 0;
                    while (i < Animations.PaletteCapacity) //&& i < pal.Colors.Count)
                    {
                        int red = pal.Colors[i].R / 8;
                        int green = pal.Colors[i].G / 8;
                        int blue = pal.Colors[i].B / 8;

                        int contaFinal = (0x400 * red) + (0x20 * green) + blue + 0x8000;
                        if (contaFinal == 0x8000)
                        {
                            contaFinal = 0x8001;
                        }

                        animIdx.Palette[i] = (ushort)contaFinal;
                        i++;
                    }
                }
                catch (IndexOutOfRangeException)
                {
                    // TODO: ignored?
                }
                catch (ArgumentOutOfRangeException)
                {
                    // TODO: ignored?
                }

                for (i = 0; i < Animations.PaletteCapacity; i++)
                {
                    if (animIdx.Palette[i] < 0x8000)
                    {
                        animIdx.Palette[i] = 0x8000;
                    }
                }
            }
        }

        public unsafe void UpdateImagePalette(Bitmap bit, AnimIdx animIdx)
        {
            int count = 0;
            var bmp = new Bitmap(bit);
            BitmapData bd = bmp.LockBits(
                new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format16bppArgb1555);
            var line = (ushort*)bd.Scan0;
            int delta = bd.Stride >> 1;

            int i = 0;
            while (i < Animations.PaletteCapacity)
            {
                animIdx.Palette[i] = 0;
                i++;
            }

            int y = 0;

            while (y < bmp.Height)
            {
                ushort* cur = line;
                for (int x = 0; x < bmp.Width; x++)
                {
                    ushort c = cur[x];
                    if (c == 0)
                    {
                        continue;
                    }

                    bool found = false;
                    i = 0;

                    while (i < animIdx.Palette.Length)
                    {
                        if (animIdx.Palette[i] == c)
                        {
                            found = true;
                            break;
                        }
                        i++;
                    }

                    if (!found)
                    {
                        animIdx.Palette[count++] = c;
                    }

                    if (count >= Animations.PaletteCapacity)
                    {
                        break;
                    }
                }

                for (i = 0; i < Animations.PaletteCapacity; i++)
                {
                    if (animIdx.Palette[i] < 0x8000)
                    {
                        animIdx.Palette[i] = 0x8000;
                    }
                }

                if (count >= Animations.PaletteCapacity)
                {
                    break;
                }

                y++;
                line += delta;
            }
        }

        public void PaletteConverter(int selector, AnimIdx animIdx)
        {
            int i;
            for (i = 0; i < Animations.PaletteCapacity; i++)
            {
                int blueTemp = (animIdx.Palette[i] - 0x8000) / 0x20;
                blueTemp *= 0x20;
                blueTemp = animIdx.Palette[i] - 0x8000 - blueTemp;

                int greenTemp = (animIdx.Palette[i] - 0x8000) / 0x400;
                greenTemp *= 0x400;
                greenTemp = animIdx.Palette[i] - 0x8000 - greenTemp - blueTemp;
                greenTemp /= 0x20;

                int redTemp = (animIdx.Palette[i] - 0x8000) / 0x400;

                int contaFinal = 0;
                switch (selector)
                {
                    case 1:
                        contaFinal = (((0x400 * redTemp) + (0x20 * greenTemp)) + blueTemp) + 0x8000;
                        break;
                    case 2:
                        contaFinal = (((0x400 * redTemp) + (0x20 * blueTemp)) + greenTemp) + 0x8000;
                        break;
                    case 3:
                        contaFinal = (((0x400 * greenTemp) + (0x20 * redTemp)) + blueTemp) + 0x8000;
                        break;
                    case 4:
                        contaFinal = (((0x400 * greenTemp) + (0x20 * blueTemp)) + redTemp) + 0x8000;
                        break;
                    case 5:
                        contaFinal = (((0x400 * blueTemp) + (0x20 * greenTemp)) + redTemp) + 0x8000;
                        break;
                    case 6:
                        contaFinal = (((0x400 * blueTemp) + (0x20 * redTemp)) + greenTemp) + 0x8000;
                        break;
                }

                if (contaFinal == 0x8000)
                {
                    contaFinal = 0x8001;
                }

                animIdx.Palette[i] = (ushort)contaFinal;
            }

            for (i = 0; i < Animations.PaletteCapacity; i++)
            {
                if (animIdx.Palette[i] < 0x8000)
                {
                    animIdx.Palette[i] = 0x8000;
                }
            }
        }

        public void PaletteReducer(int redP, int greenP, int blueP, AnimIdx animIdx)
        {
            int i;
            redP /= 8;
            greenP /= 8;
            blueP /= 8;
            for (i = 0; i < Animations.PaletteCapacity; i++)
            {
                int blueTemp = (animIdx.Palette[i] - 0x8000) / 0x20;
                blueTemp *= 0x20;
                blueTemp = animIdx.Palette[i] - 0x8000 - blueTemp;

                int greenTemp = (animIdx.Palette[i] - 0x8000) / 0x400;
                greenTemp *= 0x400;
                greenTemp = animIdx.Palette[i] - 0x8000 - greenTemp - blueTemp;
                greenTemp /= 0x20;

                int redTemp = (animIdx.Palette[i] - 0x8000) / 0x400;
                redTemp += redP;
                greenTemp += greenP;
                blueTemp += blueP;

                if (redTemp < 0)
                {
                    redTemp = 0;
                }

                if (redTemp > 0x1f)
                {
                    redTemp = 0x1f;
                }

                if (greenTemp < 0)
                {
                    greenTemp = 0;
                }

                if (greenTemp > 0x1f)
                {
                    greenTemp = 0x1f;
                }

                if (blueTemp < 0)
                {
                    blueTemp = 0;
                }

                if (blueTemp > 0x1f)
                {
                    blueTemp = 0x1f;
                }

                int contaFinal = (0x400 * redTemp) + (0x20 * greenTemp) + blueTemp + 0x8000;
                if (contaFinal == 0x8000)
                {
                    contaFinal = 0x8001;
                }

                animIdx.Palette[i] = (ushort)contaFinal;
            }

            for (i = 0; i < Animations.PaletteCapacity; i++)
            {
                if (animIdx.Palette[i] < 0x8000)
                {
                    animIdx.Palette[i] = 0x8000;
                }
            }
        }
    }
}
