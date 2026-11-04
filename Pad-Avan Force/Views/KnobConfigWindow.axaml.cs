using Avalonia.Controls;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PadAwan_Force.ViewModels;

namespace PadAwan_Force.Views
{
    public partial class KnobConfigWindow : Window
    {
        private string _knobLetter;
        private ViewModels.MainWindowViewModel? _viewModel;
        
        // Flag to prevent SelectionChanged from interfering during loading
        private bool _isLoadingConfiguration = false;
        
        // Configured actions
        public string ConfiguredCcwAction { get; private set; } = "None";
        public string ConfiguredCwAction { get; private set; } = "None";
        public string ConfiguredPressAction { get; private set; } = "None";
        
        // Configured keys
        public string ConfiguredCcwKey { get; private set; } = "";
        public string ConfiguredCwKey { get; private set; } = "";
        public string ConfiguredPressKey { get; private set; } = "";
        
        // Hold to repeat
        public bool ConfiguredPressHoldToRepeat { get; private set; } = false;
        
        // Key combo lists
        public List<string> ConfiguredCcwKeyCombo { get; private set; } = new List<string>();
        public List<string> ConfiguredCwKeyCombo { get; private set; } = new List<string>();
        public List<string> ConfiguredPressKeyCombo { get; private set; } = new List<string>();

        public KnobConfigWindow() : this("A", null)
        {
        }

        public KnobConfigWindow(string knobLetter, ViewModels.MainWindowViewModel? viewModel = null)
        {
            InitializeComponent();
            _knobLetter = knobLetter;
            _viewModel = viewModel;
            
            this.Title = $"Knob {knobLetter} • {_viewModel?.CurrentLayer?.Name ?? "No layer"}";
            UpdateKnobNumber(knobLetter);
            
            this.Loaded += async (s, e) => 
            {
                await Task.Delay(200);
                _isLoadingConfiguration = true;
                try
                {
                    await LoadCurrentConfiguration();
                }
                finally
                {
                    _isLoadingConfiguration = false;
                }
            };
        }

        private void UpdateKnobNumber(string knobLetter)
        {
            if (KnobNumberText != null)
            {
                KnobNumberText.Text = $"Knob {knobLetter}";
            }
        }

        private async Task LoadCurrentConfiguration()
        {
            if (_viewModel?.CurrentLayer != null)
            {
                if (_viewModel.CurrentLayer.Knobs.TryGetValue(_knobLetter, out var knobConfig))
                {
                    // Load CCW
                    if (CcwActionComboBox != null && CcwActionComboBox.Items != null)
                    {
                        int ccwIndex = GetActionIndex(knobConfig.CcwAction);
                        if (ccwIndex >= 0 && ccwIndex < CcwActionComboBox.Items.Count)
                        {
                            CcwActionComboBox.SelectedIndex = ccwIndex;
                        }
                    }
                    
                    // Load CW
                    if (CwActionComboBox != null && CwActionComboBox.Items != null)
                    {
                        int cwIndex = GetActionIndex(knobConfig.CwAction);
                        if (cwIndex >= 0 && cwIndex < CwActionComboBox.Items.Count)
                        {
                            CwActionComboBox.SelectedIndex = cwIndex;
                        }
                    }
                    
                    // Load Press
                    LoadPressConfiguration(knobConfig.PressAction, knobConfig.PressKey ?? "", knobConfig.PressHoldToRepeat);
                    return;
                }
            }

            SetDefaultSelections();
        }

        private void LoadPressConfiguration(string action, string key, bool holdToRepeat)
        {
            int pressIndex = GetPressActionIndex(action);
            if (PressActionComboBox != null)
            {
                PressActionComboBox.SelectedIndex = pressIndex;
            }

            UpdatePressPanelVisibility();

            if (action == "Key combo" || action == "Special Key")
            {
                PressKeyRecorder.RecordedKey = key;
                PressShortcutHoldToRepeatCheckBox.IsChecked = holdToRepeat;
            }
            else if (action == "Type Text")
            {
                PressKeyValueTextBox.Text = key;
                PressTypeTextHoldToRepeatCheckBox.IsChecked = holdToRepeat;
            }
            else if (action == "Layer Switch")
            {
                string direction = (key ?? "up").ToLower();
                PressLayerSwitchDirectionComboBox.SelectedIndex = (direction == "down") ? 1 : 0;
            }
        }

        private void SetDefaultSelections()
        {
            if (CcwActionComboBox != null) CcwActionComboBox.SelectedIndex = 6; // None
            if (CwActionComboBox != null) CwActionComboBox.SelectedIndex = 6;   // None
            if (PressActionComboBox != null) PressActionComboBox.SelectedIndex = 3; // None
            UpdatePressPanelVisibility();
        }

