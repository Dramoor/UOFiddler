using System;
using System.IO;
using System.Text;

namespace UoFiddler.Controls.Classes
{
    /// <summary>
    /// Generates ModernUO item scripts based on UO item data
    /// </summary>
    public static class ModernUOScriptGenerator
    {
        /// <summary>
        /// Generates a ModernUO item script and saves it to the specified directory
        /// </summary>
        /// <param name="itemId">The hexadecimal item ID</param>
        /// <param name="itemName">The display name of the item (with spaces)</param>
        /// <param name="outputDirectory">Directory where the script file will be saved</param>
        /// <param name="itemWeight">The weight of the item</param>
        /// <param name="useHue">Whether to use the preview hue</param>
        /// <param name="previewHue">The preview hue value (if useHue is true)</param>
        /// <param name="isStackable">Whether the item is stackable</param>
        /// <param name="prefix">The prefix to add to the item name (None, A, An, The)</param>
        /// <param name="lootType">The loot type (Regular, Newbied, Blessed, Cursed)</param>
        /// <param name="flippableId">The flippable graphic ID (0 if not flippable)</param>
        /// <param name="scriptName">The class name without spaces (used for filename and class definition)</param>
        /// <param name="isReadOnly">Whether to use DefaultName property instead of Name assignment</param>
        /// <returns>The full path to the generated script file, or null if generation failed</returns>
        public static string GenerateItemScript(int itemId, string itemName, string outputDirectory, int itemWeight = 1, bool useHue = false, int previewHue = 0, bool isStackable = false, string prefix = "None", string lootType = "Regular", int flippableId = 0, string scriptName = null, bool isReadOnly = false)
        {
            if (string.IsNullOrWhiteSpace(itemName) || string.IsNullOrWhiteSpace(outputDirectory))
            {
                return null;
            }

            try
            {
                // Create class name using scriptName if provided, otherwise derive from itemName
                string className = !string.IsNullOrWhiteSpace(scriptName) ? scriptName : ApplyTitleCase(itemName);

                // Create filename by using the class name
                string fileName = $"{className}.cs";
                string filePath = Path.Combine(outputDirectory, fileName);

                // Generate the script content
                string scriptContent = GenerateScriptContent(className, itemId, itemName, itemWeight, useHue, previewHue, isStackable, prefix, lootType, flippableId, isReadOnly);

                // Write the file
                File.WriteAllText(filePath, scriptContent, Encoding.UTF8);

                return filePath;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error generating ModernUO script: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Applies title case to a string while removing spaces.
        /// </summary>
        private static string ApplyTitleCase(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return input;
            }

            StringBuilder result = new StringBuilder();
            bool capitalizeNext = true;

            foreach (char c in input)
            {
                if (c == ' ')
                {
                    // Skip spaces but mark the next character to be capitalized
                    capitalizeNext = true;
                }
                else if (capitalizeNext)
                {
                    result.Append(char.ToUpper(c));
                    capitalizeNext = false;
                }
                else
                {
                    result.Append(c);
                }
            }

            return result.ToString();
        }

        /// <summary>
        /// Generates the C# script content for a ModernUO item
        /// </summary>
        private static string GenerateScriptContent(string className, int itemId, string itemName, int itemWeight, bool useHue, int previewHue, bool isStackable, string prefix = "None", string lootType = "Regular", int flippableId = 0, bool isReadOnly = false)
        {
            string hex = $"0x{itemId:X}";

            // Apply prefix to the item name for display
            string displayName = itemName;
            if (!string.IsNullOrWhiteSpace(prefix) && prefix != "None")
            {
                displayName = $"{prefix.ToLower()} {itemName}".ToLower();
            }

            // Build DefaultName property if isReadOnly is true
            string defaultNameProperty = "";
            if (isReadOnly)
            {
                defaultNameProperty = $"    public override string DefaultName => \"{displayName}\";\n\n";
            }

            // Build the constructor body
            StringBuilder constructorBody = new StringBuilder();
            if (isStackable)
            {
                constructorBody.AppendLine($"        Stackable = true;");
                constructorBody.AppendLine($"        Amount = amount;");
            }
            if (!isReadOnly)
            {
                constructorBody.AppendLine($"        Name = \"{displayName}\";");
            }
            if (useHue)
            {
                constructorBody.AppendLine($"        Hue = {previewHue + 1};");
            }
            constructorBody.AppendLine($"        Weight = {itemWeight};");
            if (lootType != "Regular")
            {
                constructorBody.AppendLine($"        LootType = LootType.{lootType};");
            }

            // Build the constructor signature
            string constructorSignature = isStackable
                ? $"public {className}(int amount = 1) : base({hex})"
                : $"public {className}() : base({hex})";

            // Build the flippable attribute if applicable
            string flippableAttribute = "";
            if (flippableId > 0)
            {
                string flippableHex = $"0x{flippableId:X}";
                flippableAttribute = $"[Flippable({hex}, {flippableHex})]\n";
            }

            return $@"using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
{flippableAttribute}    public partial class {className} : Item
{{
{defaultNameProperty}    [Constructible]
    {constructorSignature}
    {{
{constructorBody.ToString().TrimEnd()}
    }}
}}
";
        }
    }
}
