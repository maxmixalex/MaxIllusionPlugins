# MaximumUI Gallery

MaximumUI 的独立游戏内工作台。默认采用 Metro Dark，包含左侧导航、分层标题、可调整大小的内容区、表单、布局、表格、对话框、主题、自定义绘制和调试工具。主要代码在 `MaximumUIGalleryPlugin.cs`，展示 `MaximumUIRuntime.Mount` 与声明式控件的实际组合。功能插件继续使用原有 UI，未接入 MaximumUI。

## 页面和操作

| 页面 | 实际行为 |
| --- | --- |
| Overview | 可编辑名称、模式、强度和启用状态的表单；Apply 显示真实模态反馈，但不修改游戏角色 |
| Controls | 按钮、输入、数值、选择、颜色、图表、拖放和上下文菜单 |
| Composition | 菜单、标签、栈、网格、表格、折叠区、虚拟列表和可拖动分栏 |
| Dialogs | Alert、Confirm 和含文本/下拉输入的内联模态窗口 |
| Appearance | 即时切换四个内置主题；编辑字体大小/颜色/令牌后明确 Apply，Revert 撤销未应用修改 |
| Large lists | 10,000 条可搜索记录；仅构建可视行，过滤后保留选中记录，空结果有说明 |
| Diagnostics | 指标、输入、日志、状态、控件 ID 与使用说明 |

默认显示窗口，按 `F8` 切换。`Ctrl+1` 至 `Ctrl+6` 分别打开 Controls、Composition、Dialogs、Appearance、Diagnostics、Large lists；`Ctrl+L` 操作日志示例。默认 F8 与 ClothingCompression 的高级面板入口相同，联合验证时可单独启用 Gallery。

Gallery 设置 `BuildEveryFrame = false`，依赖状态失效构建，只创建当前页面；仅 Diagnostics 每 0.25 秒刷新指标。页面滚动区按窗口实际剩余高度排列，不再固定为 440px。10,000 条样本只在第一次打开时生成，文本筛选只在查询变化时重新执行。主题编辑先修改纯托管副本，Apply 才分配原生样式；替换与卸载释放 Gallery 拥有的自定义主题。

原先的主题/字体选择器只修改显示字符串，现已应用到运行时。字体使用 Unity/游戏提供的默认字体，移除了伪造的 atlas 尺寸、字形数量和没有实际字体切换的选项。控件演示中的普通下拉也会正确显示新选项。

## 构建与验证

```sh
dotnet build src/MaximumUIGallery/MaximumUIGallery.csproj -c Release
python scripts/test.py
```

输出为 `bin/Release/MaximumUIGallery/KK_MaximumUIGallery.dll`，安装时同时提供 `MaximumUI.dll`。Gallery 是开发示例，不是其他功能插件的必装依赖。

无界面回归验证布局、主题令牌、对比、输入策略、虚拟化等托管逻辑；它不执行 Unity 原生渲染。游戏内需要检查：主题切换后已打开下拉是否清理；中文/英文输入与换行；窗口缩放后的末行可达性；不同字体大小；模态内下拉；鼠标释放；10,000 条记录持续滚动和输入搜索时的帧耗时；反复应用主题后的长期原生内存。不能把编译或无界面测试的通过称作实际游戏画面/性能验收。

设计来源和主题 API 见 [MaximumUI README](../MaximumUI/README.md)。

## 可再生绘制预览

[Metro Dark](../../docs/maximumui-metro-dark-preview.svg) 和 [Metro Light](../../docs/maximumui-metro-light-preview.svg) 由实际 Gallery 程序集的 `Build`、`LayoutEngine`、`WidgetPainter` 和主题令牌生成，展示同一 Overview 页面在 940 × 840 窗口中的布局。它们是托管绘制预览，使用估算的 SVG 字体度量，不是游戏截图；原生窗口装饰、滚动条和输入尚未验证。生成方法、校验范围和限制见 [MaximumUI.Preview](../../tests/MaximumUI.Preview/README.md)。
