# 工作区开发说明

## 环境与构建

推荐 .NET SDK 9；运行测试辅助脚本需要 PowerShell 7。各项目的编译引用由 NuGet 还原，来源配置在 `nuget.config`，版本集中在 `Directory.Packages.props`。

```sh
dotnet restore MaxIllusionPlugins.sln
dotnet build MaxIllusionPlugins.sln -c Release --no-restore
pwsh -NoProfile -File scripts/Test-MaximumUI.ps1 -NoBuild
```

九个项目均在解决方案内。大部分项目继承 `.NET Framework 3.5` 目标；MaximumUI.Tests 使用 `net461`。NuGet 提供框架引用程序集，编译不等于需要本机游戏目录。网络环境需允许访问 nuget.org、IllusionMods 和 BepInExLibs 的仓库中所列源。

ClothingCompression 的 KKAPI、Sideloader 编译引用已改为公开 NuGet 包，去除了原先写死的本机游戏路径。运行插件时仍需在游戏中安装对应依赖。

单项目开发时直接构建其 `.csproj`，例如：

```sh
dotnet build src/ExpressionControl/ExpressionControl.csproj -c Release
```

## 输出与测试

`Directory.Build.props` 把输出统一放在 `bin/<配置>/<项目名>/`；`Directory.Build.targets` 另把各工程主产物复制到 `bin/<配置>/KK/`。后者只是汇总目录，也可能包含测试宿主等开发产物，发布时按实际项目筛选。

MaximumUI.Tests 是一个有 `Program.Main` 的类库。`scripts/Test-MaximumUI.ps1` 构建它，并用反射执行入口和检查返回值，失败会抛出错误；不能把单独运行 `dotnet test` 当成这些断言已执行。脚本应在新的 `pwsh` 进程中运行，以免复用进程中的旧程序集。

无界面测试检查布局、输入、状态和绘制命令；Unity 的实际绘制、角色、动作、镜头、网格变形、保存/读取以及其他插件兼容仍需游戏内测试。MaximumUI Gallery 用于补充 UI 实测，功能插件的默认入口见根 README 和各项目 README。

## 整理时的验证记录

2026-10-23 在本机使用 .NET SDK 9.0.319 完成整套构建和现有 MaximumUI 无界面测试。ClothingCompression 有两处 `Application.loadedLevelName` 的 CS0618 过时 API 警告；这属于已有代码。此次仓库整理未因此改变场景判断逻辑。

MultiTeamH 的 0.11.62 尚无用户游戏内验证结果，详见 [该项目交接说明](MultiTeamH-Handoff.md)。其他项目的构建成功也不代表已经完成全部游戏内回归。

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
