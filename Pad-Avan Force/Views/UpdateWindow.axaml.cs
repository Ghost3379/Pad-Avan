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
            
            // Automatically check for updates when window opens
            this.Opened += async (s, e) =>
            {
                if (DataContext is UpdateWindowViewModel vm)
                {
                    await System.Threading.Tasks.Task.Delay(400);
                    await vm.CheckForUpdatesCommand.ExecuteAsync(null);
                }
            };
        }
    }
}

