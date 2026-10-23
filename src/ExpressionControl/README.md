# ExpressionControl

Koikatsu 游戏与 Studio 中的表情控制插件。支持选择角色、编辑眉眼嘴等表情参数、保存表情预设、编辑表情程序，以及按场景保存多角色设置。现有 UI 使用 `GUIMgr`、`GUIUtl`、`ComboBox`，不依赖 MaximumUI。

- 默认按 `L` 打开窗口；快捷键由 XML 的 `KeyConfig.showGUI` 保存。
- 表情、程序及场景预设由 `XmlMgr.cs` 管理。
- 原生表情/语音相关 hook 位于 `Hooks.cs` 和 `ExpressionControl.cs`。
- 运行依赖 BepInEx 5、KKAPI；主游戏和 Studio 均有对应进程入口。

```sh
dotnet build src/ExpressionControl/ExpressionControl.csproj -c Release
```

输出：`bin/Release/ExpressionControl/KK_ExpressionControl.dll`。本源码版本为 0.2.7.20；MultiTeamH 的历史运行测试曾使用 0.2.7.15，两者不能仅凭名称当作同一个实现。

MultiTeamH 兼容代码放在 [MultiTeamH/ExpressionControlCompat.cs](../MultiTeamH/ExpressionControlCompat.cs)。改动表情程序或持久化模型时，应保留原有 XML 数据的读取兼容性，并在游戏/Studio 中复测。

## 0.2.7.20 运行修复

- `L` 在 `LateUpdate` 中排队切换可见性，窗口及命名 GUIStyle 在 `OnGUI` 中首次创建。避免场景切换后没有有效 IMGUI skin 时按键无法打开窗口，反复 Layout/Repaint 也不会重复切换。
- H 状态读取优先使用兼容层提供的 `hFrag`，使后台队伍的动作、高潮次数和表情程序跟随该队，而不是共享 `HSceneProc` 的焦点队伍。
- 支持动画描述暂缺、动作名暂缺，以及循环状态名不变时切换动作，避免空引用和角色绑定不刷新。
- 与 MultiTeamH 0.11.64 一起使用时，刷新角色列表会保留两队原有的表情锁定和程序，然后只处理当前执行队伍。

`PluginCore.Regression` 直接链接 `ExpressionWindowLifecycle.cs` 与 `ExpressionHSceneState.cs`，通过外部 GUI/游戏替身验证初始化时机、窗口切换、共享 GUI 状态恢复和分队状态读取。它不执行真实 Unity 绘制；仍需在游戏内从普通 H 进入多P、切队、按 `L`，以及关闭再打开窗口验证。
