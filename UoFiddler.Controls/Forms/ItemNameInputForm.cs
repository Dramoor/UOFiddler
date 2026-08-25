using System;
using System.Windows.Forms;

namespace UoFiddler.Controls.Forms
{
    public partial class ItemNameInputForm : Form
    {
        private TextBox itemNameTextBox;
        private TextBox weightTextBox;
        private CheckBox useHueCheckBox;
        private CheckBox isStackableCheckBox;
        private CheckBox isArtifactCheckBox;
        private ComboBox prefixComboBox;
        private Button okButton;
        private Button cancelButton;
        private Label instructionLabel;
        private Label weightLabel;
        private Label hueLabel;

        public string ItemName { get; private set; }
        public int ItemWeight { get; private set; }
        public bool UseHue { get; private set; }
        public bool IsStackable { get; private set; }
        public bool IsArtifact { get; private set; }
        public string SelectedPrefix { get; private set; }

        public ItemNameInputForm(bool isItemStackable = false, bool isRunUO = false, int previewHue = -1)
        {
            InitializeComponent();
            ItemName = string.Empty;
            ItemWeight = 1;
            UseHue = false;
            IsStackable = false;
            IsArtifact = false;
            SelectedPrefix = "None";

            // Only show stackable checkbox if the item can be stackable
            if (isItemStackable)
            {
                isStackableCheckBox.Visible = true;
            }
            else
            {
                isStackableCheckBox.Visible = false;
            }

            // Hide artifact checkbox for RunUO
            if (isRunUO)
            {
                isArtifactCheckBox.Visible = false;
            }

            // Only enable hue checkbox if preview hue is actually set
            if (previewHue < 0)
            {
                useHueCheckBox.Enabled = false;
                useHueCheckBox.Checked = false;
            }
            else
            {
                useHueCheckBox.Enabled = true;
            }
        }

