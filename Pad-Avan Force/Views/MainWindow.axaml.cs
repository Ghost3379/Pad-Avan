using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PadAwan_Force.ViewModels;
using PadAwan_Force.Models;
using Avalonia;
using Avalonia.Threading;

namespace PadAwan_Force.Views
{
    public partial class MainWindow : Window
    {
        private MainWindowViewModel? _viewModel;

        public MainWindow()
        {
            System.Console.WriteLine("MAIN WINDOW CONSTRUCTOR CALLED!");
            InitializeComponent();
            
            // DataContext is set AFTER constructor, so we need to handle it in Loaded event
            System.Diagnostics.Debug.WriteLine($"DataContext in constructor: {DataContext?.GetType().Name ?? "null"}");
            
            // Add Loaded event handler to get ViewModel after DataContext is set
            this.Loaded += MainWindow_Loaded;
            
            System.Console.WriteLine("MainWindow constructor completed");
        }

        private void MainWindow_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            System.Console.WriteLine("MainWindow Loaded event fired");
            
            // Get ViewModel from DataContext (set by App.axaml.cs)
            _viewModel = DataContext as MainWindowViewModel;
            System.Diagnostics.Debug.WriteLine($"DataContext in Loaded event: {DataContext?.GetType().Name ?? "null"}");
            System.Diagnostics.Debug.WriteLine($"MainWindow ViewModel Hash: {_viewModel?.GetHashCode()}");
            
            if (_viewModel != null)
            {
                // Force UI update after window is loaded
                _viewModel.ForceUIUpdate();
                System.Console.WriteLine("Forced UI update after window loaded");
            }
            else
            {
                System.Console.WriteLine("ERROR: ViewModel is still null in Loaded event!");
            }
        }

