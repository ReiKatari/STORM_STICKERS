using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Input;
using Microsoft.UI.Xaml;

namespace STORM_STICKERS
{
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        public RelayCommand(Action execute) => _execute = execute;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute();
#pragma warning disable CS0067
        public event EventHandler? CanExecuteChanged;
#pragma warning restore CS0067
    }

    public sealed partial class MainWindow : Window
    {
        public static MainWindow? Instance { get; private set; }
        private bool _isClosingFromTray = false;

        public MainWindow()
        {
            Instance = this;
            InitializeComponent();

            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);

            try
            {
                AppWindow.SetIcon("Assets/AppIcon.ico");
            }
            catch { }

            // Navigate the root frame to the main page on startup.
            RootFrame.Navigate(typeof(MainPage));

            // Set up tray double-click command
            MyTrayIcon.DoubleClickCommand = new RelayCommand(RestoreWindow);

            // Intercept standard window close to hide to tray
            AppWindow.Closing += AppWindow_Closing;
            this.Closed += MainWindow_Closed;
        }

        private void AppWindow_Closing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
        {
            if (!_isClosingFromTray)
            {
                args.Cancel = true; // Intercept close
                HideWindow();
            }
        }

        public void RestoreWindow()
        {
            this.AppWindow.Show();
            
            // Focus and bring to front
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            User32.ShowWindow(hwnd, User32.SW_RESTORE);
            User32.SetForegroundWindow(hwnd);
        }

        public void HideWindow()
        {
            this.AppWindow.Hide();
            
            try
            {
                // Show notification using H.NotifyIcon's helper
                MyTrayIcon.ShowNotification("STORM STICKERS", "Приложение свернуто в трей");
            }
            catch { }
        }

        private void TrayOpen_Click(object sender, RoutedEventArgs e)
        {
            RestoreWindow();
        }

        private void TrayExit_Click(object sender, RoutedEventArgs e)
        {
            _isClosingFromTray = true;
            try
            {
                MyTrayIcon.Dispose();
            }
            catch { }
            this.Close();
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            try
            {
                MyTrayIcon.Dispose();
            }
            catch { }
        }

        private static class User32
        {
            [DllImport("user32.dll")]
            public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

            [DllImport("user32.dll")]
            public static extern bool SetForegroundWindow(IntPtr hWnd);

            public const int SW_RESTORE = 9;
        }
    }
}
