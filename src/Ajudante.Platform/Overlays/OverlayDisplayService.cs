using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaColor = System.Windows.Media.Color;
using MediaColorConverter = System.Windows.Media.ColorConverter;
using WpfApplication = System.Windows.Application;
using WpfFontFamily = System.Windows.Media.FontFamily;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfImage = System.Windows.Controls.Image;
using WpfVerticalAlignment = System.Windows.VerticalAlignment;

namespace Ajudante.Platform.Overlays;

public sealed record OverlayBounds(int X, int Y, int Width, int Height, bool FullScreen);

public record OverlayDisplayOptions
{
    public OverlayBounds Bounds { get; init; } = new(0, 0, 640, 360, true);
    public int DurationMs { get; init; } = 1000;
    public bool WaitForClose { get; init; } = true;
    public bool TopMost { get; init; } = true;
    public bool ClickThrough { get; init; } = true;
    public double Opacity { get; init; } = 1;
    public string BackgroundColor { get; init; } = "#000000";
    public string Motion { get; init; } = "none";
    public int FadeInMs { get; init; } = 120;
    public int FadeOutMs { get; init; } = 120;
}

public sealed record OverlayTextOptions : OverlayDisplayOptions
{
    public string Text { get; init; } = "";
    public string FontFamily { get; init; } = "Segoe UI";
    public double FontSize { get; init; } = 48;
    public string TextColor { get; init; } = "#FFFFFF";
    public string HorizontalAlign { get; init; } = "center";
    public string VerticalAlign { get; init; } = "center";
    public string Effect { get; init; } = "shadow";
}

public sealed record OverlayImageOptions : OverlayDisplayOptions
{
    public string ImagePath { get; init; } = "";
    public string Fit { get; init; } = "contain";
}

