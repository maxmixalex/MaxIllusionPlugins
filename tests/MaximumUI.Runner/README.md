# MaximumUI 断言执行宿主

在干净的 .NET 9 进程中加载 `MaximumUI.Tests.dll`，调用原有 `MaximumUI.Tests.Program.Main` 并传播返回值/异常。它不是另一份测试实现，也不会将 `dotnet test` 的空发现结果误报为成功。

```sh
dotnet run --project tests/MaximumUI.Runner/MaximumUI.Runner.csproj -c Release -- bin/Release/MaximumUI.Tests/MaximumUI.Tests.dll
```

先构建解决方案，或直接运行根目录的 `scripts/test.py` / `scripts/Test-All.ps1`。测试程序集输出中会复制 NuGet 提供的 UnityEngine 编译引用，供纯托管枚举/结构签名解析；它不提供 Unity 原生渲染环境，不能用于游戏部署。
