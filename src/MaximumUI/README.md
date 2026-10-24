# MaximumUI

供 Unity IMGUI 插件使用的声明式 UI 库，当前版本 **0.5.0**。支持以集合初始化器组织控件，包含布局引擎、输入分发、绘制命令、控件状态、主题、导航、快捷键及运行时挂载。

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

完全由 `StatefulView.SetState` / `ReplaceState` 驱动的界面可设置 `BuildEveryFrame = false`。外部状态变化时调用 `MaximumUIRuntime.MarkDirty(view)`。失效会触发下一次 GUI 调用重建，即使它仍处于同一帧；构建期间发生的失效也会保留。Gallery 与四个接入编辑器使用此模式。Gallery 仅构建当前标签页内容，调试指标每 0.25 秒刷新一次；插件适配器只在可见时轮询必要的外部快照，并在变化时失效。

每次运行时重建都会使布局缓存失效。仅移动窗口会重新排列已有节点，改变窗口尺寸则重新测量。缓存有 256 个根路径的上限，不会无限保留动态路径。直接调用 `ImguiRenderer` 的代码默认不启用缓存；若设置 `RenderContext.CacheLayout = true`，则在修改已保留的 Widget 属性、子节点或主题度量后调用 `ImguiRenderer.InvalidateLayout()`。直接使用公开的 `LayoutCache` 时遵循相同约定，调用 `Invalidate()`；缓存不观察可变字段。

保留模式中的 ComboBox / ListBox 缓存枚举结果和格式化标签；只有数据源、控件、项目数或失效版本变化才重新枚举。同样的筛选文本复用结果，改变筛选时搜索缓存标签。**同长度集合的内容变更、格式化结果变化和直接修改控件字段都必须 MarkDirty / InvalidateLayout**，不能依赖项目数检测。直接渲染器未开 CacheLayout 时继续实时枚举。

`VirtualList.CacheItems` 默认开启，在保留模式下缓存可见范围和 overscan 的 Widget / LayoutNode；离开该范围即删除。空行也有缓存，构建异常可在下一事件重试。宽度变化重新测量，位置变化只重新排列；内容失效刷新已有行。需要在每次 GUI 事件重建行的调用方可设 `CacheItems = false`。普通 ScrollView 不提供整棵控件树的虚拟化。

`ImguiRenderer` 实现 `IDisposable`。运行时在卸载视图或挂载失败时释放渲染器持有的绘制资源；直接使用者应在销毁宿主时调用 `Dispose()`。AccessoryBatchManager、ClothingCompression 高级窗口、ExpressionControl、JuiceControl 已接入；它们的 View、适配器及真实控件回归可作为完整使用示例。

`ScrollView.ScrollPosition` 与 `VirtualList.ScrollPosition` 可选地引用同一个 `ScrollPosition`，用于并排列表同步滚动。外部请求位置与各视口的实际夹取位置分开保存：较短列表到达底部不会把较长列表拉回，只有实际输入产生的移动才写回共享位置。调用 `Reset()` 可在切换来源时同步回到顶部。AccessoryBatchManager 的复制页面使用此接口。

## 外观与交互

默认主题为 **`MaximumTheme.MetroLight`**，另提供 `MetroDark`、原有 `CleanLight` 和 `ExpressionLight`。0.4.0 以 Windows 8–10 的 Metro / MDL2 为设计依据：白色主表面、浅灰分区、中性按钮、方角、蓝色强调、选中侧边标记和清楚的键盘焦点。输入框使用完整的 2px 边框，选中、悬停、按下、禁用与焦点分别处理。复选框、单选框和开关有各自的几何与反馈；关闭、展开、搜索、加减和排序图标来自同一组线段几何，不依赖字符代替图标。

