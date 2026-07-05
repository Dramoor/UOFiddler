using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Ultima.Helpers;
using System.Reflection;
using System;

namespace Ultima
{     
    public static class Art
    {
        private static FileIndex _fileIndex;
        private static Bitmap[] _cache;
        private static bool[] _removed;
        private static readonly Dictionary<int, bool> _patched = new Dictionary<int, bool>();
        public static bool Modified;

        private static byte[] _streamBuffer;
        private static readonly byte[] _validBuffer = new byte[4];

        private struct ImageData
        {
            public byte[] Data;
            public int Position;
            public int Length;
        }

        private static List<ImageData> _landImageData;
        private static List<ImageData> _staticImageData;

        static Art()
        {
            _cache = new Bitmap[0x14000];
            _removed = new bool[0x14000];

            InitializeFileIndex();
        }

        public static int GetMaxItemId()
        {
            // High Seas
            if (GetIdxLength() >= 0x13FDC)
            {
                return 0xFFDC;
            }

            // Stygian Abyss
            if (GetIdxLength() == 0xC000)
            {
                return 0x7FFF;
            }

            // ML and older
            return 0x3FFF;
        }

        public static bool IsUOAHS()
        {
            return GetIdxLength() >= 0x13FDC;
        }

        public static ushort GetLegalItemId(int itemId, bool checkMaxId = true)
        {
            if (itemId < 0)
            {
                return 0;
            }

            if (!checkMaxId)
            {
                return (ushort)itemId;
            }

            int max = GetMaxItemId();
            if (itemId > max)
            {
                return 0;
            }

            return (ushort)itemId;
        }

        public static int GetIdxLength()
        {
            return (int)(_fileIndex.IdxLength / 12);
        }

        /// <summary>
        /// ReReads Art.mul
        /// </summary>
        public static void Reload()
        {
            InitializeFileIndex();
            _cache = new Bitmap[0x14000];
            _removed = new bool[0x14000];
            _patched.Clear();
            Modified = false;
        }

        private static void InitializeFileIndex()
        {
            try
            {
                // Ensure MulPath is loaded
                Files.LoadMulPath();

                const string idxKey = "artidx.mul";
                const string mulKey = "art.mul";
                const string uopKey = "artlegacymul.uop";

                // Always resolve art files from Files.RootDir only. Do not attempt to register or
                // store art paths in MulPath. FileIndex will, when MulPathLocked is true, look
                // for the requested files inside Files.RootDir only.
                _fileIndex = new FileIndex(idxKey, mulKey, uopKey, 0x14000, 4, ".tga", 0x13FDC, false);
            }
            catch
            {
                // On failure leave _fileIndex null so callers gracefully handle missing data
                _fileIndex = null;
            }
        }

        /// <summary>
        /// Sets bmp of index in <see cref="_cache"/> of Static
        /// </summary>
        /// <param name="index"></param>
        /// <param name="bmp"></param>
        public static void ReplaceStatic(int index, Bitmap bmp)
        {
            index = GetLegalItemId(index);
            index += 0x4000;

            _cache[index] = bmp;
            _removed[index] = false;

            _patched.Remove(index);

            Modified = true;
        }

        /// <summary>
        /// Sets bmp of index in <see cref="_cache"/> of Land
        /// </summary>
        /// <param name="index"></param>
        /// <param name="bmp"></param>
        public static void ReplaceLand(int index, Bitmap bmp)
        {
            index &= 0x3FFF;
            _cache[index] = bmp;
            _removed[index] = false;

            _patched.Remove(index);

            Modified = true;
        }

        /// <summary>
        /// Removes Static index <see cref="_removed"/>
        /// </summary>
        /// <param name="index"></param>
        public static void RemoveStatic(int index)
        {
            index = GetLegalItemId(index);
            index += 0x4000;
            _removed[index] = true;
            Modified = true;
        }

        /// <summary>
        /// Removes Land index <see cref="_removed"/>
        /// </summary>
        /// <param name="index"></param>
        public static void RemoveLand(int index)
        {
            index &= 0x3FFF;
            _removed[index] = true;
            Modified = true;
        }

