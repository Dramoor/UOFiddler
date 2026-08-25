using System.Collections.Generic;

namespace Ultima
{
    /// <summary>
    /// Configuration for a single animation file from AnimMap.xml
    /// </summary>
    public class AnimMapConfiguration
    {
        /// <summary>
        /// The anim index filename (e.g., "anim7.idx")
        /// </summary>
        public string IdxFileName { get; set; }

        /// <summary>
        /// The anim data filename (e.g., "anim7.mul")
        /// </summary>
        public string MulFileName { get; set; }

        /// <summary>
        /// File type index (0-6 typically, but can be extended)
        /// </summary>
        public int FileType { get; set; }

        /// <summary>
        /// List of animation segments defining body ranges and entries per body
        /// </summary>
        public List<AnimMapSegment> Segments { get; set; } = new List<AnimMapSegment>();

        public AnimMapConfiguration()
        {
        }

        public AnimMapConfiguration(string idxFileName, string mulFileName, int fileType)
        {
            IdxFileName = idxFileName;
            MulFileName = mulFileName;
            FileType = fileType;
        }
    }

    /// <summary>
    /// Defines a segment of animations with consistent entry counts per body
    /// </summary>
    public class AnimMapSegment
    {
        /// <summary>
        /// Starting body ID for this segment
        /// </summary>
        public int Start { get; set; }

        /// <summary>
        /// Ending body ID for this segment (exclusive), or -1 if open-ended
        /// </summary>
        public int End { get; set; } = -1;

        /// <summary>
        /// Number of animation entries per body in this segment
        /// </summary>
        public int EntriesPerBody { get; set; }

        public AnimMapSegment()
        {
        }

        public AnimMapSegment(int start, int end, int entriesPerBody)
        {
            Start = start;
            End = end;
            EntriesPerBody = entriesPerBody;
        }
    }
}
