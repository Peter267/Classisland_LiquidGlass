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
                new GradientStop(Color.FromArgb((byte)(255 * topAlpha * opacity), baseColor.R, baseColor.G, baseColor.B), 0),
                new GradientStop(Color.FromArgb((byte)(255 * midAlpha * opacity), baseColor.R, baseColor.G, baseColor.B), 0.45),
                new GradientStop(Color.FromArgb((byte)(255 * bottomAlpha * opacity), baseColor.R, baseColor.G, baseColor.B), 1)
            }
        };
    }

    private static LinearGradientBrush BuildHighlightBrush(bool isLight, double opacity)
    {
        var peakAlpha = isLight ? 0.75 : 0.28;
        return new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.FromArgb((byte)(255 * peakAlpha * opacity), 255, 255, 255), 0),
                new GradientStop(Color.FromArgb((byte)(255 * peakAlpha * opacity * 0.45), 255, 255, 255), 0.28),
                new GradientStop(Colors.Transparent, 0.55)
            }
        };
    }

    private static LinearGradientBrush BuildBorderBrush(bool isLight, double opacity)
    {
        var topAlpha = isLight ? 0.95 : 0.60;
        var midAlpha = isLight ? 0.15 : 0.10;
        var bottomAlpha = isLight ? 0.55 : 0.30;
        return new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.FromArgb((byte)(255 * topAlpha * opacity), 255, 255, 255), 0),
                new GradientStop(Color.FromArgb((byte)(255 * midAlpha * opacity), 255, 255, 255), 0.5),
                new GradientStop(Color.FromArgb((byte)(255 * bottomAlpha * opacity), 255, 255, 255), 1)
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
                new GradientStop(Color.FromArgb((byte)(255 * peakAlpha), 255, 255, 255), 0.42),
                new GradientStop(Colors.Transparent, 0.85)
            }
        };
    }

    /// <summary>
    /// 通过追加样式的方式动态调整岛屿投影强度（避免与主题样式产生优先级冲突）。
    /// </summary>
    private void ApplyShadowStyle(bool isLight, double strength)
    {
        if (_resourceLoaderBorder == null)
        {
            return;
        }

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
        var shadow = new BoxShadows(new BoxShadow
        {
            OffsetX = 0,
            OffsetY = 4,
            Blur = 18,
            Spread = 2,
            Color = color
        });
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