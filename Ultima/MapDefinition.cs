using System;

namespace Ultima
{
    /// <summary>
    /// Represents a map definition loaded from Mapnames.xml
    /// </summary>
    public class MapDefinition
    {
        /// <summary>
        /// Gets the index of the map (0-based identifier)
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// Gets the name of the map
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets the width of the map in tiles
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// Gets the height of the map in tiles
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// Creates a new map definition
        /// </summary>
        public MapDefinition()
        {
        }

        /// <summary>
        /// Creates a new map definition with specified values
        /// </summary>
        public MapDefinition(int index, string name, int width, int height)
        {
            Index = index;
            Name = name;
            Width = width;
            Height = height;
        }

        public override string ToString()
        {
            return $"MapDefinition(Index={Index}, Name='{Name}', Width={Width}, Height={Height})";
        }
    }
}
