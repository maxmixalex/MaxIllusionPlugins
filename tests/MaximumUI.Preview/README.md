# MaximumUI Gallery 渲染与检查

本工具加载本次构建的 `KK_MaximumUIGallery.dll`，通过公开的 `GalleryScenes` 目录枚举真实页面和状态，再运行 **生产 `GalleryView.Build` → `LayoutEngine` → `WidgetPainter`**。它也保留四个业务插件的匿名示例，用于检查库样式变化是否影响既有编辑窗口。

输出的 HTML 只是截图浏览器；所有界面内容来自生产 C#，没有另一份 HTML/CSS 控件实现。

## 输出范围

- Gallery 的全部页面：Form（page ID `overview`）、Buttons、Text input、Numeric input、Selection、Colors、Drawing、Layout、Dialogs、Appearance、Large lists、Diagnostics。
- 每页的亮色默认视图、深色视图、声明的最小窗口尺寸、18 px 正文字号。
- `GalleryScenes.StatesForPage` 公开的禁用、已编辑、选中、折叠、空结果、待应用主题和弹窗等状态。
- 主按钮的悬停、按下和键盘焦点，以及文本输入的焦点。
- ComboBox 展开、底部滚动、可搜索下拉的筛选和无结果；真实 File/View/Edit 菜单、子菜单返回栏、按钮上下文菜单及 Tooltip。
- 实际 Gallery 按钮回调创建的 `DialogService.Alert` / `Confirm`；仅替换字体测量和屏幕尺寸边界。
- 每个主题、尺寸和目录状态的普通滚动区域从顶端到底端的连续分片；虚拟列表取顶端、中部和底部，验证只构建可见行及缓冲行。嵌套列表必须实际滚入父视口才计作覆盖。
- AccessoryBatchManager、JuiceControl 的实际生产 View 源码；ExpressionControl、ClothingCompression 从各自回归宿主调用真实 View 工厂。

匿名示例不读取角色卡、游戏状态、用户 XML 或本地配置。

## 运行

先按仓库根 README 构建解决方案。Linux 的严格预览使用 Pango 排版和 Inkscape 栅格化；这些依赖只供开发工具使用，不进入游戏插件。

```sh
sudo apt-get install -y inkscape fonts-noto-cjk python3-pil
python3 scripts/prepare_preview_fonts.py

dotnet build tests/MaximumUI.Preview/MaximumUI.Preview.csproj -c Release -m:1 -nodeReuse:false

dotnet run --project tests/MaximumUI.Preview/MaximumUI.Preview.csproj \
  -c Release --no-build -- \
  bin/Release/MaximumUIGallery/KK_MaximumUIGallery.dll artifacts/previews --require-shaped

python3 scripts/render_gallery.py artifacts/previews --strict
python3 -m unittest discover -s scripts/tests -p 'test_gallery_preview.py' -v
```

打开输出目录中的 `index.html`，选择页面及其主题、尺寸、状态或滚动位置。下载 CI 的 `maximumui-previews-…` 产物后，也可在解压目录直接打开，无需服务器。

| 产物 | 用途 |
| --- | --- |
| `gallery-*.svg` / `.png` | 生产 Gallery 的每个采样帧 |
| 七张业务插件 SVG / PNG | 既有界面兼容性检查 |
| `gallery-contact-*.png` | 全部亮色默认页面的分组总览 |
| `index.html` | 本地截图浏览器与 SVG/PNG 链接 |
| `preview-manifest.json` | 生产目录状态、完整案例清单、程序集版本、字体、控件类型/可见类型、视口、虚拟行数、实际可见滚动范围和文字检查明细 |
| `quality-report.json` | 结构、连续滚动覆盖、字体和文本质量检查结果 |
| `README.md` | 本次生成结果及验证边界 |

## 检查方式

