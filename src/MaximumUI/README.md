# MaximumUI

供 Unity IMGUI 插件使用的声明式 UI 库。支持以集合初始化器组织控件，包含布局引擎、输入分发、绘制命令、控件状态、主题、导航、快捷键及运行时挂载。

- `MUI.cs`、`View.cs`、`Widget.cs`：界面与控件入口。
- `LayoutEngine.cs`、`LayoutNode.cs`、`LayoutHints.cs`：布局和尺寸约束。
- `InputDispatcher.cs`、`NavigationState.cs`、`ShortcutRouter.cs`：输入、焦点和快捷键。
- `DrawList.cs`、`WidgetPainter.cs`、`UnityPainter.cs`、`ImguiRenderer.cs`：绘制与 IMGUI 桥接。
- `Widgets/`：基础控件、布局容器、高级控件与调试控件。
- `Runtime/`：宿主、挂载选项和界面生命周期。

库依赖 UnityEngine 编译引用，皮肤贴图作为资源嵌入。它不是单独的 BepInEx 功能插件；使用方式参见 [MaximumUIGallery](../MaximumUIGallery/README.md) 的实际代码和 [设计/API 文档](../../MaximumUI.md)。文档包含设计说明，具体签名以当前源码为准。

```sh
dotnet build src/MaximumUI/MaximumUI.csproj -c Release
pwsh -NoProfile -File scripts/Test-MaximumUI.ps1
```

构建输出：`bin/Release/MaximumUI/MaximumUI.dll`。无界面测试不覆盖 Unity 的真实绘制、鼠标捕获和游戏内窗口交互，相关行为需结合 Gallery 检查。
