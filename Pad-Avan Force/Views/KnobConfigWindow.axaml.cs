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
        
        // Key combo tracking for Press only
        private int _pressKeyComboCount = 1;
        private const int MAX_KEY_COMBO_COUNT = 3;
        
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
            
            // Show the active layer in the config window title (so you notice immediately)
            this.Title = $"Knob {knobLetter} • {_viewModel?.CurrentLayer?.Name ?? "No layer"}";
            
            UpdateKnobNumber(knobLetter);
            
            // Load configuration after window is fully initialized and rendered
            // Use a longer delay to ensure PlatformImpl is ready
            this.Loaded += async (s, e) => 
            {
                // Wait for window to be fully rendered (PlatformImpl needs to be ready)
                await Task.Delay(300);
                _isLoadingConfiguration = true;
                try
                {
                    await LoadCurrentConfiguration();
                    // Give it a moment to settle after loading
                    await Task.Delay(200);
                }
                finally
                {
                    _isLoadingConfiguration = false;
                }
            };
        }

        private void UpdateKnobNumber(string knobLetter)
        {
            // Find the knob number text block and update it
            var knobNumberText = this.FindControl<TextBlock>("KnobNumberText");
            if (knobNumberText != null)
            {
                knobNumberText.Text = $"Knob {knobLetter}";
            }
        }

        private async Task LoadCurrentConfiguration()
        {
            Console.WriteLine($"LoadCurrentConfiguration called for knob {_knobLetter}");
            Console.WriteLine($"ViewModel is null: {_viewModel == null}");
            Console.WriteLine($"CurrentLayer is null: {_viewModel?.CurrentLayer == null}");
            
            if (_viewModel?.CurrentLayer != null)
            {
                Console.WriteLine($"Current layer name: {_viewModel.CurrentLayer.Name}");
                Console.WriteLine($"Current layer ID: {_viewModel.CurrentLayer.Id}");
            }
            
            // Load current configuration from the passed view model
            if (_viewModel?.CurrentLayer != null)
            {
                Console.WriteLine($"Looking for knob: {_knobLetter}");
                Console.WriteLine($"Available knobs: {string.Join(", ", _viewModel.CurrentLayer.Knobs.Keys)}");
                
                // Debug: Show all knob configurations in the current layer
                foreach (var kvp in _viewModel.CurrentLayer.Knobs)
                {
                    Console.WriteLine($"  Layer {_viewModel.CurrentLayer.Name} - Knob {kvp.Key}: CCW='{kvp.Value.CcwAction}', CW='{kvp.Value.CwAction}', Press='{kvp.Value.PressAction}'");
                }
                
                if (_viewModel.CurrentLayer.Knobs.TryGetValue(_knobLetter, out var knobConfig))
                {
                    Console.WriteLine($"Found knob config: CCW='{knobConfig.CcwAction}', CW='{knobConfig.CwAction}', Press='{knobConfig.PressAction}'");
                    
                    // Load CCW and CW configurations - simple synchronous approach (like ButtonConfigWindow)
                    // These are called from within Loaded event, so UI should be ready
                    var ccwComboBox = this.FindControl<ComboBox>("CcwActionComboBox");
                    if (ccwComboBox != null && ccwComboBox.Items != null && ccwComboBox.Items.Count > 0)
                    {
                        int ccwIndex = GetActionIndex(knobConfig.CcwAction);
                        if (ccwIndex >= 0 && ccwIndex < ccwComboBox.Items.Count)
                        {
                            Console.WriteLine($"Setting CCW ComboBox to index {ccwIndex} for action '{knobConfig.CcwAction}'");
                            ccwComboBox.SelectedIndex = ccwIndex;
                            
                            // Verify it stuck - retry if needed
                            await Task.Delay(50);
                            if (ccwComboBox.SelectedIndex != ccwIndex)
                            {
                                Console.WriteLine($"CCW selection didn't stick (got {ccwComboBox.SelectedIndex}), retrying...");
                                ccwComboBox.SelectedIndex = ccwIndex;
                                await Task.Delay(50);
                            }
                            
                            Console.WriteLine($"CCW ComboBox SelectedIndex is now: {ccwComboBox.SelectedIndex} (expected: {ccwIndex})");
                        }
                        else
                        {
                            Console.WriteLine($"Invalid CCW index {ccwIndex} (ComboBox has {ccwComboBox.Items.Count} items)");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"CCW ComboBox not found or not ready (Items: {ccwComboBox?.Items?.Count ?? 0})");
                    }
                    
                    // Load CW configuration
                    var cwComboBox = this.FindControl<ComboBox>("CwActionComboBox");
                    if (cwComboBox != null && cwComboBox.Items != null && cwComboBox.Items.Count > 0)
                    {
                        int cwIndex = GetActionIndex(knobConfig.CwAction);
                        if (cwIndex >= 0 && cwIndex < cwComboBox.Items.Count)
                        {
                            Console.WriteLine($"Setting CW ComboBox to index {cwIndex} for action '{knobConfig.CwAction}'");
                            cwComboBox.SelectedIndex = cwIndex;
                            
                            // Verify it stuck - retry if needed
                            await Task.Delay(50);
                            if (cwComboBox.SelectedIndex != cwIndex)
                            {
                                Console.WriteLine($"CW selection didn't stick (got {cwComboBox.SelectedIndex}), retrying...");
                                cwComboBox.SelectedIndex = cwIndex;
                                await Task.Delay(50);
                            }
                            
                            Console.WriteLine($"CW ComboBox SelectedIndex is now: {cwComboBox.SelectedIndex} (expected: {cwIndex})");
                        }
                        else
                        {
                            Console.WriteLine($"Invalid CW index {cwIndex} (ComboBox has {cwComboBox.Items.Count} items)");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"CW ComboBox not found or not ready (Items: {cwComboBox?.Items?.Count ?? 0})");
                    }
                    
                    // Load Press configuration - use async method (this one works)
                    LoadActionConfiguration("Press", knobConfig.PressAction, knobConfig.PressKey ?? "");
                }
                else
                {
                    Console.WriteLine("Knob not found, using defaults");
                    SetDefaultSelections();
                }
            }
            else
            {
                Console.WriteLine("No viewmodel or layer, using defaults");
                SetDefaultSelections();
            }
        }

        private async void LoadActionConfiguration(string actionType, string action, string key)
        {
            // Use Dispatcher to ensure UI is fully ready
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var comboBox = this.FindControl<ComboBox>($"{actionType}ActionComboBox");
                if (comboBox == null) 
                {
                    Console.WriteLine($"Could not find ComboBox: {actionType}ActionComboBox");
                    return;
                }
                
                // Wait for ComboBox to be fully initialized and rendered
                // Check if ComboBox is ready (has items and is initialized)
                int retries = 0;
                while (retries < 10 && (comboBox.Items == null || comboBox.Items.Count == 0))
                {
                    await Task.Delay(50);
                    retries++;
                }
                
                if (comboBox.Items == null || comboBox.Items.Count == 0)
                {
                    Console.WriteLine($"Warning: {actionType} ComboBox still has no items after {retries * 50}ms");
                }
                
                // Wait a bit more to ensure ComboBox is fully ready
                await Task.Delay(100);
                
                int index = actionType == "Press" 
                    ? GetPressActionIndex(action) 
                    : GetActionIndex(action);
                
                Console.WriteLine($"Loading {actionType} action: '{action}' at index {index}");
                
                // Verify ComboBox has items before trying to set selection
                if (comboBox.Items == null || comboBox.Items.Count == 0)
                {
                    Console.WriteLine($"Warning: {actionType} ComboBox has no items yet, waiting more...");
                    await Task.Delay(200);
                }
                
                // Ensure we have items now
                if (comboBox.Items == null || comboBox.Items.Count == 0)
                {
                    Console.WriteLine($"Error: {actionType} ComboBox still has no items after wait");
                    return;
                }
                
                // Verify index is valid
                if (index < 0 || index >= comboBox.Items.Count)
                {
                    Console.WriteLine($"Error: Invalid index {index} for {actionType} ComboBox (has {comboBox.Items.Count} items)");
                    return;
                }
                
                // Set selection - use SelectedItem which is more reliable
                try
                {
                    var item = comboBox.Items[index];
                    
                    // Ensure we're still in loading mode to prevent SelectionChanged from interfering
                    if (!_isLoadingConfiguration)
                    {
                        Console.WriteLine($"Warning: Not in loading mode for {actionType}, setting flag");
                        _isLoadingConfiguration = true;
                    }
                    
                    // Set the selection
                    comboBox.SelectedItem = item;
                    Console.WriteLine($"Set {actionType} ComboBox SelectedItem to index {index}");
                    
                    // Verify it was set correctly after a brief moment
                    await Task.Delay(100);
                    
                    // Double-check and retry if needed
                    if (comboBox.SelectedIndex != index)
                    {
                        Console.WriteLine($"Warning: {actionType} ComboBox selection mismatch. Expected {index}, got {comboBox.SelectedIndex}. Retrying...");
                        
                        // Try SelectedIndex as fallback
                        comboBox.SelectedIndex = index;
                        await Task.Delay(100);
                        
                        // Final check
                        if (comboBox.SelectedIndex != index)
                        {
                            Console.WriteLine($"Error: {actionType} ComboBox selection still wrong after retry. Expected {index}, got {comboBox.SelectedIndex}");
                        }
                        else
                        {
                            Console.WriteLine($"After retry: {actionType} ComboBox SelectedIndex is now {comboBox.SelectedIndex}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"{actionType} ComboBox selection set successfully to index {index}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error setting {actionType} ComboBox selection: {ex.Message}");
                    // Fallback to SelectedIndex
                    try
                    {
                        comboBox.SelectedIndex = index;
                        Console.WriteLine($"Fallback: Set {actionType} ComboBox SelectedIndex to {index}");
                    }
                    catch (Exception ex2)
                    {
                        Console.WriteLine($"Fallback also failed: {ex2.Message}");
                    }
                }
                
                // Load panels after selection is set
                LoadActionConfigurationPanels(actionType, action, key);
            });
        }
        
        private void LoadActionConfigurationPanels(string actionType, string action, string key)
        {
            // For Press action, we need to show/hide panels and load the key value
            if (actionType == "Press")
            {
                // Manually set panel visibility based on action type
                var keyValuePanel = this.FindControl<StackPanel>($"{actionType}KeyValuePanel");
                var specialKeyPanel = this.FindControl<StackPanel>($"{actionType}SpecialKeyPanel");
                var keyComboPanel = this.FindControl<StackPanel>($"{actionType}KeyComboPanel");
                
                if (keyValuePanel != null && specialKeyPanel != null && keyComboPanel != null)
                {
                    switch (action)
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
                
                // Load the key/value based on action type
                if (action == "Key combo")
                {
                    LoadKeyCombo(actionType, key);
                }
                else if (action == "Special Key")
                {
                    var specialKeyComboBox = this.FindControl<ComboBox>($"{actionType}SpecialKeyComboBox");
                    if (specialKeyComboBox != null)
                    {
                        SetComboBoxSelection(specialKeyComboBox, key);
                    }
                }
                else if (action == "Type Text")
                {
                    var textBox = this.FindControl<TextBox>($"{actionType}KeyValueTextBox");
                    if (textBox != null)
                    {
                        textBox.Text = key;
                        Console.WriteLine($"Loaded Type Text value: '{key}'");
                    }
                    else
                    {
                        Console.WriteLine($"Could not find TextBox: {actionType}KeyValueTextBox");
                    }
                }
            }
            // For CCW and CW, the selection is already set in the outer method
            else
            {
                Console.WriteLine($"{actionType} action configuration loaded: '{action}'");
            }
        }

        private void LoadKeyCombo(string actionType, string keyComboString)
        {
            if (string.IsNullOrEmpty(keyComboString))
                return;

            // Parse the key combo string (e.g., "Ctrl+C" -> ["Ctrl", "C"])
            var keys = keyComboString.Split('+').Select(k => k.Trim()).Where(k => !string.IsNullOrEmpty(k)).ToList();
            
            if (keys.Count == 0)
                return;

            // Reset the counter for Press action type
            int keyCount = Math.Min(keys.Count, MAX_KEY_COMBO_COUNT);
            if (actionType == "Press")
            {
                _pressKeyComboCount = 1; // First key already exists
            }

            // Set the first key
            var firstComboBox = this.FindControl<ComboBox>($"{actionType}FirstKeyComboBox");
            if (firstComboBox != null && keys.Count > 0)
            {
                SetComboBoxSelection(firstComboBox, keys[0]);
            }

            // Add additional keys if needed (limit to MAX_KEY_COMBO_COUNT)
            for (int i = 1; i < keyCount; i++)
            {
                // Add key programmatically
                AddKeyComboItem(actionType);
                
                // Set the selection for the newly added combo box
                var container = this.FindControl<StackPanel>($"{actionType}KeyComboContainer");
                if (container != null && container.Children.Count > i)
                {
                    if (container.Children[i] is Border border && border.Child is ComboBox comboBox)
                    {
                        SetComboBoxSelection(comboBox, keys[i]);
                    }
                }
            }
            
            // Update button states after loading
            UpdateButtonStates(actionType);
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

        private void SetComboBoxSelection(string comboBoxName, string action)
        {
            var comboBox = this.FindControl<ComboBox>(comboBoxName);
            if (comboBox != null)
            {
                int index = comboBoxName == "PressActionComboBox" 
                    ? GetPressActionIndex(action) 
                    : GetActionIndex(action);
                Console.WriteLine($"Setting {comboBoxName} to '{action}' (index {index})");
                comboBox.SelectedIndex = index;
            }
        }

        private int GetActionIndex(string action)
        {
            return action switch
            {
                "Increase Volume" => 0,
                "Decrease Volume" => 1,
                "Scroll Up" => 2,
                "Scroll Down" => 3,
                "None" => 4,  // CCW/CW ComboBox has 5 items (0-4)
                _ => 0
            };
        }

        private int GetPressActionIndex(string action)
        {
            return action switch
            {
                "Type Text" => 0,
                "Special Key" => 1,
                "Key combo" => 2,
                "Layer Switch" => 3,
                "None" => 4,  // Press Action ComboBox has 5 items (0-4)
                _ => 0
            };
        }

        private void SetDefaultSelections()
        {
            // Set defaults only if no configuration exists
            // Use Dispatcher to ensure UI is ready
            Dispatcher.UIThread.Post(() =>
            {
                var ccwComboBox = this.FindControl<ComboBox>("CcwActionComboBox");
                var cwComboBox = this.FindControl<ComboBox>("CwActionComboBox");
                var pressComboBox = this.FindControl<ComboBox>("PressActionComboBox");

                if (ccwComboBox != null && ccwComboBox.SelectedIndex == -1) 
                    ccwComboBox.SelectedIndex = 4; // None (CCW has 5 items)
                if (cwComboBox != null && cwComboBox.SelectedIndex == -1) 
                    cwComboBox.SelectedIndex = 4; // None (CW has 5 items)
                if (pressComboBox != null && pressComboBox.SelectedIndex == -1) 
                    pressComboBox.SelectedIndex = 4; // None (Press has 5 items)
            }, DispatcherPriority.Loaded);
        }

        private void SaveButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            // Get the selected actions and keys
            ConfiguredCcwAction = GetSelectedAction("CcwActionComboBox");
            ConfiguredCwAction = GetSelectedAction("CwActionComboBox");
            ConfiguredPressAction = GetSelectedAction("PressActionComboBox");
            
            // Get the keys for each action
            ConfiguredCcwKey = GetKeyForAction("CCW", ConfiguredCcwAction);
            ConfiguredCwKey = GetKeyForAction("CW", ConfiguredCwAction);
            ConfiguredPressKey = GetKeyForAction("Press", ConfiguredPressAction);

            Console.WriteLine($"Knob {_knobLetter} configured - CCW: {ConfiguredCcwAction} ({ConfiguredCcwKey}), CW: {ConfiguredCwAction} ({ConfiguredCwKey}), Press: {ConfiguredPressAction} ({ConfiguredPressKey})");

            // Close the window and return true
            this.Close(true);
        }

        private string GetKeyForAction(string actionType, string action)
        {
            // CCW and CW don't support Type Text, Special Key, or Key combo
            if (actionType != "Press")
            {
                return "";
            }

            if (action == "Key combo")
            {
                // Collect all selected keys from the key combo container
                var container = this.FindControl<StackPanel>($"{actionType}KeyComboContainer");
                var keyCombo = new List<string>();
                
                // Get the first key
                var firstComboBox = this.FindControl<ComboBox>($"{actionType}FirstKeyComboBox");
                if (firstComboBox?.SelectedItem is ComboBoxItem firstItem)
                {
                    keyCombo.Add(firstItem.Content?.ToString() ?? "");
                }
                
                // Get all dynamically added keys
                if (container != null)
                {
                    foreach (var child in container.Children)
                    {
                        if (child is Border border && border.Child is ComboBox comboBox && comboBox.Name != $"{actionType}FirstKeyComboBox")
                        {
                            if (comboBox.SelectedItem is ComboBoxItem item)
                            {
                                keyCombo.Add(item.Content?.ToString() ?? "");
                            }
                        }
                    }
                }
                
                return string.Join("+", keyCombo.Where(k => !string.IsNullOrEmpty(k)));
            }
            else if (action == "Special Key")
            {
                var specialKeyComboBox = this.FindControl<ComboBox>($"{actionType}SpecialKeyComboBox");
                if (specialKeyComboBox?.SelectedItem is ComboBoxItem specialKeyItem)
                {
                    return specialKeyItem.Content?.ToString() ?? "";
                }
            }
            else if (action == "Type Text")
            {
                var textBox = this.FindControl<TextBox>($"{actionType}KeyValueTextBox");
                return textBox?.Text?.Trim() ?? "";
            }
            
            return "";
        }

        private void CancelButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            // Close the window and return false
            this.Close(false);
        }

        private string GetSelectedAction(string comboBoxName)
        {
            var comboBox = this.FindControl<ComboBox>(comboBoxName);
            if (comboBox?.SelectedItem != null)
            {
                // Get the Content property of the ComboBoxItem
                if (comboBox.SelectedItem is ComboBoxItem item)
                {
                    return item.Content?.ToString() ?? "None";
                }
                return comboBox.SelectedItem.ToString() ?? "None";
            }
            return "None";
        }

        // SelectionChanged Handlers
        private void PressActionComboBox_SelectionChanged(object? sender, Avalonia.Controls.SelectionChangedEventArgs e)
        {
            HandleActionSelectionChanged("Press", sender as ComboBox);
        }

        private void HandleActionSelectionChanged(string actionType, ComboBox? comboBox)
        {
            // Ignore SelectionChanged events during configuration loading
            if (_isLoadingConfiguration)
            {
                Console.WriteLine($"Ignoring SelectionChanged for {actionType} during configuration load");
                return;
            }
            
            if (!this.IsInitialized || comboBox == null)
                return;

            string selectedAction = "None";
            if (comboBox.SelectedItem is ComboBoxItem item)
            {
                selectedAction = item.Content?.ToString() ?? "None";
            }
            else if (comboBox.SelectedItem != null)
            {
                selectedAction = comboBox.SelectedItem.ToString() ?? "None";
            }

            // Show/hide appropriate panels based on action type
            var keyValuePanel = this.FindControl<StackPanel>($"{actionType}KeyValuePanel");
            var specialKeyPanel = this.FindControl<StackPanel>($"{actionType}SpecialKeyPanel");
            var keyComboPanel = this.FindControl<StackPanel>($"{actionType}KeyComboPanel");
            
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
        }

        // Key Combo Add/Remove Handlers for Press
        private void PressAddKeyButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            AddKeyComboItem("Press");
        }

        private void PressRemoveKeyButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            RemoveKeyComboItem("Press");
        }

        private void AddKeyComboItem(string actionType)
        {
            // Only Press action supports key combos
            if (actionType != "Press")
            {
                return;
            }

            int currentCount = _pressKeyComboCount;

            if (currentCount >= MAX_KEY_COMBO_COUNT)
            {
                Console.WriteLine($"Maximum key combo limit reached ({MAX_KEY_COMBO_COUNT}) for {actionType}");
                return;
            }

            // Add a new key dropdown
            var container = this.FindControl<StackPanel>($"{actionType}KeyComboContainer");
            if (container != null)
            {
                // Create new key dropdown
                var newKeyComboBox = new ComboBox
                {
                    Name = $"{actionType}KeyComboBox_{currentCount}",
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

                // Update count
                _pressKeyComboCount++;
                
                // Update button states
                UpdateButtonStates(actionType);
            }
        }

        private void RemoveKeyComboItem(string actionType)
        {
            // Only Press action supports key combos
            if (actionType != "Press")
            {
                return;
            }

            // Can't remove the first key box
            if (_pressKeyComboCount <= 1)
            {
                Console.WriteLine($"Cannot remove the first key box for {actionType}");
                return;
            }

            var container = this.FindControl<StackPanel>($"{actionType}KeyComboContainer");
            if (container != null && container.Children.Count > 1)
            {
                // Remove the last added key box (LIFO - Last In, First Out)
                container.Children.RemoveAt(container.Children.Count - 1);
                
                // Update count
                _pressKeyComboCount--;
                
                // Update button states
                UpdateButtonStates(actionType);
            }
        }

        private void UpdateButtonStates(string actionType)
        {
            // Only Press action supports key combos
            if (actionType != "Press")
            {
                return;
            }

            var addButton = this.FindControl<Button>($"{actionType}AddKeyButton");
            var removeButton = this.FindControl<Button>($"{actionType}RemoveKeyButton");
            
            if (addButton != null)
            {
                addButton.IsEnabled = _pressKeyComboCount < MAX_KEY_COMBO_COUNT;
            }
            
            if (removeButton != null)
            {
                removeButton.IsEnabled = _pressKeyComboCount > 1; // Can't remove the first one
            }
        }

        public string GetConfiguredCcwAction()
        {
            return ConfiguredCcwAction;
        }

        public string GetConfiguredCwAction()
        {
            return ConfiguredCwAction;
        }

        public string GetConfiguredPressAction()
        {
            return ConfiguredPressAction;
        }

        public string GetConfiguredCcwKey()
        {
            return ConfiguredCcwKey;
        }

        public string GetConfiguredCwKey()
        {
            return ConfiguredCwKey;
        }

        public string GetConfiguredPressKey()
        {
            return ConfiguredPressKey;
        }
    }
}
