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
/// 注册只需在主题加载前完成：插件 Initialize 阶段调用一次（早于应用首次
/// LoadAllThemes），AppStarted 时幂等兜底一次。
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

        var locator = currentMutable.GetValue(null)!;

        // 定位到根定位器：所有作用域（EnterScope）的 GetService 都会沿父链回退到根，
        // 即使注册后作用域被释放（Current/CurrentMutable 被恢复），注册也不会丢失。
        var parentScope = locatorType.GetField("_parentScope",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (parentScope != null)
        {
            while (parentScope.GetValue(locator) is AvaloniaLocator parent)
            {
                locator = parent;
            }
        }

        var loaderType = typeof(AvaloniaXamlLoader)
            .GetNestedType("IRuntimeXamlLoader", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("AvaloniaXamlLoader.IRuntimeXamlLoader not found");

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
    /// 将插件程序集桥接到 <see cref="AssemblyLoadContext.Default"/>。
    /// 应用没有为默认上下文注册解析器，<c>Assembly.Load("ClassIsland.LiquidGlass")</c>
    /// （由 <c>StandardAssetLoader</c> 用于解析 avares 资源）会找不到插件程序集，
    /// 导致内嵌的主题 / 噪点等 avares 资源无法读取。这里自注册一个解析处理。
    /// 注意：必须在任何主题加载（MainWindow.Show 触发的 LoadAllThemes）之前调用。
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
                return RuntimeXamlLoaderAccess.Load().Invoke(null, args);
            }

            return null;
        }
    }

    /// <summary>
    /// 反射访问 <c>AvaloniaRuntimeXamlLoader.Load</c>。
    /// 应用自带（并已加载进默认上下文）的 <c>Avalonia.Markup.Xaml.Loader</c> 与插件
    /// 携带的副本版本可能不同，且默认上下文不允许再次加载同简单名的程序集
    /// （FileLoadException）。因此这里完全通过反射解析方法：优先使用应用
    /// 已加载的副本（任何 11.x 版本的公开 API 一致），仅在应用完全没有该程序集时
    /// 才回退到插件目录内的副本。
    /// </summary>
    private static class RuntimeXamlLoaderAccess
    {
        private const string LoaderAssemblyName = "Avalonia.Markup.Xaml.Loader";
        private const string LoaderTypeName = "Avalonia.Markup.Xaml.AvaloniaRuntimeXamlLoader";

        private static readonly Lazy<MethodInfo> LoadMethod = new(ResolveLoadMethod);

        public static MethodInfo Load() => LoadMethod.Value;

        private static MethodInfo ResolveLoadMethod()
        {
            Type? type;
            try
            {
                type = Type.GetType(LoaderTypeName + ", " + LoaderAssemblyName);
            }
            catch
            {
                type = null;
            }

            type ??= TryLoadFromPluginDirectory();
            if (type == null)
            {
                throw new TypeLoadException(
                    $"无法解析 {LoaderTypeName}，应用与插件均未提供 Avalonia.Markup.Xaml.Loader。");
            }

            return type.GetMethod("Load",
                       new[] { typeof(RuntimeXamlLoaderDocument), typeof(RuntimeXamlLoaderConfiguration) })
                   ?? throw new MissingMethodException(type.FullName, "Load");
        }

        private static Type? TryLoadFromPluginDirectory()
        {
            try
            {
                var pluginDir = Path.GetDirectoryName(typeof(RuntimeXamlLoaderRegistrar).Assembly.Location);
                if (pluginDir == null)
                {
                    return null;
                }

                var path = Path.Combine(pluginDir, LoaderAssemblyName + ".dll");
                return File.Exists(path) ? Assembly.LoadFrom(path).GetType(LoaderTypeName) : null;
            }
            catch
            {
                return null;
            }
        }
    }
}