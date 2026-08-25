        private void OnClickExportToVDScaledRemap(object sender, EventArgs e)
        {
            if (_fileType == 0)
            {
                return;
            }

            // Show resize dialog
            using (var resizeDialog = new AnimationExportResizeDialog())
            {
                if (resizeDialog.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                int scalePercentage = resizeDialog.ResizePercentage;

                using (SaveFileDialog dialog = new SaveFileDialog())
                {
                    dialog.Title = $"Export to .vd (Scaled {scalePercentage}%)";
                    dialog.InitialDirectory = Options.OutputPath;
                    dialog.Filter = "vd files (*.vd)|*.vd";
                    dialog.FileName = $"anim{_fileType}_{_currentBody}_scaled.vd";

                    if (dialog.ShowDialog() != DialogResult.OK)
                    {
                        return;
                    }

                    // Create small configuration dialog for VD type and frame selection
                    using (Form cfg = new Form())
                    {
                        cfg.StartPosition = FormStartPosition.CenterParent;
                        cfg.FormBorderStyle = FormBorderStyle.FixedDialog;
                        cfg.MinimizeBox = false;
                        cfg.MaximizeBox = false;
                        cfg.ShowInTaskbar = false;
                        cfg.Text = "Export VD Remapper (Scaled)";
                        int animLength = Animations.GetAnimLength(_currentBody, _fileType);
                        // Determine default type from animation length (same logic used elsewhere)
                        int defaultType = animLength == 22 ? 0 : animLength == 13 ? 1 : 2; // 0=High,1=Low,2=People

                        var lblType = new Label { Left = 8, Top = 12, Width = 120, Text = "Export VD Type:" };
                        // Use radio buttons for H / L / P selection
                        var rbHigh = new RadioButton { Left = 130, Top = 10, Width = 80, Text = "High (H)" };
                        var rbLow = new RadioButton { Left = 210, Top = 10, Width = 80, Text = "Low (L)" };
                        var rbPeople = new RadioButton { Left = 290, Top = 10, Width = 80, Text = "People (P)" };

                        rbHigh.Checked = defaultType == 0;
                        rbLow.Checked = defaultType == 1;
                        rbPeople.Checked = defaultType == 2;

                        // high dropdown labels (used to label source frames when source animation is High)
                        string[] highdropdownLabels = new string[]
                        {
                            "00 (Walk)", "01 (Idle1)", "02 (Die1)", "03 (Die2)", "04 (Attack1)", "05 (Attack2)",
                            "06 (Attack3)", "07 (AttackBow)", "08 (AttackCrossbow)", "09 (AttackThrow)", "10 (GetHit)", "11 (Pillage)",
                            "12 (Stomp)", "13 (Cast2)", "14 (Cast3)", "15 (BlockRight)", "16 (BlockLeft)", "17 (Idle2)",
                            "18 (Fidget)", "19 (Fly)", "20 (Takeoff)", "21 (GetHitInAir)"
                        };

                        // also used for target labels for High mapping panel
                        string[] highLabels = new string[]
                        {
                            "00 Walk", "01 Idle1", "02 Die1", "03 Die2", "04 Attack1", "05 Attack2",
                            "06 Attack3", "07 AttackBow", "08 AttackCrossbow", "09 AttackThrow", "10 GetHit", "11 Pillage",
                            "12 Stomp", "13 Cast2", "14 Cast3", "15 BlockRight", "16 BlockLeft", "17 Idle2",
                            "18 Fidget", "19 Fly", "20 Takeoff", "21 GetHitInAir"
                        };

                        // low dropdown labels (used to label source frames when source animation is Low)
                        string[] lowdropdownLabels = new string[]
                        {
                            "00 (Walk)", "01 (Run)", "02 (Idle)", "03 (Eat)", "04 (Alert)", "05 (Attack1)",
                            "06 (Attack2)", "07 (GetHit)", "08 (Die1)", "09 (Idle)", "10 (Fidget)", "11 (LieDown)", "12 (Die2)"
                        };

                        // also used for target labels for Low mapping panel
                        string[] lowLabels = new string[]
                        {
                            "00 Walk", "01 Run", "02 Idle", "03 Eat", "04 Alert", "05 Attack1",
                            "06 Attack2", "07 GetHit", "08 Die1", "09 Idle", "10 Fidget", "11 LieDown", "12 Die2"
                        };

                        // people dropdown labels (used to label source frames when source animation is People)
                        string[] peopledropdownLabels = new string[]
                        {
                            "00 Walk", "01 WalkStaff", "02 Run", "03 RunStaff", "04 Idle1", "05 Idle2", "06 Fidget_Yawn_Stretch",
                            "07 CombatIdle1Hand01", "08 CombatIdle1Hand02", "09 AttackSlash1Hand", "10 AttackPierce1Hand", "11 AttackBash1Hand",
                            "12 AttackBash2Hand", "13 AttackSlash2Hand", "14 AttackPierce2Hand", "15 CombatAdvance1Hand", "16 Spell1", "17 Spell2",
                            "18 AttackBow", "19 AttackCrossbow", "20 GetHit Fr Hi", "21 DieForward", "22 DieBackward", "23 MountWalk",
                            "24 MountRun", "25 MountIdle", "26 MountAttack1HandSlash", "27 MountAttackBow", "28 MountAttackCrossbow", "29 MountAttack2HandSlash",
                            "30 BlockShield", "31 PunchJab", "32 BowLesser", "33 SaluateArmed1Hand", "34 Eat"
                        };

                        // also used for target labels for People mapping panel
                        string[] peopleLabels = new string[]
                        {
                            "00 Walk", "01 WalkStaff", "02 Run", "03 RunStaff", "04 Idle1", "05 Idle2", "06 Fidget_Yawn_Stretch",
                            "07 CombatIdle1Hand01", "08 CombatIdle1Hand02", "09 AttackSlash1Hand", "10 AttackPierce1Hand", "11 AttackBash1Hand",
                            "12 AttackBash2Hand", "13 AttackSlash2Hand", "14 AttackPierce2Hand", "15 CombatAdvance1Hand", "16 Spell1", "17 Spell2",
                            "18 AttackBow", "19 AttackCrossbow", "20 GetHit Fr Hi", "21 DieForward", "22 DieBackward", "23 MountWalk",
                            "24 MountRun", "25 MountIdle", "26 MountAttack1HandSlash", "27 MountAttackBow", "28 MountAttackCrossbow", "29 MountAttack2HandSlash",
                            "30 BlockShield", "31 PunchJab", "32 BowLesser", "33 SaluateArmed1Hand", "34 Eat"
                        };

                        // Determine source animation type from animLength: 0=High,1=Low,2=People
                        int sourceType = animLength == 22 ? 0 : animLength == 13 ? 1 : 2;

                        // Remapping UI: prepare map lists for each target type
                        var mapHigh = new System.Collections.Generic.List<ComboBox>();
                        var mapLow = new System.Collections.Generic.List<ComboBox>();
                        var mapPeople = new System.Collections.Generic.List<ComboBox>();

                        // Panel sizes per export target: fixed values
                        const int panelHeightHigh = 621;
                        const int panelHeightLow = 370;
                        const int panelHeightPeople = 621;
                        const int panelWidthHigh = 360;
                        const int panelWidthLow = 360;
                        // wider panel for People so labels and comboboxes don't require horizontal scroll
                        const int panelWidthPeople = 570;
                        // preferred combobox width for People (narrower than full panel)
                        const int cbWidthPeople = 300;
                        const int labelWidthHigh = 110;
                        const int labelWidthLow = 110;
                        const int labelWidthPeople = 240;

                        // helper to build mapping panel
                        Panel BuildMapPanel(System.Collections.Generic.List<ComboBox> mapList, string[] targetLabels = null, int srcType = -1, int panelHeight = 370, int panelWidth = 360, int lblWidth = 110, int cbWidth = -1)
                        {
                            var pnl = new Panel { AutoScroll = true };
                            // fixed layout values
                            int y = 4;
                            int count = targetLabels != null ? targetLabels.Length : animLength;
                            int lblLeft = 4;
                            // lblWidth parameter allows wider labels for People
                            // cbLeft and cbWidth adapt to provided panelWidth
                            int cbLeft = lblLeft + lblWidth + 8;
                            int cbWidthLocal = cbWidth > 0 ? cbWidth : panelWidth - cbLeft - 8;

                            for (int tgt = 0; tgt < count; tgt++)
                            {
                                string lblText = targetLabels != null ? targetLabels[tgt] : tgt.ToString();
                                var lbl = new Label { Left = lblLeft, Top = y + 6, Width = lblWidth, Text = lblText, AutoSize = false };
                                var cb = new ComboBox { Left = cbLeft, Top = y + 2, Width = cbWidthLocal, DropDownStyle = ComboBoxStyle.DropDownList };
                                // Only add valid source actions to the dropdown
                                var items = new System.Collections.Generic.List<MapItem>();
                                items.Add(new MapItem(-1, "None"));
                                for (int src = 0; src < animLength; src++)
                                {
                                    if (!AnimationEdit.IsActionDefined(_fileType, _currentBody, src))
                                        continue;

                                    // Determine base label from the SOURCE animation type (animLength of the currently loaded animation)
                                    int effectiveSourceType = Animations.GetAnimLength(_currentBody, _fileType) == 22 ? 0 : Animations.GetAnimLength(_currentBody, _fileType) == 13 ? 1 : 2;
                                    string baseLabel;
                                    if (effectiveSourceType == 1 && src < lowdropdownLabels.Length)
                                    {
                                        baseLabel = lowdropdownLabels[src];
                                    }
                                    else if (effectiveSourceType == 0 && src < highdropdownLabels.Length)
                                    {
                                        baseLabel = highdropdownLabels[src];
                                    }
                                    else if (effectiveSourceType == 2 && src < peopledropdownLabels.Length)
                                    {
                                        baseLabel = peopledropdownLabels[src];
                                    }
                                    else
                                    {
                                        baseLabel = src.ToString();
                                    }

                                    items.Add(new MapItem(src, baseLabel));
                                }

                                foreach (var it in items) cb.Items.Add(it);
                                // default mapping: identity if defined, otherwise None
                                MapItem selItem = items.FirstOrDefault(x => x.Index == tgt);
                                if (selItem != null) cb.SelectedItem = selItem; else cb.SelectedIndex = 0;
                                pnl.Controls.Add(lbl);
                                pnl.Controls.Add(cb);
                                mapList.Add(cb);
                                y += 28;
                            }
                            // set a reasonable size; caller will position panel
                            pnl.Size = new System.Drawing.Size(panelWidth, panelHeight);
                            return pnl;
                        }

                        // Panels will be created once below and shown/hidden based on selection

                        var panelHigh = BuildMapPanel(mapHigh, highLabels, sourceType, panelHeightHigh, panelWidthHigh, labelWidthHigh);
                        var panelLow = BuildMapPanel(mapLow, lowLabels, sourceType, panelHeightLow, panelWidthLow, labelWidthLow);
                        var panelPeople = BuildMapPanel(mapPeople, peopleLabels, sourceType, panelHeightPeople, panelWidthPeople, labelWidthPeople, cbWidthPeople);

                        // position panels below radio buttons
                        panelHigh.Location = new System.Drawing.Point(8, 40);
                        panelLow.Location = new System.Drawing.Point(8, 40);
                        panelPeople.Location = new System.Drawing.Point(8, 40);
                        panelHigh.Visible = rbHigh.Checked;
                        panelLow.Visible = rbLow.Checked;
                        panelPeople.Visible = rbPeople.Checked;

                        // declare buttons so the rbChanged handler can reference them
                        Button btnOk = null;
                        Button btnCancel = null;

                        EventHandler rbChanged = (s, ev) =>
                        {
                            panelHigh.Visible = rbHigh.Checked;
                            panelLow.Visible = rbLow.Checked;
                            panelPeople.Visible = rbPeople.Checked;

                            // adjust dialog size based on selected target panel
                            int newPanelH = rbHigh.Checked ? panelHeightHigh : rbLow.Checked ? panelHeightLow : panelHeightPeople;
                            int newPanelW = rbHigh.Checked ? panelWidthHigh : rbLow.Checked ? panelWidthLow : panelWidthPeople;
                            // keep a small margin around the panel
                            cfg.ClientSize = new System.Drawing.Size(newPanelW + 60, newPanelH + 120);

                            // reposition OK/Cancel so they stay a fixed distance from bottom
                            btnOk.Top = cfg.ClientSize.Height - 45;
                            btnCancel.Top = cfg.ClientSize.Height - 45;
                        };

                        rbHigh.CheckedChanged += rbChanged;
                        rbLow.CheckedChanged += rbChanged;
                        rbPeople.CheckedChanged += rbChanged;

                        // set initial dialog size based on defaultType and panel heights
                        int initialPanelHeight = defaultType == 0 ? panelHeightHigh : defaultType == 1 ? panelHeightLow : panelHeightPeople;
                        int initialPanelWidth = defaultType == 0 ? panelWidthHigh : defaultType == 1 ? panelWidthLow : panelWidthPeople;
                        cfg.ClientSize = new System.Drawing.Size(initialPanelWidth + 60, initialPanelHeight + 120);

                        btnOk = new Button { Text = "OK", Left = 240, Width = 60, Top = cfg.ClientSize.Height - 45, DialogResult = DialogResult.OK };
                        btnCancel = new Button { Text = "Cancel", Left = 310, Width = 60, Top = cfg.ClientSize.Height - 45, DialogResult = DialogResult.Cancel };

                        cfg.Controls.AddRange(new Control[] { lblType, rbHigh, rbLow, rbPeople, panelHigh, panelLow, panelPeople, btnOk, btnCancel });
                        cfg.AcceptButton = btnOk;
                        cfg.CancelButton = btnCancel;

                        if (cfg.ShowDialog(this) != DialogResult.OK)
                        {
                            return;
                        }


                        int selectedType = rbHigh.Checked ? 0 : rbLow.Checked ? 1 : 2; // 0=High,1=Low,2=People

                        // Collect checked actions
                        int[] actionsToInclude = null;
                        // Build target->source mapping from the selected tab's ComboBoxes
                        int[] targetToSourceMap = null;
                        System.Collections.Generic.List<ComboBox> chosenMap = null;
                        if (selectedType == 0) chosenMap = mapHigh;
                        else if (selectedType == 1) chosenMap = mapLow;
                        else if (selectedType == 2) chosenMap = mapPeople;
                        else chosenMap = mapHigh;

                        if (chosenMap != null)
                        {
                            targetToSourceMap = new int[chosenMap.Count];
                            for (int t = 0; t < chosenMap.Count; t++)
                            {
                                // Be defensive: treat the first entry ("None"), no selection, or any negative index as an empty action
                                var combo = chosenMap[t];
                                var selObj = combo.SelectedItem as MapItem;
                                if (combo.SelectedIndex <= 0 || selObj == null || selObj.Index < 0)
                                    targetToSourceMap[t] = -1;
                                else
                                    targetToSourceMap[t] = selObj.Index;
                            }
                        }

                        string filePath = dialog.FileName;
                        // if exactly one target mapped to a single source, append that to filename
                        if (targetToSourceMap != null)
                        {
                            int single = -1;
                            for (int i = 0; i < targetToSourceMap.Length; i++)
                            {
                                if (targetToSourceMap[i] >= 0)
                                {
                                    if (single == -1) single = i; else { single = -2; break; }
                                }
                            }
                            if (single >= 0)
                            {
                                string dir = Path.GetDirectoryName(filePath);
                                string baseName = Path.GetFileNameWithoutExtension(filePath);
                                string ext = Path.GetExtension(filePath);
                                filePath = Path.Combine(dir, baseName + $"_t{single}_s{targetToSourceMap[single]}" + ext);
                            }
                        }

                        if (selectedType >= 0)
                        {
                            float scale = scalePercentage / 100.0f;
                            AnimationEdit.ExportToVDRemapScaled(_fileType, _currentBody, filePath, selectedType, targetToSourceMap, scale);
                        }
                        else
                        {
                            float scale = scalePercentage / 100.0f;
                            AnimationEdit.ExportToVDRemapScaled(_fileType, _currentBody, filePath, -1, targetToSourceMap, scale);
                        }

                        MessageBox.Show($"Animation saved to {Path.GetDirectoryName(filePath)}", "Export", MessageBoxButtons.OK,
                            MessageBoxIcon.Information, MessageBoxDefaultButton.Button1);
                    }
                }
            }
        }
