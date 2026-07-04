using System.IO;
using System;
using System.Xml.Linq;

namespace Ultima.Helpers
{
    public static class MapSizeDetector
    {
        /// <summary>
        /// Optional override path for Mapnames.xml (set by UI when a profile-specific file is in use).
        /// If set and the file exists, the detector will consult this file first for map sizes.
        /// </summary>
        public static string MapNamesPathOverride { get; set; }

        /// <summary>
        /// Try to detect map width/height by inspecting mul/staidx files.
        /// Returns true when detection succeeded.
        /// </summary>
        public static bool TryDetectMapSize(int fileIndex, string path, out int width, out int height)
        {
            width = 0;
            height = 0;

            // First try override path set by UI (profile-specific mapnames file)
            if (!string.IsNullOrEmpty(MapNamesPathOverride) && File.Exists(MapNamesPathOverride))
            {
                try
                {
                    var doc = XDocument.Load(MapNamesPathOverride);
                    var elems = doc.Descendants("map");
                    foreach (var el in elems)
                    {
                        var idxAttr = el.Attribute("index");
                        if (idxAttr == null) continue;
                        if (!int.TryParse(idxAttr.Value, out int idx)) continue;
                        if (idx != fileIndex) continue;

                        var wAttr = el.Attribute("width");
                        var hAttr = el.Attribute("height");
                        if (wAttr != null && hAttr != null && int.TryParse(wAttr.Value, out int w) && int.TryParse(hAttr.Value, out int h))
                        {
                            width = w;
                            height = h;
                            return true;
                        }
                    }
                }
                catch
                {
                    // ignore parse errors and continue detection
                }
            }

            // First try to read sizes from AppData mapnames.xml if present (allows manual overrides)
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string xml = Path.Combine(appData, "UoFiddler", "Mapnames.xml");

                if (File.Exists(xml))
                {
                    var doc = XDocument.Load(xml);
                    var mapElem = doc.Root?.Element("map");
                    // Support both <maps><map .../></maps> and flat file where root is maps
                    // Find the map element with matching index attribute
                    var elems = doc.Descendants("map");
                    foreach (var el in elems)
                    {
                        var idxAttr = el.Attribute("index");
                        if (idxAttr == null) continue;
                        if (!int.TryParse(idxAttr.Value, out int idx)) continue;
                        if (idx != fileIndex) continue;

                        var wAttr = el.Attribute("width");
                        var hAttr = el.Attribute("height");
                        if (wAttr != null && hAttr != null && int.TryParse(wAttr.Value, out int w) && int.TryParse(hAttr.Value, out int h))
                        {
                            width = w;
                            height = h;
                            return true;
                        }
                    }
                }
            }
            catch
            {
                // ignore parse errors and continue detection
            }

            // First try staidx (12 bytes per block)
            string idxPath = path == null
                ? Files.GetFilePath($"staidx{fileIndex}.mul")
                : Path.Combine(path, $"staidx{fileIndex}.mul");

            if (!string.IsNullOrEmpty(idxPath) && File.Exists(idxPath))
            {
                long len = new FileInfo(idxPath).Length;
                if (len > 0)
                {
                    long blocks = len / 12;
                    return TryFromBlockCount(blocks, out width, out height);
                }
            }

            // Fallback to map mul (196 bytes per 8x8 block)
            string mapPath = path == null
                ? Files.GetFilePath($"map{fileIndex}.mul")
                : Path.Combine(path, $"map{fileIndex}.mul");

            if (!string.IsNullOrEmpty(mapPath) && File.Exists(mapPath))
            {
                // If this is a UOP file we can't easily derive blocks here, so bail out.
                if (mapPath.EndsWith(".uop", System.StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                long len = new FileInfo(mapPath).Length;
                if (len > 0)
                {
                    long blocks = len / 196; // 4 header + 64*(2+1) = 196 bytes per block
                    return TryFromBlockCount(blocks, out width, out height);
                }
            }

            return false;
        }

        private static bool TryFromBlockCount(long blocks, out int width, out int height)
        {
            width = 0;
            height = 0;

            // Known block widths (map width / 8)
            int[] candidateBlockWidths = { 896, 768, 320, 288, 181, 160 };

            foreach (int bw in candidateBlockWidths)
            {
                if (blocks % bw == 0)
                {
                    long bh = blocks / bw;
                    // sanity check
                    if (bh > 0 && bh <= 8192)
                    {
                        width = bw * 8;
                        height = (int)bh * 8;
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
