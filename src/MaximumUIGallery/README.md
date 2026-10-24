# MaximumUI Gallery

MaximumUI 0.5.0 的独立控件工作台，默认使用 **Metro Light**。以 Windows 10 MDL2 的排版、方形控件、蓝色强调、平面导航与清晰状态反馈为基础。Gallery 是可选的开发示例，不是四个业务编辑器的运行依赖。

## 页面与功能

| 页面 | 内容 |
| --- | --- |
| Form | 名称、模式、强度、即时开关和提交反馈；仅操作 Gallery 样例数据 |
| Buttons | 标准、强调、轻量、危险、禁用、小型及方向按钮；链接、进度、消息、状态与上下文菜单 |
| Text input | 普通文本、字符过滤、十六进制、只读、密码、搜索、多行输入及文本控件 |
| Numeric input | 整数、浮点、双精度、拖动数值、三类滑块、角度与向量 |
| Selection | ToggleSwitch、勾选框、单选框、下拉、搜索下拉、分段选择、列表、多选和树 |
| Colors | RGB/RGBA 编辑、颜色按钮、颜色选择器及通用颜色编辑 |
| Drawing | 图片、图片按钮、折线/直方图、DrawCanvas、DrawList、拖放 |
| Layout | 菜单、标签页、栈、空白、网格、可排序表格、虚拟列表、长/短/空列表、横向和嵌套滚动、分栏与可折叠区域 |
| Dialogs | 消息、确认和带文本/下拉输入的模态编辑器 |
| Appearance | 四个内置主题、文字大小、全部受支持颜色/尺寸令牌、控件状态预览与应用/撤销 |
| Large lists | 10,000 条可搜索记录；只构建可视行，筛选时保留选择 |
| Diagnostics | 运行指标、输入、日志、状态、控件路径、选择器、示例目录与使用指南 |

窗口默认 1000 × 760，最小 820 × 600。页面按实际剩余高度滚动，导航保持可见；内容放得下时不显示无作用的整高滚动条。普通行高从 32 起按 4 像素步长随文字大小增加，避免放大文字后输入被裁切。外观页的 Apply/Revert 始终位于滚动内容之外。

默认启动时隐藏，按 **F10** 切换。在 `MaxMixAlex.MaximumUIGallery.cfg` 的 `General / Show gallery` 修改快捷键，`Show on startup` 控制启动时是否显示。编辑文本或打开模态时不触发窗口快捷键。`Ctrl+1` 至 `Ctrl+6` 分别打开 Buttons、Layout、Dialogs、Appearance、Diagnostics、Large lists；`Ctrl+L` 添加日志示例。

## 构建与性能

```sh
dotnet build src/MaximumUIGallery/MaximumUIGallery.csproj -c Release
python scripts/test.py
```

输出为 `bin/Release/MaximumUIGallery/KK_MaximumUIGallery.dll`，运行时需同时提供 `MaximumUI.dll`。

Gallery 使用 `BuildEveryFrame = false`，仅在状态变化时构建当前页面；Diagnostics 每 0.25 秒刷新。10,000 条样本按需创建，文本筛选只在查询变化时执行。外观编辑先修改托管令牌副本，Apply 才创建原生主题资源；替换与卸载释放自有主题。示例中的表格排序会实际改变行顺序，关闭的可折叠预览可以恢复。

## 完整 Gallery 渲染

`GalleryScenes` 提供纯托管入口，供渲染工具和回归测试枚举**实际生产页面**，不维护第二套示意 UI：

```csharp
string[] pages = GalleryScenes.PageIds;
string[] titles = GalleryScenes.PageTitles;
string[] states = GalleryScenes.StatesForPage("dialogs");
View view = GalleryScenes.Create("dialogs", ThemePalettes.MetroLight(), "dialog-open");
Widget window = view.Build(new BuildContext());
```

当前有 12 个页面；公开状态目录包含禁用、编辑后、选择、折叠、各标签页、打开的输入对话框、待应用主题、筛选结果和清空滚动列表。创建场景不会挂载窗口或初始化 Unity，返回的仍是游戏内使用的 `GalleryView`，所有声明和操作回调共用生产实现。

渲染工具进一步覆盖主题、尺寸、字号、横纵滚动分片和控件反馈，生成可浏览索引、绘制预览与检查报告。内建列表、主题编辑器和日志使用真实滚动偏移；滚动条悬停/按下样式也参与导出。严格检查包含最终 PNG 右边框，防止轨道再次遮挡列表边框。具体命令和输出见 [MaximumUI.Preview](../../tests/MaximumUI.Preview/README.md)。

模拟渲染检查布局、字体度量、绘制命令、控件覆盖和状态，不等同于 Unity 游戏截图。原生图片资源仅在游戏运行时提供；无 Unity 的导出保持真实空图像状态。中文输入法、鼠标捕获、游戏快捷键及最终帧耗时仍需实机检查。