        private int GetActionIndex(string action)
        {
            return action switch
            {
                "Increase Volume" => 0,
                "Decrease Volume" => 1,
                "Scroll Up" => 2,
                "Scroll Down" => 3,
                "Layer Count Up" => 4,
                "Layer Count Down" => 5,
                "None" => 6,
                _ => 6
            };
        }

        private int GetPressActionIndex(string action)
        {
            return action switch
            {
                "Key combo" or "Special Key" => 0, // Key / Shortcut
                "Type Text" => 1,
                "Layer Switch" => 2,
                "None" => 3,
                _ => 3
            };
        }

        private void PressActionComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingConfiguration) return;
            UpdatePressPanelVisibility();
        }

        private void UpdatePressPanelVisibility()
        {
            if (PressShortcutPanel == null || PressKeyValuePanel == null || PressLayerSwitchPanel == null)
                return;

            string selected = GetSelectedPressActionType();

            switch (selected)
            {
                case "Key / Shortcut":
                    PressShortcutPanel.IsVisible = true;
                    PressKeyValuePanel.IsVisible = false;
                    PressLayerSwitchPanel.IsVisible = false;
                    break;
                case "Type Text":
                    PressShortcutPanel.IsVisible = false;
                    PressKeyValuePanel.IsVisible = true;
                    PressLayerSwitchPanel.IsVisible = false;
                    break;
                case "Layer Switch":
                    PressShortcutPanel.IsVisible = false;
                    PressKeyValuePanel.IsVisible = false;
                    PressLayerSwitchPanel.IsVisible = true;
                    break;
                default: // None
                    PressShortcutPanel.IsVisible = false;
                    PressKeyValuePanel.IsVisible = false;
                    PressLayerSwitchPanel.IsVisible = false;
                    break;
            }
        }

        private string GetSelectedPressActionType()
        {
            if (PressActionComboBox?.SelectedItem is ComboBoxItem item)
            {
                return item.Content?.ToString() ?? "None";
            }
            return "None";
        }

        private string GetSelectedComboBoxText(ComboBox? comboBox)
        {
            if (comboBox?.SelectedItem is ComboBoxItem item)
            {
                return item.Content?.ToString() ?? "None";
            }
            return "None";
        }

        private void SaveButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            ConfiguredCcwAction = GetSelectedComboBoxText(CcwActionComboBox);
            ConfiguredCwAction = GetSelectedComboBoxText(CwActionComboBox);
            ConfiguredCcwKey = "";
            ConfiguredCwKey = "";

            string pressActionType = GetSelectedPressActionType();

            switch (pressActionType)
            {
                case "Key / Shortcut":
                    string key = PressKeyRecorder.RecordedKey?.Trim() ?? "";
                    if (string.IsNullOrEmpty(key))
                    {
                        ConfiguredPressAction = "None";
                        ConfiguredPressKey = "";
                        ConfiguredPressHoldToRepeat = false;
                    }
                    else
                    {
                        ConfiguredPressAction = key.Contains("+") ? "Key combo" : "Special Key";
                        ConfiguredPressKey = key;
                        ConfiguredPressHoldToRepeat = PressShortcutHoldToRepeatCheckBox.IsChecked ?? false;
                    }
                    break;

                case "Type Text":
                    ConfiguredPressAction = "Type Text";
                    ConfiguredPressKey = PressKeyValueTextBox.Text?.Trim() ?? "";
                    ConfiguredPressHoldToRepeat = PressTypeTextHoldToRepeatCheckBox.IsChecked ?? false;
                    break;

                case "Layer Switch":
                    ConfiguredPressAction = "Layer Switch";
                    ConfiguredPressKey = (PressLayerSwitchDirectionComboBox.SelectedIndex == 1) ? "down" : "up";
                    ConfiguredPressHoldToRepeat = false;
                    break;

                default:
                    ConfiguredPressAction = "None";
                    ConfiguredPressKey = "";
                    ConfiguredPressHoldToRepeat = false;
                    break;
            }

            ConfiguredPressKeyCombo = (ConfiguredPressAction == "Key combo")
                ? ConfiguredPressKey.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
                : new List<string> { ConfiguredPressKey };

            this.Close(true);
        }

        public string GetConfiguredCcwAction() => ConfiguredCcwAction;
        public string GetConfiguredCwAction() => ConfiguredCwAction;
        public string GetConfiguredPressAction() => ConfiguredPressAction;
        public string GetConfiguredCcwKey() => ConfiguredCcwKey;
        public string GetConfiguredCwKey() => ConfiguredCwKey;
        public string GetConfiguredPressKey() => ConfiguredPressKey;
        public bool GetConfiguredPressHoldToRepeat() => ConfiguredPressHoldToRepeat;
    }
}
