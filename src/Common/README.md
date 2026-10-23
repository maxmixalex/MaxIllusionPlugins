# Common

共享辅助工程，包含 `BuildNumber.cs` 中的构建编号和 `Log.cs` 中的 BepInEx 日志包装。供引用它的功能项目使用，不提供独立的游戏窗口或功能入口。

```sh
dotnet build src/Common/Common.csproj -c Release
```

构建输出：`bin/Release/Common/MaxIllusionPlugins.Common.dll`。目标框架、输出目录和引用策略继承仓库的 `Directory.Build.props`。
