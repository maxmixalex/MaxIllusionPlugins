# MultiTeamH：线上 Agent 依赖参考

核对日期：2026-10-23。范围为 MultiTeamH 的原生游戏调用、可选插件兼容和先前本地反编译所依赖的实现。

编译引用与运行实现用途不同：NuGet 的 IllusionLibs/KKAPI 引用让项目能够编译；分析原生流程、Harmony 目标、协程及插件私有字段还需要实际实现。下面将需要本地快照的实现与已有公开参考的依赖逐项对应。

## 本地参考包

文件：`.local-reference/MultiTeamH-agent-reference-v2.zip`。这是上一版参考包的扩展替代包，单独提供给线上 Agent；Git 默认不包含它。

| 本机组件 | 已核对的版本 | 包内内容与原因 |
| --- | --- | --- |
| 游戏 `Assembly-CSharp.dll` | 程序集版本 0.0.0.0；以清单的文件大小和时间辨识 | 完整按类型拆分的 C#、完整 IL、原 DLL；原生 H、角色、相机、动作和 UI 实现 |
| 游戏 `Assembly-CSharp-firstpass.dll` | 程序集版本 0.0.0.0 | 完整 C#、IL、原 DLL；含 RootMotion/FinalIK、UniRx 等游戏实际携带的实现，部分为混合的第三方库 |
| CyuNoVR | 插件 0.3.3；程序集 0.3.3.0 | 完整 C#、IL、DLL；未确认对应本机版本的公开原始实现。网上找到的其他 fork 不作为同版本替代 |
| KK_HCtrl | **插件 0.1.1**；程序集 1.0.0.0 | 完整 C#、IL、DLL；未找到对应版本的公开参考，注意插件版本与程序集版本不同 |
| Breastmilk | 插件 1.4.6；程序集 1.4.6.0；同 DLL 还含 fixmilk 1.5 | 完整 C#、IL、DLL；后台协程和静态计数器兼容依赖实际实现 |
| 已安装 ExpressionControl | 0.2.7.15 | 完整 C#、IL、DLL；本仓库源码目前为 0.2.7.18，保留旧运行版本用于对照 |
| 已安装 BetterPenetration | 4.5.5.2 | 完整 C#、IL、DLL；[公开 4.5.5 标签](https://github.com/Animal42069/BetterPenetration/tree/4.5.5)中 KK 插件 VERSION 为 4.5.5.0，不能假设与本机逐字相同 |
| 已安装 KK_Chinese | 插件 2.1.1；程序集 1.0.0.0 | 完整 C#、IL、DLL；本机使用 `KK_Chinese.Core.Patch.Text_Display`，与[公开仓库](https://github.com/sirinc0/KK_Chinese)当前 Patchers/TextPatcher 结构不同 |

包内布局：

```text
README.md                       阅读入口、工具版本与检验信息
DEPENDENCIES.md                  本文副本
HANDOFF.md                       开发交接说明副本
runtime-assemblies.json          相关本机程序集版本、插件 GUID/版本、引用关系
MANIFEST.csv                    包内文件路径和大小（无 SHA256）
game/Assembly-CSharp/            游戏 C#；例如 HSceneProc.cs、HandCtrl.cs、HSprite.cs
game/Assembly-CSharp-firstpass/  另一游戏程序集的 C#
plugins/                        六个本机插件实现快照
il/                             上述八份程序集的完整 IL，保留编译后的协程类型
assemblies/                     上述八个原 DLL，便于再次按类型/方法导出
legacy-method-extracts/          先前提取的原生重点方法片段及原名清单
```

这是从本机安装程序集反编译得到的阅读快照，不是厂商原始源码。C# 由 ILSpyCmd 10.0.1.8346 导出，firstpass 的 C# 由 9.1.0.7988 导出；对应 IL 均由 10.0.1.8346 导出。若需要确认 Harmony 所用的编译器生成类型或方法，查 IL 和原 DLL，不能只依赖重建后的 C# 协程语法。

生成的 `.csproj` 仅是反编译工具产物，不是本仓库的构建项目；其中可能保留本机引用路径。继续开发应构建 `src/MultiTeamH/MultiTeamH.csproj`。

## 已有公开参考，扩展包不重复反编译

