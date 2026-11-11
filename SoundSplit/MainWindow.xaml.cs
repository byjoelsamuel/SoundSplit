using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using SoundSplit.ViewModels;

namespace SoundSplit
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm;
        private readonly App _app;
        private bool _toldAboutTray;

        internal MainWindow(MainViewModel vm, App app)
        {
            InitializeComponent();
            _vm = vm;
            _app = app;
            DataContext = vm;

            SourceInitialized += (_, _) => RoundCorners();
            StateChanged += (_, _) =>
                // A borderless window overhangs the screen by a few pixels when maximised; pad it back in.
                Frame.Margin = WindowState == WindowState.Maximized ? new Thickness(7) : new Thickness(0);
            KeyDown += (_, e) => { if (e.Key == Key.Escape) _vm.CloseOverlaysCommand.Execute(null); };
            Closing += OnClosing;
        }

        private void OnClosing(object? sender, CancelEventArgs e)
        {
            if (_app.IsExiting) return;
            if (_vm.CloseToTray)
            {
                e.Cancel = true;
                Hide();
                if (!_toldAboutTray) { _toldAboutTray = true; _app.ShowTrayHint(); }
                return;
            }
            e.Cancel = true;
            _app.ExitApp();
        }

        private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

        private void OnMinimizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void OnBackdropClick(object sender, MouseButtonEventArgs e) => _vm.CloseOverlaysCommand.Execute(null);

        private void RoundCorners()
        {
            // Windows 11 rounded corners + shadow for our custom (borderless) window. Ignored on Windows 10.
            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                var round = 2; // DWMWCP_ROUND
                DwmSetWindowAttribute(hwnd, 33 /* DWMWA_WINDOW_CORNER_PREFERENCE */, ref round, sizeof(int));
            }
            catch { }
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    }
}
