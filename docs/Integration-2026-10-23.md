# MaximumUI 四插件接入、真实配置验证与 CI 交付

本次继续上一轮审计，落实 GitHub Actions 可下载构建、ClothingCompression XML 改名，以及四个功能编辑器接入 MaximumUI。先前的全仓修复及 MultiTeamH 功能保留在同一开发分支，历史记录见 [首轮审计](Audit-2026-10-23.md) 和 [后续修复](Followup-2026-10-23.md)。

## 获取构建

[Build, test and package](https://github.com/maxmixalex/MaxIllusionPlugins/actions/workflows/ci.yml) 对每次 push、PR 更新执行编译、回归、预览导出及打包。开发分支为 `fix/plugin-audit-maximumui`，对应 [PR #1](https://github.com/maxmixalex/MaxIllusionPlugins/pull/1)。选择该分支最近一次绿色运行，在 Artifacts 下载 `plugin-packages-…`；每次运行的摘要也提供产物链接。

其中有五个功能插件的单独安装 ZIP、`MaxIllusionPlugins-all-plugins.zip` 合集，以及独立 Gallery ZIP。四个接入插件已带上实际引用的 `MaximumUI.dll`；需要 Common 的插件同时带上 `MaxIllusionPlugins.Common.dll`。合集不含 Gallery。安装包不包含配置、角色卡、游戏或第三方 DLL。

`maximumui-previews-…` 提供九张真实控件树 SVG 和预览清单；`build-test-logs-…` 提供本次编译、断言、基准及打包日志。产物保留 30 天，下载通常需要登录 GitHub。安装及重复 DLL 清理步骤见 [CI 说明](CI.md)。

## 版本与接入范围

| 项目 | 版本 | 本次实现 |
| --- | --- | --- |
| MaximumUI | 0.3.0 | 挂载显示/隐藏、过期回调隔离、共享滚动、查询与导航记录复用，以及实际插件使用中的布局修复 |
| AccessoryBatchManager | 0.7.0 | 批量、复制/移动、设置三页；双列虚拟列表、Shift 选择、同步滚动、操作确认 |
| ClothingCompression | 0.4.0 | 高级形变编辑器；全部参数、数值输入、绑定搜索、导入、保存/重载；保留 Maker 原生页 |
| ExpressionControl | 0.3.0 | 表情、程序、场景、设置四页；角色与预设搜索、程序单位与全部条件、命名和持久化 |
| JuiceControl | 0.2.0 | 效果与预设两页；角色选择、效果锁定、SkinEffects、角色/分组预设及 XML 错误状态 |
| MaximumUI Gallery | 0.3.0 | 独立开发示例；默认 F10、启动时隐藏，可配置快捷键 |
| MultiTeamH | 0.11.64 | 保留上一轮分队兼容、后台自动结束开关及其回归，继续使用现有游戏面板 |

四个编辑器使用实际 MaximumUI 控件，旧独立绘制代码已移除。字体层级、深色分层表面、强调色、语义按钮、状态、输入、模态、窗口缩放和滚动由公共库提供。主题沿用上一轮依据 Metro for Steam 公开预览建立的 Metro Dark / Light；没有引入第三方皮肤代码或图片。

### 交互与生命周期

各插件保留原有快捷键。文本编辑或模态窗口活动时暂停开关窗口的快捷键，防止名称输入触发 L、0 或其它入口。首次需要原生 GUI 资源的挂载留在 OnGUI；隐藏、离开宿主场景和销毁分别处理显示状态与卸载。

`MaximumUIRuntime.SetVisible` 隐藏时释放输入和浮层、无回调地关闭服务对话框，并递增回调代次。随后重新打开也不会执行隐藏前排队的旧确认。有效控件回调完成后自动使所属 View 失效；无变化的显示状态调用直接返回。`IsMounted`、`IsVisible` 和 `Focus` 提供适配器所需的查询与焦点操作。

多个列表可共享 `ScrollPosition`。每个视口独立夹取请求位置，较短列表到底不会把较长列表拉回；只有实际、未被模态阻挡的移动才回写共享状态。饰品复制页采用此机制。

最小窗口尺寸使用实际布局验证。饰品窗口的最小高度调整为 600、JuiceControl 为 560，避免固定工具栏及错误提示把列表挤成零高度；默认窗口大小保持不变。

内联对话框已从普通布局流中分离。新增回归先在旧实现复现饰品窗口开启确认框后背景高度由 702 缩到 482，再验证修复后各实际页面的背景布局稳定。对话框仍保留声明路径、自身尺寸与按钮输入；网格、表格、分栏、选中 Tab 和滚动范围都有对应断言。

## 实际性能工作

### 保留控件树和可见列表

四个编辑器均按失效构建，只有当前页面生成控件。角色或外部状态按需要以 0.2–0.25 秒间隔检查；隐藏窗口停止界面快照轮询。长预设与饰品列表使用真实 VirtualList，只创建可见范围及 overscan 内的行。

MaximumUI 查询/导航登记采用有界记录池，并复用 Widget 的稳定 Key、Path 与类型标识。公开查询仍返回副本。80 个保留控件在预热后进行 1,000 次登记的回归验证零托管分配；这不包括整个 OnGUI、Unity 绘制或公开 Snapshot 的必要复制。

### ClothingCompression 参数编辑

原高级窗口数值编辑会间接触发物品目录扫描和导入列表克隆；Maker 控件刷新也存在相同链路。新增的 `CompressionEditorData` 按控制器、搭配、装备修订、来源搭配、角色身份及库修订维护快照，普通数值修改复用已有目录。

实际生产 View→控制器回归在 20 件装备下跨模拟帧执行 200 次滑条回调：额外 UI 目录查找、目录渲染器扫描和导入克隆均为 0，控制器仍调度 200 次形变更新。换装、来源变化、角色/搭配切换、保存和成功重载会刷新必要数据；坏 XML 保留最后有效快照。这个结果衡量托管工作量，不能换算为游戏 FPS。

### ExpressionControl 过滤缓存

普通参数或锁定修改不再使整个表情库的过滤缓存失效。列表来源或查询改变时才重筛；排序、改名和重载有明确失效路径。回归使用 20,000 条预设验证虚拟控件树及普通编辑时的分配上界，并确认真正修改列表后显示更新。

## 上传配置的核验

三份实际 XML 均通过生产加载器。诊断工具先复制到临时目录，再加载、保存和重新加载，比较全部可序列化模型字段；原文件不写入。

| 文件 | 实际内容数量 | 结果 |
| --- | --- | --- |
| JuiceControl 预设 XML | 3 个角色预设、0 个分组 | 全字段往返一致 |
| ExpressionControl 预设 XML | 72 个表情、5 个程序、3 个场景预设 | 全字段往返一致 |
| ClothingCompression XML | 35 个配置档、38 个形变 | 全字段往返一致 |

这批文件均不是 nil 根节点，**没有复现报告中的 InvalidDataException**。不能把上一轮的 nil 根兼容修复当作这批真实文件已证实的故障原因。JuiceControl 上传 CFG 的头部记录版本为 0.1.3，但配置头部本身不能证明当前加载的 DLL 版本。若新构建仍报错，应以 BepInEx 实际加载版本、报错文件路径及完整异常为依据继续定位。

坏文件读写保护继续有效：加载失败保留上次成功数据、禁止覆盖，界面显示路径和原因；文件修复后显式重载恢复写入。ExpressionControl 现在也提供此恢复入口，初次读失败不会让整个编辑器永久失效。

### ClothingCompression 文件名

生产默认路径为 `BepInEx/config/MaxMixAlex.ClothingCompression.xml`。不查找旧名字、不自动迁移。使用者自行将 `ClothingCompression.xml` 重命名为新名字即可，XML 内容格式不变。

本地复核命令如下；第二条接受任意明确文件路径只是诊断输入，不构成生产旧名兼容：

```sh
dotnet run --project tests/PluginCore.Regression -c Release -- --validate-presets /path/to/config-directory
dotnet run --project tests/AccessoryClothing.Regression -c Release -- --validate-presets /path/to/ClothingCompression.xml
```

## 其它修复

ExpressionControl 删除表情时，原实现只处理第一个匹配程序单位和单条重复 GUID。现在清理所有程序单位、编辑副本及活动角色程序中的引用，并避免旧表情身份在后续保存时重新进入列表。回归实际保存并重读 XML。

ClothingCompression 补齐 Studio 进程入口，使已有的 Studio 选中角色逻辑可执行。数值回调检查控制器、角色、搭配和选中形变，旧控件不会写入后来切换的目标。

饰品与皮肤效果编辑器增加实际目标会话保护。换来源、换角色或换预设库后，旧控件的队列命令不再落到新对象；相同名称、相同项目数量也不能代替对象身份检查。普通显示数值与状态文字刷新不换代，避免丢弃仍合法的点击和最后一次滑条值。组件禁用、退出 Maker 或关闭命名流程会清理挂载及持有的业务目标，重新打开不重放旧操作。

## 可再生视觉证据

九张 SVG 来自生产 View、LayoutEngine、WidgetPainter 与主题令牌。插件预览使用匿名合成数据，导出器不读取上传文件。VirtualList 调用真实行工厂，滚动页面按实际视口裁剪；所有图片已栅格化检查中文、层级、裁剪、重叠和首屏按钮。

| 编辑器 | 预览 |
| --- | --- |
| AccessoryBatchManager | [批量](accessory-batch-manager-batch-preview.svg) · [复制/移动](accessory-batch-manager-copy-preview.svg) |
| ClothingCompression | [形变编辑](clothingcompression-editor-preview.svg) |
| ExpressionControl | [表情](expressioncontrol-expressions-preview.svg) · [程序](expressioncontrol-program-preview.svg) |
| JuiceControl | [效果](juicecontrol-effects-preview.svg) · [预设](juicecontrol-presets-preview.svg) |
| Gallery | [Metro Dark](maximumui-metro-dark-preview.svg) · [Metro Light](maximumui-metro-light-preview.svg) |

这些是托管绘制预览，**不是游戏截图**。SVG 使用明确的估算字体度量，不执行原生窗口装饰、滚动条、鼠标和键盘；技术细节及复现方式见 [预览工具](../tests/MaximumUI.Preview/README.md)。

## 最终本地验证

Linux / .NET SDK 9.0.319，整仓 Release Rebuild 完成：**0 错误、4 警告**。其中两条为既有 `Application.loadedLevelName`，一条为 net35 共享 XML 源码在 net9 宿主中的 `ProhibitDtd` 过时提示，一条为测试宿主引用旧框架 UI 程序集的 NU1702。没有以屏蔽这些提示代替执行测试。

| 实际执行入口 | 通过组数 | 本次新增覆盖 |
| --- | ---: | --- |
| MaximumUI.Runner → MaximumUI.Tests | 139 | 实际编辑器控件、目标会话、共享滚动、隐藏/回调代次、查询池、内联对话框布局 |
| MultiTeamH.Regression | 39 | 保留上一轮分队、兼容、协程与后台流程回归 |
| AccessoryClothing.Regression | 56 | Clothing 实际 View/适配器/缓存，ABM 实际命令会话与来源卡边界 |
| PluginCore.Regression | 69 | Expression 四页控件、编辑/删除/重载、过滤缓存与真实 XML |
| **断言合计** | **303** | 四个真实执行入口，任一失败均使脚本失败 |
| Python 打包规则 | 5 | 依赖闭包、独立 Gallery、合集、缺失依赖及未知引用拒绝 |

三份上传 XML 的最终构建往返验证再次通过；七个实际安装 ZIP 的程序集集合符合白名单。它们不包含游戏/第三方 DLL、测试 DLL、配置、IL 或反编译资料。

本次同一修复后引擎的调度策略微基准，场景为 1,000 个 Label、150 帧、每帧两次 GUI 事件：

| 策略 | 建树次数 | 布局次数 | 线程托管分配量 |
| --- | ---: | ---: | ---: |
| 逐 GUI 事件 | 300 | 300 | 143,476,840 B |
| 默认逐帧 | 150 | 150 | 71,745,776 B |
| 按需空闲 | 1 | 1 | 478,480 B |

此微基准只执行建树/布局，不是历史 DLL 与新 DLL 的端到端对比，也不含 Unity 渲染、游戏负载或用户交互。计时不作为性能承诺；CI 日志会记录其自身运行的计时和分配结果。

```sh
dotnet restore MaxIllusionPlugins.sln -m:1 -nodeReuse:false
dotnet build MaxIllusionPlugins.sln -c Release --no-restore -t:Rebuild -m:1 -nodeReuse:false
python3 scripts/test.py --no-build --benchmark
python3 -m unittest discover -s scripts/tests -p 'test_package*.py' -v
python3 scripts/package.py --configuration Release --output artifacts/packages
```

GitHub Actions 对提交再次执行这些公开、可重复的验证，再生成九张预览并上传安装包和日志。上传的私人配置只在本地诊断临时副本中验证，不放进仓库或线上 CI。

## 验证边界

本环境没有可运行的 Koikatsu/Unity。CI 验证 net35 编译、纯托管断言、生产文件的实际 XML 往返及发布包规则；不能替代 Unity 原生字体/控件 ID、窗口输入、Studio、真实角色效果、网格形变、多队切换、第三方模组组合和长期帧耗时的游戏内验收。PR 继续保持草稿，供下载、审阅与运行反馈。
