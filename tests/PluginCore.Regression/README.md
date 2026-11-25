# ExpressionControl / JuiceControl / Common 回归

```sh
dotnet run --project tests/PluginCore.Regression/PluginCore.Regression.csproj -c Release
```

此控制台宿主直接链接生产文件，不复制被测算法：`AtomicXmlFile`、`XmlFileDiagnostics`、`ExpressionModels`、`ExpressionRules`、`ExpressionProgramEditing`、`ExpressionEditorOperations`、`ExpressionPresetEditing`、`ExpressionPresetReload`、`ExpressionEditorView`、`ExpressionProgramWidgets`、`ExpressionWindowLifecycle`、`ExpressionHSceneState`、`FemaleDataExpressionState`、`XmlMgr`、`ShortCutKeyMgr`、`Utl`、`PresetModels`、`PresetStore`、`SkinEffectsBridge`。失败返回非零退出码。

回归覆盖原子替换及备份、注入写入/序列化失败、旧 XML 的标识和角色映射、空集合和缺失 ID、损坏文件保留、DTD 拒绝、全部表情状态筛选、空/过期随机候选、单元选择权重、删除程序后编辑器重选、嘴形和嘴开合独立锁定、取消快捷键、土耳其语区域设置、插值完成回调、场景间人物查找、可选插件缺失/不兼容的负缓存，以及反射 API 的重载解析。

XML 兼容回归另覆盖 Juice/Expression 的合法 `xsi:nil` 根、空根、UTF-8（有/无 BOM）及 UTF-16（大小端）文档、含 Unicode/URI 标点的路径、解析错误位置，以及空文件/仅 BOM/非法字符/错误根/非法数值/根后尾随损坏的文件保护。Expression 重载失败后也禁止保存，成功重载或移除坏文件后显式重载可恢复。上一轮新增反例在更早版本上实际得到两项 `InvalidDataException`、一项坏文件可被覆盖、以及根后注释掩盖非法尾部的失败，再由当前实现通过。随后提供的真实 Juice/Expression XML 均为非 nil 根，当前生产加载器能正常读取并完整往返保存；不能把 nil 根回归认作此次真实文件已复现的根因。

表情运行时回归直接调用从原组件提取的 `LateUpdate`、`OnGUI` 和 `UpdateHState`，模拟 Unity 仅允许在 OnGUI 创建原生 UI 资源的边界，检查首次 MaximumUI 挂载延迟、GUI 多事件不会重复切换、异常后的共享状态恢复、文本/模态快捷键保护、销毁解除挂载、后台队伍的 H 标志绑定、缺失动作数据及高潮计数转换。测试原生挂载边界是明确替身，不能代替游戏内实际绘制测试。

Expression 的实际 MaximumUI 控件树参与测试：四页在 900×540 和 1080×760 下的可见布局边界，2 万条预设仍保持虚拟列表，角色回调捕获及角色移除保护，全部 23 个程序条件/部位和范围字段，单位独立复制/移动/删除，保存程序的稳定选择身份，命名输入/验证/取消/真实保存，重载失败使所有页面停止写入、修复后恢复，以及场景角色与间隔保存应用。删除表情回归验证所有单位、重复 GUID、编辑副本和多个活动角色的引用都被清理，并实际重读保存后的 XML。

生产控件来自真实 `MaximumUI.dll`；宿主构建会先构建 MaximumUI.Tests，取其编译期 Unity 类型签名供 CLR 加载。`ExpressionEditorGameStubs.cs` 只提供角色应用、动画更新、随机计时与组件持有状态的边界记录器，编辑和保存流程保持生产实现。`ExpressionEditorPreviewFactory` 用匿名合成模型生成同一 View 的表情页与程序页，供 CI SVG 工具调用，不读取任何配置文件。

删除程序测试调用实际的 `XML_DeleteProgram`，覆盖首项、中间项、末项及最后一个程序，随后验证编辑器单元访问、独立编辑副本、角色引用和实际保存的 XML。锁定测试调用整个生产 `FemaleData.CheckExpression`，验证嘴形锁不干涉开合、开合锁与整体锁恢复角色/混合器/Studio 状态，以及忽略口型和正在插值时的既有例外。

2026-10-24 增加的运行时测试直接执行生产 `ExpressionProcessing`、`ExpressionCharacterActions` 和 `FemaleDataRuntimeState`：原生回调提前刷新状态后的程序触发、同状态换动作、计时与转换去重、选中角色计数/失效命令，以及停用/替换程序时即时更新口型规则。程序实际应用、游戏状态识别及眼部处理使用显式边界记录器，不把它们当作实机动画或 Harmony detour 的证明。

路径切换回归实际执行 `PresetStore`：成功载入 A → B 损坏 → 保留旧内存并禁止写入 → B 修复 → 重新载入并只保存到 B。不会把“打开 B、实际写 A”的状态视为成功。

`ExternalStubs.cs` 仅模拟 Unity 输入/时间/数学、角色容器、BepInEx 配置路径与插件目录等外部边界。XML 使用真实文件和 .NET XML 序列化器；反射重载测试用 Reflection.Emit 创建具有真实重载签名的测试插件。所有测试临时文件写入系统临时目录并清理。

`ExpressionHostStubs.cs` 提供 Unity 组件持有的状态字段，不复制删除或锁定逻辑；.NET 9 已移除运行时 `BinaryFormatter`，此宿主仅将 `DeepClone` 边界换成 XML 深拷贝。测试验证编辑副本相互独立，但不宣称覆盖生产 net35 的 `BinaryFormatter` 实现。

本宿主是 .NET 9 工具，不是游戏插件，不能证明 Unity Mono 的所有运行时差异、IMGUI 绘制或真实 SkinEffects 安装版本已经通过验证。原生产插件仍单独以 net35 编译。`IsPluginArtifact=false` 防止把它放入插件汇总目录。

## 验证本地预设

```sh
dotnet run --project tests/PluginCore.Regression/PluginCore.Regression.csproj -c Release -- --validate-presets /path/to/config
```

目录应包含 `MaxMixAlex.JuiceControl.presets.xml` 与 `MaxMixAlex.ExpressionControl.presets.xml`。此模式将文件复制到临时目录，分别通过真实 `PresetStore` 和 `XmlMgr` 加载、保存并重载，比较完整序列化模型，只输出记录数。原文件不会被打开写入，用户数据不会嵌入测试或 CI 产物。

2026-10-23 提供的实际文件验证通过：Juice 为 3 个角色预设、0 个分组；Expression 为 72 个表情、5 个程序、3 个场景预设。所有序列化字段在往返后保持一致。未在这些文件上复现 `InvalidDataException`；实际游戏中的错误仍需结合明确的文件路径、插件版本和完整异常定位。
