using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Ultima.Helpers;
using System.Reflection;

namespace Ultima
{
    public sealed class Multis
    {
        public const int MaximumMultiIndex = 0x2200;

        private static MultiComponentList[] _components = new MultiComponentList[MaximumMultiIndex];
        private static FileIndex _fileIndex = new FileIndex("Multi.idx", "Multi.mul", "multicollection.uop", MaximumMultiIndex, 14, ".bin", 14, false);

        public enum ImportType
        {
            TXT,
            UOA,
            UOAB,
            WSC,
            CSV, // Punt's multi tool csv format
            UOX3,
            MULTICACHE,
            UOADESIGN,
            XML
        }


        /// <summary>
        /// ReReads multi.mul
        /// </summary>
        public static void Reload()
        {
            _fileIndex = new FileIndex("Multi.idx", "Multi.mul", "multicollection.uop", MaximumMultiIndex, 14, ".bin", 14, false);
            _components = new MultiComponentList[MaximumMultiIndex];
        }

        /// <summary>
        /// Gets <see cref="MultiComponentList"/> of multi
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public static MultiComponentList GetComponents(int index)
        {
            MultiComponentList mcl;

            if (index >= 0 && index < _components.Length)
            {
                mcl = _components[index];

                if (mcl == null)
                {
                    _components[index] = mcl = Load(index);
                }
            }
            else
            {
                mcl = MultiComponentList.Empty;
            }

            return mcl;
        }


        private struct IdxEntry
        {
            public int Id;
            public int Offset;
            public int Size;
            public int Extra;
        }

        private struct TableEntry
        {
            public long Offset;
            public int HeaderLength;
            public int Size;
            public int DecompressedSize;
            public ulong Identifier;
            public uint Hash;
            public short CompressionFlag;
            public bool Compressed;
        }

        private static readonly byte[] _emptyUopTableEntry = new byte[8 + 4 + 4 + 4 + 8 + 4 + 2];

