using System;
using System.Windows.Forms;

namespace UoFiddler.Controls.Forms
{
    public partial class ItemNameInputForm : Form
    {
        private TextBox itemNameTextBox;
        private TextBox weightTextBox;
        private TextBox flippableTextBox;
        private TextBox scriptNameTextBox;
        private CheckBox useHueCheckBox;
        private CheckBox isStackableCheckBox;
        private CheckBox isArtifactCheckBox;
        private CheckBox isReadOnlyCheckBox;
        private ComboBox prefixComboBox;
        private ComboBox lootTypeComboBox;
        private Button okButton;
        private Button cancelButton;
        private Label instructionLabel;
        private Label weightLabel;
        private Label stackableLabel;
        private Label artifactLabel;
        private Label prefixLabel;
        private Label lootTypeLabel;
        private Label flippableLabel;
        private Label hueLabel;
        private Label readonlyLabel;
        private Label scriptnameLabel;

        public string ScriptName { get; private set; }
        public string ItemName { get; private set; }
        public int ItemWeight { get; private set; }
        public bool UseHue { get; private set; }
        public bool IsStackable { get; private set; }
        public bool IsReadOnly { get; private set; }
        public bool IsArtifact { get; private set; }
        public string SelectedPrefix { get; private set; }
        public string SelectedLootType { get; private set; }
        public int FlippableId { get; private set; }

        public ItemNameInputForm(bool isItemStackable = false, bool isRunUO = false, int previewHue = -1)
        {
            InitializeComponent();
            ScriptName = string.Empty;
            ItemName = string.Empty;
            ItemWeight = 1;
            UseHue = false;
            IsStackable = false;
            IsArtifact = false;
            IsReadOnly = false;
            SelectedPrefix = "None";
            SelectedLootType = "Regular";
            FlippableId = 0;

            // Only show stackable checkbox if the item can be stackable
            if (isItemStackable)
            {
                isStackableCheckBox.Visible = true;
            }
            else
            {
                stackableLabel.Enabled = false;
                isStackableCheckBox.Visible = false;
            }

            // Hide artifact checkbox for RunUO
            if (isRunUO)
            {
                artifactLabel.Enabled = false;
                isArtifactCheckBox.Visible = false;
            }

            // Only enable hue checkbox if preview hue is actually set
            if (previewHue < 0)
            {
                hueLabel.Enabled = false;
                useHueCheckBox.Enabled = false;
                useHueCheckBox.Checked = false;
                useHueCheckBox.Visible = false;
            }
            else
            {
                useHueCheckBox.Enabled = true;
            }
        }

