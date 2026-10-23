# MaximumUI.Tests

MaximumUI 的无界面断言测试，覆盖布局约束、输入与拖动、控件绘制命令、选择状态、导航、快捷键、表格及调试工具。

当前工程是 `.NET Framework 4.6.1` 类库，测试入口为 `Program.Main`，没有引入测试框架。`dotnet test` 不能代替执行这些断言。仓库脚本会先构建工程，再调用此入口并检查退出结果：

```sh
pwsh -NoProfile -File scripts/Test-MaximumUI.ps1
```

跨平台运行全部插件回归时使用 `python scripts/test.py` 或 `pwsh -NoProfile -File scripts/Test-All.ps1`。它们通过 .NET 9 的 `tests/MaximumUI.Runner` 在新进程中执行本程序集。构建时复制的 UnityEngine 编译引用仅用于解析纯托管类型，不提供 Unity 游戏运行环境。

已构建时可使用 `-NoBuild`，也可通过 `-Configuration Debug` 切换构建配置。测试成功时输出 `MaximumUI headless tests passed.`。

这些测试验证纯布局和状态逻辑，不验证 Unity 渲染及游戏行为。`MaximumUI.Tests.dll` 不应安装为游戏插件。
