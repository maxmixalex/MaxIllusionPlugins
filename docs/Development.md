# 工作区开发说明

## 环境与构建

推荐 .NET SDK 9；统一测试脚本可使用 Python 3 或 PowerShell 7。各项目的编译引用由 NuGet 还原，来源配置在 `nuget.config`，版本集中在 `Directory.Packages.props`。

```sh
dotnet restore MaxIllusionPlugins.sln -m:1 -nodeReuse:false
dotnet build MaxIllusionPlugins.sln -c Release --no-restore -m:1 -nodeReuse:false
python scripts/test.py --no-build
# Windows / PowerShell alternative:
pwsh -NoProfile -File scripts/Test-All.ps1 -NoBuild
```

解决方案包含原有九个项目及四个执行测试的宿主，共十三个项目。生产项目仍继承 `.NET Framework 3.5` 目标，MaximumUI.Tests 保持 `net461`，`tests/` 下四个宿主使用 `net9.0`。独立的 `MaximumUI.Benchmark` 比较工具和 `MaximumUI.Preview` 实际 Gallery/插件 View 导出工具不在解决方案中，需要显式指定被测 DLL；两者均不作为游戏插件发布。

NuGet 提供框架引用程序集，编译不需要本机游戏目录。网络环境需允许访问 nuget.org、IllusionMods 和 BepInExLibs 的仓库中所列源。包源映射必须包含 HarmonyX、MonoMod.*、Mono.Cecil 与 KoikatuCompatibilityAnalyzer 等传递依赖；缺失映射会在全新缓存下导致 NU1100。

ClothingCompression 的 KKAPI、Sideloader 编译引用使用公开 NuGet 包，不依赖写死的本机游戏路径。本次增加 Common 项目引用以共享原子 XML 保存，部署时必须带上 `MaxIllusionPlugins.Common.dll`。运行插件时仍需在游戏中安装对应依赖。

单项目开发时直接构建其 `.csproj`，例如：

```sh
dotnet build src/ExpressionControl/ExpressionControl.csproj -c Release
```

## 输出与测试

`Directory.Build.props` 把输出统一放在 `bin/<配置>/<项目名>/`；`Directory.Build.targets` 另把生产工程主产物复制到 `bin/<配置>/KK/`。测试项目设置 `IsPluginArtifact=false`。旧版本留下的文件不会自动删除，因此发布前仍须筛选，不能直接打包整个开发输出目录。

`scripts/test.py` 和 `scripts/Test-All.ps1` 顺序执行四个真实断言入口，任一失败均返回失败。默认先整仓构建；已构建可使用 `--no-build` / `-NoBuild`。Python 脚本支持通过 `DOTNET` 环境变量指定 SDK 可执行文件。

| 测试宿主 | 被测实现与边界 |
| --- | --- |
| MaximumUI.Runner → MaximumUI.Tests | 加载真实 MaximumUI DLL，执行 net461 类库的 `Program.Main`；布局、状态、输入、绘制命令及 Juice/Accessory 实际 View 与会话守卫 |
| MultiTeamH.Regression | 链接实际兼容补丁、会话选择和协程清理实现；游戏、Harmony 与可选插件边界使用明确测试替身 |
| AccessoryClothing.Regression | 链接实际饰品操作/会话、高亮、材质桥、形变控制器、XML/目录、Clothing View/适配器与缓存；模拟外部角色、网格与事件服务 |
| PluginCore.Regression | 链接实际表情四页 View、编辑/删除/重载、筛选、快捷键、XML、SkinEffects 反射桥；使用真实文件和序列化器，模拟外部 Unity/游戏服务 |

MaximumUI.Tests 本身是类库；不能把单独运行 `dotnet test` 当成这些断言已执行。原来的 `scripts/Test-MaximumUI.ps1` 仍可单独运行 UI 测试，应该启动新的 `pwsh` 进程以免复用旧程序集。本次 Linux 验证使用 .NET 9 执行宿主，没有宣称已执行 PowerShell 脚本。

