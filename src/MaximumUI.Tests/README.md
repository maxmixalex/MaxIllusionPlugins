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

入口实际执行 **238 组**回归。前一版的 179 组覆盖控件/布局/调度、Metro/MDL2、弹层、缓存、导航、滚动坐标、数值输入、业务 View 与会话、内联对话框；本轮新增 59 组：滚动框与轨道 22、文字提交/焦点/滚轮 13、嵌套测量工作量 5、绘制阶段组合 2、多窗口前置 7、离屏文本提交生命周期 4、Juice 原布局与操作 6。`Program.Main` 会输出总组数，任一断言失败返回非零。

新增测试调用实际 MaximumUI 类，包含 10,000 选项缓存、100,000 行虚拟视口、移动/缩放布局、按键与焦点所有权、隐藏/卸载/模态/已消费 MouseUp、RequestFocus 上升沿、滚动条互相诱发、嵌套剪裁、延迟数值确认、CultureInfo 与极值等边界。预热分配测量仅针对特定纯托管热路径；没有把它当作原生 GUI 每帧分配结果。

`DialogLayoutTests` 注入字体度量与视口后，调用生产 `DialogService.Alert/Confirm` 和 `LayoutEngine`，测试按钮实际坐标与正文滚动范围。数值输入测试调用生产解析器/状态机；原生 GUI.TextField 的键盘事件与字段焦点仍需游戏内验收。

`PluginEditorViewTests` 直接编译 JuiceControl / AccessoryBatchManager 的生产 View，验证控件回调、模态、写入禁用、选中目标与超长虚拟列表。另两个编辑器的完整 View→生产业务测试分别在 AccessoryClothing.Regression 和 PluginCore.Regression 执行。`EmbeddedUiTests` 验证短长列表共用滚动、隐藏后旧回调取消、显示状态、查询快照独立，以及 80 个控件在预热后 1,000 次登记的分配量。

`InlineDialogLayoutTests` 在修复前以实际饰品窗口复现开启确认框把背景高度从 702 压缩到 482。修复后验证实际饰品/皮肤效果页面在默认和最小尺寸下的背景尺寸稳定，并覆盖嵌套容器、网格/表格/分栏、原始路径、选中 Tab、独立对话框尺寸与按钮输入、滚动范围和绘制指令。Juice 会话回归直接链接 `JuiceEditorSession`，验证换库/同名换角/禁用重开拒绝旧命令，以及普通显示刷新保留合法的最后一次数值编辑。

MDL2 回归验证真实几何图标、开关、禁用继承、可见焦点、放大字号、图表连线、颜色列对齐、数值标签测量、弹层虚拟化、边界翻转及输入阻止与外观状态分离。原生图标采样和弹层几何在预热后的循环中另有零分配断言。

浮层所属控件的性能测试使用渲染器同型的复用 `List<Widget>`；完整 10,000 次预热后连续测三个各 10,000 次批次，每批独立要求 0 B，同时验证输入仍有效、代次和复用容器保持稳定。不会以多批最小值或非零容忍量掩盖分配。冷进程测量的已知边界见 [MDL2 记录](../../docs/MDL2-2026-10-23.md)。

本轮设计和验证见 [MDL2 记录](../../docs/MDL2-2026-10-23.md)。此前接入证据、执行命令及未验证范围见 [接入记录](../../docs/Integration-2026-10-23.md)；先前修复的反例保留在 [后续修复记录](../../docs/Followup-2026-10-23.md)。界面绘制预览由 [独立工具](../../tests/MaximumUI.Preview/README.md) 从真实 Gallery 和插件控件树生成，不属于游戏截图。

0.5.0 的最终证据见 [UI 成熟度改进](../../docs/UI-Maturity-2026-10-24.md)。新增测量回归实际换入修复前 DLL 验证失败；保留文本回归通过真实 VirtualListCache 淘汰编辑行，再验证失焦提交先于保存，隐藏/卸载则丢弃旧草稿。前置回归调用实际 Runtime 挂载、Focus、排序和路由，覆盖模态与非窗口层边界。它们仍不执行 Unity 原生 GUI 事件循环。
