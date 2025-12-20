using Avalonia.Controls;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using PadAwan_Force.Models;
using PadAwan_Force.ViewModels;

namespace PadAwan_Force.Views
{
    public partial class ButtonConfigWindow : Window
    {
        private int _buttonNumber;
        private int _keyComboCount = 1; // Track number of key combo elements
        private const int MAX_KEY_COMBO_COUNT = 3; // Maximum 3 key boxes

        public string ConfiguredAction { get; private set; } = "None";
        public string ConfiguredKey { get; private set; } = "";
        public List<string> ConfiguredKeyCombo { get; private set; } = new List<string>();

        public ButtonConfigWindow() : this(1, null)
        {
        }

        public ButtonConfigWindow(int buttonNumber, MainWindowViewModel? viewModel)
        {
            InitializeComponent();
            _buttonNumber = buttonNumber;
            _viewModel = viewModel;
            
            // Show the active layer in the config window title (so you notice immediately)
            this.Title = $"Button {buttonNumber} • {_viewModel?.CurrentLayer?.Name ?? "No layer"}";
            
            UpdateButtonNumber(buttonNumber);
            LoadCurrentConfiguration();
        }

        private MainWindowViewModel? _viewModel;

        private void UpdateButtonNumber(int buttonNumber)
        {
            var buttonNumberText = this.FindControl<TextBlock>("ButtonNumberText");
            if (buttonNumberText != null)
            {
                buttonNumberText.Text = $"Button {buttonNumber}";
            }
        }

        private void LoadCurrentConfiguration()
        {
            Console.WriteLine($"LoadCurrentConfiguration called for button {_buttonNumber}");
            Console.WriteLine($"ViewModel is null: {_viewModel == null}");
            Console.WriteLine($"CurrentLayer is null: {_viewModel?.CurrentLayer == null}");
            
            if (_viewModel?.CurrentLayer != null)
            {
                Console.WriteLine($"Current layer name: {_viewModel.CurrentLayer?.Name ?? "Unknown"}");
                Console.WriteLine($"Current layer ID: {_viewModel.CurrentLayer?.Id ?? 0}");
            }
            
            // Load current configuration from the passed view model
            if (_viewModel?.CurrentLayer != null)
            {
                string buttonId = _buttonNumber.ToString();
                Console.WriteLine($"Looking for button ID: {buttonId}");
                Console.WriteLine($"Available buttons: {string.Join(", ", _viewModel.CurrentLayer?.Buttons.Keys ?? Enumerable.Empty<string>())}");
                
                // Debug: Show all button configurations in the current layer
                foreach (var kvp in _viewModel.CurrentLayer?.Buttons ?? new Dictionary<string, ButtonConfig>())
                {
                    Console.WriteLine($"  Layer {_viewModel.CurrentLayer?.Name ?? "Unknown"} - Button {kvp.Key}: Action='{kvp.Value.Action}', Key='{kvp.Value.Key}', Enabled={kvp.Value.Enabled}");
                }
                
                if (_viewModel.CurrentLayer?.Buttons.TryGetValue(buttonId, out var buttonConfig) == true)
                {
                    Console.WriteLine($"Found button config: Action='{buttonConfig.Action}', Key='{buttonConfig.Key}'");
                    
                    // Set the action type
                    int actionIndex = GetActionIndex(buttonConfig.Action);
                    Console.WriteLine($"Setting action index to: {actionIndex}");
                    ActionTypeComboBox.SelectedIndex = actionIndex;
                    
                    // Handle different action types
                    if (buttonConfig.Action == "Key combo")
                    {
                        // Parse key combo and populate the combo boxes
                        LoadKeyCombo(buttonConfig.Key ?? "");
                    }
                    else if (buttonConfig.Action == "Special Key")
                    {
                        // Set the special key selection
                        var specialKeyComboBox = this.FindControl<ComboBox>("SpecialKeyComboBox");
                        if (specialKeyComboBox != null)
                        {
                            SetComboBoxSelection(specialKeyComboBox, buttonConfig.Key ?? "");
                        }
                    }
                    else
                    {
                        // Set the key value for other actions
                        KeyValueTextBox.Text = buttonConfig.Key ?? "";
                        Console.WriteLine($"Set text box to: '{KeyValueTextBox.Text}'");
                    }
                    
                    // Enable/disable text box based on action
                    bool isEnabled = buttonConfig.Action != "None" && buttonConfig.Action != "Key combo" && buttonConfig.Action != "Special Key";
                    KeyValueTextBox.IsEnabled = isEnabled;
                    Console.WriteLine($"TextBox enabled: {isEnabled}");
                }
                else
                {
                    Console.WriteLine("Button not found, using defaults");
                    // Default values if no configuration exists
                    ActionTypeComboBox.SelectedIndex = 4; // None
                    KeyValueTextBox.Text = "";
                    KeyValueTextBox.IsEnabled = false; // Disabled for "None"
                }
            }
            else
            {
                Console.WriteLine("No viewmodel or layer, using defaults");
                // Default values if no layer is available
                ActionTypeComboBox.SelectedIndex = 4; // None
                KeyValueTextBox.Text = "";
                KeyValueTextBox.IsEnabled = false; // Disabled for "None"
            }
        }