        private void InitializeComponent()
        {
            this.itemNameTextBox = new TextBox();
            this.weightTextBox = new TextBox();
            this.useHueCheckBox = new CheckBox();
            this.isStackableCheckBox = new CheckBox();
            this.isArtifactCheckBox = new CheckBox();
            this.prefixComboBox = new ComboBox();
            this.okButton = new Button();
            this.cancelButton = new Button();
            this.instructionLabel = new Label();
            this.weightLabel = new Label();
            this.hueLabel = new Label();
            Label stackableLabel = new Label();
            Label artifactLabel = new Label();
            Label prefixLabel = new Label();

            // 

            // 
            this.instructionLabel.AutoSize = true;
            this.instructionLabel.Location = new System.Drawing.Point(12, 9);
            this.instructionLabel.Name = "instructionLabel";
            this.instructionLabel.Size = new System.Drawing.Size(135, 13);
            this.instructionLabel.TabIndex = 0;
            this.instructionLabel.Text = "Enter the item display name:";

            // 
            // itemNameTextBox
            // 
            this.itemNameTextBox.Location = new System.Drawing.Point(12, 25);
            this.itemNameTextBox.Name = "itemNameTextBox";
            this.itemNameTextBox.Size = new System.Drawing.Size(360, 20);
            this.itemNameTextBox.TabIndex = 1;

            // 
            // weightLabel
            // 
            this.weightLabel.AutoSize = true;
            this.weightLabel.Location = new System.Drawing.Point(12, 55);
            this.weightLabel.Name = "weightLabel";
            this.weightLabel.Size = new System.Drawing.Size(44, 13);
            this.weightLabel.TabIndex = 2;
            this.weightLabel.Text = "Weight:";

            // 
            // weightTextBox
            // 
            this.weightTextBox.Location = new System.Drawing.Point(62, 52);
            this.weightTextBox.Name = "weightTextBox";
            this.weightTextBox.Size = new System.Drawing.Size(60, 20);
            this.weightTextBox.TabIndex = 3;
            this.weightTextBox.Text = "1";

            // 
            // hueLabel
            // 
            this.hueLabel.AutoSize = true;
            this.hueLabel.Location = new System.Drawing.Point(150, 55);
            this.hueLabel.Name = "hueLabel";
            this.hueLabel.Size = new System.Drawing.Size(89, 13);
            this.hueLabel.TabIndex = 4;
            this.hueLabel.Text = "Use Preview Hue:";

            // 
            // useHueCheckBox
            // 
            this.useHueCheckBox.AutoSize = true;
            this.useHueCheckBox.Location = new System.Drawing.Point(245, 55);
            this.useHueCheckBox.Name = "useHueCheckBox";
            this.useHueCheckBox.Size = new System.Drawing.Size(15, 14);
            this.useHueCheckBox.TabIndex = 5;
            this.useHueCheckBox.UseVisualStyleBackColor = true;

            // 
            // stackableLabel
            // 
            stackableLabel.AutoSize = true;
            stackableLabel.Location = new System.Drawing.Point(12, 80);
            stackableLabel.Name = "stackableLabel";
            stackableLabel.Size = new System.Drawing.Size(62, 13);
            stackableLabel.TabIndex = 6;
            stackableLabel.Text = "Stackable:";

            // 
            // isStackableCheckBox
            // 
            this.isStackableCheckBox.AutoSize = true;
            this.isStackableCheckBox.Location = new System.Drawing.Point(80, 80);
            this.isStackableCheckBox.Name = "isStackableCheckBox";
            this.isStackableCheckBox.Size = new System.Drawing.Size(15, 14);
            this.isStackableCheckBox.TabIndex = 7;
            this.isStackableCheckBox.UseVisualStyleBackColor = true;
            this.isStackableCheckBox.Visible = false;

            // 
            // artifactLabel
            // 
            artifactLabel.AutoSize = true;
            artifactLabel.Location = new System.Drawing.Point(150, 80);
            artifactLabel.Name = "artifactLabel";
            artifactLabel.Size = new System.Drawing.Size(62, 13);
            artifactLabel.TabIndex = 8;
            artifactLabel.Text = "Is Artifact:";

            // 
            // isArtifactCheckBox
            // 
            this.isArtifactCheckBox.AutoSize = true;
            this.isArtifactCheckBox.Location = new System.Drawing.Point(215, 80);
            this.isArtifactCheckBox.Name = "isArtifactCheckBox";
            this.isArtifactCheckBox.Size = new System.Drawing.Size(15, 14);
            this.isArtifactCheckBox.TabIndex = 9;
            this.isArtifactCheckBox.UseVisualStyleBackColor = true;

            // 
            // prefixLabel
            // 
            prefixLabel.AutoSize = true;
            prefixLabel.Location = new System.Drawing.Point(12, 105);
            prefixLabel.Name = "prefixLabel";
            prefixLabel.Size = new System.Drawing.Size(39, 13);
            prefixLabel.TabIndex = 10;
            prefixLabel.Text = "Prefix:";

            // 
            // prefixComboBox
            // 
            this.prefixComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            this.prefixComboBox.FormattingEnabled = true;
            this.prefixComboBox.Items.AddRange(new object[] { "None", "A", "An", "The" });
            this.prefixComboBox.Location = new System.Drawing.Point(57, 102);
            this.prefixComboBox.Name = "prefixComboBox";
            this.prefixComboBox.Size = new System.Drawing.Size(65, 21);
            this.prefixComboBox.TabIndex = 11;
            this.prefixComboBox.SelectedIndex = 0;

            // 
            // okButton
            // 
            this.okButton.DialogResult = DialogResult.OK;
            this.okButton.Location = new System.Drawing.Point(216, 130);
            this.okButton.Name = "okButton";
            this.okButton.Size = new System.Drawing.Size(75, 23);
            this.okButton.TabIndex = 12;
            this.okButton.Text = "OK";
            this.okButton.UseVisualStyleBackColor = true;
            this.okButton.Click += okButton_Click;

            // 
            // cancelButton
            // 
            this.cancelButton.DialogResult = DialogResult.Cancel;
            this.cancelButton.Location = new System.Drawing.Point(297, 130);
            this.cancelButton.Name = "cancelButton";
            this.cancelButton.Size = new System.Drawing.Size(75, 23);
            this.cancelButton.TabIndex = 13;
            this.cancelButton.Text = "Cancel";
            this.cancelButton.UseVisualStyleBackColor = true;

            // 
            // ItemNameInputForm
            // 
            this.AcceptButton = this.okButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.CancelButton = this.cancelButton;
            this.ClientSize = new System.Drawing.Size(384, 165);
            this.Controls.Add(this.cancelButton);
            this.Controls.Add(this.okButton);
            this.Controls.Add(this.prefixComboBox);
            this.Controls.Add(prefixLabel);
            this.Controls.Add(this.isArtifactCheckBox);
            this.Controls.Add(artifactLabel);
            this.Controls.Add(this.isStackableCheckBox);
            this.Controls.Add(stackableLabel);
            this.Controls.Add(this.useHueCheckBox);
            this.Controls.Add(this.hueLabel);
            this.Controls.Add(this.weightTextBox);
            this.Controls.Add(this.weightLabel);
            this.Controls.Add(this.itemNameTextBox);
            this.Controls.Add(this.instructionLabel);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ItemNameInputForm";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Text = "Create ServUO Item Script";
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private void okButton_Click(object sender, EventArgs e)
        {
            ItemName = itemNameTextBox.Text;
            UseHue = useHueCheckBox.Checked;
            IsStackable = isStackableCheckBox.Checked;
            IsArtifact = isArtifactCheckBox.Checked;
            SelectedPrefix = prefixComboBox.SelectedItem?.ToString() ?? "None";

            // Parse weight, default to 1 if invalid
            if (!int.TryParse(weightTextBox.Text, out int weight) || weight < 0)
            {
                weight = 1;
            }
            ItemWeight = weight;

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}

