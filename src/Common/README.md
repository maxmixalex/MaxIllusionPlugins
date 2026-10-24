# Common

共享辅助工程，包含 `BuildNumber.cs` 中的构建编号、`Log.cs` 中的 BepInEx 日志包装与 `AtomicXmlFile.cs` 中的 XML 读写。供引用它的功能项目使用，不提供独立的游戏窗口或功能入口。

`AtomicXmlFile` 先在目标目录中完整写入临时文件，再用 `File.Replace` 替换现有文件；序列化/写入失败时保留原文档。它支持可选备份路径，读取时禁止 DTD，并验证到文件末尾，防止序列化器在根后的注释处返回而漏掉后面的损坏内容或第二个根。它不提供跨进程锁或断电耐久性承诺，替换失败会向调用方报告错误，不能降级为先删除旧文件。

`XmlFileDiagnostics.Describe` 提取最内层异常的类型与消息，帮助预设界面显示 XML 解析位置或实际 I/O 原因；调用方仍需记录完整异常和文件路径。`AtomicXmlFile.Read` 对合法的 `xsi:nil` 根按序列化器约定返回 `null`，是否把它视为空文档由各模型的载入器处理。

```sh
dotnet build src/Common/Common.csproj -c Release
```

构建输出：`bin/Release/Common/MaxIllusionPlugins.Common.dll`。目标框架、输出目录和引用策略继承仓库的 `Directory.Build.props`。
