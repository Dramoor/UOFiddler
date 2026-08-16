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
using System.Text;
using System.Windows.Forms;
using System.Linq;
using Ultima;
using UoFiddler.Controls.Classes;
using UoFiddler.Controls.Forms;
using UoFiddler.Controls.Helpers;
using UoFiddler.Controls.UserControls.TileView;
using System.Reflection;

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

            InitializeFilterMenu();
        }

        private List<int> _itemList = new List<int>();
        // full unfiltered list of items (used when dynamic filtering is enabled)
        private List<int> _allItemList = new List<int>();
        private bool _showFreeSlots;

        // Item flag filtering
        private TileFlag _selectedItemFlags = TileFlag.None;

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

        private SearchType _currentSearchType = SearchType.Name;

        private int _selectedGraphicId = -1;

        public int SelectedGraphicId
        {
            get => _selectedGraphicId;
            set
            {
                _selectedGraphicId = value < 0 ? 0 : value;
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
        /// Initializes the filter menu with only flags that are actually used in items
        /// </summary>
        private void InitializeFilterMenu()
        {
            try
            {
                filterToolStripMenuItem.DropDownItems.Clear();

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

        /// <summary>
        /// Updates if TileSize is changed
        /// </summary>
        public void UpdateTileView()
        {
            var newSize = new Size(Options.ArtItemSizeWidth, Options.ArtItemSizeHeight);

            ItemsTileView.TileBorderColor = Options.RemoveTileBorder
                ? Color.Transparent
                : Color.Gray;

            if (Options.OverrideBackgroundColorFromTile)
            {
                ItemsTileView.BackColor = _backgroundColorItem;
            }

            var sameTileSize = ItemsTileView.TileSize == newSize;
            var sameFocusColor = ItemsTileView.TileFocusColor == Options.TileFocusColor;
            var sameSelectionColor = ItemsTileView.TileHighlightColor == Options.TileSelectionColor;
            if (sameTileSize && sameFocusColor && sameSelectionColor)
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
        /// Searches graphic number and selects it
        /// </summary>
        /// <param name="graphic"></param>
        /// <returns></returns>
        public static bool SearchGraphic(int graphic)
        {
            if (!RefMarker.IsLoaded)
            {
                RefMarker.OnLoad(RefMarker, EventArgs.Empty);
            }

            if (RefMarker._itemList.TrueForAll(t => t != graphic))
            {
                return false;
            }
            // we have to invalidate focus so it will scroll to item
            RefMarker.ItemsTileView.FocusIndex = -1;
            RefMarker.SelectedGraphicId = graphic;

            return true;
        }

        /// <summary>
        /// Searches for items by layer and selects the next/previous matching item
        /// </summary>
        /// <param name="layer">layer number to search for</param>
        /// <param name="next">true = search forward, false = search backward</param>
        /// <returns>true if found</returns>
        public static bool SearchByLayer(int layer, bool next)
        {
            if (!RefMarker.IsLoaded)
            {
                RefMarker.OnLoad(RefMarker, EventArgs.Empty);
            }

            var count = RefMarker._itemList.Count;
            if (count == 0)
            {
                return false;
            }

            int start = RefMarker._selectedGraphicId >= 0 ? RefMarker._itemList.IndexOf(RefMarker._selectedGraphicId) : 0;

            for (int k = 1; k <= count; ++k)
            {
                int i = next ? (start + k) % count : (start - k) % count;
                if (i < 0) i += count;

                var id = RefMarker._itemList[i];
                var item = TileData.ItemTable[id];

                // Check if item matches flag filters
                if (RefMarker._selectedItemFlags != TileFlag.None && 
                    (item.Flags & RefMarker._selectedItemFlags) == 0)
                {
                    continue; // Item doesn't have any of the selected flags
                }

                // For ItemData, 'Quality' stores the layer for wearable items
                // Only consider items that are wearable, weapon or armor
                var relevantFlags = TileFlag.Wearable | TileFlag.Weapon | TileFlag.Armor;
                if (item.Quality == layer && (item.Flags & relevantFlags) != 0)
                {
                    RefMarker.ItemsTileView.FocusIndex = -1;
                    RefMarker.SelectedGraphicId = id;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Searches for name and selects
        /// </summary>
        /// <param name="name"></param>
        /// <param name="next">starting from current selected</param>
        /// <returns></returns>
        public static bool SearchName(string name, bool next, bool fromStart = false)
        {
            if (!RefMarker.IsLoaded)
            {
                RefMarker.OnLoad(RefMarker, EventArgs.Empty);
            }

            var count = RefMarker._itemList.Count;
            if (count == 0)
            {
                return false;
            }

            var searchMethod = SearchHelper.GetSearchMethod();

            // Determine start index (current selection). If fromStart==true or nothing selected, start before first item (-1)
            int start;
            if (fromStart)
            {
                start = -1;
            }
            else
            {
                start = RefMarker._selectedGraphicId >= 0 ? RefMarker._itemList.IndexOf(RefMarker._selectedGraphicId) : -1;
            }

            // Cycle through the list once in the requested direction (next or previous)
            for (int k = 1; k <= count; ++k)
            {
                int i = next ? (start + k) % count : (start - k) % count;
                if (i < 0) i += count;

                var id = RefMarker._itemList[i];

                // Check if item matches flag filters
                if (RefMarker._selectedItemFlags != TileFlag.None && 
                    (TileData.ItemTable[id].Flags & RefMarker._selectedItemFlags) == 0)
                {
                    continue; // Item doesn't have any of the selected flags
                }

                var searchResult = searchMethod(name, TileData.ItemTable[id].Name);
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
                RefMarker.SelectedGraphicId = id;

                return true;
            }

            return false;
        }

        /// <summary>
        /// Searches for items by animation ID and selects the next/previous matching item
        /// </summary>
        /// <param name="animation">animation ID to search for</param>
        /// <param name="next">true = search forward, false = search backward</param>
        /// <returns>true if found</returns>
        public static bool SearchByAnimation(int animation, bool next)
        {
            if (!RefMarker.IsLoaded)
            {
                RefMarker.OnLoad(RefMarker, EventArgs.Empty);
            }

            var count = RefMarker._itemList.Count;
            if (count == 0)
            {
                return false;
            }

            int start = RefMarker._selectedGraphicId >= 0 ? RefMarker._itemList.IndexOf(RefMarker._selectedGraphicId) : 0;

            for (int k = 1; k <= count; ++k)
            {
                int i = next ? (start + k) % count : (start - k) % count;
                if (i < 0) i += count;

                var id = RefMarker._itemList[i];
                var item = TileData.ItemTable[id];

                // Check if item matches flag filters
                if (RefMarker._selectedItemFlags != TileFlag.None && 
                    (item.Flags & RefMarker._selectedItemFlags) == 0)
                {
                    continue; // Item doesn't have any of the selected flags
                }

                if (item.Animation == animation)
                {
                    RefMarker.ItemsTileView.FocusIndex = -1;
                    RefMarker.SelectedGraphicId = id;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Searches for items by weight and selects the next/previous matching item
        /// </summary>
        /// <param name="weight">weight value to search for</param>
        /// <param name="next">true = search forward, false = search backward</param>
        /// <returns>true if found</returns>
        public static bool SearchByWeight(int weight, bool next)
        {
            if (!RefMarker.IsLoaded)
            {
                RefMarker.OnLoad(RefMarker, EventArgs.Empty);
            }

            var count = RefMarker._itemList.Count;
            if (count == 0)
            {
                return false;
            }

            int start = RefMarker._selectedGraphicId >= 0 ? RefMarker._itemList.IndexOf(RefMarker._selectedGraphicId) : 0;

            for (int k = 1; k <= count; ++k)
            {
                int i = next ? (start + k) % count : (start - k) % count;
                if (i < 0) i += count;

                var id = RefMarker._itemList[i];
                var item = TileData.ItemTable[id];

                // Check if item matches flag filters
                if (RefMarker._selectedItemFlags != TileFlag.None && 
                    (item.Flags & RefMarker._selectedItemFlags) == 0)
                {
                    continue; // Item doesn't have any of the selected flags
                }

                if (item.Weight == weight)
                {
                    RefMarker.ItemsTileView.FocusIndex = -1;
                    RefMarker.SelectedGraphicId = id;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Searches for items by stack offset and selects the next/previous matching item
        /// </summary>
        /// <param name="stackOffset">stack offset value to search for</param>
        /// <param name="next">true = search forward, false = search backward</param>
        /// <returns>true if found</returns>
        public static bool SearchByStackOffset(int stackOffset, bool next)
        {
            if (!RefMarker.IsLoaded)
            {
                RefMarker.OnLoad(RefMarker, EventArgs.Empty);
            }

            var count = RefMarker._itemList.Count;
            if (count == 0)
            {
                return false;
            }

            int start = RefMarker._selectedGraphicId >= 0 ? RefMarker._itemList.IndexOf(RefMarker._selectedGraphicId) : 0;

            for (int k = 1; k <= count; ++k)
            {
                int i = next ? (start + k) % count : (start - k) % count;
                if (i < 0) i += count;

                var id = RefMarker._itemList[i];
                var item = TileData.ItemTable[id];

                // Check if item matches flag filters
                if (RefMarker._selectedItemFlags != TileFlag.None && 
                    (item.Flags & RefMarker._selectedItemFlags) == 0)
                {
                    continue; // Item doesn't have any of the selected flags
                }

                if (item.StackingOffset == stackOffset)
                {
                    RefMarker.ItemsTileView.FocusIndex = -1;
                    RefMarker.SelectedGraphicId = id;
                    return true;
                }
            }

            return false;
        }

        public static bool SearchByHeight(int height, bool next)
        {
            if (!RefMarker.IsLoaded)
            {
                RefMarker.OnLoad(RefMarker, EventArgs.Empty);
            }

            var count = RefMarker._itemList.Count;
            if (count == 0)
            {
                return false;
            }

            int start = RefMarker._selectedGraphicId >= 0 ? RefMarker._itemList.IndexOf(RefMarker._selectedGraphicId) : 0;

            for (int k = 1; k <= count; ++k)
            {
                int i = next ? (start + k) % count : (start - k) % count;
                if (i < 0) i += count;

                var id = RefMarker._itemList[i];
                var item = TileData.ItemTable[id];

                // Check if item matches flag filters
                if (RefMarker._selectedItemFlags != TileFlag.None && 
                    (item.Flags & RefMarker._selectedItemFlags) == 0)
                {
                    continue; // Item doesn't have any of the selected flags
                }

                if (item.Height == height)
                {
                    RefMarker.ItemsTileView.FocusIndex = -1;
                    RefMarker.SelectedGraphicId = id;
                    return true;
                }
            }

            return false;
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

            Cursor.Current = Cursors.WaitCursor;
            Options.LoadedUltimaClass["TileData"] = true;
            Options.LoadedUltimaClass["Art"] = true;
            Options.LoadedUltimaClass["Animdata"] = true;
            Options.LoadedUltimaClass["Hues"] = true;

            if (!IsLoaded) // only once
            {
                Plugin.PluginEvents.FireModifyItemShowContextMenuEvent(TileViewContextMenuStrip);
            }

            // Initialize visibility of layer search controls based on misc menu setting
            try
            {
                // Legacy layer search controls removed - search types now handled via menu
            }
            catch
            {
                // ignore when designer context
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

            // keep a copy of the full unfiltered list so we can restore it after dynamic filtering
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
            }

            IsLoaded = true;
            Cursor.Current = Cursors.Default;
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
            // keep master list in sync
            if (Art.IsValidStatic(index))
            {
                bool doneAll = false;
                for (int i = 0; i < _allItemList.Count; ++i)
                {
                    if (index < _allItemList[i])
                    {
                        _allItemList.Insert(i, index);
                        doneAll = true;
                        break;
                    }

                    if (index != _allItemList[i])
                    {
                        continue;
                    }

                    doneAll = true;
                    break;
                }

                if (!doneAll)
                {
                    _allItemList.Add(index);
                }
            }
            else
            {
                _allItemList.Remove(index);
            }

            // if dynamic searching is enabled, reapply current filter
            try
            {
                if (dynamicItemSearchToolStripMenuItem != null && dynamicItemSearchToolStripMenuItem.Checked)
                {
                    ApplyNameFilter(searchByNameToolStripTextBox.Text);
                }
            }
            catch
            {
                // ignore when designer
            }


            ItemsTileView.VirtualListSize = _itemList.Count;
            ItemsTileView.Invalidate();
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

        private void ApplyFilter(string searchValue)
        {
            if (_allItemList == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(searchValue) && _selectedItemFlags == TileFlag.None)
            {
                // empty search and no filters → restore full list
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
                                (TileData.ItemTable[id].Flags & _selectedItemFlags) == 0)
                            {
                                continue; // Item doesn't have any of the selected flags
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
                                    (TileData.ItemTable[id].Flags & _selectedItemFlags) == 0)
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
                                    (TileData.ItemTable[id].Flags & _selectedItemFlags) == 0)
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
                                    (TileData.ItemTable[id].Flags & _selectedItemFlags) == 0)
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
                        if (int.TryParse(searchValue, out int stackOffset))
                        {
                            foreach (var id in _allItemList)
                            {
                                // Check if item matches flag filters
                                if (_selectedItemFlags != TileFlag.None && 
                                    (TileData.ItemTable[id].Flags & _selectedItemFlags) == 0)
                                {
                                    continue;
                                }

                                if (TileData.ItemTable[id].StackingOffset == stackOffset)
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
                                    (TileData.ItemTable[id].Flags & _selectedItemFlags) == 0)
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

        private Color _backgroundColorItem = Color.White;
        private static bool _didSimulateClilocInit = false;

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
            if (searchTypeHeightToolStripMenuItem != null)
            {
                searchTypeHeightToolStripMenuItem.Checked = false;
            }

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
        }

        private void ChangeBackgroundColorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (colorDialog.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            _backgroundColorItem = colorDialog.Color;

            if (Options.OverrideBackgroundColorFromTile)
            {
                ItemsTileView.BackColor = _backgroundColorItem;
            }

            ItemsTileView.Invalidate();
        }

        private Color _backgroundDetailColor = Color.White;

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
                    newGraph.Clear(_backgroundDetailColor);
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
                    newGraph.Clear(_backgroundDetailColor);
                    newGraph.DrawImage(bit, (DetailPictureBox.Size.Width - bit.Width) / 2, 5);
                }

                DetailPictureBox.Image?.Dispose();
                DetailPictureBox.Image = newBit;

                Art.Measure(bit, out xMin, out yMin, out xMax, out yMax);
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Name: {item.Name}");
            // Also show related Cliloc text for this item (use same calculation as TileData). Show only the text.
            try
            {
                int clilocNumberHeader = graphic < 0x4000 ? 1020000 + graphic : 1078872 + graphic;
                string clilocTextHeader = ClilocControl.GetStringFromLoaded(clilocNumberHeader);
                if (!string.IsNullOrWhiteSpace(clilocTextHeader))
                {
                    sb.AppendLine($"Cliloc: {clilocTextHeader}");
                }
            }
            catch
            {
                // ignore if cliloc cannot be loaded
            }
            sb.AppendLine($"Graphic: 0x{graphic:X4}({graphic})");
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

            if ((item.Flags & TileFlag.Animation) != 0)
            {
                Animdata.AnimdataEntry info = Animdata.GetAnimData(graphic);
                if (info != null)
                {
                    sb.AppendLine($"Animation FrameCount: {info.FrameCount} Interval: {info.FrameInterval}");
                }
            }

            DetailTextBox.Clear();
            // Append basic tile info
            DetailTextBox.AppendText(sb.ToString());

            // cliloc already included in the header above when available
        }

        private void ChangeBackgroundColorToolStripMenuItemDetail_Click(object sender, EventArgs e)
        {
            if (colorDialog.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            _backgroundDetailColor = colorDialog.Color;
            if (_selectedGraphicId != -1)
            {
                UpdateDetail(_selectedGraphicId);
            }
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

                    Art.ReplaceStatic(_selectedGraphicId, bitmap);

                    ControlEvents.FireItemChangeEvent(this, _selectedGraphicId);

                    ItemsTileView.Invalidate();
                    UpdateToolStripLabels(_selectedGraphicId);
                    UpdateDetail(_selectedGraphicId);

                    Options.ChangedUltimaClass["Art"] = true;
                }
            }
        }

        private void OnClickRemove(object sender, EventArgs e)
        {
            if (!Art.IsValidStatic(_selectedGraphicId))
            {
                return;
            }

            DialogResult result = MessageBox.Show($"Are you sure to remove 0x{_selectedGraphicId:X}", "Save",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
            if (result != DialogResult.Yes)
            {
                return;
            }

            Art.RemoveStatic(_selectedGraphicId);
            ControlEvents.FireItemChangeEvent(this, _selectedGraphicId);

            if (!_showFreeSlots)
            {
                _itemList.Remove(_selectedGraphicId);
                ItemsTileView.VirtualListSize = _itemList.Count;
                var moveToIndex = --_selectedGraphicId;
                SelectedGraphicId = moveToIndex <= 0 ? 0 : _selectedGraphicId; // TODO: get last index visible instead just curr -1
            }
            ItemsTileView.Invalidate();

            Options.ChangedUltimaClass["Art"] = true;
        }

        private void OnClickRemoveAll(object sender, EventArgs e)
        {
            // Check if multiple items are selected
            if (ItemsTileView.SelectedIndices.Count > 1)
            {
                // Multiple selection case
                int count = ItemsTileView.SelectedIndices.Count;
                DialogResult result = MessageBox.Show(
                    $"Are you sure you want to remove {count} selected tiles?",
                    "Remove All",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2);

                if (result != DialogResult.Yes)
                {
                    return;
                }

                // Remove all selected tiles
                var selectedIndices = new List<int>(ItemsTileView.SelectedIndices);
                foreach (var index in selectedIndices)
                {
                    if (Art.IsValidStatic(index))
                    {
                        Art.RemoveStatic(index);
                        ControlEvents.FireItemChangeEvent(this, index);

                        if (!_showFreeSlots)
                        {
                            _itemList.Remove(index);
                            _allItemList.Remove(index);
                        }
                    }
                }

                // Update UI
                ItemsTileView.VirtualListSize = _itemList.Count;
                ItemsTileView.SelectedIndices.Clear();
                ItemsTileView.Invalidate();
                UpdateDetail(-1);
                Options.ChangedUltimaClass["Art"] = true;
            }
            else if (_selectedGraphicId > 0)
            {
                // Single selection case - use existing Remove logic
                OnClickRemove(sender, e);
            }
        }

        private void OnTextChangedInsert(object sender, EventArgs e)
        {
            if (Utils.ConvertStringToInt(InsertText.Text, out int index, 0, Art.GetMaxItemId()))
            {
                InsertText.ForeColor = Art.IsValidStatic(index) ? Color.Red : Color.Black;
            }
            else
            {
                InsertText.ForeColor = Color.Red;
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
            // show hex and decimal together to ensure visibility
            GraphicLabel.Text = $"Graphic: 0x{graphic:X4} ({graphic})";
            if (DecimalGraphicLabel != null)
            {
                DecimalGraphicLabel.Text = string.Empty;
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

            Cursor.Current = Cursors.WaitCursor;
            ProgressBarDialog barDialog = new ProgressBarDialog(Art.GetIdxLength(), "Save");
            Art.Save(Options.OutputPath);
            barDialog.Dispose();
            Cursor.Current = Cursors.Default;
            Options.ChangedUltimaClass["Art"] = false;
            MessageBox.Show($"Saved to {Options.OutputPath}", "Save", MessageBoxButtons.OK, MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button1);
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
            ExportSelectedImages(ImageFormat.Bmp);
        }

        private void Extract_Image_ClickTiff(object sender, EventArgs e)
        {
            ExportSelectedImages(ImageFormat.Tiff);
        }

        private void Extract_Image_ClickJpg(object sender, EventArgs e)
        {
            ExportSelectedImages(ImageFormat.Jpeg);
        }

        private void Extract_Image_ClickPng(object sender, EventArgs e)
        {
            ExportSelectedImages(ImageFormat.Png);
        }

        private void ExportSelectedImages(ImageFormat imageFormat)
        {
            // If multi-select is enabled and there are selected indices, export all selected.
            var selected = ItemsTileView.SelectedIndices;

            if (ItemsTileView.MultiSelect && selected != null && selected.Count > 0)
            {
                Cursor.Current = Cursors.WaitCursor;
                int saved = 0;
                foreach (int tileIndex in selected)
                {
                    if (tileIndex < 0 || tileIndex >= _itemList.Count)
                    {
                        continue;
                    }

                    int graphic = _itemList[tileIndex];
                    if (!Art.IsValidStatic(graphic))
                    {
                        continue;
                    }

                    string fileExtension = Utils.GetFileExtensionFor(imageFormat);
                    string fileName = Path.Combine(Options.OutputPath, $"Item {graphic}.{fileExtension}");

                    using (Bitmap bit = new Bitmap(Art.GetStatic(graphic)))
                    {
                        bit.Save(fileName, imageFormat);
                    }

                    saved++;
                }

                Cursor.Current = Cursors.Default;

                MessageBox.Show($"{saved} item(s) saved to {Options.OutputPath}", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information,
                    MessageBoxDefaultButton.Button1);
            }
            else
            {
                if (_selectedGraphicId == -1)
                {
                    return;
                }

                ExportItemImage(_selectedGraphicId, imageFormat);
            }
        }

        private static void ExportItemImage(int index, ImageFormat imageFormat)
        {
            if (!Art.IsValidStatic(index))
            {
                return;
            }

            string fileExtension = Utils.GetFileExtensionFor(imageFormat);
            string fileName = Path.Combine(Options.OutputPath, $"Item {index}.{fileExtension}");

            using (Bitmap bit = new Bitmap(Art.GetStatic(index)))
            {
                bit.Save(fileName, imageFormat);
            }

            MessageBox.Show($"Item saved to {fileName}", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button1);
        }

        private void OnClickSelectTiledata(object sender, EventArgs e)
        {
            if (_selectedGraphicId >= 0)
            {
                TileDataControl.Select(_selectedGraphicId, false);
            }
        }

        private void OnClickSelectRadarCol(object sender, EventArgs e)
        {
            if (_selectedGraphicId >= 0)
            {
                RadarColorControl.Select(_selectedGraphicId, false);
            }
        }

        private void OnClickSelectAllTabs(object sender, EventArgs e)
        {
            if (_selectedGraphicId < 0)
            {
                return;
            }

            // Select in TileData and RadarColor
            TileDataControl.Select(_selectedGraphicId, false);
            RadarColorControl.Select(_selectedGraphicId, false);

            // Calculate cliloc number and select in Cliloc control
            try
            {
                int clilocNumber = _selectedGraphicId < 0x4000 ? 1020000 + _selectedGraphicId : 1078872 + _selectedGraphicId;

                // Only once after loading, simulate selecting the Cliloc tab (temporarily) so it initializes as if clicked.
                try
                {
                    if (!_didSimulateClilocInit && !ClilocControl.IsControlLoaded)
                    {
                        foreach (Form f in Application.OpenForms)
                        {
                            var tab = FindControlRecursive(f, typeof(TabControl)) as TabControl;
                            if (tab == null)
                            {
                                continue;
                            }

                            TabPage clilocPage = null;
                            foreach (TabPage p in tab.TabPages)
                            {
                                if (string.Equals(p.Name, "ClilocTab", StringComparison.Ordinal) ||
                                    (!string.IsNullOrEmpty(p.Text) && p.Text.IndexOf("CliLoc", StringComparison.OrdinalIgnoreCase) >= 0) ||
                                    (p.Tag is int tag && tag == 10))
                                {
                                    clilocPage = p;
                                    break;
                                }
                            }

                            if (clilocPage != null)
                            {
                                var previous = tab.SelectedTab;
                                try
                                {
                                    tab.SelectedTab = clilocPage; // simulate click
                                    Application.DoEvents();
                                }
                                finally
                                {
                                    // restore previous tab so UI stays where user was
                                    try
                                    {
                                        if (previous != null && tab.TabPages.Contains(previous))
                                        {
                                            tab.SelectedTab = previous;
                                            Application.DoEvents();
                                        }
                                    }
                                    catch
                                    {
                                        // ignore
                                    }
                                }

                                _didSimulateClilocInit = true;
                                break;
                            }
                        }
                    }
                }
                catch
                {
                    // ignore
                }

                // Ensure the Cliloc control is initialized in background without changing UI selection
                ClilocControl.EnsureLoaded();

                // Apply selection; pending selection is handled by ClilocControl
                ClilocControl.Select(clilocNumber);
                // Also attempt to select related gump (male) if available
                try
                {
                    var itemData = TileData.ItemTable[_selectedGraphicId];
                    if (itemData.Animation > 0)
                    {
                        int gumpId = itemData.Animation + _maleGumpOffset;
                        if (GumpControl.HasGumpId(gumpId))
                        {
                            SelectInGumpsTab(_selectedGraphicId);
                        }
                    }
                }
                catch
                {
                    // ignore gump selection errors
                }
            }
            catch
            {
                // ignore selection errors
            }
        }

        private static Control FindControlRecursive(Control parent, Type type)
        {
            if (parent == null)
            {
                return null;
            }

            if (parent.GetType() == type)
            {
                return parent;
            }

            foreach (Control c in parent.Controls)
            {
                var found = FindControlRecursive(c, type);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
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

                Cursor.Current = Cursors.WaitCursor;

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

                        string fileName = Path.Combine(dialog.SelectedPath, $"Item 0x{index:X4}.{fileExtension}");
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

                Cursor.Current = Cursors.Default;

                MessageBox.Show($"All items saved to {dialog.SelectedPath}", "Saved", MessageBoxButtons.OK,
                    MessageBoxIcon.Information, MessageBoxDefaultButton.Button1);
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
            foreach (int item in _itemList)
            {
                Art.GetStatic(item);
                PreLoader.ReportProgress(1);
            }
        }

        private void PreLoaderProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            ProgressBar.PerformStep();
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

            var previousClip = e.Graphics.Clip;

            e.Graphics.Clip = new Region(rect);

            var selected = ItemsTileView.SelectedIndices.Contains(e.Index);
            if (!selected)
            {
                e.Graphics.Clear(_backgroundColorItem);
            }

            var bitmap = Art.GetStatic(_itemList[e.Index], out bool patched);
            if (bitmap == null)
            {
                e.Graphics.Clip = new Region(rect);

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

                if (Options.ArtItemClip)
                {
                    e.Graphics.DrawImage(bitmap, itemPoint);
                }
                else
                {
                    int width = bitmap.Width;
                    int height = bitmap.Height;
                    if (width > ItemsTileView.TileSize.Width)
                    {
                        width = ItemsTileView.TileSize.Width;
                        height = ItemsTileView.TileSize.Height * bitmap.Height / bitmap.Width;
                    }

                    if (height > ItemsTileView.TileSize.Height)
                    {
                        height = ItemsTileView.TileSize.Height;
                        width = ItemsTileView.TileSize.Width * bitmap.Width / bitmap.Height;
                    }

                    e.Graphics.DrawImage(bitmap, new Rectangle(itemPoint, new Size(width, height)));
                }

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

        internal static void SelectItem(int graphicId)
        {
            if (!RefMarker.IsLoaded)
            {
                RefMarker.OnLoad(EventArgs.Empty);
            }

            RefMarker.SelectedGraphicId = graphicId;
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
            // Check if multiple items are selected
            bool hasMultipleSelections = ItemsTileView.SelectedIndices.Count > 1;
            bool hasSingleSelection = ItemsTileView.SelectedIndices.Count == 1;

            // Remove: only enabled for single selection
            removeToolStripMenuItem.Enabled = hasSingleSelection;

            // Remove All Selected: only enabled for multiple selections
            removeAllToolStripMenuItem.Enabled = hasMultipleSelections;

            // Disable single-item-only operations when multiple items are selected
            selectInAllTabsToolStripMenuItem.Enabled = !hasMultipleSelections;
            selectInTileDataTabToolStripMenuItem.Enabled = !hasMultipleSelections;
            selectInRadarColorTabToolStripMenuItem.Enabled = !hasMultipleSelections;
            replaceToolStripMenuItem.Enabled = !hasMultipleSelections;

            if (SelectedGraphicId <= 0 || hasMultipleSelections)
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

        /// <summary>
        /// Performs a search based on the current search type
        /// </summary>
        private void PerformSearchByCurrentType(string searchValue, bool next, bool fromStart = false)
        {
            if (string.IsNullOrWhiteSpace(searchValue))
            {
                return;
            }

            switch (_currentSearchType)
            {
                case SearchType.Name:
                    SearchName(searchValue, next, fromStart);
                    break;
                case SearchType.Animation:
                    if (int.TryParse(searchValue, out int animation))
                    {
                        SearchByAnimation(animation, next);
                    }
                    break;
                case SearchType.Weight:
                    if (int.TryParse(searchValue, out int weight))
                    {
                        SearchByWeight(weight, next);
                    }
                    break;
                case SearchType.Layer:
                    if (int.TryParse(searchValue, out int layer))
                    {
                        SearchByLayer(layer, next);
                    }
                    break;
                case SearchType.StackOffset:
                    if (int.TryParse(searchValue, out int stackOffset))
                    {
                        SearchByStackOffset(stackOffset, next);
                    }
                    break;
                case SearchType.Height:
                    if (int.TryParse(searchValue, out int height))
                    {
                        SearchByHeight(height, next);
                    }
                    break;
            }
        }

        private void SearchByNameToolStripTextBox_KeyUp(object sender, KeyEventArgs e)
        {
            // Start search forward from beginning when typing in the search box so initial match is the first matching item
            try
            {
                if (dynamicItemSearchToolStripMenuItem != null && dynamicItemSearchToolStripMenuItem.Checked)
                {
                    ApplyFilter(searchByNameToolStripTextBox.Text);
                    return;
                }
            }
            catch
            {
                // ignore in designer
            }

            PerformSearchByCurrentType(searchByNameToolStripTextBox.Text, next: true, fromStart: true);
        }
        private void SearchByNameToolStripButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (dynamicItemSearchToolStripMenuItem != null && dynamicItemSearchToolStripMenuItem.Checked)
                {
                    ApplyFilter(searchByNameToolStripTextBox.Text);
                    return;
                }
            }
            catch
            {
                // ignore in designer
            }

            PerformSearchByCurrentType(searchByNameToolStripTextBox.Text, next: true, fromStart: false);
        }

        private void LayerNextToolStripButton_Click(object sender, EventArgs e)
        {
            PerformSearchByCurrentType(searchByNameToolStripTextBox.Text, next: true, fromStart: false);
        }

        private void LayerPrevToolStripButton_Click(object sender, EventArgs e)
        {
            PerformSearchByCurrentType(searchByNameToolStripTextBox.Text, next: false, fromStart: false);
        }

        private void SearchByNamePrevToolStripButton_Click(object sender, EventArgs e)
        {
            PerformSearchByCurrentType(searchByNameToolStripTextBox.Text, next: false, fromStart: false);
        }

    }
}
