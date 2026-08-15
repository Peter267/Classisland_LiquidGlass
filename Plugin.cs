using ClassIsland.Core;
using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Extensions.Registry;
using ClassIsland.Core.Models.XamlTheme;
using ClassIsland.LiquidGlass.Models;
using ClassIsland.LiquidGlass.Services;
using ClassIsland.LiquidGlass.Views.SettingsPages;
using ClassIsland.Shared;
using ClassIsland.Shared.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ClassIsland.LiquidGlass;

/// <summary>
/// 液体玻璃插件入口。
/// </summary>
[PluginEntrance]
public class Plugin : PluginBase
{
    /// <summary>
    /// 插件设置。
    /// </summary>
    public LiquidGlassSettings Settings { get; private set; } = new();

    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        Settings = ConfigureFileHelper.LoadConfig<LiquidGlassSettings>(
            Path.Combine(PluginConfigFolder, "Settings.json"));
        Settings.PropertyChanged += (_, _) =>
            ConfigureFileHelper.SaveConfig<LiquidGlassSettings>(
                Path.Combine(PluginConfigFolder, "Settings.json"), Settings);

        services.AddSingleton(this);
        services.AddSingleton<LiquidGlassThemeManager>();
        services.AddSettingsPage<LiquidGlassSettingsPage>();
        services.AddXamlTheme(
            new Uri("avares://ClassIsland.LiquidGlass/XamlThemes/LiquidGlass/Styles.axamlx"),
            new ThemeManifest
            {
                Id = LiquidGlassThemeManager.ThemeId,
                Name = "Liquid Glass",
                Description = "受 Apple Liquid Glass 设计语言启发的玻璃质感主题：分层高光、渐变玻璃、发光边框与光泽扫过动画。",
                Version = "1.0.0.0",
                Author = "YourName",
                Url = "https://github.com/yourname/ClassIsland.LiquidGlass",
                Banner = "avares://ClassIsland.LiquidGlass/icon.png"
            });

        AppBase.Current.AppStarted += (_, _) =>
        {
            try
            {
                // 主题加载前必须注册：应用未提供运行时 XAML 加载器，也无法从
                // 默认程序集上下文解析插件的 avares 资源。
                RuntimeXamlLoaderRegistrar.BridgePluginAssembly();
                RuntimeXamlLoaderRegistrar.EnsureRegistered();

                IAppHost.GetService<LiquidGlassThemeManager>().Initialize();
            }
            catch (Exception e)
            {
                Console.WriteLine($"[LiquidGlass] 初始化失败：{e}");
            }
        };
    }
}