以下链接已核对；公开标签是查阅入口，不代表未进行二进制比较就能证明与本机完全相同。

| 依赖 | 本机/编译版本 | 在线源码或文档 |
| --- | --- | --- |
| KKAPI | 本机 1.42.1.1；本项目 NuGet 编译引用 1.47.0 | [源码 v1.42.1](https://github.com/IllusionMods/IllusionModdingAPI/tree/v1.42.1)、[该版本文档](https://github.com/IllusionMods/IllusionModdingAPI/tree/v1.42.1/doc)、[源码 v1.47](https://github.com/IllusionMods/IllusionModdingAPI/tree/v1.47)；注意运行与编译版本差异 |
| KK_Pregnancy | 2.8.0 | [p2.8 源码](https://github.com/ManlyMarco/KoikatuGameplayMods/tree/p2.8/src)，重点看 Core_Pregnancy_KK、KK_Pregnancy |
| KK_PregnancyPlus | 6.3.3 | [6.3.3 源码](https://github.com/thojmr/PregnancyPlus/tree/6.3.3) |
| AnimationLoader.Koikatu | 1.1.3.0 | [v1.1.3.0 源码](https://github.com/IllusionMods/AnimationLoader/tree/v1.1.3.0)、[动作包格式与说明](https://github.com/IllusionMods/AnimationLoader/blob/v1.1.3.0/README.md)，重点看 AnimationLoader.SwapAnim |
| KK_HSceneOptions | 3.2.1 | [3.2.1 源码](https://github.com/MayouKurayami/KK_HSceneOptions/tree/3.2.1)，重点看 AnimationToggle 的 forceOLoop、proc、动画状态 |
| Sideloader / BepisPlugins | 本机插件 20.0；程序集 1.0.0.0；ClothingCompression 编译包 20.0.0 | [r20.0 源码](https://github.com/IllusionMods/BepisPlugins/tree/r20.0)，ClothingCompression 现通过 NuGet 还原编译引用 |
| XUnity.AutoTranslator | 5.2.0 | [源码及集成文档](https://github.com/bbepis/XUnity.AutoTranslator)，查 DefaultTranslator / TryTranslate |
| BepInEx 5 | 编译包 5.4.11；实际运行版本应查运行环境 | [API 文档](https://docs.bepinex.dev/v5.4.21/api/)、[源码](https://github.com/BepInEx/BepInEx) |
| Harmony | 由 BepInEx 包及运行环境提供 | [官方文档](https://harmony.pardeike.net/)，具体补丁方法签名以本项目引用为准 |
| UnityEngine / UnityEngine.UI | 游戏 Unity 5.6.2.4 编译引用 | [Unity 5.6 API](https://docs.unity3d.com/560/Documentation/ScriptReference/)；引擎代码无需在本包重复导出 |
| DOTween | 游戏携带版本，见原生程序集引用表 | [官方 API 文档](https://dotween.demigiant.com/documentation.php) |

其余 .NET、Unity/TMP、Sirenix 编译引用由项目既有 NuGet 配置还原。完整游戏程序集对这些库的 AssemblyRef 已保存在 `runtime-assemblies.json`，不意味着要把整个游戏目录复制给 Agent。

## 覆盖范围与使用顺序

1. 先读仓库 `docs/MultiTeamH-Handoff.md`，定位当前插件中的调用与 hook；再查包中对应 C# 类型。编译生成名称、协程和可疑反编译结果用 IL 复核。
2. 遇到上表公开依赖，按指定版本阅读对应仓库；若后续发现本机有修改，再补该具体版本快照。不要用最新主分支默认替代历史安装版本。
3. 本机没有找到独立名为 kPlug/hPlug 的程序集；已覆盖本机游戏程序集，并提供实际存在的 AnimationLoader 版本入口。不能因此宣称已包含任意其他 kPlug/hPlug 安装的实现。
4. 此包覆盖上述代码调查所需的本地实现。动作 zipmod/assetbundle、角色卡、存档、用户配置和日志不在包内；它不是可运行游戏环境。涉及特定动作资源数据的复现仍可能需要另行提供对应资源或列表。

原 DLL 与反编译资料仍由 `.gitignore` 排除，作为单独参考包提供。公开依赖的许可和本项目许可分别适用；该包不改变资料原有来源。