`ThemeTypography.CreateLayoutMetrics` 提供与游戏端共用的布局度量。Pango 对与 SVG 相同的字体回退链进行实际测量，替代旧工具的固定字符宽度估算。字体准备脚本安装 Microsoft 开源 Selawik 的五种字重，作为 Linux 上 Segoe UI 的度量兼容回退；中文使用 Noto Sans CJK。清单的 `primaryFont` 记录实际解析到的拉丁字体。绘制命令中的 `TextFormat` 提供字号、字重、对齐、换行和垂直位置；SVG 将文本限制在真实内容框内。

排版器关闭 Pango 的整像素字宽取整和 Cairo 字体 hint metrics，使用与 SVG 轮廓相同的连续度量。固定版本的 Selawik 把 Light 和 Semilight 均声明为 weight 300，Pango 请求 350 时实际回退至 Light；SVG 1.1 对 350 的解析则会退为 Regular。因此这个已知回退在 SVG 中写为 300，并用 `data-requested-font-weight` 保留生产请求的 350。游戏端仍按原有 Segoe UI Semilight 家族选择字体；预览的这项字体限制不会修改运行库的字重。

生产滚动几何决定内容范围、视口及虚拟行区间。导出器仅扩展生产 `BuildItem` 返回的可见行，并把生产绘制命令置于对应裁切作用域中。它不会把十万行列表全部构建出来。

清单分别记录 `declaredWidgetTypes`（真实控件声明树，含上下文菜单）、`widgetTypes`（实际布局节点）和 `visibleWidgetTypes`（穿过裁切后有可见区域的节点）。`TableColumn` 等结构声明由所属 `Table` 绘制，不会为了让类型数量一致而伪造独立的可见控件。

弹层使用与原生端共用的 `PopupGeometry`、`PopupPresentation` 和 `ChoiceCache`。菜单内容直接取实际 Gallery 的 `Menu` 树；上下文菜单用生产布局器重新布局真实 `ContextMenuContent`。对话框置于主视口中央并在滚动裁切层之外绘制，内联和服务对话框使用同一灰色遮罩。

结构检查拒绝非有限/负尺寸、非滚动内容越出窗口，以及非空虚拟列表没有可用视口。独立的 Python 检查要求每页具备完整主题/尺寸矩阵、目录中的每个案例实际导出、普通滚动分片没有中间缺口、虚拟列表具备顶/中/底采样且不超出可见行预算，并拒绝产物同名覆盖。

文本检查先用 Pango 的实际字形边界检查可见文字的水平溢出、垂直截断、重叠和缺字。PNG 生成时，Inkscape 在同一进程中返回实际 SVG 字形边界，再独立检查真实渲染结果是否超出生产内容框，或与同一绘制层上的其他文字重叠。同一控件内部的多个文本命令也受检查；弹层遮住背景属于正常分层。输入框等本身允许滚动/裁切的控件不把正常内容裁切记为溢出。`--strict` 会在任一文字问题尚未解决时返回失败。

几何边界允许 1 px 的栅格取整误差；文字框交叠在两个方向均超过 2 px 时计为重叠。这些固定阈值同时用于测量与实际渲染检查，未针对个别页面放宽。报告的 0 项表示没有超出这些检查阈值的问题，不代表已经执行 Unity 字形或输入验证。

## 验证边界

这是对真实控件声明、布局和托管绘制路径的模拟运行。它没有启动 Unity、Koikatsu、BepInEx、游戏内输入法或 GPU 渲染。Linux 通常没有 Segoe UI，实际采用已安装的回退字体；字体名称和测量后端记录在清单中，因此不能把这些图称为 Windows 游戏截图或声称字形逐像素一致。

没有纹理的 Image 按真实绘制语义保持空白；工具不会捏造图片。原生 Unity 纹理不在该导出器中解码。原生输入事件、OS 字体加载以及现场游戏环境仍需实机验收。任何 CI 或托管测试通过都不应被描述为已完成这些实机验证。
