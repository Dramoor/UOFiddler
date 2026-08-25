/***************************************************************************
 *
 * $Author: Turley
 * 
 * "THE BEER-WARE LICENSE"
 * As long as you retain this notice you can do whatever you want with 
 * this stuff. If we meet some day, and you think this stuff is worth it,
 * you can buy me a beer in return.
 *
 ***************************************************************************/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml;
using Microsoft.Extensions.Logging;
using Ultima;
using Ultima.Helpers;
using UoFiddler.Controls.Classes;

namespace UoFiddler.Classes
{
    public static class FiddlerOptions
    {
        private static readonly ILogger _log = AppLog.For(typeof(FiddlerOptions));

        public static List<ExternTool> ExternTools { get; private set; }

        public static Version AppVersion => typeof(FiddlerOptions).Assembly.GetName().Version;

        /// <summary>
        /// Defines if an Update Check should be made on startup
        /// </summary>
        public static bool UpdateCheckOnStart { get; set; }

        public static string RepositoryOwner { get; } = "polserver";
        public static string RepositoryName { get; } = "UOFiddler";

        public static bool StoreFormState { get; set; }
        public static bool MaximisedForm { get; set; }
        public static Point FormPosition { get; set; }
        public static Size FormSize { get; set; }

        private static void MoveFiles(IEnumerable<FileInfo> files, string path)
        {
            foreach (FileInfo file in files)
            {
                string destFileName = Path.Combine(path, file.Name);
                if (File.Exists(destFileName))
                {
                    _log.LogInformation("MoveFiles. File exists. Skipping: {File}", destFileName);
                    continue;
                }

                _log.LogInformation("MoveFiles. Copying file: {File}", destFileName);
                file.CopyTo(destFileName);
            }
        }

        public static void Startup()
        {
            if (!Directory.Exists(Options.AppDataPath))
            {
                _log.LogInformation("Creating main app data path {AppDataPath}", Options.AppDataPath);
                Directory.CreateDirectory(Options.AppDataPath);
            }

            string plugInPath = Path.Combine(Options.AppDataPath, "plugins");
            if (!Directory.Exists(plugInPath))
            {
                _log.LogInformation("Creating app data plugin {AppDataPath}", plugInPath);
                Directory.CreateDirectory(plugInPath);
            }

            DirectoryInfo di = new DirectoryInfo(Application.StartupPath);
            MoveFiles(di.GetFiles("Options_default.xml", SearchOption.TopDirectoryOnly), Options.AppDataPath);
            MoveFiles(di.GetFiles("Animationlist.xml", SearchOption.TopDirectoryOnly), Options.AppDataPath);
            MoveFiles(di.GetFiles("Multilist.xml", SearchOption.TopDirectoryOnly), Options.AppDataPath);
            MoveFiles(di.GetFiles("Gumplist.xml", SearchOption.TopDirectoryOnly), Options.AppDataPath);
            MoveFiles(di.GetFiles("DynamicItems.xml", SearchOption.TopDirectoryOnly), Options.AppDataPath);
            MoveFiles(di.GetFiles("AnimMap.xml", SearchOption.TopDirectoryOnly), Options.AppDataPath);
            MoveFiles(di.GetFiles("Mapnames.xml", SearchOption.TopDirectoryOnly), Options.AppDataPath);

            di = new DirectoryInfo(Path.Combine(Application.StartupPath, "plugins"));
            MoveFiles(di.GetFiles("*.xml", SearchOption.TopDirectoryOnly), plugInPath);

            string fileName = Path.Combine(Options.AppDataPath, "Options_default.xml");
            if (!File.Exists(fileName))
            {
                _log.LogCritical("Can't find default profile file: {FileName}", fileName);
                throw new FileNotFoundException($"Can't load default profile file {fileName}", "Options_default.xml");
            }

            DynamicItemsConfig.EnsureLoaded();

            // Initialize maps from Mapnames.xml or profile-specific version
            string mapnamesPath = null;
            string profile = null;
            if (!string.IsNullOrEmpty(Options.ProfileName))
                profile = Options.ProfileName.Replace("Options_", "").Replace(".xml", "");

            // Try profile-specific Mapnames first (including default profile!)
            if (!string.IsNullOrEmpty(profile))
                mapnamesPath = Path.Combine(Options.AppDataPath, $"Mapnames_{profile}.xml");

            // Fall back to default if no profile-specific file
            if (mapnamesPath == null || !File.Exists(mapnamesPath))
                mapnamesPath = Path.Combine(Options.AppDataPath, "Mapnames.xml");

            if (File.Exists(mapnamesPath))
            {
                _log.LogInformation("Initializing maps from {MapnamesPath}", mapnamesPath);
                Ultima.Map.InitializeFromXml(mapnamesPath);
                UoFiddler.Controls.Classes.Options.UpdateMapNamesFromMaps();
            }
            else
            {
                _log.LogWarning("Mapnames.xml not found at {MapnamesPath}, using default maps", mapnamesPath);
            }
        }

