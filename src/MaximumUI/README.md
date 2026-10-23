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
python scripts/test.py
```

构建输出：`bin/Release/MaximumUI/MaximumUI.dll`。无界面测试不覆盖 Unity 的真实绘制、鼠标捕获和游戏内窗口交互，相关行为需结合 Gallery 检查。

## 构建、布局与失效

`MountOptions` 默认每个 Unity 帧构建一次控件树（`BuildEveryFrame = true`、`BuildEveryOnGUI = false`）。同一帧的 Layout、Repaint 和输入事件共享控件树与测量结果。普通 `View` 仍会在下一帧读取外部状态；依赖每个 GUI 事件重新构建的旧代码可显式设置 `BuildEveryOnGUI = true`。

完全由 `StatefulView.SetState` / `ReplaceState` 驱动的界面可设置 `BuildEveryFrame = false`。外部状态变化时调用 `MaximumUIRuntime.MarkDirty(view)`。失效会触发下一次 GUI 调用重建，即使它仍处于同一帧；构建期间发生的失效也会保留。Gallery 使用此模式，并仅构建当前标签页内容，调试指标每 0.25 秒刷新一次。

每次运行时重建都会使布局缓存失效。仅移动窗口会重新排列已有节点，改变窗口尺寸则重新测量。缓存有 256 个根路径的上限，不会无限保留动态路径。直接调用 `ImguiRenderer` 的代码默认不启用缓存；若设置 `RenderContext.CacheLayout = true`，则在修改已保留的 Widget 属性、子节点或主题度量后调用 `ImguiRenderer.InvalidateLayout()`。直接使用公开的 `LayoutCache` 时遵循相同约定，调用 `Invalidate()`；缓存不观察可变字段。

`ImguiRenderer` 实现 `IDisposable`。运行时在卸载视图或挂载失败时释放渲染器持有的绘制资源；直接使用者应在销毁宿主时调用 `Dispose()`。业务插件尚未接入此库。

## 外观与交互

默认主题为 `MaximumTheme.CleanLight`：浅色不透明面板、细边框、深色文字、蓝色选择状态和左对齐标题。`MaximumTheme.ExpressionLight` 仍可显式选择。字体缩放限制在原始大小的 1–1.5 倍，行高、窗口内边距和换行测量与实际字体相匹配。主题及绘制纹理不生成 mipmap。

自定义样式与绘制命令只在 Repaint 事件执行；输入控件仍处理相应输入事件。滚动容器按完整内容高度测量，长列表建议使用 `VirtualList`；普通 `ScrollView` 的全部子控件仍会参与渲染遍历。下拉列表只遍历可视行，但展开时仍会枚举选项以支持过滤，因此超大数据集应优先使用虚拟列表。

模态窗口在挂载之间统一确定输入归属，并置于其他挂载之上。同一挂载内只有最上层对话框可交互；相同优先级保持注册顺序。关闭确认框时先移除该对话框再调用确认回调，回调新建的对话框不会被误关。`CloseAll` 只关闭调用时存在的对话框。

## 无界面回归与性能测量

当前测试包括原有控件布局、输入分发与绘制命令断言，以及同帧失效、失效期间重建、布局复用/移动/缩放、长内容滚动、窄分栏、可见标签页中的模态查找、禁用控件导航、字体/换行尺寸、Repaint 策略、拖拽释放策略、自引用绘制列表及重入对话框回调。浮层测试验证稳定顺序和顶层输入归属。

可选的构建/布局基准使用修复后的同一程序集比较三种调度策略；它不是原始版本与新版本二进制的直接对比。基准保留控件数量和构建/布局次数断言，可在 .NET 9 宿主中读取实际线程分配量：

```sh
dotnet build src/MaximumUI.Tests/MaximumUI.Tests.csproj -c Release
MAXIMUMUI_BENCHMARK=1 dotnet run --project tests/MaximumUI.Runner/MaximumUI.Runner.csproj -c Release -- bin/Release/MaximumUI.Tests/MaximumUI.Tests.dll
```

场景为 1000 个 Label、150 帧、每帧 2 个 GUI 事件。逐事件策略执行 300 次树构建和布局，默认逐帧策略执行 150 次，按需空闲策略执行 1 次。2026-10-23 的 .NET 9.0.319 测量中，三者分配量分别为 126,662,440 / 63,338,576 / 422,432 字节。计时受预热、JIT 和运行环境影响；该基准不含 Unity 绘制、输入处理和游戏负载，不能换算为游戏帧率。

`python scripts/test.py --benchmark` 也会执行上述策略基准及全部回归。若需比较历史版本与修复版本的独立 DLL，使用 [MaximumUI.Benchmark](../../tests/MaximumUI.Benchmark/README.md) 的显式程序集路径和独立输出目录；该项目不加入解决方案、不产生插件发布产物。

游戏内仍需用 Gallery 验证文本输入、窗口拖动及释放、滚动到末行、无 Key 窗口中的下拉定位、多挂载/多层模态、对话框中的下拉、主题/分辨率变化，以及长时间打开后的内存和帧耗时。真实字体显示、原生 GUI control ID、鼠标捕获与其他插件叠加不在无界面宿主的覆盖范围内。
