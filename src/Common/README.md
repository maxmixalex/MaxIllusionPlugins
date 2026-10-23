# Common

共享辅助工程，包含 `BuildNumber.cs` 中的构建编号、`Log.cs` 中的 BepInEx 日志包装与 `AtomicXmlFile.cs` 中的 XML 读写。供引用它的功能项目使用，不提供独立的游戏窗口或功能入口。

`AtomicXmlFile` 先在目标目录中完整写入临时文件，再用 `File.Replace` 替换现有文件；序列化/写入失败时保留原文档。它支持可选备份路径，读取时禁止 DTD。它不提供跨进程锁或断电耐久性承诺，替换失败会向调用方报告错误，不能降级为先删除旧文件。

```sh
dotnet build src/Common/Common.csproj -c Release
```

构建输出：`bin/Release/Common/MaxIllusionPlugins.Common.dll`。目标框架、输出目录和引用策略继承仓库的 `Directory.Build.props`。
