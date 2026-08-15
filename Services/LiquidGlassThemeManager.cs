using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using ClassIsland.Core;
using ClassIsland.Core.Abstractions.Services;

namespace ClassIsland.LiquidGlass.Services;

/// <summary>
/// 液体玻璃效果管理器：负责将插件设置实时应用到主界面的主题资源上，
/// 并根据系统明暗模式与用户设置构建对应的玻璃画刷。
/// </summary>
public class LiquidGlassThemeManager
{
    /// <summary>
    /// 主题 ID，需要与 manifest.yml 中的插件 ID 保持一致。
    /// </summary>
    public const string ThemeId = "dev.you.liquidglass";

    private const string KeyBackgroundBrush = "LG.BackgroundBrush";
    private const string KeyHighlightBrush = "LG.HighlightBrush";
    private const string KeyBorderBrush = "LG.BorderBrush";
    private const string KeyShineBrush = "LG.ShineBrush";
    private const string KeyShineEnabled = "LG.ShineEnabled";
    private const string KeyNoiseEnabled = "LG.NoiseEnabled";
    private const string KeyTextBrush = "LG.TextBrush";
    private const string KeyInnerShadowBrush = "LG.InnerShadowBrush";
    private const string KeyReflectionBrush = "LG.ReflectionBrush";

    private readonly Plugin _plugin;
    private readonly IThemeService _themeService;
    private readonly IXamlThemeService _xamlThemeService;

    private Window? _mainWindow;
    private Border? _resourceLoaderBorder;
    private Style? _shadowStyle;
    private bool _isAcrylicApplied;
    private bool _isReorderingShadow;

    public LiquidGlassThemeManager(Plugin plugin, IThemeService themeService, IXamlThemeService xamlThemeService)
    {
        _plugin = plugin;
        _themeService = themeService;
        _xamlThemeService = xamlThemeService;
    }

    /// <summary>
    /// 初始化管理器。需要在应用启动完成后（AppStarted 事件）调用。
    /// </summary>
    public void Initialize()
    {
        _mainWindow = AppBase.Current.MainWindow;
        _resourceLoaderBorder = _mainWindow?.FindControl<Border>("ResourceLoaderBorder")
            ?? _mainWindow?.GetVisualDescendants().OfType<Border>().FirstOrDefault(b => b.Name == "ResourceLoaderBorder");
        if (_resourceLoaderBorder?.Styles is Styles styles)
        {
            styles.CollectionChanged += (_, _) => ReorderShadowStyle();
        }

        _themeService.ThemeUpdated += (_, _) => ApplyVisuals();
        _plugin.Settings.PropertyChanged += (_, _) =>
        {
            ApplyVisuals();
            ApplyAcrylicBackdrop();
            if (_plugin.Settings.IsEnabled)
            {
                SyncEnabledThemes();
            }
        };

        SyncEnabledThemes();
        ApplyVisuals();
        ApplyAcrylicBackdrop();
    }

    /// <summary>
    /// 同步主题在 ClassIsland 主题列表中的启用状态。
    /// </summary>
    public void SyncEnabledThemes()
    {
        var themes = _xamlThemeService.EnabledThemes;
        var contains = themes.Contains(ThemeId);
        if (_plugin.Settings.IsEnabled && !contains)
        {
            themes.Add(ThemeId);
            _xamlThemeService.LoadAllThemes();
        }
        else if (!_plugin.Settings.IsEnabled && contains)
        {
            if (themes.Count > 1)
            {
                themes.Remove(ThemeId);
                _xamlThemeService.LoadAllThemes();
            }
            else
            {
                _plugin.Settings.IsEnabled = true;
            }
        }
    }

