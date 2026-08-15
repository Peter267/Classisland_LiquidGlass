using System;
using System.Reflection;
using System.Runtime.Loader;
using Avalonia;
using Avalonia.Markup.Xaml;

namespace ClassIsland.LiquidGlass.Services;

/// <summary>
/// 在运行时为 Avalonia 注册原始 XAML 加载能力。
/// ClassIsland 未注册内部的 <c>AvaloniaXamlLoader.IRuntimeXamlLoader</c>，导致
/// 通过 <c>AvaloniaXamlLoader.Load(avares://…)</c> 加载未编译 XAML 主题必然失败。
/// 这里用反射 + <see cref="DispatchProxy"/> 将该接口委托给公开的
/// <c>AvaloniaRuntimeXamlLoader</c>（来自 Avalonia.Markup.Xaml.Loader 包）。
/// 注册只需在主题加载前完成，插件启动时调用一次即可。
/// </summary>
internal static class RuntimeXamlLoaderRegistrar
{
    /// <summary>
    /// 如果尚未注册运行时 XAML 加载器，则注册之。幂等。
    /// </summary>
    public static void EnsureRegistered()
    {
        var locatorType = typeof(AvaloniaLocator);
        var currentMutable = locatorType
            .GetProperty("CurrentMutable", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("AvaloniaLocator.CurrentMutable not found");

        var loaderType = typeof(AvaloniaXamlLoader)
            .GetNestedType("IRuntimeXamlLoader", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("AvaloniaXamlLoader.IRuntimeXamlLoader not found");

        var locator = currentMutable.GetValue(null)!;
        var getService = locatorType.GetMethod("GetService", new[] { typeof(Type) })
            ?? throw new InvalidOperationException("GetService(Type) not found");
        if (getService.Invoke(locator, new object[] { loaderType }) != null)
        {
            return;
        }

        var proxy = DispatchProxy.Create(loaderType, typeof(RuntimeXamlLoaderProxy));
        var bind = locatorType.GetMethod("Bind", Type.EmptyTypes)
            ?? throw new InvalidOperationException("Bind<T> not found");
        var helper = bind.MakeGenericMethod(loaderType).Invoke(locator, null)!;
        var toConstant = helper.GetType().GetMethod("ToConstant")
            ?? throw new InvalidOperationException("ToConstant not found");
        toConstant.MakeGenericMethod(proxy.GetType()).Invoke(helper, new[] { proxy });
    }

    /// <summary>
    /// 将插件程序集桥接到默认 <see cref="AssemblyLoadContext"/>。
    /// 应用没有为默认上下文注册解析器，<c>Assembly.Load("ClassIsland.LiquidGlass")</c>
    /// （由 <c>StandardAssetLoader</c> 用于解析 avares 资源）会找不到插件程序集，
    /// 导致内嵌的主题 / 噪点等 avares 资源无法读取。这里自注册一个解析处理。
    /// </summary>
    public static void BridgePluginAssembly()
    {
        var self = typeof(RuntimeXamlLoaderRegistrar).Assembly;
        var selfName = self.GetName().Name;
        AssemblyLoadContext.Default.Resolving += (_, name) =>
        {
            if (name.Name == selfName)
            {
                return self;
            }
            var already = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < already.Length; i++)
            {
                if (already[i].GetName().Name == name.Name)
                {
                    return already[i];
                }
            }
            return null;
        };
    }

    private class RuntimeXamlLoaderProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "Load")
            {
                return AvaloniaRuntimeXamlLoader.Load(
                    (RuntimeXamlLoaderDocument)args![0]!,
                    (RuntimeXamlLoaderConfiguration)args![1]!);
            }
            return null;
        }
    }
}