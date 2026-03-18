using FloatingClockWidget.Native.Interop;
using FloatingClockWidget.Native.Models;
using FloatingClockWidget.Native.Services;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace FloatingClockWidget.Native;

public partial class MainWindow : Window
{
    private const string AppVersion = "0.1.0";

    private readonly RuntimePaths _runtimePaths;
    private readonly ThemeService _themeService;
    private readonly DispatcherTimer _clockTimer;

    private DispatcherTimer? _locatorTimer;
    private HotKeyManager? _hotKeyManager;
    private Storyboard? _driftStoryboard;
    private ThemeMotion _currentMotion = new();

    private IReadOnlyList<ThemeSummary> _themes = [];
    private bool _isApplyingTheme;
    private bool _reducedMotion;
    private bool _liteMode;

    public MainWindow()
    {
        InitializeComponent();

        _runtimePaths = RuntimePaths.Resolve();
        _themeService = new ThemeService(_runtimePaths);

        _clockTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _clockTimer.Tick += (_, _) => UpdateClock();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        VersionText.Text = $"v{AppVersion}";
        _reducedMotion = !SystemParameters.ClientAreaAnimation;
        _liteMode = LoadLiteMode();
        ApplyPerformanceMode();

        UpdateClock();
        _clockTimer.Start();

        await _themeService.EnsureInitializedAsync(AppVersion);
        await ReloadThemesAsync(ThemeService.DefaultThemeId);

        if (_liteMode)
        {
            SetStatus("LITE mode enabled");
        }
        else
        {
            SetStatus("Ctrl+Alt+C for locator pulse");
        }
    }

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        _hotKeyManager = new HotKeyManager(this);
        var registered = _hotKeyManager.TryRegister(
            id: 1,
            modifiers: HotKeyModifiers.Control | HotKeyModifiers.Alt | HotKeyModifiers.NoRepeat,
            virtualKey: KeyInterop.VirtualKeyFromKey(Key.C));

        if (!registered)
        {
            SetStatus("Hotkey registration failed (Ctrl+Alt+C)", isError: true);
            return;
        }