        private static void CreateMultiUopFromFiles(string mulPath, string idxPath, string outFile)
        {
            if (string.IsNullOrEmpty(mulPath) || string.IsNullOrEmpty(idxPath) || string.IsNullOrEmpty(outFile))
                return;

            const long firstTable = 0x200;
            const int tableSize = 0x64;

            using (var fileMul = new FileStream(mulPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var fileIdx = new FileStream(idxPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new BinaryReader(fileMul, System.Text.Encoding.Default, true))
            using (var readerIdx = new BinaryReader(fileIdx, System.Text.Encoding.Default, true))
            using (var writer = new BinaryWriter(new FileStream(outFile, FileMode.Create, FileAccess.Write, FileShare.None)))
            {
                var idxEntries = new System.Collections.Generic.List<IdxEntry>();

                int idxEntryCount = (int)(readerIdx.BaseStream.Length / 12);
                for (int i = 0; i < idxEntryCount; ++i)
                {
                    int offset = readerIdx.ReadInt32();

                    if (offset < 0)
                    {
                        readerIdx.BaseStream.Seek(8, SeekOrigin.Current);
                        continue;
                    }

                    IdxEntry e = new IdxEntry
                    {
                        Id = i,
                        Offset = offset,
                        Size = readerIdx.ReadInt32(),
                        Extra = readerIdx.ReadInt32()
                    };

                    idxEntries.Add(e);
                }

                // If a housing.bin exists next to the mul file, include it as an extra UOP entry
                string housingPath = Path.Combine(Path.GetDirectoryName(mulPath) ?? string.Empty, "housing.bin");
                if (File.Exists(housingPath))
                {
                    var fi = new FileInfo(housingPath);
                    IdxEntry he = new IdxEntry
                    {
                        Id = idxEntries.Count,
                        Offset = -2, // special marker: read from external housing file
                        Size = (int)Math.Min(int.MaxValue, fi.Length),
                        Extra = 0
                    };

                    idxEntries.Add(he);
                }

                // File header
                writer.Write(0x50594D); // MYP
                writer.Write(5); // version
                writer.Write(0xFD23EC43); // format timestamp?
                writer.Write(firstTable); // first table
                writer.Write(tableSize); // table size
                writer.Write(idxEntries.Count); // file count
                writer.Write(0); // modified count?
                writer.Write(0);
                writer.Write(0);

                // Padding
                for (int i = 0x28; i < firstTable; ++i)
                {
                    writer.Write((byte)0);
                }

                int tableCount = (int)System.Math.Ceiling((double)idxEntries.Count / tableSize);
                TableEntry[] tableEntries = new TableEntry[tableSize];

                string[] hashFormat = new string[] { "build/multicollection/{0:000000}.bin", string.Empty };

                for (int t = 0; t < tableCount; ++t)
                {
                    long thisTable = writer.BaseStream.Position;

                    int idxStart = t * tableSize;
                    int idxEnd = System.Math.Min((t + 1) * tableSize, idxEntries.Count);

                    // Table header
                    writer.Write(idxEnd - idxStart);
                    writer.Write((long)0); // next table, filled in later
                    writer.Seek(34 * tableSize, SeekOrigin.Current); // table entries, filled in later

                    // Data
                    int tableIdx = 0;

                    for (int j = idxStart; j < idxEnd; ++j, ++tableIdx)
                    {
                        byte[] data;

                        if (idxEntries[j].Offset == -2)
                        {
                            // housing.bin special case
                            string housingPathLocal = Path.Combine(Path.GetDirectoryName(mulPath) ?? string.Empty, "housing.bin");
                            data = File.ReadAllBytes(housingPathLocal);
                        }
                        else
                        {
                            reader.BaseStream.Seek(idxEntries[j].Offset, SeekOrigin.Begin);
                            data = reader.ReadBytes(idxEntries[j].Size);
                        }

                        tableEntries[tableIdx].Offset = writer.BaseStream.Position;

                        // Keep original (decompressed) size
                        int decompressedSize = data.Length;

                        // Try to compress with zlib
                        var compressResult = UopUtils.Compress(data);
                        if (compressResult.success && compressResult.compressedData.Length > 0)
                        {
                            byte[] compressed = compressResult.compressedData;
                            tableEntries[tableIdx].DecompressedSize = decompressedSize;
                            tableEntries[tableIdx].Size = compressed.Length;
                            tableEntries[tableIdx].CompressionFlag = (short)CompressionFlag.Zlib;

                            if (idxEntries[j].Offset == -2)
                            {
                                tableEntries[tableIdx].Identifier = HashLittle2("build/multicollection/housing.bin");
                            }
                            else
                            {
                                tableEntries[tableIdx].Identifier = HashLittle2(string.Format(hashFormat[0], idxEntries[j].Id));
                            }

                            tableEntries[tableIdx].Hash = HashAdler32(compressed);
                            writer.Write(compressed);
                        }
                        else
                        {
                            tableEntries[tableIdx].DecompressedSize = decompressedSize;
                            tableEntries[tableIdx].Size = data.Length;
                            tableEntries[tableIdx].CompressionFlag = (short)CompressionFlag.None;

                            if (idxEntries[j].Offset == -2)
                            {
                                tableEntries[tableIdx].Identifier = HashLittle2("build/multicollection/housing.bin");
                            }
                            else
                            {
                                tableEntries[tableIdx].Identifier = HashLittle2(string.Format(hashFormat[0], idxEntries[j].Id));
                            }

                            tableEntries[tableIdx].Hash = HashAdler32(data);
                            writer.Write(data);
                        }
                    }

                    long nextTable = writer.BaseStream.Position;

                    // Go back and fix table header
                    if (t < tableCount - 1)
                    {
                        writer.BaseStream.Seek(thisTable + 4, SeekOrigin.Begin);
                        writer.Write(nextTable);
                    }
                    else
                    {
                        writer.BaseStream.Seek(thisTable + 12, SeekOrigin.Begin);
                        // No need to fix the next table address, it's the last
                    }

                    // Table entries
                    tableIdx = 0;

                    for (int j = idxStart; j < idxEnd; ++j, ++tableIdx)
                    {
                        writer.Write(tableEntries[tableIdx].Offset);
                        writer.Write(0); // header length
                        writer.Write(tableEntries[tableIdx].Size); // compressed size
                        writer.Write(tableEntries[tableIdx].DecompressedSize); // decompressed size
                        writer.Write(tableEntries[tableIdx].Identifier);
                        writer.Write(tableEntries[tableIdx].Hash);
                        writer.Write(tableEntries[tableIdx].CompressionFlag); // compression method
                    }

                    // Fill remainder with empty entries
                    for (; tableIdx < tableSize; ++tableIdx)
                    {
                        writer.Write(_emptyUopTableEntry);
                    }

                    writer.BaseStream.Seek(nextTable, SeekOrigin.Begin);
                }
            }


        }

        private static ulong HashLittle2(string s)
        {
            int length = s.Length;

            uint a, b, c;
            a = b = c = 0xDEADBEEF + (uint)length;

            int k = 0;

            while (length > 12)
            {
                a += s[k];
                a += (uint)s[k + 1] << 8;
                a += (uint)s[k + 2] << 16;
                a += (uint)s[k + 3] << 24;
                b += s[k + 4];
                b += (uint)s[k + 5] << 8;
                b += (uint)s[k + 6] << 16;
                b += (uint)s[k + 7] << 24;
                c += s[k + 8];
                c += (uint)s[k + 9] << 8;
                c += (uint)s[k + 10] << 16;
                c += (uint)s[k + 11] << 24;

                a -= c; a ^= c << 4 | c >> 28; c += b;
                b -= a; b ^= a << 6 | a >> 26; a += c;
                c -= b; c ^= b << 8 | b >> 24; b += a;
                a -= c; a ^= c << 16 | c >> 16; c += b;
                b -= a; b ^= a << 19 | a >> 13; a += c;
                c -= b; c ^= b << 4 | b >> 28; b += a;

                length -= 12;
                k += 12;
            }

            if (length == 0)
            {
                return (ulong)b << 32 | c;
            }

            switch (length)
            {
                case 12: c += (uint)s[k + 11] << 24; goto case 11;
                case 11: c += (uint)s[k + 10] << 16; goto case 10;
                case 10: c += (uint)s[k + 9] << 8; goto case 9;
                case 9: c += s[k + 8]; goto case 8;
                case 8: b += (uint)s[k + 7] << 24; goto case 7;
                case 7: b += (uint)s[k + 6] << 16; goto case 6;
                case 6: b += (uint)s[k + 5] << 8; goto case 5;
                case 5: b += s[k + 4]; goto case 4;
                case 4: a += (uint)s[k + 3] << 24; goto case 3;
                case 3: a += (uint)s[k + 2] << 16; goto case 2;
                case 2: a += (uint)s[k + 1] << 8; goto case 1;
                case 1: a += s[k]; break;
            }

            c ^= b; c -= b << 14 | b >> 18;
            a ^= c; a -= c << 11 | c >> 21;
            b ^= a; b -= a << 25 | a >> 7;
            c ^= b; c -= b << 16 | b >> 16;
            a ^= c; a -= c << 4 | c >> 28;
            b ^= a; b -= a << 14 | a >> 18;
            c ^= b; c -= b << 24 | b >> 8;
            a ^= c; a -= c << 4 | c >> 28;
            b ^= a; b -= a << 14 | a >> 18;

            return (ulong)b << 32 | c;
        }

        private static uint HashAdler32(byte[] d)
        {
            uint a = 1;
            uint b = 0;

            for (int i = 0; i < d.Length; i++)
            {
                a = (a + d[i]) % 65521;
                b = (b + a) % 65521;
            }

            return b << 16 | a;
        }

        public static MultiComponentList Load(int index)
        {
            try
            {
                Stream stream = _fileIndex.Seek(index, out int length, out int _, out bool _);

                if (stream == null)
                {
                    return MultiComponentList.Empty;
                }

                if (Art.IsUOAHS())
                {
                    return new MultiComponentList(new BinaryReader(stream), length / 16, true);
                }
                else
                {
                    return new MultiComponentList(new BinaryReader(stream), length / 12, false);
                }
            }
            catch
            {
                return MultiComponentList.Empty;
            }
        }

        public static void Remove(int index)
        {
            _components[index] = MultiComponentList.Empty;
        }

        public static void Add(int index, MultiComponentList comp)
        {
            _components[index] = comp;
        }

        public static MultiComponentList ImportFromFile(int index, string fileName, ImportType type)
        {
            try
            {
                return _components[index] = new MultiComponentList(fileName, type);
            }
            catch
            {
                return _components[index] = MultiComponentList.Empty;
            }
        }

        public static MultiComponentList LoadFromFile(string fileName, ImportType type)
        {
            try
            {
                return new MultiComponentList(fileName, type);
            }
            catch
            {
                return MultiComponentList.Empty;
            }
        }

        public static List<MultiComponentList> LoadFromCache(string fileName)
        {
            var multiComponentLists = new List<MultiComponentList>();
            using (var ip = new StreamReader(fileName))
            {
                while (ip.ReadLine() is { } line)
                {
                    string[] split = Regex.Split(line, @"\s+");
                    if (split.Length != 7)
                    {
                        continue;
                    }

                    int count = Convert.ToInt32(split[2]);
                    multiComponentLists.Add(new MultiComponentList(ip, count));
                }
            }
            return multiComponentLists;
        }

        public static List<object[]> LoadFromDesigner(string fileName)
        {
            var multiList = new List<object[]>();

            string root = Path.GetFileNameWithoutExtension(fileName);
            string idx = $"{root}.idx";
            string bin = $"{root}.bin";

            if ((!File.Exists(idx)) || (!File.Exists(bin)))
            {
                return multiList;
            }

            using (var idxfs = new FileStream(idx, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var binfs = new FileStream(bin, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (var idxbin = new BinaryReader(idxfs))
                using (var binbin = new BinaryReader(binfs))
                {
                    int count = idxbin.ReadInt32();
                    int version = idxbin.ReadInt32();

                    for (int i = 0; i < count; ++i)
                    {
                        var data = new object[2];

                        switch (version)
                        {
                            case 0:
                                data[0] = MultiHelpers.ReadUOAString(idxbin);
                                var arr = new List<MultiComponentList.MultiTileEntry>();
                                data[0] += "-" + MultiHelpers.ReadUOAString(idxbin);
                                data[0] += "-" + MultiHelpers.ReadUOAString(idxbin);

                                _ = idxbin.ReadInt32();
                                _ = idxbin.ReadInt32();
                                _ = idxbin.ReadInt32();
                                _ = idxbin.ReadInt32();

                                long filepos = idxbin.ReadInt64();
                                int reccount = idxbin.ReadInt32();

                                binbin.BaseStream.Seek(filepos, SeekOrigin.Begin);
                                for (int j = 0; j < reccount; ++j)
                                {
                                    int x;
                                    int y;
                                    int z;
                                    int index = x = y = z = 0;

                                    switch (binbin.ReadInt32())
                                    {
                                        case 0:
                                            index = binbin.ReadInt32();
                                            x = binbin.ReadInt32();
                                            y = binbin.ReadInt32();
                                            z = binbin.ReadInt32();
                                            binbin.ReadInt32();
                                            break;

                                        case 1:
                                            index = binbin.ReadInt32();
                                            x = binbin.ReadInt32();
                                            y = binbin.ReadInt32();
                                            z = binbin.ReadInt32();
                                            binbin.ReadInt32();
                                            binbin.ReadInt32();
                                            break;
                                    }

                                    var tempItem =
                                        new MultiComponentList.MultiTileEntry
                                        {
                                            ItemId = (ushort)index,
                                            Flags = 1,
                                            OffsetX = (short)x,
                                            OffsetY = (short)y,
                                            OffsetZ = (short)z,
                                            Unk1 = 0
                                        };
                                    arr.Add(tempItem);
                                }

                                data[1] = new MultiComponentList(arr);
                                break;
                        }

                        multiList.Add(data);
                    }
                }

                return multiList;
            }
        }

        private static List<MultiComponentList.MultiTileEntry> RebuildTiles(MultiComponentList.MultiTileEntry[] tiles)
        {
            var newTiles = new List<MultiComponentList.MultiTileEntry>();
            newTiles.AddRange(tiles);

            if (newTiles[0].OffsetX == 0 && newTiles[0].OffsetY == 0 && newTiles[0].OffsetZ == 0) // found a center item
            {
                if (newTiles[0].ItemId != 0x1) // its a "good" one
                {
                    for (int j = newTiles.Count - 1; j >= 0; --j) // remove all invis items
                    {
                        if (newTiles[j].ItemId == 0x1)
                        {
                            newTiles.RemoveAt(j);
                        }
                    }
                    return newTiles;
                }
                else // a bad one
                {
                    for (int i = 1; i < newTiles.Count; ++i) // do we have a better one?
                    {
                        if (newTiles[i].OffsetX != 0 || newTiles[i].OffsetY != 0 || newTiles[i].ItemId == 0x1 ||
                            newTiles[i].OffsetZ != 0)
                        {
                            continue;
                        }

                        MultiComponentList.MultiTileEntry centerItem = newTiles[i];
                        newTiles.RemoveAt(i); // jep so save it

                        for (int j = newTiles.Count-1; j >= 0; --j) // and remove all invis
                        {
                            if (newTiles[j].ItemId == 0x1)
                            {
                                newTiles.RemoveAt(j);
                            }
                        }

                        newTiles.Insert(0, centerItem);

                        return newTiles;
                    }

                    for (int j = newTiles.Count-1; j >= 1; --j) // nothing found so remove all invis except the first
                    {
                        if (newTiles[j].ItemId == 0x1)
                        {
                            newTiles.RemoveAt(j);
                        }
                    }

                    return newTiles;
                }
            }

            for (int i = 0; i < newTiles.Count; ++i) // is there a good one
            {
                if (newTiles[i].OffsetX != 0 || newTiles[i].OffsetY != 0 || newTiles[i].ItemId == 0x1 ||
                    newTiles[i].OffsetZ != 0)
                {
                    continue;
                }

                MultiComponentList.MultiTileEntry centerItem = newTiles[i];
                newTiles.RemoveAt(i); // store it
                for (int j = newTiles.Count-1; j >= 0; --j) // remove all invis
                {
                    if (newTiles[j].ItemId == 0x1)
                    {
                        newTiles.RemoveAt(j);
                    }
                }

                newTiles.Insert(0, centerItem);

                return newTiles;
            }

            for (int j = newTiles.Count-1; j >= 0; --j) // nothing found so remove all invis
            {
                if (newTiles[j].ItemId == 0x1)
                {
                    newTiles.RemoveAt(j);
                }
            }

            // and create a new invis
            var invisItem =
                new MultiComponentList.MultiTileEntry
                {
                    ItemId = 0x1,
                    OffsetX = 0,
                    OffsetY = 0,
                    OffsetZ = 0,
                    Flags = 0,
                    Unk1 = 0
                };

            newTiles.Insert(0, invisItem);

            return newTiles;
        }

        public static void Save(string path)
        {
            bool isUOAHS = Art.IsUOAHS();

            string idx = Path.Combine(path, "multi.idx");
            string mul = Path.Combine(path, "multi.mul");

            using (var fsidx = new FileStream(idx, FileMode.Create, FileAccess.Write, FileShare.Write))
            using (var fsmul = new FileStream(mul, FileMode.Create, FileAccess.Write, FileShare.Write))
            using (var binidx = new BinaryWriter(fsidx))
            using (var binmul = new BinaryWriter(fsmul))
            {
                for (int index = 0; index < MaximumMultiIndex; ++index)
                {
                    MultiComponentList comp = GetComponents(index);

                    if (comp == MultiComponentList.Empty)
                    {
                        binidx.Write(-1); // lookup
                        binidx.Write(-1); // length
                        binidx.Write(-1); // extra
                    }
                    else
                    {
                        List<MultiComponentList.MultiTileEntry> tiles = RebuildTiles(comp.SortedTiles);
                        binidx.Write((int)fsmul.Position); // lookup
                        if (isUOAHS)
                        {
                            binidx.Write(tiles.Count * 16); // length
                        }
                        else
                        {
                            binidx.Write(tiles.Count * 12); // length
                        }

                        binidx.Write(-1); // extra
                        for (int i = 0; i < tiles.Count; ++i)
                        {
                            binmul.Write(tiles[i].ItemId);
                            binmul.Write(tiles[i].OffsetX);
                            binmul.Write(tiles[i].OffsetY);
                            binmul.Write(tiles[i].OffsetZ);
                            binmul.Write(tiles[i].Flags);
                            if (isUOAHS)
                            {
                                binmul.Write(tiles[i].Unk1);
                            }
                        }
                    }
                }
            }

            // Optionally create MultiCollection.uop when saving if configured
            try
            {
                bool saveUop = false;
                try
                {
                    foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        var optType = a.GetType("UoFiddler.Controls.Classes.Options");
                        if (optType == null)
                            continue;

                        var prop = optType.GetProperty("SaveUopWhenSaving", BindingFlags.Public | BindingFlags.Static);
                        if (prop != null)
                        {
                            saveUop = (bool)prop.GetValue(null);
                            break;
                        }
                    }
                }
                catch { }

                if (saveUop)
                {
                    string uopPath = Path.Combine(path, "MultiCollection.uop");

                    // Try plugin converter first (reflection to avoid hard dependency)
                    try
                    {
                        Type convType = null;
                        foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                        {
                            convType = a.GetType("UoFiddler.Plugin.UopPacker.Classes.LegacyMulFileConverter");
                            if (convType != null) break;
                        }

                        if (convType == null)
                        {
                            string possible = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins", "UOPPacker.dll");
                            if (File.Exists(possible))
                            {
                                var asm = Assembly.LoadFrom(possible);
                                convType = asm.GetType("UoFiddler.Plugin.UopPacker.Classes.LegacyMulFileConverter");
                            }
                        }

                        bool invoked = false;
                        if (convType != null)
                        {
                            var toUop = convType.GetMethod("ToUop", BindingFlags.Public | BindingFlags.Static);
                            if (toUop != null)
                            {
                                Type fileTypeEnum = convType.Assembly.GetType("UoFiddler.Plugin.UopPacker.Classes.FileType");
                                object fileTypeVal = null;
                                if (fileTypeEnum != null)
                                {
                                    fileTypeVal = Enum.Parse(fileTypeEnum, "MultiCollection");
                                }

                                try
                                {
                                    toUop.Invoke(null, new object[] { mul, idx, uopPath, fileTypeVal, 0, CompressionFlag.Zlib });
                                    invoked = true;
                                }
                                catch { }
                            }
                        }

                        if (!invoked)
                        {
                            CreateMultiUopFromFiles(mul, idx, uopPath);
                        }
                    }
                    catch
                    {
                        try
                        {
                            CreateMultiUopFromFiles(mul, idx, uopPath);
                        }
                        catch { }
                    }
                }
            }
            catch { }

        }
    }
}