排版参考 [Microsoft 2015 UWP 设计指南](https://download.microsoft.com/download/2/4/A/24A81A29-77CF-4AA5-967E-64E42554F21B/UWP%20app%20design%20guidelines%20v1509.pdf) 与 [Windows 10 的 Segoe UI / 4px 网格说明](https://learn.microsoft.com/en-us/archive/msdn-magazine/2015/windows-10-special-issue/ui-design-responsive-design-for-universal-windows-apps)。本库为插件编辑器采用 14px 正文、20px 行高、32px 常规控件高度，标题为 24px Semilight，分节标题 20px，注释 12px。它是适应桌面插件密度的实现；没有套用 2015 UWP 的 15px 正文作为所有控件的强制尺寸。放大字号同时扩大共用行高，不再按屏幕宽度单独放大字体。

`Label.Role` 支持 `Body`、`Heading`、`Subheading`、`Muted`、`Caption`，已有显式 `Label.Style` 优先。`Button.Variant` 支持 `Default`、`Primary`、`Quiet`、`Danger`；`ToggleSwitch` 提供独立的立即生效开关，不改变原 `Toggle` / `Checkbox` 的语义。`DrawList.Text` 可携带 `TextFormat`，显式传递字号、字重、对齐、换行与垂直位置；没有字号时继续保留调用方的原生样式。`ThemeTypography.CreateLayoutMetrics` 统一布局度量，游戏端使用实际 `GUIStyle`，预览使用 Pango 排版测量。

0.5.0 的 `Selectable.ShowCheckbox` 可为现有选择行显示真正的复选框几何，保留整行点击、Shift 范围选择、双击及原来的标签文字。`Panel.Selected` 为活动编辑单元显示 1px 强调色边框；边框在子控件之后绘制，避免被内部表面遮盖。两个属性都默认关闭。`HelpBox` 按实际可用宽度测量换行高度，原生与共享绘制同时使用对应的警告/错误背景。

原生字体优先使用系统 Segoe UI 字重家族，再回退到微软雅黑、日文字体与 Noto；由 Unity 5.6 的 `CreateDynamicFontFromOSFont` 按字符选择已安装字体。字体按字重缓存，未分发 Microsoft 字体文件。Linux 模拟预览使用可用回退字体，不能当作 Windows Segoe UI 字形完全一致的截图。自定义主题可设置 8–32px；标题、行高与换行随字号一起适配。

内置双配色的正文、次要文字、主按钮各状态文字、输入边框及焦点/选中标记对比由纯托管回归验证。默认强调色为 `#0076D7`，使白色主按钮文字达到 4.5:1；禁用控件使用单独的灰色状态。

主题可在挂载后真正切换，而不必卸载视图：

```csharp
MaximumUIRuntime.SetTheme(mountId, MaximumTheme.MetroLight);
// Returns false if the mount no longer exists.
```

`ThemePalettes.MetroDark()` / `MetroLight()` 返回相互独立、可编辑的纯托管 `ThemeTokens`。`MaximumTheme.Create(tokens, fontSize: 14, name: "My panel")` 会复制并规范化令牌，再创建原生样式。不要就地修改正在使用的 `theme.Tokens`；编辑副本后创建新主题并调用 `SetTheme`，让原生样式和缓存一起切换。调用方拥有自定义主题，替换后或卸载后需 `Dispose()`；不要在别的挂载仍在引用时释放。内置共享主题的 `Dispose()` 不销毁全局资源。Gallery 在明确点击 Apply 时才创建主题，并释放自己持有的旧主题，避免每次颜色拖动分配原生纹理。

ThemeEditor 的 `ColorTokens` / `ScalarTokens` 可选数组限定可编辑项，null 沿用全量 token，空数组表示没有这一类项。颜色标签按整组最长文字计算固定列宽，RGBA 字段保持纵向对齐；字号与滑条/勾选框尺寸都会进入原生样式。原生 MDL2 后端保持固定方角和边框规则，因此 Gallery 不提供 `Radius` / `BorderWidth` 编辑。`TextDisabled` / `ButtonDisabledBg` 用于禁用按钮、选择控件和自绘内容；原生文本输入还会经过 Unity 自身的禁用绘制。

滚动条使用 16px 稳定轨道、居中的 8px 拖块和常规尺寸下 24px 的最小拖块。正常、悬停、按下和禁用状态采用不同的主题颜色；短列表按实际需要显示轨道并使用完整内容宽度。`AlwaysShowVertical` / `AlwaysShowHorizontal` 仍支持明确要求的常驻轨道；没有可滚动内容时，这类轨道不代表可执行的操作。

`MaximumTheme.BeginScrollView` 在首次使用时创建私有 `GUISkin`，同时设置轨道、拖块、方向按钮和滚动背景。每次调用只在原生 `GUI.BeginScrollView` 内暂时切换 skin，并在 `finally` 恢复调用者皮肤；主题释放时一起释放这个皮肤。拖块长度采用原生的“最小长度加剩余轨道比例”公式。极短轨道同时缩减端部留白和最小拖块，保留正的拖动计算分母；内容视口塌缩到零时跳过原生滑块处理，保持滚动 clip 和 control ID 顺序。

此实现针对 [Unity 5.6 的原生 BeginScrollView 重载](https://docs.unity3d.com/560/Documentation/ScriptReference/GUI.BeginScrollView.html)，并交叉核对了 Unity 官方 2017.4 的 [GUI.cs](https://github.com/Unity-Technologies/UnityCsReference/blob/2017.4/Runtime/IMGUI/Managed/GUI.cs) 与 [SliderHandler.cs](https://github.com/Unity-Technologies/UnityCsReference/blob/2017.4/Runtime/IMGUI/Managed/SliderHandler.cs)。编译所用 Unity 5.6 引用程序集的方法体经过裁剪，不能用它证明实际游戏运行行为；原生拖块命中、捕获释放与字体显示仍需要游戏内验证。

### 滚动框架与诊断接口

0.5.0 的原生渲染器与模拟绘制共用公开的 `ScrollGeometry` / `ScrollFrameLayout`。列表外框、滚动宿主和内容剪裁是不同的矩形，外框在内容 clip 之外、行和滚动条之后绘制，因此滚动条不会吃掉右侧边框。

| `ScrollFrameLayout` 字段 | 用途 |
| --- | --- |
| `Bounds` | 包含 1px 边框的完整控件区域。 |
| `Viewport` | 边框内的原生滚动宿主。 |
| `ContentViewport` | 扣除实际滚动槽后的可见内容区域。 |
| `Content` | 从原点开始的内容尺寸，供原生滚动和末行定位使用。 |
| `VerticalGutter` | 内容右侧、外框以内的垂直轨道区域。 |
| `Vertical` / `ClampY` | 是否显示垂直轨道，以及与实际视口一致的位置夹取。 |

`ListFrame`、`DebugFrame`、`ThemeEditorFrame` 和 `VerticalFrame` 提供生产使用的测量规则。诊断或预览代码优先调用 `WidgetPainter.TryGetScrollFrame(LayoutNode, out ScrollFrameLayout)`：它使用当前 painter 的度量及真实筛选后项目数，支持内建列表、虚拟列表、调试与主题面板。不要另猜行高、表头高度或滚动槽宽度。

`WidgetPaintState.ScrollPositions` 以节点 `Path` 指定模拟绘制的位置，`HoveredScrollbarPath` / `ActiveScrollbarPath` 指定轨道交互状态。内建列表、调试和主题面板会据此绘制实际可见行；`VirtualList` / 普通 `ScrollView` 的宿主还负责安排可见子节点与滚动 clip。Gallery 预览通过这些生产接口生成顶部、中部、底部与轨道状态，不替代原生输入运行。

需要自行安排子节点 clip 或变换的宿主，可使用 `WidgetPainter.PaintNode(node, state, beforeChildren, afterChildren)`。它只绘制当前节点，宿主依次输出前置命令、子节点、后置命令，并通过 `WidgetPaintState.ParentEnabled` 传递祖先的禁用状态。不要用整棵树的命令数量相减来猜测子节点范围；选中面板等控件有必须在子节点之后执行的描边。普通 `Paint` 继续提供完整递归绘制。

普通 `ScrollView` 使用最终分配宽度决定换行和滚动槽。父布局缩小高度时会为滚动条重新测量，扩大宽高后也能撤销不再需要的槽；`VisibleItems` 对应完整行数，边框另计，不会因边框额外制造滚动条。筛选输入保持在列表之外，改变筛选时重置列表位置。

嵌套滚动的窄宽度重测集中在最终排列阶段，避免每一层的初测都重复测量整个子树。5 组专门回归覆盖 1–8 层溢出、自然高度、双轴滚动、内容/度量失效和仅移动窗口；保留缓存不会用旧文本或旧字体测量替代当前内容。

主题与绘制纹理不生成 mipmap。平面表面使用小型九宫格纹理；选择控件与自定义绘制的填充圆复用懒创建的抗锯齿圆纹理，以一次纹理绘制替代按扫描行绘制。`UnityPainter` 按来源样式和 `TextFormat` 建立至多 128 项的白色文字样式缓存，确保绘制颜色不被来源样式的黑色文字乘成黑色；切换主题清理缓存，绘制异常恢复 GUI 颜色与矩阵。图标几何不加载字体或每帧创建贴图。

自定义样式与绘制命令只在 Repaint 事件执行；输入控件仍处理相应输入事件。滚动容器按完整内容高度测量，长列表建议使用 `VirtualList`；普通 `ScrollView` 的全部子控件仍会参与渲染遍历。下拉与 ListBox 仅绘制可见行；首次构建/失效时仍需处理全部选项，因此超大数据集应优先使用虚拟列表。

模态窗口在挂载之间统一确定输入归属，并置于其他挂载之上。同一挂载内只有最上层对话框可交互；相同优先级保持注册顺序。关闭确认框时先移除该对话框再调用确认回调，回调新建的对话框不会被误关。`CloseAll` 只关闭调用时存在的对话框。

内联 `Dialog` 是独立叠加窗口，不参与父栈、网格、表格或分栏的普通布局流，也不会扩大滚动内容范围。布局仍保留其原始声明路径及独立内容测量，因此打开模态不会压缩背景列表，模态发现、查询与按钮输入继续可用。

### 窗口、输入与生命周期

`Window.Resizable` 默认开启，右下角拖动调整大小。折叠后展开恢复原高度；分辨率小于指定最小尺寸时以屏幕边界优先，标题与控制按钮保持可达。对话框独立使用 `DialogContentPadding`，服务 Alert/Confirm 按实际文本度量计算尺寸，过长正文使用滚动区，操作按钮留在外面。

多挂载在绘制前按可见顺序确定鼠标归属，键盘跟随最后点击或明确请求焦点的挂载。可调用 `MaximumUIRuntime.Focus(mountId)` 聚焦并前置普通窗口，再通过 BuildContext 的 Navigation 聚焦控件。窗口前置同步绘制与命中顺序，保留明确的非窗口渲染层边界；已打开的模态仍拥有最高输入优先级。RequestFocus 仅在新请求出现时生效；不要永久依赖 true 每帧重新抢焦点。Tab / Shift+Tab 使用当前可见、可用控件的顺序，常用按钮/选择控件支持 Enter / Space；列表/下拉/滑条/树有对应方向键行为。普通鼠标聚焦不会立即启动整窗的键盘导航。

通过 `IsMounted(id)` / `IsVisible(id)` 查询挂载，通过 `SetVisible(id, visible)` 显示或隐藏。隐藏保留窗口与用户状态，立即释放输入、清除浮层队列、关闭服务对话框，并取消该次显示期间尚未执行的控件回调；隐藏后再打开不会执行旧确认操作。由 View 自行声明的内联 Dialog 状态仍由 View 管理，四个接入编辑器会在关闭流程清理业务模态与目标。没有变化的可见性调用直接返回。显示会重新构建并请求焦点；彻底离开宿主场景时调用 `Unmount`。全局 `IsAnyTextFieldFocused` / `IsModalOpen` 可用于插件快捷键保护。

嵌套滚动和分组保留屏幕原点与祖先剪裁交集，浮层定位、拖放和 Query 使用屏幕坐标。控件新捕获需同时命中自身矩形与当前可见区域；已经捕获的拖动可继续越出矩形，松开仍清理。隐藏、卸载、模态抢占和已被其它 OnGUI 消费的 MouseUp 都有清理路径。底层挂载仍可保留几何 Hover 查询，但不会把被屏蔽的输入登记成 Active / Clicked。

嵌套滚轮由最内层还能沿事件方向移动的视口接收；到达该方向边界时保留事件，交给外层。水平和垂直 delta 各自作用于原生对应轴。`VirtualList` 的键盘焦点支持 Home / End、PageUp / PageDown 及上下移动；搜索列表的输入框有独立导航身份，文本编辑不会触发后方列表的翻页或选择。

通用 `TextField` / `TextArea` / `PasswordField` / `SearchBox` 可仅通过 `OnSubmit` / `OnBlur` 提交。失焦事件保留最后的编辑草稿，再捕获提交值；同一事件先排 `OnEdit` / `OnChange`，后排提交回调。提交完成后同步归一化的模型值，较早的延迟回调不会覆盖随后输入的内容。点击真正的命令按钮时先在按下阶段释放文本焦点，按钮在松开阶段执行，因此“保存”可以读取刚提交的值；滚动条和搜索清除按钮保留编辑焦点。空闲文本同步及无变化的普通/数字字符过滤复用现有字符串，避免每次 GUI 事件创建重复缓冲区。

渲染器只保留一个当前编辑字段的提交所有权。即使 `VirtualList` 已回收离屏行，后续命令按下或键盘激活仍先排队该字段的失焦提交；换字段也会先交接原草稿，避免覆盖或重复提交。提交保留原控件及浮层代次的保护，不借用“保存”按钮的身份。隐藏、卸载和挂载失败明确清除这项所有权，普通滚动条拖动与 MouseUp 捕获清理不会清除它。相关断言执行实际虚拟行缓存和托管回调队列，并不等同于 Unity 的完整文本输入实测。

InputInt / InputFloat / InputDouble 可实际键入。编辑中保留负号、小数点和指数等中间态；完整有效数字才触发回调。Enter/失焦提交，Escape 回滚；外部状态真正改变时同步，已排队但尚未应用的回调不会被误判成外部覆盖。解析接受当前文化和 invariant 小数，禁止千位误读，拒绝非有限数值及类型溢出。编辑器使用 round-trip 数值文本；`Format` 只控制未聚焦的显示，含单位或很长的格式化文本不会进入解析。没有 OnChange 的数值字段只读。

下拉默认定位到当前值，Escape/Tab/外部点击关闭；菜单和上下文菜单优先处理自己的输入，嵌套菜单支持进入与返回。标签页重排保留 MouseDown/Drag/原始 MouseUp 状态，不因按钮消费事件而漏掉回调。重新挂载、隐藏和主题切换清理遗留浮层。

Runtime Update 采用稳定挂载快照，允许回调卸载当前视图或添加新视图，新挂载在下一次 Update 进入遍历。渲染期间排队的回调关联来源挂载及可见性代次，卸载或隐藏后跳过；有效回调完成后自动使其所属 View 失效。同一次 Flush 中新增工作留到下一次，异常不阻断同批其它回调。自定义主题仍由调用方按上述所有权规则释放。

查询与导航的内部记录使用有界对象池，Widget 缓存稳定 Key / Path 的规范标识；Key 应视为稳定身份，替换 Key 或路径会刷新缓存。公开 `GetItem` / Snapshot 返回副本，不会因下一帧复用记录而改变。池分别最多保留 512 个项目/导航记录和 32 个窗口记录，超出部分仍正确处理，但不保证零分配。

## 无界面回归与性能测量

当前测试包括原有控件布局、输入分发与绘制命令断言，以及同帧失效、失效期间重建、布局复用/移动/缩放、长内容滚动、窄分栏、可见标签页中的模态查找、禁用控件导航、字体/换行尺寸、Repaint 策略、拖拽释放策略、自引用绘制列表及重入对话框回调。浮层测试验证稳定顺序和顶层输入归属。

可选的构建/布局基准使用修复后的同一程序集比较三种调度策略；它不是原始版本与新版本二进制的直接对比。基准保留控件数量和构建/布局次数断言，可在 .NET 9 宿主中读取实际线程分配量：

```sh
dotnet build src/MaximumUI.Tests/MaximumUI.Tests.csproj -c Release
MAXIMUMUI_BENCHMARK=1 dotnet run --project tests/MaximumUI.Runner/MaximumUI.Runner.csproj -c Release -- bin/Release/MaximumUI.Tests/MaximumUI.Tests.dll
```

历史基准场景为 1000 个 Label、150 帧、每帧 2 个 GUI 事件。逐事件策略执行 300 次树构建和布局，默认逐帧策略执行 150 次，按需空闲策略执行 1 次。**以下是 0.2.0 的历史数据，不是 0.5.0 的重新测量**：.NET 9.0.319 宿主中，三者分配量分别为 129,062,440 / 64,538,576 / 430,432 字节。计时受预热、JIT 和运行环境影响；该基准不含 Unity 绘制、输入处理和游戏负载，不能换算为游戏帧率。

当前 UI 入口执行 **238 组回归**。其中 0.5.0 新增 22 组滚动框架/绘制一致性、13 组编辑生命周期/输入路由、5 组嵌套滚动测量、2 组分阶段绘制组合、7 组窗口前置、4 组离屏编辑所有权，以及 6 组 JuiceControl 原布局约定回归。新断言覆盖右边框在 clip 外绘制、所有内建长列表的末行可达、轨道 hover/active、极短滑块的非零拖动分母、宽高变化后滚动槽释放、嵌套滚轮边界、只在失焦提交的草稿、归一化回显、延迟提交保护及命令执行顺序。已有 MDL2 几何、禁用继承、焦点、挂载、弹层、真实编辑器与内联 Dialog 回归继续执行。

热路径断言包括 10,000 选项在 300 次空闲事件中仅格式化一次、100,000 虚拟行只构建并测量当前 13 行、逐行滚动只新增进入的一行，以及预热后的路由、剪裁、数值/文本同步与弹层所属控件检查零分配。0.3.0 增加的 80 个保留控件在 1,000 次查询/导航登记循环中的零托管分配断言也保留。零分配只针对这些被测方法；没有宣称整个 OnGUI 零分配。ClothingCompression 和 ExpressionControl 的实际 View→业务回归由各自插件宿主执行。本轮验证和模拟渲染证据见 [0.5.0 UI 成熟度记录](../../docs/UI-Maturity-2026-10-24.md)；历史结果见 [0.4.0 MDL2 记录](../../docs/MDL2-2026-10-23.md) 与 [此前业务接入记录](../../docs/Integration-2026-10-23.md)。

`python scripts/test.py --benchmark` 也会执行上述策略基准及全部回归。若需比较历史版本与修复版本的独立 DLL，使用 [MaximumUI.Benchmark](../../tests/MaximumUI.Benchmark/README.md) 的显式程序集路径和独立输出目录；该项目不加入解决方案、不产生插件发布产物。

游戏内仍需用 Gallery 验证文本输入、窗口拖动及释放、滚动到末行、无 Key 窗口中的下拉定位、多挂载/多层模态、对话框中的下拉、主题/分辨率变化，以及长时间打开后的内存和帧耗时。真实字体显示、原生 GUI control ID、鼠标捕获与其他插件叠加不在无界面宿主的覆盖范围内。
