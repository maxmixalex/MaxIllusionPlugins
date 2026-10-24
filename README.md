# MaxIllusionPlugins

面向 **Koikatsu / Koikatsu Party** 的 C# 插件与 UI 开发工作区，使用 SDK 风格工程和集中 NuGet 版本管理。游戏插件主要面向 BepInEx 5、Unity 5.6 和 .NET Framework 3.5；MaximumUI 的测试宿主使用 .NET Framework 4.6.1。

这是开发中的源码集合。构建通过和无界面测试通过不代表所有插件、动作及其他模组组合已完成游戏内验证。

## 下载开发构建

[GitHub Actions](https://github.com/maxmixalex/MaxIllusionPlugins/actions/workflows/ci.yml) 会在每次推送和 PR 更新后编译、执行回归、生成界面预览并上传安装包。打开 `fix/plugin-audit-maximumui` 分支最近一次绿色运行，在 **Artifacts** 下载 `plugin-packages-…`；其中提供五插件合集、各插件单独 ZIP 和可选的 Gallery。共享库已按实际依赖包含，解压所选安装 ZIP 内的 `BepInEx` 到游戏根目录。完整步骤见 [CI 与下载说明](docs/CI.md)。

更新时请先移除其他目录里的同名旧 DLL，避免重复加载。ClothingCompression 的 XML 现使用 `MaxMixAlex.ClothingCompression.xml`；旧文件请自行重命名，程序不回退读取旧名字。

## 项目

| 项目 | 介绍 | 使用入口 |
| --- | --- | --- |
| [MultiTeamH](src/MultiTeamH/README.md) | 自由 H 的“多P”入口，2F2M 双队、焦点切换、后台流程及可选插件兼容 | `Tab` 切队，`-` 打开面板 |
| [AccessoryBatchManager](src/AccessoryBatchManager/README.md) | 角色编辑器中的饰品多选、批量整理、跨搭配复制/移动与高亮 | `Shift+=` 或小键盘 `+` |
| [ClothingCompression](src/ClothingCompression/README.md) | 身体勒肉/局部网格形变，支持服装饰品绑定、左右侧控制和配置保存 | 编辑器“勒肉形变”页，`F8` 高级面板 |
| [ExpressionControl](src/ExpressionControl/README.md) | 角色表情编辑、表情程序、H/Studio 场景中的表情控制和 XML 预设 | 默认 `L`，可在 XML 中修改 |
| [JuiceControl](src/JuiceControl/README.md) | 手动调整角色皮肤效果、锁定效果等级、角色及分组预设，兼容 SkinEffects | 默认主键盘 `0` |
| [MaximumUI](src/MaximumUI/README.md) | 声明式 IMGUI 控件、布局、输入、绘制列表、主题和运行时挂载框架 | 供其他插件调用的库 |
| [MaximumUIGallery](src/MaximumUIGallery/README.md) | MaximumUI 控件、布局、对话框和调试工具的独立游戏内示例 | 默认 `F10`，启动时隐藏 |
| [MaximumUI.Tests](src/MaximumUI.Tests/README.md) | MaximumUI 布局、输入、选择、绘制及导航的无界面断言测试 | `scripts/test.py` / `scripts/Test-All.ps1` |
| [Common](src/Common/README.md) | 构建版本号、日志与原子 XML 读写辅助代码 | 共享库 |

各项目的实际版本见对应 `.csproj` 和插件版本常量。AccessoryBatchManager、ClothingCompression 高级窗口、ExpressionControl 和 JuiceControl 已接入 MaximumUI 的真实声明式控件。MaximumUI 0.5.0 默认使用亮色 MDL2 主题，本轮重做滚动框和滚动条，修复输入提交与嵌套滚动，并按迁移前 `c0380d6` 恢复四插件的布局、按钮原文和操作路径。之前迁移造成的偏移及逐项核对见 [业务 UI 复刻记录](docs/UI-Parity-2026-10-24.md)。ClothingCompression 的 Maker 原生页面继续提供编辑入口；MultiTeamH 保留现有游戏面板。Gallery 是可选的独立开发示例。

Gallery 提供 12 个页面，CI 从真实 C# 控件树模拟渲染亮色、深色、最小窗口、放大字号与各类交互/滚动状态；四插件也检查各窗口、最小尺寸、滚动与下拉框。质量检查包括最终 PNG 的滚动列表右边框，防止轨道覆盖边框后仍然通过。下载 `maximumui-previews-…` 后打开 `index.html` 可查看整套 PNG、SVG 与检查结果。设计与验证记录见 [UI 成熟度改进](docs/UI-Maturity-2026-10-24.md)。

## 构建与测试

推荐使用 **.NET SDK 9**。编译引用通过仓库中的 `nuget.config` 和 `Directory.Packages.props` 还原，无需安装游戏或复制本机游戏 DLL。

```sh
dotnet restore MaxIllusionPlugins.sln
dotnet build MaxIllusionPlugins.sln -c Release --no-restore -m:1 -nodeReuse:false
```

只开发一个项目时可以单独构建，例如：

```sh
dotnet build src/MultiTeamH/MultiTeamH.csproj -c Release
```

执行所有回归测试（Python 3 或 PowerShell 7 二选一；脚本默认会先构建解决方案）：

```sh
python scripts/test.py
pwsh -NoProfile -File scripts/Test-All.ps1
```

已完成构建时可传 `--no-build` / `-NoBuild`；增加 `--benchmark` / `-Benchmark` 可执行 MaximumUI 的托管构建与布局策略微基准。`tests/` 中的三个插件回归宿主直接编译相关生产源码，仅替换游戏和外部插件边界。MaximumUI.Runner 执行原有 `net461` 断言程序集的真实 `Main`；单独执行 `dotnet test` 不会运行这些断言。

详细构建、输出、测试边界见 [开发说明](docs/Development.md)。首轮修复及基准见 [全仓审计](docs/Audit-2026-10-23.md)，上一轮 Metro UI、后台流程、XML 与表情修复见 [后续修复记录](docs/Followup-2026-10-23.md)；四插件接入、真实配置验证与 CI 交付见 [接入记录](docs/Integration-2026-10-23.md)。

## 游戏内使用

运行插件需要已安装对应游戏、BepInEx 5 和各项目说明中的运行依赖。按需要从 `bin/Release/<项目名>/` 选择插件 DLL，放入游戏的 `BepInEx/plugins/`；有项目引用时也需带上对应库。MaximumUI Gallery 需同时提供 `MaximumUI.dll`。

四个接入插件需要随附同次构建的 `MaximumUI.dll`；引用 Common 的项目还需要 `MaxIllusionPlugins.Common.dll`。CI 安装包已包含这些本仓共享库，无需另装 Gallery。合集只安装五个功能插件与共享库。

`bin/Release/KK/` 是构建汇总目录，不等于经过筛选的发布包。测试工程已禁止向此目录复制主产物，但旧构建留下的文件不会自动删除；不要把测试程序集当作插件安装。角色卡、存档、游戏和第三方运行依赖不随本源码仓库分发。

## 开发资料

- [工作区开发说明](docs/Development.md)
- [GitHub Actions、安装包下载与本地复现](docs/CI.md)
- [MaximumUI 0.5.0 滚动、输入、完整渲染与回归验证](docs/UI-Maturity-2026-10-24.md)
- [四插件迁移前布局、按钮与文案复刻核对](docs/UI-Parity-2026-10-24.md)
- [MultiTeamH 与 ExpressionControl 按角色上下文审计](docs/Runtime-Audit-2026-10-24.md)
- [MaximumUI 0.4.0 亮色 MDL2、全 Gallery 渲染与验证](docs/MDL2-2026-10-23.md)
- [MaximumUI 0.3.0 四插件接入、配置验证与 CI 交付](docs/Integration-2026-10-23.md)
- [全仓审计、回归证据与待游戏内验证事项](docs/Audit-2026-10-23.md)
- [后续修复、MaximumUI 0.2.0 与 226 组回归](docs/Followup-2026-10-23.md)
- [真实 Gallery 与插件控件树的可再生预览](tests/MaximumUI.Preview/README.md)
- [MaximumUI 设计与 API 长文档](MaximumUI.md)（具体接口以当前源码为准）
- [MultiTeamH 开发交接与最后已知验证状态](docs/MultiTeamH-Handoff.md)
- [MultiTeamH 依赖版本、公开源码与本地参考包索引](docs/MultiTeamH-Dependencies.md)

本地反编译参考包、临时构建目录和工具缓存被 Git 忽略。线上 Agent 若需要本地实现快照，应另行获得参考包；仅克隆仓库不会包含它。

## 许可

本仓库沿用 [GPL-3.0 许可](LICENSE)。源码中的原有作者说明予以保留；外部游戏、框架和模组资料分别适用其自身来源与许可。
