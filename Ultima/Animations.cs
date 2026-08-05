using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace Ultima
{
    public static class Animations
    {
        public const int _maxAnimationValue = 4096; // bodyconv.def says it's maximum animation value so max bodyId?
        public static readonly int PaletteCapacity = 0x100;

        // Keep legacy fields for compatibility; primary source of truth is _fileIndexMap populated on Reload()
        private static FileIndex _fileIndex = new FileIndex("Anim.idx", "Anim.mul", 0x40000, 6);
        private static FileIndex _fileIndex2 = new FileIndex("Anim2.idx", "Anim2.mul", 0x10000, -1);
        private static FileIndex _fileIndex3 = new FileIndex("Anim3.idx", "Anim3.mul", 0x20000, -1);
        private static FileIndex _fileIndex4 = new FileIndex("Anim4.idx", "Anim4.mul", 0x20000, -1);
        private static FileIndex _fileIndex5 = new FileIndex("Anim5.idx", "Anim5.mul", 0x20000, -1);

        // Dynamic map of discovered anim file indexes keyed by fileType (1 = anim.idx, 2 = anim2.idx, ...)
        private static readonly Dictionary<int, FileIndex> _fileIndexMap = new Dictionary<int, FileIndex>();
        // Optional JSON-driven per-file mapping: filename -> segments describing entries-per-body
        private static readonly Dictionary<string, AnimFileMapping> _animMappings = new Dictionary<string, AnimFileMapping>(System.StringComparer.OrdinalIgnoreCase);

        private static byte[] _streamBuffer;

        // Optional external overrides (set by UI) for AppData path and profile name
        public static string AppDataPath { get; set; }
        public static string ProfileName { get; set; }

        private class Segment
        {
            public int Start { get; set; }
            public int? End { get; set; }
            public int EntriesPerBody { get; set; }
            // Optional segment-specific anim type: 0=High(22),1=Low(13),2=People(35)
            public int? AnimType { get; set; }
        }

        /// <summary>
        /// Public wrapper for other components (eg. AnimationEdit) to reuse Animations file index calculation.
        /// </summary>
        public static void GetFileIndexForEditor(int body, int action, int direction, int fileType, out FileIndex fileIndex, out int index)
        {
            GetFileIndex(body, action, direction, fileType, out fileIndex, out index);
        }

        /// <summary>
        /// Returns FileIndex object for given fileType (1 = anim.idx, 2 = anim2.idx, ...)
        /// </summary>
        public static FileIndex GetFileIndexByType(int fileType)
        {
            if (_fileIndexMap.TryGetValue(fileType, out FileIndex fi))
                return fi;

            switch (fileType)
            {
                case 1: return _fileIndex;
                case 2: return _fileIndex2;
                case 3: return _fileIndex3;
                case 4: return _fileIndex4;
                case 5: return _fileIndex5;
                default: return null;
            }
        }

        /// <summary>
        /// Returns all available file types (1..N) that Animations knows about (discovered or legacy)
        /// </summary>
        public static IEnumerable<int> GetAvailableFileTypes()
        {
            var list = new List<int>();

            // include discovered dynamic file indexes
            foreach (var kv in _fileIndexMap)
            {
                if (kv.Value != null && kv.Value.IndexLength > 0)
                    list.Add(kv.Key);
            }

            // include legacy ones if not already present
            for (int i = 1; i <= 5; ++i)
            {
                if (!list.Contains(i))
                {
                    FileIndex fi = null;
                    switch (i)
                    {
                        case 1: fi = _fileIndex; break;
                        case 2: fi = _fileIndex2; break;
                        case 3: fi = _fileIndex3; break;
                        case 4: fi = _fileIndex4; break;
                        case 5: fi = _fileIndex5; break;
                    }

                    if (fi != null && fi.IndexLength > 0)
                        list.Add(i);
                }
            }

            list.Sort();
            return list;
        }

        private static void LoadAnimMappingXml()
        {
            _animMappings.Clear();
            try
            {

                string profile = null;
                if (!string.IsNullOrEmpty(ProfileName))
                    profile = ProfileName.Replace("Options_", "").Replace(".xml", "");

                string fileName = null;
                if (!string.IsNullOrEmpty(profile))
                    fileName = Path.Combine(AppDataPath ?? string.Empty, $"AnimMap_{profile}.xml");

                if (string.IsNullOrEmpty(fileName) || !File.Exists(fileName))
                    fileName = Path.Combine(AppDataPath ?? string.Empty, "AnimMap.xml");


                if (!File.Exists(fileName))
                {
                    // fallback to root dir next to data files
                    string root = Files.RootDir ?? Files.Directory ?? Directory.GetCurrentDirectory();
                    if (!string.IsNullOrEmpty(profile))
                        fileName = Path.Combine(root, $"AnimMap_{profile}.xml");

                    if (string.IsNullOrEmpty(fileName) || !File.Exists(fileName))
                        fileName = Path.Combine(root, "AnimMap.xml");
                }

                if (!File.Exists(fileName))
                    return;

                var doc = new System.Xml.XmlDocument();
                doc.Load(fileName);

                var animFiles = doc.SelectNodes("/AnimFiles/AnimFile");
                if (animFiles == null)
                    return;

                foreach (System.Xml.XmlNode node in animFiles)
                {
                    var fileAttr = node.Attributes?["file"];
                    if (fileAttr == null) continue;
                    var mapping = new AnimFileMapping { File = fileAttr.Value, Segments = new List<Segment>() };

                    // optional file-level default anim type
                    if (node.Attributes?["defaultType"] != null)
                    {
                        if (int.TryParse(node.Attributes["defaultType"].Value, out int dt))
                            mapping.DefaultType = dt;
                    }

                    foreach (System.Xml.XmlNode seg in node.SelectNodes("Segment"))
                    {
                        int start = 0;
                        int? end = null;
                        int entries = 0;
                        int? animType = null;

                        if (seg.Attributes?["start"] != null)
                            int.TryParse(seg.Attributes["start"].Value, out start);

                        if (seg.Attributes?["end"] != null)
                        {
                            if (int.TryParse(seg.Attributes["end"].Value, out int e)) end = e;
                        }

                        if (seg.Attributes?["entriesPerBody"] != null)
                            int.TryParse(seg.Attributes["entriesPerBody"].Value, out entries);

                        if (seg.Attributes?["animType"] != null)
                        {
                            if (int.TryParse(seg.Attributes["animType"].Value, out int t))
                                animType = t;
                        }

                        mapping.Segments.Add(new Segment { Start = start, End = end, EntriesPerBody = entries, AnimType = animType });
                    }

                    if (!string.IsNullOrEmpty(mapping.File))
                        _animMappings[Path.GetFileName(mapping.File)] = mapping;
                }
            }
            catch
            {
                // Ignore malformed config
            }
        }

        private class AnimFileMapping
        {
            public string File { get; set; }
            public List<Segment> Segments { get; set; }
            // Optional default anim type for this file: 0=High(22),1=Low(13),2=People(35)
            public int? DefaultType { get; set; }
        }

        /// <summary>
        /// Rereads AnimX files and bodyconv, body.def
        /// </summary>
        public static void Reload()
        {
            // Recreate legacy indexes
            _fileIndex = new FileIndex("Anim.idx", "Anim.mul", 0x40000, 6);
            _fileIndex2 = new FileIndex("Anim2.idx", "Anim2.mul", 0x10000, -1);
            _fileIndex3 = new FileIndex("Anim3.idx", "Anim3.mul", 0x20000, -1);
            _fileIndex4 = new FileIndex("Anim4.idx", "Anim4.mul", 0x20000, -1);
            _fileIndex5 = new FileIndex("Anim5.idx", "Anim5.mul", 0x20000, -1);

            // Build dynamic map of anim files present in the RootDir (anim.idx, anim2.idx, anim3.idx, ...)
            _fileIndexMap.Clear();
            string root = Files.RootDir ?? Files.Directory ?? Directory.GetCurrentDirectory();
            if (!string.IsNullOrEmpty(root) && Directory.Exists(root))
            {
                foreach (var idxPath in Directory.GetFiles(root, "anim*.idx", SearchOption.TopDirectoryOnly))
                {
                    var name = Path.GetFileName(idxPath);
                    // determine fileType: anim.idx -> 1, anim2.idx -> 2, etc.
                    int fileType = 0;
                    if (name.Equals("anim.idx", System.StringComparison.OrdinalIgnoreCase)) fileType = 1;
                    else
                    {
                        var s = name.ToLowerInvariant();
                        if (s.StartsWith("anim") && s.EndsWith(".idx"))
                        {
                            var numPart = s.Substring(4, s.Length - 8); // between 'anim' and '.idx'
                            if (int.TryParse(numPart, out int n)) fileType = n;
                        }
                    }

                    if (fileType > 0)
                    {
                        var mulName = Path.ChangeExtension(name, ".mul");
                        var fi = new FileIndex(name, mulName, -1);
                        if (fi != null && fi.IndexLength > 0)
                        {
                            _fileIndexMap[fileType] = fi;
                        }
                    }
                }
            }

            // Load optional mapping XML that describes per-body entries for each anim file
            LoadAnimMappingXml();

            BodyConverter.Initialize();
            BodyTable.Initialize();
        }

        /// <summary>
        ///     Returns animation frames
        /// </summary>
        /// <param name="body"></param>
        /// <param name="action"></param>
        /// <param name="direction"></param>
        /// <param name="hue"></param>
        /// <param name="preserveHue">
        ///     No Hue override <see cref="bodydev" />
        /// </param>
        /// <param name="firstFrame"></param>
        /// <returns></returns>
        public static AnimationFrame[] GetAnimation(int body, int action, int direction, ref int hue, bool preserveHue, bool firstFrame)
        {
            if (preserveHue)
            {
                Translate(ref body);
            }
            else
            {
                Translate(ref body, ref hue);
            }

            int fileType = BodyConverter.Convert(ref body);

            GetFileIndex(body, action, direction, fileType, out FileIndex fileIndex, out int index);

            Stream stream = fileIndex.Seek(index, out int length, out int _, out bool _);
            if (stream == null)
            {
                return null;
            }

            if (_streamBuffer == null || _streamBuffer.Length < length)
            {
                _streamBuffer = new byte[length];
            }

            _ = stream.Read(_streamBuffer, 0, length);

            var memoryStream = new MemoryStream(_streamBuffer, false);

            bool flip = direction > 4;
            AnimationFrame[] frames;
            using (var bin = new BinaryReader(memoryStream))
            {
                var palette = new ushort[PaletteCapacity];

                for (int i = 0; i < PaletteCapacity; ++i)
                {
                    palette[i] = (ushort)(bin.ReadUInt16() ^ 0x8000);
                }

                var start = (int)bin.BaseStream.Position;
                int frameCount = bin.ReadInt32();

                var lookups = new int[frameCount];

                for (int i = 0; i < frameCount; ++i)
                {
                    lookups[i] = start + bin.ReadInt32();
                }

                bool onlyHueGrayPixels = (hue & 0x8000) != 0;

                hue = (hue & 0x3FFF) - 1;

                Hue hueObject;

                if (hue >= 0 && hue < Hues.List.Length)
                {
                    hueObject = Hues.List[hue];
                }
                else
                {
                    hueObject = null;
                }

                if (firstFrame)
                {
                    frameCount = 1;
                }

                frames = new AnimationFrame[frameCount];

                for (int i = 0; i < frameCount; ++i)
                {
                    bin.BaseStream.Seek(lookups[i], SeekOrigin.Begin);
                    frames[i] = new AnimationFrame(palette, bin, flip);

                    if (hueObject != null && frames[i]?.Bitmap != null)
                    {
                        hueObject.ApplyTo(frames[i].Bitmap, onlyHueGrayPixels);
                    }
                }
            }

            memoryStream.Close();

            return frames;
        }

        public static AnimationFrame[] GetAnimation(int body, int action, int direction, int fileType)
        {
            GetFileIndex(body, action, direction, fileType, out FileIndex fileIndex, out int index);

            Stream stream = fileIndex.Seek(index, out int _, out int _, out bool _);
            if (stream == null)
            {
                return null;
            }

            bool flip = direction > 4;

            using (var bin = new BinaryReader(stream))
            {
                var palette = new ushort[PaletteCapacity];

                for (int i = 0; i < PaletteCapacity; ++i)
                {
                    palette[i] = (ushort)(bin.ReadUInt16() ^ 0x8000);
                }

                var start = (int)bin.BaseStream.Position;
                int frameCount = bin.ReadInt32();

                var lookups = new int[frameCount];

                for (int i = 0; i < frameCount; ++i)
                {
                    lookups[i] = start + bin.ReadInt32();
                }

                var frames = new AnimationFrame[frameCount];

                for (int i = 0; i < frameCount; ++i)
                {
                    bin.BaseStream.Seek(lookups[i], SeekOrigin.Begin);
                    frames[i] = new AnimationFrame(palette, bin, flip);
                }

                return frames;
            }
        }

        private static int[] _table;

        /// <summary>
        /// Translates body (body.def)
        /// </summary>
        /// <param name="body"></param>
        public static void Translate(ref int body)
        {
            if (_table == null)
            {
                LoadTable();
            }

            if (body <= 0 || body >= _table.Length)
            {
                body = 0;
                return;
            }

            body = _table[body] & 0x7FFF;
        }

        /// <summary>
        /// Translates body and hue (body.def)
        /// </summary>
        /// <param name="body"></param>
        /// <param name="hue"></param>
        public static void Translate(ref int body, ref int hue)
        {
            if (_table == null)
            {
                LoadTable();
            }

            if (body <= 0 || body >= _table.Length)
            {
                body = 0;
                return;
            }

            int table = _table[body];
            if ((table & (1 << 31)) == 0)
            {
                return;
            }

            body = table & 0x7FFF;

            int vhue = (hue & 0x3FFF) - 1;
            if (vhue < 0 || vhue >= Hues.List.Length)
            {
                hue = (table >> 15) & 0xFFFF;
            }
        }

        private static void LoadTable()
        {
            // TODO: check why it was fixed at max 1697. Probably old code for anim.mul?
            //int count = 400 + ((_fileIndex.Index.Length - 35000) / 175);

            _table = new int[_maxAnimationValue + 1];

            for (int i = 0; i < _table.Length; ++i)
            {
                var bodyTableEntryExist = BodyTable.Entries.TryGetValue(i, out BodyTableEntry bodyTableEntry);
                if (!bodyTableEntryExist || BodyConverter.Contains(i))
                {
                    _table[i] = i;
                }
                else
                {
                    _table[i] = bodyTableEntry.OldId | (1 << 31) | ((bodyTableEntry.NewHue & 0xFFFF) << 15);
                }
            }
        }

        /// <summary>
        /// Is Body with action and direction defined
        /// </summary>
        /// <param name="body"></param>
        /// <param name="action"></param>
        /// <param name="direction"></param>
        /// <returns></returns>
        public static bool IsActionDefined(int body, int action, int direction)
        {
            Translate(ref body);
            int fileType = BodyConverter.Convert(ref body);

            GetFileIndex(body, action, direction, fileType, out FileIndex fileIndex, out int index);

            bool valid = fileIndex.Valid(index, out int length, out int _, out bool _);

            return valid && (length >= 1);
        }

        /// <summary>
        /// Is Animation in given anim file defined
        /// </summary>
        /// <param name="body"></param>
        /// <param name="action"></param>
        /// <param name="dir"></param>
        /// <param name="fileType"></param>
        /// <returns></returns>
        public static bool IsAnimDefined(int body, int action, int dir, int fileType)
        {
            GetFileIndex(body, action, dir, fileType, out FileIndex fileIndex, out int index);

            Stream stream = fileIndex.Seek(index, out int length, out int _, out bool _);

            bool def = !((stream == null) || (length == 0));

            stream?.Close();

            return def;
        }

        /// <summary>
        /// Returns Animation count in given anim file
        /// </summary>
        /// <param name="fileType"></param>
        /// <returns></returns>
        public static int GetAnimCount(int fileType)
        {
            // If dynamic file map contains this fileType, derive body count from its idx length and mapping
            if (_fileIndexMap.TryGetValue(fileType, out FileIndex fi))
            {
                if (fi != null && fi.IdxLength > 0)
                {
                    // total number of entries (each entry is 12 bytes in idx)
                    int totalEntries = (int)(fi.IdxLength / 12);

                    // If we have a mapping for entries-per-body, use it to determine how many bodies are present
                    string idxName = fileType == 1 ? "anim.idx" : $"anim{fileType}.idx";
                    if (_animMappings.TryGetValue(idxName, out AnimFileMapping mapping) && mapping?.Segments != null)
                    {
                        int cum = 0;
                        int body = 0;
                        while (cum < totalEntries && body <= _maxAnimationValue)
                        {
                            int entriesPerBody = 0;
                            foreach (var seg in mapping.Segments)
                            {
                                if (body >= seg.Start && (seg.End == null || body < seg.End.Value))
                                {
                                    entriesPerBody = seg.EntriesPerBody;
                                    break;
                                }
                            }

                            if (entriesPerBody == 0)
                                entriesPerBody = body < 200 ? 110 : 65;

                            cum += entriesPerBody;
                            if (cum > totalEntries) break;
                            body++;
                        }

                        return body;
                    }

                    // No mapping available: approximate by consuming entries using default per-body sizes
                    int cumDefault = 0;
                    int bDefault = 0;
                    while (cumDefault < totalEntries && bDefault <= _maxAnimationValue)
                    {
                        int entriesPerBody = bDefault < 200 ? 110 : 65;
                        cumDefault += entriesPerBody;
                        if (cumDefault > totalEntries) break;
                        bDefault++;
                    }

                    return bDefault;
                }
            }

            // Fallback to legacy behavior
            switch (fileType)
            {
                case 1:
                default:
                    return 400 + ((int)(_fileIndex.IdxLength - (35000 * 12)) / (12 * 175));
                case 2:
                    return 200 + ((int)(_fileIndex2.IdxLength - (22000 * 12)) / (12 * 65));
                case 3:
                    return 400 + ((int)(_fileIndex3.IdxLength - (35000 * 12)) / (12 * 175));
                case 4:
                    return 400 + ((int)(_fileIndex4.IdxLength - (35000 * 12)) / (12 * 175));
                case 5:
                    return 400 + ((int)(_fileIndex5.IdxLength - (35000 * 12)) / (12 * 175));
            }
        }

        /// <summary>
        /// Action count of given Body in given anim file
        /// </summary>
        /// <param name="body"></param>
        /// <param name="fileType"></param>
        /// <returns></returns>
        public static int GetAnimLength(int body, int fileType)
        {
            string idxName = fileType == 1 ? "anim.idx" : $"anim{fileType}.idx";
            if (_animMappings.TryGetValue(idxName, out AnimFileMapping mapping) && mapping?.Segments != null)
            {
                foreach (var segment in mapping.Segments)
                {
                    if (body >= segment.Start && (!segment.End.HasValue || body < segment.End.Value))
                    {
                        return segment.EntriesPerBody / 5;
                    }
                }
            }

            int length;
            switch (fileType)
            {
                case 1:
                default:
                    if (body < 200)
                    {
                        length = 22; // high
                    }
                    else if (body < 400)
                    {
                        length = 13; // low
                    }
                    else
                    {
                        length = 35; // people
                    }

                    break;
                case 2:
                    if (body < 200)
                    {
                        length = 22; // high
                    }
                    else
                    {
                        length = 13; // low
                    }

                    break;
                case 3:
                    if (body < 300)
                    {
                        length = 13;
                    }
                    else if (body < 400)
                    {
                        length = 22;
                    }
                    else
                    {
                        length = 35;
                    }

                    break;
                case 4:
                case 5:
                    if (body < 200)
                    {
                        length = 22;
                    }
                    else if (body < 400)
                    {
                        length = 13;
                    }
                    else
                    {
                        length = 35;
                    }

                    break;
            }
            return length;
        }

        /// <summary>
        /// Returns the anim type for a given body in given anim file.
        /// 0 = High (22), 1 = Low (13), 2 = People (35)
        /// Mapping preferences: segment.AnimType -> file.DefaultType -> inferred from action count
        /// </summary>
        public static int GetAnimType(int body, int fileType)
        {
            string idxName = fileType == 1 ? "anim.idx" : $"anim{fileType}.idx";
            if (_animMappings.TryGetValue(idxName, out AnimFileMapping mapping) && mapping?.Segments != null)
            {
                foreach (var segment in mapping.Segments)
                {
                    if (body >= segment.Start && (!segment.End.HasValue || body < segment.End.Value))
                    {
                        if (segment.AnimType.HasValue)
                            return segment.AnimType.Value;
                        break; // no segment-specific animType, fall through to file default or inference
                    }
                }

                if (mapping.DefaultType.HasValue)
                    return mapping.DefaultType.Value;
            }

            // infer from anim length
            int length = GetAnimLength(body, fileType);
            return length == 22 ? 0 : length == 13 ? 1 : 2;
        }

        /// <summary>
        /// Gets files index index based on fileType, body, action and direction
        /// </summary>
        /// <param name="body"></param>
        /// <param name="action"></param>
        /// <param name="direction"></param>
        /// <param name="fileType">animX</param>
        /// <param name="fileIndex"></param>
        /// <param name="index"></param>
        private static void GetFileIndex(int body, int action, int direction, int fileType, out FileIndex fileIndex, out int index)
        {
            // If we detected/loaded this anim file, use its FileIndex and mapping
            if (_fileIndexMap.TryGetValue(fileType, out FileIndex fi))
            {
                fileIndex = fi;

                // Determine entries per body for each body using JSON mapping if present, otherwise default
                // to case-2 style (body<200 -> 110, else -> 65)
                int cum = 0;
                // If mapping exists for this file, use it
                string idxName = fileType == 1 ? "anim.idx" : $"anim{fileType}.idx";
                AnimFileMapping mapping = null;
                _animMappings.TryGetValue(idxName, out mapping);

                for (int b = 0; b < body; ++b)
                {
                    int entries = 0;
                    if (mapping != null && mapping.Segments != null)
                    {
                        foreach (var seg in mapping.Segments)
                        {
                            if (b >= seg.Start && (seg.End == null || b < seg.End.Value))
                            {
                                entries = seg.EntriesPerBody;
                                break;
                            }
                        }
                    }

                    if (entries == 0)
                    {
                        // default case-2 style
                        entries = b < 200 ? 110 : 65;
                    }

                    cum += entries;
                }

                index = cum + action * 5;

                if (direction <= 4)
                {
                    index += direction;
                }
                else
                {
                    index += direction - ((direction - 4) * 2);
                }

                return;
            }

            // Fallback to legacy hard-coded behavior for anim1..anim5
            switch (fileType)
            {
                case 1:
                default:
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
                    if ((body < 200) && (body != 34)) // looks strange, though it works.
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

        /// <summary>
        /// Returns Filename body is in
        /// </summary>
        /// <param name="body"></param>
        /// <returns>anim{0}.mul</returns>
        public static string GetFileName(int body)
        {
            Translate(ref body);
            int fileType = BodyConverter.Convert(ref body);

            return fileType == 1 ? "anim.mul" : $"anim{fileType}.mul";
        }
    }

    public sealed class AnimationFrame
    {
        public Point Center { get; set; }
        public Bitmap Bitmap { get; set; }

        private const int _doubleXor = (0x200 << 22) | (0x200 << 12);

        public static readonly AnimationFrame Empty = new AnimationFrame();
        //public static readonly AnimationFrame[] EmptyFrames = new AnimationFrame[1] { Empty };

        private AnimationFrame()
        {
            Bitmap = new Bitmap(1, 1);
        }

        public unsafe AnimationFrame(ushort[] palette, BinaryReader bin, bool flip)
        {
            int xCenter = bin.ReadInt16();
            int yCenter = bin.ReadInt16();

            int width = bin.ReadUInt16();
            int height = bin.ReadUInt16();
            if (height == 0 || width == 0)
            {
                return;
            }

            var bmp = new Bitmap(width, height, PixelFormat.Format16bppArgb1555);
            BitmapData bd = bmp.LockBits(
                new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format16bppArgb1555);
            var line = (ushort*)bd.Scan0;
            int delta = bd.Stride >> 1;

            int header;

            int xBase = xCenter - 0x200;
            int yBase = (yCenter + height) - 0x200;

            if (!flip)
            {
                line += xBase;
                line += yBase * delta;

                while ((header = bin.ReadInt32()) != 0x7FFF7FFF)
                {
                    header ^= _doubleXor;

                    ushort* cur = line + ((((header >> 12) & 0x3FF) * delta) + ((header >> 22) & 0x3FF));
                    ushort* end = cur + (header & 0xFFF);
                    while (cur < end)
                    {
                        *cur++ = palette[bin.ReadByte()];
                    }
                }
            }
            else
            {
                line -= xBase - width + 1;
                line += yBase * delta;

                while ((header = bin.ReadInt32()) != 0x7FFF7FFF)
                {
                    header ^= _doubleXor;

                    ushort* cur = line + ((((header >> 12) & 0x3FF) * delta) - ((header >> 22) & 0x3FF));
                    ushort* end = cur - (header & 0xFFF);

                    while (cur > end)
                    {
                        *cur-- = palette[bin.ReadByte()];
                    }
                }

                xCenter = width - xCenter;
            }

            bmp.UnlockBits(bd);

            Center = new Point(xCenter, yCenter);
            Bitmap = bmp;
        }
    }
}