        public static void SaveProfile()
        {
            if (Options.ProfileName is null)
            {
                _log.LogWarning("SaveProfile - ProfileName is null!");
                return;
            }

            string fileName = Path.Combine(Options.AppDataPath, Options.ProfileName);
            _log.LogInformation("SaveProfile - start {Filename}", fileName);

            XmlDocument dom = new XmlDocument();
            XmlDeclaration decl = dom.CreateXmlDeclaration("1.0", "utf-8", null);
            dom.AppendChild(decl);
            XmlElement sr = dom.CreateElement("Options");

            XmlComment comment = dom.CreateComment("Output Path");
            sr.AppendChild(comment);
            XmlElement elem = dom.CreateElement("OutputPath");
            elem.SetAttribute("path", Options.OutputPath);
            sr.AppendChild(elem);
            comment = dom.CreateComment("ItemSize controls the size of images in items tab");
            sr.AppendChild(comment);
            elem = dom.CreateElement("ItemSize");
            elem.SetAttribute("width", Options.ArtItemSizeWidth.ToString());
            elem.SetAttribute("height", Options.ArtItemSizeHeight.ToString());
            sr.AppendChild(elem);
            comment = dom.CreateComment("ItemClip images in items tab shrinked or clipped");
            sr.AppendChild(comment);
            elem = dom.CreateElement("ItemClip");
            elem.SetAttribute("active", Options.ArtItemClip.ToString());
            sr.AppendChild(elem);
            comment = dom.CreateComment("CacheData should mul entries be cached for faster load");
            sr.AppendChild(comment);
            elem = dom.CreateElement("CacheData");
            elem.SetAttribute("active", Files.CacheData.ToString());
            sr.AppendChild(elem);
            // + Colors
            comment = dom.CreateComment("Focus tile color for tile views");
            sr.AppendChild(comment);
            elem = dom.CreateElement("TileFocusColor");
            elem.SetAttribute("value", ColorTranslator.ToHtml(Options.TileFocusColor));
            sr.AppendChild(elem);

            comment = dom.CreateComment("Selected tile color for tile views");
            sr.AppendChild(comment);
            elem = dom.CreateElement("TileSelectionColor");
            elem.SetAttribute("value", ColorTranslator.ToHtml(Options.TileSelectionColor));
            sr.AppendChild(elem);

            comment = dom.CreateComment("Remove tile border in tile views");
            sr.AppendChild(comment);
            elem = dom.CreateElement("RemoveTileBorder");
            elem.SetAttribute("active", Options.RemoveTileBorder.ToString());
            sr.AppendChild(elem);

            comment = dom.CreateComment("Preview background color for items and multis");
            sr.AppendChild(comment);
            elem = dom.CreateElement("PreviewBackgroundColor");
            elem.SetAttribute("value", ColorTranslator.ToHtml(Options.PreviewBackgroundColor));
            sr.AppendChild(elem);
            // - Colors
            comment = dom.CreateComment("NewMapSize Felucca/Trammel width 7168?");
            sr.AppendChild(comment);
            elem = dom.CreateElement("NewMapSize");
            elem.SetAttribute("active", Map.Felucca.Width == 7168 ? true.ToString() : false.ToString());
            sr.AppendChild(elem);
            comment = dom.CreateComment("UseMapDiff should mapdiff files be used");
            sr.AppendChild(comment);
            elem = dom.CreateElement("UseMapDiff");
            elem.SetAttribute("active", Map.UseDiff.ToString());
            sr.AppendChild(elem);
            comment = dom.CreateComment("Offset Sound Ids by 1 (POL specific setting)");
            sr.AppendChild(comment);
            elem = dom.CreateElement("PolSoundIdOffset");
            elem.SetAttribute("active", Options.PolSoundIdOffset.ToString());
            sr.AppendChild(elem);
            comment = dom.CreateComment("Should an Update Check be done on startup?");
            sr.AppendChild(comment);
            elem = dom.CreateElement("UpdateCheck");
            elem.SetAttribute("active", UpdateCheckOnStart.ToString());
            sr.AppendChild(elem);

            comment = dom.CreateComment("Defines the cmd to send Client to loc");
            sr.AppendChild(comment);
            comment = dom.CreateComment("{1} = x, {2} = y, {3} = z, {4} = mapid, {5} = mapname");
            sr.AppendChild(comment);
            elem = dom.CreateElement("SendCharToLoc");
            elem.SetAttribute("cmd", Options.MapCmd);
            elem.SetAttribute("args", Options.MapArgs);
            sr.AppendChild(elem);

            comment = dom.CreateComment("Defines the map names");
            sr.AppendChild(comment);
            elem = dom.CreateElement("MapNames");
            elem.SetAttribute("map0", Options.MapNames[0]);
            elem.SetAttribute("map1", Options.MapNames[1]);
            elem.SetAttribute("map2", Options.MapNames[2]);
            elem.SetAttribute("map3", Options.MapNames[3]);
            elem.SetAttribute("map4", Options.MapNames[4]);
            elem.SetAttribute("map5", Options.MapNames[5]);
            sr.AppendChild(elem);

            comment = dom.CreateComment("Extern Tools settings");
            sr.AppendChild(comment);

            if (ExternTools != null)
            {
                foreach (ExternTool tool in ExternTools)
                {
                    XmlElement externalToolElement = dom.CreateElement("ExternTool");
                    externalToolElement.SetAttribute("name", tool.Name);
                    externalToolElement.SetAttribute("path", tool.FileName);

                    for (int i = 0; i < tool.Args.Count; i++)
                    {
                        XmlElement argsElement = dom.CreateElement("Args");
                        argsElement.SetAttribute("name", tool.ArgsName[i]);
                        argsElement.SetAttribute("arg", tool.Args[i]);
                        externalToolElement.AppendChild(argsElement);
                    }
                    sr.AppendChild(externalToolElement);
                }
            }

            comment = dom.CreateComment("Loaded Plugins");
            sr.AppendChild(comment);
            if (Options.PluginsToLoad != null)
            {
                foreach (string plugIn in Options.PluginsToLoad)
                {
                    _log.LogInformation("SaveProfile - saving plugin {PlugIn}", plugIn);
                    XmlElement xmlPlugin = dom.CreateElement("Plugin");
                    xmlPlugin.SetAttribute("name", plugIn);
                    sr.AppendChild(xmlPlugin);
                }
            }

            comment = dom.CreateComment("Path settings");
            sr.AppendChild(comment);
            elem = dom.CreateElement("RootPath");
            elem.SetAttribute("path", Files.RootDir);
            sr.AppendChild(elem);
            // Individual file paths are no longer saved - all files are resolved from RootDir
            dom.AppendChild(sr);

            comment = dom.CreateComment("Disabled Tab Views");
            sr.AppendChild(comment);
            foreach (KeyValuePair<int, bool> kvp in Options.ChangedViewState)
            {
                if (kvp.Value)
                {
                    continue;
                }

                XmlElement viewState = dom.CreateElement("TabView");
                viewState.SetAttribute("tab", kvp.Key.ToString());
                sr.AppendChild(viewState);
            }

            comment = dom.CreateComment("ViewState of the MainForm");
            sr.AppendChild(comment);
            elem = dom.CreateElement("ViewState");
            elem.SetAttribute("Active", StoreFormState.ToString());
            elem.SetAttribute("Maximised", MaximisedForm.ToString());
            elem.SetAttribute("PositionX", FormPosition.X.ToString());
            elem.SetAttribute("PositionY", FormPosition.Y.ToString());
            elem.SetAttribute("Height", FormSize.Height.ToString());
            elem.SetAttribute("Width", FormSize.Width.ToString());
            sr.AppendChild(elem);

            comment = dom.CreateComment("TileData Options");
            sr.AppendChild(comment);
            elem = dom.CreateElement("TileDataDirectlySaveOnChange");
            elem.SetAttribute("value", Options.TileDataDirectlySaveOnChange.ToString());
            sr.AppendChild(elem);

            dom.Save(fileName);
            _log.LogInformation("SaveProfile - done {Filename}", fileName);
        }

