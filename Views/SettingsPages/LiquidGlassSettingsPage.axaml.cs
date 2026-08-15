using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Enums.SettingsWindow;

namespace ClassIsland.LiquidGlass.Views.SettingsPages;

/// <summary>
/// 液体玻璃插件的设置页面。
/// </summary>
[SettingsPageInfo("dev.you.liquidglass.settings", "Liquid Glass", "\uE713", "\uE713")]
public partial class LiquidGlassSettingsPage : SettingsPageBase
{
    public Plugin Plugin { get; }

    public LiquidGlassSettingsPage(Plugin plugin)
    {
        Plugin = plugin;
        InitializeComponent();
        DataContext = this;
    }
}