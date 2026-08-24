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
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Ultima;
using UoFiddler.Controls.Classes;
using UoFiddler.Controls.Forms;
using UoFiddler.Controls.Helpers;
using UoFiddler.Controls.UserControls.TileView;

namespace UoFiddler.Controls.UserControls
{
    public partial class ItemsControl : UserControl
    {
        public ItemsControl()
        {
            InitializeComponent();

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);

            RefMarker = this;
            DetailTextBox.AddBasicContextMenu();

            InitializeFilterMenuItems();
            InitializeExportWithHueMenu();

            // Hook into VisibleChanged to execute pending navigation when the tab is activated
            VisibleChanged += (_, _) => {
                if (Visible && IsLoaded && _pendingNavigationGraphicId >= 0)
                {
                    ExecutePendingNavigation();
                }
            };
        }

        private static readonly Regex _hexIndexRegex = new(@"0[xX][0-9a-fA-F]+", RegexOptions.Compiled);

        /// <summary>
        /// Enum for search types
        /// </summary>
        private enum SearchType
        {
            Name,
            Animation,
            Weight,
            Layer,
            StackOffset,
            Height
        }

        private List<int> _itemList = new List<int>();
        // full unfiltered list of items (used when dynamic filtering is enabled)
        private List<int> _allItemList = new List<int>();
        private bool _showFreeSlots;

        // Item flag filtering
        private TileFlag _selectedItemFlags = TileFlag.None;

        // Item hue preview
        private int _previewHue = -1;
        private bool _detailPartialHue;

        // Current search type
        private SearchType _currentSearchType = SearchType.Name;

        private int _selectedGraphicId = -1;
        private int _pendingNavigationGraphicId = -1;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int SelectedGraphicId
        {
            get => _selectedGraphicId;
            set
            {
                _selectedGraphicId = value < 0 ? 0 : value;

                // If the control isn't loaded or visible yet, defer the update
                if (!IsLoaded || !Visible)
                {
                    _pendingNavigationGraphicId = _selectedGraphicId;
                    return;
                }

                ItemsTileView.FocusIndex = _itemList.Count == 0 ? -1 : _itemList.IndexOf(_selectedGraphicId);

                UpdateToolStripLabels(_selectedGraphicId);
                UpdateDetail(_selectedGraphicId);
            }
        }

        public IReadOnlyList<int> ItemList { get => _itemList.AsReadOnly(); }
        public static ItemsControl RefMarker { get; private set; }
        public static TileViewControl TileView => RefMarker.ItemsTileView;
        public bool IsLoaded { get; private set; }

        /// <summary>
        /// Updates if TileSize is changed
        /// </summary>
        public void UpdateTileView()
        {
            var newSize = new Size(Options.ArtItemSizeWidth, Options.ArtItemSizeHeight);

            ItemsTileView.TileBorderColor = Options.RemoveTileBorder
                ? Color.Transparent
                : Color.Gray;

            var sameBackColor = ItemsTileView.BackColor == Options.PreviewBackgroundColor;
            ItemsTileView.BackColor = Options.PreviewBackgroundColor;

            var sameTileSize = ItemsTileView.TileSize == newSize;
            var sameFocusColor = ItemsTileView.TileFocusColor == Options.TileFocusColor;
            var sameSelectionColor = ItemsTileView.TileHighlightColor == Options.TileSelectionColor;
            if (sameTileSize && sameFocusColor && sameSelectionColor && sameBackColor)
            {
                return;
            }

            ItemsTileView.TileFocusColor = Options.TileFocusColor;
            ItemsTileView.TileHighlightColor = Options.TileSelectionColor;

            ItemsTileView.TileSize = newSize;
            ItemsTileView.Invalidate();

            if (_selectedGraphicId != -1)
            {
                UpdateDetail(_selectedGraphicId);
            }
        }

        /// <summary>
        /// Initializes dynamic filter menu items (only the runtime-generated TileFlag filters)
        /// </summary>
        private void InitializeFilterMenuItems()
        {
            try
            {
                if (filterToolStripMenuItem == null)
                {
                    return;
                }

                // Add "None" option to clear all filters
                var filterNoneMenuItem = new ToolStripMenuItem
                {
                    Name = "filterNone",
                    Text = "None",
                    CheckOnClick = true,
                    Checked = true
                };
                filterNoneMenuItem.Click += FilterNone_Click;
                filterToolStripMenuItem.DropDownItems.Add(filterNoneMenuItem);

                // Add separator
                filterToolStripMenuItem.DropDownItems.Add(new ToolStripSeparator());

                // Scan all items to find which flags are actually used
                var usedFlags = new HashSet<TileFlag>();
                for (int i = 0; i < TileData.ItemTable.Length; i++)
                {
                    var item = TileData.ItemTable[i];
                    if (item.Flags != TileFlag.None)
                    {
                        // Add each individual flag that's set on this item
                        foreach (TileFlag flag in Enum.GetValues(typeof(TileFlag)))
                        {
                            if (flag != TileFlag.None && (item.Flags & flag) != 0)
                            {
                                usedFlags.Add(flag);
                            }
                        }
                    }
                }

                // Add checkboxes only for flags that are actually used
                foreach (var flag in usedFlags.OrderBy(f => f.ToString()))
                {
                    var menuItem = new ToolStripMenuItem
                    {
                        Name = $"filter{flag}",
                        Text = flag.ToString(),
                        CheckOnClick = true,
                        Tag = flag
                    };
                    menuItem.Click += FilterFlag_Click;
                    filterToolStripMenuItem.DropDownItems.Add(menuItem);
                }
            }
            catch
            {
                // Ignore errors in designer
            }
        }

        /// <summary>
        /// Initializes the Export with Hue submenu with format options
        /// </summary>
        private void InitializeExportWithHueMenu()
        {
            try
            {
                if (exportWithHueToolStripMenuItem == null)
                {
                    return;
                }

                exportWithHueToolStripMenuItem.DropDownItems.Clear();

                var bmpHueItem = new ToolStripMenuItem
                {
                    Name = "exportWithHueBmpToolStripMenuItem",
                    Text = "As Bmp",
                    Enabled = false
                };
                bmpHueItem.Click += Extract_Image_WithHue_ClickBmp;
                exportWithHueToolStripMenuItem.DropDownItems.Add(bmpHueItem);

                var tiffHueItem = new ToolStripMenuItem
                {
                    Name = "exportWithHueTiffToolStripMenuItem",
                    Text = "As Tiff",
                    Enabled = false
                };
                tiffHueItem.Click += Extract_Image_WithHue_ClickTiff;
                exportWithHueToolStripMenuItem.DropDownItems.Add(tiffHueItem);

                var jpgHueItem = new ToolStripMenuItem
                {
                    Name = "exportWithHueJpgToolStripMenuItem",
                    Text = "As Jpg",
                    Enabled = false
                };
                jpgHueItem.Click += Extract_Image_WithHue_ClickJpg;
                exportWithHueToolStripMenuItem.DropDownItems.Add(jpgHueItem);

                var pngHueItem = new ToolStripMenuItem
                {
                    Name = "exportWithHuePngToolStripMenuItem",
                    Text = "As Png",
                    Enabled = false
                };
                pngHueItem.Click += Extract_Image_WithHue_ClickPng;
                exportWithHueToolStripMenuItem.DropDownItems.Add(pngHueItem);

                // Also initialize the context menu export with hue item as a submenu
                if (extractWithHueToolStripMenuItem != null)
                {
                    extractWithHueToolStripMenuItem.DropDownItems.Clear();

                    var bmpContextItem = new ToolStripMenuItem
                    {
                        Name = "extractWithHueBmpToolStripMenuItem",
                        Text = "As Bmp",
                        Enabled = false
                    };
                    bmpContextItem.Click += Extract_Image_WithHue_ClickBmp;
                    extractWithHueToolStripMenuItem.DropDownItems.Add(bmpContextItem);

                    var tiffContextItem = new ToolStripMenuItem
                    {
                        Name = "extractWithHueTiffToolStripMenuItem",
                        Text = "As Tiff",
                        Enabled = false
                    };
                    tiffContextItem.Click += Extract_Image_WithHue_ClickTiff;
                    extractWithHueToolStripMenuItem.DropDownItems.Add(tiffContextItem);

                    var jpgContextItem = new ToolStripMenuItem
                    {
                        Name = "extractWithHueJpgToolStripMenuItem",
                        Text = "As Jpg",
                        Enabled = false
                    };
                    jpgContextItem.Click += Extract_Image_WithHue_ClickJpg;
                    extractWithHueToolStripMenuItem.DropDownItems.Add(jpgContextItem);

                    var pngContextItem = new ToolStripMenuItem
                    {
                        Name = "extractWithHuePngToolStripMenuItem",
                        Text = "As Png",
                        Enabled = false
                    };
                    pngContextItem.Click += Extract_Image_WithHue_ClickPng;
                    extractWithHueToolStripMenuItem.DropDownItems.Add(pngContextItem);
                }
            }
            catch
            {
                // Ignore errors in designer
            }
        }

