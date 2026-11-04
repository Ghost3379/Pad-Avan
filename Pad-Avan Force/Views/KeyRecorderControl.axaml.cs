using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PadAwan_Force.Views
{
    public partial class KeyRecorderControl : UserControl
    {
        private string _recordedKey = string.Empty;
        private string _previousKey = string.Empty;
        private bool _isRecording = false;
        private Window? _hookedWindow = null;

        // Negative-edge (falling edge) tracking
        private readonly HashSet<string> _heldModifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private string? _heldNonModifierKey = null;
        private KeyModifiers _activeModifiers = KeyModifiers.None;

        public event EventHandler<string>? KeyChanged;

        public string RecordedKey
        {
            get => _recordedKey;
            set
            {
                if (_recordedKey != value)
                {
                    _recordedKey = value ?? string.Empty;
                    UpdateDisplay();
                    KeyChanged?.Invoke(this, _recordedKey);
                }
            }
        }

        public bool IsRecording => _isRecording;

        public KeyRecorderControl()
        {
            InitializeComponent();

            RecorderBorder.PointerPressed += RecorderBorder_PointerPressed;
            ClearButton.Click += ClearButton_Click;
            MouseButton.Click += MouseButton_Click;

            this.DetachedFromVisualTree += (s, e) =>
            {
                StopRecording(revert: true);
            };

            UpdateDisplay();
        }

        private void RecorderBorder_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!_isRecording)
            {
                StartRecording();
            }
            else
            {
                StopRecording(revert: false);
            }
        }

        private void MouseButton_Click(object? sender, RoutedEventArgs e)
        {
            if (MouseButton.ContextMenu != null)
            {
                MouseButton.ContextMenu.Open(MouseButton);
            }
        }

        private void MouseMenuItem_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item)
            {
                string text = item.Header?.ToString() ?? "";
                if (!string.IsNullOrWhiteSpace(text))
                {
                    StopRecording(revert: false);
                    RecordedKey = text;
                }
            }
        }

        private void ClearButton_Click(object? sender, RoutedEventArgs e)
        {
            StopRecording(revert: true);
            RecordedKey = string.Empty;
        }

        public void StartRecording()
        {
            if (_isRecording) return;

            _isRecording = true;
            _previousKey = _recordedKey;
            _heldModifiers.Clear();
            _heldNonModifierKey = null;
            _activeModifiers = KeyModifiers.None;

            // Visual feedback: recording active
            RecorderPrompt.IsVisible = true;
            RecorderPrompt.Text = "Press key or combo...";
            RecorderPrompt.Foreground = Brushes.Aqua;
            ChipsContainer.IsVisible = false;
            ChipsContainer.Children.Clear();
            ClearButton.IsVisible = false;

            RecorderBorder.BorderBrush = Brushes.Aqua;
            RecorderBorder.Background = Brush.Parse("#1A2B35");

            // Attach tunneling event handlers to the parent Window
            var topLevel = TopLevel.GetTopLevel(this) as Window;
            if (topLevel != null)
            {
                _hookedWindow = topLevel;
                _hookedWindow.AddHandler(InputElement.KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel);
                _hookedWindow.AddHandler(InputElement.KeyUpEvent, OnWindowKeyUp, RoutingStrategies.Tunnel);
            }

            RecorderBorder.Focus();
        }

        public void StopRecording(bool revert)
        {
            if (!_isRecording) return;

            _isRecording = false;

            if (_hookedWindow != null)
            {
                _hookedWindow.RemoveHandler(InputElement.KeyDownEvent, OnWindowKeyDown);
                _hookedWindow.RemoveHandler(InputElement.KeyUpEvent, OnWindowKeyUp);
                _hookedWindow = null;
            }

            if (revert)
            {
                _recordedKey = _previousKey;
            }

            _heldModifiers.Clear();
            _heldNonModifierKey = null;
            _activeModifiers = KeyModifiers.None;

            UpdateDisplay();
        }

        private void OnWindowKeyDown(object? sender, KeyEventArgs e)
        {
            if (!_isRecording) return;

            e.Handled = true;

            // Check if key is a modifier
            string? modName = GetModifierNameFromKey(e.Key);
            if (modName != null)
            {
                _heldModifiers.Add(modName);
                _activeModifiers |= e.KeyModifiers;
            }
            else
            {
                // Non-modifier key pressed (letters, numbers, Enter, Esc, F1-F12, Arrows, etc.)
                string? formattedKey = FormatKey(e.Key);
                if (formattedKey != null)
                {
                    _heldNonModifierKey = formattedKey;
                    _activeModifiers |= e.KeyModifiers;
                }
            }

            // Real-time visual feedback: render all currently held keys
            RenderCurrentlyHeldKeys();
        }

        private void OnWindowKeyUp(object? sender, KeyEventArgs e)
        {
            if (!_isRecording) return;

            e.Handled = true;

            // NEGATIVE EDGE TRIGGER:
            // When a non-modifier key was pressed and is now released (or user releases keys after press)
            if (!string.IsNullOrEmpty(_heldNonModifierKey))
            {
                var parts = BuildCurrentComboList();
                string finalShortcut = string.Join("+", parts);

                _recordedKey = finalShortcut;
                StopRecording(revert: false);
                KeyChanged?.Invoke(this, _recordedKey);
                return;
            }

            // If a single modifier key was tapped and released alone without any other key
            if (e.Key == Key.LWin || e.Key == Key.RWin)
            {
                _recordedKey = "Windows Key";
                StopRecording(revert: false);
                KeyChanged?.Invoke(this, _recordedKey);
                return;
            }

            if (e.Key == Key.Apps)
            {
                _recordedKey = "Menu Key";
                StopRecording(revert: false);
                KeyChanged?.Invoke(this, _recordedKey);
                return;
            }

            // Normal modifier release without any key press: update held state
            string? modName = GetModifierNameFromKey(e.Key);
            if (modName != null)
            {
                _heldModifiers.Remove(modName);
            }

            if (_heldModifiers.Count > 0)
            {
                RenderCurrentlyHeldKeys();
            }
            else
            {
                ChipsContainer.IsVisible = false;
                RecorderPrompt.IsVisible = true;
                RecorderPrompt.Text = "Press key or combo...";
                RecorderPrompt.Foreground = Brushes.Aqua;
            }
        }

        private void RenderCurrentlyHeldKeys()
        {
            var parts = BuildCurrentComboList();
            if (parts.Count > 0)
            {
                RenderChips(parts, isHolding: true);
            }
        }

        private List<string> BuildCurrentComboList()
        {
            var list = new List<string>();

            // Canonical modifier order: Ctrl, Alt, Shift, Win
            if ((_activeModifiers & KeyModifiers.Control) != 0 || _heldModifiers.Contains("Ctrl")) list.Add("Ctrl");
            if ((_activeModifiers & KeyModifiers.Alt) != 0 || _heldModifiers.Contains("Alt")) list.Add("Alt");
            if ((_activeModifiers & KeyModifiers.Shift) != 0 || _heldModifiers.Contains("Shift")) list.Add("Shift");
            if ((_activeModifiers & KeyModifiers.Meta) != 0 || _heldModifiers.Contains("Win")) list.Add("Win");

            if (!string.IsNullOrEmpty(_heldNonModifierKey))
            {
                list.Add(_heldNonModifierKey);
            }

            return list;
        }

        private static string? GetModifierNameFromKey(Key key)
        {
            return key switch
            {
                Key.LeftCtrl or Key.RightCtrl => "Ctrl",
                Key.LeftAlt or Key.RightAlt => "Alt",
                Key.LeftShift or Key.RightShift => "Shift",
                Key.LWin or Key.RWin => "Win",
                _ => null
            };
        }

        private static string? FormatKey(Key key)
        {
            // Letters (A-Z)
            if (key >= Key.A && key <= Key.Z)
            {
                return key.ToString().ToUpperInvariant();
            }

            // Top row numbers (0-9)
            if (key >= Key.D0 && key <= Key.D9)
            {
                return ((int)key - (int)Key.D0).ToString();
            }

            // Numpad numbers (0-9)
            if (key >= Key.NumPad0 && key <= Key.NumPad9)
            {
                return ((int)key - (int)Key.NumPad0).ToString();
            }

            // Function keys (F1 - F24)
            if (key >= Key.F1 && key <= Key.F24)
            {
                return key.ToString().ToUpperInvariant();
            }

            // Special Keys
            return key switch
            {
                Key.Return or Key.Enter => "Enter",
                Key.Escape => "Esc",
                Key.Tab => "Tab",
                Key.Space => "Space",
                Key.Back => "Backspace",
                Key.Delete => "Delete",
                Key.Insert => "Insert",
                Key.Home => "Home",
                Key.End => "End",
                Key.PageUp => "Page Up",
                Key.PageDown => "Page Down",
                Key.Up => "Arrow Up",
                Key.Down => "Arrow Down",
                Key.Left => "Arrow Left",
                Key.Right => "Arrow Right",
                Key.LWin or Key.RWin => "Windows Key",
                Key.Apps => "Menu Key",
                Key.PrintScreen => "Print Screen",
                Key.Scroll => "Scroll Lock",
                Key.Pause => "Pause",
                Key.CapsLock => "Caps Lock",
                Key.NumLock => "Num Lock",
                Key.MediaPlayPause => "Play/Pause",
                Key.MediaNextTrack => "Next Track",
                Key.MediaPreviousTrack => "Previous Track",
                Key.MediaStop => "Stop",
                Key.VolumeUp => "Volume Up",
                Key.VolumeDown => "Volume Down",
                Key.VolumeMute => "Mute",
                _ => null
            };
        }

        private void UpdateDisplay()
        {
            if (ChipsContainer == null || RecorderPrompt == null || RecorderBorder == null || ClearButton == null)
                return;

            RecorderBorder.Background = Brush.Parse("#404040");

            if (string.IsNullOrWhiteSpace(_recordedKey))
            {
                RecorderPrompt.IsVisible = true;
                RecorderPrompt.Text = "Click to record key or combo...";
                RecorderPrompt.Foreground = Brush.Parse("#AAAAAA");
                ChipsContainer.IsVisible = false;
                ChipsContainer.Children.Clear();
                ClearButton.IsVisible = false;
                RecorderBorder.BorderBrush = Brush.Parse("#707070");
            }
            else
            {
                RecorderPrompt.IsVisible = false;
                ClearButton.IsVisible = true;
                RecorderBorder.BorderBrush = Brushes.Aqua;

                var parts = _recordedKey.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                RenderChips(parts.ToList(), isHolding: false);
            }
        }

        private void RenderChips(List<string> parts, bool isHolding)
        {
            ChipsContainer.Children.Clear();
            ChipsContainer.IsVisible = true;
            RecorderPrompt.IsVisible = false;

            for (int i = 0; i < parts.Count; i++)
            {
                if (i > 0)
                {
                    ChipsContainer.Children.Add(new TextBlock
                    {
                        Text = "+",
                        Foreground = Brush.Parse("#888888"),
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                        FontWeight = FontWeight.Bold,
                        Margin = new Thickness(2, 0)
                    });
                }

                ChipsContainer.Children.Add(new Border
                {
                    Background = Brush.Parse("#1A1A1A"),
                    BorderBrush = Brushes.Aqua,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(8, 3),
                    Margin = new Thickness(2, 0),
                    Child = new TextBlock
                    {
                        Text = parts[i],
                        Foreground = Brushes.Aqua,
                        FontWeight = FontWeight.Bold,
                        FontSize = 13,
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                    }
                });
            }
        }
    }
}