        private int GetActionIndex(string action)
        {
            return action switch
            {
                "Type Text" => 0,
                "Special Key" => 1,
                "Key combo" => 2,
                "Layer Switch" => 3,
                "None" => 4,
                _ => 0
            };
        }

        private void SaveButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            // Get the selected action type
            string actionType = GetSelectedActionType();
            string keyValue = KeyValueTextBox.Text?.Trim() ?? "";

            Console.WriteLine($"SaveButton_Click - ActionType: '{actionType}', KeyValue: '{keyValue}'");

            // Handle Special Key selection
            if (actionType == "Special Key")
            {
                var specialKeyComboBox = this.FindControl<ComboBox>("SpecialKeyComboBox");
                if (specialKeyComboBox?.SelectedItem is ComboBoxItem specialKeyItem)
                {
                    keyValue = specialKeyItem.Content?.ToString() ?? "";
                    actionType = "Special Key"; // Keep the action type as Special Key
                }
            }

            // Store the configured values
            ConfiguredAction = actionType;
            ConfiguredKey = keyValue;

            // If it's a key combo, collect all selected keys
            if (actionType == "Key combo")
            {
                ConfiguredKeyCombo.Clear();
                
                // Get the first key
                var firstComboBox = this.FindControl<ComboBox>("FirstKeyComboBox");
                if (firstComboBox?.SelectedItem is ComboBoxItem firstItem)
                {
                    ConfiguredKeyCombo.Add(firstItem.Content?.ToString() ?? "");
                }
                
                // Get all dynamically added keys
                var container = this.FindControl<StackPanel>("KeyComboContainer");
                if (container != null)
                {
                    // Get all ComboBoxes in the container (excluding the first one)
                    foreach (var child in container.Children)
                    {
                        if (child is Border border && border.Child is ComboBox comboBox && comboBox.Name != "FirstKeyComboBox")
                        {
                            if (comboBox.SelectedItem is ComboBoxItem item)
                            {
                                ConfiguredKeyCombo.Add(item.Content?.ToString() ?? "");
                            }
                        }
                    }
                }
                
                // Convert key combo to string format (e.g., "Ctrl+C")
                keyValue = string.Join("+", ConfiguredKeyCombo.Where(k => !string.IsNullOrEmpty(k)));
                ConfiguredKey = keyValue;
            }

            Console.WriteLine($"Stored values - ConfiguredAction: '{ConfiguredAction}', ConfiguredKey: '{ConfiguredKey}'");

            // Show debug info in the title
            this.Title = $"Saving: {actionType} - {keyValue}";

            // Close the window and return true
            this.Close(true);
        }

