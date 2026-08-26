using System;
using System.IO;
using System.Text;

namespace UoFiddler.Controls.Classes
{
    /// <summary>
    /// Generates RunUO item scripts based on UO item data
    /// </summary>
    public static class RunUOScriptGenerator
    {
        /// <summary>
        /// Generates a RunUO item script and saves it to the specified directory
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
        /// <returns>The full path to the generated script file, or null if generation failed</returns>
        public static string GenerateItemScript(int itemId, string itemName, string outputDirectory, int itemWeight = 1, bool useHue = false, int previewHue = 0, bool isStackable = false, string prefix = "None", string lootType = "Regular", int flippableId = 0)
        {
            if (string.IsNullOrWhiteSpace(itemName) || string.IsNullOrWhiteSpace(outputDirectory))
            {
                return null;
            }

            try
            {
                // Create class name by removing spaces and applying title case
                string className = ApplyTitleCase(itemName);

                // Create filename by using the class name
                string fileName = $"{className}.cs";
                string filePath = Path.Combine(outputDirectory, fileName);

                // Generate the script content
                string scriptContent = GenerateScriptContent(className, itemId, itemName, itemWeight, useHue, previewHue, isStackable, prefix, lootType, flippableId);

                // Write the file
                File.WriteAllText(filePath, scriptContent, Encoding.UTF8);

                return filePath;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error generating RunUO script: {ex.Message}");
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
        /// Generates the C# script content for a RunUO item
        /// </summary>
        private static string GenerateScriptContent(string className, int itemId, string itemName, int itemWeight, bool useHue, int previewHue, bool isStackable, string prefix = "None", string lootType = "Regular", int flippableId = 0)
        {
            string hex = $"0x{itemId:X}";

            // Apply prefix to the item name for display
            string displayName = itemName;
            if (!string.IsNullOrWhiteSpace(prefix) && prefix != "None")
            {
                displayName = $"{prefix.ToLower()} {itemName}".ToLower();
            }

            // Build the primary constructor (default constructor)
            StringBuilder constructor1Body = new StringBuilder();
            if (!isStackable)
            {
                constructor1Body.AppendLine($"            Name = \"{displayName}\";");
                if (useHue)
                {
                    constructor1Body.AppendLine($"            Hue = {previewHue + 1};");
                }
                constructor1Body.AppendLine($"            Weight = {itemWeight};");
                if (lootType != "Regular")
                {
                    constructor1Body.AppendLine($"            LootType = LootType.{lootType};");
                }
            }

            string secondaryConstructor = "";
            if (isStackable)
            {
                // Build the secondary constructor (with amount parameter)
                StringBuilder constructor2Body = new StringBuilder();
                constructor2Body.AppendLine($"            Stackable = true;");
                constructor2Body.AppendLine($"            Amount = amt;");
                constructor2Body.AppendLine($"            Name = \"{displayName}\";");
                if (useHue)
                {
                    constructor2Body.AppendLine($"            Hue = {previewHue + 1};");
                }
                constructor2Body.AppendLine($"            Weight = {itemWeight};");
                if (lootType != "Regular")
                {
                    constructor2Body.AppendLine($"            LootType = LootType.{lootType};");
                }

                // For stackable items, create a default constructor that calls this(1) and the amount constructor
                secondaryConstructor = $@"
        [Constructable]
        public {className}() : this(1)
        {{
        }}

        [Constructable]
        public {className}(int amt) : base({hex})
        {{
{constructor2Body.ToString().TrimEnd()}
        }}
";
            }

            // Build the serial constructor
            string serialConstructor = $@"        public {className}(Serial serial) : base(serial)
        {{
        }}";

            // Build default constructor only if NOT stackable
            string defaultConstructor = "";
            if (!isStackable)
            {
                defaultConstructor = $@"        [Constructable]
        public {className}() : base({hex})
        {{
{constructor1Body.ToString().TrimEnd()}
        }}";
            }

            // Build the flippable attribute if applicable
            string flippableAttribute = "";
            if (flippableId > 0)
            {
                string flippableHex = $"0x{flippableId:X}";
                flippableAttribute = $"[FlipableAttribute({hex}, {flippableHex})]\n";
            }

            return $@"using System;
using Server;
using Server.Items;

namespace Server.Items
{{
    {flippableAttribute}    public class {className} : Item
    {{
{defaultConstructor}{secondaryConstructor}

{serialConstructor}

        public override void Serialize(GenericWriter writer)
        {{
            base.Serialize(writer);
            writer.Write((int)0); // Version tracker
        }}

        public override void Deserialize(GenericReader reader)
        {{
            base.Deserialize(reader);
            int version = reader.ReadInt();
        }}
    }}
}}
";
        }
    }
}