`--benchmark` / `-Benchmark` 追加同一版本中逐事件、逐帧、按需构建策略的托管微基准。真正的原始/修复 DLL 对比方法与限制见 [MaximumUI.Benchmark](../tests/MaximumUI.Benchmark/README.md) 和 [审计记录](Audit-2026-10-23.md)。

无界面测试检查布局、输入、状态和绘制命令；Unity 的实际绘制、角色、动作、镜头、网格变形、保存/读取以及其他插件兼容仍需游戏内测试。MaximumUI Gallery 用于补充 UI 实测，功能插件的默认入口见根 README 和各项目 README。

## 验证记录

2026-10-23 使用 .NET SDK 9.0.319 完成整仓修复和统一执行回归，具体数量、基线反例、性能数据与未验证内容记录在 [审计记录](Audit-2026-10-23.md)。ClothingCompression 的两处 `Application.loadedLevelName` CS0618 过时 API 警告属于已有代码；本次没有仅为消除警告而改变场景判断逻辑。饰品/服装 .NET 9 测试宿主还会提示共享 net35 源码使用 `XmlReaderSettings.ProhibitDtd` 已过时；这是旧目标框架所需的 API，不代表未阻止 DTD。最终全量编译共 3 个警告、0 个错误。

上一轮完成整仓 Rebuild 和 226 组实际断言：MaximumUI 104、MultiTeamH 39、Accessory/Clothing 38、PluginCore 45，详见 [后续修复记录](Followup-2026-10-23.md)。四插件接入、性能缓存、真实 XML 往返、可下载 CI 与最新执行结果见 [接入记录](Integration-2026-10-23.md)。当前 MultiTeamH 为 0.11.64；原交接记录中的 0.11.62 相机行为，以及后续兼容/自动结束修复仍无游戏内验证结果，详见 [原交接说明](MultiTeamH-Handoff.md)。所有项目的构建成功均不代表已经完成全部游戏内回归。

MaximumUI 0.3.0 接入后的整仓 Rebuild 为 0 错误、4 警告：上述三条旧 API 提示与 AccessoryClothing 测试宿主引用旧框架 UI 的 NU1702。实际执行 **303 组**：MaximumUI 139、MultiTeamH 39、Accessory/Clothing 56、PluginCore 69；另有 5 项打包规则测试。三份上传 XML 通过生产加载器的临时副本往返校验。Actions 重复公开构建/测试/预览/打包流程，私人配置不参与线上执行。

MaximumUI 0.4.0 MDL2 本轮完成整仓 Release Rebuild 和 **343 组**实际断言：MaximumUI 179、MultiTeamH 39、Accessory/Clothing 56、PluginCore 69；另有 **16 项** Python 打包/预览检查。构建仍为 0 错误、4 个上述已有警告。Gallery 全页、主题、字号、弹层和连续滚动的模拟渲染及边界检查详见 [MDL2 记录](MDL2-2026-10-23.md)。

## 维护约定

- 源码放在 `src/<项目名>/`，共用构建设置优先留在根 `.props` / `.targets` 中。
- 表情、预设和形变配置已有用户数据；改模型时保留原 XML/标识的兼容性。
- 多角色兼容应核对对象所属角色和队伍，尤其是共享的静态状态、原生 UI、语音、镜头和协程。
- 公共 API 有正式源码或文档时优先按实际版本查阅；对本机私有字段的反射不能仅凭新版上游源码猜测。
- 不把 `bin/`、`obj/`、临时版本目录、日志、角色卡、用户配置、第三方 DLL 和反编译资料混入源码提交。
- 用户未明确要求时不要计算 SHA256。

## 线上 Agent 与本机参考资料

[MultiTeamH 依赖索引](MultiTeamH-Dependencies.md) 提供公开源码标签和本地参考包清单。本地 `.local-reference/` 被 Git 忽略；线上环境只拿到公开仓库时不会自动得到这些资料。

参考包中的游戏与插件 C#/IL 是本机实现的阅读快照，不是重新构建游戏的工程，也不随本源码的 GPL 许可重新授权。它不能提供可运行游戏环境。调查游戏行为时，应记录所测 DLL 版本、最短复现步骤和日志，再区分“构建通过”“已部署”与“实测通过”。