        private void CancelButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            // Close the window and return false
            this.Close(false);
        }

        public string GetConfiguredAction()
        {
            return ConfiguredAction;
        }

        public string GetConfiguredKey()
        {
            return ConfiguredKey;
        }

        private string GetSelectedActionType()
        {
            if (ActionTypeComboBox.SelectedItem != null)
            {
                // Get the Content property of the ComboBoxItem
                if (ActionTypeComboBox.SelectedItem is ComboBoxItem item)
                {
                    string action = item.Content?.ToString() ?? "None";
                    Console.WriteLine($"Selected action type: '{action}'");
                    return action;
                }
                string fallbackAction = ActionTypeComboBox.SelectedItem.ToString() ?? "None";
                Console.WriteLine($"Selected action type: '{fallbackAction}'");
                return fallbackAction;
            }
            Console.WriteLine("No item selected, returning None");
            return "None";
        }

        private void ActionTypeComboBox_SelectionChanged(object? sender, Avalonia.Controls.SelectionChangedEventArgs e)
        {
            // Only handle selection changes after the window is fully loaded
            if (!this.IsInitialized)
            {
                System.Diagnostics.Debug.WriteLine("Window not yet initialized, skipping selection change");
                return;
            }

            var comboBox = sender as ComboBox;
            if (comboBox?.SelectedItem != null)
            {
                // Get the Content property of the ComboBoxItem (same logic as GetSelectedActionType)
                string selectedAction = "None";
                if (comboBox.SelectedItem is ComboBoxItem item)
                {
                    selectedAction = item.Content?.ToString() ?? "None";
                }
                else
                {
                    selectedAction = comboBox.SelectedItem.ToString() ?? "None";
                }
                
                // Show/hide appropriate panels based on action type
                var keyValuePanel = this.FindControl<StackPanel>("KeyValuePanel");
                var specialKeyPanel = this.FindControl<StackPanel>("SpecialKeyPanel");
                var keyComboPanel = this.FindControl<StackPanel>("KeyComboPanel");
                
                if (keyValuePanel != null && specialKeyPanel != null && keyComboPanel != null)
                {
                    switch (selectedAction)
                    {
                        case "Type Text":
                            keyValuePanel.IsVisible = true;
                            specialKeyPanel.IsVisible = false;
                            keyComboPanel.IsVisible = false;
                            break;
                        case "Special Key":
                            keyValuePanel.IsVisible = false;
                            specialKeyPanel.IsVisible = true;
                            keyComboPanel.IsVisible = false;
                            break;
                        case "Key combo":
                            keyValuePanel.IsVisible = false;
                            specialKeyPanel.IsVisible = false;
                            keyComboPanel.IsVisible = true;
                            break;
                        default:
                            keyValuePanel.IsVisible = false;
                            specialKeyPanel.IsVisible = false;
                            keyComboPanel.IsVisible = false;
                            break;
                    }
                }
                
                bool isEnabled = selectedAction != "None";
                
                try
                {
                    var textBox = this.FindControl<TextBox>("KeyValueTextBox");
                    if (textBox != null)
                    {
                        textBox.IsEnabled = isEnabled;
                        if (!isEnabled)
                        {
                            textBox.Text = ""; // Clear text when disabled
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error finding TextBox: {ex.Message}");
                    // Fallback: try to find it after a short delay
                    Dispatcher.UIThread.Post(() =>
                    {
                        try
                        {
                            var textBox = this.FindControl<TextBox>("KeyValueTextBox");
                            if (textBox != null)
                            {
                                textBox.IsEnabled = isEnabled;
                                if (!isEnabled)
                                {
                                    textBox.Text = "";
                                }
                            }
                        }
                        catch (Exception ex2)
                        {
                            System.Diagnostics.Debug.WriteLine($"Fallback also failed: {ex2.Message}");
                        }
                    }, DispatcherPriority.Loaded);
                }
                
                System.Diagnostics.Debug.WriteLine($"Action changed to: {selectedAction}, TextBox enabled: {isEnabled}");
            }
        }

        private void LoadKeyCombo(string keyComboString)
        {
            if (string.IsNullOrEmpty(keyComboString))
                return;

            // Parse the key combo string (e.g., "Ctrl+C" -> ["Ctrl", "C"])
            var keys = keyComboString.Split('+').Select(k => k.Trim()).Where(k => !string.IsNullOrEmpty(k)).ToList();
            
            if (keys.Count == 0)
                return;

            // Set the first key
            var firstComboBox = this.FindControl<ComboBox>("FirstKeyComboBox");
            if (firstComboBox != null && keys.Count > 0)
            {
                SetComboBoxSelection(firstComboBox, keys[0]);
            }

            // Add additional keys if needed (limit to MAX_KEY_COMBO_COUNT)
            for (int i = 1; i < Math.Min(keys.Count, MAX_KEY_COMBO_COUNT); i++)
            {
                AddKeyButton_Click(null!, null!);
                
                // Set the selection for the newly added combo box
                var container = this.FindControl<StackPanel>("KeyComboContainer");
                if (container != null && container.Children.Count > i)
                {
                    if (container.Children[i] is Border border && border.Child is ComboBox comboBox)
                    {
                        SetComboBoxSelection(comboBox, keys[i]);
                    }
                }
            }
            
            // Update button states after loading
            UpdateButtonStates();
        }

        private void SetComboBoxSelection(ComboBox comboBox, string key)
        {
            for (int i = 0; i < comboBox.Items.Count; i++)
            {
                if (comboBox.Items[i] is ComboBoxItem item && item.Content?.ToString() == key)
                {
                    comboBox.SelectedIndex = i;
                    break;
                }
            }
        }

        private void AddKeyButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            // Check if we've reached the maximum limit
            if (_keyComboCount >= MAX_KEY_COMBO_COUNT)
            {
                Console.WriteLine($"Maximum key combo limit reached ({MAX_KEY_COMBO_COUNT})");
                return;
            }

            // Add a new key dropdown
            var container = this.FindControl<StackPanel>("KeyComboContainer");
            if (container != null)
            {
                // Create new key dropdown
                var newKeyComboBox = new ComboBox
                {
                    Name = $"KeyComboBox_{_keyComboCount}",
                    Width = 120,
                    Height = 25,
                    FontSize = 12,
                    Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(64, 64, 64)), // #404040
                    Foreground = Avalonia.Media.Brushes.White,
                    BorderBrush = Avalonia.Media.Brushes.Transparent,
                    BorderThickness = new Avalonia.Thickness(0)
                };

                // Add all the key options
                string[] keyOptions = { "Ctrl", "Alt", "Shift", "Win", "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z", "Space", "Enter", "Tab", "Esc", "Backspace", "Delete", "Home", "End", "Page Up", "Page Down", "Arrow Up", "Arrow Down", "Arrow Left", "Arrow Right", "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12", "Play/Pause", "Next Track", "Previous Track", "Stop", "Volume Up", "Volume Down", "Mute", "Left Mouse", "Right Mouse", "Middle Mouse" };
                
                foreach (string key in keyOptions)
                {
                    newKeyComboBox.Items.Add(new ComboBoxItem { Content = key });
                }

                // Create border for the new dropdown
                var newKeyBorder = new Border
                {
                    BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(112, 112, 112)), // #707070
                    BorderThickness = new Avalonia.Thickness(2),
                    CornerRadius = new Avalonia.CornerRadius(5),
                    Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(64, 64, 64)), // #404040
                    Child = newKeyComboBox,
                    Margin = new Avalonia.Thickness(0, 0, 5, 0)
                };

                // Add the new key dropdown
                container.Children.Add(newKeyBorder);

                _keyComboCount++;
                
                // Update button states
                UpdateButtonStates();
            }
        }

        private void RemoveKeyButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            // Can't remove the first key box
            if (_keyComboCount <= 1)
            {
                Console.WriteLine("Cannot remove the first key box");
                return;
            }

            var container = this.FindControl<StackPanel>("KeyComboContainer");
            if (container != null && container.Children.Count > 1)
            {
                // Remove the last added key box (LIFO - Last In, First Out)
                container.Children.RemoveAt(container.Children.Count - 1);
                _keyComboCount--;
                
                // Update button states
                UpdateButtonStates();
            }
        }

        private void UpdateButtonStates()
        {
            var addButton = this.FindControl<Button>("AddKeyButton");
            var removeButton = this.FindControl<Button>("RemoveKeyButton");
            
            if (addButton != null)
            {
                addButton.IsEnabled = _keyComboCount < MAX_KEY_COMBO_COUNT;
            }
            
            if (removeButton != null)
            {
                removeButton.IsEnabled = _keyComboCount > 1; // Can't remove the first one
            }
        }
    }
}
