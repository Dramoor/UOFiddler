using System;
using System.IO;
using Microsoft.Extensions.Logging;
using Ultima;
using UoFiddler.Controls.Classes;

namespace UoFiddler.Controls.Helpers
{
    /// <summary>
    /// Helper class to convert MUL files back to UOP format after saving.
    /// Used when the Art/Gumps system is loading from UOP but save creates MUL files.
    /// </summary>
    public static class UopConversionHelper
    {
        private static readonly ILogger _logger = AppLog.For(typeof(UopConversionHelper));

        /// <summary>
        /// Converts saved art MUL/IDX files to UOP format if the system is currently using UOP.
        /// </summary>
        /// <param name="outputPath">Path where art.mul and artidx.mul were saved</param>
        /// <returns>True if conversion was successful or skipped, false if it failed</returns>
        public static bool ConvertArtToUopIfNeeded(string outputPath)
        {
            _logger.LogInformation("Converting art to UOP format if needed for output path: {OutputPath}", outputPath);

            try
            {
                // Only convert if we're currently using UOP format
                bool isUsingUop = Art.IsUsingUopLegacy();

                if (!isUsingUop)
                {
                    return true;
                }

                string mulFile = Path.Combine(outputPath, "art.mul");
                string idxFile = Path.Combine(outputPath, "artidx.mul");
                string uopFile = Path.Combine(outputPath, "artLegacyMUL.uop");

                // Verify the MUL files exist before attempting conversion
                if (!File.Exists(mulFile))
                {
                    _logger.LogError("MUL file not found during UOP conversion: {Path}", mulFile);
                    return false;
                }

                if (!File.Exists(idxFile))
                {
                    _logger.LogError("IDX file not found during UOP conversion: {Path}", idxFile);
                    return false;
                }

                _logger.LogInformation(
                    "Converting saved art MUL/IDX to UOP format: {Mul} + {Idx} -> {Uop}",
                    mulFile, idxFile, uopFile);

                // Load the converter through reflection (avoids direct plugin dependency)
                bool conversionSuccess = TryConvertToUop(mulFile, idxFile, uopFile);

                if (conversionSuccess)
                {
                    _logger.LogInformation("Successfully converted art MUL/IDX to UOP: {Uop}", uopFile);
                    return true;
                }
                else
                {
                    _logger.LogError("UOP conversion failed");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    "Error converting art MUL/IDX to UOP: {Error}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Converts saved gumps MUL/IDX files to UOP format if the system is currently using UOP.
        /// </summary>
        /// <param name="outputPath">Path where gumps.mul and gumpsidx.mul were saved</param>
        /// <returns>True if conversion was successful or skipped, false if it failed</returns>
        public static bool ConvertGumpsToUopIfNeeded(string outputPath)
        {
            _logger.LogInformation("Converting gumps to UOP format if needed for output path: {OutputPath}", outputPath);

            try
            {
                // Only convert if we're currently using UOP format
                bool isUsingUop = Gumps.IsUsingUopLegacy();
                _logger.LogInformation("Gumps.IsUsingUopLegacy() returned: {IsUsingUop}", isUsingUop);

                if (!isUsingUop)
                {
                    _logger.LogInformation("Gumps are not using UOP format, skipping conversion");
                    return true;
                }

                string mulFile = Path.Combine(outputPath, "Gumpart.mul");
                string idxFile = Path.Combine(outputPath, "Gumpidx.mul");
                string uopFile = Path.Combine(outputPath, "gumpartLegacyMUL.uop");

                _logger.LogInformation("Looking for MUL files: {Mul}, {Idx}", mulFile, idxFile);

                // Verify the MUL files exist before attempting conversion
                if (!File.Exists(mulFile))
                {
                    _logger.LogError("MUL file not found during UOP conversion: {Path}", mulFile);
                    return false;
                }

                if (!File.Exists(idxFile))
                {
                    _logger.LogError("IDX file not found during UOP conversion: {Path}", idxFile);
                    return false;
                }

                _logger.LogInformation(
                    "Converting saved gumps MUL/IDX to UOP format: {Mul} + {Idx} -> {Uop}",
                    mulFile, idxFile, uopFile);

                // Load the converter through reflection (avoids direct plugin dependency)
                bool conversionSuccess = TryConvertGumpsToUop(mulFile, idxFile, uopFile);

                if (conversionSuccess)
                {
                    // Verify the output file was actually created
                    if (File.Exists(uopFile))
                    {
                        var fileInfo = new FileInfo(uopFile);
                        _logger.LogInformation("Successfully converted gumps MUL/IDX to UOP: {Uop} (Size: {Size} bytes)", uopFile, fileInfo.Length);
                        return true;
                    }
                    else
                    {
                        _logger.LogError("Conversion reported success but UOP file was not created: {Uop}", uopFile);
                        return false;
                    }
                }
                else
                {
                    _logger.LogError("UOP conversion failed");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    "Error converting gumps MUL/IDX to UOP: {Error}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Attempts to convert MUL files to UOP using the LegacyMulFileConverter via reflection.
        /// Explicitly loads the plugin assembly if needed.
        /// </summary>
        private static bool TryConvertToUop(string mulFile, string idxFile, string uopFile)
        {
            try
            {
                // Try to load the plugin assembly explicitly
                System.Reflection.Assembly pluginAssembly = null;
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;

                // Try multiple possible plugin paths
                string[] possiblePaths = new[]
                {
                    Path.Combine(baseDir, "plugins", "UOPPacker.dll"),
                    Path.Combine(baseDir, "plugins", "UoFiddler.Plugin.UopPacker.dll"),
                    Path.Combine(baseDir, "UOPPacker.dll"),
                    Path.Combine(baseDir, "UoFiddler.Plugin.UopPacker.dll"),
                    Path.Combine(baseDir, "..", "plugins", "UOPPacker.dll"),
                    Path.Combine(baseDir, "..", "plugins", "UoFiddler.Plugin.UopPacker.dll"),
                    Path.Combine(baseDir, "..", "UoFiddler.Plugin.UopPacker", "bin", "Debug", "UOPPacker.dll"),
                    Path.Combine(baseDir, "..", "UoFiddler.Plugin.UopPacker", "bin", "Release", "UOPPacker.dll")
                };

                foreach (string pluginPath in possiblePaths)
                {
                    string fullPath = Path.GetFullPath(pluginPath);

                    if (File.Exists(fullPath))
                    {
                        try
                        {
                            pluginAssembly = System.Reflection.Assembly.LoadFrom(fullPath);
                            break;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning("Failed to load plugin assembly from {Path}: {Error}", fullPath, ex.Message);
                        }
                    }
                }

                // Try to get the converter type from either already-loaded or newly-loaded assembly
                Type converterType = null;
                if (pluginAssembly != null)
                {
                    converterType = pluginAssembly.GetType("UoFiddler.Plugin.UopPacker.Classes.LegacyMulFileConverter");
                }

                if (converterType == null)
                {
                    converterType = Type.GetType("UoFiddler.Plugin.UopPacker.Classes.LegacyMulFileConverter", false);
                }

                if (converterType == null)
                {
                    _logger.LogWarning(
                        "UOP Packer plugin not found - cannot convert to UOP format. " +
                        "Art was saved as MUL/IDX files only.");
                    return true; // Not fatal - MUL files are saved
                }

                var fileTypeEnum = pluginAssembly?.GetType("UoFiddler.Plugin.UopPacker.Classes.FileType") 
                    ?? Type.GetType("UoFiddler.Plugin.UopPacker.Classes.FileType", false);

                if (fileTypeEnum == null)
                {
                    _logger.LogWarning("FileType enum not found in UOP Packer plugin.");
                    return true; // Not fatal
                }

                // CompressionFlag is a nested enum in Ultima.FileIndex
                var ultimaAssembly = typeof(Art).Assembly; // Art is in Ultima namespace
                var fileIndexType = ultimaAssembly.GetType("Ultima.FileIndex", false);
                var compressionFlagEnum = fileIndexType?.GetNestedType("CompressionFlag");

                if (compressionFlagEnum == null)
                {
                    // Fallback: use integer 0 directly (CompressionFlag.None = 0)
                    compressionFlagEnum = typeof(int);
                }

                var toUopMethod = converterType.GetMethod("ToUop",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

                if (toUopMethod == null)
                {
                    _logger.LogWarning("ToUop method not found in LegacyMulFileConverter.");
                    return true; // Not fatal
                }

                // Parse enum values: FileType.ArtLegacyMul and CompressionFlag.None
                object fileTypeArtLegacy = Enum.Parse(fileTypeEnum, "ArtLegacyMul");
                object compressionNone;

                // If we have the real enum type, parse it; otherwise use integer 0
                if (compressionFlagEnum == typeof(int))
                {
                    compressionNone = 0; // CompressionFlag.None = 0
                }
                else
                {
                    compressionNone = Enum.Parse(compressionFlagEnum, "None");
                }

                _logger.LogInformation("Invoking LegacyMulFileConverter.ToUop for Art with FileType={FileType}", "ArtLegacyMul");
                _logger.LogInformation("Input: mul={Mul}, idx={Idx}", mulFile, idxFile);
                _logger.LogInformation("Output: uop={Uop}", uopFile);

                // Invoke: ToUop(mulFile, idxFile, uopFile, fileType, typeIndex, compression, housing, progress, components)
                try
                {
                    var result = toUopMethod.Invoke(null, new object[]
                    {
                        mulFile,           // inFile
                        idxFile,           // inFileIdx
                        uopFile,           // outFile
                        fileTypeArtLegacy, // type
                        0,                 // typeIndex
                        compressionNone,   // compressionFlag
                        "",                // housingBinFile (default)
                        null,              // progress (default)
                        ""                 // componentsFile (default)
                    });
                    _logger.LogInformation("ToUop invoke completed successfully for Art");
                }
                catch (System.Reflection.TargetInvocationException tex)
                {
                    _logger.LogError("ToUop method threw exception: {Error}", tex.InnerException?.ToString());
                    throw;
                }

                return true;
            }
            catch (System.Reflection.TargetInvocationException ex)
            {
                _logger.LogError(
                    "ToUop method threw exception: {Error} - {Message}",
                    ex.InnerException?.GetType().Name, ex.InnerException?.Message);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    "Reflection-based UOP conversion failed: {Error}", ex.Message);
                return false;
            }
        }

        private static bool TryConvertGumpsToUop(string mulFile, string idxFile, string uopFile)
        {
            try
            {
                _logger.LogInformation("Starting Gumps UOP conversion");
                _logger.LogInformation("Input files - MUL: {Mul}, IDX: {Idx}", mulFile, idxFile);
                _logger.LogInformation("Output file - UOP: {Uop}", uopFile);

                System.Reflection.Assembly pluginAssembly = null;
                Type converterType = null;

                // First, try to find the converter type directly (it may already be loaded in AppDomain)
                converterType = Type.GetType("UoFiddler.Plugin.UopPacker.Classes.LegacyMulFileConverter", false);
                _logger.LogInformation("Direct Type.GetType result: {ConverterType}", converterType?.FullName ?? "NOT FOUND");

                // If not found, check already-loaded assemblies in AppDomain
                if (converterType == null)
                {
                    var appDomainAssemblies = AppDomain.CurrentDomain.GetAssemblies();
                    _logger.LogInformation("Searching {AssemblyCount} assemblies in AppDomain", appDomainAssemblies.Length);

                    foreach (var assembly in appDomainAssemblies)
                    {
                        _logger.LogInformation("Checking assembly: {AssemblyName}", assembly.FullName);
                        if (assembly.FullName.Contains("UopPacker") || assembly.FullName.Contains("UOPPacker"))
                        {
                            _logger.LogInformation("Found UOP Packer assembly: {AssemblyName}", assembly.FullName);
                            converterType = assembly.GetType("UoFiddler.Plugin.UopPacker.Classes.LegacyMulFileConverter");
                            if (converterType != null)
                            {
                                _logger.LogInformation("Found LegacyMulFileConverter in AppDomain assembly");
                                pluginAssembly = assembly;
                                break;
                            }
                        }
                    }
                }

                // Try to load the plugin assembly from disk as last resort
                if (converterType == null)
                {
                    string baseDir = AppDomain.CurrentDomain.BaseDirectory;

                    _logger.LogInformation("Base directory for plugin search: {BaseDir}", baseDir);

                    // Try multiple possible plugin paths
                    string[] possiblePaths = new[]
                    {
                        Path.Combine(baseDir, "plugins", "UOPPacker.dll"),
                        Path.Combine(baseDir, "plugins", "UoFiddler.Plugin.UopPacker.dll"),
                        Path.Combine(baseDir, "UOPPacker.dll"),
                        Path.Combine(baseDir, "UoFiddler.Plugin.UopPacker.dll"),
                        Path.Combine(baseDir, "..", "plugins", "UOPPacker.dll"),
                        Path.Combine(baseDir, "..", "plugins", "UoFiddler.Plugin.UopPacker.dll"),
                        Path.Combine(baseDir, "..", "UoFiddler.Plugin.UopPacker", "bin", "Debug", "UOPPacker.dll"),
                        Path.Combine(baseDir, "..", "UoFiddler.Plugin.UopPacker", "bin", "Release", "UOPPacker.dll")
                    };

                    foreach (string pluginPath in possiblePaths)
                    {
                        string fullPath = Path.GetFullPath(pluginPath);
                        _logger.LogInformation("Checking for plugin at: {FullPath}", fullPath);

                        if (File.Exists(fullPath))
                        {
                            try
                            {
                                _logger.LogInformation("Found plugin assembly, loading: {FullPath}", fullPath);
                                pluginAssembly = System.Reflection.Assembly.LoadFrom(fullPath);
                                converterType = pluginAssembly.GetType("UoFiddler.Plugin.UopPacker.Classes.LegacyMulFileConverter");
                                _logger.LogInformation("Successfully loaded plugin assembly from: {FullPath}", fullPath);
                                break;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning("Failed to load plugin assembly from {Path}: {Error}", fullPath, ex.Message);
                            }
                        }
                    }
                }

                if (converterType == null)
                {
                    _logger.LogWarning(
                        "UOP Packer plugin not found - cannot convert to UOP format. " +
                        "Gumps were saved as MUL/IDX files only.");
                    return true; // Not fatal - MUL files are saved
                }

                var fileTypeEnum = pluginAssembly?.GetType("UoFiddler.Plugin.UopPacker.Classes.FileType") 
                    ?? Type.GetType("UoFiddler.Plugin.UopPacker.Classes.FileType", false);

                if (fileTypeEnum == null)
                {
                    _logger.LogWarning("FileType enum not found in UOP Packer plugin.");
                    return true; // Not fatal
                }

                // CompressionFlag is a nested enum in Ultima.FileIndex
                var ultimaAssembly = typeof(Gumps).Assembly; // Gumps is in Ultima namespace
                var fileIndexType = ultimaAssembly.GetType("Ultima.FileIndex", false);
                var compressionFlagEnum = fileIndexType?.GetNestedType("CompressionFlag");

                if (compressionFlagEnum == null)
                {
                    // Fallback: use integer 0 directly (CompressionFlag.None = 0)
                    compressionFlagEnum = typeof(int);
                    _logger.LogInformation("Using integer 0 for CompressionFlag.None fallback");
                }

                var toUopMethod = converterType.GetMethod("ToUop",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

                if (toUopMethod == null)
                {
                    _logger.LogWarning("ToUop method not found in LegacyMulFileConverter.");
                    return true; // Not fatal
                }

                // Parse enum values: FileType.GumpartLegacyMul and CompressionFlag.None
                object fileTypeGumpsLegacy = Enum.Parse(fileTypeEnum, "GumpartLegacyMul");
                object compressionNone;

                // If we have the real enum type, parse it; otherwise use integer 0
                if (compressionFlagEnum == typeof(int))
                {
                    compressionNone = 0; // CompressionFlag.None = 0
                }
                else
                {
                    compressionNone = Enum.Parse(compressionFlagEnum, "None");
                }

                _logger.LogInformation("Invoking LegacyMulFileConverter.ToUop for Gumps with FileType={FileType}", "GumpartLegacyMul");
                _logger.LogInformation("Input: mul={Mul}, idx={Idx}", mulFile, idxFile);
                _logger.LogInformation("Output: uop={Uop}", uopFile);

                // Invoke: ToUop(mulFile, idxFile, uopFile, fileType, typeIndex, compression, housing, progress, components)
                try
                {
                    var result = toUopMethod.Invoke(null, new object[]
                    {
                        mulFile,             // inFile
                        idxFile,             // inFileIdx
                        uopFile,             // outFile
                        fileTypeGumpsLegacy, // type
                        0,                   // typeIndex
                        compressionNone,     // compressionFlag
                        "",                  // housingBinFile (default)
                        null,                // progress (default)
                        ""                   // componentsFile (default)
                    });
                    _logger.LogInformation("ToUop invoke completed successfully for Gumps");
                }
                catch (System.Reflection.TargetInvocationException tex)
                {
                    _logger.LogError("ToUop method threw exception: {Error}", tex.InnerException?.ToString());
                    throw;
                }

                return true;
            }
            catch (System.Reflection.TargetInvocationException ex)
            {
                _logger.LogError(
                    "ToUop method threw exception: {Error} - {Message}",
                    ex.InnerException?.GetType().Name, ex.InnerException?.Message);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    "Reflection-based UOP conversion failed: {Error}", ex.Message);
                return false;
            }
        }
    }
}