    /// <summary>
    /// 根据当前设置与明暗模式构建玻璃画刷，并写入主题资源。
    /// </summary>
    private void ApplyVisuals()
    {
        if (_resourceLoaderBorder == null)
        {
            return;
        }

        var settings = _plugin.Settings;
        var isLight = settings.GlassMode == 0
            ? _themeService.CurrentRealThemeMode == 0
            : settings.GlassMode == 1;

        _resourceLoaderBorder.Resources[KeyBackgroundBrush] = BuildBackgroundBrush(isLight, settings.TintOpacity);
        _resourceLoaderBorder.Resources[KeyHighlightBrush] = BuildHighlightBrush(isLight, settings.HighlightOpacity);
        _resourceLoaderBorder.Resources[KeyBorderBrush] = BuildBorderBrush(isLight, settings.BorderOpacity);
        _resourceLoaderBorder.Resources[KeyShineBrush] = BuildShineBrush(isLight);
        _resourceLoaderBorder.Resources[KeyShineEnabled] = settings.EnableShine;
        _resourceLoaderBorder.Resources[KeyNoiseEnabled] = settings.EnableNoise;
        _resourceLoaderBorder.Resources[KeyInnerShadowBrush] = BuildInnerShadowBrush(isLight);
        _resourceLoaderBorder.Resources[KeyReflectionBrush] = BuildReflectionBrush(isLight);
        if (settings.AdaptTextColor)
        {
            _resourceLoaderBorder.Resources[KeyTextBrush] =
                new SolidColorBrush(isLight ? Color.FromRgb(20, 22, 28) : Color.FromRgb(244, 246, 250));
        }
        else
        {
            _resourceLoaderBorder.Resources.Remove(KeyTextBrush);
        }

        ApplyShadowStyle(isLight, settings.ShadowStrength);
    }

    /// <summary>
    /// 应用或移除系统级亚克力背景（仅 Windows 11 生效，实验性）。
    /// </summary>
    private void ApplyAcrylicBackdrop()
    {
        if (_mainWindow == null)
        {
            return;
        }

        if (_plugin.Settings.IsEnabled && _plugin.Settings.EnableAcrylicBackdrop && IsAcrylicSupported())
        {
            if (!_isAcrylicApplied)
            {
                _mainWindow.TransparencyLevelHint =
                    new[] { WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.Transparent };
                _isAcrylicApplied = true;
            }
        }
        else if (_isAcrylicApplied)
        {
            _mainWindow.TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
            _isAcrylicApplied = false;
        }
    }

    private static bool IsAcrylicSupported() =>
        OperatingSystem.IsWindows() && Environment.OSVersion.Version >= new Version(10, 0, 22000);

    private static Color BaseTint(bool isLight) =>
        isLight ? Color.FromRgb(236, 241, 250) : Color.FromRgb(24, 27, 40);