        /// <summary>
        /// Tests if Static is defined (width and height check)
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public static bool IsValidStatic(int index)
        {
            index = GetLegalItemId(index);
            index += 0x4000;

            if (_removed[index])
            {
                return false;
            }

            if (_cache[index] != null)
            {
                return true;
            }

            Stream stream = _fileIndex.Seek(index, out int _, out int _, out bool _);

            if (stream == null)
            {
                return false;
            }

            stream.Seek(4, SeekOrigin.Current);
            stream.Read(_validBuffer, 0, 4);

            short width = (short)(_validBuffer[0] | (_validBuffer[1] << 8));
            short height = (short)(_validBuffer[2] | (_validBuffer[3] << 8));

            return width > 0 && height > 0;
        }

        // --- Embedded UOP creation (adapted from LegacyMulFileConverter.ToUop) ---
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

        private static void CreateUopFromStreams(Stream mulStream, Stream idxStream, string outFile)
        {
            if (mulStream == null || idxStream == null || string.IsNullOrEmpty(outFile))
                return;

            const long firstTable = 0x200;
            const int tableSize = 0x64;

            idxStream.Position = 0;
            mulStream.Position = 0;
            using (var reader = new BinaryReader(mulStream, System.Text.Encoding.Default, true))
            using (var readerIdx = new BinaryReader(idxStream, System.Text.Encoding.Default, true))
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

                string[] hashFormat = new string[] { "build/artlegacymul/{0:00000000}.tga", string.Empty };

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
                        reader.BaseStream.Seek(idxEntries[j].Offset, SeekOrigin.Begin);
                        byte[] data = reader.ReadBytes(idxEntries[j].Size);

                        tableEntries[tableIdx].Offset = writer.BaseStream.Position;
                        tableEntries[tableIdx].DecompressedSize = data.Length;
                        tableEntries[tableIdx].CompressionFlag = (short)CompressionFlag.None;

                        tableEntries[tableIdx].Identifier = HashLittle2(string.Format(hashFormat[0], idxEntries[j].Id));
                        tableEntries[tableIdx].Size = data.Length;
                        tableEntries[tableIdx].Hash = HashAdler32(data);
                        writer.Write(data);
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
                a += (uint)s[k] + ((uint)s[k + 1] << 8) + ((uint)s[k + 2] << 16) + ((uint)s[k + 3] << 24);
                b += (uint)s[k + 4] + ((uint)s[k + 5] << 8) + ((uint)s[k + 6] << 16) + ((uint)s[k + 7] << 24);
                c += (uint)s[k + 8] + ((uint)s[k + 9] << 8) + ((uint)s[k + 10] << 16) + ((uint)s[k + 11] << 24);

                // scramble
                c = c ^ b; c -= (b << 14) | (b >> 18);
                a = a ^ c; a -= (c << 11) | (c >> 21);
                b = b ^ a; b -= (a << 25) | (a >> 7);
                c = c ^ b; c -= (b << 16) | (b >> 16);
                a = a ^ c; a -= (c << 4) | (c >> 28);
                b = b ^ a; b -= (a << 14) | (a >> 18);
                c = c ^ b; c -= (b << 24) | (b >> 8);

                k += 12;
                length -= 12;
            }

            // tail
            uint aa = a, bb = b, cc = c;
            switch (length)
            {
                case 12: cc += (uint)s[k + 11] << 24; goto case 11;
                case 11: cc += (uint)s[k + 10] << 16; goto case 10;
                case 10: cc += (uint)s[k + 9] << 8; goto case 9;
                case 9: cc += (uint)s[k + 8]; goto case 8;
                case 8: bb += (uint)s[k + 7] << 24; goto case 7;
                case 7: bb += (uint)s[k + 6] << 16; goto case 6;
                case 6: bb += (uint)s[k + 5] << 8; goto case 5;
                case 5: bb += (uint)s[k + 4]; goto case 4;
                case 4: aa += (uint)s[k + 3] << 24; goto case 3;
                case 3: aa += (uint)s[k + 2] << 16; goto case 2;
                case 2: aa += (uint)s[k + 1] << 8; goto case 1;
                case 1: aa += (uint)s[k]; break;
            }

