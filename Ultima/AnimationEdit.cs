using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace Ultima
{
    public sealed class AnimationEdit
    {
        // Dynamic animation file registry keyed by file type index
        private static Dictionary<int, FileIndex> _fileIndices = new Dictionary<int, FileIndex>();
        private static Dictionary<int, AnimIdx[]> _animCaches = new Dictionary<int, AnimIdx[]>();

        // Legacy fallback fields for backward compatibility
        private static FileIndex _fileIndex;
        private static FileIndex _fileIndex2;
        private static FileIndex _fileIndex3;
        private static FileIndex _fileIndex4;
        private static FileIndex _fileIndex5;
        private static FileIndex _fileIndex6;

        private static AnimIdx[] _animCache;
        private static AnimIdx[] _animCache2;
        private static AnimIdx[] _animCache3;
        private static AnimIdx[] _animCache4;
        private static AnimIdx[] _animCache5;
        private static AnimIdx[] _animCache6;

        private static bool _initialized = false;

        static AnimationEdit()
        {
            EnsureInitialized();
            InitializeCache();
        }

        /// <summary>
        /// Ensures legacy files are initialized (called before any access)
        /// </summary>
        private static void EnsureInitialized()
        {
            if (!_initialized)
            {
                _fileIndex = new FileIndex("Anim.idx", "Anim.mul", 6);
                _fileIndex2 = new FileIndex("Anim2.idx", "Anim2.mul", -1);
                _fileIndex3 = new FileIndex("Anim3.idx", "Anim3.mul", -1);
                _fileIndex4 = new FileIndex("Anim4.idx", "Anim4.mul", -1);
                _fileIndex5 = new FileIndex("Anim5.idx", "Anim5.mul", -1);
                _fileIndex6 = new FileIndex("Anim6.idx", "Anim6.mul", -1);

                // Add to dynamic registry with correct fileType mappings
                // fileType 1 = Anim.idx, fileType 2-6 = Anim2-6.idx
                _fileIndices[1] = _fileIndex;
                _fileIndices[2] = _fileIndex2;
                _fileIndices[3] = _fileIndex3;
                _fileIndices[4] = _fileIndex4;
                _fileIndices[5] = _fileIndex5;
                _fileIndices[6] = _fileIndex6;

                _initialized = true;
            }
        }

        private static void InitializeCache()
        {
            _legacyInitializeCache();
            InitializeDynamicCache();
        }

        private static void _legacyInitializeCache()
        {
            if (_fileIndex.IdxLength > 0)
            {
                _animCache = new AnimIdx[_fileIndex.IdxLength / 12];
            }

            if (_fileIndex2.IdxLength > 0)
            {
                _animCache2 = new AnimIdx[_fileIndex2.IdxLength / 12];
            }

            if (_fileIndex3.IdxLength > 0)
            {
                _animCache3 = new AnimIdx[_fileIndex3.IdxLength / 12];
            }

            if (_fileIndex4.IdxLength > 0)
            {
                _animCache4 = new AnimIdx[_fileIndex4.IdxLength / 12];
            }

            if (_fileIndex5.IdxLength > 0)
            {
                _animCache5 = new AnimIdx[_fileIndex5.IdxLength / 12];
            }

            if (_fileIndex6.IdxLength > 0)
            {
                _animCache6 = new AnimIdx[_fileIndex6.IdxLength / 12];
            }
        }

        private static void InitializeDynamicCache()
        {
            // Initialize caches for any dynamically discovered anim7+ files
            foreach (var kvp in _fileIndices)
            {
                int fileType = kvp.Key;
                FileIndex fileIndex = kvp.Value;

                // Skip legacy files (1-6) as they were already initialized above
                if (fileType >= 1 && fileType <= 6)
                    continue;

                if (fileIndex.IdxLength > 0)
                {
                    _animCaches[fileType] = new AnimIdx[fileIndex.IdxLength / 12];
                    System.Diagnostics.Debug.WriteLine($"AnimationEdit.InitializeDynamicCache: Initialized cache for fileType {fileType} with {fileIndex.IdxLength / 12} entries");
                }
            }
        }

        /// <summary>
        /// Rereads AnimX files from the Ultima data directory
        /// </summary>
        public static void Reload(string appDataPath = null, string profileName = null)
        {
            EnsureInitialized();

            // Dispose all dynamic file indices
            foreach (var fileIndex in _fileIndices.Values)
            {
                fileIndex?.Dispose();
            }
            _fileIndices.Clear();
            _animCaches.Clear();

            // Reinitialize legacy files
            _fileIndex = new FileIndex("Anim.idx", "Anim.mul", 6);
            _fileIndex2 = new FileIndex("Anim2.idx", "Anim2.mul", -1);
            _fileIndex3 = new FileIndex("Anim3.idx", "Anim3.mul", -1);
            _fileIndex4 = new FileIndex("Anim4.idx", "Anim4.mul", -1);
            _fileIndex5 = new FileIndex("Anim5.idx", "Anim5.mul", -1);
            _fileIndex6 = new FileIndex("Anim6.idx", "Anim6.mul", -1);

            // Add legacy files to dynamic registry with correct fileType mappings
            // fileType 1 = Anim.idx, fileType 2-6 = Anim2-6.idx
            _fileIndices[1] = _fileIndex;
            _fileIndices[2] = _fileIndex2;
            _fileIndices[3] = _fileIndex3;
            _fileIndices[4] = _fileIndex4;
            _fileIndices[5] = _fileIndex5;
            _fileIndices[6] = _fileIndex6;

            // Discover and initialize anim7+ files dynamically
            InitializeDynamicAnimFiles();

            InitializeCache();
        }

        /// <summary>
        /// Initializes FileIndex objects and caches for any dynamically discovered anim7+ files
        /// </summary>
        private static void InitializeDynamicAnimFiles()
        {
            var availableAnimFiles = Files.GetAvailableAnimFiles();

            // Iterate through all discovered files, only process those not already in the registry
            // (files 1-6 are already initialized as legacy files in _fileIndices)
            foreach (int fileType in availableAnimFiles)
            {
                if (!_fileIndices.ContainsKey(fileType))
                {
                    // Determine correct naming (fileType 1 = anim.idx, fileType 2+ = anim#.idx)
                    string idxFileName = fileType == 1 ? "Anim.idx" : $"Anim{fileType}.idx";
                    string mulFileName = fileType == 1 ? "Anim.mul" : $"Anim{fileType}.mul";

                    // Create FileIndex with default capacity like Anim3-6
                    var fileIndex = new FileIndex(idxFileName, mulFileName, -1);
                    _fileIndices[fileType] = fileIndex;
                }
            }
        }

        /// <summary>
        /// Returns a list of all available animation file type indices for editing
        /// This includes both legacy files (1-6) and any dynamically discovered anim7+ files
        /// </summary>
        public static System.Collections.Generic.IEnumerable<int> GetAvailableFileTypes()
        {
            EnsureInitialized();
            return _fileIndices.Keys;
        }

        private static void GetFileIndex(
                int body, int fileType, int action, int direction, out FileIndex fileIndex, out int index)
        {
            switch (fileType)
            {
                case 1:
                    fileIndex = _fileIndex;
                    if (body < 200)
                    {
                        index = body * 110;
                    }
                    else if (body < 400)
                    {
                        index = 22000 + ((body - 200) * 65);
                    }
                    else
                    {
                        index = 35000 + ((body - 400) * 175);
                    }

                    break;
                case 2:
                    fileIndex = _fileIndex2;
                    if (body < 200)
                    {
                        index = body * 110;
                    }
                    else
                    {
                        index = 22000 + ((body - 200) * 65);
                    }

                    break;
                case 3:
                    fileIndex = _fileIndex3;
                    if (body < 300)
                    {
                        index = body * 65;
                    }
                    else if (body < 400)
                    {
                        index = 33000 + ((body - 300) * 110);
                    }
                    else
                    {
                        index = 35000 + ((body - 400) * 175);
                    }

                    break;
                case 4:
                    fileIndex = _fileIndex4;
                    if (body < 200)
                    {
                        index = body * 110;
                    }
                    else if (body < 400)
                    {
                        index = 22000 + ((body - 200) * 65);
                    }
                    else
                    {
                        index = 35000 + ((body - 400) * 175);
                    }

                    break;
                case 5:
                    fileIndex = _fileIndex5;
                    if ((body < 200) && (body != 34))
                    {
                        index = body * 110;
                    }
                    else if (body < 400)
                    {
                        index = 22000 + ((body - 200) * 65);
                    }
                    else
                    {
                        index = 35000 + ((body - 400) * 175);
                    }

                    break;
                case 6:
                    fileIndex = _fileIndex6;
                    if (body < 200)
                    {
                        index = body * 110;
                    }
                    else if (body < 400)
                    {
                        index = 22000 + ((body - 200) * 65);
                    }
                    else
                    {
                        index = 35000 + ((body - 400) * 175);
                    }

                    break;
                default:
                    // For anim7+ files, use Anim2 settings
                    if (_fileIndices.TryGetValue(fileType, out FileIndex dynamicFileIndex))
                    {
                        fileIndex = dynamicFileIndex;
                    }
                    else
                    {
                        // Fallback to Anim2 if file not found
                        fileIndex = _fileIndex2;
                    }

                    // Apply Anim2 body range calculation for all anim7+ files
                    if (body < 200)
                    {
                        index = body * 110;
                    }
                    else
                    {
                        index = 22000 + ((body - 200) * 65);
                    }

                    break;
            }

            index += action * 5;

            if (direction <= 4)
            {
                index += direction;
            }
            else
            {
                index += direction - ((direction - 4) * 2);
            }
        }


        private static AnimIdx[] GetCache(int fileType)
        {
            // Try static legacy caches for backward compatibility
            switch (fileType)
            {
                case 0:
                    return _animCache;
                case 1:
                    return _animCache;
                case 2:
                    return _animCache2;
                case 3:
                    return _animCache3;
                case 4:
                    return _animCache4;
                case 5:
                    return _animCache5;
                case 6:
                    return _animCache6;
                default:
                    // For dynamic anim7+ files, use the dictionary
                    if (_animCaches.TryGetValue(fileType, out AnimIdx[] cache))
                    {
                        return cache;
                    }
                    // Cache not found - this shouldn't happen if Reload() was called
                    // Return null instead of wrong cache to catch the issue
                    System.Diagnostics.Debug.WriteLine($"Warning: Cache not found for fileType {fileType}");
                    return null;
            }
        }

        public static AnimIdx GetAnimation(int fileType, int body, int action, int dir)
        {
            AnimIdx[] cache = GetCache(fileType);

            GetFileIndex(body, fileType, action, dir, out FileIndex fileIndex, out int index);

            if (cache?[index] != null)
            {
                return cache[index];
            }

            return cache[index] = new AnimIdx(index, fileIndex);
        }

        public static bool IsActionDefined(int fileType, int body, int action)
        {
            // Reject actions beyond the body's physical idx block before computing
            // the index; otherwise index = base + action*5 crosses into the next
            // body's records. Replaces a prior off-by-one GetAnimLength check
            // (animCount < action) that both missed the boundary and used the
            // now-clamped category count.
            if (action < 0 || action >= Animations.GetActionCapacity(body, fileType))
            {
                return false;
            }

            AnimIdx[] cache = GetCache(fileType);
            if (cache == null)
            {
                // Cache not initialized - return false to be safe
                System.Diagnostics.Debug.WriteLine($"Warning: IsActionDefined called but cache is null for fileType {fileType}");
                return false;
            }

            GetFileIndex(body, fileType, action, 0, out FileIndex fileIndex, out int index);

            // Verify index is within cache bounds
            if (index < 0 || index >= cache.Length)
            {
                System.Diagnostics.Debug.WriteLine($"Warning: Animation index {index} out of bounds for cache of size {cache.Length} (fileType={fileType}, body={body}, action={action})");
                return false;
            }

            if (cache[index] != null)
            {
                return cache[index].Frames?.Count > 0;
            }

            bool valid = fileIndex.Valid(index, out int length, out int _, out bool _);

            return valid && length >= 1;
        }

        public static void LoadFromVD(int fileType, int body, BinaryReader bin)
        {
            AnimIdx[] cache = GetCache(fileType);
            GetFileIndex(body, fileType, 0, 0, out FileIndex _, out int index);
            int animLength = Animations.GetAnimLength(body, fileType) * 5;
            var entries = new Entry3D[animLength];

            for (int i = 0; i < animLength; ++i)
            {
                entries[i].Lookup = bin.ReadInt32();
                entries[i].Length = bin.ReadInt32();
                entries[i].Extra = bin.ReadInt32();
            }

            foreach (Entry3D entry in entries)
            {
                if ((entry.Lookup > 0) && (entry.Lookup < bin.BaseStream.Length) && (entry.Length > 0))
                {
                    bin.BaseStream.Seek(entry.Lookup, SeekOrigin.Begin);
                    cache[index] = new AnimIdx(bin, entry.Extra);
                }
                ++index;
            }
        }

        public static void ExportToVD(int fileType, int body, string file)
        {
            AnimIdx[] cache = GetCache(fileType);
            GetFileIndex(body, fileType, 0, 0, out FileIndex fileIndex, out int index);
            using (var fs = new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.Write))
            using (var bin = new BinaryWriter(fs))
            {
                bin.Write((short)6);
                int animLength = Animations.GetAnimLength(body, fileType);
                int currType = animLength == 22 ? 0 : animLength == 13 ? 1 : 2;
                bin.Write((short)currType);
                long indexPos = bin.BaseStream.Position;
                long animPos = bin.BaseStream.Position + (12 * animLength * 5);
                for (int i = index; i < index + (animLength * 5); i++)
                {
                    AnimIdx anim;
                    if (cache != null)
                    {
                        anim = cache[i] != null ? cache[i] : cache[i] = new AnimIdx(i, fileIndex);
                    }
                    else
                    {
                        anim = cache[i] = new AnimIdx(i, fileIndex);
                    }

                    if (anim == null)
                    {
                        bin.BaseStream.Seek(indexPos, SeekOrigin.Begin);
                        bin.Write(-1);
                        bin.Write(-1);
                        bin.Write(-1);
                        indexPos = bin.BaseStream.Position;
                    }
                    else
                    {
                        anim.ExportToVD(bin, ref indexPos, ref animPos);
                    }
                }
            }
        }

        public static void ExportToVDRemap(int fileType, int body, string file, int animType, int[] targetToSourceMap)
        {
            AnimIdx[] cache = GetCache(fileType);
            // Use GetFileIndex to get the correct FileIndex for dynamic anim files
            GetFileIndex(body, fileType, 0, 0, out FileIndex fileIndex, out int index);
            using (var fs = new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.Write))
            using (var bin = new BinaryWriter(fs))
            {
                bin.Write((short)6);
                int animLength = Animations.GetAnimLength(body, fileType);
                int currType;
                if (animType >= 0)
                {
                    currType = animType;
                }
                else
                {
                    currType = animLength == 22 ? 0 : animLength == 13 ? 1 : 2;
                }

                // Determine target animation length based on the target animation type
                int targetAnimLength = currType == 0 ? 22 : currType == 1 ? 13 : 35;

                bin.Write((short)currType);
                long indexPos = bin.BaseStream.Position;
                long animPos = bin.BaseStream.Position + (12 * targetAnimLength * 5);

                for (int i = 0; i < (targetAnimLength * 5); i++)
                {
                    int action = i / 5;
                    int directionOffset = i % 5;

                    if (targetToSourceMap == null || targetToSourceMap.Length != targetAnimLength)
                    {
                        // fallback to default behavior for this entry
                        int fallbackIndex = index + (action * 5) + directionOffset;
                        AnimIdx anim;
                        if (cache != null)
                        {
                            anim = cache[fallbackIndex] != null ? cache[fallbackIndex] : cache[fallbackIndex] = new AnimIdx(fallbackIndex, fileIndex);
                        }
                        else
                        {
                            anim = cache[fallbackIndex] = new AnimIdx(fallbackIndex, fileIndex);
                        }

                        if (anim == null)
                        {
                            bin.BaseStream.Seek(indexPos, SeekOrigin.Begin);
                            bin.Write(-1);
                            bin.Write(-1);
                            bin.Write(-1);
                            indexPos = bin.BaseStream.Position;
                        }
                        else
                        {
                            anim.ExportToVD(bin, ref indexPos, ref animPos);
                        }

                        continue;
                    }

                    int srcAction = targetToSourceMap[action];
                    if (srcAction < 0 || srcAction >= animLength)
                    {
                        // write empty entry
                        bin.BaseStream.Seek(indexPos, SeekOrigin.Begin);
                        bin.Write(-1);
                        bin.Write(-1);
                        bin.Write(-1);
                        indexPos = bin.BaseStream.Position;
                        continue;
                    }

                    int srcIndex = index + (srcAction * 5) + directionOffset;
                    AnimIdx srcAnim;
                    if (cache != null)
                    {
                        srcAnim = cache[srcIndex] != null ? cache[srcIndex] : cache[srcIndex] = new AnimIdx(srcIndex, fileIndex);
                    }
                    else
                    {
                        srcAnim = cache[srcIndex] = new AnimIdx(srcIndex, fileIndex);
                    }

                    if (srcAnim == null)
                    {
                        bin.BaseStream.Seek(indexPos, SeekOrigin.Begin);
                        bin.Write(-1);
                        bin.Write(-1);
                        bin.Write(-1);
                        indexPos = bin.BaseStream.Position;
                    }
                    else
                    {
                        srcAnim.ExportToVD(bin, ref indexPos, ref animPos);
                    }
                }
            }
        }

        public static void ExportToVDScaled(int fileType, int body, string file, int animType, float scale)
        {
            AnimIdx[] cache = GetCache(fileType);
            GetFileIndex(body, fileType, 0, 0, out FileIndex fileIndex, out int index);
            using (var fs = new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.Write))
            using (var bin = new BinaryWriter(fs))
            {
                bin.Write((short)6);
                int animLength = Animations.GetAnimLength(body, fileType);
                int currType;
                if (animType >= 0)
                {
                    currType = animType;
                }
                else
                {
                    currType = animLength == 22 ? 0 : animLength == 13 ? 1 : 2;
                }

                bin.Write((short)currType);
                long indexPos = bin.BaseStream.Position;
                long animPos = bin.BaseStream.Position + (12 * animLength * 5);

                for (int i = index; i < index + (animLength * 5); i++)
                {
                    AnimIdx anim;
                    if (cache != null)
                    {
                        anim = cache[i] != null ? cache[i] : cache[i] = new AnimIdx(i, fileIndex);
                    }
                    else
                    {
                        anim = cache[i] = new AnimIdx(i, fileIndex);
                    }

                    if (anim == null)
                    {
                        bin.BaseStream.Seek(indexPos, SeekOrigin.Begin);
                        bin.Write(-1);
                        bin.Write(-1);
                        bin.Write(-1);
                        indexPos = bin.BaseStream.Position;
                    }
                    else
                    {
                        anim.ExportToVDScaled(bin, ref indexPos, ref animPos, scale);
                    }
                }
            }
        }

        public static void ExportToVDRemapScaled(int fileType, int body, string file, int animType, int[] targetToSourceMap, float scale)
        {
            AnimIdx[] cache = GetCache(fileType);
            GetFileIndex(body, fileType, 0, 0, out FileIndex fileIndex, out int index);
            using (var fs = new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.Write))
            using (var bin = new BinaryWriter(fs))
            {
                bin.Write((short)6);
                int animLength = Animations.GetAnimLength(body, fileType);
                int currType;
                if (animType >= 0)
                {
                    currType = animType;
                }
                else
                {
                    currType = animLength == 22 ? 0 : animLength == 13 ? 1 : 2;
                }

                bin.Write((short)currType);
                long indexPos = bin.BaseStream.Position;
                long animPos = bin.BaseStream.Position + (12 * animLength * 5);

                for (int i = index; i < index + (animLength * 5); i++)
                {
                    int action = (i - index) / 5;
                    int directionOffset = (i - index) % 5;

                    if (targetToSourceMap == null || targetToSourceMap.Length != animLength)
                    {
                        // Fallback to default scaled behavior
                        AnimIdx anim;
                        if (cache != null)
                        {
                            anim = cache[i] != null ? cache[i] : cache[i] = new AnimIdx(i, fileIndex);
                        }
                        else
                        {
                            anim = cache[i] = new AnimIdx(i, fileIndex);
                        }

                        if (anim == null)
                        {
                            bin.BaseStream.Seek(indexPos, SeekOrigin.Begin);
                            bin.Write(-1);
                            bin.Write(-1);
                            bin.Write(-1);
                            indexPos = bin.BaseStream.Position;
                        }
                        else
                        {
                            anim.ExportToVDScaled(bin, ref indexPos, ref animPos, scale);
                        }
                    }
                    else
                    {
                        int sourceAction = targetToSourceMap[action];
                        if (sourceAction < 0)
                        {
                            // Empty entry
                            bin.BaseStream.Seek(indexPos, SeekOrigin.Begin);
                            bin.Write(-1);
                            bin.Write(-1);
                            bin.Write(-1);
                            indexPos = bin.BaseStream.Position;
                        }
                        else
                        {
                            int sourceIdx = index + (sourceAction * 5) + directionOffset;
                            AnimIdx anim;
                            if (cache != null)
                            {
                                anim = cache[sourceIdx] != null ? cache[sourceIdx] : cache[sourceIdx] = new AnimIdx(sourceIdx, fileIndex);
                            }
                            else
                            {
                                anim = cache[sourceIdx] = new AnimIdx(sourceIdx, fileIndex);
                            }

                            if (anim == null)
                            {
                                bin.BaseStream.Seek(indexPos, SeekOrigin.Begin);
                                bin.Write(-1);
                                bin.Write(-1);
                                bin.Write(-1);
                                indexPos = bin.BaseStream.Position;
                            }
                            else
                            {
                                anim.ExportToVDScaled(bin, ref indexPos, ref animPos, scale);
                            }
                        }
                    }
                }
            }
        }

        public static void Save(int fileType, string path)
        {
            string filename;
            AnimIdx[] cache;
            FileIndex fileIndex;

            // Try dynamic registry first (includes anim7+)
            if (_fileIndices.TryGetValue(fileType, out FileIndex dynamicFileIndex))
            {
                filename = fileType == 1 ? "anim" : $"anim{fileType}";

                // Try to get dynamic cache
                if (_animCaches.TryGetValue(fileType, out AnimIdx[] dynamicCache))
                {
                    cache = dynamicCache;
                }
                else
                {
                    // Fallback: shouldn't happen if initialized properly
                    System.Diagnostics.Debug.WriteLine($"Warning: No cache found for fileType {fileType}");
                    return;
                }

                fileIndex = dynamicFileIndex;
            }
            else
            {
                // Legacy hardcoded files (fallback for compatibility)
                switch (fileType)
                {
                    default:
                    case 1:
                        filename = "anim";
                        cache = _animCache;
                        fileIndex = _fileIndex;
                        break;
                    case 2:
                        filename = "anim2";
                        cache = _animCache2;
                        fileIndex = _fileIndex2;
                        break;
                    case 3:
                        filename = "anim3";
                        cache = _animCache3;
                        fileIndex = _fileIndex3;
                        break;
                    case 4:
                        filename = "anim4";
                        cache = _animCache4;
                        fileIndex = _fileIndex4;
                        break;
                    case 5:
                        filename = "anim5";
                        cache = _animCache5;
                        fileIndex = _fileIndex5;
                        break;
                    case 6:
                        filename = "anim6";
                        cache = _animCache6;
                        fileIndex = _fileIndex6;
                        break;
                }
            }

            string idx = Path.Combine(path, filename + ".idx");
            string mul = Path.Combine(path, filename + ".mul");

            using (var fsidx = new FileStream(idx, FileMode.Create, FileAccess.Write, FileShare.Write))
            using (var fsmul = new FileStream(mul, FileMode.Create, FileAccess.Write, FileShare.Write))
            using (var binidx = new BinaryWriter(fsidx))
            using (var binmul = new BinaryWriter(fsmul))
            {
                for (int idxc = 0; idxc < cache.Length; ++idxc)
                {
                    AnimIdx anim;
                    if (cache != null)
                    {
                        anim = cache[idxc] != null ? cache[idxc] : cache[idxc] = new AnimIdx(idxc, fileIndex);
                    }
                    else
                    {
                        anim = cache[idxc] = new AnimIdx(idxc, fileIndex);
                    }

                    if (anim == null)
                    {
                        binidx.Write(-1);
                        binidx.Write(-1);
                        binidx.Write(-1);
                    }
                    else
                    {
                        anim.Save(binmul, binidx);
                    }
                }
            }
        }
    }

    public sealed class AnimIdx
    {
        public readonly int PaletteCapacity = 0x100;

        private readonly int _idxExtra;

        public ushort[] Palette { get; private set; }
        public List<FrameEdit> Frames { get; private set; }

        public AnimIdx(int index, FileIndex fileIndex)
        {
            Palette = new ushort[PaletteCapacity];

            Stream stream = fileIndex.Seek(index, out int length, out int extra, out bool _);
            if ((stream == null) || (length < 1))
            {
                return;
            }

            _idxExtra = extra;

            // leaveOpen: stream is owned by the shared FileIndex; disposing the
            // BinaryReader must not close it, or the next FileIndex.Seek pays a
            // full re-open.
            using (var bin = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                for (int i = 0; i < PaletteCapacity; ++i)
                {
                    Palette[i] = (ushort)(bin.ReadUInt16() ^ 0x8000);
                }

                var start = (int)bin.BaseStream.Position;
                int frameCount = bin.ReadInt32();

                var lookups = new int[frameCount];

                for (int i = 0; i < frameCount; ++i)
                {
                    lookups[i] = start + bin.ReadInt32();
                }

                Frames = new List<FrameEdit>();

                for (int i = 0; i < frameCount; ++i)
                {
                    stream.Seek(lookups[i], SeekOrigin.Begin);
                    Frames.Add(new FrameEdit(bin));
                }
            }
        }

        public AnimIdx(BinaryReader bin, int extra)
        {
            _idxExtra = extra;

            Palette = new ushort[PaletteCapacity];
            for (int i = 0; i < PaletteCapacity; ++i)
            {
                Palette[i] = (ushort)(bin.ReadUInt16() ^ 0x8000);
            }

            var start = (int)bin.BaseStream.Position;
            int frameCount = bin.ReadInt32();

            var lookups = new int[frameCount];

            for (int i = 0; i < frameCount; ++i)
            {
                lookups[i] = start + bin.ReadInt32();
            }

            Frames = new List<FrameEdit>();

            for (int i = 0; i < frameCount; ++i)
            {
                bin.BaseStream.Seek(lookups[i], SeekOrigin.Begin);
                Frames.Add(new FrameEdit(bin));
            }
        }

        public unsafe Bitmap[] GetFrames()
        {
            if ((Frames == null) || (Frames.Count == 0))
            {
                return null;
            }

            var bits = new Bitmap[Frames.Count];
            for (int i = 0; i < bits.Length; ++i)
            {
                FrameEdit frame = Frames[i];
                int width = frame.Width;
                int height = frame.Height;
                if (height == 0 || width == 0)
                {
                    continue;
                }

                var bmp = new Bitmap(width, height, PixelFormat.Format16bppArgb1555);
                BitmapData bd = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format16bppArgb1555);
                var line = (ushort*)bd.Scan0;
                int delta = bd.Stride >> 1;

                int xBase = frame.Center.X - 0x200;
                int yBase = frame.Center.Y + height - 0x200;

                line += xBase;
                line += yBase * delta;

                for (int j = 0; j < frame.RawData.Length; ++j)
                {
                    FrameEdit.Raw raw = frame.RawData[j];

                    ushort* cur = line + (((raw.offsetY) * delta) + ((raw.offsetX) & 0x3FF));
                    ushort* end = cur + (raw.run);

                    int ii = 0;
                    while (cur < end)
                    {
                        *cur++ = Palette[raw.data[ii++]];
                    }
                }

                bmp.UnlockBits(bd);
                bits[i] = bmp;
            }

            return bits;
        }

        public void AddFrame(Bitmap bit, int centerX = 0, int centerY = 0 )
        {
            if (Frames == null)
            {
                Frames = new List<FrameEdit>();
            }

            Frames.Add(new FrameEdit(bit, Palette, centerX, centerY));
        }

        public void ReplaceFrame(Bitmap bit, int index)
        {
            if ((Frames == null) || (Frames.Count == 0))
            {
                return;
            }

            if (index > Frames.Count)
            {
                return;
            }

            Frames[index] = new FrameEdit(bit, Palette, Frames[index].Center.X, Frames[index].Center.Y);
        }

        public void RemoveFrame(int index)
        {
            if (Frames == null)
            {
                return;
            }

            if (index > Frames.Count)
            {
                return;
            }

            Frames.RemoveAt(index);
        }

        public void ClearFrames()
        {
            Frames?.Clear();
        }

        public void ExportPalette(string filename, int type)
        {
            switch (type)
            {
                case 0:
                    using (var tex = new StreamWriter(new FileStream(filename, FileMode.Create, FileAccess.ReadWrite)))
                    {
                        for (int i = 0; i < PaletteCapacity; ++i)
                        {
                            tex.WriteLine(Palette[i]);
                        }
                    }
                    break;
                case 1:
                    SavePaletteImage(filename, ImageFormat.Bmp);
                    break;
                case 2:
                    SavePaletteImage(filename, ImageFormat.Tiff);
                    break;
            }
        }

        private unsafe void SavePaletteImage(string filename, ImageFormat imageFormat)
        {
            using (var bmp = new Bitmap(PaletteCapacity, 20, PixelFormat.Format16bppArgb1555))
            {
                BitmapData bd = bmp.LockBits(
                    new Rectangle(0, 0, PaletteCapacity, 20), ImageLockMode.WriteOnly, PixelFormat.Format16bppArgb1555);
                var line = (ushort*) bd.Scan0;
                int delta = bd.Stride >> 1;

                for (int y = 0; y < bd.Height; ++y, line += delta)
                {
                    ushort* cur = line;
                    for (int i = 0; i < PaletteCapacity; ++i)
                    {
                        *cur++ = Palette[i];
                    }
                }

                bmp.UnlockBits(bd);
                using (var b = new Bitmap(bmp))
                {
                    b.Save(filename, imageFormat);
                }
            }
        }

        public void ReplacePalette(ushort[] palette)
        {
            Palette = palette;
        }

        public void Save(BinaryWriter bin, BinaryWriter idx)
        {
            if ((Frames == null) || (Frames.Count == 0))
            {
                idx.Write(-1);
                idx.Write(-1);
                idx.Write(-1);

                return;
            }

            long start = bin.BaseStream.Position;
            idx.Write((int)start);

            for (int i = 0; i < PaletteCapacity; ++i)
            {
                bin.Write((ushort)(Palette[i] ^ 0x8000));
            }

            long startPosition = bin.BaseStream.Position;
            bin.Write(Frames.Count);

            long seek = bin.BaseStream.Position;
            long curr = bin.BaseStream.Position + (4 * Frames.Count);

            foreach (FrameEdit frame in Frames)
            {
                bin.BaseStream.Seek(seek, SeekOrigin.Begin);
                bin.Write((int)(curr - startPosition));
                seek = bin.BaseStream.Position;
                bin.BaseStream.Seek(curr, SeekOrigin.Begin);
                frame.Save(bin);
                curr = bin.BaseStream.Position;
            }

            start = bin.BaseStream.Position - start;
            idx.Write((int)start);
            idx.Write(_idxExtra);
        }

        /// <summary>
        /// Debug method: Export a single frame to PNG for visual inspection.
        /// </summary>
        private static void ExportFrameToPng(FrameEdit frame, ushort[] palette, string filePath)
        {
            if (frame == null || palette == null || frame.RawData == null)
                return;

            try
            {
                int width = frame.Width;
                int height = frame.Height;

                if (width <= 0 || height <= 0)
                    return;

                // Create bitmap
                Bitmap bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);

                // Fill with transparent background
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        bmp.SetPixel(x, y, Color.Transparent);
                    }
                }

                // Decode frame runs into pixels
                // The run data is stored with offset from center, convert to local coordinates
                foreach (var raw in frame.RawData)
                {
                    int yPos = raw.offsetY;

                    if (yPos < 0 || yPos >= height || raw.data == null)
                        continue;

                    int xStart = raw.offsetX;

                    for (int i = 0; i < raw.run && i < raw.data.Length; i++)
                    {
                        int xPos = xStart + i;
                        if (xPos >= 0 && xPos < width)
                        {
                            byte palIdx = raw.data[i];
                            // Palette index 0 is transparent, skip it
                            if (palIdx > 0 && palIdx < palette.Length)
                            {
                                ushort palColor = palette[palIdx];
                                // Convert ARGB1555 to RGB (ignore alpha bit for now)
                                int r = ((palColor >> 10) & 0x1F) * 8;
                                int g = ((palColor >> 5) & 0x1F) * 8;
                                int b = (palColor & 0x1F) * 8;
                                // Clamp values to 0-255
                                r = Math.Min(255, r);
                                g = Math.Min(255, g);
                                b = Math.Min(255, b);
                                bmp.SetPixel(xPos, yPos, Color.FromArgb(255, r, g, b));
                            }
                        }
                    }
                }

                bmp.Save(filePath, ImageFormat.Png);
                bmp.Dispose();
            }
            catch
            {
                // Silent fail on error
            }
        }

        public void ExportToVD(BinaryWriter bin, ref long indexpos, ref long animpos)
        {
            bin.BaseStream.Seek(indexpos, SeekOrigin.Begin);
            if ((Frames == null) || (Frames.Count == 0))
            {
                bin.Write(-1);
                bin.Write(-1);
                bin.Write(-1);
                indexpos = bin.BaseStream.Position;
                return;
            }

            bin.Write((int)animpos);
            indexpos = bin.BaseStream.Position;
            bin.BaseStream.Seek(animpos, SeekOrigin.Begin);

            for (int i = 0; i < PaletteCapacity; i++)
            {
                bin.Write((ushort)(Palette[i] ^ 0x8000));
            }


            long startPosition = (int)bin.BaseStream.Position;
            bin.Write(Frames.Count);
            long seek = (int)bin.BaseStream.Position;
            long curr = bin.BaseStream.Position + (4 * Frames.Count);
            foreach (FrameEdit frame in Frames)
            {
                bin.BaseStream.Seek(seek, SeekOrigin.Begin);
                bin.Write((int)(curr - startPosition));
                seek = bin.BaseStream.Position;
                bin.BaseStream.Seek(curr, SeekOrigin.Begin);
                frame.Save(bin);
                curr = bin.BaseStream.Position;
            }

            long length = bin.BaseStream.Position - animpos;
            animpos = bin.BaseStream.Position;
            bin.BaseStream.Seek(indexpos, SeekOrigin.Begin);
            bin.Write((int)length);
            bin.Write(_idxExtra);
            indexpos = bin.BaseStream.Position;
        }

        public void ExportToVDScaled(BinaryWriter bin, ref long indexpos, ref long animpos, float scale)
        {
            bin.BaseStream.Seek(indexpos, SeekOrigin.Begin);
            if ((Frames == null) || (Frames.Count == 0))
            {
                bin.Write(-1);
                bin.Write(-1);
                bin.Write(-1);
                indexpos = bin.BaseStream.Position;
                return;
            }

            bin.Write((int)animpos);
            indexpos = bin.BaseStream.Position;
            bin.BaseStream.Seek(animpos, SeekOrigin.Begin);


            for (int i = 0; i < PaletteCapacity; i++)
            {
                bin.Write((ushort)(Palette[i] ^ 0x8000));
            }

            long startPosition = (int)bin.BaseStream.Position;
            bin.Write(Frames.Count);
            long seek = (int)bin.BaseStream.Position;
            long curr = bin.BaseStream.Position + (4 * Frames.Count);

            for (int frameIdx = 0; frameIdx < Frames.Count; frameIdx++)
            {
                FrameEdit frame = Frames[frameIdx];
                bin.BaseStream.Seek(seek, SeekOrigin.Begin);
                bin.Write((int)(curr - startPosition));
                seek = bin.BaseStream.Position;
                bin.BaseStream.Seek(curr, SeekOrigin.Begin);

                FrameEdit.ScaleAndSaveFrame(frame, scale, Palette, bin);
                curr = bin.BaseStream.Position;
            }

            long length = bin.BaseStream.Position - animpos;
            animpos = bin.BaseStream.Position;
            bin.BaseStream.Seek(indexpos, SeekOrigin.Begin);
            bin.Write((int)length);
            bin.Write(_idxExtra);
            indexpos = bin.BaseStream.Position;
        }
    }

    public sealed class FrameEdit
    {
        private const int _doubleXor = (0x200 << 22) | (0x200 << 12);

        public struct Raw
        {
            public int run;
            public int offsetX;
            public int offsetY;
            public byte[] data;
        }

        public Raw[] RawData { get; }
        public Point Center { get; set; }

        public readonly int Width;
        public readonly int Height;

        public FrameEdit(BinaryReader bin)
        {
            int xCenter = bin.ReadInt16();
            int yCenter = bin.ReadInt16();

            Width = bin.ReadUInt16();
            Height = bin.ReadUInt16();

            if (Height == 0 || Width == 0)
            {
                return;
            }

            int header;

            var tmp = new List<Raw>();

            while ((header = bin.ReadInt32()) != 0x7FFF7FFF)
            {
                var raw = new Raw();
                header ^= _doubleXor;
                raw.run = (header & 0xFFF);
                raw.offsetY = ((header >> 12) & 0x3FF);
                raw.offsetX = ((header >> 22) & 0x3FF);

                int i = 0;
                raw.data = new byte[raw.run];

                while (i < raw.run)
                {
                    raw.data[i++] = bin.ReadByte();
                }

                tmp.Add(raw);
            }

            RawData = tmp.ToArray();
            Center = new Point(xCenter, yCenter);
        }

        public unsafe FrameEdit(Bitmap bit, ushort[] palette, int centerX, int centerY)
        {
            Center = new Point(centerX, centerY);
            Width = bit.Width;
            Height = bit.Height;

            BitmapData bd = bit.LockBits(new Rectangle(0, 0, Width, Height), ImageLockMode.ReadOnly, PixelFormat.Format16bppArgb1555);
            var line = (ushort*)bd.Scan0;
            int delta = bd.Stride >> 1;
            var tmp = new List<Raw>();

            for (int y = 0; y < bit.Height; ++y, line += delta)
            {
                ushort* cur = line;

                int i = 0;
                int x = 0;

                while (i < bit.Width)
                {
                    for (i = x; i <= bit.Width; ++i)
                    {
                        // first pixel set
                        if (i < bit.Width && cur[i] != 0)
                        {
                            break;
                        }
                    }

                    if (i >= bit.Width)
                    {
                        continue;
                    }

                    int j;
                    for (j = (i + 1); j < bit.Width; ++j)
                    {
                        // next non set pixel
                        if (cur[j] == 0)
                        {
                            break;
                        }
                    }

                    var raw = new Raw
                    {
                        run = j - i
                    };
                    raw.offsetX = j - raw.run - centerX;
                    raw.offsetX += 512;
                    raw.offsetY = y - centerY - bit.Height;
                    raw.offsetY += 512;

                    int r = 0;
                    raw.data = new byte[raw.run];
                    while (r < raw.run)
                    {
                        ushort col = cur[r + i];
                        raw.data[r++] = GetPaletteIndex(palette, col);
                    }
                    tmp.Add(raw);
                    x = j + 1;
                    i = x;
                }
            }

            RawData = tmp.ToArray();
            bit.UnlockBits(bd);
        }

        public void ChangeCenter(int x, int y)
        {
            for (int i = 0; i < RawData.Length; i++)
            {
                RawData[i].offsetX += Center.X;
                RawData[i].offsetX -= x;
                RawData[i].offsetY += Center.Y;
                RawData[i].offsetY -= y;
            }

            Center = new Point(x, y);
        }

        private static byte GetPaletteIndex(IReadOnlyList<ushort> palette, ushort col)
        {
            for (int i = 0; i < palette.Count; i++)
            {
                if (palette[i] == col)
                {
                    return (byte)i;
                }
            }

            return 0;
        }

        public void Save(BinaryWriter bin)
        {
            bin.Write((short)Center.X);
            bin.Write((short)Center.Y);
            bin.Write((ushort)Width);
            bin.Write((ushort)Height);

            if (RawData != null)
            {
                for (int j = 0; j < RawData.Length; j++)
                {
                    int newHeader = RawData[j].run | (RawData[j].offsetY << 12) | (RawData[j].offsetX << 22);
                    newHeader ^= _doubleXor;
                    bin.Write(newHeader);
                    foreach (byte b in RawData[j].data)
                    {
                        bin.Write(b);
                    }
                }
            }

            bin.Write(0x7FFF7FFF);
        }

        internal static void ScaleAndSaveFrame(FrameEdit frame, float scale, ushort[] palette, BinaryWriter output)
        {
            // Null check
            if (frame == null)
            {
                return;
            }

            // If scale is 1.0, just save normally
            if (Math.Abs(scale - 1.0f) < 0.001f)
            {
                frame.Save(output);
                return;
            }

            // Step 1: Decode the frame's run-length data into a 2D indexed pixel grid
            int width = frame.Width;
            int height = frame.Height;
            byte[][] pixelGrid = new byte[height][];
            for (int i = 0; i < height; i++)
            {
                pixelGrid[i] = new byte[width];
                // Initialize to 0 (transparent)
                for (int j = 0; j < width; j++)
                    pixelGrid[i][j] = 0;
            }

            // Decode raw runs into the grid
            int xBase = frame.Center.X - 0x200;
            int yBase = frame.Center.Y + height - 0x200;

            if (frame.RawData != null)
            {
                foreach (var raw in frame.RawData)
                {
                    int xStart = xBase + raw.offsetX;
                    int yPos = yBase + raw.offsetY;

                    if (yPos < 0 || yPos >= height || raw.data == null)
                        continue;

                    for (int i = 0; i < raw.run && i < raw.data.Length; i++)
                    {
                        int xPos = xStart + i;
                        if (xPos >= 0 && xPos < width)
                        {
                            pixelGrid[yPos][xPos] = raw.data[i];
                        }
                    }
                }
            }

            // Step 2: Scale the pixel grid using nearest-neighbor
            int newWidth = Math.Max(1, (int)Math.Round(width * scale));
            int newHeight = Math.Max(1, (int)Math.Round(height * scale));
            byte[][] scaledGrid = new byte[newHeight][];
            for (int i = 0; i < newHeight; i++)
            {
                scaledGrid[i] = new byte[newWidth];
            }

            for (int y = 0; y < newHeight; y++)
            {
                int srcY = (int)(y / scale);
                if (srcY >= height) srcY = height - 1;

                for (int x = 0; x < newWidth; x++)
                {
                    int srcX = (int)(x / scale);
                    if (srcX >= width) srcX = width - 1;
                    scaledGrid[y][x] = pixelGrid[srcY][srcX];
                }
            }

            // Step 3: Scale the frame center and dimensions
            int newCenterX = (int)Math.Round(frame.Center.X * scale);
            int newCenterY = (int)Math.Round(frame.Center.Y * scale);

            // Step 4: Write header
            output.Write((short)newCenterX);
            output.Write((short)newCenterY);
            output.Write((ushort)newWidth);
            output.Write((ushort)newHeight);

            // Step 5: Re-encode the scaled grid to run format using the same coordinate system
            int newXBase = newCenterX - 0x200;
            int newYBase = newCenterY + newHeight - 0x200;

            for (int y = 0; y < newHeight; y++)
            {
                int x = 0;
                while (x < newWidth)
                {
                    // Skip transparent pixels
                    while (x < newWidth && scaledGrid[y][x] == 0)
                        x++;

                    if (x >= newWidth)
                        break;

                    int runStart = x;
                    var runData = new List<byte>();

                    // Collect opaque run - keep original palette indices as-is
                    // Color correction is applied to the palette, not individual pixels
                    while (x < newWidth && scaledGrid[y][x] != 0)
                    {
                        runData.Add(scaledGrid[y][x]);
                        x++;
                    }

                    if (runData.Count == 0)
                        continue;

                    // Encode using the same coordinate system as the decoder
                    int runOffsetX = runStart - newXBase;
                    int runOffsetY = y - newYBase;

                    // The offsetX and offsetY stored in the header are already in the 0x200-adjusted space
                    // We need to clamp them to the 10-bit range (0-1023)
                    runOffsetX = runOffsetX & 0x3FF;
                    runOffsetY = runOffsetY & 0x3FF;

                    int header = runData.Count | (runOffsetY << 12) | (runOffsetX << 22);
                    header ^= _doubleXor;
                    output.Write(header);

                    foreach (byte idx in runData)
                        output.Write(idx);
                }
            }

            output.Write(0x7FFF7FFF);
        }
    }
}