    private static LinearGradientBrush BuildBackgroundBrush(bool isLight, double opacity)
    {
        var baseColor = BaseTint(isLight);
        var topAlpha = isLight ? 0.34 : 0.50;
        var midAlpha = isLight ? 0.16 : 0.26;
        var bottomAlpha = isLight ? 0.24 : 0.40;
        return new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(WithAlpha(baseColor, topAlpha * opacity), 0),
                new GradientStop(WithAlpha(baseColor, midAlpha * opacity), 0.45),
                new GradientStop(WithAlpha(baseColor, bottomAlpha * opacity), 1)
            }
        };
    }

    private static LinearGradientBrush BuildHighlightBrush(bool isLight, double opacity)
    {
        // 顶部镜面高光：峰值更亮、衰减更快，贴近玻璃表面的镜面反射。
        var peakAlpha = isLight ? 0.82 : 0.36;
        var peak = (byte)(Clamp01(peakAlpha * opacity) * 255);
        return new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.FromArgb(peak, 255, 255, 255), 0),
                new GradientStop(Color.FromArgb((byte)(peak * 0.38), 255, 255, 255), 0.22),
                new GradientStop(Colors.Transparent, 0.5)
            }
        };
    }

    private static LinearGradientBrush BuildBorderBrush(bool isLight, double opacity)
    {
        // 方向性受光边缘：光源从左上 45° 入射（等效 shader 的 edge highlight，
        // 圆角矩形法线与光源方向点积），顶部与左侧边缘最亮、沿对角衰减，
        // 右下角保留微弱环境反射，形成真正的“玻璃受光”而非均匀描边。
        var topAlpha = isLight ? 0.95 : 0.60;
        var sideAlpha = isLight ? 0.28 : 0.14;
        var bottomAlpha = isLight ? 0.12 : 0.07;
        var reflectionAlpha = isLight ? 0.38 : 0.18;
        return new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.FromArgb((byte)(Clamp01(topAlpha * opacity) * 255), 255, 255, 255), 0),
                new GradientStop(Color.FromArgb((byte)(Clamp01(sideAlpha * opacity) * 255), 255, 255, 255), 0.35),
                new GradientStop(Color.FromArgb((byte)(Clamp01(bottomAlpha * opacity) * 255), 255, 255, 255), 0.78),
                new GradientStop(Color.FromArgb((byte)(Clamp01(reflectionAlpha * opacity) * 255), 255, 255, 255), 1)
            }
        };
    }

    /// <summary>
    /// 玻璃底部内阴影：模拟玻璃厚度，使表面产生向下“下沉”的体积感。
    /// </summary>
    private static LinearGradientBrush BuildInnerShadowBrush(bool isLight)
    {
        var bottomAlpha = isLight ? 0.18 : 0.30;
        return new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.FromArgb((byte)(bottomAlpha * 255), 0, 0, 0), 0),
                new GradientStop(Color.FromArgb((byte)(bottomAlpha * 0.45 * 255), 0, 0, 0), 0.35),
                new GradientStop(Colors.Transparent, 0.75)
            }
        };
    }

    /// <summary>
    /// 玻璃表面底部反射：内容下方一道向上渐隐的微光，增强镜面质感。
    /// </summary>
    private static LinearGradientBrush BuildReflectionBrush(bool isLight)
    {
        var peakAlpha = isLight ? 0.10 : 0.07;
        return new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.FromArgb((byte)(peakAlpha * 255), 255, 255, 255), 0),
                new GradientStop(Colors.Transparent, 0.6)
            }
        };
    }

    private static LinearGradientBrush BuildShineBrush(bool isLight)
    {
        var peakAlpha = isLight ? 0.16 : 0.10;
        return new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Colors.Transparent, 0),
                new GradientStop(Color.FromArgb((byte)(peakAlpha * 255), 255, 255, 255), 0.42),
                new GradientStop(Colors.Transparent, 0.85)
            }
        };
    }

    private static Color WithAlpha(Color baseColor, double alpha)
    {
        var a = (byte)(Clamp01(alpha) * 255);
        return Color.FromArgb(a, baseColor.R, baseColor.G, baseColor.B);
    }

    private static double Clamp01(double value)
    {
        return double.IsNaN(value) ? 0 : Math.Clamp(value, 0, 1);
    }

    /// <summary>
    /// 通过追加样式的方式动态调整岛屿投影强度（避免与主题样式产生优先级冲突）。
    /// </summary>
    private void ApplyShadowStyle(bool isLight, double strengthRaw)
    {
        if (_resourceLoaderBorder == null)
        {
            return;
        }

        var strength = Clamp01(strengthRaw);
        if (strength <= 0.01)
        {
            if (_shadowStyle != null && _resourceLoaderBorder.Styles.Contains(_shadowStyle))
            {
                _resourceLoaderBorder.Styles.Remove(_shadowStyle);
            }

            return;
        }

        var alpha = (byte)((isLight ? 0.14 : 0.30) * strength * 255);
        var color = Color.FromArgb(alpha, 0, 0, 0);
        // 双层投影：环境光（远、柔、宽）+ 接触阴影（近、硬、窄），
        // 模拟玻璃悬浮于背景之上的真实光照层次。
        var ambient = new BoxShadow
        {
            OffsetX = 0,
            OffsetY = 10,
            Blur = 26,
            Spread = 0,
            Color = color
        };
        var contact = new BoxShadow
        {
            OffsetX = 0,
            OffsetY = 2,
            Blur = 7,
            Spread = 0,
            Color = Color.FromArgb((byte)(alpha * 1.25), 0, 0, 0)
        };
        var shadow = new BoxShadows(ambient, new[] { contact });
        if (_shadowStyle == null)
        {
            _shadowStyle = new Style(selector => selector.OfType<Border>().Class("line-background"));
            _shadowStyle.Setters.Add(new Setter(Border.BoxShadowProperty, shadow));
            _resourceLoaderBorder.Styles.Add(_shadowStyle);
        }
        else
        {
            if (_resourceLoaderBorder.Styles.Contains(_shadowStyle))
            {
                _resourceLoaderBorder.Styles.Remove(_shadowStyle);
            }

            ((Setter)_shadowStyle.Setters[0]).Value = shadow;
            _resourceLoaderBorder.Styles.Add(_shadowStyle);
        }
    }

    /// <summary>
    /// 确保阴影样式始终位于主题样式之后，避免被主题样式的投影覆盖。
    /// </summary>
    private void ReorderShadowStyle()
    {
        if (_isReorderingShadow || _resourceLoaderBorder == null || _shadowStyle == null)
        {
            return;
        }

        var styles = _resourceLoaderBorder.Styles;
        if (!styles.Contains(_shadowStyle))
        {
            return;
        }

        _isReorderingShadow = true;
        try
        {
            styles.Remove(_shadowStyle);
            styles.Add(_shadowStyle);
        }
        finally
        {
            _isReorderingShadow = false;
        }
    }
}