            cc = (cc ^ bb) - ((bb >> 18) ^ (bb << 14));
            uint ecx = (cc ^ aa) - ((aa >> 21) ^ (aa << 11));
            bb = (bb ^ ecx) - ((ecx >> 7) ^ (ecx << 25));
            aa = (aa ^ bb) - ((bb >> 16) ^ (bb << 16));
            cc = (cc ^ aa) - ((aa >> 28) ^ (aa << 4));
            bb = (bb ^ cc) - ((cc >> 18) ^ (cc << 14));
            aa = (aa ^ bb) - ((bb >> 8) ^ (bb << 24));

            return ((ulong)bb << 32) | cc;
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

            return (b << 16) | a;
        }

        /// <summary>
        /// Tests if LandTile is defined
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public static bool IsValidLand(int index)
        {
            index &= 0x3FFF;
            if (_removed[index])
            {
                return false;
            }

            if (_cache[index] != null)
            {
                return true;
            }

            return _fileIndex.Valid(index, out int _, out int _, out bool _);
        }

        /// <summary>
        /// Returns Bitmap of LandTile (with Cache)
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public static Bitmap GetLand(int index)
        {
            return GetLand(index, out bool _);
        }

        /// <summary>
        /// Returns Bitmap of LandTile (with Cache) and verdata bool
        /// </summary>
        /// <param name="index"></param>
        /// <param name="patched"></param>
        /// <returns></returns>
        public static Bitmap GetLand(int index, out bool patched)
        {
            index &= 0x3FFF;
            patched = _patched.ContainsKey(index) && _patched[index];

            if (_removed[index])
            {
                return null;
            }

            if (_cache[index] != null)
            {
                return _cache[index];
            }

            Stream stream = _fileIndex.Seek(index, out int length, out int _, out patched);
            if (stream == null)
            {
                return null;
            }

            if (patched)
            {
                _patched[index] = true;
            }

            if (Files.CacheData)
            {
                return _cache[index] = LoadLand(stream, length);
            }

            return LoadLand(stream, length);
        }

        // ReSharper disable once UnusedMember.Global
        public static byte[] GetRawLand(int index)
        {
            index &= 0x3FFF;

            Stream stream = _fileIndex.Seek(index, out int length, out int _, out bool _);
            if (stream == null)
            {
                return null;
            }

            var buffer = new byte[length];
            stream.Read(buffer, 0, length);
            stream.Close();
            return buffer;
        }

        /// <summary>
        /// Returns Bitmap of Static (with Cache)
        /// </summary>
        /// <param name="index"></param>
        /// <param name="checkMaxId"></param>
        /// <returns></returns>
        public static Bitmap GetStatic(int index, bool checkMaxId = true)
        {
            return GetStatic(index, out bool _, checkMaxId);
        }

        /// <summary>
        /// Returns Bitmap of Static (with Cache) and verdata bool
        /// </summary>
        /// <param name="index"></param>
        /// <param name="patched"></param>
        /// <param name="checkMaxId"></param>
        /// <returns></returns>
        public static Bitmap GetStatic(int index, out bool patched, bool checkMaxId = true)
        {
            index = GetLegalItemId(index, checkMaxId);
            index += 0x4000;

            patched = _patched.ContainsKey(index) && _patched[index];

            if (_removed[index])
            {
                return null;
            }

            if (_cache[index] != null)
            {
                return _cache[index];
            }

            Stream stream = _fileIndex.Seek(index, out int length, out int _, out patched);
            if (stream == null)
            {
                return null;
            }

            if (patched)
            {
                _patched[index] = true;
            }

            if (Files.CacheData)
            {
                return _cache[index] = LoadStatic(stream, length);
            }

            return LoadStatic(stream, length);
        }