        public static void LoadProfile(string filename)
        {
            _log.LogInformation("LoadProfile - start: {Filename}", filename);

            string fileName = Path.Combine(Options.AppDataPath, filename);
            if (!File.Exists(fileName))
            {
                _log.LogWarning("LoadProfile: profile file doesn't exist: {Filename}", filename);
                return;
            }

            XmlDocument dom = new XmlDocument();
            dom.Load(fileName);
            XmlElement xOptions = dom["Options"];
            XmlElement elem = (XmlElement)xOptions?.SelectSingleNode("OutputPath");
            if (elem != null)
            {
                Options.OutputPath = elem.GetAttribute("path");
                if (!Directory.Exists(Options.OutputPath))
                {
                    Options.OutputPath = Options.AppDataPath;
                }
            }
            else
            {
                Options.OutputPath = Options.AppDataPath;
            }

            elem = (XmlElement)xOptions.SelectSingleNode("ItemSize");
            if (elem != null)
            {
                Options.ArtItemSizeWidth = int.Parse(elem.GetAttribute("width"));
                Options.ArtItemSizeHeight = int.Parse(elem.GetAttribute("height"));
            }

            elem = (XmlElement)xOptions.SelectSingleNode("ItemClip");
            if (elem != null)
            {
                Options.ArtItemClip = bool.Parse(elem.GetAttribute("active"));
            }

            elem = (XmlElement)xOptions.SelectSingleNode("CacheData");
            if (elem != null)
            {
                Files.CacheData = bool.Parse(elem.GetAttribute("active"));
            }

            elem = (XmlElement)xOptions.SelectSingleNode("TileFocusColor");
            if (elem != null)
            {
                Options.TileFocusColor = ColorTranslator.FromHtml(elem.GetAttribute("value"));
            }

            elem = (XmlElement)xOptions.SelectSingleNode("TileSelectionColor");
            if (elem != null)
            {
                Options.TileSelectionColor = ColorTranslator.FromHtml(elem.GetAttribute("value"));
            }

            elem = (XmlElement)xOptions.SelectSingleNode("RemoveTileBorder");
            if (elem != null)
            {
                Options.RemoveTileBorder = bool.Parse(elem.GetAttribute("active"));
            }

            elem = (XmlElement)xOptions.SelectSingleNode("PreviewBackgroundColor");
            if (elem != null)
            {
                Options.PreviewBackgroundColor = ColorTranslator.FromHtml(elem.GetAttribute("value"));
            }

            elem = (XmlElement)xOptions.SelectSingleNode("NewMapSize");
            if (elem != null && bool.Parse(elem.GetAttribute("active")))
            {
                Map.Felucca.Width = 7168;
                Map.Trammel.Width = 7168;
            }

            elem = (XmlElement)xOptions.SelectSingleNode("UseMapDiff");
            if (elem != null)
            {
                Map.StartUpSetDiff(bool.Parse(elem.GetAttribute("active")));
            }

            elem = (XmlElement)xOptions.SelectSingleNode("PolSoundIdOffset");
            if (elem != null)
            {
                Options.PolSoundIdOffset = bool.Parse(elem.GetAttribute("active"));
            }

            elem = (XmlElement)xOptions.SelectSingleNode("UpdateCheck");
            if (elem != null)
            {
                UpdateCheckOnStart = bool.Parse(elem.GetAttribute("active"));
            }

            elem = (XmlElement)xOptions.SelectSingleNode("SendCharToLoc");
            if (elem != null)
            {
                Options.MapCmd = elem.GetAttribute("cmd");
                Options.MapArgs = elem.GetAttribute("args");
            }

            // Map names now come exclusively from Mapnames.xml, not from Options XML

            // Reload maps for the current profile
            string profileNameWithoutOptions = filename.Replace("Options_", "").Replace(".xml", "");
            string mapnamesPath = null;

            System.Diagnostics.Debug.WriteLine($"FiddlerOptions.LoadProfile: filename={filename}");
            System.Diagnostics.Debug.WriteLine($"FiddlerOptions.LoadProfile: profileNameWithoutOptions={profileNameWithoutOptions}");
            System.Diagnostics.Debug.WriteLine($"FiddlerOptions.LoadProfile: AppDataPath={Options.AppDataPath}");

            // Try profile-specific Mapnames first (including default profile!)
            if (!string.IsNullOrEmpty(profileNameWithoutOptions))
            {
                mapnamesPath = Path.Combine(Options.AppDataPath, $"Mapnames_{profileNameWithoutOptions}.xml");
                System.Diagnostics.Debug.WriteLine($"FiddlerOptions.LoadProfile: Looking for profile-specific maps at {mapnamesPath}");
                if (!File.Exists(mapnamesPath))
                {
                    System.Diagnostics.Debug.WriteLine($"FiddlerOptions.LoadProfile: Profile-specific map file NOT FOUND, falling back to default");
                    mapnamesPath = null;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"FiddlerOptions.LoadProfile: Profile-specific map file FOUND!");
                }
            }

