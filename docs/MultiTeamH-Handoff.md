# MultiTeamH 开发交接

本说明随源码提交；本机反编译资料单独保存在 `.local-reference/`，已被 Git 忽略。交接整理日期：2026-10-23。

## 构建

```sh
dotnet restore src/MultiTeamH/MultiTeamH.csproj
dotnet build src/MultiTeamH/MultiTeamH.csproj -c Release --no-restore
```

项目通过 `nuget.config`、`Directory.Packages.props` 获取 BepInEx、KKAPI、游戏及 Unity 编译引用。构建 MultiTeamH 不要求本机安装游戏，也不需要反编译资料。使用正常的 `obj/` 和 `bin/` 即可；旧的 `obj0105/` 等目录只是历次临时构建缓存。

输出：`bin/Release/MultiTeamH/KK_MultiTeamH.dll`，另有 `bin/Release/KK/` 汇总输出。运行时需要游戏、BepInEx 和 KKAPI；编译成功不能证明游戏行为正确。

整个解决方案共九个项目。仓库发布整理时已将 `ClothingCompression` 的 KKAPI/Sideloader 引用改为 NuGet，去掉本机游戏目录要求；可直接构建整个解决方案，详见 [工作区开发说明](Development.md)。

## 源码入口

| 文件 | 主要职责 |
| --- | --- |
| `src/MultiTeamH/MultiTeamHPlugin.cs` | 插件生命周期、Harmony hooks、快捷键和减号面板 |
| `src/MultiTeamH/FreeHModeController.cs` | 多P入口、队伍选择和角色卡持久化 |
| `src/MultiTeamH/TeamSession.cs` | 两队状态、后台原生流程、焦点切换、镜头/UI隔离和多人动作；内含 TeamState/TeamControllers 等辅助类型 |
| `src/MultiTeamH/RuntimeModCompat.cs` | CyuNoVR、HCtrl、Pregnancy/PregnancyPlus 等运行时兼容 |
| `src/MultiTeamH/ExpressionControlCompat.cs` | 本项目内对 ExpressionControl 的兼容补丁 |
| `src/MultiTeamH/BreastmilkCompat.cs` | Breastmilk 后台流程兼容 |
| `src/MultiTeamH/HSceneOptionsCompat.cs` | HSceneOptions 动作控制兼容 |
| `src/MultiTeamH/DiagnosticProbe.cs` | `[probe]` 诊断输出 |

两队复用同一个原生 HSceneProc、相机和 HSprite，分别保存 HFlag、动作控制器及运行状态。后台执行时要临时绑定正确队伍，随后恢复焦点上下文。修改共享对象时尤其要核对：voice 的角色槽位、HandCtrl、右下角气泡状态、相机快照、Lookat_dan 所属男性。兼容补丁放在本项目内。

## 最后已知状态

源码版本为 **0.11.62**。以下来自本次交接之前的用户反馈，不能当作全面回归测试结果：

- 0.11.60 后，亲吻结束再按 Tab 的镜头突变、男性器歪斜均反馈“似乎好了”。
- 0.11.61 后，3P 切出后的嘴部串扰反馈“似乎好了”。
- 0.11.61 的亲吻后平滑拉回仍有问题。0.11.62 移除了持续 16 帧回写镜头的 blend，只保留结束时单次恢复；已构建、部署，**尚无用户验证结果**。
- 不要把更早的大量修复尝试当作已验证结果。

优先复测：分别亲吻两人后反复 Tab；亲吻刚结束立即拖动镜头；A爱抚/B侍奉时把A改为侍奉；进入3P再切出并观察另一人的嘴部、动作和右下角UI。后台快感、高潮、动画必须继续运行，不能用视觉模拟代替原生流程。

本机游戏目录曾为 `E:\Game\Koikatsu BetterRepack RX18`，运行日志位于 `BepInEx/LogOutput.log`。这是本机环境信息，云端不能直接访问。请从日志中的插件加载版本和 `[probe]` 事件核对复现，报告时区分“构建通过”“已部署”“游戏实测通过”。用户明确要求：**未明确要求时不要计算 SHA256**。

## 给云端 Agent 的反编译参考

优先提供扩展包 `.local-reference/MultiTeamH-agent-reference-v2.zip`，其中已有完整游戏 C#、IL，以及需要核对本机实现的插件快照。只推送 GitHub 源码不会自动把这个包带到云端。原来的 `MultiTeamH-agent-reference.zip` 作为历史资料保留。

完整覆盖表、已安装版本与公开源码链接见 [依赖参考](MultiTeamH-Dependencies.md)。扩展包包含文件清单、大小、程序集引用和插件元数据，未做哈希计算。主要内容：

- `game/`：本机 Assembly-CSharp、Assembly-CSharp-firstpass 按类型拆分的 C#；包含原生 H、HandCtrl、相机、动作切换、FinalIK 等实现。
- `plugins/`：CyuNoVR、KK_HCtrl、Breastmilk，以及已安装版本的 ExpressionControl、BetterPenetration、KK_Chinese 的 C#；后面三者用于对照本机版本与已有源码的差异。
- `il/`、`assemblies/`：上述八份程序集的完整 IL 与原 DLL，可复核协程生成类型和反编译结果。
- `legacy-method-extracts/`：此前的 HActionBase、HHoushi、HLesbian、HSceneProc 及 changeanim、changeplay、createmotion 等重点路径片段。
- KKAPI、Pregnancy、PregnancyPlus、HSceneOptions、AnimationLoader 等已有公开源码的部分：依赖参考中提供对应版本链接，扩展包不重复反编译。

这些是本机版本的参考快照，可能与未来安装版本不同；重建后的 C# 和 IL 都不是厂商原始源码。线上 Agent 可按 C# 文件查类型，并用 IL 和本项目 Harmony 目标复核调用路径。反编译生成的工程不属于本仓库构建目标。
