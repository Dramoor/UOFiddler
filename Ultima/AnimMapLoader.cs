using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace Ultima
{
    /// <summary>
    /// Loads animation file configuration from AnimMap.xml
    /// </summary>
    public static class AnimMapLoader
    {
        // Default fallback segments (from Anim2) for any anim file not defined in AnimMap.xml
        private static readonly List<AnimMapSegment> _defaultSegments = new List<AnimMapSegment>
        {
            new AnimMapSegment(0, 200, 110),
            new AnimMapSegment(200, -1, 65)
        };

        /// <summary>
        /// Loads animation configuration from AnimMap.xml or AnimMap_{profile}.xml if profile is provided
        /// Returns a dictionary keyed by file type (0-N) mapping to AnimMapConfiguration
        /// </summary>
        public static Dictionary<int, AnimMapConfiguration> LoadAnimMap(string baseDir, string profileName = null)
        {
            var animMap = new Dictionary<int, AnimMapConfiguration>();

            // Determine which file to load
            string filePath = null;

            // Try profile-specific file first if profile is provided
            if (!string.IsNullOrEmpty(profileName))
            {
                filePath = Path.Combine(baseDir, $"AnimMap_{profileName}.xml");
                System.Diagnostics.Debug.WriteLine($"AnimMapLoader: Looking for profile-specific AnimMap at {filePath}");

                if (!File.Exists(filePath))
                {
                    System.Diagnostics.Debug.WriteLine($"AnimMapLoader: Profile-specific AnimMap not found, falling back to default");
                    filePath = null;
                }
            }

            // Fall back to default AnimMap.xml if no profile-specific file
            if (filePath == null)
            {
                filePath = Path.Combine(baseDir, "AnimMap.xml");
                System.Diagnostics.Debug.WriteLine($"AnimMapLoader: Using default AnimMap at {filePath}");
            }

            if (!File.Exists(filePath))
            {
                System.Diagnostics.Debug.WriteLine($"AnimMapLoader: AnimMap.xml not found at {filePath}, will use defaults for dynamic files");
                return animMap;
            }

            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(filePath);

                XmlNodeList animFiles = doc.SelectNodes("//AnimFile");
                System.Diagnostics.Debug.WriteLine($"AnimMapLoader: Found {animFiles.Count} animation file definitions in {filePath}");
                foreach (XmlNode animFileNode in animFiles)
                {
                    if (animFileNode is XmlElement animFileElement)
                    {
                        string fileName = animFileElement.GetAttribute("file");
                        System.Diagnostics.Debug.WriteLine($"  - {fileName}");
                    }
                }

                foreach (XmlNode animFileNode in animFiles)
                {
                    if (animFileNode is XmlElement animFileElement)
                    {
                        string fileName = animFileElement.GetAttribute("file");
                        if (string.IsNullOrEmpty(fileName))
                            continue;

                        // Extract file type from the filename
                        // anim.idx → fileType 1
                        // anim2.idx → fileType 2
                        // anim7.idx → fileType 7
                        // anim99.idx → fileType 99
                        int fileType = ExtractFileTypeFromName(fileName);
                        if (fileType < 1)
                        {
                            System.Diagnostics.Debug.WriteLine($"AnimMapLoader: Could not extract valid file type from {fileName}");
                            continue;
                        }

                        // Convert anim.idx to anim.mul, anim2.idx to anim2.mul, etc.
                        string mulFileName = fileName.Replace(".idx", ".mul");

                        var config = new AnimMapConfiguration(fileName, mulFileName, fileType);

                        XmlNodeList segments = animFileElement.SelectNodes("Segment");
                        foreach (XmlNode segmentNode in segments)
                        {
                            if (segmentNode is XmlElement segmentElement)
                            {
                                if (int.TryParse(segmentElement.GetAttribute("start"), out int start) &&
                                    int.TryParse(segmentElement.GetAttribute("entriesPerBody"), out int entriesPerBody))
                                {
                                    int end = -1;  // Default to open-ended
                                    if (!string.IsNullOrEmpty(segmentElement.GetAttribute("end")))
                                    {
                                        int.TryParse(segmentElement.GetAttribute("end"), out end);
                                    }

                                    int offset = 0;  // Default to no offset
                                    if (!string.IsNullOrEmpty(segmentElement.GetAttribute("offset")))
                                    {
                                        int.TryParse(segmentElement.GetAttribute("offset"), out offset);
                                    }

                                    config.Segments.Add(new AnimMapSegment(start, end, entriesPerBody, offset));
                                    System.Diagnostics.Debug.WriteLine($"    Segment: start={start}, end={end}, entriesPerBody={entriesPerBody}, offset={offset}");
                                }
                            }
                        }

                        animMap[fileType] = config;
                        System.Diagnostics.Debug.WriteLine($"AnimMapLoader: Loaded {fileName} (fileType={fileType}) with {config.Segments.Count} segments");
                    }
                }

                System.Diagnostics.Debug.WriteLine($"AnimMapLoader: Successfully loaded {animMap.Count} animation configurations");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading AnimMap.xml: {ex.Message}");
            }

            return animMap;
        }

        /// <summary>
        /// Gets configuration for a specific anim file, with fallback to Anim2 defaults if not in AnimMap.xml
        /// </summary>
        public static AnimMapConfiguration GetAnimConfiguration(string idxFileName, int fileTypeIndex)
        {
            string mulFileName = idxFileName.Replace(".idx", ".mul");
            var config = new AnimMapConfiguration(idxFileName, mulFileName, fileTypeIndex);

            // Use default Anim2 segments as fallback
            config.Segments.AddRange(_defaultSegments);

            System.Diagnostics.Debug.WriteLine($"AnimMapLoader: Using default configuration for {idxFileName}");

            return config;
        }

        /// <summary>
        /// Extracts the file type index from an animation filename.
        /// anim.idx → 1, anim2.idx → 2, anim7.idx → 7, etc.
        /// </summary>
        private static int ExtractFileTypeFromName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return -1;

            // Normalize to lowercase for comparison
            string name = fileName.ToLowerInvariant();

            // Check if it's "anim.idx" or "anim.mul" (which maps to fileType 1)
            if (name == "anim.idx" || name == "anim.mul")
                return 1;

            // Check if it matches "anim#.idx" or "anim#.mul" pattern
            if (name.StartsWith("anim") && (name.EndsWith(".idx") || name.EndsWith(".mul")))
            {
                string numberPart = name.Substring(4); // Skip "anim"
                if (name.EndsWith(".idx"))
                    numberPart = numberPart.Substring(0, numberPart.Length - 4); // Remove ".idx"
                else if (name.EndsWith(".mul"))
                    numberPart = numberPart.Substring(0, numberPart.Length - 4); // Remove ".mul"

                if (int.TryParse(numberPart, out int fileType) && fileType > 0)
                {
                    return fileType;
                }
            }

            return -1;
        }

        /// <summary>
        /// Gets the number of animation entries for a specific body in a file's configuration
        /// </summary>
        public static int GetEntriesPerBody(AnimMapConfiguration config, int body)
        {
            if (config?.Segments != null)
            {
                foreach (var segment in config.Segments)
                {
                    bool inRange = body >= segment.Start && 
                                   (segment.End == -1 || body < segment.End);
                    if (inRange)
                    {
                        return segment.EntriesPerBody;
                    }
                }
            }

            // Fallback
            return 110;
        }
    }
}
