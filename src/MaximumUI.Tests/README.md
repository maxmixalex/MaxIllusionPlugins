# MaximumUI.Tests

MaximumUI 的无界面断言测试，覆盖布局约束、输入与拖动、控件绘制命令、选择状态、导航、快捷键、表格及调试工具。

当前工程是 `.NET Framework 4.6.1` 类库，测试入口为 `Program.Main`，没有引入测试框架。`dotnet test` 不能代替执行这些断言。仓库脚本会先构建工程，再调用此入口并检查退出结果：

```sh
pwsh -NoProfile -File scripts/Test-MaximumUI.ps1
```

跨平台运行全部插件回归时使用 `python scripts/test.py` 或 `pwsh -NoProfile -File scripts/Test-All.ps1`。它们通过 .NET 9 的 `tests/MaximumUI.Runner` 在新进程中执行本程序集。构建时复制的 UnityEngine 编译引用仅用于解析纯托管类型，不提供 Unity 游戏运行环境。

已构建时可使用 `-NoBuild`，也可通过 `-Configuration Debug` 切换构建配置。测试成功时输出 `MaximumUI headless tests passed.`。

这些测试验证纯布局和状态逻辑，不验证 Unity 渲染及游戏行为。`MaximumUI.Tests.dll` 不应安装为游戏插件。

## 当前覆盖

入口实际执行 **139 组**回归：原有控件/布局/调度 41、Metro 主题与编辑器 6、交互/缓存/生命周期 12、多挂载输入与导航 15、缓存异常恢复 5、滚动坐标/剪裁 9、真实服务对话框布局 4、真实数值输入状态 12、接入插件实际 View/会话 19、共享滚动/隐藏生命周期/查询池 8、内联 Dialog 布局 8。`Program.Main` 会输出总组数，任一断言失败返回非零。

新增测试调用实际 MaximumUI 类，包含 10,000 选项缓存、100,000 行虚拟视口、移动/缩放布局、按键与焦点所有权、隐藏/卸载/模态/已消费 MouseUp、RequestFocus 上升沿、滚动条互相诱发、嵌套剪裁、延迟数值确认、CultureInfo 与极值等边界。预热分配测量仅针对特定纯托管热路径；没有把它当作原生 GUI 每帧分配结果。

`DialogLayoutTests` 注入字体度量与视口后，调用生产 `DialogService.Alert/Confirm` 和 `LayoutEngine`，测试按钮实际坐标与正文滚动范围。数值输入测试调用生产解析器/状态机；原生 GUI.TextField 的键盘事件与字段焦点仍需游戏内验收。

`PluginEditorViewTests` 直接编译 JuiceControl / AccessoryBatchManager 的生产 View，验证控件回调、模态、写入禁用、选中目标与超长虚拟列表。另两个编辑器的完整 View→生产业务测试分别在 AccessoryClothing.Regression 和 PluginCore.Regression 执行。`EmbeddedUiTests` 验证短长列表共用滚动、隐藏后旧回调取消、显示状态、查询快照独立，以及 80 个控件在预热后 1,000 次登记的分配量。

`InlineDialogLayoutTests` 在修复前以实际饰品窗口复现开启确认框把背景高度从 702 压缩到 482。修复后验证实际饰品/皮肤效果页面在默认和最小尺寸下的背景尺寸稳定，并覆盖嵌套容器、网格/表格/分栏、原始路径、选中 Tab、独立对话框尺寸与按钮输入、滚动范围和绘制指令。Juice 会话回归直接链接 `JuiceEditorSession`，验证换库/同名换角/禁用重开拒绝旧命令，以及普通显示刷新保留合法的最后一次数值编辑。

完整证据、执行命令及未验证范围见 [接入记录](../../docs/Integration-2026-10-23.md)；先前修复的反例保留在 [后续修复记录](../../docs/Followup-2026-10-23.md)。界面绘制预览由 [独立工具](../../tests/MaximumUI.Preview/README.md) 从真实 Gallery 和插件控件树生成，不属于游戏截图。
