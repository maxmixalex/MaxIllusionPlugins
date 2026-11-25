# MultiTeamH / ExpressionControl 运行时复查（2026-10-24）

## 范围与基线

本轮从 `ce07f846f7e6ac68abebf781c5fa51f954694699` 继续，针对“非焦点角色的 ExpressionControl 表情不生效”检查表情调度、原生脸部回调、插值回调、声音绑定、角色选择和场景退出。用户没有提供此次失效的具体日志，因此下面区分源码可确认的缺陷、可执行回归与待游戏验收。

已经阅读 [MultiTeamH 交接](MultiTeamH-Handoff.md) 和 [依赖说明](MultiTeamH-Dependencies.md)。本次恢复的公开仓库不包含被忽略的游戏 C#/IL 参考包；没有把编译引用当作完整游戏实现，也没有改动未经复核的相机或原生动作算法。

## 已修复的缺陷

### 1. 表情上下文只覆盖了 Proc，漏掉原生与插值回调

原来的 `ExpressionControlCompat` 在 `Proc` 前临时选择队伍和 `hFrag`，但以下入口在 `Proc` 之外也会执行：

- 原生 `FaceListCtrl.SetFace` 调用 `IsIgnoreSetFace`。
- 眼、嘴、眨眼、高光、脸红的原生写入调用 `ShouldBlock…`。
- 平滑眼睛/嘴部开合协程在后续帧调用上述保护方法。
- UI 或其他方法直接匹配、执行角色程序。

这些方法最终根据全局 `hFrag`、动作、绝顶次数及角色状态筛选程序。后台角色因此可能按焦点队伍条件决定是否阻止原生表情覆盖。

现在这些入口按实际 `ChaControl` / `FemaleData.female` 所属队伍进入嵌套作用域，绑定该队旗标、动作和计数，完成或异常时恢复调用者。相同角色的嵌套零件回调直接复用当前作用域。跨队回调发生在过滤后的 `Proc` 内时，临时提供完整角色列表，结束后恢复该 `Proc` 的队伍过滤；期间角色列表刷新仍保留两队已有的锁定与程序对象。

