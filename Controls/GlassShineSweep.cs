using System;
using System.Diagnostics;
using System.Threading;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace ClassIsland.LiquidGlass.Controls;

/// <summary>
/// 周期性地在父容器内扫过一道斜向高光。全部动画逻辑在代码中完成，
/// 避免在主题 XAML 中依赖 <c>x:Name</c> 定位变换元素。
/// </summary>
public class GlassShineSweep : Panel
{
    /// <summary>
    /// 扫光带宽度。
    /// </summary>
    public const double SweepWidth = 240;

    /// <summary>
    /// 扫光带倾斜角度。
    /// </summary>
    public const double SweepAngle = 14;

    /// <summary>
    /// 单次扫过周期（含停顿）。
    /// </summary>
    public static readonly TimeSpan SweepCycleDuration = TimeSpan.FromSeconds(6.5);

    /// <summary>
    /// 扫过段占整个周期的比例，其余时间为停顿。
    /// </summary>
    private const double SweepSegment = 0.18;

    private static readonly CubicEaseInOut SweepEasing = new();

    private readonly Border _sweep;
    private readonly TranslateTransform _translate;
    private CancellationTokenSource? _cts;
    private DispatcherTimer? _sweepTimer;
    private bool _attached;
    private double _lastSweepWidth = double.NaN;

    static GlassShineSweep()
    {
        // 类处理器只需注册一次，避免每个实例重复订阅导致处理器堆积。
        IsVisibleProperty.Changed.AddClassHandler<GlassShineSweep>((o, _) => o.OnIsVisibleChanged());
        BoundsProperty.Changed.AddClassHandler<GlassShineSweep>((o, _) => o.OnBoundsChanged());
    }

    public GlassShineSweep()
    {
        IsHitTestVisible = false;
        ClipToBounds = true;

        _translate = new TranslateTransform();
        _sweep = new Border
        {
            Width = SweepWidth,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Stretch,
            IsHitTestVisible = false,
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF), 0),
                    new GradientStop(Color.FromArgb(0x2A, 0xFF, 0xFF, 0xFF), 0.35),
                    new GradientStop(Color.FromArgb(0x42, 0xFF, 0xFF, 0xFF), 0.5),
                    new GradientStop(Color.FromArgb(0x2A, 0xFF, 0xFF, 0xFF), 0.65),
                    new GradientStop(Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF), 1),
                }
            },
            RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
            RenderTransform = new TransformGroup
            {
                Children =
                {
                    _translate,
                    new RotateTransform(SweepAngle),
                }
            },
        };
        Children.Add(_sweep);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        UpdateSweepState();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        StopSweep();
    }

    private void OnIsVisibleChanged()
    {
        if (_attached)
        {
            UpdateSweepState();
        }
    }

    private void OnBoundsChanged()
    {
        if (_attached)
        {
            UpdateSweepState();
        }
    }

    private void UpdateSweepState()
    {
        if (Design.IsDesignMode)
        {
            return;
        }

        if (IsEffectivelyVisible && Bounds.Width > 0)
        {
            // 尺寸持续变化时（如窗口缩放、宽度过渡动画）不重启扫光，
            // 仅在宽度发生有效变化或尚未启动时才重新开始。
            if (_cts == null || Math.Abs(Bounds.Width - _lastSweepWidth) > 0.5)
            {
                _lastSweepWidth = Bounds.Width;
                StartSweep();
            }
        }
        else
        {
            StopSweep();
        }
    }

    private void StartSweep()
    {
        StopSweep();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        var width = Bounds.Width;
        var start = -SweepWidth * 1.4;
        var end = width + SweepWidth * 1.4;
        var durationMs = SweepCycleDuration.TotalMilliseconds;
        var stopwatch = Stopwatch.StartNew();

        // Avalonia 的动画系统无法通过公开 API 启动无限循环动画
        // （RunAsync 对 IterationCount.Infinite 抛异常，IAnimation.Apply 为
        // internal，且 TransformAnimator 要求动画目标为 Visual 而非 Transform），
        // 因此改用 DispatcherTimer 手动驱动 TranslateTransform.X，效果一致且
        // 不依赖任何 internal API，跨版本稳定。
        var timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16),
        };
        timer.Tick += (_, _) =>
        {
            if (token.IsCancellationRequested)
            {
                timer.Stop();
                return;
            }

            try
            {
                var t = (stopwatch.Elapsed.TotalMilliseconds % durationMs) / durationMs;
                _translate.X = t < SweepSegment
                    ? start + (end - start) * SweepEasing.Ease(t / SweepSegment)
                    : end;
            }
            catch (Exception)
            {
                // 定时器异常只影响扫光效果，绝不能崩溃应用。
                timer.Stop();
            }
        };
        timer.Start();
        _sweepTimer = timer;
    }

    private void StopSweep()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        if (_sweepTimer != null)
        {
            _sweepTimer.Stop();
            _sweepTimer = null;
        }
    }
}