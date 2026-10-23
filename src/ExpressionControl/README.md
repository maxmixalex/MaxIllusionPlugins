# ExpressionControl

Koikatsu 游戏与 Studio 中的表情控制插件。0.3.0 使用 MaximumUI 的 Metro 工作窗口，支持选择角色、编辑眉眼嘴等表情参数、保存表情预设、编辑表情程序，以及按场景保存多角色设置。

- 默认按 `L` 打开窗口；快捷键由 XML 的 `KeyConfig.showGUI` 保存。
- 表情、程序及场景预设由 `XmlMgr.cs` 管理。
- 原生表情/语音相关 hook 位于 `Hooks.cs` 和 `ExpressionControl.cs`。
- 运行依赖 BepInEx 5、KKAPI、`MaximumUI.dll` 与 `MaxIllusionPlugins.Common.dll`；主游戏和 Studio 均有对应进程入口。CI 的插件独立安装包已经带上这两个本仓共享 DLL。

```sh
dotnet build src/ExpressionControl/ExpressionControl.csproj -c Release
```

输出：`bin/Release/ExpressionControl/KK_ExpressionControl.dll`。本源码版本为 0.3.0；MultiTeamH 的历史运行测试曾使用 0.2.7.15，两者不能仅凭名称当作同一个实现。

MultiTeamH 兼容代码放在 [MultiTeamH/ExpressionControlCompat.cs](../MultiTeamH/ExpressionControlCompat.cs)。改动表情程序或持久化模型时，应保留原有 XML 数据的读取兼容性，并在游戏/Studio 中复测。

## 0.3.0 MaximumUI 工作窗口

- **表情**：角色搜索、眉/眼/嘴图案选择与前后切换、眼睛及嘴巴开合、瞳孔位置与大小、高光、眨眼及频率/速度、眼泪、脸红和各项独立锁定；右侧提供可搜索的虚拟预设列表、保存/覆盖/删除、当前角色程序与计时设置。
- **程序**：程序选择、空白/另存/覆盖/删除，程序单位新增/独立复制/移动/删除，全部动作类型、状态、敏感度与次数范围、控制部位、候选表情 GUID 选择及预设顺序调整。退出“调整表情顺序”会保存排序结果。
- **场景**：保存角色程序和时间间隔，按 F1/F2/M1/M2 等位置恢复，支持命名、覆盖和删除。
- **设置**：自定义窗口快捷键、查看 XML 路径、重新载入。深浅主题切换在窗口顶部；窗口支持拖动、缩放、折叠和关闭。

保留 `L` 默认入口与 XML 中的 `KeyConfig.showGUI`。输入字段和模态对话框期间暂停窗口快捷键，防止名称中的 `L` 关闭整个编辑器；角色表情处理继续运行。首次挂载仍排队到 `OnGUI`，场景切换可隐藏窗口，组件销毁时解除挂载。

界面使用 MaximumUI 的原生声明式控件，已移除旧 `GUIMgr`、`GUIUtl` 和独立 `ComboBox`。只构建当前页面，列表按可见行创建。保留的控件树按修改重建；角色页至多每 0.2 秒比较一次实时值，仅变化时请求更新，隐藏窗口不轮询界面快照。

XML 载入失败会显示文件路径和解析原因，停止保存，并保留上次成功读取的数据。修复文件后可在窗口重载。删除表情现在会清除所有程序单位、编辑副本和当前角色程序中的重复引用，避免后续保存重新带回已删除 GUID；角色当前表情数值不受删除影响。

## 回归与边界

`tests/PluginCore.Regression` 直接链接实际 `ExpressionEditorView`、`ExpressionProgramWidgets`、编辑/删除/重载业务以及窗口生命周期。测试覆盖四页在 900×540 与 1080×760 下的布局、2 万条表情的虚拟控件树、角色切换后的回调目标、全部条件控件、单位独立复制/移动、命名对话框和真实 XML 保存、坏文件只读及修复重载、所有删除引用、场景角色与间隔往返。

测试使用明确的 Unity/游戏边界替身，不执行角色网格、动画或真实 IMGUI 绘制。仍需在游戏和 Studio 中验证窗口显示、中文字体、键盘/鼠标输入、表情应用与 MultiTeamH 切队行为。CI SVG 预览来自同一生产 View 配合匿名合成数据，不读取用户预设。

## 0.2.7.20 运行修复

- `L` 在 `LateUpdate` 中排队切换可见性，窗口及命名 GUIStyle 在 `OnGUI` 中首次创建。避免场景切换后没有有效 IMGUI skin 时按键无法打开窗口，反复 Layout/Repaint 也不会重复切换。
- H 状态读取优先使用兼容层提供的 `hFrag`，使后台队伍的动作、高潮次数和表情程序跟随该队，而不是共享 `HSceneProc` 的焦点队伍。
- 支持动画描述暂缺、动作名暂缺，以及循环状态名不变时切换动作，避免空引用和角色绑定不刷新。
- 与 MultiTeamH 0.11.64 一起使用时，刷新角色列表会保留两队原有的表情锁定和程序，然后只处理当前执行队伍。

这些 H 状态和分队修复保留在 0.3.0 中。`PluginCore.Regression` 继续直接链接 `ExpressionWindowLifecycle.cs` 与 `ExpressionHSceneState.cs`，检查初始化时机、窗口切换、共享 GUI 状态恢复和分队状态读取；窗口生命周期回归已升级到 MaximumUI 挂载/隐藏/卸载。
