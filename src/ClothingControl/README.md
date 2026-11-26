# ClothingControl

**0.5.0**：新增 **Free H** 使用入口（默认 F7）。角色选择读取游戏当前 `HSceneProc` 的 `lstFemale` 及单独的 `male`，包括原生 3P 当前角色，不扫描场景里不相关的 NPC。当前角色的九类衣物原生状态、饰品可见性和 XML 预设可以在播放期间管理。为了不破坏动画，Free H 不允许主动切换整套配装；不匹配当前配装的预设会拒绝应用，而不是强制角色重新加载。Maker 与 Studio 原有配装切换行为不变。

**0.4.0**：原生服装控制现覆盖全部九类 ClothesKind（上衣、下装、胸衣、内裤、手套、连裤袜、袜子、室内鞋、室外鞋）。每类衣物的 0–3 状态只提供当前物品实际支持的档位，并通过 `SetClothesState` 和读回确认应用。预设存储精确状态且继续验证服装身份；旧 XML 没有 State 字段时沿用原有 Visible=true/false → 0/3 的含义，不改变其他已保存信息。

**0.3.0**：Studio 新增独立的角色/场景预设选择。场景保存包含所有可唯一识别的角色；发现重复身份或尚未就绪的状态时拒绝写入。读取时按角色名与角色卡文件 basename（可用时）匹配，而非列表顺序或不稳定的 Studio 对象编号；同一身份出现多次、未找到、角色被替换、场景变化时跳过或取消。每个角色依次经过配装加载屏障，逐项核对已应用的饰品状态。支持取消剩余操作，已完成的改变不会回滚。XML 保留旧角色预设，新场景预设与角色预设可同名、独立保存/覆盖/删除。此版本不保证区分使用同一张卡且同名的多个实例，不记录完整角色卡路径，也不支持旧版写回含新字段的 XML。

**0.2.0**：窗口增加服装与饰品两个页面。服装页可恢复完整穿着，控制当前配装中的手套、袜子及室内外鞋，并在 Maker 使用原生鞋型切换；饰品页按主/副类别及名称/槽位筛选，批量显示或隐藏当前筛选结果。预设包含这些服装状态及可读鞋型；旧 XML 不含这些字段时不修改对应状态。未知服装、已替换物品以及读取失败的状态不写回。仍不包含特殊场景或全部 ClothingStateMenu 功能。

**0.1.1**：角色、配装和预设选择增加前后箭头；生产窗口纳入真实 UI 预览和最小尺寸回归。配装切换期间锁定编辑，等待目标角色与配装内容读回一致；KKAPI 重载回调加速完成判断，无回调时需要至少 0.75 秒的稳定匹配。超时或目标变化时不写饰品状态。外部角色身份/配装变化会使旧回调失效，未变化的窗口不再周期性重建。

Koikatsu / Koikatsu Party 的常规配装与饰品状态窗口，采用本仓 MaximumUI。默认 F7，可在 BepInEx 配置中更改。Maker 原生右侧控制栏提供 ClothingControl 开关，与快捷键及窗口关闭操作双向同步；`Show in Maker` 记忆显示偏好。原生界面被隐藏时仅暂停窗口显示，不改变该偏好。退出 Maker 时释放原生开关订阅并取消未完成操作。

- Maker 支持当前角色；Studio 列出场景中的角色并可独立切换；Free H 读取当前 HSceneProc 所有真实在场角色（包括男性）。
- 通过游戏原生配装 API 选择已有服装搭配，控制配装中已有饰品的显示；不会创建、删除或替换饰品。
- 保存、读取、覆盖、删除 XML 预设；预设记录配装编号和已有饰品的槽位、类别、物品 ID 与显示状态，不记录角色卡或服装资源。
- 应用预设时，槽位和物品身份均须匹配；不匹配时跳过。切换角色、场景或关闭面板会使旧操作失效。
- XML 默认位于 `BepInEx/config/MaxMixAlex.ClothingControl.presets.xml`，写入采用原子替换及备份。读取失败保留上次数据，禁止覆盖，支持重载。
- 0.1.0 范围限于 Maker / Studio 的常规配装和饰品管理，不包含特殊场景、身体显示控制或完整 ClothingStateMenu 功能复刻。

参考了 ManlyMarco / essu 的 [Illusion_ClothingStateMenu](https://github.com/ManlyMarco/Illusion_ClothingStateMenu) 对原生配装及饰品 API 的公开用法。这里是独立的 MaximumUI 实现，没有复制原项目代码。

```sh
dotnet build src/ClothingControl/ClothingControl.csproj -c Release
```

输出 `bin/Release/ClothingControl/KK_ClothingControl.dll`。安装时还需要同次构建的 `MaximumUI.dll`、`MaxIllusionPlugins.Common.dll` 以及游戏已有的 BepInEx 5、KKAPI。

验证：PluginCore.Regression 覆盖 XML 保存/覆盖/读回、重复名称、损坏文件保护、槽位及物品身份匹配。真实 Maker / Studio 的配装重载、扩展饰品和窗口操作仍需要游戏内测试。
