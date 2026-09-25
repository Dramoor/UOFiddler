using System;
using System.Windows.Forms;
using Ultima;

namespace UoFiddler.Controls.Forms
{
    public partial class SelectAnimationSlotDialog : Form
    {
        private readonly int _fileType;
        private readonly int _currentBody;
        private readonly string[] _bodyNames;
        private readonly int _filterAnimLength; // -1 means no filter

        public int SelectedBody { get; private set; } = -1;

        public SelectAnimationSlotDialog(int fileType, int currentBody, string[] bodyNames, int filterAnimLength = -1)
        {
            InitializeComponent();
            _fileType = fileType;
            _currentBody = currentBody;
            _bodyNames = bodyNames ?? Array.Empty<string>();
            _filterAnimLength = filterAnimLength;

            InitializeAvailableBodies();
        }

        private void InitializeAvailableBodies()
        {
            BodyListBox.Items.Clear();

            // Get all available bodies for this file type
            // Enumerate through possible body indices and find empty ones
            int maxBodies = 10000; // Reasonable max to check

            for (int bodyId = 0; bodyId < maxBodies; bodyId++)
            {
                if (bodyId == _currentBody)
                {
                    continue; // Skip the current body
                }

                // Check if this body is empty (has no valid animations)
                bool isEmpty = true;

                // Check a few action slots to see if body is empty
                for (int action = 0; action < 10; action++)
                {
                    try
                    {
                        AnimIdx anim = AnimationEdit.GetAnimation(_fileType, bodyId, action, 0);
                        if (anim != null && anim.Frames != null && anim.Frames.Count > 0)
                        {
                            isEmpty = false;
                            break;
                        }
                    }
                    catch
                    {
                        // If we can't access this body, assume it's empty
                    }
                }

                if (isEmpty)
                {
                    // If a length filter is specified, check if this body has the matching capacity
                    if (_filterAnimLength > 0)
                    {
                        int bodyCapacity = Animations.GetActionCapacity(bodyId, _fileType);
                        if (bodyCapacity != _filterAnimLength)
                        {
                            continue; // Skip bodies that don't match the filter
                        }
                    }

                    string bodyName = bodyId < _bodyNames.Length ? _bodyNames[bodyId] : $"Body {bodyId}";
                    BodyListBox.Items.Add(new BodySlotItem(bodyId, bodyName));
                }
            }

            // If no empty slots were found, show a message
            if (BodyListBox.Items.Count == 0)
            {
                string filterMsg = _filterAnimLength > 0 ? $" with {_filterAnimLength} actions" : "";
                BodyListBox.Items.Add($"No empty animation slots available{filterMsg}");
                OkButton.Enabled = false;
            }
            else
            {
                BodyListBox.SelectedIndex = 0;
            }
        }

        private void OkButton_Click(object sender, EventArgs e)
        {
            if (BodyListBox.SelectedItem is BodySlotItem item)
            {
                SelectedBody = item.BodyId;
                DialogResult = DialogResult.OK;
            }
            else
            {
                DialogResult = DialogResult.Cancel;
            }
            Close();
        }

        private void CancelButton_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private class BodySlotItem
        {
            public int BodyId { get; }
            public string BodyName { get; }

            public BodySlotItem(int bodyId, string bodyName)
            {
                BodyId = bodyId;
                BodyName = bodyName;
            }

            public override string ToString() => $"{BodyId}: {BodyName}";
        }
    }
}
