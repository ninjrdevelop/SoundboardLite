using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using SoundboardLite.Core.Models;

namespace SoundboardLite.App.Controls;

/// <summary>
/// Draws a clip's peak envelope and two draggable trim handles. Fully custom-rendered (no
/// ControlTemplate) since it's just a waveform + two vertical lines — a <see cref="FrameworkElement"/>
/// with an <see cref="OnRender"/> override is simpler here than a templated <c>Control</c>.
/// </summary>
public sealed class WaveformControl : FrameworkElement
{
    private const double HandleHitTestWidth = 8;

    public static readonly DependencyProperty PeaksProperty = DependencyProperty.Register(
        nameof(Peaks), typeof(PeakData), typeof(WaveformControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public PeakData? Peaks
    {
        get => (PeakData?)GetValue(PeaksProperty);
        set => SetValue(PeaksProperty, value);
    }

    public static readonly DependencyProperty TotalDurationMsProperty = DependencyProperty.Register(
        nameof(TotalDurationMs), typeof(double), typeof(WaveformControl),
        new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public double TotalDurationMs
    {
        get => (double)GetValue(TotalDurationMsProperty);
        set => SetValue(TotalDurationMsProperty, value);
    }

    public static readonly DependencyProperty TrimStartMsProperty = DependencyProperty.Register(
        nameof(TrimStartMs), typeof(double), typeof(WaveformControl),
        new FrameworkPropertyMetadata(
            0.0,
            FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public double TrimStartMs
    {
        get => (double)GetValue(TrimStartMsProperty);
        set => SetValue(TrimStartMsProperty, value);
    }

    public static readonly DependencyProperty TrimEndMsProperty = DependencyProperty.Register(
        nameof(TrimEndMs), typeof(double), typeof(WaveformControl),
        new FrameworkPropertyMetadata(
            0.0,
            FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public double TrimEndMs
    {
        get => (double)GetValue(TrimEndMsProperty);
        set => SetValue(TrimEndMsProperty, value);
    }

    private bool _draggingStart;
    private bool _draggingEnd;

    private static readonly SolidColorBrush BackgroundBrush = Freeze(Color.FromRgb(30, 30, 30));
    private static readonly SolidColorBrush WaveBrush = Freeze(Color.FromRgb(90, 170, 250));
    private static readonly SolidColorBrush DimBrush = Freeze(Color.FromArgb(160, 0, 0, 0));
    private static readonly SolidColorBrush HandleBrush = Freeze(Color.FromRgb(255, 200, 0));

    private static SolidColorBrush Freeze(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    public WaveformControl()
    {
        Focusable = true;
        IsHitTestVisible = true;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 400 : availableSize.Width;
        var height = double.IsInfinity(availableSize.Height) ? 80 : availableSize.Height;
        return new Size(width, height);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= 0 || height <= 0) return;

        dc.DrawRectangle(BackgroundBrush, null, new Rect(0, 0, width, height));

        var peaks = Peaks;
        if (peaks is not null && peaks.PeakCount > 0)
        {
            var mid = height / 2;
            var count = peaks.PeakCount;
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(new Point(0, mid), isFilled: true, isClosed: true);
                for (var i = 0; i < count; i++)
                {
                    var x = width * i / count;
                    var y = mid - (peaks.Max[i] * mid);
                    ctx.LineTo(new Point(x, y), isStroked: true, isSmoothJoin: false);
                }
                for (var i = count - 1; i >= 0; i--)
                {
                    var x = width * i / count;
                    var y = mid - (peaks.Min[i] * mid);
                    ctx.LineTo(new Point(x, y), isStroked: true, isSmoothJoin: false);
                }
            }
            geometry.Freeze();
            dc.DrawGeometry(WaveBrush, null, geometry);
        }

        var total = Math.Max(1.0, TotalDurationMs);
        var startX = Math.Clamp(width * (TrimStartMs / total), 0, width);
        var endX = Math.Clamp(width * (TrimEndMs / total), 0, width);

        if (startX > 0) dc.DrawRectangle(DimBrush, null, new Rect(0, 0, startX, height));
        if (endX < width) dc.DrawRectangle(DimBrush, null, new Rect(endX, 0, width - endX, height));

        dc.DrawRectangle(HandleBrush, null, new Rect(Math.Max(0, startX - 1), 0, 2, height));
        dc.DrawRectangle(HandleBrush, null, new Rect(Math.Max(0, endX - 1), 0, 2, height));
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (ActualWidth <= 0) return;

        var x = e.GetPosition(this).X;
        var total = Math.Max(1.0, TotalDurationMs);
        var startX = ActualWidth * (TrimStartMs / total);
        var endX = ActualWidth * (TrimEndMs / total);

        if (Math.Abs(x - startX) <= HandleHitTestWidth)
        {
            _draggingStart = true;
            CaptureMouse();
        }
        else if (Math.Abs(x - endX) <= HandleHitTestWidth)
        {
            _draggingEnd = true;
            CaptureMouse();
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_draggingStart && !_draggingEnd) return;
        if (ActualWidth <= 0) return;

        var x = Math.Clamp(e.GetPosition(this).X, 0, ActualWidth);
        var total = Math.Max(1.0, TotalDurationMs);
        var ms = total * (x / ActualWidth);

        if (_draggingStart)
        {
            TrimStartMs = Math.Clamp(ms, 0, TrimEndMs - 1);
        }
        else if (_draggingEnd)
        {
            TrimEndMs = Math.Clamp(ms, TrimStartMs + 1, total);
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (!_draggingStart && !_draggingEnd) return;

        _draggingStart = false;
        _draggingEnd = false;
        ReleaseMouseCapture();
    }
}
