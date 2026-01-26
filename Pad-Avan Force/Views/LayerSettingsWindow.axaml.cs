using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using PadAwan_Force.Models;
using Avalonia.LogicalTree;

namespace PadAwan_Force.Views
{
    public partial class LayerSettingsWindow : Window
    {
        public ObservableCollection<Layer> OrderedLayers { get; private set; }
        public bool WrapAround { get; private set; } = true; // Default to true

        // Parameterless constructor for XAML designer
        public LayerSettingsWindow()
        {
            InitializeComponent();
            OrderedLayers = new ObservableCollection<Layer>();
        }

        public LayerSettingsWindow(ObservableCollection<Layer> layers, bool wrapAround = true)
        {
            InitializeComponent();
            
            WrapAround = wrapAround;
            
            // Set checkbox state
            var wrapAroundCheckBox = this.FindControl<CheckBox>("WrapAroundCheckBox");
            if (wrapAroundCheckBox != null)
            {
                wrapAroundCheckBox.IsChecked = wrapAround;
            }
            
            // Create a copy of the layers for editing
            OrderedLayers = new ObservableCollection<Layer>(layers.Select(l => new Layer
            {
                Id = l.Id,
                Name = l.Name,
                Buttons = l.Buttons,
                Knobs = l.Knobs
            }));
            
            LayerItemsControl.ItemsSource = OrderedLayers;
            
            // Set up drag and drop
            SetupDragAndDrop();
            
            // Wait for UI to be ready, then update button states
            this.Loaded += (s, e) =>
            {
                UpdateButtonStates();
            };
            
            // Subscribe to collection changes to update button states
            OrderedLayers.CollectionChanged += (s, e) =>
            {
                UpdateButtonStates();
            };
        }

        private void SetupDragAndDrop()
        {
            // Drag and drop is handled via up/down buttons
        }

        private void MoveUpButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Layer layer)
            {
                int index = OrderedLayers.IndexOf(layer);
                // Prevent moving Layer 1 (first layer) up, and prevent moving layer at index 1 above Layer 1
                if (index > 1) // Can only move up if index is 2 or higher
                {
                    OrderedLayers.Move(index, index - 1);
                    // Update button states after move
                    UpdateButtonStates();
                }
            }
        }

        private void MoveDownButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Layer layer)
            {
                int index = OrderedLayers.IndexOf(layer);
                if (index >= 0 && index < OrderedLayers.Count - 1)
                {
                    OrderedLayers.Move(index, index + 1);
                    // Update button states after move
                    UpdateButtonStates();
                }
            }
        }

        private void UpdateButtonStates()
        {
            // Update button states after UI is rendered
            Dispatcher.UIThread.Post(() =>
            {
                // Use a delay to ensure ItemsControl has rendered its items
                Task.Delay(50).ContinueWith(_ =>
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        // Find all visual children that are Borders (our layer items)
                        var layerBorders = LayerItemsControl.GetLogicalDescendants()
                            .OfType<Border>()
                            .Where(b => b.Name == "LayerBorder")
                            .ToList();
                        
                        for (int i = 0; i < layerBorders.Count && i < OrderedLayers.Count; i++)
                        {
                            var border = layerBorders[i];
                            
                            // Find controls within this border
                            var buttonPanel = border.GetLogicalDescendants().OfType<StackPanel>()
                                .FirstOrDefault(sp => sp.Name == "MoveButtonsPanel");
                            var upButton = border.GetLogicalDescendants().OfType<Button>()
                                .FirstOrDefault(b => b.Name == "MoveUpButton");
                            var downButton = border.GetLogicalDescendants().OfType<Button>()
                                .FirstOrDefault(b => b.Name == "MoveDownButton");
                            var layer1Badge = border.GetLogicalDescendants().OfType<Border>()
                                .FirstOrDefault(b => b.Name == "Layer1Badge");
                            
                            if (i == 0)
                            {
                                // Layer 1: Hide move buttons completely
                                if (buttonPanel != null)
                                {
                                    buttonPanel.IsVisible = false;
                                }
                                
                                // Show Layer 1 badge
                                if (layer1Badge != null)
                                {
                                    layer1Badge.IsVisible = true;
                                }
                                
                                // Highlight Layer 1 border with cyan
                                border.BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(0, 255, 255)); // Cyan
                                border.BorderThickness = new Avalonia.Thickness(3);
                            }
                            else
                            {
                                // Other layers: Show buttons but disable up button for layer directly under Layer 1
                                if (buttonPanel != null)
                                {
                                    buttonPanel.IsVisible = true;
                                }
                                
                                // Disable up button for second layer (can't move above Layer 1)
                                if (upButton != null)
                                {
                                    upButton.IsEnabled = (i > 1); // Disable for index 1 (second layer)
                                    upButton.Opacity = (i == 1) ? 0.5 : 1.0;
                                }
                                
                                // Hide Layer 1 badge
                                if (layer1Badge != null)
                                {
                                    layer1Badge.IsVisible = false;
                                }
                                
                                // Normal border for other layers
                                border.BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(112, 112, 112)); // #707070
                                border.BorderThickness = new Avalonia.Thickness(2);
                            }
                        }
                    }, Avalonia.Threading.DispatcherPriority.Loaded);
                });
            }, Avalonia.Threading.DispatcherPriority.Loaded);
        }

        private void LayerNameTextBox_LostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            // Validate and update layer name
            if (sender is TextBox textBox && textBox.DataContext is Layer layer)
            {
                string newName = textBox.Text?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(newName))
                {
                    // Restore original name if empty
                    textBox.Text = layer.Name;
                }
                else
                {
                    layer.Name = newName;
                }
            }
        }

        private void SaveButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            // Validate all layer names
            foreach (var layer in OrderedLayers)
            {
                if (string.IsNullOrWhiteSpace(layer.Name))
                {
                    layer.Name = $"Layer {layer.Id}";
                }
            }
            
            // Reassign IDs sequentially based on position (position 1 = ID 1, position 2 = ID 2, etc.)
            for (int i = 0; i < OrderedLayers.Count; i++)
            {
                OrderedLayers[i].Id = i + 1; // Position 0 = ID 1, Position 1 = ID 2, etc.
            }
            
            // Get wrap-around setting
            var wrapAroundCheckBox = this.FindControl<CheckBox>("WrapAroundCheckBox");
            if (wrapAroundCheckBox != null)
            {
                WrapAround = wrapAroundCheckBox.IsChecked ?? true;
            }
            
            this.Close(true);
        }

        private void CancelButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            this.Close(false);
        }
    }
}
