# ClassIsland Liquid Glass

为 ClassIsland 2.x 主界面添加漂亮的液体玻璃（Liquid Glass）质感效果。

## 效果特性

- **分层玻璃**：渐变玻璃底色 + 顶部镜面高光 + 底部内光，接近 Apple Liquid Glass 的观感
- **发光边框**：上亮下暗的渐变发光描边与内阴影，强化玻璃边缘
- **光泽扫过动画**：玻璃表面周期性斜向光泽扫过
- **噪点纹理**：WinUI Acrylic 同款噪点颗粒，增强真实玻璃质感
- **明暗适配**：自动跟随系统浅色/深色模式，或手动指定玻璃明暗
- **系统亚克力背景（实验性）**：Windows 11 下启用 `AcrylicBlur`，让桌面壁纸透过玻璃真实模糊
- **保留原有交互**：提醒遮罩、斜切动画、进度条、悬浮提示等主界面动效全部兼容

## 使用方法

1. 在 ClassIsland【应用设置 → 插件】中安装本插件（将 `ClassIsland.LiquidGlass.cipx` 放入插件目录或从市场安装）。
2. 安装后，在【应用设置 → 主题】中启用 **Liquid Glass** 主题。
3. 在【应用设置 → 插件 → Liquid Glass】中调整玻璃参数，所有修改实时生效。
4. 建议在【外观】设置中将背景不透明度与圆角半径调整到喜欢的数值，Liquid Glass 主题会自动适配。

## 开发

```bash
dotnet build
```

构建产物：
- `bin/Debug/net8.0/` 调试输出
- `cipx/ClassIsland.LiquidGlass.cipx` 插件包

> 说明：`ClassIsland.PluginSdk` 的 `CreateCipx` 目标会在打包时调用 PowerShell 生成 MD5 校验文件（`GenerateHashSummary`）。本机无 PowerShell 时已在项目文件中将其关闭；如需 MD5 摘要，请在 Windows 上重新开启或手动运行 SDK 中的 `generate-md5.ps1`。

## 发布

参考 [ClassIsland 插件发布文档](https://docs.classisland.tech/dev/plugins/publishing.html)：

1. 修改 `manifest.yml` 中的 `id` / `author` / `url`（当前为占位值，需替换）。
2. 在 GitHub 仓库创建 Release，Tag 必须是纯版本号（如 `1.0.0.0`），上传 `ClassIsland.LiquidGlass.cipx`。
3. Release 描述中附带 `<!-- CLASSISLAND_PKG_MD5 {...} -->` 注释（用 `ClassIsland.PluginSdk` 中的 `generate-md5.ps1` 生成）。
4. 向 [ClassIsland/PluginIndex](https://github.com/ClassIsland/PluginIndex) 提交 PR，在 `index/plugins-v2/` 下添加清单文件（见 `docs/plugin-index.yml` 示例）。

## 技术说明

- 本插件注册了一个 XAML 主题（`AddXamlTheme`），主题样式注入主窗口 `ResourceLoaderBorder`，可完全覆盖 `MainWindowLine` 模板与 `line-background` 样式。
- 插件运行时（`LiquidGlassThemeManager`）在应用启动完成后（`AppStarted`）按设置与系统明暗动态构建玻璃画刷，写入主题资源键，实现参数实时调整；投影强度通过追加样式实现，避免与主题样式产生优先级冲突。
- 主题 XAML 以资源形式随插件分发（以 `.axamlx` 名称内嵌，不参与编译），由 ClassIsland 运行时加载，与第三方主题的加载方式一致。
- 应用本身未注册 Avalonia 运行时 XAML 加载器，也无法从默认程序集上下文解析插件程序集；插件在启动时通过 `RuntimeXamlLoaderRegistrar` 完成两项注册（反射 + `DispatchProxy` 委托给 `AvaloniaRuntimeXamlLoader`、默认 `AssemblyLoadContext.Resolving` 桥接插件程序集），使原始 XAML 主题与内嵌的噪点/图标等 `avares` 资源可正常解析。

## 许可证

MIT