            // Fall back to default if no profile-specific file
            if (mapnamesPath == null)
            {
                mapnamesPath = Path.Combine(Options.AppDataPath, "Mapnames.xml");
                System.Diagnostics.Debug.WriteLine($"FiddlerOptions.LoadProfile: Using default maps at {mapnamesPath}");
            }

            if (File.Exists(mapnamesPath))
            {
                _log.LogInformation("Loading maps from {MapnamesPath}", mapnamesPath);
                System.Diagnostics.Debug.WriteLine($"FiddlerOptions.LoadProfile: Loading from {mapnamesPath}");
                Ultima.Map.InitializeFromXml(mapnamesPath);
                UoFiddler.Controls.Classes.Options.UpdateMapNamesFromMaps();
                System.Diagnostics.Debug.WriteLine($"FiddlerOptions.LoadProfile: Loaded {Ultima.Map.GetAllMaps().Count()} maps total");

                // Notify UI that maps have changed
                UoFiddler.Controls.Classes.ControlEvents.FireMapNameChangeEvent();
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"FiddlerOptions.LoadProfile: ERROR - No map file found at {mapnamesPath}");
                _log.LogWarning("No map file found at {MapnamesPath}", mapnamesPath);
            }

            ExternTools = new List<ExternTool>();
            foreach (XmlElement xTool in xOptions.SelectNodes("ExternTool"))
            {
                string name = xTool.GetAttribute("name");
                string file = xTool.GetAttribute("path");
                ExternTool tool = new ExternTool(name, file);
                foreach (XmlElement xArg in xTool.SelectNodes("Args"))
                {
                    string argName = xArg.GetAttribute("name");
                    string arg = xArg.GetAttribute("arg");
                    tool.Args.Add(arg);
                    tool.ArgsName.Add(argName);
                }
                ExternTools.Add(tool);
            }

