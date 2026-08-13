using System;
using System.Collections.Generic;
using System.IO;

namespace Ultima
{
    /// <summary>
    /// Helper class for creating UOP files with proper formatting and structure
    /// Based on UOutils implementation by Dramoor
    /// </summary>
    internal class UOPFile
    {
        private List<UOPData> data = new List<UOPData>();
        private bool compress;
        private int version;
        private bool fulltables = true; // assumed by Mythic
        private bool mapheader = true;  // assumed by runuo (including some data in the map header)
        private int tableSize = 0x3E8; // 1000

        public UOPFile(int ver, bool comp)
        {
            version = ver;
            compress = comp;
        }

        public long FirstTable { get; set; } = 0x200;
        public int TableSize { get => tableSize; set => tableSize = value; }

        /// <summary>
        /// Add data to the UOP file with automatic compression
        /// </summary>
        public void Add(string name, MemoryStream header, MemoryStream dataStream)
        {
            UOPData ud = new UOPData();
            ud.name = name;
            ud.OSize = (uint)dataStream.Length;

            if (compress)
            {
                dataStream.Seek(0, SeekOrigin.Begin);
                MemoryStream compressedStream = ZlibNative.Compress(dataStream);
                if (compressedStream != null && compressedStream.Length > 0)
                {
                    ud.data = compressedStream.ToArray();
                    ud.CMethod = 1; // zlib compression
                }
                else
                {
                    ud.data = dataStream.ToArray();
                    ud.CMethod = 0; // no compression fallback
                }
            }
            else
            {
                ud.data = dataStream.ToArray();
                ud.CMethod = 0;
            }

            if (header != null)
                ud.header = header.ToArray();
            else
                ud.header = new byte[0];

            this.data.Add(ud);
        }

        /// <summary>
        /// Add raw data to the UOP file (already compressed or not to be compressed)
        /// </summary>
        public void AddRaw(string name, MemoryStream header, MemoryStream dataStream, uint decompressedSize, short compressionMethod)
        {
            UOPData ud = new UOPData();
            ud.name = name;
            ud.OSize = decompressedSize;
            ud.data = dataStream.ToArray();
            ud.CMethod = compressionMethod;

            if (header != null)
                ud.header = header.ToArray();
            else
                ud.header = new byte[0];

            this.data.Add(ud);
        }

        /// <summary>
        /// Copy data from a FileIndex into the UOP
        /// </summary>
        public void Copy(FileIndex fileIndex, int index, string entryName)
        {
            if (fileIndex == null)
                return;

            Stream stream = fileIndex.Seek(index, out int length, out int extra, out bool patched);
            if (stream == null)
            {
                Console.WriteLine("UOP copy failed {0}", entryName);
                return;
            }

            MemoryStream dataMs = new MemoryStream();
            stream.CopyTo(dataMs);
            dataMs.Flush();

            MemoryStream headerMs = new MemoryStream();
            if (extra > 0)
            {
                headerMs.Write(BitConverter.GetBytes((ushort)3));     // version??
                headerMs.Write(BitConverter.GetBytes((ushort)8));
                headerMs.Write(BitConverter.GetBytes((ushort)0x08db)); // changes
                headerMs.Write(BitConverter.GetBytes((ushort)0x511c));
                headerMs.Write(BitConverter.GetBytes((uint)0x01d4c4ff)); // fixed?
            }

            AddRaw(entryName, headerMs, dataMs, (uint)length, 0);
        }

        /// <summary>
        /// Save the UOP file to disk
        /// </summary>
        public void Save(string filePath)
        {
            // Sanity check: at least 0x28 bytes needed for the header
            if (FirstTable < 0x28)
                throw new Exception("At least 0x28 bytes are needed for the header.");

            using (BinaryWriter writer = new BinaryWriter(new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None)))
            {
                // File header
                writer.Write(0x50594D); // MYP
                writer.Write(version); // version
                writer.Write(0xFD23EC43); // format timestamp
                writer.Write(FirstTable); // first table offset
                writer.Write(TableSize); // table size
                writer.Write(data.Count); // file count
                writer.Write(1); // modified count
                writer.Write(1); // unknown
                writer.Write(0); // unknown

                // Padding
                for (int i = 0x28; i < FirstTable; i++)
                {
                    writer.Write((byte)0);
                }

                int tableCount = (int)Math.Ceiling((double)data.Count / TableSize);

                // Reserve space for tables and entries (will fill in later)
                byte bb = 0;
                if (fulltables)
                {
                    for (int i = 0; i < 34 * TableSize * tableCount + 12 * tableCount; i++)
                        writer.Write(bb);
                }
                else
                {
                    for (int i = 0; i < 34 * data.Count + 12 * tableCount; i++)
                        writer.Write(bb);
                }

                // Write data blocks
                for (int j = 0; j < data.Count; j++)
                {
                    UOPData ud = data[j];
                    writer.Flush();
                    ud.Offset = writer.BaseStream.Position;
                    ud.Identifier = Multis.HashLittle2(ud.name);
                    ud.Hash = Multis.HashAdler32(ud.data);

                    writer.Write(ud.header);
                    writer.Write(ud.data);
                }

                // Write tables
                writer.BaseStream.Seek(FirstTable, SeekOrigin.Begin);

                for (int i = 0; i < tableCount; i++)
                {
                    writer.Flush();
                    long thisTable = writer.BaseStream.Position;

                    int idxStart = i * TableSize;
                    int idxEnd = (i + 1) * TableSize;
                    if (idxEnd > data.Count)
                        idxEnd = data.Count;

                    int num = idxEnd - idxStart;
                    long tableNxt = 0;
                    if (i + 1 != tableCount)
                    {
                        tableNxt = 12 + 34 * num + thisTable;
                    }

                    if (fulltables)
                        writer.Write(TableSize); // 4 bytes
                    else
                        writer.Write(num); // 4 bytes

                    writer.Write(tableNxt); // 8 bytes - next table offset

                    // Write table entries
                    for (int j = 0; j < num; j++)
                    {
                        UOPData ud = data[idxStart + j];
                        writer.Write(ud.Offset); // 8 bytes
                        writer.Write(ud.header.Length); // 4 bytes
                        writer.Write(ud.CSize); // 4 bytes - compressed size
                        writer.Write(ud.OSize); // 4 bytes - original size
                        writer.Write(ud.Identifier); // 8 bytes
                        writer.Write(ud.Hash); // 4 bytes
                        writer.Write(ud.CMethod); // 2 bytes - compression method
                    }

                    // Pad empty entries if using full tables
                    if (fulltables)
                    {
                        for (int j = num; j < TableSize; j++)
                        {
                            writer.Write((ulong)0); // 8 bytes
                            writer.Write((uint)0); // 4 bytes
                            writer.Write((uint)0); // 4 bytes
                            writer.Write((uint)0); // 4 bytes
                            writer.Write((ulong)0); // 8 bytes
                            writer.Write((uint)0); // 4 bytes
                            writer.Write((ushort)0); // 2 bytes
                        }
                    }
                }

                writer.Close();
            }
        }
    }

    /// <summary>
    /// Helper class to hold UOP entry data
    /// </summary>
    internal class UOPData
    {
        internal byte[] data = new byte[0];
        internal uint Hash;
        internal string name;
        internal byte[] header = new byte[0];

        internal uint OSize; // Original/decompressed size
        internal short CMethod; // Compression method (0 = none, 1 = zlib)
        public long Offset { get; internal set; }
        public ulong Identifier { get; internal set; }
        internal uint CSize => (uint)data.Length; // Compressed size
        internal uint HSize => (uint)header.Length; // Header size
    }
}