        private void InitializeComponent()
        {
            scriptNameTextBox = new TextBox();
            itemNameTextBox = new TextBox();
            weightTextBox = new TextBox();
            flippableTextBox = new TextBox();
            useHueCheckBox = new CheckBox();
            isStackableCheckBox = new CheckBox();
            isArtifactCheckBox = new CheckBox();
            isReadOnlyCheckBox = new CheckBox();
            prefixComboBox = new ComboBox();
            lootTypeComboBox = new ComboBox();
            okButton = new Button();
            cancelButton = new Button();
            instructionLabel = new Label();
            weightLabel = new Label();
            hueLabel = new Label();
            stackableLabel = new Label();
            artifactLabel = new Label();
            prefixLabel = new Label();
            lootTypeLabel = new Label();
            flippableLabel = new Label();
            readonlyLabel = new Label();
            scriptnameLabel = new Label();
            SuspendLayout();
            // 
            // scriptNameTextBox
            // 
            scriptNameTextBox.Location = new System.Drawing.Point(87, 6);
            scriptNameTextBox.Margin = new Padding(4, 3, 4, 3);
            scriptNameTextBox.Name = "scriptNameTextBox";
            scriptNameTextBox.Size = new System.Drawing.Size(276, 23);
            scriptNameTextBox.TabIndex = 1;
            scriptNameTextBox.TextChanged += scriptNameTextBox_TextChanged;
            // 
            // itemNameTextBox
            // 
            itemNameTextBox.Location = new System.Drawing.Point(87, 40);
            itemNameTextBox.Margin = new Padding(4, 3, 4, 3);
            itemNameTextBox.Name = "itemNameTextBox";
            itemNameTextBox.Size = new System.Drawing.Size(276, 23);
            itemNameTextBox.TabIndex = 3;
            itemNameTextBox.TextChanged += itemNameTextBox_TextChanged;
            // 
            // weightTextBox
            // 
            weightTextBox.Location = new System.Drawing.Point(60, 144);
            weightTextBox.Margin = new Padding(4, 3, 4, 3);
            weightTextBox.Name = "weightTextBox";
            weightTextBox.Size = new System.Drawing.Size(69, 23);
            weightTextBox.TabIndex = 15;
            weightTextBox.Text = "1";
            // 
            // flippableTextBox
            // 
            flippableTextBox.Location = new System.Drawing.Point(236, 75);
            flippableTextBox.Margin = new Padding(4, 3, 4, 3);
            flippableTextBox.Name = "flippableTextBox";
            flippableTextBox.Size = new System.Drawing.Size(139, 23);
            flippableTextBox.TabIndex = 9;
            // 
            // useHueCheckBox
            // 
            useHueCheckBox.AutoSize = true;
            useHueCheckBox.Location = new System.Drawing.Point(360, 116);
            useHueCheckBox.Margin = new Padding(4, 3, 4, 3);
            useHueCheckBox.Name = "useHueCheckBox";
            useHueCheckBox.Size = new System.Drawing.Size(15, 14);
            useHueCheckBox.TabIndex = 13;
            useHueCheckBox.UseVisualStyleBackColor = true;
            // 
            // isStackableCheckBox
            // 
            isStackableCheckBox.AutoSize = true;
            isStackableCheckBox.Location = new System.Drawing.Point(72, 114);
            isStackableCheckBox.Margin = new Padding(4, 3, 4, 3);
            isStackableCheckBox.Name = "isStackableCheckBox";
            isStackableCheckBox.Size = new System.Drawing.Size(15, 14);
            isStackableCheckBox.TabIndex = 11;
            isStackableCheckBox.UseVisualStyleBackColor = true;
            isStackableCheckBox.Visible = false;
            // 
            // isArtifactCheckBox
            // 
            isArtifactCheckBox.AutoSize = true;
            isArtifactCheckBox.Location = new System.Drawing.Point(195, 115);
            isArtifactCheckBox.Margin = new Padding(4, 3, 4, 3);
            isArtifactCheckBox.Name = "isArtifactCheckBox";
            isArtifactCheckBox.Size = new System.Drawing.Size(15, 14);
            isArtifactCheckBox.TabIndex = 9;
            isArtifactCheckBox.UseVisualStyleBackColor = true;
            // 
            // isReadOnlyCheckBox
            // 
            isReadOnlyCheckBox.AutoSize = true;
            isReadOnlyCheckBox.Location = new System.Drawing.Point(434, 44);
            isReadOnlyCheckBox.Margin = new Padding(4, 3, 4, 3);
            isReadOnlyCheckBox.Name = "isReadOnlyCheckBox";
            isReadOnlyCheckBox.Size = new System.Drawing.Size(15, 14);
            isReadOnlyCheckBox.TabIndex = 5;
            isReadOnlyCheckBox.UseVisualStyleBackColor = true;
            // 
            // prefixComboBox
            // 
            prefixComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            prefixComboBox.FormattingEnabled = true;
            prefixComboBox.Items.AddRange(new object[] { "None", "A", "An", "The" });
            prefixComboBox.Location = new System.Drawing.Point(51, 75);
            prefixComboBox.Margin = new Padding(4, 3, 4, 3);
            prefixComboBox.SelectedIndex = 0;
            prefixComboBox.Name = "prefixComboBox";
            prefixComboBox.Size = new System.Drawing.Size(75, 23);
            prefixComboBox.TabIndex = 7;
            prefixComboBox.SelectedIndexChanged += prefixComboBox_SelectedIndexChanged;
            // 
            // lootTypeComboBox
            // 
            lootTypeComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            lootTypeComboBox.FormattingEnabled = true;
            lootTypeComboBox.Items.AddRange(new object[] { "Regular", "Newbied", "Blessed", "Cursed" });
            lootTypeComboBox.Location = new System.Drawing.Point(265, 144);
            lootTypeComboBox.Margin = new Padding(4, 3, 4, 3);
            lootTypeComboBox.Name = "lootTypeComboBox";
            lootTypeComboBox.SelectedIndex = 0; // Default to "Regular"
            lootTypeComboBox.Size = new System.Drawing.Size(116, 23);
            lootTypeComboBox.TabIndex = 17;
            // 
            // okButton
            // 
            okButton.DialogResult = DialogResult.OK;
            okButton.Location = new System.Drawing.Point(13, 210);
            okButton.Margin = new Padding(4, 3, 4, 3);
            okButton.Name = "okButton";
            okButton.Size = new System.Drawing.Size(88, 27);
            okButton.TabIndex = 18;
            okButton.Text = "OK";
            okButton.UseVisualStyleBackColor = true;
            okButton.Click += okButton_Click;
            // 
            // cancelButton
            // 
            cancelButton.DialogResult = DialogResult.Cancel;
            cancelButton.Location = new System.Drawing.Point(361, 210);
            cancelButton.Margin = new Padding(4, 3, 4, 3);
            cancelButton.Name = "cancelButton";
            cancelButton.Size = new System.Drawing.Size(88, 27);
            cancelButton.TabIndex = 19;
            cancelButton.Text = "Cancel";
            cancelButton.UseVisualStyleBackColor = true;
            // 
            // instructionLabel
            // 
            instructionLabel.AutoSize = true;
            instructionLabel.Location = new System.Drawing.Point(4, 43);
            instructionLabel.Margin = new Padding(4, 0, 4, 0);
            instructionLabel.Name = "instructionLabel";
            instructionLabel.Size = new System.Drawing.Size(69, 15);
            instructionLabel.TabIndex = 2;
            instructionLabel.Text = "Item Name:";
            // 
            // weightLabel
            // 
            weightLabel.AutoSize = true;
            weightLabel.Location = new System.Drawing.Point(4, 147);
            weightLabel.Margin = new Padding(4, 0, 4, 0);
            weightLabel.Name = "weightLabel";
            weightLabel.Size = new System.Drawing.Size(48, 15);
            weightLabel.TabIndex = 14;
            weightLabel.Text = "Weight:";
            // 
            // hueLabel
            // 
            hueLabel.AutoSize = true;
            hueLabel.Location = new System.Drawing.Point(254, 115);
            hueLabel.Margin = new Padding(4, 0, 4, 0);
            hueLabel.Name = "hueLabel";
            hueLabel.Size = new System.Drawing.Size(98, 15);
            hueLabel.TabIndex = 4;
            hueLabel.Text = "Use Preview Hue:";
            // 
            // stackableLabel
            // 
            stackableLabel.AutoSize = true;
            stackableLabel.Location = new System.Drawing.Point(4, 113);
            stackableLabel.Margin = new Padding(4, 0, 4, 0);
            stackableLabel.Name = "stackableLabel";
            stackableLabel.Size = new System.Drawing.Size(60, 15);
            stackableLabel.TabIndex = 10;
            stackableLabel.Text = "Stackable:";
            // 
            // artifactLabel
            // 
            artifactLabel.AutoSize = true;
            artifactLabel.Location = new System.Drawing.Point(127, 114);
            artifactLabel.Margin = new Padding(4, 0, 4, 0);
            artifactLabel.Name = "artifactLabel";
            artifactLabel.Size = new System.Drawing.Size(60, 15);
            artifactLabel.TabIndex = 12;
            artifactLabel.Text = "Is Artifact:";
            // 
            // prefixLabel
            // 
            prefixLabel.AutoSize = true;
            prefixLabel.Location = new System.Drawing.Point(4, 78);
            prefixLabel.Margin = new Padding(4, 0, 4, 0);
            prefixLabel.Name = "prefixLabel";
            prefixLabel.Size = new System.Drawing.Size(39, 15);
            prefixLabel.TabIndex = 6;
            prefixLabel.Text = "Prefix:";
            // 
            // lootTypeLabel
            // 
            lootTypeLabel.AutoSize = true;
            lootTypeLabel.Location = new System.Drawing.Point(195, 147);
            lootTypeLabel.Margin = new Padding(4, 0, 4, 0);
            lootTypeLabel.Name = "lootTypeLabel";
            lootTypeLabel.Size = new System.Drawing.Size(62, 15);
            lootTypeLabel.TabIndex = 16;
            lootTypeLabel.Text = "Loot Type:";
            // 
            // flippableLabel
            // 
            flippableLabel.AutoSize = true;
            flippableLabel.Location = new System.Drawing.Point(172, 78);
            flippableLabel.Margin = new Padding(4, 0, 4, 0);
            flippableLabel.Name = "flippableLabel";
            flippableLabel.Size = new System.Drawing.Size(58, 15);
            flippableLabel.TabIndex = 8;
            flippableLabel.Text = "Flippable:";
            flippableLabel.Click += flippableLabel_Click;
            // 
            // readonlyLabel
            // 
            readonlyLabel.AutoSize = true;
            readonlyLabel.Location = new System.Drawing.Point(368, 43);
            readonlyLabel.Margin = new Padding(4, 0, 4, 0);
            readonlyLabel.Name = "readonlyLabel";
            readonlyLabel.Size = new System.Drawing.Size(64, 15);
            readonlyLabel.TabIndex = 4;
            readonlyLabel.Text = "Read Only:";
            // 
            // scriptnameLabel
            // 
            scriptnameLabel.AutoSize = true;
            scriptnameLabel.Location = new System.Drawing.Point(4, 9);
            scriptnameLabel.Margin = new Padding(4, 0, 4, 0);
            scriptnameLabel.Name = "scriptnameLabel";
            scriptnameLabel.Size = new System.Drawing.Size(75, 15);
            scriptnameLabel.TabIndex = 0;
            scriptnameLabel.Text = "Script Name:";
            // 
            // ItemNameInputForm
            // 
            AcceptButton = okButton;
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            CancelButton = cancelButton;
            ClientSize = new System.Drawing.Size(462, 256);
            Controls.Add(cancelButton);
            Controls.Add(okButton);
            Controls.Add(flippableTextBox);
            Controls.Add(flippableLabel);
            Controls.Add(lootTypeComboBox);
            Controls.Add(lootTypeLabel);
            Controls.Add(prefixComboBox);
            Controls.Add(prefixLabel);
            Controls.Add(isArtifactCheckBox);
            Controls.Add(artifactLabel);
            Controls.Add(isStackableCheckBox);
            Controls.Add(stackableLabel);
            Controls.Add(useHueCheckBox);
            Controls.Add(hueLabel);
            Controls.Add(weightTextBox);
            Controls.Add(weightLabel);
            Controls.Add(itemNameTextBox);
            Controls.Add(instructionLabel);
            Controls.Add(readonlyLabel);
            Controls.Add(isReadOnlyCheckBox);
            Controls.Add(scriptNameTextBox);
            Controls.Add(scriptnameLabel);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            Margin = new Padding(4, 3, 4, 3);
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "ItemNameInputForm";
            ShowIcon = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = "Create ServUO Item Script";
            ResumeLayout(false);
            PerformLayout();
        }