            foreach (XmlElement xPlug in xOptions.SelectNodes("Plugin"))
            {
                string name = xPlug.GetAttribute("name");
                _log.LogInformation("LoadProfile: adding plugin to load: {PluginName}", name);
                Options.PluginsToLoad.Add(name);
            }

            elem = (XmlElement)xOptions.SelectSingleNode("RootPath");
            if (elem != null)
            {
                Files.RootDir = elem.GetAttribute("path");
            }

            // Initialize all file paths from RootDir (no individual path overrides)
            Files.SetMulPath(Files.RootDir);

            foreach (XmlElement xTab in xOptions.SelectNodes("TabView"))
            {
                int viewTab = Convert.ToInt32(xTab.GetAttribute("tab"));
                Options.ChangedViewState[viewTab] = false;
            }

            elem = (XmlElement)xOptions.SelectSingleNode("ViewState");
            if (elem != null)
            {
                StoreFormState = bool.Parse(elem.GetAttribute("Active"));
                MaximisedForm = bool.Parse(elem.GetAttribute("Maximised"));
                FormPosition = new Point(int.Parse(elem.GetAttribute("PositionX")), int.Parse(elem.GetAttribute("PositionY")));
                FormSize = new Size(int.Parse(elem.GetAttribute("Width")), int.Parse(elem.GetAttribute("Height")));
            }

            elem = (XmlElement)xOptions.SelectSingleNode("TileDataDirectlySaveOnChange");
            Options.TileDataDirectlySaveOnChange = elem != null && (elem.GetAttribute("value") ?? "").Equals("true", StringComparison.OrdinalIgnoreCase);

            MapHelper.CheckForNewMapSize();

            _log.LogInformation("LoadProfile - done: {Filename}", filename);
        }
    }
}