        /// <summary>
        /// Handles filter "None" click - clears all flag filters
        /// </summary>
        private void FilterNone_Click(object sender, EventArgs e)
        {
            _selectedItemFlags = TileFlag.None;

            // Uncheck all flag items except "None"
            foreach (ToolStripItem item in filterToolStripMenuItem.DropDownItems)
            {
                if (item is ToolStripMenuItem menuItem && menuItem.Name != "filterNone")
                {
                    menuItem.Checked = false;
                }
            }

            // Re-apply filter only if dynamic search is enabled
            if (dynamicItemSearchToolStripMenuItem?.Checked == true)
            {
                ApplyNameFilter(searchByNameToolStripTextBox.Text);
            }
        }

        /// <summary>
        /// Handles individual flag checkbox clicks
        /// </summary>
        private void FilterFlag_Click(object sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem menuItem && menuItem.Tag is TileFlag flag)
            {
                if (menuItem.Checked)
                {
                    _selectedItemFlags |= flag;
                }
                else
                {
                    _selectedItemFlags &= ~flag;
                }

                // Uncheck "None" if any flag is checked
                var noneItem = filterToolStripMenuItem.DropDownItems.Cast<ToolStripItem>()
                    .FirstOrDefault(x => x is ToolStripMenuItem m && m.Name == "filterNone") as ToolStripMenuItem;
                if (noneItem != null)
                {
                    noneItem.Checked = _selectedItemFlags == TileFlag.None;
                }

                // Re-apply filter only if dynamic search is enabled
                if (dynamicItemSearchToolStripMenuItem?.Checked == true)
                {
                    ApplyNameFilter(searchByNameToolStripTextBox.Text);
                }
            }
        }

        private HuePopUpItemForm _huePreviewForm;

        /// <summary>
        /// Opens hue selector for preview hue
        /// </summary>
        private void OnClick_PreviewHue(object sender, EventArgs e)
        {
            if (_huePreviewForm?.IsDisposed != false)
            {
                _huePreviewForm = new HuePopUpItemForm(UpdatePreviewHue, _previewHue);
            }
            else
            {
                _huePreviewForm.SetHue(_previewHue);
            }

            _huePreviewForm.TopMost = true;
            _huePreviewForm.Show();
        }

        /// <summary>
        /// Updates preview hue and refreshes tile view and detail preview
        /// </summary>
        private void UpdatePreviewHue(int selectedHue)
        {
            _previewHue = selectedHue;

            // Update "Remove Hue Preview" menu item visibility
            if (removeHuePreviewToolStripMenuItem != null)
            {
                removeHuePreviewToolStripMenuItem.Enabled = _previewHue >= 0 && _previewHue != -1;
            }

            // Update "Export with Hue" menu item visibility
            if (exportWithHueToolStripMenuItem != null)
            {
                exportWithHueToolStripMenuItem.Enabled = _previewHue >= 0;
                // Enable all submenu items as well
                foreach (ToolStripItem item in exportWithHueToolStripMenuItem.DropDownItems)
                {
                    if (item is ToolStripMenuItem menuItem)
                    {
                        menuItem.Enabled = _previewHue >= 0;
                    }
                }
            }

            // Update "Extract with Hue" menu item visibility (context menu)
            if (extractWithHueToolStripMenuItem != null)
            {
                extractWithHueToolStripMenuItem.Enabled = _previewHue >= 0;
                // Enable all submenu items as well
                foreach (ToolStripItem item in extractWithHueToolStripMenuItem.DropDownItems)
                {
                    if (item is ToolStripMenuItem menuItem)
                    {
                        menuItem.Enabled = _previewHue >= 0;
                    }
                }
            }

            // Refresh the tile view to apply the hue
            ItemsTileView.Invalidate();

            // Refresh detail preview
            UpdateDetail(_selectedGraphicId);
        }

        /// <summary>
        /// Removes the hue preview (sets to -1)
        /// </summary>
        private void OnClick_RemoveHuePreview(object sender, EventArgs e)
        {
            _previewHue = -1;

            // Update "Remove Hue Preview" menu item visibility
            if (removeHuePreviewToolStripMenuItem != null)
            {
                removeHuePreviewToolStripMenuItem.Enabled = false;
            }

            // Update "Export with Hue" menu item visibility
            if (exportWithHueToolStripMenuItem != null)
            {
                exportWithHueToolStripMenuItem.Enabled = false;
                // Disable all submenu items as well
                foreach (ToolStripItem item in exportWithHueToolStripMenuItem.DropDownItems)
                {
                    if (item is ToolStripMenuItem menuItem)
                    {
                        menuItem.Enabled = false;
                    }
                }
            }

            // Update "Extract with Hue" menu item visibility
            if (extractWithHueToolStripMenuItem != null)
            {
                extractWithHueToolStripMenuItem.Enabled = false;
                // Disable all submenu items as well
                foreach (ToolStripItem item in extractWithHueToolStripMenuItem.DropDownItems)
                {
                    if (item is ToolStripMenuItem menuItem)
                    {
                        menuItem.Enabled = false;
                    }
                }
            }

            // Refresh the tile view
            ItemsTileView.Invalidate();

            // Refresh detail preview
            UpdateDetail(_selectedGraphicId);
        }

        private void ApplyFilter(string searchValue)
        {
            if (_allItemList == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(searchValue) && _selectedItemFlags == TileFlag.None)
            {
                // empty search and no filters - restore full list
                _itemList = new List<int>(_allItemList);
                ItemsTileView.VirtualListSize = _itemList.Count;
                ItemsTileView.Invalidate();
                if (_itemList.Count > 0)
                {
                    SelectedGraphicId = _itemList[0];
                }
                return;
            }

            var filtered = new List<int>();

            switch (_currentSearchType)
            {
                case SearchType.Name:
                    {
                        var searchMethod = SearchHelper.GetSearchMethod();
                        foreach (var id in _allItemList)
                        {
                            // Check if item matches flag filters
                            if (_selectedItemFlags != TileFlag.None && 
                                (TileData.ItemTable[id].Flags & _selectedItemFlags) != _selectedItemFlags)
                            {
                                continue; // Item doesn't have all of the selected flags
                            }

                            // If search value is empty, include if flag matches
                            if (string.IsNullOrWhiteSpace(searchValue))
                            {
                                filtered.Add(id);
                                continue;
                            }

                            var result = searchMethod(searchValue, TileData.ItemTable[id].Name);
                            if (result.HasErrors)
                            {
                                break;
                            }

                            if (result.EntryFound)
                            {
                                filtered.Add(id);
                            }
                        }
                        break;
                    }
                case SearchType.Animation:
                    {
                        if (int.TryParse(searchValue, out int animation))
                        {
                            foreach (var id in _allItemList)
                            {
                                // Check if item matches flag filters
                                if (_selectedItemFlags != TileFlag.None && 
                                    (TileData.ItemTable[id].Flags & _selectedItemFlags) != _selectedItemFlags)
                                {
                                    continue;
                                }

                                if (TileData.ItemTable[id].Animation == animation)
                                {
                                    filtered.Add(id);
                                }
                            }
                        }
                        break;
                    }
                case SearchType.Weight:
                    {
                        if (int.TryParse(searchValue, out int weight))
                        {
                            foreach (var id in _allItemList)
                            {
                                // Check if item matches flag filters
                                if (_selectedItemFlags != TileFlag.None && 
                                    (TileData.ItemTable[id].Flags & _selectedItemFlags) != _selectedItemFlags)
                                {
                                    continue;
                                }

                                if (TileData.ItemTable[id].Weight == weight)
                                {
                                    filtered.Add(id);
                                }
                            }
                        }
                        break;
                    }
                case SearchType.Layer:
                    {
                        if (int.TryParse(searchValue, out int layer))
                        {
                            foreach (var id in _allItemList)
                            {
                                // Check if item matches flag filters
                                if (_selectedItemFlags != TileFlag.None && 
                                    (TileData.ItemTable[id].Flags & _selectedItemFlags) != _selectedItemFlags)
                                {
                                    continue;
                                }

                                var item = TileData.ItemTable[id];
                                var relevantFlags = TileFlag.Wearable | TileFlag.Weapon | TileFlag.Armor;
                                if (item.Quality == layer && (item.Flags & relevantFlags) != 0)
                                {
                                    filtered.Add(id);
                                }
                            }
                        }
                        break;
                    }
                case SearchType.StackOffset:
                    {
                        if (int.TryParse(searchValue, out int stackingOffset))
                        {
                            foreach (var id in _allItemList)
                            {
                                // Check if item matches flag filters
                                if (_selectedItemFlags != TileFlag.None && 
                                    (TileData.ItemTable[id].Flags & _selectedItemFlags) != _selectedItemFlags)
                                {
                                    continue;
                                }

                                if (TileData.ItemTable[id].StackingOffset == stackingOffset)
                                {
                                    filtered.Add(id);
                                }
                            }
                        }
                        break;
                    }
                case SearchType.Height:
                    {
                        if (int.TryParse(searchValue, out int height))
                        {
                            foreach (var id in _allItemList)
                            {
                                // Check if item matches flag filters
                                if (_selectedItemFlags != TileFlag.None && 
                                    (TileData.ItemTable[id].Flags & _selectedItemFlags) != _selectedItemFlags)
                                {
                                    continue;
                                }

                                if (TileData.ItemTable[id].Height == height)
                                {
                                    filtered.Add(id);
                                }
                            }
                        }
                        break;
                    }
            }

            _itemList = filtered;
            ItemsTileView.VirtualListSize = _itemList.Count;
            ItemsTileView.Invalidate();

            if (_itemList.Count > 0)
            {
                SelectedGraphicId = _itemList[0];
            }
        }

        private void ApplyNameFilter(string name)
        {
            ApplyFilter(name);
        }

        private void DynamicItemSearchToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem mi)
            {
                bool dynamic = mi.Checked;
                // hide the Find Next/Prev buttons when dynamic searching is enabled
                searchByNameToolStripButton.Visible = !dynamic;
                searchByNamePrevToolStripButton.Visible = !dynamic;

                if (!dynamic)
                {
                    // restore full list
                    _itemList = new List<int>(_allItemList);
                    ItemsTileView.VirtualListSize = _itemList.Count;
                    ItemsTileView.Invalidate();
                }
                else
                {
                    // apply filter if any text is present
                    ApplyNameFilter(searchByNameToolStripTextBox.Text);
                }
            }
        }

        private void SearchTypeMenuItem_Click(object sender, EventArgs e)
        {
            if (!(sender is ToolStripMenuItem mi))
            {
                return;
            }

            // Uncheck all search type menu items
            searchTypeNameToolStripMenuItem.Checked = false;
            searchTypeAnimationToolStripMenuItem.Checked = false;
            searchTypeWeightToolStripMenuItem.Checked = false;
            searchTypeLayerToolStripMenuItem.Checked = false;
            searchTypeStackOffsetToolStripMenuItem.Checked = false;
            searchTypeHeightToolStripMenuItem.Checked = false;

            // Check the selected item and update current search type
            mi.Checked = true;

            if (mi == searchTypeNameToolStripMenuItem)
            {
                _currentSearchType = SearchType.Name;
                toolStripLabel2.Text = "Name:";
                searchByNameToolStripTextBox.ToolTipText = "Search by item name";
            }
            else if (mi == searchTypeAnimationToolStripMenuItem)
            {
                _currentSearchType = SearchType.Animation;
                toolStripLabel2.Text = "Animation:";
                searchByNameToolStripTextBox.ToolTipText = "Search by animation ID";
            }
            else if (mi == searchTypeWeightToolStripMenuItem)
            {
                _currentSearchType = SearchType.Weight;
                toolStripLabel2.Text = "Weight:";
                searchByNameToolStripTextBox.ToolTipText = "Search by weight value";
            }
            else if (mi == searchTypeLayerToolStripMenuItem)
            {
                _currentSearchType = SearchType.Layer;
                toolStripLabel2.Text = "Layer:";
                searchByNameToolStripTextBox.ToolTipText = "Search by layer (wearables only)";
            }
            else if (mi == searchTypeStackOffsetToolStripMenuItem)
            {
                _currentSearchType = SearchType.StackOffset;
                toolStripLabel2.Text = "Stack Offset:";
                searchByNameToolStripTextBox.ToolTipText = "Search by stack offset value";
            }
            else if (mi == searchTypeHeightToolStripMenuItem)
            {
                _currentSearchType = SearchType.Height;
                toolStripLabel2.Text = "Height:";
                searchByNameToolStripTextBox.ToolTipText = "Search by height value";
            }

            // Clear the search box when changing search type
            searchByNameToolStripTextBox.Text = "";

            // If dynamic searching is enabled, reapply the filter with the new search type
            if (dynamicItemSearchToolStripMenuItem?.Checked == true)
            {
                ApplyFilter("");
            }
        }

        /// <summary>
        /// Searches graphic number and selects it
        /// </summary>
        /// <param name="graphic"></param>
        /// <returns></returns>
        public static bool SearchGraphic(int graphic)
        {
            if (RefMarker == null)
            {
                return false;
            }

            if (!RefMarker.IsLoaded)
            {
                RefMarker.OnLoad(RefMarker, EventArgs.Empty);
            }

            if (RefMarker._itemList.TrueForAll(t => t != graphic))
            {
                return false;
            }

            TabPageNavigator.ActivateOwningTabPage(RefMarker);

            if (RefMarker.IsHandleCreated)
            {
                RefMarker.BeginInvoke(new Action(() =>
                {
                    // we have to invalidate focus so it will scroll to item
                    RefMarker.ItemsTileView.FocusIndex = -1;
                    RefMarker.SelectedGraphicId = graphic;
                }));
            }
            else
            {
                RefMarker.ItemsTileView.FocusIndex = -1;
                RefMarker.SelectedGraphicId = graphic;
            }

            return true;
        }

        /// <summary>
        /// Searches for name and selects
        /// </summary>
        /// <param name="name"></param>
        /// <param name="next">starting from current selected</param>
        /// <returns></returns>
        public static bool SearchName(string name, bool next)
        {
            int index = 0;
            if (next)
            {
                if (RefMarker._selectedGraphicId >= 0)
                {
                    index = RefMarker._itemList.IndexOf(RefMarker._selectedGraphicId) + 1;
                }

                if (index >= RefMarker._itemList.Count)
                {
                    index = 0;
                }
            }

            var searchMethod = SearchHelper.GetSearchMethod();

            // First pass: search from current index to end
            for (int i = index; i < RefMarker._itemList.Count; ++i)
            {
                int itemId = RefMarker._itemList[i];
                var item = TileData.ItemTable[itemId];
                string searchValue = GetSearchableValue(item);

                var searchResult = searchMethod(name, searchValue);
                if (searchResult.HasErrors)
                {
                    break;
                }

                if (!searchResult.EntryFound)
                {
                    continue;
                }

                // we have to invalidate focus so it will scroll to item
                RefMarker.ItemsTileView.FocusIndex = -1;
                RefMarker.SelectedGraphicId = itemId;

                return true;
            }

            // Second pass: if we didn't find anything, wrap and search from the beginning
            if (index > 0)
            {
                for (int i = 0; i < index; ++i)
                {
                    int itemId = RefMarker._itemList[i];
                    var item = TileData.ItemTable[itemId];
                    string searchValue = GetSearchableValue(item);

                    var searchResult = searchMethod(name, searchValue);
                    if (searchResult.HasErrors)
                    {
                        break;
                    }

                    if (!searchResult.EntryFound)
                    {
                        continue;
                    }

                    // we have to invalidate focus so it will scroll to item
                    RefMarker.ItemsTileView.FocusIndex = -1;
                    RefMarker.SelectedGraphicId = itemId;

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the searchable value from an item based on the current search type
        /// </summary>
        private static string GetSearchableValue(ItemData item)
        {
            return RefMarker._currentSearchType switch
            {
                SearchType.Name => item.Name,
                SearchType.Animation => item.Animation.ToString(),
                SearchType.Weight => item.Weight.ToString(),
                SearchType.Layer => item.Quality.ToString(),
                SearchType.StackOffset => item.StackingOffset.ToString(),
                SearchType.Height => item.Height.ToString(),
                _ => item.Name
            };
        }


        public void OnLoad(object sender, EventArgs e)
        {
            if (IsAncestorSiteInDesignMode || FormsDesignerHelper.IsInDesignMode())
            {
                return;
            }

            if (IsLoaded && (!(e is MyEventArgs args) || args.Type != MyEventArgs.Types.ForceReload))
            {
                return;
            }

            using (new WaitCursorScope(this))
            {
                Options.LoadedUltimaClass["TileData"] = true;
                Options.LoadedUltimaClass["Art"] = true;
                Options.LoadedUltimaClass["Animdata"] = true;
                Options.LoadedUltimaClass["Hues"] = true;

                // Preload cliloc data so GetClilocText works when displaying item details
                ClilocControl.EnsureLoaded();

                if (!IsLoaded) // only once
                {
                    Plugin.PluginEvents.FireModifyItemShowContextMenuEvent(TileViewContextMenuStrip);
                }

                UpdateTileView();

                _showFreeSlots = false;
                showFreeSlotsToolStripMenuItem.Checked = false;

                var prevSelected = SelectedGraphicId;

                int staticLength = Art.GetMaxItemId();
                _itemList = new List<int>(staticLength);
                for (int i = 0; i <= staticLength; ++i)
                {
                    if (Art.IsValidStatic(i))
                    {
                        _itemList.Add(i);
                    }
                }

                // Initialize the full unfiltered list for dynamic filtering
                _allItemList = new List<int>(_itemList);

                ItemsTileView.VirtualListSize = _itemList.Count;

                if (prevSelected >= 0)
                {
                    SelectedGraphicId = _itemList.Contains(prevSelected) ? prevSelected : 0;
                }

                if (!IsLoaded)
                {
                    ControlEvents.FilePathChangeEvent += OnFilePathChangeEvent;
                    ControlEvents.ItemChangeEvent += OnItemChangeEvent;
                    ControlEvents.TileDataChangeEvent += OnTileDataChangeEvent;
                    ControlEvents.PreviewBackgroundColorChangeEvent += OnPreviewBackgroundColorChanged;
                }

                IsLoaded = true;
                UpdateFileLoadedLabel();
            }
        }

        /// <summary>
        /// ReLoads if loaded
        /// </summary>
        private void Reload()
        {
            if (IsLoaded)
            {
                OnLoad(this, new MyEventArgs(MyEventArgs.Types.ForceReload));
            }
        }

        private void OnFilePathChangeEvent()
        {
            Reload();
        }

        private void OnPreviewBackgroundColorChanged()
        {
            ItemsTileView.BackColor = Options.PreviewBackgroundColor;
            ItemsTileView.Invalidate();
            if (_selectedGraphicId != -1)
            {
                UpdateDetail(_selectedGraphicId);
            }
        }

        private void OnTileDataChangeEvent(object sender, int id)
        {
            if (!IsLoaded)
            {
                return;
            }

            if (sender.Equals(this))
            {
                return;
            }

            if (id < 0x4000)
            {
                return;
            }

            id -= 0x4000;

            if (_selectedGraphicId != id)
            {
                return;
            }

            UpdateToolStripLabels(id);
            UpdateDetail(id);
        }

        private void OnItemChangeEvent(object sender, int index)
        {
            if (!IsLoaded)
            {
                return;
            }

            if (sender.Equals(this))
            {
                return;
            }

            if (Art.IsValidStatic(index))
            {
                bool done = false;
                for (int i = 0; i < _itemList.Count; ++i)
                {
                    if (index < _itemList[i])
                    {
                        _itemList.Insert(i, index);
                        done = true;
                        break;
                    }

                    if (index != _itemList[i])
                    {
                        continue;
                    }

                    done = true;
                    break;
                }

                if (!done)
                {
                    _itemList.Add(index);
                }
            }
            else
            {
                if (_showFreeSlots)
                {
                    return;
                }

                _itemList.Remove(index);
            }

            ItemsTileView.VirtualListSize = _itemList.Count;
            ItemsTileView.Invalidate();
        }

        private void ChangeBackgroundColorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (colorDialog.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            Options.PreviewBackgroundColor = colorDialog.Color;
            ControlEvents.FirePreviewBackgroundColorChangeEvent();
        }

        private void UpdateDetail(int graphic)
        {
            if (IsAncestorSiteInDesignMode || FormsDesignerHelper.IsInDesignMode())
            {
                return;
            }

            if (!IsLoaded)
            {
                return;
            }

            if (_scrolling)
            {
                return;
            }

            // Validate graphic ID is within bounds
            if (graphic < 0 || graphic >= TileData.ItemTable.Length)
            {
                return;
            }

            ItemData item = TileData.ItemTable[graphic];
            Bitmap bit = Art.GetStatic(graphic);

            int xMin = 0;
            int xMax = 0;
            int yMin = 0;
            int yMax = 0;

            const int defaultSplitterDistance = 180;
            if (bit == null)
            {
                splitContainer2.SplitterDistance = defaultSplitterDistance;
                Bitmap newBit = new Bitmap(DetailPictureBox.Size.Width, DetailPictureBox.Size.Height);
                using (Graphics newGraph = Graphics.FromImage(newBit))
                {
                    newGraph.Clear(Options.PreviewBackgroundColor);
                }

                DetailPictureBox.Image?.Dispose();
                DetailPictureBox.Image = newBit;
            }
            else
            {
                var distance = bit.Size.Height + 10;
                splitContainer2.SplitterDistance = distance < defaultSplitterDistance ? defaultSplitterDistance : distance;

                Bitmap newBit = new Bitmap(DetailPictureBox.Size.Width, DetailPictureBox.Size.Height);
                using (Graphics newGraph = Graphics.FromImage(newBit))
                {
                    newGraph.Clear(Options.PreviewBackgroundColor);

                    // Apply hue if preview is set
                    if (_previewHue >= 0)
                    {
                        // Clone the bitmap to apply hue
                        Bitmap hueBit = new Bitmap(bit);
                        bool usePartialHue = (item.Flags & TileFlag.PartialHue) != 0;
                        Hue hue = Hues.List[_previewHue];
                        hue.ApplyTo(hueBit, usePartialHue);
                        _detailPartialHue = usePartialHue;
                        newGraph.DrawImage(hueBit, (DetailPictureBox.Size.Width - hueBit.Width) / 2, 5);
                        hueBit.Dispose();
                    }
                    else
                    {
                        newGraph.DrawImage(bit, (DetailPictureBox.Size.Width - bit.Width) / 2, 5);
                    }
                }

                DetailPictureBox.Image?.Dispose();
                DetailPictureBox.Image = newBit;

                Art.Measure(bit, out xMin, out yMin, out xMax, out yMax);
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Name: {item.Name}");
            // Calculate the cliloc label number based on item ID
            int clilocNumber = graphic < 0x4000
                ? 1020000 + graphic
                : 1078872 + graphic;
            string clilocText = ClilocControl.GetClilocText(clilocNumber);
            sb.AppendLine($"Cliloc: {clilocText}");
            //sb.AppendLine($"Cliloc: {clilocNumber} - {clilocText}");
            sb.AppendLine($"Graphic: 0x{graphic:X4}({graphic})");
            //sb.AppendLine($"Graphic: 0x{graphic:X4}");
            sb.AppendLine($"Height/Capacity: {item.Height}");
            sb.AppendLine($"Weight: {item.Weight}");
            sb.AppendLine($"Animation: {item.Animation}");
            sb.AppendLine($"Quality/Layer/Light: {item.Quality}");
            sb.AppendLine($"Quantity: {item.Quantity}");
            sb.AppendLine($"Hue: {item.Hue}");
            sb.AppendLine($"StackingOffset/Unk4: {item.StackingOffset}");
            sb.AppendLine($"Flags: {item.Flags}");
            sb.AppendLine($"Graphic Size: {bit?.Width ?? 0} x {bit?.Height ?? 0} ");
            sb.AppendLine($"Graphic Offset xMin, yMin, xMax, yMax: {xMin} {yMin} {xMax} {yMax}");
            //sb.AppendLine($"Graphic pixel size width, height: {bit?.Width ?? 0} {bit?.Height ?? 0} ");
            //sb.AppendLine($"Graphic pixel offset xMin, yMin, xMax, yMax: {xMin} {yMin} {xMax} {yMax}");

            if ((item.Flags & TileFlag.Animation) != 0)
            {
                Animdata.AnimdataEntry info = Animdata.GetAnimData(graphic);
                if (info != null)
                {
                    sb.AppendLine($"Animation FrameCount: {info.FrameCount} Interval: {info.FrameInterval}");
                }
            }

            DetailTextBox.Clear();
            DetailTextBox.AppendText(sb.ToString());
        }

        private void ChangeBackgroundColorToolStripMenuItemDetail_Click(object sender, EventArgs e)
        {
            if (colorDialog.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            Options.PreviewBackgroundColor = colorDialog.Color;
            ControlEvents.FirePreviewBackgroundColorChangeEvent();
        }

        private bool _scrolling;

        private void OnClickFindFree(object sender, EventArgs e)
        {
            if (_showFreeSlots)
            {
                int i = _selectedGraphicId > -1 ? _itemList.IndexOf(_selectedGraphicId) + 1 : 0;
                for (; i < _itemList.Count; ++i)
                {
                    if (Art.IsValidStatic(_itemList[i]))
                    {
                        continue;
                    }

                    SelectedGraphicId = _itemList[i];
                    ItemsTileView.Invalidate();
                    break;
                }
            }
            else
            {
                int id, i;

                if (_selectedGraphicId > -1)
                {
                    id = _selectedGraphicId + 1;
                    i = _itemList.IndexOf(_selectedGraphicId) + 1;
                }
                else
                {
                    id = 0;
                    i = 0;
                }

                for (; i < _itemList.Count; ++i, ++id)
                {
                    if (id >= _itemList[i])
                    {
                        continue;
                    }

                    SelectedGraphicId = _itemList[i];
                    ItemsTileView.Invalidate();
                    break;
                }
            }
        }

        private void OnClickReplace(object sender, EventArgs e)
        {
            if (ItemsTileView.SelectedIndices.Count > 1)
            {
                ReplaceMultipleSelected();
                return;
            }

            if (_selectedGraphicId < 0)
            {
                return;
            }

            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Multiselect = false;
                dialog.Title = "Choose image file to replace";
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
                        bitmap = Utils.ConvertBmp(bitmap);
                    }

                    // Validate image size before replacing
                    if (!Art.ValidateStaticSize(bitmap, out int estimatedSize))
                    {
                        MessageBox.Show(
                            $"Image is too large for MUL format!\n\n" +
                            $"Image dimensions: {bitmap.Width}x{bitmap.Height}\n" +
                            $"Encoded size: {estimatedSize:N0} ushorts\n" +
                            $"Maximum allowed: 65,535 ushorts\n\n" +
                            $"The static art format encodes opaque pixel runs only; cost per row is\n" +
                            $"2 ushorts per run + 1 ushort per opaque pixel + 2 end markers.\n" +
                            $"Reduce the image size or the amount of opaque content.",
                            "Image Too Large",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                        return;
                    }

                    Art.ReplaceStatic(_selectedGraphicId, bitmap);

                    ControlEvents.FireItemChangeEvent(this, _selectedGraphicId);

                    ItemsTileView.Invalidate();
                    UpdateToolStripLabels(_selectedGraphicId);
                    UpdateDetail(_selectedGraphicId);

                    Options.ChangedUltimaClass["Art"] = true;
                }
            }
        }

        private void ReplaceMultipleSelected()
        {
            var ids = GetSelectedGraphicIds();
            if (ids.Count == 0)
            {
                return;
            }

            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Multiselect = true;
                dialog.Title = $"Choose {ids.Count} image files to replace selected items";
                dialog.CheckFileExists = true;
                dialog.Filter = "Image files (*.tif;*.tiff;*.bmp;*.png)|*.tif;*.tiff;*.bmp;*.png";

                if (dialog.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                var files = dialog.FileNames.OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase).ToArray();

                if (files.Length != ids.Count)
                {
                    MessageBox.Show(
                        $"Selected {ids.Count} items but chose {files.Length} images.\n\nNo changes made.",
                        "Selection Mismatch",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                // Load and validate all images first; abort the whole batch on any failure so no partial writes happen.
                var bitmaps = new List<Bitmap>(ids.Count);
                try
                {
                    for (int i = 0; i < ids.Count; ++i)
                    {
                        using (var bmpTemp = new Bitmap(files[i]))
                        {
                            Bitmap bitmap = new Bitmap(bmpTemp);

                            if (files[i].Contains(".bmp"))
                            {
                                bitmap = Utils.ConvertBmp(bitmap);
                            }

                            if (!Art.ValidateStaticSize(bitmap, out int estimatedSize))
                            {
                                bitmap.Dispose();
                                MessageBox.Show(
                                    $"Image is too large for MUL format!\n\n" +
                                    $"File: {Path.GetFileName(files[i])}\n" +
                                    $"Encoded size: {estimatedSize:N0} ushorts\n" +
                                    $"Maximum allowed: 65,535 ushorts\n\n" +
                                    $"No changes made.",
                                    "Image Too Large",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Warning);
                                return;
                            }

                            bitmaps.Add(bitmap);
                        }
                    }
                }
                catch
                {
                    foreach (var bmp in bitmaps)
                    {
                        bmp.Dispose();
                    }
                    throw;
                }

                for (int i = 0; i < ids.Count; ++i)
                {
                    Art.ReplaceStatic(ids[i], bitmaps[i]);
                    ControlEvents.FireItemChangeEvent(this, ids[i]);
                }

                ItemsTileView.Invalidate();
                UpdateToolStripLabels(_selectedGraphicId);
                UpdateDetail(_selectedGraphicId);

                Options.ChangedUltimaClass["Art"] = true;
            }
        }

        private void OnClickRemove(object sender, EventArgs e)
        {
            var ids = GetSelectedGraphicIds().Where(Art.IsValidStatic).ToList();
            if (ids.Count == 0)
            {
                return;
            }

            string prompt = ids.Count == 1
                ? $"Are you sure to remove 0x{ids[0]:X}"
                : $"Are you sure to remove {ids.Count} items?";

            DialogResult result = MessageBox.Show(prompt, "Save",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
            if (result != DialogResult.Yes)
            {
                return;
            }

            foreach (int id in ids)
            {
                Art.RemoveStatic(id);
                ControlEvents.FireItemChangeEvent(this, id);

                if (!_showFreeSlots)
                {
                    _itemList.Remove(id);
                }
            }

            ItemsTileView.SelectedIndices.Clear();

            if (!_showFreeSlots)
            {
                ItemsTileView.VirtualListSize = _itemList.Count;
                int moveToId = ids[0] - 1;
                SelectedGraphicId = moveToId <= 0 ? 0 : moveToId; // TODO: get last index visible instead just curr -1
            }
            ItemsTileView.Invalidate();

            Options.ChangedUltimaClass["Art"] = true;
        }

        private void OnTextChangedInsert(object sender, EventArgs e)
        {
            Color invalidColor = Options.DarkMode ? Color.OrangeRed : Color.Red;
            if (Utils.ConvertStringToInt(InsertText.Text, out int index, 0, Art.GetMaxItemId()))
            {
                InsertText.ForeColor = Art.IsValidStatic(index) ? invalidColor : SystemColors.ControlText;
            }
            else
            {
                InsertText.ForeColor = invalidColor;
            }
        }

        private void OnKeyDownInsertText(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
            {
                return;
            }

            if (!Utils.ConvertStringToInt(InsertText.Text, out int index, 0, Art.GetMaxItemId()))
            {
                return;
            }

            if (Art.IsValidStatic(index))
            {
                return;
            }

            TileViewContextMenuStrip.Close();

            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Multiselect = false;
                dialog.Title = $"Choose images to replace starting at 0x{index:X}";
                dialog.CheckFileExists = true;
                dialog.Filter = "Image files (*.tif;*.tiff;*.bmp;*.png)|*.tif;*.tiff;*.bmp;*.png";

                if (dialog.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                AddSingleItem(dialog.FileName, index);
            }
        }

        private void UpdateToolStripLabels(int graphic)
        {
            if (IsAncestorSiteInDesignMode || FormsDesignerHelper.IsInDesignMode())
            {
                return;
            }

            if (!IsLoaded)
            {
                return;
            }

            if (_scrolling)
            {
                return;
            }

            NameLabel.Text = !Art.IsValidStatic(graphic) ? "Name: FREE" : $"Name: {TileData.ItemTable[graphic].Name}";
            GraphicLabel.Text = $"Graphic: 0x{graphic:X4} ({graphic})";

            // FileLoadedLabel always shows regardless of selection
            UpdateFileLoadedLabel();
        }

        private void UpdateFileLoadedLabel()
        {
            FileLoadedLabel.Text = Art.IsUsingUopLegacy() ? "Loaded: UOP" : "Loaded: MUL";
        }

        private void ExecutePendingNavigation()
        {
            if (_pendingNavigationGraphicId >= 0 && IsLoaded && Visible)
            {
                int graphicToSelect = _pendingNavigationGraphicId;
                _pendingNavigationGraphicId = -1; // Clear after execution

                ItemsTileView.FocusIndex = _itemList.Count == 0 ? -1 : _itemList.IndexOf(graphicToSelect);

                UpdateToolStripLabels(graphicToSelect);
                UpdateDetail(graphicToSelect);
            }
        }

        private void OnClickSave(object sender, EventArgs e)
        {
            DialogResult result = MessageBox.Show("Are you sure? Will take a while", "Save", MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);

            if (result != DialogResult.Yes)
            {
                return;
            }

            using (new WaitCursorScope(this))
            {
                ProgressBarDialog barDialog = new ProgressBarDialog(Art.GetIdxLength(), "Save");
                Art.Save(Options.OutputPath);
                barDialog.Dispose();

                // If currently using UOP, convert the saved MUL files back to UOP format
                UopConversionHelper.ConvertArtToUopIfNeeded(Options.OutputPath);
            }

            Options.ChangedUltimaClass["Art"] = false;

            FileSavedDialog.Show(FindForm(), Options.OutputPath, "Files saved successfully.");
        }

        private void OnClickShowFreeSlots(object sender, EventArgs e)
        {
            _showFreeSlots = !_showFreeSlots;
            if (_showFreeSlots)
            {
                for (int j = 0; j <= Art.GetMaxItemId(); ++j)
                {
                    if (_itemList.Count > j)
                    {
                        if (_itemList[j] != j)
                        {
                            _itemList.Insert(j, j);
                        }
                    }
                    else
                    {
                        _itemList.Insert(j, j);
                    }
                }

                var prevSelected = SelectedGraphicId;

                ItemsTileView.VirtualListSize = _itemList.Count;

                if (prevSelected >= 0)
                {
                    SelectedGraphicId = prevSelected;
                }

                ItemsTileView.Invalidate();
            }
            else
            {
                Reload();
            }
        }

        private void Extract_Image_ClickBmp(object sender, EventArgs e)
        {
            ExportSelected(ImageFormat.Bmp);
        }

        private void Extract_Image_ClickTiff(object sender, EventArgs e)
        {
            ExportSelected(ImageFormat.Tiff);
        }

        private void Extract_Image_ClickJpg(object sender, EventArgs e)
        {
            ExportSelected(ImageFormat.Jpeg);
        }

        private void Extract_Image_ClickPng(object sender, EventArgs e)
        {
            ExportSelected(ImageFormat.Png);
        }

        private void Extract_Image_WithHue_ClickBmp(object sender, EventArgs e)
        {
            ExportSelected(ImageFormat.Bmp, applyHue: true);
        }

        private void Extract_Image_WithHue_ClickTiff(object sender, EventArgs e)
        {
            ExportSelected(ImageFormat.Tiff, applyHue: true);
        }

        private void Extract_Image_WithHue_ClickJpg(object sender, EventArgs e)
        {
            ExportSelected(ImageFormat.Jpeg, applyHue: true);
        }

        private void Extract_Image_WithHue_ClickPng(object sender, EventArgs e)
        {
            ExportSelected(ImageFormat.Png, applyHue: true);
        }

        private void ExportSelected(ImageFormat imageFormat)
        {
            ExportSelected(imageFormat, applyHue: false);
        }

        private void ExportSelected(ImageFormat imageFormat, bool applyHue)
        {
            var ids = GetSelectedGraphicIds().Where(Art.IsValidStatic).ToList();
            if (ids.Count == 0)
            {
                return;
            }

            if (ids.Count == 1)
            {
                ExportItemImage(ids[0], imageFormat, applyHue);
                return;
            }

            ExportMultipleItemImages(ids, imageFormat, applyHue);
        }

        private void ExportMultipleItemImages(List<int> ids, ImageFormat imageFormat)
        {
            ExportMultipleItemImages(ids, imageFormat, applyHue: false);
        }

        private void ExportMultipleItemImages(List<int> ids, ImageFormat imageFormat, bool applyHue = false)
        {
            string fileExtension = Utils.GetFileExtensionFor(imageFormat);

            foreach (int index in ids)
            {
                var artBitmap = Art.GetStatic(index);
                if (artBitmap is null)
                {
                    continue;
                }

                string hueSuffix = (applyHue && _previewHue >= 0) ? $" - Hue {_previewHue}" : "";
                string fileName = Path.Combine(Options.OutputPath, $"Item {Utils.FormatExportId(index)}{hueSuffix}.{fileExtension}");
                using (Bitmap bit = new Bitmap(artBitmap))
                {
                    if (applyHue && _previewHue >= 0)
                    {
                        ItemData item = TileData.ItemTable[index];
                        bool usePartialHue = (item.Flags & TileFlag.PartialHue) != 0;
                        Hue hue = Hues.List[_previewHue];
                        hue.ApplyTo(bit, usePartialHue);
                    }
                    bit.Save(fileName, imageFormat);
                }
            }

            FileSavedDialog.Show(FindForm(), Options.OutputPath, $"{ids.Count} items saved successfully.");
        }

        private static void ExportItemImage_Static(int index, ImageFormat imageFormat)
        {
            if (!Art.IsValidStatic(index))
            {
                return;
            }

            string fileExtension = Utils.GetFileExtensionFor(imageFormat);
            string fileName = Path.Combine(Options.OutputPath, $"Item {Utils.FormatExportId(index)}.{fileExtension}");

            using (Bitmap bit = new Bitmap(Art.GetStatic(index)))
            {
                bit.Save(fileName, imageFormat);
            }

            MessageBox.Show($"Item saved to {fileName}", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button1);
        }

        private void ExportItemImage(int index, ImageFormat imageFormat, bool applyHue = false)
        {
            if (!Art.IsValidStatic(index))
            {
                return;
            }

            string fileExtension = Utils.GetFileExtensionFor(imageFormat);
            string hueSuffix = (applyHue && _previewHue >= 0) ? $" - Hue {_previewHue}" : "";
            string fileName = Path.Combine(Options.OutputPath, $"Item {Utils.FormatExportId(index)}{hueSuffix}.{fileExtension}");

            using (Bitmap bit = new Bitmap(Art.GetStatic(index)))
            {
                if (applyHue && _previewHue >= 0)
                {
                    ItemData item = TileData.ItemTable[index];
                    bool usePartialHue = (item.Flags & TileFlag.PartialHue) != 0;
                    Hue hue = Hues.List[_previewHue];
                    hue.ApplyTo(bit, usePartialHue);
                }
                bit.Save(fileName, imageFormat);
            }

            MessageBox.Show($"Item saved to {fileName}", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button1);
        }

        private void OnClickSelectAllTabs(object sender, EventArgs e)
        {
            if (_selectedGraphicId >= 0)
            {
                // Cache navigation targets for all tabs
                TileDataControl.SetPendingNavigation(_selectedGraphicId, land: false);
                RadarColorControl.SetPendingNavigation(_selectedGraphicId, land: false);

                // Calculate the cliloc label number based on item ID
                int clilocNumber = _selectedGraphicId < 0x4000
                    ? 1020000 + _selectedGraphicId
                    : 1078872 + _selectedGraphicId;
                ClilocControl.SetPendingNavigation(clilocNumber);

                // For Gumps, try to find and set pending navigation using the same logic as the menu options
                var itemData = TileData.ItemTable[_selectedGraphicId];
                if (itemData.Animation > 0)
                {
                    // Try male gump first
                    int maleGumpId = itemData.Animation + _maleGumpOffset;
                    if (GumpControl.HasGumpId(maleGumpId))
                    {
                        GumpControl.SetPendingNavigation(maleGumpId);
                    }
                    // Fall back to female gump
                    else
                    {
                        int femaleGumpId = itemData.Animation + _femaleGumpOffset;
                        if (GumpControl.HasGumpId(femaleGumpId))
                        {
                            GumpControl.SetPendingNavigation(femaleGumpId);
                        }
                    }
                }
            }
        }

        private void OnClickSelectTiledata(object sender, EventArgs e)
        {
            if (_selectedGraphicId >= 0)
            {
                // Cache navigation target for fast load
                TileDataControl.SetPendingNavigation(_selectedGraphicId, land: false);
                TileDataControl.Select(_selectedGraphicId, false);
            }
        }

        private void OnClickSelectRadarCol(object sender, EventArgs e)
        {
            if (_selectedGraphicId >= 0)
            {
                // Cache navigation target for fast load
                RadarColorControl.SetPendingNavigation(_selectedGraphicId, land: false);
                RadarColorControl.Select(_selectedGraphicId, false);
            }
        }

        private void OnClickSelectCliloc(object sender, EventArgs e)
        {
            if (_selectedGraphicId >= 0)
            {
                // Calculate the cliloc label number based on item ID
                int clilocNumber = _selectedGraphicId < 0x4000
                    ? 1020000 + _selectedGraphicId
                    : 1078872 + _selectedGraphicId;

                // Cache navigation target for fast load
                ClilocControl.SetPendingNavigation(clilocNumber);
                ClilocControl.Select(clilocNumber);
            }
        }

        private void OnClick_SaveAllBmp(object sender, EventArgs e)
        {
            ExportAllItemImages(ImageFormat.Bmp);
        }

        private void OnClick_SaveAllTiff(object sender, EventArgs e)
        {
            ExportAllItemImages(ImageFormat.Tiff);
        }

        private void OnClick_SaveAllJpg(object sender, EventArgs e)
        {
            ExportAllItemImages(ImageFormat.Jpeg);
        }

        private void OnClick_SaveAllPng(object sender, EventArgs e)
        {
            ExportAllItemImages(ImageFormat.Png);
        }

        private void ExportAllItemImages(ImageFormat imageFormat)
        {
            string fileExtension = Utils.GetFileExtensionFor(imageFormat);

            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select directory";
                dialog.ShowNewFolderButton = true;
                if (dialog.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                using (new WaitCursorScope(this))
                {
                    using (new ProgressBarDialog(_itemList.Count, $"Export to {fileExtension}", false))
                    {
                        foreach (var artItemIndex in _itemList)
                        {
                            ControlEvents.FireProgressChangeEvent();
                            Application.DoEvents();

                            int index = artItemIndex;
                            if (index < 0)
                            {
                                continue;
                            }

                            string fileName = Path.Combine(dialog.SelectedPath, $"Item {Utils.FormatExportId(index)}.{fileExtension}");
                            var artBitmap = Art.GetStatic(index);
                            if (artBitmap is null)
                            {
                                continue;
                            }

                            using (Bitmap bit = new Bitmap(artBitmap))
                            {
                                bit.Save(fileName, imageFormat);
                            }
                        }
                    }
                }

                FileSavedDialog.Show(FindForm(), dialog.SelectedPath, "All items saved successfully.");
            }
        }

        private void OnClickPreLoad(object sender, EventArgs e)
        {
            if (PreLoader.IsBusy)
            {
                return;
            }

            ProgressBar.Minimum = 1;
            ProgressBar.Maximum = _itemList.Count;
            ProgressBar.Step = 1;
            ProgressBar.Value = 1;
            ProgressBar.Visible = true;
            PreLoader.RunWorkerAsync();
        }

        private void PreLoaderDoWork(object sender, DoWorkEventArgs e)
        {
            int total = _itemList.Count;
            int reportEvery = Math.Max(1, total / 200);
            int sinceReport = 0;
            int done = 0;
            foreach (int item in _itemList)
            {
                Art.GetStatic(item);
                ++done;
                if (++sinceReport >= reportEvery)
                {
                    sinceReport = 0;
                    PreLoader.ReportProgress(done);
                }
            }
            PreLoader.ReportProgress(done);
        }

        private void PreLoaderProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            ProgressBar.Value = Math.Min(ProgressBar.Maximum, Math.Max(ProgressBar.Minimum, e.ProgressPercentage));
        }

        private void PreLoaderCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            ProgressBar.Visible = false;
        }

        private void ItemsTileView_DrawItem(object sender, TileViewControl.DrawTileListItemEventArgs e)
        {
            if (IsAncestorSiteInDesignMode || FormsDesignerHelper.IsInDesignMode())
            {
                return;
            }

            Point itemPoint = new Point(e.Bounds.X + ItemsTileView.TilePadding.Left, e.Bounds.Y + ItemsTileView.TilePadding.Top);

            Rectangle rect = new Rectangle(itemPoint, ItemsTileView.TileSize);

            using var previousClip = e.Graphics.Clip;

            using var clipRegion = new Region(rect);
            e.Graphics.Clip = clipRegion;

            var selected = ItemsTileView.SelectedIndices.Contains(e.Index);
            if (!selected)
            {
                e.Graphics.Clear(Options.PreviewBackgroundColor);
            }

            var bitmap = Art.GetStatic(_itemList[e.Index], out bool patched);
            if (bitmap == null)
            {
                rect.X += 5;
                rect.Y += 5;

                rect.Width -= 10;
                rect.Height -= 10;

                e.Graphics.FillRectangle(Brushes.Red, rect);
                e.Graphics.Clip = previousClip;
            }
            else
            {
                if (patched && !selected)
                {
                    e.Graphics.FillRectangle(Brushes.LightCoral, rect);
                }

                // Apply hue if preview is set
                Bitmap displayBitmap = bitmap;
                Bitmap hueBit = null;
                if (_previewHue >= 0 && _itemList[e.Index] < TileData.ItemTable.Length)
                {
                    ItemData item = TileData.ItemTable[_itemList[e.Index]];
                    bool usePartialHue = (item.Flags & TileFlag.PartialHue) != 0;
                    hueBit = new Bitmap(bitmap);
                    Hue hue = Hues.List[_previewHue];
                    hue.ApplyTo(hueBit, usePartialHue);
                    displayBitmap = hueBit;
                }

                if (Options.ArtItemClip)
                {
                    e.Graphics.DrawImage(displayBitmap, itemPoint);
                }
                else
                {
                    int width = displayBitmap.Width;
                    int height = displayBitmap.Height;
                    if (width > ItemsTileView.TileSize.Width)
                    {
                        width = ItemsTileView.TileSize.Width;
                        height = ItemsTileView.TileSize.Height * displayBitmap.Height / displayBitmap.Width;
                    }

                    if (height > ItemsTileView.TileSize.Height)
                    {
                        height = ItemsTileView.TileSize.Height;
                        width = ItemsTileView.TileSize.Width * displayBitmap.Width / displayBitmap.Height;
                    }

                    e.Graphics.DrawImage(displayBitmap, new Rectangle(itemPoint, new Size(width, height)));
                }

                hueBit?.Dispose();
                e.Graphics.Clip = previousClip;
            }
        }

        private void ItemsTileView_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            if (!e.IsSelected)
            {
                return;
            }

            UpdateSelection(e.ItemIndex);
        }

        private void ItemsTileView_FocusSelectionChanged(object sender, TileViewControl.ListViewFocusedItemSelectionChangedEventArgs e)
        {
            if (!e.IsFocused)
            {
                return;
            }

            UpdateSelection(e.FocusedItemIndex);
        }

        /// <summary>
        /// Resolves the current tile selection to a sorted list of graphic IDs.
        /// </summary>
        private List<int> GetSelectedGraphicIds()
        {
            var ids = new List<int>();
            foreach (int idx in ItemsTileView.SelectedIndices)
            {
                if (idx >= 0 && idx < _itemList.Count)
                {
                    ids.Add(_itemList[idx]);
                }
            }
            ids.Sort();
            return ids;
        }

        private void UpdateSelection(int itemIndex)
        {
            if (_itemList.Count == 0)
            {
                return;
            }

            SelectedGraphicId = itemIndex < 0 || itemIndex > _itemList.Count
                ? _itemList[0]
                : _itemList[itemIndex];
        }

        public void ItemsTileView_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (ItemsTileView.SelectedIndices.Count == 0)
            {
                return;
            }

            ItemDetailForm f = new ItemDetailForm(_itemList[ItemsTileView.SelectedIndices[0]])
            {
                TopMost = true
            };
            f.Show();
        }

        private void ItemsTileView_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyData == Keys.PageDown || e.KeyData == Keys.PageUp)
            {
                _scrolling = true;
            }
        }

        private void ItemsTileView_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyData != Keys.PageDown && e.KeyData != Keys.PageUp)
            {
                return;
            }

            _scrolling = false;

            if (ItemsTileView.FocusIndex > 0)
            {
                UpdateToolStripLabels(_selectedGraphicId);
                UpdateDetail(_selectedGraphicId);
            }
        }

        private const int _maleGumpOffset = 50_000;
        private const int _femaleGumpOffset = 60_000;

        private static void SelectInGumpsTab(int graphicId, bool female = false)
        {
            int gumpOffset = female ? _femaleGumpOffset : _maleGumpOffset;
            var itemData = TileData.ItemTable[graphicId];

            GumpControl.Select(itemData.Animation + gumpOffset);
        }

        private void SelectInGumpsTabMaleToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (SelectedGraphicId <= 0)
            {
                return;
            }

            SelectInGumpsTab(SelectedGraphicId);
        }

        private void SelectInGumpsTabFemaleToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (SelectedGraphicId <= 0)
            {
                return;
            }

            SelectInGumpsTab(SelectedGraphicId, true);
        }

        private void TileViewContextMenuStrip_Opening(object sender, CancelEventArgs e)
        {
            int selectedCount = ItemsTileView.SelectedIndices.Count;
            removeToolStripMenuItem.Text = selectedCount > 1 ? $"Remove {selectedCount}" : "Remove";
            extractToolStripMenuItem.Text = selectedCount > 1 ? $"Export {selectedCount} Images..." : "Export Image..";
            replaceToolStripMenuItem.Text = selectedCount > 1 ? $"Replace {selectedCount}..." : "Replace...";

            if (SelectedGraphicId <= 0)
            {
                selectInGumpsTabMaleToolStripMenuItem.Enabled = false;
                selectInGumpsTabFemaleToolStripMenuItem.Enabled = false;
            }
            else
            {
                var itemData = TileData.ItemTable[SelectedGraphicId];

                if (itemData.Animation > 0)
                {
                    selectInGumpsTabMaleToolStripMenuItem.Enabled =
                        GumpControl.HasGumpId(itemData.Animation + _maleGumpOffset);

                    selectInGumpsTabFemaleToolStripMenuItem.Enabled =
                        GumpControl.HasGumpId(itemData.Animation + _femaleGumpOffset);
                }
                else
                {
                    selectInGumpsTabMaleToolStripMenuItem.Enabled = false;
                    selectInGumpsTabFemaleToolStripMenuItem.Enabled = false;
                }
            }
        }

        private void ReplaceStartingFromText_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
            {
                return;
            }

            if (!Utils.ConvertStringToInt(ReplaceStartingFromText.Text, out int index, 0, Art.GetMaxItemId()))
            {
                return;
            }

            TileViewContextMenuStrip.Close();

            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Multiselect = true;
                dialog.Title = $"Choose image file replace starting at 0x{index:X}";
                dialog.CheckFileExists = true;
                dialog.Filter = "Image files (*.tif;*.tiff;*.bmp;*.png)|*.tif;*.tiff;*.bmp;*.png";

                if (dialog.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                for (int i = 0; i < dialog.FileNames.Length; i++)
                {
                    var currentIdx = index + i;

                    if (IsIndexValid(currentIdx))
                    {
                        AddSingleItem(dialog.FileNames[i], currentIdx);
                    }
                }

                ItemsTileView.VirtualListSize = _itemList.Count;
                ItemsTileView.Invalidate();

                SelectedGraphicId = index;

                UpdateToolStripLabels(index);
                UpdateDetail(index);
            }
        }

        /// <summary>
        /// Adds a single static item.
        /// </summary>
        /// <param name="fileName">Filename of the image to add.</param>
        /// <param name="index">Index where the static item will be added.</param>
        private void AddSingleItem(string fileName, int index)
        {
            using (var bmpTemp = new Bitmap(fileName))
            {
                Bitmap bitmap = new Bitmap(bmpTemp);

                if (fileName.Contains(".bmp"))
                {
                    bitmap = Utils.ConvertBmp(bitmap);
                }

                Art.ReplaceStatic(index, bitmap);

                ControlEvents.FireItemChangeEvent(this, index);

                Options.ChangedUltimaClass["Art"] = true;

                if (_showFreeSlots)
                {
                    SelectedGraphicId = index;

                    UpdateToolStripLabels(index);
                    UpdateDetail(index);
                }
                else
                {
                    bool done = false;

                    for (int i = 0; i < _itemList.Count; ++i)
                    {
                        if (index > _itemList[i])
                        {
                            continue;
                        }

                        _itemList[i] = index;

                        done = true;

                        break;
                    }

                    if (!done)
                    {
                        _itemList.Add(index);
                    }

                    ItemsTileView.VirtualListSize = _itemList.Count;
                    ItemsTileView.Invalidate();

                    SelectedGraphicId = index;

                    UpdateToolStripLabels(index);
                    UpdateDetail(index);
                }
            }
        }

        /// <summary>
        /// Check if it's valid index for land tile. Land tiles has fixed size 0x4000.
        /// </summary>
        /// <param name="index">Starting Index</param>
        private static bool IsIndexValid(int index)
        {
            return index >= 0 && index <= Art.GetMaxItemId();
        }

        private void OnClickReplaceFromFolder(object sender, EventArgs e)
        {
            using FolderBrowserDialog dialog = new FolderBrowserDialog();
            dialog.Description = "Select folder containing images to replace";

            if (dialog.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            string[] allFiles = Directory.GetFiles(dialog.SelectedPath);
            var replacedLines = new List<string>();
            var skippedLines = new List<string>();

            foreach (string file in allFiles)
            {
                string ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext != ".bmp" && ext != ".jpg" && ext != ".jpeg" && ext != ".png" && ext != ".tif" && ext != ".tiff")
                {
                    continue;
                }

                string name = Path.GetFileName(file);
                Match match = _hexIndexRegex.Match(Path.GetFileNameWithoutExtension(file));
                if (!match.Success)
                {
                    skippedLines.Add($"  {name}  (no hex ID in filename)");
                    continue;
                }

                int index;
                try
                {
                    index = Convert.ToInt32(match.Value, 16);
                }
                catch
                {
                    skippedLines.Add($"  {name}  (invalid hex value)");
                    continue;
                }

                if (!IsIndexValid(index))
                {
                    skippedLines.Add($"  {name}  (index 0x{index:X} out of range)");
                    continue;
                }

                try
                {
                    AddSingleItem(file, index);
                    replacedLines.Add($"  0x{index:X4}  {name}");
                }
                catch
                {
                    skippedLines.Add($"  {name}  (failed to load image)");
                }
            }

            ItemsTileView.VirtualListSize = _itemList.Count;
            ItemsTileView.Invalidate();

            var sb = new StringBuilder();
            sb.AppendLine($"Replaced: {replacedLines.Count}    Skipped: {skippedLines.Count}");

            if (replacedLines.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"Replaced ({replacedLines.Count}):");
                foreach (string line in replacedLines)
                {
                    sb.AppendLine(line);
                }
            }

            if (skippedLines.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"Skipped ({skippedLines.Count}):");
                foreach (string line in skippedLines)
                {
                    sb.AppendLine(line);
                }
            }

            using var resultForm = new ReplaceFromFolderResultForm(sb.ToString());
            resultForm.ShowDialog(this);
        }

        private void SearchByIdToolStripTextBox_KeyUp(object sender, KeyEventArgs e)
        {
            if (!Utils.ConvertStringToInt(searchByIdToolStripTextBox.Text, out int indexValue))
            {
                return;
            }

            var maximumIndex = Art.GetMaxItemId();

            if (indexValue < 0)
            {
                indexValue = 0;
            }

            if (indexValue > maximumIndex)
            {
                indexValue = maximumIndex;
            }

            // we have to invalidate focus so it will scroll to item
            ItemsTileView.FocusIndex = -1;
            SelectedGraphicId = indexValue;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.F3 || keyData == (Keys.F3 | Keys.Shift))
            {
                if (searchByNameToolStripTextBox.TextBox.Focused)
                {
                    return false;
                }

                if (!string.IsNullOrEmpty(searchByNameToolStripTextBox.Text))
                {
                    if (keyData == Keys.F3)
                    {
                        SearchName(searchByNameToolStripTextBox.Text, true);
                    }
                    else
                    {
                        SearchNamePrevious(searchByNameToolStripTextBox.Text);
                    }
                }
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        public static bool SearchNamePrevious(string name)
        {
            var searchMethod = SearchHelper.GetSearchMethod();

            int index = RefMarker._itemList.Count - 1;
            if (RefMarker._selectedGraphicId >= 0)
            {
                index = RefMarker._itemList.IndexOf(RefMarker._selectedGraphicId) - 1;
                if (index < 0)
                {
                    index = RefMarker._itemList.Count - 1;
                }
            }

            // First pass: search from current index down to 0
            for (int i = index; i >= 0; --i)
            {
                int itemId = RefMarker._itemList[i];
                var item = TileData.ItemTable[itemId];
                string searchValue = GetSearchableValue(item);

                var searchResult = searchMethod(name, searchValue);
                if (searchResult.HasErrors)
                {
                    break;
                }

                if (!searchResult.EntryFound)
                {
                    continue;
                }

                RefMarker.ItemsTileView.FocusIndex = -1;
                RefMarker.SelectedGraphicId = itemId;
                return true;
            }

            // Second pass: if we didn't find anything, wrap and search from the end
            if (index < RefMarker._itemList.Count - 1)
            {
                for (int i = RefMarker._itemList.Count - 1; i > index; --i)
                {
                    int itemId = RefMarker._itemList[i];
                    var item = TileData.ItemTable[itemId];
                    string searchValue = GetSearchableValue(item);

                    var searchResult = searchMethod(name, searchValue);
                    if (searchResult.HasErrors)
                    {
                        break;
                    }

                    if (!searchResult.EntryFound)
                    {
                        continue;
                    }

                    RefMarker.ItemsTileView.FocusIndex = -1;
                    RefMarker.SelectedGraphicId = itemId;
                    return true;
                }
            }

            return false;
        }

        private void SearchByNameToolStripTextBox_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F3)
            {
                if (e.Shift)
                {
                    SearchNamePrevious(searchByNameToolStripTextBox.Text);
                }
                else
                {
                    SearchName(searchByNameToolStripTextBox.Text, true);
                }
                return;
            }

            // If dynamic search is enabled, apply filter as the user types
            if (dynamicItemSearchToolStripMenuItem?.Checked == true)
            {
                ApplyFilter(searchByNameToolStripTextBox.Text);
            }
            else
            {
                SearchName(searchByNameToolStripTextBox.Text, false);
            }
        }

        private void SearchByNameToolStripButton_Click(object sender, EventArgs e)
        {
            SearchName(searchByNameToolStripTextBox.Text, true);
        }

        private void SearchByNamePrevToolStripButton_Click(object sender, EventArgs e)
        {
            SearchNamePrevious(searchByNameToolStripTextBox.Text);
        }
    }
}