        // ReSharper disable once UnusedMember.Global
        public static byte[] GetRawStatic(int index)
        {
            index = GetLegalItemId(index);
            index += 0x4000;

            Stream stream = _fileIndex.Seek(index, out int length, out int _, out bool _);
            if (stream == null)
            {
                return null;
            }

            var buffer = new byte[length];
            stream.Read(buffer, 0, length);
            stream.Close();
            return buffer;
        }

        public static unsafe void Measure(Bitmap bmp, out int xMin, out int yMin, out int xMax, out int yMax)
        {
            xMin = yMin = 0;
            xMax = yMax = -1;

            if (bmp == null || bmp.Width <= 0 || bmp.Height <= 0)
            {
                return;
            }

            BitmapData bd = bmp.LockBits(
                new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format16bppArgb1555);

            int delta = (bd.Stride >> 1) - bd.Width;
            int lineDelta = bd.Stride >> 1;

            var pBuffer = (ushort*)bd.Scan0;
            ushort* pLineEnd = pBuffer + bd.Width;
            ushort* pEnd = pBuffer + (bd.Height * lineDelta);

            bool foundPixel = false;

            int x = 0, y = 0;

            while (pBuffer < pEnd)
            {
                while (pBuffer < pLineEnd)
                {
                    ushort c = *pBuffer++;

                    if ((c & 0x8000) != 0)
                    {
                        if (!foundPixel)
                        {
                            foundPixel = true;
                            xMin = xMax = x;
                            yMin = yMax = y;
                        }
                        else
                        {
                            if (x < xMin)
                            {
                                xMin = x;
                            }

                            if (y < yMin)
                            {
                                yMin = y;
                            }

                            if (x > xMax)
                            {
                                xMax = x;
                            }

                            if (y > yMax)
                            {
                                yMax = y;
                            }
                        }
                    }
                    ++x;
                }

                pBuffer += delta;
                pLineEnd += lineDelta;
                ++y;
                x = 0;
            }

            bmp.UnlockBits(bd);
        }

        private static unsafe Bitmap LoadStatic(Stream stream, int length)
        {
            if (_streamBuffer == null || _streamBuffer.Length < length)
            {
                _streamBuffer = new byte[length];
            }

            stream.Read(_streamBuffer, 0, length);
            stream.Close();

            Bitmap bmp;
            fixed (byte* data = _streamBuffer)
            {
                var binData = (ushort*)data;
                int count = 2;
                int width = binData[count++];
                int height = binData[count++];

                if (width <= 0 || height <= 0)
                {
                    return null;
                }

                var lookups = new int[height];

                int start = height + 4;

                for (int i = 0; i < height; ++i)
                {
                    lookups[i] = start + binData[count++];
                }

                bmp = new Bitmap(width, height, PixelFormat.Format16bppArgb1555);
                BitmapData bd = bmp.LockBits(
                    new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format16bppArgb1555);

                var line = (ushort*)bd.Scan0;
                int delta = bd.Stride >> 1;

                for (int y = 0; y < height; ++y, line += delta)
                {
                    count = lookups[y];

                    ushort* cur = line;
                    int xOffset, xRun;

                    while ((xOffset = binData[count++]) + (xRun = binData[count++]) != 0)
                    {
                        if (xOffset > delta)
                        {
                            break;
                        }

                        cur += xOffset;
                        if (xOffset + xRun > delta)
                        {
                            break;
                        }

                        ushort* end = cur + xRun;
                        while (cur < end)
                        {
                            *cur++ = (ushort)(binData[count++] ^ 0x8000);
                        }
                    }
                }

                bmp.UnlockBits(bd);
            }

            return bmp;
        }

