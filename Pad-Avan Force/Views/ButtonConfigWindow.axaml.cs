using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PadAwan_Force.Models;
using PadAwan_Force.ViewModels;

namespace PadAwan_Force.Views
{
    public partial class ButtonConfigWindow : Window
    {
        private int _buttonNumber;
        private MainWindowViewModel? _viewModel;
        private bool _isLoadingConfiguration = false;

        public string ConfiguredAction { get; private set; } = "None";
        public string ConfiguredKey { get; private set; } = "";
        public string? ConfiguredLabel { get; private set; } = null;
        public List<string> ConfiguredKeyCombo { get; private set; } = new List<string>();
        public bool ConfiguredHoldToRepeat { get; private set; } = false;

        public ButtonConfigWindow() : this(1, null)
        {
        }

        public ButtonConfigWindow(int buttonNumber, MainWindowViewModel? viewModel)
        {
            InitializeComponent();
            _buttonNumber = buttonNumber;
            _viewModel = viewModel;

            this.Title = $"Button {buttonNumber} • {_viewModel?.CurrentLayer?.Name ?? "No layer"}";
            UpdateButtonNumber(buttonNumber);

            this.Loaded += async (s, e) =>
            {
                await Task.Delay(200);
                _isLoadingConfiguration = true;
                try
                {
                    LoadCurrentConfiguration();
                }
                finally
                {
                    _isLoadingConfiguration = false;
                }
            };
        }

        private void UpdateButtonNumber(int buttonNumber)
        {
            if (ButtonNumberText != null)
            {
                ButtonNumberText.Text = $"Button {buttonNumber}";
            }
        }

        private void LoadCurrentConfiguration()
        {
            if (_viewModel?.CurrentLayer != null)
            {
                string buttonId = _buttonNumber.ToString();
                if (_viewModel.CurrentLayer.Buttons.TryGetValue(buttonId, out var buttonConfig))
                {
                    if (ButtonLabelTextBox != null)
                    {
                        ButtonLabelTextBox.Text = buttonConfig.Label ?? "";
                    }

                    string action = buttonConfig.Action ?? "None";
                    int actionIndex = GetActionIndex(action);
                    
                    if (ActionTypeComboBox != null)
                    {
                        ActionTypeComboBox.SelectedIndex = actionIndex;
                    }

                    UpdatePanelVisibility(GetSelectedActionType());

                    if (action == "Key combo" || action == "Special Key")
                    {
                        if (KeyRecorder != null)
                        {
                            KeyRecorder.RecordedKey = buttonConfig.Key ?? "";
                        }
                        if (ShortcutHoldToRepeatCheckBox != null)
                        {
                            ShortcutHoldToRepeatCheckBox.IsChecked = buttonConfig.HoldToRepeat;
                        }
                    }
                    else if (action == "Type Text")
                    {
                        if (KeyValueTextBox != null)
                        {
                            KeyValueTextBox.Text = buttonConfig.Key ?? "";
                        }
                        if (HoldToRepeatCheckBox != null)
                        {
                            HoldToRepeatCheckBox.IsChecked = buttonConfig.HoldToRepeat;
                        }
                    }
                    else if (action == "Layer Switch")
                    {
                        if (LayerSwitchDirectionComboBox != null)
                        {
                            string direction = (buttonConfig.Key ?? "up").ToLower();
                            LayerSwitchDirectionComboBox.SelectedIndex = (direction == "down") ? 1 : 0;
                        }
                    }
                    return;
                }
            }

            // Defaults if not configured
            if (ActionTypeComboBox != null)
            {
                ActionTypeComboBox.SelectedIndex = 3; // None
            }
            UpdatePanelVisibility("None");
        }

        private int GetActionIndex(string action)
        {
            return action switch
            {
                "Key combo" or "Special Key" => 0,
                "Type Text" => 1,
                "Layer Switch" => 2,
                "None" => 3,
                _ => 3
            };
        }

        private string GetSelectedActionType()
        {
            if (ActionTypeComboBox?.SelectedItem is ComboBoxItem item)
            {
                return item.Content?.ToString() ?? "None";
            }
            return "None";
        }

        private void ActionTypeComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingConfiguration) return;
            UpdatePanelVisibility(GetSelectedActionType());
        }

        private void UpdatePanelVisibility(string selectedAction)
        {
            if (ShortcutPanel == null || KeyValuePanel == null || LayerSwitchPanel == null)
                return;

            switch (selectedAction)
            {
                case "Key / Shortcut":
                    ShortcutPanel.IsVisible = true;
                    KeyValuePanel.IsVisible = false;
                    LayerSwitchPanel.IsVisible = false;
                    break;

                case "Type Text":
                    ShortcutPanel.IsVisible = false;
                    KeyValuePanel.IsVisible = true;
                    LayerSwitchPanel.IsVisible = false;
                    break;

                case "Layer Switch":
                    ShortcutPanel.IsVisible = false;
                    KeyValuePanel.IsVisible = false;
                    LayerSwitchPanel.IsVisible = true;
                    break;

                default: // None
                    ShortcutPanel.IsVisible = false;
                    KeyValuePanel.IsVisible = false;
                    LayerSwitchPanel.IsVisible = false;
                    break;
            }
        }

        private void SaveButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            string selectedAction = GetSelectedActionType();

            switch (selectedAction)
            {
                case "Key / Shortcut":
                    string key = KeyRecorder?.RecordedKey?.Trim() ?? "";
                    if (string.IsNullOrEmpty(key))
                    {
                        ConfiguredAction = "None";
                        ConfiguredKey = "";
                        ConfiguredHoldToRepeat = false;
                    }
                    else
                    {
                        ConfiguredAction = key.Contains("+") ? "Key combo" : "Special Key";
                        ConfiguredKey = key;
                        ConfiguredHoldToRepeat = ShortcutHoldToRepeatCheckBox?.IsChecked ?? false;
                    }
                    break;

                case "Type Text":
                    ConfiguredAction = "Type Text";
                    ConfiguredKey = KeyValueTextBox?.Text?.Trim() ?? "";
                    ConfiguredHoldToRepeat = HoldToRepeatCheckBox?.IsChecked ?? false;
                    break;

                case "Layer Switch":
                    ConfiguredAction = "Layer Switch";
                    ConfiguredKey = (LayerSwitchDirectionComboBox?.SelectedIndex == 1) ? "down" : "up";
                    ConfiguredHoldToRepeat = false;
                    break;

                default:
                    ConfiguredAction = "None";
                    ConfiguredKey = "";
                    ConfiguredHoldToRepeat = false;
                    break;
            }

            ConfiguredLabel = string.IsNullOrWhiteSpace(ButtonLabelTextBox?.Text) ? null : ButtonLabelTextBox.Text.Trim();

            ConfiguredKeyCombo = (ConfiguredAction == "Key combo")
                ? ConfiguredKey.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
                : new List<string> { ConfiguredKey };

            this.Close(true);
        }

        public string GetConfiguredAction() => ConfiguredAction;
        public string GetConfiguredKey() => ConfiguredKey;
        public string? GetConfiguredLabel() => ConfiguredLabel;
        public bool GetConfiguredHoldToRepeat() => ConfiguredHoldToRepeat;
    }
}
