# ExpressionControl

Koikatsu 游戏与 Studio 中的表情控制插件。支持选择角色、编辑眉眼嘴等表情参数、保存表情预设、编辑表情程序，以及按场景保存多角色设置。现有 UI 使用 `GUIMgr`、`GUIUtl`、`ComboBox`，不依赖 MaximumUI。

- 默认按 `L` 打开窗口；快捷键由 XML 的 `KeyConfig.showGUI` 保存。
- 表情、程序及场景预设由 `XmlMgr.cs` 管理。
- 原生表情/语音相关 hook 位于 `Hooks.cs` 和 `ExpressionControl.cs`。
- 运行依赖 BepInEx 5、KKAPI；主游戏和 Studio 均有对应进程入口。

```sh
dotnet build src/ExpressionControl/ExpressionControl.csproj -c Release
```

输出：`bin/Release/ExpressionControl/KK_ExpressionControl.dll`。本源码版本为 0.2.7.18；MultiTeamH 的历史运行测试曾使用 0.2.7.15，两者不能仅凭名称当作同一个实现。

MultiTeamH 兼容代码放在 [MultiTeamH/ExpressionControlCompat.cs](../MultiTeamH/ExpressionControlCompat.cs)。改动表情程序或持久化模型时，应保留原有 XML 数据的读取兼容性，并在游戏/Studio 中复测。
