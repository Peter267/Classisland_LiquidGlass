using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ClassIsland.LiquidGlass.Models;

/// <summary>
/// 液体玻璃插件的设置。
/// </summary>
public class LiquidGlassSettings : INotifyPropertyChanged
{
    private bool _isEnabled;
    private int _glassMode;
    private double _tintOpacity = 0.5;
    private double _highlightOpacity = 0.8;
    private double _borderOpacity = 0.6;
    private double _shadowStrength = 0.6;
    private bool _enableShine = true;
    private bool _enableNoise = true;
    private bool _enableAcrylicBackdrop;
    private bool _adaptTextColor = true;

    /// <summary>
    /// 是否启用液体玻璃主题（控制主题在 ClassIsland 主题列表中的启用状态）。
    /// </summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetField(ref _isEnabled, value);
    }

    /// <summary>
    /// 玻璃色调模式：0 = 跟随系统明暗，1 = 浅色，2 = 深色。
    /// </summary>
    public int GlassMode
    {
        get => _glassMode;
        set => SetField(ref _glassMode, value);
    }

    /// <summary>
    /// 玻璃着色强度（0.2 ~ 0.9）。
    /// </summary>
    public double TintOpacity
    {
        get => _tintOpacity;
        set => SetField(ref _tintOpacity, value);
    }

    /// <summary>
    /// 顶部高光强度（0 ~ 1）。
    /// </summary>
    public double HighlightOpacity
    {
        get => _highlightOpacity;
        set => SetField(ref _highlightOpacity, value);
    }

    /// <summary>
    /// 边框光效强度（0 ~ 1）。
    /// </summary>
    public double BorderOpacity
    {
        get => _borderOpacity;
        set => SetField(ref _borderOpacity, value);
    }

    /// <summary>
    /// 投影强度（0 ~ 1）。
    /// </summary>
    public double ShadowStrength
    {
        get => _shadowStrength;
        set => SetField(ref _shadowStrength, value);
    }

    /// <summary>
    /// 是否启用光泽扫过动画。
    /// </summary>
    public bool EnableShine
    {
        get => _enableShine;
        set => SetField(ref _enableShine, value);
    }

    /// <summary>
    /// 是否启用玻璃噪点纹理。
    /// </summary>
    public bool EnableNoise
    {
        get => _enableNoise;
        set => SetField(ref _enableNoise, value);
    }

    /// <summary>
    /// 是否启用系统级亚克力背景（实验性，仅 Windows 11 生效）。
    /// </summary>
    public bool EnableAcrylicBackdrop
    {
        get => _enableAcrylicBackdrop;
        set => SetField(ref _enableAcrylicBackdrop, value);
    }

    /// <summary>
    /// 是否让主界面文字颜色随玻璃明暗适配。
    /// </summary>
    public bool AdaptTextColor
    {
        get => _adaptTextColor;
        set => SetField(ref _adaptTextColor, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}