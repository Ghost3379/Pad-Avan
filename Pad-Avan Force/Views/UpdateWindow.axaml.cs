using Avalonia.Controls;
using Avalonia.Interactivity;
using PadAwan_Force.ViewModels;
using System;

namespace PadAwan_Force.Views
{
    public partial class UpdateWindow : Window
    {
        public UpdateWindow()
        {
            InitializeComponent();
            
            // Refresh device firmware version when window opens
            this.Opened += async (s, e) =>
            {
                if (DataContext is UpdateWindowViewModel vm)
                {
                    // Small delay to ensure connection is ready
                    await System.Threading.Tasks.Task.Delay(500);
                    await vm.LoadDeviceFirmwareVersionAsync();
                }
            };
        }

        private void CloseButton_Click(object? sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}

