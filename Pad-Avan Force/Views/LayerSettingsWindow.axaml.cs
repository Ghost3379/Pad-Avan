using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using PadAwan_Force.Models;

namespace PadAwan_Force.Views
{
    public partial class LayerSettingsWindow : Window
    {
        public ObservableCollection<Layer> OrderedLayers { get; private set; }
        public bool WrapAround { get; private set; } = true; // Default to true

        private bool _isDragging = false;
        private Layer? _draggedLayer = null;
        private Layer? _potentialDraggedLayer = null;
        private Point _pointerPressedPoint;
        private IPointer? _capturedPointer = null;

        // Minimum pixel movement to distinguish an intentional drag from a click
        private const double DragThreshold = 8.0;

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
            
            // Wait for UI to be ready, then update visual states
            this.Loaded += (s, e) =>
            {
                UpdateVisualStates();
            };
            
            // Subscribe to collection changes to update visual states
            OrderedLayers.CollectionChanged += (s, e) =>
            {
                UpdateVisualStates();
            };
        }

        #region Drag and Drop Logic (Window-level capture so it never drops mid-drag)

        private void DragHandle_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

            if (sender is Control handle && handle.DataContext is Layer layer)
            {
                int index = OrderedLayers.IndexOf(layer);
                // Protection: Layer 1 at index 0 is protected and cannot be moved
                if (index <= 0) return;

                _isDragging = false;
                _potentialDraggedLayer = layer;
                _draggedLayer = null;
                _pointerPressedPoint = e.GetPosition(this);

                // Capture pointer to the Window so items moving in the list never cause capture loss
                _capturedPointer = e.Pointer;
                _capturedPointer.Capture(this);

                e.Handled = true;
            }
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);

            if (_potentialDraggedLayer == null || _capturedPointer == null) return;

            // If the left button is no longer pressed, terminate drag
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                StopDragging();
                return;
            }

            Point currentPoint = e.GetPosition(this);
            double deltaY = currentPoint.Y - _pointerPressedPoint.Y;
            double distance = Math.Sqrt(Math.Pow(currentPoint.X - _pointerPressedPoint.X, 2) + Math.Pow(deltaY, 2));

            // Only initiate drag once cursor has moved beyond threshold to prevent clicks from jumping
            if (!_isDragging)
            {
                if (distance < DragThreshold)
                {
                    return; // Still within click deadband
                }
                _isDragging = true;
                _draggedLayer = _potentialDraggedLayer;
                UpdateVisualStates();
            }

            if (!_isDragging || _draggedLayer == null) return;

            int currentIndex = OrderedLayers.IndexOf(_draggedLayer);
            if (currentIndex <= 0) return; // Cannot move Layer 1

            Point currentItemsPos = e.GetPosition(LayerItemsControl);

            // Auto-scroll when dragging near top or bottom edges of ScrollViewer
            var scrollViewer = this.FindControl<ScrollViewer>("LayerScrollViewer");
            if (scrollViewer != null)
            {
                Point scrollPos = e.GetPosition(scrollViewer);
                double scrollHeight = scrollViewer.Bounds.Height;
                if (scrollPos.Y < 35 && scrollViewer.Offset.Y > 0)
                {
                    scrollViewer.Offset = new Vector(0, Math.Max(0, scrollViewer.Offset.Y - 8));
                }
                else if (scrollPos.Y > scrollHeight - 35)
                {
                    scrollViewer.Offset = new Vector(0, scrollViewer.Offset.Y + 8);
                }
            }

            // Find all rendered cards and map them to their exact layer DataContext
            var renderedBorders = LayerItemsControl.GetLogicalDescendants()
                .OfType<Border>()
                .Where(b => b.Name == "LayerBorder" && b.DataContext is Layer)
                .ToList();

            // Check against each other layer (starting at index 1: Layer 1 at index 0 is protected)
            for (int k = 1; k < OrderedLayers.Count; k++)
            {
                if (k == currentIndex) continue;

                var targetLayer = OrderedLayers[k];
                var targetBorder = renderedBorders.FirstOrDefault(b => b.DataContext == targetLayer);
                if (targetBorder == null) continue;

                var transform = targetBorder.TransformToVisual(LayerItemsControl);
                if (transform == null) continue;

                Point targetTopLeft = transform.Value.Transform(new Point(0, 0));
                double top = targetTopLeft.Y;
                double height = targetBorder.Bounds.Height;
                double mid = top + (height / 2.0);

                bool shouldSwap = false;
                if (currentIndex < k && currentItemsPos.Y > mid)
                {
                    shouldSwap = true;
                }
                else if (currentIndex > k && currentItemsPos.Y < mid)
                {
                    shouldSwap = true;
                }

                if (shouldSwap && k >= 1)
                {
                    // Move the item in collection, keeping cyan highlight on it
                    OrderedLayers.Move(currentIndex, k);
                    UpdateVisualStates();
                    break;
                }
            }

            e.Handled = true;
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);

            if (_potentialDraggedLayer != null || _isDragging)
            {
                StopDragging();
                e.Handled = true;
            }
        }

        protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
        {
            base.OnPointerCaptureLost(e);
            StopDragging();
        }

        private void StopDragging()
        {
            _isDragging = false;
            _draggedLayer = null;
            _potentialDraggedLayer = null;

            _capturedPointer?.Capture(null);
            _capturedPointer = null;

            UpdateVisualStates();
        }

        #endregion

        #region Visual State Updates

        private void UpdateVisualStates()
        {
            Dispatcher.UIThread.Post(() =>
            {
                var layerBorders = LayerItemsControl.GetLogicalDescendants()
                    .OfType<Border>()
                    .Where(b => b.Name == "LayerBorder" && b.DataContext is Layer)
                    .ToList();
                
                for (int i = 0; i < OrderedLayers.Count; i++)
                {
                    var layer = OrderedLayers[i];
                    var border = layerBorders.FirstOrDefault(b => b.DataContext == layer);
                    if (border == null) continue;
                    
                    var dragHandle = border.GetLogicalDescendants().OfType<Border>()
                        .FirstOrDefault(b => b.Name == "DragHandle");
                    var layer1Badge = border.GetLogicalDescendants().OfType<Border>()
                        .FirstOrDefault(b => b.Name == "Layer1Badge");
                    
                    if (i == 0)
                    {
                        // Layer 1: Protected, cannot be moved or dragged
                        if (dragHandle != null) dragHandle.IsVisible = false;
                        if (layer1Badge != null) layer1Badge.IsVisible = true;
                        
                        // Highlight Layer 1 border with cyan
                        border.BorderBrush = Brushes.Cyan;
                        border.BorderThickness = new Thickness(3);
                        border.Background = new SolidColorBrush(Color.Parse("#404040"));
                    }
                    else
                    {
                        // Other layers: Show drag handle, hide badge
                        if (dragHandle != null) dragHandle.IsVisible = true;
                        if (layer1Badge != null) layer1Badge.IsVisible = false;

                        // Active drag state: keep cyan highlight on the dragged layer the ENTIRE time until mouse release!
                        if (_isDragging && layer == _draggedLayer)
                        {
                            border.BorderBrush = Brushes.Cyan;
                            border.BorderThickness = new Thickness(2);
                            border.Background = new SolidColorBrush(Color.Parse("#505050"));

                            if (dragHandle != null)
                            {
                                foreach (var dot in dragHandle.GetLogicalDescendants().OfType<Ellipse>())
                                {
                                    dot.Fill = Brushes.Cyan;
                                }
                            }
                        }
                        else
                        {
                            border.BorderBrush = new SolidColorBrush(Color.Parse("#707070"));
                            border.BorderThickness = new Thickness(2);
                            border.Background = new SolidColorBrush(Color.Parse("#404040"));

                            if (dragHandle != null)
                            {
                                foreach (var dot in dragHandle.GetLogicalDescendants().OfType<Ellipse>())
                                {
                                    dot.Fill = new SolidColorBrush(Color.Parse("#707070"));
                                }
                            }
                        }
                    }
                }
            }, DispatcherPriority.Loaded);
        }

        #endregion

        #region Saving & Validation

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

        #endregion
    }
}