`IsIgnoreSetFace` 还检查控制器的角色归属：ExpressionControl 自己会在该方法中写脸，不能仅依靠另一个原生入口补丁稍后阻断。此次直接在实际写入之前拒绝外队控制器，不依赖两个插件的 Harmony 注册顺序。参考 [Harmony Prefix 说明](https://harmony.pardeike.net/v2/articles/patching-prefix.html) 与 [补丁优先级说明](https://harmony.pardeike.net/v2/articles/priorities.html)；测试没有把替身当作 Harmony detour 引擎运行结果。

### 2. 后台重放遗留了全局动作与计数

旧清理只恢复 `femaleList` 和 `hFrag`，`nowMotion`、`nowHAction`、`orgazumCount` 等仍是最后执行的后台队伍。现在后台重放结束保存自身进度并恢复调用者的表达状态；顶层处理仍保留当前焦点的最新状态，不会把 Tab 切换还原成旧焦点。

递归 `Proc` 不再被误认成第二个顶层入口。没有后台旗标或会话已结束时，不会以完整角色列表再次重放。会话选择会匹配 ExpressionControl 当前的 `hsceneProc`，避免旧场景尚未销毁时接管新场景，也避免离开 H 后继续套用残留会话。作用域在退出途中遇到会话清理，不会重新把已释放角色写回缓存。

### 3. 声音身份与声音请求槽位混淆

`TeamSession.BindVoiceSlots` 会随着当前绑定队伍交换 `nowVoices[0]` 和 `[1]`。原 `TryGetExpressionBindings` 却以固定队伍编号直接索引该数组，切换后可能让角色观察另一人的声音状态。现在优先使用已按队伍保存的 `voiceByTeam` 稳定对象；未初始化时仅按明确的当前主队映射现有槽位，不再用另一角色的对象填补缺失值。

独立 1F1M 队伍的 `HFlag.voice.playVoices` 使用自身的槽 0；共享双女性动作才按主角/伙伴使用 0/1。这个区别现在由生产 `ExpressionVoiceBinding` 集中处理并执行回归。

### 4. 男角色被当作未管理角色按前台条件执行

ExpressionControl 的角色发现同时包含男女角色。旧过滤器只识别两位女性，因此两个男性都会留在顶层“未管理角色”分支，而不随自己的队伍执行。现在识别每队的男女成员，每人只在所属队伍通过一次过滤；真正不属于会话的角色仍只处理一次。

### 5. 原生 SetFace 提前刷新状态，吞掉程序转换

旧 `Proc` 仅在 `updateState && oldFemaleState != newFemaleState` 时重新执行程序。原生 `IsIgnoreSetFace` 可能已经刷新了 `FemaleData.state`，使该条件变成 false；选择“仅状态转换”的间隔时，角色会一直等到下一次有机会触发的变化。同一粗粒度状态下更换动作也存在该问题。

将实际 `Proc` 循环提取到生产 `ExpressionProcessing.cs`，保留对外 `Proc` 入口。待处理的动作/状态转换现在负责一次程序评估，完成后清除标志；原有计时模式仍按间隔运行，同一转换与到期计时器不会重复选择两次。测试执行该生产循环，并保留实际的 `FemaleData.CheckExpression` 锁定代码，游戏角色/程序应用边界由记录器替代。

### 6. UI 选择角色和批量场景程序也使用所属队伍

保留原界面的控件、文案和排列，为已有操作提供三个小型角色入口：`RunCharacterAction`、`GetCharacterOrgasmCount`、`AdjustCharacterOrgasmCount`。没有 MultiTeamH 时维持单场景行为；可选兼容层按选中角色进入同一作用域。非焦点角色的计数箭头不再改动前台队伍，计数改变也会触发一次程序条件重新评估。

对 `FemaleData.UpdateState` 的兼容补丁还按该角色的旗标提供动作和口型模式，覆盖批量应用场景预设时传入宿主当前动作的路径。失效/离场角色的延迟 UI 回调不能修改当前场景。

### 7. 停用或替换程序后，口型忽略规则未即时刷新

UI 在切换程序或改变启用间隔时调用 `UpdateState(false, …)`，旧实现把口型忽略规则也放在 `updateState == true` 的分支里。结果是程序已经停用，之前的 `ignoreMouthOpen` / `ignoreMouthLoad` 仍可能阻止手动编辑，直到下一次动作变化。

现在生产 `FemaleDataRuntimeState` 每次刷新当前程序的口型规则，动画状态是否重算仍由原来的状态/亲吻变化决定。对应测试覆盖启用、替换、停用、恢复手动口型控制和允许动作范围。

## 验证

本子任务验证时：

- `MultiTeamH.Regression`：**60 项通过**，包含 21 项新角色上下文、声音绑定、回调/异常/场景清理回归。
- `PluginCore.Regression`：**78 项通过、0 失败**，包含 9 项新的实际表情处理、计数命令和口型规则回归，以及原有 XML 与本轮恢复布局后的编辑器测试。
- 生产 `src/MultiTeamH/MultiTeamH.csproj` 和 `src/ExpressionControl/ExpressionControl.csproj` 均使用原 net35 目标编译，**0 警告、0 错误**。
- 自动高潮的既有 7 项回归继续通过：有限满槽值、侍奉/插入支持的动画、Animator 就绪、焦点/锁定/待动作/转场保护及可用内外结束选择。没有用新的表情修复修改其游戏原生结束流程。
- 原有 HCtrl、Cyu、Breastmilk、HSceneOptions、原生 UI 异常恢复、协程清理及延迟场景退出回归继续执行。

另在隔离测试副本中只恢复旧的 `updateState && oldState != newState` 条件，实际运行得到 **75 通过、3 失败**：原生回调提前刷新、同状态换动作、手动计数改变三条新回归均失败；其余回归保持通过。使用修复后的生产循环时为上述 **78 通过、0 失败**。这是针对旧条件的反例验证，不是原始完整 DLL 的游戏运行对照。

主要实现及测试：

- [ExpressionControlCompat](../src/MultiTeamH/ExpressionControlCompat.cs)
- [ExpressionVoiceBinding](../src/MultiTeamH/ExpressionVoiceBinding.cs)
- [ExpressionProcessing](../src/ExpressionControl/ExpressionProcessing.cs)
- [ExpressionCharacterActions](../src/ExpressionControl/ExpressionCharacterActions.cs)
- [FemaleDataRuntimeState](../src/ExpressionControl/FemaleDataRuntimeState.cs)
- [队伍上下文回归](../tests/MultiTeamH.Regression/ExpressionTeamContextRegression.cs)
- [表情处理回归](../tests/PluginCore.Regression/ExpressionProcessingRegression.cs)

最终整仓数量与可下载安装包以本轮 CI 为准。上述回归直接链接生产代码，`TeamSession`、Unity、游戏、Harmony 注册与原生脸部应用边界仍使用明确替身；它们没有启动 Koikatsu，也没有执行真实 Harmony detour、语音、动画和插件组合。

## 游戏内验收重点

1. 两队选择不同模式和不同表情程序，把后台角色设为仅状态变化触发，观察动作转换、原生语音更新、眼口平滑变化时是否保持自身规则。
2. 交替 Tab 后，对非焦点角色使用原界面的表情、程序、定时器与计数箭头；确认前台角色的程序条件和手动锁定不被改变。
3. 在普通两队与共享双女性动作之间往返，交换主队，观察角色的声音相关表情、口型忽略与计数是否继续归属正确。
4. 在固定动作中启用、替换、停用程序，确认口型控制即时释放；一次应用场景预设时两队分别取自己的动作上下文。
5. 开关后台自动高潮，并重复进出场景、Tab、亲吻和已有可选插件组合。前一轮相机与多人动作的游戏实测边界继续保留，不能因这些表情回归通过而称插件已“完美”。
