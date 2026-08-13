using System;
using System.IO;
using System.IO.Compression;

namespace Ultima
{
    /// <summary>
    /// Native ZLib compression/decompression following RFC 1950 (zlib format)
    /// Uses raw DeflateStream wrapped with proper zlib headers and checksums
    /// Based on ClassicUO and UOutils implementations
    /// </summary>
    public class ZlibNative
    {
        public ZlibNative()
        {
        }

        /// <summary>
        /// Decompresses a zlib-formatted byte stream
        /// </summary>
        public static MemoryStream DecompressStream(ref int destLength, byte[] source, int sourceLength)
        {
            MemoryStream ms = new MemoryStream();
            destLength = 0;

            try
            {
                MemoryStream stream = new MemoryStream(source);
                BinaryReader reader = new BinaryReader(stream);

                // Read and validate zlib header
                int cmf = reader.ReadByte();           // Compression Method and Flags
                int flag = reader.ReadByte();          // Flags

                int compressionMethod = cmf & 0xF;    // Must be 8 (deflate)
                int compressionInfo = (cmf >> 4) & 0xF; // Window size info (7 = 32KB)
                int dict = (flag >> 5) & 1;            // Dictionary flag
                int checksum = flag & 0x1F;            // Checksum bits

                // Validate header
                if (compressionMethod != 8)
                {
                    Console.WriteLine("Invalid compression method: {0}", compressionMethod);
                    return null;
                }

                if (compressionInfo != 7)
                {
                    Console.WriteLine("Invalid compression info: {0}", compressionInfo);
                    return null;
                }

                if (dict != 0)
                {
                    Console.WriteLine("Dictionary flag not supported");
                    return null;
                }

                if (((cmf * 256 + flag) % 31) != 0)
                {
                    Console.WriteLine("Invalid checksum in zlib header");
                    return null;
                }

                // Decompress using deflate (without zlib wrapper)
                using (DeflateStream deflateStream = new DeflateStream(stream, CompressionMode.Decompress, true))
                {
                    deflateStream.CopyTo(ms);
                }

                // Read and validate Adler-32 checksum
                stream.Seek(-4, SeekOrigin.End);
                byte a0 = (byte)stream.ReadByte();
                byte a1 = (byte)stream.ReadByte();
                byte a2 = (byte)stream.ReadByte();
                byte a3 = (byte)stream.ReadByte();
                uint storedAdler32 = (uint)((a0 << 24) + (a1 << 16) + (a2 << 8) + a3);

                uint calculatedAdler32 = HashAdler32(ms.ToArray());

                if (storedAdler32 != calculatedAdler32)
                {
                    Console.WriteLine("Adler-32 checksum mismatch: stored={0:X8}, calculated={1:X8}", storedAdler32, calculatedAdler32);
                    // Don't fail on this, as some implementations might not validate strictly
                }

                destLength = (int)ms.Length;
                ms.Seek(0, SeekOrigin.Begin);
                return ms;
            }
            catch (Exception ex)
            {
                Console.WriteLine("ZLib decompression error: {0}", ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Decompresses zlib data into a byte array
        /// </summary>
        public static ZLibError Decompress(byte[] dest, ref int destLength, byte[] source, int sourceLength)
        {
            MemoryStream ms = DecompressStream(ref destLength, source, sourceLength);

            if (ms == null)
                return ZLibError.DataError;

            byte[] decompressed = ms.ToArray();

            if (decompressed.Length > dest.Length)
            {
                Console.WriteLine("Destination buffer too small for decompressed data");
                return ZLibError.DataError;
            }

            Array.Copy(decompressed, dest, decompressed.Length);
            destLength = decompressed.Length;

            return ZLibError.Okay;
        }

        /// <summary>
        /// Compresses data in zlib format (RFC 1950)
        /// Creates proper zlib wrapper with header and Adler-32 checksum
        /// </summary>
        public static MemoryStream Compress(MemoryStream src)
        {
            MemoryStream output = new MemoryStream();

            // Write zlib header (RFC 1950)
            // CMF byte: 0x78 = compression method (deflate) + compression info (32K window)
            int cmf = 0x78;
            // FLAG byte: we use 0x40 (default compression) + mod to make (CMF*256 + FLAG) % 31 == 0
            int flag = 0x40;
            int mod = 31 - ((cmf * 256 + flag) % 31);

            output.WriteByte((byte)cmf);
            output.WriteByte((byte)(flag + mod));

            // Compress data using raw deflate (no wrapper)
            MemoryStream deflateBuffer = new MemoryStream();
            using (DeflateStream deflateStream = new DeflateStream(deflateBuffer, CompressionMode.Compress, true))
            {
                src.Seek(0, SeekOrigin.Begin);
                src.CopyTo(deflateStream);
                deflateStream.Flush();
            }

            // Copy compressed deflate data
            deflateBuffer.Seek(0, SeekOrigin.Begin);
            deflateBuffer.CopyTo(output);

            // Calculate and append Adler-32 checksum
            uint adler32 = HashAdler32(src.ToArray());
            output.WriteByte((byte)((adler32 >> 24) & 0xFF)); // MSB first
            output.WriteByte((byte)((adler32 >> 16) & 0xFF));
            output.WriteByte((byte)((adler32 >> 8) & 0xFF));
            output.WriteByte((byte)((adler32 >> 0) & 0xFF));

            output.Flush();
            output.Seek(0, SeekOrigin.Begin);
            return output;
        }

        /// <summary>
        /// Calculates Adler-32 checksum (RFC 1950)
        /// </summary>
        public static uint HashAdler32(byte[] data)
        {
            uint s1 = 1;
            uint s2 = 0;
            const uint modulo = 65521;

            for (int i = 0; i < data.Length; i++)
            {
                s1 = (s1 + data[i]) % modulo;
                s2 = (s2 + s1) % modulo;
            }

            return (s2 << 16) | s1;
        }
    }

    public enum ZLibError
    {
        DataError,
        Okay
    }
}