        private static unsafe Bitmap LoadLand(Stream stream, int length)
        {
            var bmp = new Bitmap(44, 44, PixelFormat.Format16bppArgb1555);
            BitmapData bd = bmp.LockBits(new Rectangle(0, 0, 44, 44), ImageLockMode.WriteOnly, PixelFormat.Format16bppArgb1555);
            if (_streamBuffer == null || _streamBuffer.Length < length)
            {
                _streamBuffer = new byte[length];
            }

            stream.Read(_streamBuffer, 0, length);
            stream.Close();
            fixed (byte* binData = _streamBuffer)
            {
                var bdata = (ushort*)binData;
                int xOffset = 21;
                int xRun = 2;

                var line = (ushort*)bd.Scan0;
                int delta = bd.Stride >> 1;

                for (int y = 0; y < 22; ++y, --xOffset, xRun += 2, line += delta)
                {
                    ushort* cur = line + xOffset;
                    ushort* end = cur + xRun;

                    while (cur < end)
                    {
                        *cur++ = (ushort)(*bdata++ | 0x8000);
                    }
                }

                xOffset = 0;
                xRun = 44;

                for (int y = 0; y < 22; ++y, ++xOffset, xRun -= 2, line += delta)
                {
                    ushort* cur = line + xOffset;
                    ushort* end = cur + xRun;

                    while (cur < end)
                    {
                        *cur++ = (ushort)(*bdata++ | 0x8000);
                    }
                }
            }

            bmp.UnlockBits(bd);

            return bmp;
        }

