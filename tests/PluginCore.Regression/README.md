# ExpressionControl / JuiceControl / Common 回归

```sh
dotnet run --project tests/PluginCore.Regression/PluginCore.Regression.csproj -c Release
```

此控制台宿主直接链接生产文件，不复制被测算法：`AtomicXmlFile`、`ExpressionModels`、`ExpressionRules`、`ExpressionProgramEditing`、`FemaleDataExpressionState`、`XmlMgr`、`ShortCutKeyMgr`、`Utl`、`PresetModels`、`PresetStore`、`SkinEffectsBridge`。失败返回非零退出码。

28 项回归覆盖原子替换及备份、注入写入/序列化失败、旧 XML 的标识和角色映射、空集合和缺失 ID、损坏文件保留、DTD 拒绝、全部表情状态筛选、空/过期随机候选、单元选择权重、删除程序后编辑器重选、嘴形和嘴开合独立锁定、取消快捷键、土耳其语区域设置、插值完成回调、场景间人物查找、可选插件缺失/不兼容的负缓存，以及反射 API 的重载解析。

删除程序测试调用实际的 `XML_DeleteProgram`，覆盖首项、中间项、末项及最后一个程序，随后验证编辑器单元访问、独立编辑副本、角色引用和实际保存的 XML。锁定测试调用整个生产 `FemaleData.CheckExpression`，验证嘴形锁不干涉开合、开合锁与整体锁恢复角色/混合器/Studio 状态，以及忽略口型和正在插值时的既有例外。

路径切换回归实际执行 `PresetStore`：成功载入 A → B 损坏 → 保留旧内存并禁止写入 → B 修复 → 重新载入并只保存到 B。不会把“打开 B、实际写 A”的状态视为成功。

`ExternalStubs.cs` 仅模拟 Unity 输入/时间/数学、角色容器、BepInEx 配置路径与插件目录等外部边界。XML 使用真实文件和 .NET XML 序列化器；反射重载测试用 Reflection.Emit 创建具有真实重载签名的测试插件。所有测试临时文件写入系统临时目录并清理。

`ExpressionHostStubs.cs` 提供 Unity 组件持有的状态字段，不复制删除或锁定逻辑；.NET 9 已移除运行时 `BinaryFormatter`，此宿主仅将 `DeepClone` 边界换成 XML 深拷贝。测试验证编辑副本相互独立，但不宣称覆盖生产 net35 的 `BinaryFormatter` 实现。

本宿主是 .NET 9 工具，不是游戏插件，不能证明 Unity Mono 的所有运行时差异、IMGUI 绘制或真实 SkinEffects 安装版本已经通过验证。原生产插件仍单独以 net35 编译。`IsPluginArtifact=false` 防止把它放入插件汇总目录。