        _hotKeyManager.HotKeyPressed += (_, _) =>
        {
            RunLocatorPulse();
            SetStatus("Locator pulse: Ctrl+Alt+C");
        };
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _driftStoryboard?.Stop();
        _locatorTimer?.Stop();
        _clockTimer.Stop();
        _hotKeyManager?.Dispose();
    }

    private void DragArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed)
        {
            return;
        }

        try
        {
            DragMove();
        }
        catch
        {
            // no-op
        }
    }

    private async Task ReloadThemesAsync(string? preferredThemeId = null)
    {
        _themes = await _themeService.ListThemesAsync();
        ThemeComboBox.ItemsSource = _themes;

        if (_themes.Count == 0)
        {
            SetStatus("No themes found", isError: true);
            return;
        }

        var selected = _themes.FirstOrDefault(theme => theme.Id == preferredThemeId) ?? _themes[0];
        ThemeComboBox.SelectedItem = selected;
        await ApplyThemeAsync(selected.Id);
    }

    private async void ThemeComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_isApplyingTheme)
        {
            return;
        }

        if (ThemeComboBox.SelectedItem is not ThemeSummary summary)
        {
            return;
        }

        await ApplyThemeAsync(summary.Id);
    }

    private async Task ApplyThemeAsync(string themeId)
    {
        _isApplyingTheme = true;
        try
        {
            var bundle = await _themeService.LoadThemeBundleAsync(themeId);
            ApplyTokens(bundle.Tokens);
            SetStatus($"Theme applied: {bundle.Manifest.Name}");
        }
        catch (Exception ex)
        {
            SetStatus($"Theme load failed: {ex.Message}", isError: true);
        }
        finally
        {
            _isApplyingTheme = false;
        }
    }

    private void ApplyTokens(ThemeTokens tokens)
    {
        var panelColor = ParseColor(tokens.Colors.Panel, Color.FromArgb(0xDD, 0x0B, 0x0B, 0x12));
        var textMainColor = ParseColor(tokens.Colors.TextMain, Colors.White);
        var textSubColor = ParseColor(tokens.Colors.TextSub, Color.FromRgb(156, 182, 201));
        var accentAColor = ParseColor(tokens.Colors.AccentA, Color.FromRgb(0, 229, 255));
        var accentBColor = ParseColor(tokens.Colors.AccentB, Color.FromRgb(255, 61, 255));

        PanelBorder.Background = new SolidColorBrush(panelColor);
        TimeText.Foreground = new SolidColorBrush(textMainColor);
        DateText.Foreground = new SolidColorBrush(textSubColor);
        BrandText.Foreground = new SolidColorBrush(accentAColor);
        StatusText.Foreground = new SolidColorBrush(textSubColor);
        VersionText.Foreground = new SolidColorBrush(textSubColor);
        PulseRing.BorderBrush = new SolidColorBrush(accentAColor) { Opacity = 0.65 };
        PanelBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF));
        PanelBorder.CornerRadius = new CornerRadius(Math.Clamp(tokens.Radius.Panel, 8, 64));

        PanelShadowEffect.Color = accentBColor;
        PanelShadowEffect.BlurRadius = 18;
        PanelShadowEffect.ShadowDepth = 0;
        PanelShadowEffect.Opacity = 0.24;

        TryApplyFont(TimeText, tokens.Typography.ClockFont);
        TryApplyFont(this, tokens.Typography.UiFont);

        _currentMotion = tokens.Motion;
        RestartLocatorTimer();
        RestartDriftAnimation();
        ApplyPerformanceMode();
    }

    private void RestartLocatorTimer()
    {
        _locatorTimer?.Stop();
        _locatorTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(Math.Clamp(_currentMotion.LocatorPulseSec, 2, 60)),
        };
        _locatorTimer.Tick += (_, _) => RunLocatorPulse();

        if (!ShouldDisableMotion())
        {
            _locatorTimer.Start();
        }
    }

    private void RestartDriftAnimation()
    {
        _driftStoryboard?.Stop(this);
        _driftStoryboard = null;
        PanelTranslateTransform.X = 0;
        PanelTranslateTransform.Y = 0;

        if (ShouldDisableMotion())
        {
            return;
        }

        var driftPx = Math.Clamp(_currentMotion.DriftPx, 0, 16);
        if (driftPx <= 0)
        {
            return;
        }

        var duration = TimeSpan.FromSeconds(Math.Clamp(_currentMotion.DriftCycleSec, 4, 60));

        var xAnimation = new DoubleAnimationUsingKeyFrames
        {
            Duration = duration,
            RepeatBehavior = RepeatBehavior.Forever,
        };
        xAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(-0.4 * driftPx, KeyTime.FromPercent(0.00)));
        xAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(1.0 * driftPx, KeyTime.FromPercent(0.25)));
        xAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(-0.7 * driftPx, KeyTime.FromPercent(0.50)));
        xAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(0.5 * driftPx, KeyTime.FromPercent(0.75)));
        xAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(-0.4 * driftPx, KeyTime.FromPercent(1.00)));
        Storyboard.SetTarget(xAnimation, PanelTranslateTransform);
        Storyboard.SetTargetProperty(xAnimation, new PropertyPath(TranslateTransform.XProperty));

        var yAnimation = new DoubleAnimationUsingKeyFrames
        {
            Duration = duration,
            RepeatBehavior = RepeatBehavior.Forever,
        };
        yAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(-0.2 * driftPx, KeyTime.FromPercent(0.00)));
        yAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(0.36 * driftPx, KeyTime.FromPercent(0.25)));
        yAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(0.8 * driftPx, KeyTime.FromPercent(0.50)));
        yAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(-0.8 * driftPx, KeyTime.FromPercent(0.75)));
        yAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(-0.2 * driftPx, KeyTime.FromPercent(1.00)));
        Storyboard.SetTarget(yAnimation, PanelTranslateTransform);
        Storyboard.SetTargetProperty(yAnimation, new PropertyPath(TranslateTransform.YProperty));

        _driftStoryboard = new Storyboard();
        _driftStoryboard.Children.Add(xAnimation);
        _driftStoryboard.Children.Add(yAnimation);
        _driftStoryboard.Begin(this, isControllable: true);
    }

    private void RunLocatorPulse()
    {
        if (ShouldDisableMotion())
        {
            return;
        }

        PulseRing.Opacity = 0;
        PulseScaleTransform.ScaleX = 0.94;
        PulseScaleTransform.ScaleY = 0.94;

        var pulseStoryboard = new Storyboard();

        var opacityAnimation = new DoubleAnimation
        {
            From = 0,
            To = 0.82,
            Duration = TimeSpan.FromMilliseconds(280),
            AutoReverse = true,
        };
        Storyboard.SetTarget(opacityAnimation, PulseRing);
        Storyboard.SetTargetProperty(opacityAnimation, new PropertyPath(OpacityProperty));
        pulseStoryboard.Children.Add(opacityAnimation);

        var scaleXAnimation = new DoubleAnimation
        {
            From = 0.94,
            To = 1.08,
            Duration = TimeSpan.FromMilliseconds(560),
        };
        Storyboard.SetTarget(scaleXAnimation, PulseScaleTransform);
        Storyboard.SetTargetProperty(scaleXAnimation, new PropertyPath(ScaleTransform.ScaleXProperty));
        pulseStoryboard.Children.Add(scaleXAnimation);

        var scaleYAnimation = new DoubleAnimation
        {
            From = 0.94,
            To = 1.08,
            Duration = TimeSpan.FromMilliseconds(560),
        };
        Storyboard.SetTarget(scaleYAnimation, PulseScaleTransform);
        Storyboard.SetTargetProperty(scaleYAnimation, new PropertyPath(ScaleTransform.ScaleYProperty));
        pulseStoryboard.Children.Add(scaleYAnimation);

        pulseStoryboard.Begin(this);
    }

    private void UpdateClock()
    {
        var now = DateTime.Now;
        TimeText.Text = now.ToString("HH:mm:ss");
        DateText.Text = now.ToString("yyyy-MM-dd");
    }

    private async void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Theme Zip",
            Filter = "Zip files (*.zip)|*.zip",
            CheckFileExists = true,
            Multiselect = false,
        };

        var opened = dialog.ShowDialog(this);
        if (opened != true)
        {
            return;
        }

        SetStatus("Importing theme...");
        var result = await _themeService.ImportThemeZipAsync(dialog.FileName, AppVersion);
        if (!result.Ok)
        {
            var checkSummary = string.Join(
                Environment.NewLine,
                result.Checks.Where(check => !check.Pass).Select(check => $"- {check.Label}: {check.Detail}"));
            if (string.IsNullOrWhiteSpace(checkSummary))
            {
                checkSummary = result.Message;
            }

            SetStatus($"Import failed: {result.Message}", isError: true);
            MessageBox.Show(
                this,
                checkSummary,
                "Theme Import Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        await ReloadThemesAsync(result.ThemeId);
        SetStatus($"Theme imported: {result.Manifest?.Name ?? result.ThemeId}");
    }

    private void LiteButton_Click(object sender, RoutedEventArgs e)
    {
        _liteMode = !_liteMode;
        SaveLiteMode(_liteMode);
        ApplyPerformanceMode();
        SetStatus(_liteMode ? "LITE mode enabled" : "LITE mode disabled");
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ApplyPerformanceMode()
    {
        LiteButton.Content = _liteMode ? "LITE ON" : "LITE OFF";
        SceneBackground.Opacity = _liteMode ? 0.70 : 0.95;

        if (_liteMode)
        {
            PanelBorder.Effect = null;
            _driftStoryboard?.Pause(this);
            _locatorTimer?.Stop();
        }
        else
        {
            if (PanelBorder.Effect is null)
            {
                PanelBorder.Effect = PanelShadowEffect;
            }

            if (!ShouldDisableMotion())
            {
                _driftStoryboard?.Resume(this);
                _locatorTimer?.Start();
            }
        }

        RestartDriftAnimation();
        RestartLocatorTimer();
    }

    private bool ShouldDisableMotion()
    {
        return _reducedMotion || _liteMode;
    }

    private void SetStatus(string message, bool isError = false)
    {
        StatusText.Text = message;
        StatusText.Foreground = isError
            ? new SolidColorBrush(Color.FromRgb(255, 158, 197))
            : new SolidColorBrush(ParseColor("#9CB6C9", Color.FromRgb(156, 182, 201)));
    }

    private static Color ParseColor(string value, Color fallback)
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(value);
            return color;
        }
        catch
        {
            return fallback;
        }
    }

    private static void TryApplyFont(DependencyObject element, string fontName)
    {
        if (string.IsNullOrWhiteSpace(fontName))
        {
            return;
        }

        try
        {
            var fontFamily = new FontFamily(fontName);
            switch (element)
            {
                case System.Windows.Controls.Control control:
                    control.FontFamily = fontFamily;
                    break;
                case System.Windows.Controls.TextBlock textBlock:
                    textBlock.FontFamily = fontFamily;
                    break;
            }
        }
        catch
        {
            // no-op
        }
    }

    private sealed class LocalSettings
    {
        public bool LiteMode { get; set; }
    }

    private bool LoadLiteMode()
    {
        try
        {
            var settingsPath = Path.Combine(_runtimePaths.RootDir, "settings.json");
            if (!File.Exists(settingsPath))
            {
                return Environment.ProcessorCount <= 4;
            }

            var source = File.ReadAllText(settingsPath);
            var settings = JsonSerializer.Deserialize<LocalSettings>(source);
            return settings?.LiteMode ?? false;
        }
        catch
        {
            return Environment.ProcessorCount <= 4;
        }
    }

    private void SaveLiteMode(bool value)
    {
        try
        {
            Directory.CreateDirectory(_runtimePaths.RootDir);
            var settingsPath = Path.Combine(_runtimePaths.RootDir, "settings.json");
            var source = JsonSerializer.Serialize(new LocalSettings { LiteMode = value });
            File.WriteAllText(settingsPath, source);
        }
        catch
        {
            // no-op
        }
    }
}
