using System;
using System.IO;
using System.Runtime.InteropServices;
using Ultima;

namespace UoFiddler.Plugin.Compare.Classes
{
    internal static class SecondHue
    {
        public static Hue[] List { get; private set; }

        /// <summary>
        /// Reads hues.mul and fills <see cref="List"/>
        /// </summary>
        public static void Initialize(string path)
        {
            int index = 0;

            List = new Hue[10000];

            if (path != null)
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    int blockCount = (int)fs.Length / 708;

                    if (blockCount > 1250)
                    {
                        blockCount = 1250;
                    }

                    int structSize = Marshal.SizeOf(typeof(HueDataMul));
                    var buffer = new byte[blockCount * (4 + (8 * structSize))];
                    GCHandle gc = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                    try
                    {
                        fs.Read(buffer, 0, buffer.Length);
                        long currentPos = 0;

                        for (int i = 0; i < blockCount; ++i)
                        {
                            // 4-byte header per block is unused on the Compare side.
                            currentPos += 4;

                            for (int j = 0; j < 8; ++j, ++index)
                            {
                                var ptr = new IntPtr(gc.AddrOfPinnedObject() + currentPos);
                                currentPos += structSize;
                                var cur = (HueDataMul)Marshal.PtrToStructure(ptr, typeof(HueDataMul));
                                List[index] = new Hue(index, cur);
                            }
                        }
                    }
                    finally
                    {
                        gc.Free();
                    }
                }
            }

            for (; index < List.Length; ++index)
            {
                List[index] = new Hue(index);
            }
        }

        // TODO: unused method?
        // public static Hue GetHue(int index)
        // {
        //     index &= 0x3FFF;
        //
        //     if (index >= 0 && index < 3000)
        //     {
        //         return List[index];
        //     }
        //
        //     return List[0];
        // }
    }
}
