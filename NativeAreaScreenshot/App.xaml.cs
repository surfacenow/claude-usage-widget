using System;
using System.Windows;
using System.Windows.Input;
using Hardcodet.Wpf.TaskbarNotification;
using NHotkey;
using NHotkey.Wpf;

namespace NativeAreaScreenshot
{
    public partial class App : Application
    {
        private TaskbarIcon _notifyIcon;
        private MainWindow _currentOverlay;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // Create the context menu
            var contextMenu = new System.Windows.Controls.ContextMenu();
            
            var captureMenuItem = new System.Windows.Controls.MenuItem { Header = "スクリーンショットを撮る (Ctrl+Shift+S)" };
            captureMenuItem.Click += (s, ev) => StartCapture();
            
            var settingsMenuItem = new System.Windows.Controls.MenuItem { Header = "設定..." };
            settingsMenuItem.Click += (s, ev) => ShowSettings();
            
            contextMenu.Items.Add(captureMenuItem);
            contextMenu.Items.Add(settingsMenuItem);
            contextMenu.Items.Add(new System.Windows.Controls.Separator());
            
            var exitMenuItem = new System.Windows.Controls.MenuItem { Header = "終了" };
            exitMenuItem.Click += (s, ev) => Shutdown();
            contextMenu.Items.Add(exitMenuItem);

            // Initialize the NotifyIcon
            _notifyIcon = new TaskbarIcon
            {
                IconContent = "✂️", // Could use a real icon .ico file, using emoji text for simplicity in PoC
                ToolTipText = "Area Screenshot (Ctrl+Shift+Sで起動)",
                ContextMenu = contextMenu
            };
            
            // Double click opens settings
            _notifyIcon.TrayMouseDoubleClick += (s, ev) => ShowSettings();

            // Register global hotkey: Ctrl + Shift + S
            try
            {
                HotkeyManager.Current.AddOrReplace("StartCapture", Key.S, ModifierKeys.Control | ModifierKeys.Shift, OnScreenshotHotkey);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"ホットキーの登録に失敗しました:\n{ex.Message}", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        
        private void OnScreenshotHotkey(object sender, HotkeyEventArgs e)
        {
            StartCapture();
            e.Handled = true;
        }

        private void StartCapture()
        {
            if (_currentOverlay != null && _currentOverlay.IsVisible)
            {
                // Already capturing
                return;
            }

            _currentOverlay = new MainWindow();
            _currentOverlay.Show();
        }

        private void ShowSettings()
        {
            var settingsWin = new SettingsWindow();
            settingsWin.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _notifyIcon?.Dispose();
            base.OnExit(e);
        }
    }
}