        private void okButton_Click(object sender, EventArgs e)
        {
            ScriptName = scriptNameTextBox.Text;
            ItemName = itemNameTextBox.Text;
            IsReadOnly = isReadOnlyCheckBox.Checked;
            UseHue = useHueCheckBox.Checked;
            IsStackable = isStackableCheckBox.Checked;
            IsArtifact = isArtifactCheckBox.Checked;
            SelectedPrefix = prefixComboBox.SelectedItem?.ToString() ?? "None";
            SelectedLootType = lootTypeComboBox.SelectedItem?.ToString() ?? "Regular";

            // Parse weight, default to 1 if invalid
            if (!int.TryParse(weightTextBox.Text, out int weight) || weight < 0)
            {
                weight = 1;
            }
            ItemWeight = weight;

            // Parse flippable ID - supports both hex (0x####) and decimal formats
            FlippableId = 0;
            string flippableInput = flippableTextBox.Text.Trim();
            if (!string.IsNullOrEmpty(flippableInput))
            {
                if (flippableInput.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                {
                    // Hex format
                    if (int.TryParse(flippableInput.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out int hexValue))
                    {
                        FlippableId = hexValue;
                    }
                }
                else
                {
                    // Decimal format
                    if (int.TryParse(flippableInput, out int decimalValue))
                    {
                        FlippableId = decimalValue;
                    }
                }
            }

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void flippableLabel_Click(object sender, EventArgs e)
        {

        }

        private void prefixComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void itemNameTextBox_TextChanged(object sender, EventArgs e)
        {

        }

        private void scriptNameTextBox_TextChanged(object sender, EventArgs e)
        {

        }
    }
}

