# MaximumUI 独立程序集基准

此项目不加入解决方案，也不会复制到插件发布目录。它通过显式 `MaximumUiAssembly` 路径加载要测量的真实 DLL；基准源码对两次构建相同。`UseFixedApi=true` 额外启用修复版本的实际 `ViewBuildSchedule` 和 `LayoutCache`，未复制两者的实现。程序集命名为 `MaximumUI.Tests` 是为了使用库已有的测试友元权限。

需要 .NET SDK 9。先分别构建原始和修复版本的 `MaximumUI.dll`，保留在两个独立目录；构建当前 `src/MaximumUI.Tests/MaximumUI.Tests.csproj` 可同时准备真实 UnityEngine 编译引用。也可显式传入 `UnityEngineAssembly` 路径。

下面的 DLL 路径须替换为本机绝对路径；不同的 `BenchmarkVariant` 保留各自的输出和中间文件，避免将同名程序集混在一个进程内。

```sh
dotnet build src/MaximumUI.Tests/MaximumUI.Tests.csproj -c Release

dotnet build tests/MaximumUI.Benchmark/MaximumUI.Benchmark.csproj -c Release -p:MaximumUiAssembly=/absolute/path/to/baseline/MaximumUI.dll -p:BenchmarkVariant=baseline
dotnet bin/Release/MaximumUI.Benchmark/baseline/MaximumUI.Tests.dll

dotnet build tests/MaximumUI.Benchmark/MaximumUI.Benchmark.csproj -c Release -p:MaximumUiAssembly=/absolute/path/to/fixed/MaximumUI.dll -p:UseFixedApi=true -p:BenchmarkVariant=fixed
dotnet bin/Release/MaximumUI.Benchmark/fixed/MaximumUI.Tests.dll
```

场景：1000 个 Label、150 帧、每帧 2 个 GUI 事件；每种模式先预热 3 次（每次 25 帧），再测量 5 次并输出中位耗时。每次运行都断言控件数量、末行路径、树构建次数和布局次数。分配量来自 `GC.GetAllocatedBytesForCurrentThread()`，不含建立测量对象之前的分配。

| 模式 | 行为 |
|---|---|
| `mode=0` | 每次事件重新构建树并布局；原始 DLL 与修复 DLL 都可运行 |
| `mode=1` | 修复版本默认每帧构建一次，相邻事件复用布局 |
| `mode=2` | 修复版本按需构建；没有状态变化的整个测量期间只构建一次 |

2026-10-23，.NET 9.0.319，在独立工作树构建原始 `17f2d36` DLL 并使用相同源运行的记录：

| DLL / 模式 | 树构建 / 布局 | 中位耗时（ms） | 线程分配（字节） |
|---|---:|---:|---:|
| 原始 / 每事件 | 300 / 300 | 40.626 | 126,662,440 |
| 修复 / 每事件 | 300 / 300 | 44.431 | 126,662,440 |
| 修复 / 每帧 | 150 / 150 | 19.913 | 63,338,576 |
| 修复 / 按需空闲 | 1 / 1 | 0.133 | 422,432 |

收益来自减少重复构建与布局，表中没有证据表明单次布局吞吐变快。按需模式适用于通过 `SetState` / `MarkDirty` 显式失效的视图。基准不执行 Unity 原生绘制、输入分发或游戏逻辑，不能转换为游戏 FPS，也没有验证 GPU 开销。

`python scripts/test.py --benchmark` 是另一种用途：它执行全部回归测试，并在**同一修复后程序集**中比较三种调度策略。该命令不会自动构建或加载历史 DLL；它的输出不能称为直接版本前后对比。本项目则用于独立 DLL 对比，保留独立预热和中位数测量。
