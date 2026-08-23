using System;
using System.IO;
using Microsoft.Extensions.Logging;
using Ultima;
using UoFiddler.Controls.Classes;

namespace UoFiddler.Controls.Helpers
{
    /// <summary>
    /// Helper class to convert MUL files back to UOP format after saving.
    /// Used when the Art system is loading from UOP but save creates MUL files.
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

                _logger.LogInformation("Invoking LegacyMulFileConverter.ToUop");

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
                        "",                // housingBinFile
                        null,              // progress
                        ""                 // componentsFile
                    });
                    _logger.LogInformation("ToUop invoke completed successfully");
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
