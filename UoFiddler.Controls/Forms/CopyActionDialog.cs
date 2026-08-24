using System;
using System.Windows.Forms;
using Ultima;

namespace UoFiddler.Controls.Forms
{
    public partial class CopyActionDialog : Form
    {
        private readonly int _fileType;
        private readonly int _currentBody;
        private readonly string[] _actionNames;
        private readonly int _currentAction;

        public int SelectedAction { get; private set; }

        public CopyActionDialog(int fileType, int currentBody, string[] actionNames, int currentAction)
        {
            InitializeComponent();
            _fileType = fileType;
            _currentBody = currentBody;
            _actionNames = actionNames ?? Array.Empty<string>();
            _currentAction = currentAction;

            InitializeActionComboBox();
        }

        private void InitializeActionComboBox()
        {
            ActionComboBox.Items.Clear();

            int animCount = Animations.GetAnimLength(_currentBody, _fileType);
            for (int i = 0; i < animCount; i++)
            {
                string animName = i < _actionNames.Length ? _actionNames[i] : $"Action {i}";
                ActionComboBox.Items.Add($"{i}: {animName}");
            }

            ActionComboBox.SelectedIndex = _currentAction >= 0 && _currentAction < ActionComboBox.Items.Count ? _currentAction : 0;
        }

        private void OkButton_Click(object sender, EventArgs e)
        {
            SelectedAction = ActionComboBox.SelectedIndex;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void CancelActionButton_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
