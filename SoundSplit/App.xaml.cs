using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using SoundSplit.Core;
using SoundSplit.ViewModels;
using WinForms = System.Windows.Forms;

namespace SoundSplit
{
    public partial class App : System.Windows.Application
    {
        private const string MutexName = @"Local\SoundSplit.SingleInstance";
        private const string ShowEventName = @"Local\SoundSplit.ShowWindow";

        private Mutex? _mutex;
        private EventWaitHandle? _showSignal;
        private WinForms.NotifyIcon? _tray;
        private WinForms.ToolStripMenuItem? _trayToggle;
        private MainViewModel? _vm;
        private global::SoundSplit.MainWindow? _window;

        internal bool IsExiting { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            // Only one SoundSplit at a time: a second launch just brings the first one to the front.
            _mutex = new Mutex(true, MutexName, out var isFirst);
            if (!isFirst)
            {
                try { EventWaitHandle.OpenExisting(ShowEventName).Set(); } catch { }
                Shutdown();
                return;
            }
            _showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
            new Thread(() =>
            {
                while (_showSignal.WaitOne())
                {
                    if (IsExiting) break;
                    Dispatcher.BeginInvoke(ShowMain);
                }
            }) { IsBackground = true, Name = "SoundSplit show-signal" }.Start();

            DispatcherUnhandledException += OnUnhandled;
            base.OnStartup(e);

            var demo = e.Args.Any(a => a.Equals("--demo", StringComparison.OrdinalIgnoreCase));
            // Demo mode never touches your real saved settings.
            var settings = demo ? new AppSettings { DoNotSave = true } : AppSettings.Load();
            ThemeManager.Apply(settings.Theme);

            _vm = new MainViewModel(settings, demo);
            _vm.EnabledChanged += (_, _) => UpdateTray();
            _window = new global::SoundSplit.MainWindow(_vm, this);
            MainWindow = _window;
            CreateTray();

            var startHidden = e.Args.Any(a => a.Equals("--tray", StringComparison.OrdinalIgnoreCase));
            if (!startHidden) _window.Show();
        }

        private void CreateTray()
        {
            var menu = new WinForms.ContextMenuStrip();
            menu.Items.Add("Open SoundSplit", null, (_, _) => ShowMain());
            _trayToggle = new WinForms.ToolStripMenuItem("Routing: on");
            _trayToggle.Click += (_, _) => { if (_vm != null) _vm.IsEnabled = !_vm.IsEnabled; };
            menu.Items.Add(_trayToggle);
            menu.Items.Add(new WinForms.ToolStripSeparator());
            menu.Items.Add("Quit", null, (_, _) => ExitApp());

            _tray = new WinForms.NotifyIcon
            {
                Text = "SoundSplit",
                Icon = LoadTrayIcon(),
                ContextMenuStrip = menu,
                Visible = true,
            };
            _tray.MouseClick += (_, args) => { if (args.Button == WinForms.MouseButtons.Left) ShowMain(); };
            UpdateTray();
        }

        private static System.Drawing.Icon LoadTrayIcon()
        {
            try
            {
                var info = GetResourceStream(new Uri("pack://application:,,,/Assets/SoundSplit.ico"));
                if (info != null)
                {
                    using var s = info.Stream;
                    return new System.Drawing.Icon(s, WinForms.SystemInformation.SmallIconSize);
                }
            }
            catch { }
            return System.Drawing.SystemIcons.Application;
        }

        private void UpdateTray()
        {
            if (_vm == null || _tray == null || _trayToggle == null) return;
            _trayToggle.Text = _vm.IsEnabled ? "Turn routing off" : "Turn routing on";
            _trayToggle.Checked = _vm.IsEnabled;
            _tray.Text = _vm.IsEnabled ? "SoundSplit: on" : "SoundSplit: off";
        }

        internal void ShowTrayHint()
        {
            _tray?.ShowBalloonTip(3000, "SoundSplit is still running",
                "Routing stays active. Click the tray icon to reopen, or right-click to quit.",
                WinForms.ToolTipIcon.Info);
        }

        internal void ShowMain()
        {
            if (_window == null || IsExiting) return;
            _window.Show();
            if (_window.WindowState == WindowState.Minimized) _window.WindowState = WindowState.Normal;
            _window.Activate();
            _window.Topmost = true;
            _window.Topmost = false;
            _window.Focus();
        }

        internal void ExitApp()
        {
            if (IsExiting) return;
            IsExiting = true;
            if (_tray != null) { _tray.Visible = false; _tray.Dispose(); _tray = null; }
            _vm?.Dispose();
            _showSignal?.Set();
            Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_tray != null) { _tray.Visible = false; _tray.Dispose(); }
            try { _mutex?.ReleaseMutex(); } catch { }
            base.OnExit(e);
        }

        private void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            try
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SoundSplit");
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "error.log"), $"[{DateTime.Now:u}] {e.Exception}\n\n");
            }
            catch { }
            _vm?.Toast("Something went wrong. Details were written to error.log.");
            e.Handled = true;
        }
    }
}
