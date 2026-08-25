using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace Ultima
{
    /// <summary>
    /// Loads map definitions from Mapnames.xml
    /// </summary>
    public static class MapLoader
    {
        /// <summary>
        /// Loads map definitions from Mapnames.xml file
        /// </summary>
        /// <param name="filePath">Path to the Mapnames.xml file</param>
        /// <returns>List of MapDefinition objects, or empty list if file not found</returns>
        public static List<MapDefinition> LoadMapDefinitions(string filePath)
        {
            List<MapDefinition> maps = new List<MapDefinition>();

            if (!File.Exists(filePath))
            {
                System.Diagnostics.Debug.WriteLine($"MapLoader: File not found: {filePath}");
                return maps;
            }

            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(filePath);

                XmlNodeList mapNodes = doc.SelectNodes("//map");
                System.Diagnostics.Debug.WriteLine($"MapLoader: Found {mapNodes.Count} map nodes in {filePath}");

                foreach (XmlNode mapNode in mapNodes)
                {
                    if (mapNode is XmlElement mapElement)
                    {
                        string indexStr = mapElement.GetAttribute("index");
                        string name = mapElement.GetAttribute("name");
                        string widthStr = mapElement.GetAttribute("width");
                        string heightStr = mapElement.GetAttribute("height");

                        if (int.TryParse(indexStr, out int index) &&
                            int.TryParse(widthStr, out int width) &&
                            int.TryParse(heightStr, out int height) &&
                            !string.IsNullOrEmpty(name))
                        {
                            MapDefinition mapDef = new MapDefinition(index, name, width, height);
                            maps.Add(mapDef);
                            System.Diagnostics.Debug.WriteLine($"MapLoader: Loaded map {index}: {name} ({width}x{height})");
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"MapLoader: Successfully loaded {maps.Count} maps from {filePath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading map definitions from {filePath}: {ex.Message}");
            }

            return maps;
        }
    }
}