        /// <summary>
        /// Saves mul
        /// </summary>
        /// <param name="path"></param>
        public static unsafe void Save(string path)
        {
            _landImageData = new List<ImageData>();
            _staticImageData = new List<ImageData>();

            string idx = Path.Combine(path, "artidx.mul");
            string mul = Path.Combine(path, "art.mul");

            using (var fsidx = new FileStream(idx, FileMode.Create, FileAccess.Write, FileShare.Write))
            using (var fsmul = new FileStream(mul, FileMode.Create, FileAccess.Write, FileShare.Write))
            {
                var memidx = new MemoryStream();
                var memmul = new MemoryStream();

                using (var binidx = new BinaryWriter(memidx))
                using (var binmul = new BinaryWriter(memmul))
                {
                    for (int index = 0; index < GetIdxLength(); index++)
                    {
                        Files.FireFileSaveEvent();
                        if (_cache[index] == null)
                        {
                            if (index < 0x4000)
                            {
                                _cache[index] = GetLand(index);
                            }
                            else
                            {
                                _cache[index] = GetStatic(index - 0x4000, false);
                            }
                        }

                        Bitmap bmp = _cache[index];
                        if (bmp == null || _removed[index])
                        {
                            binidx.Write(-1); // lookup
                            binidx.Write(0);  // Length
                            binidx.Write(-1); // extra
                        }
                        else if (index < 0x4000)
                        {
                            byte[] imageData = bmp.ToArray(PixelFormat.Format16bppArgb1555).ToSha256();
                            if (CompareSaveImagesLand(imageData, out ImageData resultImageData))
                            {
                                binidx.Write(resultImageData.Position); // lookup
                                binidx.Write(resultImageData.Length);
                                binidx.Write(0);

                                continue;
                            }

                            // land
                            BitmapData bd = bmp.LockBits(
                                new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly,
                                PixelFormat.Format16bppArgb1555);
                            var line = (ushort*)bd.Scan0;
                            int delta = bd.Stride >> 1;
                            binidx.Write((int)binmul.BaseStream.Position); // lookup
                            var length = (int)binmul.BaseStream.Position;
                            int x = 22;
                            int y = 0; // TODO: y is never used?
                            int lineWidth = 2;
                            for (int m = 0; m < 22; ++m, ++y, line += delta, lineWidth += 2)
                            {
                                --x;
                                ushort* cur = line;
                                for (int n = 0; n < lineWidth; ++n)
                                {
                                    binmul.Write((ushort)(cur[x + n] ^ 0x8000));
                                }
                            }

                            x = 0;
                            lineWidth = 44;
                            y = 22;
                            line = (ushort*)bd.Scan0;
                            line += delta * 22;
                            for (int m = 0; m < 22; m++, y++, line += delta, ++x, lineWidth -= 2)
                            {
                                ushort* cur = line;
                                for (int n = 0; n < lineWidth; n++)
                                {
                                    binmul.Write((ushort)(cur[x + n] ^ 0x8000));
                                }
                            }

                            int start = length;
                            length = (int)binmul.BaseStream.Position - length;
                            binidx.Write(length);
                            binidx.Write(0);
                            bmp.UnlockBits(bd);

                            _landImageData.Add(new ImageData
                            {
                                Position = start,
                                Length = length,
                                Data = imageData
                            });
                        }
                        else
                        {
                            byte[] imageData = bmp.ToArray(PixelFormat.Format16bppArgb1555).ToSha256();
                            if (CompareSaveImagesStatic(imageData, out ImageData resultImageData))
                            {
                                binidx.Write(resultImageData.Position); // lookup
                                binidx.Write(resultImageData.Length);
                                binidx.Write(0);

                                continue;
                            }

                            // art
                            BitmapData bd = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format16bppArgb1555);
                            var line = (ushort*)bd.Scan0;
                            int delta = bd.Stride >> 1;
                            binidx.Write((int)binmul.BaseStream.Position); // lookup
                            var length = (int)binmul.BaseStream.Position;
                            binmul.Write(1234); // header //TODO: check what to write to header? Maybe different value will be better?
                            binmul.Write((short)bmp.Width);
                            binmul.Write((short)bmp.Height);
                            var lookup = (int)binmul.BaseStream.Position;
                            int streamLoc = lookup + (bmp.Height * 2);
                            int width = 0;
                            for (int i = 0; i < bmp.Height; ++i) // fill lookup
                            {
                                binmul.Write(width);
                            }

                            for (int y = 0; y < bmp.Height; ++y, line += delta)
                            {
                                ushort* cur = line;
                                width = (int)(binmul.BaseStream.Position - streamLoc) / 2;
                                binmul.BaseStream.Seek(lookup + (y * 2), SeekOrigin.Begin);
                                binmul.Write(width);
                                binmul.BaseStream.Seek(streamLoc + (width * 2), SeekOrigin.Begin);
                                int i = 0;
                                int x = 0;
                                while (i < bmp.Width)
                                {
                                    for (i = x; i <= bmp.Width; ++i)
                                    {
                                        // first pixel set
                                        if (i >= bmp.Width)
                                        {
                                            continue;
                                        }

                                        if (cur[i] != 0)
                                        {
                                            break;
                                        }
                                    }

                                    if (i >= bmp.Width)
                                    {
                                        continue;
                                    }

                                    int j;
                                    for (j = i + 1; j < bmp.Width; ++j)
                                    {
                                        // next non set pixel
                                        if (cur[j] == 0)
                                        {
                                            break;
                                        }
                                    }

                                    binmul.Write((short)(i - x)); // xOffset
                                    binmul.Write((short)(j - i)); // run

                                    for (int p = i; p < j; ++p)
                                    {
                                        binmul.Write((ushort)(cur[p] ^ 0x8000));
                                    }

                                    x = j;
                                }

                                binmul.Write((short)0); // xOffset
                                binmul.Write((short)0); // Run
                            }

                            int start = length;
                            length = (int)binmul.BaseStream.Position - length;
                            binidx.Write(length);
                            binidx.Write(0);
                            bmp.UnlockBits(bd);

                            _staticImageData.Add(new ImageData
                            {
                                Position = start,
                                Length = length,
                                Data = imageData
                            });
                        }
                    }

                    memidx.WriteTo(fsidx);
                    memmul.WriteTo(fsmul);

                    // ensure files are flushed and closed so conversion can open them
                    try
                    {
                        fsidx.Flush();
                        fsmul.Flush();
                        fsidx.Close();
                        fsmul.Close();
                    }
                    catch { }
                }
            // If a legacy UOP target exists in the configured MulPath, try to auto-create/overwrite it
            try
            {
                // always create the .uop next to the mul files being written
                string uopPath = Path.Combine(path, "artLegacyMUL.uop");
                if (!string.IsNullOrEmpty(uopPath))
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
                        // Attempt to find the UOP packer type in already loaded assemblies
                        Type convType = null;
                        foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                        {
                            convType = a.GetType("UoFiddler.Plugin.UopPacker.Classes.LegacyMulFileConverter");
                            if (convType != null) break;
                        }

                        // If not loaded, try loading the plugin DLL from the application's plugins folder
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
                            MethodInfo toUop = convType.GetMethod("ToUop", BindingFlags.Public | BindingFlags.Static);
                            if (toUop != null)
                            {
                                // Determine FileType enum value from the converter assembly
                                Type fileTypeEnum = convType.Assembly.GetType("UoFiddler.Plugin.UopPacker.Classes.FileType");
                                object fileTypeVal = null;
                                if (fileTypeEnum != null)
                                {
                                    fileTypeVal = Enum.Parse(fileTypeEnum, "ArtLegacyMul");
                                }
                                // Invoke ToUop(inMul, inIdx, outUop, FileType.ArtLegacyMul, 0, CompressionFlag.None)
                                // Last parameter is Ultima.CompressionFlag which is shared from this assembly
                                try
                                {
                                    toUop.Invoke(null, new object[] { mul, idx, uopPath, fileTypeVal, 0, CompressionFlag.None });
                                    invoked = true;
                                }
                                catch (Exception ex)
                                {
                                    // record failure to invoke plugin converter
                                    try
                                    {
                                        string logFile = Path.Combine(path, "uop_plugin_error.log");
                                        File.AppendAllText(logFile, ex + System.Environment.NewLine + "----" + System.Environment.NewLine);
                                    }
                                    catch { }
                                }
                            }
                        }

                        // If plugin converter not available or invocation failed, use embedded conversion on the written files
                        if (!invoked)
                        {
                            try
                            {
                                using (var fileMul = new FileStream(mul, FileMode.Open, FileAccess.Read, FileShare.Read))
                                using (var fileIdx = new FileStream(idx, FileMode.Open, FileAccess.Read, FileShare.Read))
                                {
                                    CreateUopFromStreams(fileMul, fileIdx, uopPath);
                                }
                            }
                            catch (Exception ex)
                            {
                                // write diagnostic to app folder where files were saved
                                try
                                {
                                    string logFile = Path.Combine(path, "uop_create_error.log");
                                    File.AppendAllText(logFile, ex + System.Environment.NewLine + "----" + System.Environment.NewLine);
                                }
                                catch { }
                            }
                        }
                    }
                }
            }
            catch
            {
                // conversion failure should not prevent normal save; swallow exceptions
            }
            }
        }

        private static bool CompareSaveImagesLand(IReadOnlyList<byte> newChecksum, out ImageData sum)
        {
            sum = new ImageData();
            for (int i = 0; i < _landImageData.Count; ++i)
            {
                byte[] cmp = _landImageData[i].Data;
                if (cmp == null || newChecksum == null || cmp.Length != newChecksum.Count)
                {
                    return false;
                }

                bool valid = true;

                for (int j = 0; j < cmp.Length; ++j)
                {
                    if (cmp[j] == newChecksum[j])
                    {
                        continue;
                    }

                    valid = false;
                    break;
                }

                if (!valid)
                {
                    continue;
                }

                sum = _landImageData[i];

                return true;
            }

            return false;
        }

        private static bool CompareSaveImagesStatic(byte[] imageData, out ImageData resultImageData)
        {
            resultImageData = new ImageData();

            for (int i = 0; i < _staticImageData.Count; ++i)
            {
                byte[] cmp = _staticImageData[i].Data;

                if (cmp == null || imageData == null || cmp.Length != imageData.Length)
                {
                    return false;
                }

                bool valid = true;

                for (int j = 0; j < cmp.Length; ++j)
                {
                    if (cmp[j] == imageData[j])
                    {
                        continue;
                    }

                    valid = false;
                    break;
                }

                if (!valid)
                {
                    continue;
                }

                resultImageData = _staticImageData[i];

                return true;
            }

            return false;
        }
    }
}