using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
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

    private readonly Border _sweep;
    private readonly TranslateTransform _translate;
    private CancellationTokenSource? _cts;
    private bool _attached;

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
                    new GradientStop(Color.FromArgb(0x2E, 0xFF, 0xFF, 0xFF), 0.5),
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

        IsVisibleProperty.Changed.AddClassHandler<GlassShineSweep>((o, _) => o.OnIsVisibleChanged());
        BoundsProperty.Changed.AddClassHandler<GlassShineSweep>((o, _) => o.OnBoundsChanged());
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
            StartSweep();
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
        var translate = _translate;
        var width = Bounds.Width;
        var start = -SweepWidth * 1.4;
        var end = width + SweepWidth * 1.4;

        var animation = new Animation
        {
            Duration = SweepCycleDuration,
            IterationCount = IterationCount.Infinite,
            Easing = new CubicEaseInOut(),
        };
        animation.Children.Add(new KeyFrame
        {
            KeyTime = TimeSpan.Zero,
            Setters = { new Setter(TranslateTransform.XProperty, start) },
        });
        animation.Children.Add(new KeyFrame
        {
            KeyTime = TimeSpan.FromSeconds(SweepCycleDuration.TotalSeconds * 0.18),
            Setters = { new Setter(TranslateTransform.XProperty, end) },
        });
        animation.Children.Add(new KeyFrame
        {
            KeyTime = SweepCycleDuration,
            Setters = { new Setter(TranslateTransform.XProperty, end) },
        });

        Dispatcher.UIThread.Post(() =>
        {
            if (token.IsCancellationRequested)
            {
                return;
            }

            _ = animation.RunAsync(translate, token)
                .ContinueWith(_ => { }, token, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }, DispatcherPriority.Loaded);
    }

    private void StopSweep()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }
}