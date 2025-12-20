using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Media;
using Avalonia.Threading;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PadAwan_Force.Models;
using Avalonia;

namespace PadAwan_Force.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        [ObservableProperty]
        private string greeting = "Welcome to PadAwan Force!";

        [ObservableProperty]
        private string status = "Disconnected";

        [ObservableProperty]
        private IBrush color = Brushes.Red;

        [ObservableProperty]
        private string comPort = "n/c";

        [ObservableProperty]
        private bool displayOn = true;

        private string _knobAFunction = "Volume Control";
        private string _knobBFunction = "Scroll Control";

        public string KnobAFunction
        {
            get => _knobAFunction;
            set 
            {
                if (SetProperty(ref _knobAFunction, value))
                {
                    UpdateDisplayText();
                }
            }
        }

        public string KnobBFunction
        {
            get => _knobBFunction;
            set 
            {
                if (SetProperty(ref _knobBFunction, value))
                {
                    UpdateDisplayText();
                }
            }
        }

        [ObservableProperty]
        private string displayText = "Layer";

        [ObservableProperty]
        private string padDisplayText = "Layer";

        [ObservableProperty]
        private int selectedDisplayFunction = 0;

        [ObservableProperty]
        private ObservableCollection<Layer> layers = new();

        [ObservableProperty]
        private int selectedLayerIndex = 0;

        [ObservableProperty]
        private int battery = 0;

        [ObservableProperty]
        private Layer? currentLayer;

        // Device Information Properties
        [ObservableProperty]
        private string deviceName = "PadAwan Force";

        [ObservableProperty]
        private string firmwareVersion = "v1.2.3";

        [ObservableProperty]
        private string padawanVersion = "v2.1.0";

        [ObservableProperty]
        private string hardwareRevision = "Rev A";

        [ObservableProperty]
        private string serialNumber = "PAF-2024-001";

        // Display Mode Properties
        [ObservableProperty]
        private string displayMode = "layer";

        [ObservableProperty]
        private bool displayEnabled = true;

        [ObservableProperty]
        private int displayModeIndex = 0; // 0=layer, 1=battery, 2=time

        partial void OnDisplayModeChanged(string value)
        {
            // Update the index when display mode changes programmatically
            DisplayModeIndex = value switch
            {
                "layer" => 0,
                "battery" => 1,
                "time" => 2,
                _ => 0
            };
            
            // Update pad display text
            UpdatePadDisplayText();
        }

        partial void OnDisplayModeIndexChanged(int value)
        {
            // Update the display mode string when index changes from UI
            string newMode = value switch
            {
                0 => "layer",
                1 => "battery", 
                2 => "time",
                _ => "layer"
            };
            
            // Update the display mode
            DisplayMode = newMode;
            
            // Save display settings locally - will be sent when user clicks "Send to Device"
            System.Diagnostics.Debug.WriteLine($"Display mode index changed to {value} ({newMode}) - saved locally");
            DisplayText = $"Display mode set to {newMode} - saved locally. Press 'Send to Device' to transfer.";
            _ = Task.Run(async () => await SaveLayersAsync());
        }

        partial void OnDisplayEnabledChanged(bool value)
        {
            // Save display settings locally - will be sent when user clicks "Send to Device"
            System.Diagnostics.Debug.WriteLine($"Display enabled changed to {value} - saved locally");
            DisplayText = $"Display {(value ? "enabled" : "disabled")} - saved locally. Press 'Send to Device' to transfer.";
            _ = Task.Run(async () => await SaveLayersAsync());
            
            // Update pad display text
            UpdatePadDisplayText();
        }

        private void UpdatePadDisplayText()
        {
            if (!DisplayEnabled)
            {
                PadDisplayText = "OFF";
            }
            else
            {
                PadDisplayText = DisplayMode switch
                {
                    "layer" => "Layer",
                    "battery" => "Battery",
                    "time" => "Time",
                    _ => "Layer"
                };
            }
        }

        [ObservableProperty]
        private string uptime = "2h 34m";

        [ObservableProperty]
        private string lastConnected = "Today 14:23";

        [ObservableProperty]
        private string dataTransferred = "1.2 MB";

        private string _button1Text = "";
        private string _button2Text = "";
        private string _button3Text = "";
        private string _button4Text = "";
        private string _button5Text = "";
        private string _button6Text = "";

        public string Button1Text
        {
            get => _button1Text;
            set => SetProperty(ref _button1Text, value);
        }

        public string Button2Text
        {
            get => _button2Text;
            set => SetProperty(ref _button2Text, value);
        }

        public string Button3Text
        {
            get => _button3Text;
            set => SetProperty(ref _button3Text, value);
        }

        public string Button4Text
        {
            get => _button4Text;
            set => SetProperty(ref _button4Text, value);
        }

        public string Button5Text
        {
            get => _button5Text;
            set => SetProperty(ref _button5Text, value);
        }

        public string Button6Text
        {
            get => _button6Text;
            set => SetProperty(ref _button6Text, value);
        }

        [ObservableProperty]
        private string debugInfo = "Debug: Not started";

        private readonly ConfigManager _configManager = new();
        private readonly FeatherConnection _featherConnection = new();
        
        // Connection monitoring counter
        private int _refreshCounter = 0;
        
        // Make accessible for debugging
        public FeatherConnection FeatherConnection => _featherConnection;
        

        public MainWindowViewModel()
        {
            System.Diagnostics.Debug.WriteLine("MainWindowViewModel constructor called");
            Console.WriteLine("MainWindowViewModel constructor called");
            
            try
            {
                // Initialize with default layer immediately to ensure we always have a layer
                Layers.Add(new Layer
                {
                    Id = 1,
                    Name = "Layer 1",
                    Buttons = new Dictionary<string, ButtonConfig>
                    {
                        ["1"] = new ButtonConfig { Action = "None" },
                        ["2"] = new ButtonConfig { Action = "None" },
                        ["3"] = new ButtonConfig { Action = "None" },
                        ["4"] = new ButtonConfig { Action = "None" },
                        ["5"] = new ButtonConfig { Action = "None" },
                        ["6"] = new ButtonConfig { Action = "None" }
                    },
                    Knobs = new Dictionary<string, KnobConfig>
                    {
                        ["A"] = new KnobConfig { CcwAction = "None", CwAction = "None", PressAction = "None" },
                        ["B"] = new KnobConfig { CcwAction = "None", CwAction = "None", PressAction = "None" }
                    }
                });
                // Set to Layer 1 by default (will be overridden by config file if available)
                SelectedLayerIndex = 0;
                System.Diagnostics.Debug.WriteLine($"Initialized with Layer 1 selected - Layers.Count: {Layers.Count}");
                Console.WriteLine($"Initialized with Layer 1 selected - Layers.Count: {Layers.Count}");
                
                // Note: Using polling instead of events to eliminate race conditions
                _featherConnection.ConnectionStatusChanged += OnConnectionStatusChanged;
                // _featherConnection.DeviceInfoChanged += OnDeviceInfoChanged;
                
                // Initialize device information from FeatherConnection
                UpdateDeviceInfoFromConnection();
                
                // Set initial status
                Status = "n/c";
                ComPort = "None";
                Color = Brushes.Red;
                DebugInfo = "Debug: Initializing...";
                
                System.Diagnostics.Debug.WriteLine($"Initial status set - Status: {Status}, ComPort: {ComPort}");
                Console.WriteLine($"Initial status set - Status: {Status}, ComPort: {ComPort}");
                
                // Initialize pad display text
                UpdatePadDisplayText();
                
                // Ensure Layer 1 is selected immediately
                EnsureLayer1Selected();
                
                // Force UI update after initial setup
                OnPropertyChanged(nameof(Layers));
                OnPropertyChanged(nameof(SelectedLayerIndex));
                OnPropertyChanged(nameof(CurrentLayer));
                
                // Then load from file asynchronously (with a small delay to let UI render)
                _ = Task.Delay(100).ContinueWith(_ => LoadLayersAsync());
                
                // Start automatic connection attempts asynchronously (this will handle initial UI update)
                _ = Task.Run(async () => await StartAutomaticConnection());
                
                System.Diagnostics.Debug.WriteLine("MainWindowViewModel constructor completed");
                Console.WriteLine("MainWindowViewModel constructor completed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in MainWindowViewModel constructor: {ex.Message}");
                Console.WriteLine($"Error in MainWindowViewModel constructor: {ex.Message}");
                // Set error status
                Status = "Error";
                DebugInfo = $"Error: {ex.Message}";
            }
        }

        partial void OnSelectedLayerIndexChanged(int value)
        {
            System.Diagnostics.Debug.WriteLine($"=== OnSelectedLayerIndexChanged called with value: {value} ===");
            System.Diagnostics.Debug.WriteLine($"Layers.Count: {Layers.Count}");
            System.Diagnostics.Debug.WriteLine($"Current CurrentLayer: {CurrentLayer?.Name} (ID={CurrentLayer?.Id})");
            
            if (value >= 0 && value < Layers.Count)
            {
                System.Diagnostics.Debug.WriteLine($"=== LAYER SWITCH DEBUG ===");
                System.Diagnostics.Debug.WriteLine($"Switching to layer index: {value}");
                System.Diagnostics.Debug.WriteLine($"Layer name: {Layers[value].Name}");
                System.Diagnostics.Debug.WriteLine($"Layer ID: {Layers[value].Id}");
                
                // Debug: Show button configurations before switching
                System.Diagnostics.Debug.WriteLine($"Button configs in {Layers[value].Name}:");
                foreach (var kvp in Layers[value].Buttons)
                {
                    System.Diagnostics.Debug.WriteLine($"  Button {kvp.Key}: Action='{kvp.Value.Action}', Key='{kvp.Value.Key}', Enabled={kvp.Value.Enabled}");
                }
                
                CurrentLayer = Layers[value];
                System.Diagnostics.Debug.WriteLine($"CurrentLayer set to: {CurrentLayer?.Name} (ID={CurrentLayer?.Id})");
                
                UpdateButtonTexts();
                UpdateKnobTexts();
                
                System.Diagnostics.Debug.WriteLine($"=== END LAYER SWITCH DEBUG ===");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"Invalid layer index: {value} (Layers.Count: {Layers.Count})");
            }
        }

        private void EnsureLayer1Selected()
        {
            // Force Layer 1 to be selected if we have layers
            if (Layers.Count > 0)
            {
                SelectedLayerIndex = 0;
                CurrentLayer = Layers[0];
                UpdateButtonTexts();
                UpdateKnobTexts();
                System.Diagnostics.Debug.WriteLine($"Ensured Layer 1 is selected - Layers.Count: {Layers.Count}");
                Console.WriteLine($"Ensured Layer 1 is selected - Layers.Count: {Layers.Count}");
                
                // Force property change notification
                OnPropertyChanged(nameof(Layers));
                OnPropertyChanged(nameof(SelectedLayerIndex));
                OnPropertyChanged(nameof(CurrentLayer));
            }
        }

        public void ForceUIUpdate()
        {
            // Force all UI properties to update
            OnPropertyChanged(nameof(Layers));
            OnPropertyChanged(nameof(SelectedLayerIndex));
            OnPropertyChanged(nameof(CurrentLayer));
            OnPropertyChanged(nameof(DisplayMode));
            OnPropertyChanged(nameof(DisplayEnabled));
            OnPropertyChanged(nameof(PadDisplayText));
            
            // Ensure Layer 1 is selected
            EnsureLayer1Selected();
            
            System.Diagnostics.Debug.WriteLine("Forced UI update completed");
            Console.WriteLine("Forced UI update completed");
        }

        public void TestUIUpdate()
        {
            // Test method to see if UI updates work at all
            Button1Text = "TEST1";
            Button2Text = "TEST2";
            KnobAFunction = "TEST A";
            KnobBFunction = "TEST B";
        }

        public void ForceUIRefresh()
        {
            Console.WriteLine("ForceUIRefresh called - forcing all UI properties to refresh");
            
            // Force update all button and knob properties
            OnPropertyChanged(nameof(Button1Text));
            OnPropertyChanged(nameof(Button2Text));
            OnPropertyChanged(nameof(Button3Text));
            OnPropertyChanged(nameof(Button4Text));
            OnPropertyChanged(nameof(Button5Text));
            OnPropertyChanged(nameof(Button6Text));
            OnPropertyChanged(nameof(KnobAFunction));
            OnPropertyChanged(nameof(KnobBFunction));
            
            // Also force update layer-related properties
            OnPropertyChanged(nameof(CurrentLayer));
            OnPropertyChanged(nameof(SelectedLayerIndex));
            OnPropertyChanged(nameof(Layers));
            
            Console.WriteLine("ForceUIRefresh completed");
        }

        public void UpdateButtonTexts()
        {
            // System.Diagnostics.Debug.WriteLine("=== UpdateButtonTexts called ===");
            if (CurrentLayer == null) 
            {
                // System.Diagnostics.Debug.WriteLine("CurrentLayer is null");
                return;
            }

            // System.Diagnostics.Debug.WriteLine($"CurrentLayer: {CurrentLayer.Name}");
            // System.Diagnostics.Debug.WriteLine($"Buttons count: {CurrentLayer.Buttons.Count}");
            
            // Debug: Show all button configurations in the layer
            // foreach (var kvp in CurrentLayer.Buttons)
            // {
            //     System.Diagnostics.Debug.WriteLine($"  Button {kvp.Key}: Action='{kvp.Value.Action}', Key='{kvp.Value.Key}', Enabled={kvp.Value.Enabled}");
            // }
            
            // Update button texts and colors
            var button1Config = CurrentLayer.Buttons.GetValueOrDefault("1");
            var button2Config = CurrentLayer.Buttons.GetValueOrDefault("2");
            var button3Config = CurrentLayer.Buttons.GetValueOrDefault("3");
            var button4Config = CurrentLayer.Buttons.GetValueOrDefault("4");
            var button5Config = CurrentLayer.Buttons.GetValueOrDefault("5");
            var button6Config = CurrentLayer.Buttons.GetValueOrDefault("6");
            
            var newButton1Text = GetButtonDisplayText(button1Config);
            var newButton2Text = GetButtonDisplayText(button2Config);
            var newButton3Text = GetButtonDisplayText(button3Config);
            var newButton4Text = GetButtonDisplayText(button4Config);
            var newButton5Text = GetButtonDisplayText(button5Config);
            var newButton6Text = GetButtonDisplayText(button6Config);
            
            var newButton1Color = GetButtonColor(button1Config);
            var newButton2Color = GetButtonColor(button2Config);
            var newButton3Color = GetButtonColor(button3Config);
            var newButton4Color = GetButtonColor(button4Config);
            var newButton5Color = GetButtonColor(button5Config);
            var newButton6Color = GetButtonColor(button6Config);
            
            // System.Diagnostics.Debug.WriteLine($"New button texts: 1='{newButton1Text}', 2='{newButton2Text}', 3='{newButton3Text}', 4='{newButton4Text}', 5='{newButton5Text}', 6='{newButton6Text}'");
            
            // Use SetProperty to ensure proper UI thread notifications
            // System.Diagnostics.Debug.WriteLine("Using SetProperty for all button texts...");
            
            SetProperty(ref _button1Text, newButton1Text, nameof(Button1Text));
            SetProperty(ref _button2Text, newButton2Text, nameof(Button2Text));
            SetProperty(ref _button3Text, newButton3Text, nameof(Button3Text));
            SetProperty(ref _button4Text, newButton4Text, nameof(Button4Text));
            SetProperty(ref _button5Text, newButton5Text, nameof(Button5Text));
            SetProperty(ref _button6Text, newButton6Text, nameof(Button6Text));
            
            Button1Color = newButton1Color;
            Button2Color = newButton2Color;
            Button3Color = newButton3Color;
            Button4Color = newButton4Color;
            Button5Color = newButton5Color;
            Button6Color = newButton6Color;
            
            // System.Diagnostics.Debug.WriteLine($"After SetProperty: 1='{Button1Text}', 2='{Button2Text}', 3='{Button3Text}', 4='{Button4Text}', 5='{Button5Text}', 6='{Button6Text}'");
        }

        public void UpdateKnobTexts()
        {
            // Console.WriteLine("UpdateKnobTexts called");
            if (CurrentLayer == null) 
            {
                // Console.WriteLine("CurrentLayer is null");
                return;
            }

            // Console.WriteLine($"CurrentLayer: {CurrentLayer.Name}");
            // Console.WriteLine($"Knobs count: {CurrentLayer.Knobs.Count}");
            
            // For now, just set simple A and B text
            string newKnobAText = "A";
            string newKnobBText = "B";
            
            // Console.WriteLine($"Knob A: '{newKnobAText}' (was: '{KnobAFunction}')");
            // Console.WriteLine($"Knob B: '{newKnobBText}' (was: '{KnobBFunction}')");
            
            // Use direct assignment and manual property change notifications
            _knobAFunction = newKnobAText;
            _knobBFunction = newKnobBText;
            
            // Console.WriteLine($"After direct assignment: A='{_knobAFunction}', B='{_knobBFunction}'");
            
            // Force property change notifications
            // Console.WriteLine("Calling OnPropertyChanged for knob texts...");
            OnPropertyChanged(nameof(KnobAFunction));
            OnPropertyChanged(nameof(KnobBFunction));
            
            // Console.WriteLine($"Final knob texts: A='{KnobAFunction}', B='{KnobBFunction}'");
        }

        private string GetKnobDisplayText(KnobConfig config)
        {
            // For now, just return the knob letter (A or B)
            // This will be set in UpdateKnobTexts method
            return "";
        }

        private string GetButtonDisplayText(ButtonConfig? config)
        {
            if (config == null)
            {
                return " "; // Return space instead of empty
            }
            
            // Check for Layer Switch action (case-insensitive and handle variations)
            if (!string.IsNullOrEmpty(config.Action) && 
                (config.Action.Equals("Layer Switch", StringComparison.OrdinalIgnoreCase) ||
                 config.Action.Equals("LayerSwitch", StringComparison.OrdinalIgnoreCase) ||
                 config.Action.Equals("Layer", StringComparison.OrdinalIgnoreCase)))
            {
                return "LS"; // Show "LS" instead of "Layer Switch"
            }
            
            if (string.IsNullOrEmpty(config.Key))
            {
                return " "; // Return space instead of "Empty"
            }
            
            return config.Key;
        }
        
        // Button color properties for Layer Switch indication
        private IBrush _button1Color = Brushes.White;
        private IBrush _button2Color = Brushes.White;
        private IBrush _button3Color = Brushes.White;
        private IBrush _button4Color = Brushes.White;
        private IBrush _button5Color = Brushes.White;
        private IBrush _button6Color = Brushes.White;
        
        public IBrush Button1Color
        {
            get => _button1Color;
            set => SetProperty(ref _button1Color, value);
        }
        
        public IBrush Button2Color
        {
            get => _button2Color;
            set => SetProperty(ref _button2Color, value);
        }
        
        public IBrush Button3Color
        {
            get => _button3Color;
            set => SetProperty(ref _button3Color, value);
        }
        
        public IBrush Button4Color
        {
            get => _button4Color;
            set => SetProperty(ref _button4Color, value);
        }
        
        public IBrush Button5Color
        {
            get => _button5Color;
            set => SetProperty(ref _button5Color, value);
        }
        
        public IBrush Button6Color
        {
            get => _button6Color;
            set => SetProperty(ref _button6Color, value);
        }
        
        private IBrush GetButtonColor(ButtonConfig? config)
        {
            if (config == null)
            {
                return Brushes.White;
            }
            
            // Check for Layer Switch action (case-insensitive and handle variations)
            if (!string.IsNullOrEmpty(config.Action) && 
                (config.Action.Equals("Layer Switch", StringComparison.OrdinalIgnoreCase) ||
                 config.Action.Equals("LayerSwitch", StringComparison.OrdinalIgnoreCase) ||
                 config.Action.Equals("Layer", StringComparison.OrdinalIgnoreCase)))
            {
                // Return cyan color (0x00FFFF) for Layer Switch
                // Use Color.Parse to create from hex string
                var cyanColor = Avalonia.Media.Color.Parse("#00FFFF");
                return new SolidColorBrush(cyanColor);
            }
            
            return Brushes.White; // Default white for other actions
        }

        [RelayCommand]
        private async Task AddLayerAsync()
        {
            // Get the highest layer number from existing layers
            int highestLayerNumber = Layers.Count > 0 ? Layers.Max(l => GetLayerNumberFromName(l.Name)) : 0;
            int newLayerNumber = highestLayerNumber + 1;

            var newLayer = new Layer
            {
                Id = Layers.Count > 0 ? Layers.Max(l => l.Id) + 1 : 1,
                Name = $"Layer {newLayerNumber}",
                Buttons = new Dictionary<string, ButtonConfig>
                {
                    ["1"] = new ButtonConfig { Action = "None" },
                    ["2"] = new ButtonConfig { Action = "None" },
                    ["3"] = new ButtonConfig { Action = "None" },
                    ["4"] = new ButtonConfig { Action = "None" },
                    ["5"] = new ButtonConfig { Action = "None" },
                    ["6"] = new ButtonConfig { Action = "None" }
                },
                Knobs = new Dictionary<string, KnobConfig>
                {
                    ["A"] = new KnobConfig { CcwAction = "None", CwAction = "None", PressAction = "None" },
                    ["B"] = new KnobConfig { CcwAction = "None", CwAction = "None", PressAction = "None" }
                }
            };

            System.Diagnostics.Debug.WriteLine($"=== CREATING NEW LAYER ===");
            System.Diagnostics.Debug.WriteLine($"ViewModel Hash: {this.GetHashCode()}");
            System.Diagnostics.Debug.WriteLine($"New layer: {newLayer.Name} (ID={newLayer.Id})");
            System.Diagnostics.Debug.WriteLine($"Button 1 config: Action='{newLayer.Buttons["1"].Action}', Key='{newLayer.Buttons["1"].Key}'");
            System.Diagnostics.Debug.WriteLine($"Button 1 object hash: {newLayer.Buttons["1"].GetHashCode()}");
            
            // Debug: Show all existing layers before adding
            System.Diagnostics.Debug.WriteLine("Existing layers before adding new one:");
            for (int i = 0; i < Layers.Count; i++)
            {
                var layer = Layers[i];
                System.Diagnostics.Debug.WriteLine($"  Layer {i}: {layer.Name} (ID={layer.Id})");
                if (layer.Buttons.TryGetValue("1", out var btnConfig))
                {
                    System.Diagnostics.Debug.WriteLine($"    Button 1: Action='{btnConfig.Action}', Key='{btnConfig.Key}', Hash={btnConfig.GetHashCode()}");
                }
            }

            Layers.Add(newLayer);
            SelectedLayerIndex = Layers.Count - 1;
            
            System.Diagnostics.Debug.WriteLine($"Added new layer. SelectedLayerIndex: {SelectedLayerIndex}");
            System.Diagnostics.Debug.WriteLine($"CurrentLayer after add: {CurrentLayer?.Name} (ID={CurrentLayer?.Id})");
            
            await SaveLayersAsync();
        }

        [RelayCommand]
        private async Task DeleteLayerAsync()
        {
            System.Diagnostics.Debug.WriteLine("=== DELETE LAYER COMMAND CALLED ===");
            System.Diagnostics.Debug.WriteLine($"Layers.Count: {Layers.Count}");
            System.Diagnostics.Debug.WriteLine($"SelectedLayerIndex: {SelectedLayerIndex}");
            System.Diagnostics.Debug.WriteLine($"CurrentLayer: {CurrentLayer?.Name}");
            
            if (SelectedLayerIndex < 0 || SelectedLayerIndex >= Layers.Count) 
            {
                System.Diagnostics.Debug.WriteLine("Cannot delete - invalid layer index");
                return; // Safety check
            }
            
            // Special handling for Layer 1: Reset instead of delete
            if (SelectedLayerIndex == 0)
            {
                System.Diagnostics.Debug.WriteLine("Layer 1 selected - will reset instead of delete");
                await ResetLayer1Async();
                return;
            }
            
            // For other layers, check if we can delete
            if (Layers.Count <= 1) 
            {
                System.Diagnostics.Debug.WriteLine("Cannot delete - only one layer left");
                return; // Don't delete the last layer
            }

            // Store the current layer number before deletion
            int currentLayerNumber = GetLayerNumberFromName(Layers[SelectedLayerIndex].Name);
            
            Layers.RemoveAt(SelectedLayerIndex);

            // Find the layer with the highest number that's lower than the deleted layer
            int targetIndex = 0;
            int highestLowerNumber = 0;
            
            for (int i = 0; i < Layers.Count; i++)
            {
                int layerNumber = GetLayerNumberFromName(Layers[i].Name);
                if (layerNumber < currentLayerNumber && layerNumber > highestLowerNumber)
                {
                    highestLowerNumber = layerNumber;
                    targetIndex = i;
                }
            }

            // If no lower layer found, select the first available layer
            if (highestLowerNumber == 0)
            {
                targetIndex = 0;
            }

            SelectedLayerIndex = targetIndex;
            await SaveLayersAsync();
        }

        private async Task ResetLayer1Async()
        {
            System.Diagnostics.Debug.WriteLine("=== RESETTING LAYER 1 ===");
            
            if (CurrentLayer != null)
            {
                // Reset all buttons to "None" with empty keys
                foreach (var buttonKey in CurrentLayer.Buttons.Keys.ToList())
                {
                    CurrentLayer.Buttons[buttonKey] = new ButtonConfig
                    {
                        Action = "None",
                        Key = "",
                        Enabled = false
                    };
                    System.Diagnostics.Debug.WriteLine($"Reset Button {buttonKey} to None");
                }

                // Reset all knobs to "None" with empty actions
                foreach (var knobKey in CurrentLayer.Knobs.Keys.ToList())
                {
                    CurrentLayer.Knobs[knobKey] = new KnobConfig
                    {
                        CcwAction = "None",
                        CwAction = "None",
                        PressAction = "None"
                    };
                    System.Diagnostics.Debug.WriteLine($"Reset Knob {knobKey} to None");
                }

                // Update UI to reflect changes
                UpdateButtonTexts();
                UpdateKnobTexts();
                
                // Save changes
                await SaveLayersAsync();
                
                // Force UI refresh
                ForceUIRefresh();
                
                System.Diagnostics.Debug.WriteLine("Layer 1 reset completed - all buttons and knobs set to None");
            }
        }

        private async Task LoadLayersAsync()
        {
            try
            {
                var loadedLayers = await _configManager.LoadLayersAsync();
                
                // Only replace if we actually loaded something from file
                if (loadedLayers.Count > 0)
                {
                    Layers.Clear();
                    foreach (var layer in loadedLayers)
                    {
                        Layers.Add(layer);
                    }
                    // Always ensure Layer 1 is selected by default
                    SelectedLayerIndex = 0;
                    System.Diagnostics.Debug.WriteLine($"Loaded {loadedLayers.Count} layers, selected Layer 1 - Layers.Count: {Layers.Count}");
                    Console.WriteLine($"Loaded {loadedLayers.Count} layers, selected Layer 1 - Layers.Count: {Layers.Count}");
                    
                    // Force property change notification
                    OnPropertyChanged(nameof(Layers));
                    OnPropertyChanged(nameof(SelectedLayerIndex));
                    
                    UpdateButtonTexts(); // Update button texts and colors after loading
                    UpdateKnobTexts(); // Update knob texts after loading
                }
                else
                {
                    // If no layers loaded, ensure Layer 1 is selected
                    SelectedLayerIndex = 0;
                    System.Diagnostics.Debug.WriteLine("No layers loaded from file, using default Layer 1");
                    UpdateButtonTexts();
                    UpdateKnobTexts();
                }
                
                // Load display settings and current layer from config file
                await LoadDisplaySettingsAsync();
                
                // Ensure Layer 1 is selected after loading (in case config overrode it)
                EnsureLayer1Selected();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading layers: {ex.Message}");
                // On error, ensure Layer 1 is selected
                SelectedLayerIndex = 0;
                System.Diagnostics.Debug.WriteLine("Error occurred, defaulting to Layer 1");
                UpdateButtonTexts(); // Update button texts with default layer
                UpdateKnobTexts(); // Update knob texts with default layer
                EnsureLayer1Selected(); // Double-check Layer 1 is selected
            }
        }

        private async Task LoadDisplaySettingsAsync()
        {
            try
            {
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "macropad_config.json");
                if (File.Exists(configPath))
                {
                    var json = await File.ReadAllTextAsync(configPath);
                    var config = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(json);
                    
                    if (config?.display != null)
                    {
                        DisplayMode = config.display.mode ?? "layer";
                        DisplayEnabled = config.display.enabled ?? true;
                        System.Diagnostics.Debug.WriteLine($"Loaded display settings - Mode: {DisplayMode}, Enabled: {DisplayEnabled}");
                    }
                    
                    // Load current layer from config file
                    if (config?.currentLayer != null)
                    {
                        int layerIndex = (int)config.currentLayer - 1;
                        if (layerIndex >= 0 && layerIndex < Layers.Count)
                        {
                            SelectedLayerIndex = layerIndex;
                            System.Diagnostics.Debug.WriteLine($"Loaded current layer from config: {layerIndex + 1}");
                        }
                        else
                        {
                            // If config layer is invalid, default to Layer 1
                            SelectedLayerIndex = 0;
                            System.Diagnostics.Debug.WriteLine($"Invalid layer in config ({layerIndex + 1}), defaulting to Layer 1");
                        }
                    }
                    else
                    {
                        // If no currentLayer in config, default to Layer 1
                        SelectedLayerIndex = 0;
                        System.Diagnostics.Debug.WriteLine("No currentLayer in config, defaulting to Layer 1");
                    }
                    
                    // Update button texts after setting the layer
                    UpdateButtonTexts();
                    UpdateKnobTexts();
                }
                else
                {
                    // If no config file exists, default to Layer 1
                    SelectedLayerIndex = 0;
                    System.Diagnostics.Debug.WriteLine("No config file found, defaulting to Layer 1");
                    UpdateButtonTexts();
                    UpdateKnobTexts();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading display settings: {ex.Message}");
                // On error, default to Layer 1
                SelectedLayerIndex = 0;
                UpdateButtonTexts();
                UpdateKnobTexts();
            }
        }

        public async Task SaveLayersAsync()
        {
            try
            {
            await _configManager.SaveLayersAsync(Layers.ToList(), DisplayMode, DisplayEnabled, SelectedLayerIndex + 1);
            
            // Configuration is saved locally - user can send to device manually with "Send to Device" button
            Console.WriteLine("Configuration saved locally - use 'Send to Device' button to transfer to FeatherS3");
                DisplayText = "✅ Configuration saved";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving configuration: {ex.Message}");
                DisplayText = $"❌ Error saving config: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"Error saving configuration: {ex.Message}\n{ex.StackTrace}");
            }
        }

        partial void OnDisplayOnChanged(bool value)
        {
            UpdateDisplayText();
        }

        partial void OnSelectedDisplayFunctionChanged(int value)
        {
            UpdateDisplayText();
        }


        private void UpdateDisplayText()
        {
            if (!DisplayOn)
            {
                DisplayText = "OFF";
            }
            else
            {
                // Show the selected display function
                switch (SelectedDisplayFunction)
                {
                    case 0: DisplayText = "Layer"; break;
                    case 1: DisplayText = $"{Battery}%"; break;
                    case 2: DisplayText = "Time"; break;
                    case 3: DisplayText = "Status"; break;
                    case 4: DisplayText = "Custom"; break;
                    default: DisplayText = "Layer"; break;
                }
            }
        }

        public void UpdateKnobFunction(string knobLetter, string function)
        {
            if (knobLetter == "A")
            {
                KnobAFunction = function;
            }
            else if (knobLetter == "B")
            {
                KnobBFunction = function;
            }
        }

        private int GetLayerNumberFromName(string layerName)
        {
            // Extract number from "Layer X" format
            if (layerName.StartsWith("Layer "))
            {
                string numberPart = layerName.Substring(6); // Remove "Layer "
                if (int.TryParse(numberPart, out int number))
                {
                    return number;
                }
            }
            return 0; // Default fallback
        }

        private int GetBatteryInfo()
        {
            // For now, return a simulated battery percentage
            // In a real implementation, this would read from the actual device
            return 85; // Simulated 85% battery
        }

        [RelayCommand]
        private async Task RefreshDeviceInfoAsync()
        {
            System.Diagnostics.Debug.WriteLine($"RefreshDeviceInfoAsync called - IsConnected: {_featherConnection.IsConnected}");
            
            // Always refresh device info, but handle connection state properly
            if (_featherConnection.IsConnected)
            {
                // Request real battery status from FeatherS3
                bool success = await _featherConnection.RequestBatteryStatusAsync();
                if (success)
                {
                    System.Diagnostics.Debug.WriteLine("Battery status refreshed successfully");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Failed to refresh battery status");
                }
            }
            else
            {
                // When not connected, refresh the device info to show default values
                await _featherConnection.RefreshDeviceInfoAsync();
                System.Diagnostics.Debug.WriteLine("Not connected - refreshed device info with default values");
            }
            
            // Force UI update regardless of connection state
            OnDeviceInfoChanged(null, EventArgs.Empty);
        }

        [RelayCommand]
        private async Task SendConfigurationAsync()
        {
            if (!_featherConnection.IsConnected)
            {
                DisplayText = "Not connected to device!";
                System.Diagnostics.Debug.WriteLine("Cannot send configuration - not connected to FeatherS3");
                return;
            }

            try
            {
                DisplayText = "Sending configuration...";
                System.Diagnostics.Debug.WriteLine("Sending configuration to FeatherS3...");

                // Send the current layer configuration
                bool success = await _featherConnection.SendLayersAsync(Layers.ToList());
                
                if (success)
                {
                    DisplayText = "Configuration sent successfully!";
                    System.Diagnostics.Debug.WriteLine("Configuration sent successfully to FeatherS3");
                }
                else
                {
                    DisplayText = "Failed to send configuration!";
                    System.Diagnostics.Debug.WriteLine("Failed to send configuration to FeatherS3");
                }
            }
            catch (Exception ex)
            {
                DisplayText = $"Error: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"Error sending configuration: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task ForceRefreshConnectionAsync()
        {
            System.Diagnostics.Debug.WriteLine("ForceRefreshConnectionAsync called");
            System.Diagnostics.Debug.WriteLine($"Current state before refresh - IsConnected: {_featherConnection.IsConnected}, Status: {_featherConnection.Status}");
            
            // Test if we can update the debug info manually
            DebugInfo = "Debug: Testing manual update...";
            System.Diagnostics.Debug.WriteLine($"Manually set DebugInfo to: {DebugInfo}");
            
            // Force disconnect first to ensure clean state
            _featherConnection.Disconnect();
            await Task.Delay(500); // Wait for cleanup
            
            // Try to connect
            bool connected = await _featherConnection.TryConnectAsync();
            System.Diagnostics.Debug.WriteLine($"Current state after refresh - IsConnected: {_featherConnection.IsConnected}, Status: {_featherConnection.Status}");
            
            // Force UI update
            ForceSyncAllWindows();
            
            // Update debug info after connection attempt
            DebugInfo = $"Debug: After refresh - Connected={_featherConnection.IsConnected}, Status={_featherConnection.Status}";
            System.Diagnostics.Debug.WriteLine($"After refresh DebugInfo set to: {DebugInfo}");
        }

        [RelayCommand]
        private void TestDebugUpdate()
        {
            DebugInfo = $"Debug: Test button clicked at {DateTime.Now:HH:mm:ss}";
            Status = $"Test Status at {DateTime.Now:HH:mm:ss}";
            Battery = new Random().Next(1, 100);
            ComPort = "TEST_PORT";
            Color = Brushes.Blue;
            System.Diagnostics.Debug.WriteLine($"TestDebugUpdate called - DebugInfo set to: {DebugInfo}");
            System.Diagnostics.Debug.WriteLine($"TestDebugUpdate called - Status set to: {Status}");
            System.Diagnostics.Debug.WriteLine($"TestDebugUpdate called - Battery set to: {Battery}");
            System.Diagnostics.Debug.WriteLine($"TestDebugUpdate called - ComPort set to: {ComPort}");
        }

        [RelayCommand]
        private void TestRefreshButton()
        {
            System.Diagnostics.Debug.WriteLine("TestRefreshButton called - simulating refresh button click");
            DebugInfo = $"Debug: Refresh button test at {DateTime.Now:HH:mm:ss}";
            Battery = new Random().Next(50, 100);
            System.Diagnostics.Debug.WriteLine($"TestRefreshButton - Battery set to: {Battery}");
        }

        [RelayCommand]
        private void CheckDeviceInfoState()
        {
            System.Diagnostics.Debug.WriteLine("CheckDeviceInfoState called");
            System.Diagnostics.Debug.WriteLine($"Current state - IsConnected: {_featherConnection.IsConnected}, Status: {Status}, Battery: {Battery}");
            DebugInfo = $"Debug: State check - Connected={_featherConnection.IsConnected}, Status={Status}, Battery={Battery}";
        }

        [RelayCommand]
        private async Task ForceCompleteRefreshAsync()
        {
            System.Diagnostics.Debug.WriteLine("ForceCompleteRefreshAsync called");
            
            // Force disconnect and reconnect
            _featherConnection.Disconnect();
            await Task.Delay(1000); // Wait for cleanup
            
            // Try to connect
            bool connected = await _featherConnection.TryConnectAsync();
            
            // Force refresh device info
            await _featherConnection.RefreshDeviceInfoAsync();
            
            // Force UI updates
            ForceSyncAllWindows();
            
            DebugInfo = $"Debug: Complete refresh - Connected={_featherConnection.IsConnected}, Status={_featherConnection.Status}";
            System.Diagnostics.Debug.WriteLine($"ForceCompleteRefreshAsync completed - Connected: {_featherConnection.IsConnected}");
        }


        public void ForceSyncAllWindows()
        {
            try
            {
                // Ensure UI updates happen on the UI thread
                Dispatcher.UIThread.Post(() =>
                {
                    try
                    {
                        if (_featherConnection != null)
                        {
                            // Read current state from FeatherConnection and update UI
                            var currentStatus = _featherConnection.Status;
                            var currentComPort = _featherConnection.ComPort;
                            var currentIsConnected = _featherConnection.IsConnected;
                            var currentBattery = _featherConnection.Battery;
                            
                            Status = currentStatus;
                            ComPort = currentComPort;
                            Color = currentIsConnected ? Brushes.LimeGreen : Brushes.Red;
                            Battery = currentBattery;
                            
                            System.Diagnostics.Debug.WriteLine($"ForceSyncAllWindows - Status: {Status}, ComPort: {ComPort}, IsConnected: {currentIsConnected}, Battery: {Battery}");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("ForceSyncAllWindows - _featherConnection is null");
                            Console.WriteLine("ForceSyncAllWindows - _featherConnection is null");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error in ForceSyncAllWindows UI update: {ex.Message}");
                        Console.WriteLine($"Error in ForceSyncAllWindows UI update: {ex.Message}");
                        // Set error status
                        Status = "Error";
                        DebugInfo = $"Sync Error: {ex.Message}";
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in ForceSyncAllWindows: {ex.Message}");
                Console.WriteLine($"Error in ForceSyncAllWindows: {ex.Message}");
                // Set error status
                Status = "Error";
                DebugInfo = $"Sync Error: {ex.Message}";
            }
        }


        [RelayCommand]
        private async Task TestConnectionAsync()
        {
            System.Diagnostics.Debug.WriteLine("TestConnectionAsync called");
            System.Diagnostics.Debug.WriteLine($"Current state - IsConnected: {_featherConnection.IsConnected}, Status: {_featherConnection.Status}");
            
            // Try to connect
            bool result = await _featherConnection.TryConnectAsync();
            System.Diagnostics.Debug.WriteLine($"Connection result: {result}");
            System.Diagnostics.Debug.WriteLine($"After connection - IsConnected: {_featherConnection.IsConnected}, Status: {_featherConnection.Status}");
            
            // Wait a moment and check again
            await Task.Delay(2000);
            System.Diagnostics.Debug.WriteLine($"After 2 seconds - IsConnected: {_featherConnection.IsConnected}, Status: {_featherConnection.Status}");
        }

        [RelayCommand]
        public async Task DownloadConfigurationAsync()
        {
            Console.WriteLine("=== Downloading configuration from FeatherS3 ===");
            
            if (!_featherConnection.IsConnected)
            {
                Console.WriteLine("Cannot download configuration - not connected to device");
                DisplayText = "Not connected to FeatherS3 - please connect first";
                return;
            }

            try
            {
                DisplayText = "Downloading configuration...";
                System.Diagnostics.Debug.WriteLine("Downloading configuration from FeatherS3...");
                
                string? configJson = await _featherConnection.GetCurrentConfigurationAsync();
                
                if (configJson != null)
                {
                    // Open file save dialog to let user choose where to save
                    var mainWindow = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                        ? desktop.MainWindow
                        : null;

                    if (mainWindow == null)
                    {
                        DisplayText = "❌ Cannot open file dialog - main window not found";
                        return;
                    }

                    var file = await mainWindow.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
                    {
                        Title = "Save Configuration File",
                        SuggestedFileName = "macropad_config.json",
                        FileTypeChoices = new[]
                        {
                            new Avalonia.Platform.Storage.FilePickerFileType("JSON Files")
                            {
                                Patterns = new[] { "*.json" }
                            },
                            new Avalonia.Platform.Storage.FilePickerFileType("All Files")
                            {
                                Patterns = new[] { "*" }
                            }
                        }
                    });

                    if (file != null)
                    {
                        string filePath = file.Path.LocalPath;
                        await File.WriteAllTextAsync(filePath, configJson);
                    
                    Console.WriteLine("=== Configuration downloaded successfully ===");
                        Console.WriteLine($"Saved to: {filePath}");
                    Console.WriteLine("=== Configuration Content ===");
                    Console.WriteLine(configJson);
                    Console.WriteLine("=== End Configuration ===");
                    
                        DisplayText = $"✅ Config downloaded! Saved to: {Path.GetFileName(filePath)}";
                        System.Diagnostics.Debug.WriteLine($"Configuration downloaded and saved to: {filePath}");
                    }
                    else
                    {
                        Console.WriteLine("File save dialog was cancelled");
                        DisplayText = "Download cancelled";
                        System.Diagnostics.Debug.WriteLine("File save dialog was cancelled by user");
                    }
                }
                else
                {
                    Console.WriteLine("Failed to download configuration");
                    DisplayText = "❌ Failed to download configuration";
                    System.Diagnostics.Debug.WriteLine("Failed to download configuration from FeatherS3");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error downloading configuration: {ex.Message}");
                DisplayText = $"Error: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"Error downloading configuration: {ex.Message}");
            }
        }

        [RelayCommand]
        public async Task UploadConfigFromFileAsync()
        {
            Console.WriteLine("=== Uploading configuration from file ===");
            
            if (!_featherConnection.IsConnected)
            {
                Console.WriteLine("Cannot upload configuration - not connected to device");
                DisplayText = "Not connected to FeatherS3 - please connect first";
                return;
            }

            try
            {
                var mainWindow = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                    ? desktop.MainWindow
                    : null;

                if (mainWindow == null)
                {
                    DisplayText = "❌ Cannot open file dialog - main window not found";
                    return;
                }

                var files = await mainWindow.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
                {
                    Title = "Select Configuration File",
                    FileTypeFilter = new[]
                    {
                        new Avalonia.Platform.Storage.FilePickerFileType("JSON Files")
                        {
                            Patterns = new[] { "*.json" }
                        },
                        new Avalonia.Platform.Storage.FilePickerFileType("All Files")
                        {
                            Patterns = new[] { "*" }
                        }
                    }
                });
                
                if (files != null && files.Count > 0)
                {
                    string filePath = files[0].Path.LocalPath;
                    string configJson = await File.ReadAllTextAsync(filePath);
                    
                    DisplayText = "Uploading configuration from file...";
                    System.Diagnostics.Debug.WriteLine($"Uploading configuration from file: {filePath}");
                    
                    bool success = await _featherConnection.UploadConfigurationAsync(configJson);
                    
                    if (success)
                    {
                        DisplayText = $"✅ Config uploaded from file!";
                        System.Diagnostics.Debug.WriteLine("Configuration uploaded successfully from file");
                    }
                    else
                    {
                        DisplayText = "❌ Failed to upload configuration";
                        System.Diagnostics.Debug.WriteLine("Failed to upload configuration from file");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error uploading configuration from file: {ex.Message}");
                DisplayText = $"Error: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"Error uploading configuration from file: {ex.Message}");
            }
        }

        [RelayCommand]
        public async Task ReadConfigAsync()
        {
            Console.WriteLine("=== Reading configuration from FeatherS3 ===");
            
            if (!_featherConnection.IsConnected)
            {
                Console.WriteLine("Cannot read configuration - not connected to device");
                DisplayText = "Not connected to FeatherS3 - please connect first";
                return;
            }

            try
            {
                DisplayText = "Reading configuration...";
                System.Diagnostics.Debug.WriteLine("Reading configuration from FeatherS3...");
                
                string? configJson = await _featherConnection.GetCurrentConfigurationAsync();
                
                if (configJson != null)
                {
                    Console.WriteLine("=== Configuration read successfully ===");
                    Console.WriteLine($"Configuration retrieved successfully! Length: {configJson.Length}");
                    System.Diagnostics.Debug.WriteLine("Configuration read successfully");
                    
                    // Parse the configuration JSON
                    try
                    {
                        var config = System.Text.Json.JsonSerializer.Deserialize<ConfigData>(configJson, new System.Text.Json.JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                        
                        if (config != null && config.Layers != null && config.Layers.Count > 0)
                        {
                            // Update Layers collection
                            Layers.Clear();
                            foreach (var layer in config.Layers)
                            {
                                Layers.Add(layer);
                            }
                            
                            // Update display settings
                            if (config.Display != null)
                            {
                                DisplayMode = config.Display.Mode ?? "layer";
                                DisplayEnabled = config.Display.Enabled;
                            }
                            
                            // Set current layer (if specified in config)
                            if (config.CurrentLayer > 0 && config.CurrentLayer <= Layers.Count)
                            {
                                SelectedLayerIndex = config.CurrentLayer - 1; // Convert to 0-based index
                            }
                            else
                            {
                                SelectedLayerIndex = 0; // Default to Layer 1
                            }
                            
                            // Force property change notifications
                            OnPropertyChanged(nameof(Layers));
                            OnPropertyChanged(nameof(SelectedLayerIndex));
                            OnPropertyChanged(nameof(CurrentLayer));
                            
                            // Update button and knob texts
                            UpdateButtonTexts();
                            UpdateKnobTexts();
                            
                            // Also save to local file for consistency
                            var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "macropad_config.json");
                            await File.WriteAllTextAsync(configPath, configJson);
                            
                            Console.WriteLine($"✅ Configuration loaded: {config.Layers.Count} layer(s), Current Layer: {config.CurrentLayer}");
                            DisplayText = $"✅ Config loaded! {config.Layers.Count} layer(s) loaded.";
                            System.Diagnostics.Debug.WriteLine($"Configuration loaded successfully: {config.Layers.Count} layers");
                        }
                        else
                        {
                            Console.WriteLine("⚠️ Configuration parsed but no layers found");
                            DisplayText = "⚠️ Config read but no layers found";
                        }
                    }
                    catch (Exception parseEx)
                    {
                        Console.WriteLine($"Error parsing configuration JSON: {parseEx.Message}");
                        Console.WriteLine($"JSON content: {configJson.Substring(0, Math.Min(500, configJson.Length))}...");
                        DisplayText = $"❌ Error parsing config: {parseEx.Message}";
                        System.Diagnostics.Debug.WriteLine($"Error parsing configuration: {parseEx.Message}");
                    }
                }
                else
                {
                    Console.WriteLine("Failed to read configuration");
                    DisplayText = "❌ Failed to read configuration";
                    System.Diagnostics.Debug.WriteLine("Failed to read configuration from FeatherS3");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading configuration: {ex.Message}");
                DisplayText = $"Error: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"Error reading configuration: {ex.Message}");
            }
        }

        [RelayCommand]
        private void OpenUpdateWindow()
        {
            var updateWindow = new Views.UpdateWindow
            {
                DataContext = new UpdateWindowViewModel(_featherConnection)
            };
            
            var mainWindow = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;
            
            if (mainWindow != null)
            {
                updateWindow.ShowDialog(mainWindow);
            }
            else
            {
                updateWindow.Show();
            }
        }

        [RelayCommand]
        public async Task<bool> SendConfigurationToDeviceAsync()
        {
            Console.WriteLine("=== Sending ENTIRE configuration to FeatherS3 ===");
            Console.WriteLine($"IsConnected: {_featherConnection.IsConnected}");
            Console.WriteLine($"Status: {_featherConnection.Status}");
            Console.WriteLine($"ComPort: {_featherConnection.ComPort}");
            
            if (!_featherConnection.IsConnected)
            {
                Console.WriteLine("Cannot send configuration - not connected to device");
                System.Diagnostics.Debug.WriteLine("Cannot send configuration - not connected to device");
                DisplayText = "Not connected to FeatherS3 - please connect first";
                return false;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine("Sending configuration to FeatherS3...");
                
                // Convert layers to comprehensive JSON format that FeatherS3 expects
                var config = new
                {
                    // Metadata
                    version = "1.0",
                    created = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    device = "FeatherS3",
                    
                    // Display settings
                    display = new
                    {
                        mode = DisplayMode,
                        enabled = DisplayEnabled
                    },
                    
                    // System time for time display mode
                    systemTime = new
                    {
                        currentTime = DateTime.Now.ToString("HH:mm"),
                        currentDate = DateTime.Now.ToString("yyyy-MM-dd"),
                        timezone = TimeZoneInfo.Local.Id,
                        timestamp = DateTimeOffset.Now.ToUnixTimeSeconds()
                    },
                    
                    // Current layer
                    currentLayer = SelectedLayerIndex + 1,
                    
                    // Layer configuration
                    layers = Layers.Select(layer => new
                    {
                        id = layer.Id,
                        name = layer.Name,
                        buttons = layer.Buttons.ToDictionary(
                            kvp => kvp.Key,
                            kvp => new
                            {
                                action = kvp.Value.Action,
                                key = kvp.Value.Key,
                                enabled = kvp.Value.Enabled
                            }
                        ),
                        knobs = layer.Knobs.ToDictionary(
                            kvp => kvp.Key,
                            kvp =>
                            {
                                // Build dynamic object - only include pressKey if pressAction needs it
                                var knobObj = new Dictionary<string, object>
                                {
                                    ["ccwAction"] = kvp.Value.CcwAction,
                                    ["cwAction"] = kvp.Value.CwAction,
                                    ["pressAction"] = kvp.Value.PressAction
                                };
                                
                                // Only include pressKey if the action requires it (Type Text, Special Key, Key combo)
                                if (kvp.Value.PressAction == "Type Text" || 
                                    kvp.Value.PressAction == "Special Key" || 
                                    kvp.Value.PressAction == "Key combo")
                                {
                                    knobObj["pressKey"] = kvp.Value.PressKey ?? "";
                                }
                                
                                return knobObj;
                            }
                        )
                    }).ToArray(),
                    
                    // Configuration limits (for FeatherS3)
                    limits = new
                    {
                        maxLayers = Layers.Count, // Dynamic based on actual layers
                        maxButtons = 6, // Fixed for this macropad
                        maxKnobs = 2   // Fixed for this macropad
                    }
                };

                string jsonConfig = Newtonsoft.Json.JsonConvert.SerializeObject(config, Newtonsoft.Json.Formatting.Indented);
                System.Diagnostics.Debug.WriteLine($"JSON Config: {jsonConfig}");
                Console.WriteLine($"JSON Config: {jsonConfig}");
                
                // Debug: Check if buttons and knobs are included
                Console.WriteLine($"Layers count: {Layers.Count}");
                foreach (var layer in Layers)
                {
                    Console.WriteLine($"Layer {layer.Name}: Buttons={layer.Buttons.Count}, Knobs={layer.Knobs.Count}");
                    foreach (var button in layer.Buttons)
                    {
                        Console.WriteLine($"  Button {button.Key}: {button.Value.Action} - {button.Value.Key}");
                    }
                    foreach (var knob in layer.Knobs)
                    {
                        Console.WriteLine($"  Knob {knob.Key}: CCW={knob.Value.CcwAction}, CW={knob.Value.CwAction}, Press={knob.Value.PressAction}");
                    }
                }

                Console.WriteLine("Calling SendLayerConfigurationAsync...");
                bool success = await _featherConnection.SendLayerConfigurationAsync(jsonConfig);
                Console.WriteLine($"SendLayerConfigurationAsync result: {success}");
                
                if (success)
                {
                    Console.WriteLine("ENTIRE configuration sent successfully to FeatherS3!");
                    System.Diagnostics.Debug.WriteLine("Configuration sent successfully to FeatherS3!");
                    DisplayText = "✅ Entire configuration sent to FeatherS3!";
                }
                else
                {
                    Console.WriteLine("Failed to send configuration to FeatherS3");
                    System.Diagnostics.Debug.WriteLine("Failed to send configuration to FeatherS3");
                    DisplayText = "❌ Failed to send configuration to device";
                }
                return success;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error sending configuration: {ex.Message}");
                DisplayText = $"Error: {ex.Message}";
                return false;
            }
        }


        public async Task SetDisplayModeAsync(string mode)
        {
            if (!_featherConnection.IsConnected)
            {
                System.Diagnostics.Debug.WriteLine("Cannot set display mode - not connected to device");
                DisplayText = "Not connected to device";
                return;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine($"Setting display mode to: {mode}, enabled: {DisplayEnabled}");
                bool success = await _featherConnection.SetDisplayModeAsync(mode, DisplayEnabled);
                if (success)
                {
                    DisplayText = $"Display mode set to: {mode}";
                    System.Diagnostics.Debug.WriteLine($"Successfully set display mode to: {mode}");
                    
                // If setting to time mode, also send current time immediately
                if (mode == "time")
                {
                    string currentTime = DateTime.Now.ToString("HH:mm");
                    System.Diagnostics.Debug.WriteLine($"Sending current time: {currentTime}");
                    await _featherConnection.SetTimeAsync(currentTime);
                }
                }
                else
                {
                    DisplayText = "Failed to set display mode";
                    System.Diagnostics.Debug.WriteLine($"Failed to set display mode to: {mode}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting display mode: {ex.Message}");
                DisplayText = $"Error: {ex.Message}";
            }
        }

        private async Task ToggleDisplayAsync()
        {
            if (!_featherConnection.IsConnected)
            {
                System.Diagnostics.Debug.WriteLine("Cannot toggle display - not connected to device");
                return;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine($"Toggling display: {DisplayEnabled}");
                bool success = await _featherConnection.SetDisplayModeAsync(DisplayMode, DisplayEnabled);
                if (success)
                {
                    DisplayText = $"Display {(DisplayEnabled ? "enabled" : "disabled")}";
                }
                else
                {
                    DisplayText = "Failed to toggle display";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error toggling display: {ex.Message}");
                DisplayText = $"Error: {ex.Message}";
            }
        }

        private async Task StartAutomaticConnection()
        {
            System.Diagnostics.Debug.WriteLine("StartAutomaticConnection called");
            Console.WriteLine("StartAutomaticConnection called");
            
            try
            {
                // Start time update timer for time mode
                var timeUpdateTimer = new System.Timers.Timer(60000); // Update every minute
                timeUpdateTimer.Elapsed += async (sender, e) =>
                {
                    try
                    {
                        if (_featherConnection.IsConnected && DisplayMode == "time")
                        {
                            string currentTime = DateTime.Now.ToString("HH:mm");
                            System.Diagnostics.Debug.WriteLine($"Updating time on FeatherS3: {currentTime}");
                            Console.WriteLine($"Updating time on FeatherS3: {currentTime}");
                            await _featherConnection.SetTimeAsync(currentTime);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error updating time: {ex.Message}");
                        Console.WriteLine($"Error updating time: {ex.Message}");
                    }
                };
                timeUpdateTimer.Start();
                
                // Also start immediate time update when switching to time mode
                if (DisplayMode == "time" && _featherConnection.IsConnected)
                {
                    string currentTime = DateTime.Now.ToString("HH:mm");
                    System.Diagnostics.Debug.WriteLine($"Immediate time update: {currentTime}");
                    await _featherConnection.SetTimeAsync(currentTime);
                }
                
                // Wait a moment for the device to be ready (non-blocking)
                await Task.Delay(200); // Reduziert von 500ms - sehr schnelle Verbindung
                
                // Try to connect once (non-blocking)
                bool connected = await _featherConnection.TryConnectAsync();
                System.Diagnostics.Debug.WriteLine($"Initial connection attempt: {connected}");
                Console.WriteLine($"Initial connection attempt: {connected}");
                
                // Force UI update after initial connection attempt
                ForceSyncAllWindows();
                
                // Set up periodic connection monitoring every 250ms for very fast updates
                var timer = new System.Timers.Timer(250);  // Reduziert von 1000ms - viel schnellere Reaktion
                timer.Elapsed += async (sender, e) =>
                {
                    try
                    {
                        // Sync UI with current state - events handle most updates
                        // ForceSyncAllWindows(); // Disabled to prevent race conditions with events
                        
                        if (_featherConnection.IsConnected)
                        {
                            // Check if device is still actually connected (aggressive for fast detection)
                            // Check every 1 second (4 * 250ms) for fast disconnection detection
                            _refreshCounter++;
                            if (_refreshCounter >= 4) // 4 * 250ms = 1 second between checks
                            {
                                _refreshCounter = 0;
                                
                                // Check connection health (but only if we're past the grace period)
                                // The grace period check is done inside IsDeviceStillConnected()
                                if (!_featherConnection.IsDeviceStillConnected())
                                {
                                    // Wait a bit and check again - Windows might temporarily remove port from list
                                    await Task.Delay(500); // Reduziert von 1000ms - schnellere Reaktion
                                    if (!_featherConnection.IsDeviceStillConnected())
                                    {
                                        // Double-check with a ping before disconnecting
                                        // Only disconnect if ping fails twice in a row
                                        bool pingResult1 = await _featherConnection.PingAsync();
                                        if (!pingResult1)
                                        {
                                            await Task.Delay(500);
                                            bool pingResult2 = await _featherConnection.PingAsync();
                                            if (!pingResult2)
                                        {
                                                System.Diagnostics.Debug.WriteLine("Device disconnected - ping failed twice, updating UI");
                                            _featherConnection.Disconnect();
                                            ForceSyncAllWindows(); // Update UI immediately
                                        }
                                        else
                                        {
                                                System.Diagnostics.Debug.WriteLine("Device still connected - second ping succeeded");
                                            }
                                        }
                                        else
                                        {
                                            System.Diagnostics.Debug.WriteLine("Device still connected - first ping succeeded");
                                        }
                                    }
                                    else
                                    {
                                        System.Diagnostics.Debug.WriteLine("Device still connected - port check passed on retry");
                                    }
                                }
                                else
                                {
                                    // Device is connected, refresh device info periodically (every 20 seconds = 10 checks)
                                    try
                                    {
                                        await _featherConnection.RefreshDeviceInfoAsync();
                                    }
                                    catch (Exception ex)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"RefreshDeviceInfoAsync failed: {ex.Message}");
                                        // Don't disconnect on refresh failure - just log it
                                    }
                                }
                            }
                        }
                        else
                        {
                            // Try to reconnect if not connected (non-blocking)
                            // But skip if firmware update is in progress
                            if (!_featherConnection.IsUpdatingFirmware)
                            {
                                // Reconnect immediately without delay - timer already provides spacing
                                _ = Task.Run(async () =>
                                {
                                    try
                                    {
                                        System.Diagnostics.Debug.WriteLine("Attempting to reconnect...");
                                        bool reconnected = await _featherConnection.TryConnectAsync();
                                        if (reconnected)
                                        {
                                            System.Diagnostics.Debug.WriteLine("Reconnection successful");
                                            ForceSyncAllWindows(); // Update UI immediately after connection
                                        }
                                        else
                                        {
                                            System.Diagnostics.Debug.WriteLine("Reconnection failed");
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"Error in reconnection: {ex.Message}");
                                        Console.WriteLine($"Error in reconnection: {ex.Message}");
                                    }
                                });
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine("Skipping reconnection - firmware update in progress");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error in connection monitoring: {ex.Message}");
                        Console.WriteLine($"Error in connection monitoring: {ex.Message}");
                    }
                };
                timer.Start();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in StartAutomaticConnection: {ex.Message}");
                Console.WriteLine($"Error in StartAutomaticConnection: {ex.Message}");
                // Set error status
                Status = "Error";
                DebugInfo = $"Connection Error: {ex.Message}";
            }
        }


        private void OnConnectionStatusChanged(object? sender, EventArgs e)
        {
            // Use a lock to ensure only one thread can update the UI at a time
            lock (this)
            {
                if (_featherConnection != null)
                {
                    // Always update - no state change detection to eliminate race conditions
                    var currentStatus = _featherConnection.Status;
                    var currentComPort = _featherConnection.ComPort;
                    var currentIsConnected = _featherConnection.IsConnected;
                    var currentBattery = _featherConnection.Battery;
                    
                    Status = currentStatus;
                    ComPort = currentComPort;
                    Color = currentIsConnected ? Brushes.LimeGreen : Brushes.Red;
                    Battery = currentBattery;
                    DebugInfo = $"Debug: Connected={currentIsConnected}, Status={currentStatus}, Port={currentComPort}";
                    
                    System.Diagnostics.Debug.WriteLine($"OnConnectionStatusChanged: Status={currentStatus}, Connected={currentIsConnected}, Port={currentComPort}");
                }
            }
        }

        private void OnDeviceInfoChanged(object? sender, EventArgs e)
        {
            UpdateDeviceInfoFromConnection();
        }

        private void UpdateDeviceInfoFromConnection()
        {
            if (_featherConnection != null)
            {
                System.Diagnostics.Debug.WriteLine($"Updating device info - Battery: {_featherConnection.Battery}, IsConnected: {_featherConnection.IsConnected}");
                
                // Update properties directly
                DeviceName = _featherConnection.DeviceName;
                FirmwareVersion = _featherConnection.FirmwareVersion;
                HardwareRevision = _featherConnection.HardwareRevision;
                SerialNumber = _featherConnection.SerialNumber;
                Uptime = _featherConnection.Uptime;
                LastConnected = _featherConnection.LastConnected;
                DataTransferred = _featherConnection.DataTransferred;
                Battery = _featherConnection.Battery;
                System.Diagnostics.Debug.WriteLine($"Updated main window - Battery: {Battery}");
            }
        }
    }
}