public static class OverlayDisplayService
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;

    public static Task ShowColorAsync(OverlayDisplayOptions options, CancellationToken cancellationToken)
    {
        return ShowOverlayAsync(options, () => new Border
        {
            Background = CreateBrush(options.BackgroundColor, Colors.Black),
            HorizontalAlignment = WpfHorizontalAlignment.Stretch,
            VerticalAlignment = WpfVerticalAlignment.Stretch
        }, cancellationToken);
    }

    public static Task ShowTextAsync(OverlayTextOptions options, CancellationToken cancellationToken)
    {
        return ShowOverlayAsync(options, () =>
        {
            var textBlock = new TextBlock
            {
                Text = options.Text,
                FontFamily = new WpfFontFamily(options.FontFamily),
                FontSize = Math.Max(1, options.FontSize),
                Foreground = CreateBrush(options.TextColor, Colors.White),
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = ResolveTextAlignment(options.HorizontalAlign),
                HorizontalAlignment = ResolveHorizontalAlignment(options.HorizontalAlign),
                VerticalAlignment = ResolveVerticalAlignment(options.VerticalAlign),
                Margin = new Thickness(32)
            };

            if (!string.Equals(options.Effect, "none", StringComparison.OrdinalIgnoreCase))
            {
                textBlock.Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = string.Equals(options.Effect, "outline", StringComparison.OrdinalIgnoreCase) ? 10 : 18,
                    ShadowDepth = string.Equals(options.Effect, "outline", StringComparison.OrdinalIgnoreCase) ? 0 : 3,
                    Opacity = 0.85
                };
            }

            return textBlock;
        }, cancellationToken);
    }

    public static Task ShowImageAsync(OverlayImageOptions options, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.ImagePath))
            throw new ArgumentException("Image path is required.", nameof(options));

        if (!File.Exists(options.ImagePath))
            throw new FileNotFoundException("Overlay image was not found.", options.ImagePath);

        return ShowOverlayAsync(options, () =>
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(options.ImagePath, UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();

            return new WpfImage
            {
                Source = bitmap,
                Stretch = ResolveStretch(options.Fit),
                HorizontalAlignment = WpfHorizontalAlignment.Stretch,
                VerticalAlignment = WpfVerticalAlignment.Stretch
            };
        }, cancellationToken);
    }

    private static async Task ShowOverlayAsync(
        OverlayDisplayOptions options,
        Func<UIElement> contentFactory,
        CancellationToken cancellationToken)
    {
        var dispatcher = WpfApplication.Current?.Dispatcher;
        if (dispatcher is null)
            throw new InvalidOperationException("A WPF dispatcher is required to show overlays.");

        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await dispatcher.InvokeAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var window = CreateOverlayWindow(options);
            var root = new Grid
            {
                Background = CreateBrush(options.BackgroundColor, Colors.Transparent)
            };
            root.Children.Add(contentFactory());
            window.Content = root;

            DispatcherTimer? closeTimer = null;
            CancellationTokenRegistration cancellationRegistration = default;

            window.SourceInitialized += (_, _) =>
            {
                if (options.ClickThrough)
                    MakeClickThrough(window);
            };

            window.Closed += (_, _) =>
            {
                closeTimer?.Stop();
                cancellationRegistration.Dispose();
                closed.TrySetResult();
            };

            if (options.DurationMs > 0)
            {
                closeTimer = new DispatcherTimer(DispatcherPriority.Normal, dispatcher)
                {
                    Interval = TimeSpan.FromMilliseconds(options.DurationMs)
                };
                closeTimer.Tick += (_, _) =>
                {
                    closeTimer.Stop();
                    CloseWithFade(window, options.FadeOutMs);
                };
                closeTimer.Start();
            }

            cancellationRegistration = cancellationToken.Register(() =>
            {
                dispatcher.BeginInvoke(() =>
                {
                    if (window.IsVisible)
                        window.Close();
                });
            });

            window.Show();
            ApplyMotion(window, options);
        });

        if (options.WaitForClose)
            await closed.Task.WaitAsync(cancellationToken);
    }

    private static Window CreateOverlayWindow(OverlayDisplayOptions options)
    {
        var window = new Window
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = MediaBrushes.Transparent,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            Topmost = options.TopMost,
            Opacity = Math.Clamp(options.Opacity, 0.05, 1.0),
            Focusable = false,
            ShowActivated = false
        };

        if (options.Bounds.FullScreen)
        {
            window.Left = SystemParameters.VirtualScreenLeft;
            window.Top = SystemParameters.VirtualScreenTop;
            window.Width = SystemParameters.VirtualScreenWidth;
            window.Height = SystemParameters.VirtualScreenHeight;
        }
        else
        {
            window.Left = options.Bounds.X;
            window.Top = options.Bounds.Y;
            window.Width = Math.Max(1, options.Bounds.Width);
            window.Height = Math.Max(1, options.Bounds.Height);
        }

        return window;
    }

    private static void ApplyMotion(Window window, OverlayDisplayOptions options)
    {
        var fadeInMs = Math.Max(0, options.FadeInMs);
        if (fadeInMs > 0)
        {
            var finalOpacity = window.Opacity;
            window.Opacity = 0;
            window.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(finalOpacity, TimeSpan.FromMilliseconds(fadeInMs))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
        }

        var motion = options.Motion.Trim().ToLowerInvariant();
        if (motion is not ("slideup" or "slidedown" or "slideleft" or "slideright"))
            return;

        const double distance = 80;
        var finalLeft = window.Left;
        var finalTop = window.Top;

        switch (motion)
        {
            case "slideup":
                window.Top = finalTop + distance;
                window.BeginAnimation(Window.TopProperty, CreatePositionAnimation(finalTop, fadeInMs));
                break;
            case "slidedown":
                window.Top = finalTop - distance;
                window.BeginAnimation(Window.TopProperty, CreatePositionAnimation(finalTop, fadeInMs));
                break;
            case "slideleft":
                window.Left = finalLeft + distance;
                window.BeginAnimation(Window.LeftProperty, CreatePositionAnimation(finalLeft, fadeInMs));
                break;
            case "slideright":
                window.Left = finalLeft - distance;
                window.BeginAnimation(Window.LeftProperty, CreatePositionAnimation(finalLeft, fadeInMs));
                break;
        }
    }

    private static DoubleAnimation CreatePositionAnimation(double to, int durationMs)
    {
        return new DoubleAnimation(to, TimeSpan.FromMilliseconds(Math.Max(120, durationMs)))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
    }

    private static void CloseWithFade(Window window, int fadeOutMs)
    {
        if (fadeOutMs <= 0)
        {
            window.Close();
            return;
        }

        var animation = new DoubleAnimation(0, TimeSpan.FromMilliseconds(fadeOutMs));
        animation.Completed += (_, _) => window.Close();
        window.BeginAnimation(UIElement.OpacityProperty, animation);
    }

    private static MediaBrush CreateBrush(string value, MediaColor fallback)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                var color = (MediaColor)MediaColorConverter.ConvertFromString(value);
                return new SolidColorBrush(color);
            }
        }
        catch
        {
            // Use fallback below.
        }

        return new SolidColorBrush(fallback);
    }

    private static WpfHorizontalAlignment ResolveHorizontalAlignment(string value) => value.Trim().ToLowerInvariant() switch
    {
        "left" => WpfHorizontalAlignment.Left,
        "right" => WpfHorizontalAlignment.Right,
        "stretch" => WpfHorizontalAlignment.Stretch,
        _ => WpfHorizontalAlignment.Center
    };

    private static WpfVerticalAlignment ResolveVerticalAlignment(string value) => value.Trim().ToLowerInvariant() switch
    {
        "top" => WpfVerticalAlignment.Top,
        "bottom" => WpfVerticalAlignment.Bottom,
        "stretch" => WpfVerticalAlignment.Stretch,
        _ => WpfVerticalAlignment.Center
    };

    private static TextAlignment ResolveTextAlignment(string value) => value.Trim().ToLowerInvariant() switch
    {
        "left" => TextAlignment.Left,
        "right" => TextAlignment.Right,
        _ => TextAlignment.Center
    };

    private static Stretch ResolveStretch(string value) => value.Trim().ToLowerInvariant() switch
    {
        "cover" => Stretch.UniformToFill,
        "stretch" => Stretch.Fill,
        "none" => Stretch.None,
        _ => Stretch.Uniform
    };

    private static void MakeClickThrough(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
            return;

        var style = GetWindowLongPtr(handle, GwlExStyle);
        SetWindowLongPtr(handle, GwlExStyle, new IntPtr(style.ToInt64() | WsExTransparent | WsExToolWindow));
    }

    private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
    {
        return IntPtr.Size == 8
            ? GetWindowLongPtr64(hWnd, nIndex)
            : new IntPtr(GetWindowLong32(hWnd, nIndex));
    }

    private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
    {
        return IntPtr.Size == 8
            ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong)
            : new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
}