        private void Button1_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            System.Console.WriteLine("BUTTON 1 CLICKED!");
            if (_viewModel != null)
                _viewModel.DisplayText = "Button 1 clicked!";
            OpenButtonConfigWindow(1);
        }

        private void Button2_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            OpenButtonConfigWindow(2);
        }

        private void Button3_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            OpenButtonConfigWindow(3);
        }

        private void Button4_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            OpenButtonConfigWindow(4);
        }

        private void Button5_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            OpenButtonConfigWindow(5);
        }

        private void Button6_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            OpenButtonConfigWindow(6);
        }

        private async void OpenButtonConfigWindow(int buttonNumber)
        {
            // Console.WriteLine($"Opening config window for button {buttonNumber}");
            // Console.WriteLine($"ViewModel is null: {_viewModel == null}");
            // Console.WriteLine($"CurrentLayer is null: {_viewModel?.CurrentLayer == null}");
            
            if (_viewModel != null)
            {
                // Make 100% sure CurrentLayer matches the ComboBox selection
                var i = _viewModel.SelectedLayerIndex;
                System.Diagnostics.Debug.WriteLine($"Force-sync: SelectedLayerIndex={i}, Layers.Count={_viewModel.Layers.Count}");
                System.Diagnostics.Debug.WriteLine($"ViewModel Hash: {_viewModel.GetHashCode()}");
                
                if (i >= 0 && i < _viewModel.Layers.Count)
                {
                    _viewModel.CurrentLayer = _viewModel.Layers[i];
                    System.Diagnostics.Debug.WriteLine($"Force-synced CurrentLayer to: {_viewModel.CurrentLayer?.Name} (ID={_viewModel.CurrentLayer?.Id})");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"ERROR: Invalid SelectedLayerIndex {i}! Available layers: {_viewModel.Layers.Count}");
                    // Fallback: use the first layer
                    if (_viewModel.Layers.Count > 0)
                    {
                        _viewModel.CurrentLayer = _viewModel.Layers[0];
                        System.Diagnostics.Debug.WriteLine($"Fallback: Set CurrentLayer to first layer: {_viewModel.CurrentLayer?.Name}");
                    }
                }
                
                _viewModel.DisplayText = $"Opening config for button {buttonNumber}";
            }
            
            // Log breadcrumb when opening (helps see mismatches in console)
            System.Diagnostics.Debug.WriteLine($"Config open for Button {buttonNumber} on layer: {_viewModel?.CurrentLayer?.Name} (ID={_viewModel?.CurrentLayer?.Id})");
            System.Diagnostics.Debug.WriteLine($"SelectedLayerIndex: {_viewModel?.SelectedLayerIndex}");
            System.Diagnostics.Debug.WriteLine($"Layers count: {_viewModel?.Layers.Count}");
            
            // Debug: Show all layers and their button configs
            if (_viewModel != null)
            {
                for (int i = 0; i < _viewModel.Layers.Count; i++)
                {
                    var layer = _viewModel.Layers[i];
                    System.Diagnostics.Debug.WriteLine($"  Layer {i}: {layer.Name} (ID={layer.Id})");
                    if (layer.Buttons.TryGetValue(buttonNumber.ToString(), out var btnConfig))
                    {
                        System.Diagnostics.Debug.WriteLine($"    Button {buttonNumber}: Action='{btnConfig.Action}', Key='{btnConfig.Key}'");
                    }
                }
            }
            
            var configWindow = new ButtonConfigWindow(buttonNumber, _viewModel);
            System.Diagnostics.Debug.WriteLine($"ButtonConfigWindow created with ViewModel Hash: {_viewModel?.GetHashCode()}");
            var result = await configWindow.ShowDialog<bool>(this);
            
            // Console.WriteLine($"Dialog result: {result}");
            if (_viewModel != null)
                _viewModel.DisplayText = $"Dialog result: {result}";
            
            // If the user saved, update the button configuration
            if (result && _viewModel != null && _viewModel.CurrentLayer != null)
            {
                // Get the button key from the config window
                string buttonKey = configWindow.GetConfiguredKey();
                string buttonAction = configWindow.GetConfiguredAction();
                
                // Console.WriteLine($"Retrieved from config window - Action='{buttonAction}', Key='{buttonKey}'");
                if (_viewModel != null)
                    _viewModel.DisplayText = $"Retrieved: Action='{buttonAction}', Key='{buttonKey}'";
                
                // Update the current layer's button configuration
                string buttonId = buttonNumber.ToString();
                // Console.WriteLine($"Looking for button ID: {buttonId}");
                var availableButtons = _viewModel?.CurrentLayer?.Buttons.Keys.ToArray() ?? new string[0];
                // System.Diagnostics.Debug.WriteLine($"Available buttons: {string.Join(", ", availableButtons)}");
                if (_viewModel != null)
                    _viewModel.DisplayText = $"Looking for button {buttonId}, Available: {string.Join(", ", availableButtons)}";
                
                if (_viewModel?.CurrentLayer?.Buttons.ContainsKey(buttonId) == true)
                {
                    var oldConfig = _viewModel.CurrentLayer?.Buttons[buttonId];
                    // System.Diagnostics.Debug.WriteLine($"Old config - Action='{oldConfig?.Action}', Key='{oldConfig?.Key}'");
                    
                    System.Diagnostics.Debug.WriteLine($"=== BUTTON CONFIG UPDATE DEBUG ===");
                    System.Diagnostics.Debug.WriteLine($"Updating button {buttonId} in layer: {_viewModel.CurrentLayer?.Name}");
                    System.Diagnostics.Debug.WriteLine($"Layer ID: {_viewModel.CurrentLayer?.Id}");
                    System.Diagnostics.Debug.WriteLine($"Old config: Action='{oldConfig?.Action}', Key='{oldConfig?.Key}'");
                    System.Diagnostics.Debug.WriteLine($"New config: Action='{buttonAction}', Key='{buttonKey}'");
                    
                    // Debug: Show object hashes before update
                    System.Diagnostics.Debug.WriteLine($"Button config object hash before update: {oldConfig?.GetHashCode()}");
                    System.Diagnostics.Debug.WriteLine($"Layer object hash: {_viewModel.CurrentLayer?.GetHashCode()}");
                    
                    // Update the button configuration in the current layer
                    _viewModel.CurrentLayer!.Buttons[buttonId] = new Models.ButtonConfig
                    {
                        Action = buttonAction,
                        Key = buttonKey,
                        Enabled = buttonAction != "None",
                        HoldToRepeat = configWindow.ConfiguredHoldToRepeat
                    };
                    
                    // Debug: Show object hashes after update
                    var updatedConfig = _viewModel.CurrentLayer.Buttons[buttonId];
                    System.Diagnostics.Debug.WriteLine($"Button config object hash after update: {updatedConfig.GetHashCode()}");
                    System.Diagnostics.Debug.WriteLine($"Updated config: Action='{updatedConfig.Action}', Key='{updatedConfig.Key}'");
                    
                    var newConfig = _viewModel.CurrentLayer?.Buttons[buttonId];
                    // System.Diagnostics.Debug.WriteLine($"New config - Action='{newConfig?.Action}', Key='{newConfig?.Key}', Enabled={newConfig?.Enabled}");
                    
                    // Debug: Check what's in the layer after update
                    // System.Diagnostics.Debug.WriteLine($"Layer after update - Button {buttonId}: Action='{_viewModel.CurrentLayer?.Buttons[buttonId]?.Action}', Key='{_viewModel.CurrentLayer?.Buttons[buttonId]?.Key}'");
                    
                    string layerName = _viewModel.CurrentLayer?.Name ?? "Unknown";
                    // System.Diagnostics.Debug.WriteLine($"Updated button {buttonId} in layer {layerName}");
                    if (_viewModel != null)
                        _viewModel.DisplayText = $"Updated button {buttonId}: {buttonAction} - {buttonKey}";
                    
                    // Debug: Show all button configurations in the current layer after update
                    System.Diagnostics.Debug.WriteLine($"After button update - Layer {layerName}:");
                    var buttons = _viewModel?.CurrentLayer?.Buttons ?? new Dictionary<string, ButtonConfig>();
                    foreach (var kvp in buttons)
                    {
                        System.Diagnostics.Debug.WriteLine($"  Button {kvp.Key}: Action='{kvp.Value.Action}', Key='{kvp.Value.Key}', Enabled={kvp.Value.Enabled}");
                    }
                    
                    // Update the button display
                    // System.Diagnostics.Debug.WriteLine("Calling UpdateButtonTexts()");
                    if (_viewModel != null)
                    {
                        // System.Diagnostics.Debug.WriteLine($"Before UpdateButtonTexts - Button1: '{_viewModel.Button1Text}', Button2: '{_viewModel.Button2Text}'");
                        _viewModel.UpdateButtonTexts();
                        _viewModel.UpdateKnobTexts();
                        // System.Diagnostics.Debug.WriteLine($"After UpdateButtonTexts - Button1: '{_viewModel.Button1Text}', Button2: '{_viewModel.Button2Text}'");
                        
                        // Save the configuration
                        await _viewModel.SaveLayersAsync();
                        
                        // Force UI refresh on UI thread
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            _viewModel.ForceUIRefresh();
                        });
                        
                        // Try multiple UI refresh approaches
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            // Method 1: Force DataContext refresh
                            var currentDataContext = this.DataContext;
                            this.DataContext = null;
                            this.DataContext = currentDataContext;
                        });
                        
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            // Method 2: Force all buttons to refresh their content directly
                            // System.Diagnostics.Debug.WriteLine($"Forcing direct button content updates...");
                            
                            if (Button1 != null)
                            {
                                // System.Diagnostics.Debug.WriteLine($"Button1: '{Button1.Content}' -> '{_viewModel.Button1Text}'");
                                Button1.Content = _viewModel.Button1Text;
                            }
                            if (Button2 != null)
                            {
                                // System.Diagnostics.Debug.WriteLine($"Button2: '{Button2.Content}' -> '{_viewModel.Button2Text}'");
                                Button2.Content = _viewModel.Button2Text;
                            }
                            if (Button3 != null)
                            {
                                // System.Diagnostics.Debug.WriteLine($"Button3: '{Button3.Content}' -> '{_viewModel.Button3Text}'");
                                Button3.Content = _viewModel.Button3Text;
                            }
                            if (Button4 != null)
                            {
                                // System.Diagnostics.Debug.WriteLine($"Button4: '{Button4.Content}' -> '{_viewModel.Button4Text}'");
                                Button4.Content = _viewModel.Button4Text;
                            }
                            if (Button5 != null)
                            {
                                // System.Diagnostics.Debug.WriteLine($"Button5: '{Button5.Content}' -> '{_viewModel.Button5Text}'");
                                Button5.Content = _viewModel.Button5Text;
                            }
                            if (Button6 != null)
                            {
                                // System.Diagnostics.Debug.WriteLine($"Button6: '{Button6.Content}' -> '{_viewModel.Button6Text}'");
                                Button6.Content = _viewModel.Button6Text;
                            }
                        });
                        
                        // System.Diagnostics.Debug.WriteLine("Forced UI refresh completed");
                    }
                    
                    // Save the layers
                    // System.Diagnostics.Debug.WriteLine("Saving layers...");
                    if (_viewModel != null)
                        await _viewModel.SaveLayersAsync();
                    
                    // System.Diagnostics.Debug.WriteLine("Layers saved successfully");
                    
                    // Just save locally, don't send to device automatically
                    if (_viewModel != null)
                        _viewModel.DisplayText = $"Button {buttonNumber} configured: {buttonKey} - Saved locally. Press 'Send to Device' to transfer.";
                }
                else
                {
                    Console.WriteLine($"Button {buttonId} not found in current layer");
                    if (_viewModel != null)
                        _viewModel.DisplayText = $"Button {buttonId} not found!";
                }
            }
            else
            {
                Console.WriteLine($"Save cancelled or no viewmodel/layer available. Result: {result}");
                if (_viewModel == null) Console.WriteLine("ViewModel is null");
                if (_viewModel?.CurrentLayer == null) Console.WriteLine("CurrentLayer is null");
                if (_viewModel != null)
                    _viewModel.DisplayText = $"Save cancelled. Result: {result}, ViewModel: {_viewModel != null}, Layer: {_viewModel?.CurrentLayer != null}";
            }
        }

        private void KnobA_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            OpenKnobConfigWindow("A");
        }

        private void KnobB_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            OpenKnobConfigWindow("B");
        }

        private async void OpenKnobConfigWindow(string knobLetter)
        {
            // Console.WriteLine($"Opening knob config window for knob {knobLetter}");
            
            if (_viewModel != null)
            {
                // Make 100% sure CurrentLayer matches the ComboBox selection
                var i = _viewModel.SelectedLayerIndex;
                if (i >= 0 && i < _viewModel.Layers.Count)
                {
                    _viewModel.CurrentLayer = _viewModel.Layers[i];
                    Console.WriteLine($"Force-synced CurrentLayer to: {_viewModel.CurrentLayer?.Name} (ID={_viewModel.CurrentLayer?.Id})");
                }
            }
            
            // Log breadcrumb when opening (helps see mismatches in console)
            Console.WriteLine($"Knob config open for {knobLetter} on layer: {_viewModel?.CurrentLayer?.Name} (ID={_viewModel?.CurrentLayer?.Id})");
            
            var configWindow = new KnobConfigWindow(knobLetter, _viewModel);
            var result = await configWindow.ShowDialog<bool>(this);
            
            // Console.WriteLine($"Knob config dialog result: {result}");
            
            // If the user saved, update the knob configuration
            if (result && _viewModel != null && _viewModel.CurrentLayer != null)
            {
                // Get the configured actions and keys from the config window
                string ccwAction = configWindow.GetConfiguredCcwAction();
                string cwAction = configWindow.GetConfiguredCwAction();
                string pressAction = configWindow.GetConfiguredPressAction();
                string ccwKey = configWindow.GetConfiguredCcwKey();
                string cwKey = configWindow.GetConfiguredCwKey();
                string pressKey = configWindow.GetConfiguredPressKey();
                bool pressHoldToRepeat = configWindow.GetConfiguredPressHoldToRepeat();
                
                Console.WriteLine($"Retrieved knob config - CCW: {ccwAction} ({ccwKey}), CW: {cwAction} ({cwKey}), Press: {pressAction} ({pressKey}), HoldToRepeat: {pressHoldToRepeat}");
                Console.WriteLine($"PressKey value: '{pressKey}' (length: {pressKey?.Length ?? 0}, isNullOrEmpty: {string.IsNullOrEmpty(pressKey)})");
                
                // Update the current layer's knob configuration
                if (_viewModel.CurrentLayer?.Knobs.ContainsKey(knobLetter) == true)
                {
                    var newKnobConfig = new Models.KnobConfig
                    {
                        CcwAction = ccwAction,
                        CwAction = cwAction,
                        PressAction = pressAction,
                        CcwKey = string.IsNullOrEmpty(ccwKey) ? null : ccwKey,
                        CwKey = string.IsNullOrEmpty(cwKey) ? null : cwKey,
                        PressKey = string.IsNullOrEmpty(pressKey) ? null : pressKey,
                        PressHoldToRepeat = pressHoldToRepeat
                    };
                    
                    Console.WriteLine($"Saving knob config - PressAction: '{newKnobConfig.PressAction}', PressKey: '{newKnobConfig.PressKey ?? "NULL"}'");
                    
                    _viewModel.CurrentLayer!.Knobs[knobLetter] = newKnobConfig;
                    
                    string layerName = _viewModel.CurrentLayer?.Name ?? "Unknown";
                    Console.WriteLine($"Updated knob {knobLetter} in layer {layerName}");
                    _viewModel.DisplayText = $"Updated knob {knobLetter}: CCW={ccwAction}, CW={cwAction}, Press={pressAction}";
                    
                    // Debug: Show all knob configurations in the current layer after update
                    Console.WriteLine($"After knob update - Layer {layerName}:");
                    var knobs = _viewModel?.CurrentLayer?.Knobs ?? new Dictionary<string, KnobConfig>();
                    foreach (var kvp in knobs)
                    {
                        Console.WriteLine($"  Knob {kvp.Key}: CCW='{kvp.Value.CcwAction}', CW='{kvp.Value.CwAction}', Press='{kvp.Value.PressAction}'");
                    }
                    
                    // Update the knob display
                    if (_viewModel != null)
                    {
                        Console.WriteLine($"Before UpdateKnobTexts - KnobA: '{_viewModel.KnobAFunction}', KnobB: '{_viewModel.KnobBFunction}'");
                        _viewModel.UpdateKnobTexts(); // Update the actual knob text display
                        Console.WriteLine($"After UpdateKnobTexts - KnobA: '{_viewModel.KnobAFunction}', KnobB: '{_viewModel.KnobBFunction}'");
                    }
                    
                    // Save the configuration
                    Console.WriteLine("Saving layers after knob update...");
                    if (_viewModel != null)
                        await _viewModel.SaveLayersAsync();
                    Console.WriteLine("Layers saved successfully after knob update");
                    
                    // Force UI refresh on UI thread
                    if (_viewModel != null)
                    {
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            _viewModel.ForceUIRefresh();
                        });
                    }
                    
                    // Also try updating DataContext
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        var currentDataContext = this.DataContext;
                        this.DataContext = null;
                        this.DataContext = currentDataContext;
                    });
                    
                    Console.WriteLine("Forced UI refresh completed for knob");
                    
                    // Save the layers
                    Console.WriteLine("Saving layers...");
                    if (_viewModel != null)
                        await _viewModel.SaveLayersAsync();
                    Console.WriteLine("Layers saved successfully");
                    
                    // Just save locally, don't send to device automatically
                    if (_viewModel != null)
                        _viewModel.DisplayText = $"Knob {knobLetter} configured - Saved locally. Press 'Send to Device' to transfer.";
                }
                else
                {
                    Console.WriteLine($"Knob {knobLetter} not found in current layer");
                    if (_viewModel != null)
                        _viewModel.DisplayText = $"Knob {knobLetter} not found!";
                }
            }
            else
            {
                Console.WriteLine($"Knob config cancelled or no viewmodel/layer available. Result: {result}");
                if (_viewModel != null)
                    _viewModel.DisplayText = $"Knob config cancelled. Result: {result}";
            }
        }

        private void DisplayModeComboBox_SelectionChanged(object? sender, Avalonia.Controls.SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && _viewModel != null)
            {
                int selectedIndex = comboBox.SelectedIndex;
                System.Diagnostics.Debug.WriteLine($"ComboBox selection changed to index: {selectedIndex}");
                
                string newMode = selectedIndex switch
                {
                    0 => "layer",
                    1 => "battery",
                    2 => "time",
                    _ => "layer"
                };
                
                // Get the Content property of the ComboBoxItem
                if (comboBox.SelectedItem is ComboBoxItem item)
                {
                    newMode = item.Content?.ToString()?.ToLower() ?? "layer";
                }
                
                System.Diagnostics.Debug.WriteLine($"Setting display mode to: {newMode}");
                _viewModel.DisplayMode = newMode;
                
                // Display mode is saved locally - will be sent when user clicks "Send to Device"
                System.Diagnostics.Debug.WriteLine($"Display mode set to {newMode} - saved locally");
            }
        }


    }
}