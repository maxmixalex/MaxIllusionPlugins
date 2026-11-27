# ClothingControl

**0.5.8 — native built-in bra and shorts:** Restored the other two owner chains using the installed game's decompiled `ChaControl`: `notBra` (top owns bra) and `notShorts` (bra owns shorts). Each integrated part receives a usable row even without a separate clothing ID. When that part has a real native state dictionary, operations use it; otherwise the row explicitly follows the actual owning top/bra state channel, never a fabricated child slot. Labels distinguish `胸罩（随上衣）`, `内裤（随胸罩）` and `内裤（随上衣）`. Both standalone and multi-part garments preserve their identity in presets and use only original `SetClothesState` transitions. Pure rule, UI cycling and preset round-trip tests included.

**0.5.7 — native one-piece lower-state control:** Reversed the incorrect assumption that `parts[bot].id == 0` means there is no usable bottom control. In the shipped game's `ChaControl.ChangeClothesTopAsync`, a top with `Coordinate=2` sets `notBot`; `AddClothesStateKind(0)` builds the bottom state dictionary from the composite garment; and `SetClothesState(1,...)` uses the native `notBot` synchronization for top/bottom states. ClothingControl now recognizes this real bottom channel, displays `下装（连体）`, takes the owning top garment's ID as a stable preset identity, and cycles only native-supported bottom states. Save/reload and UI dispatch regression coverage included. Normal separate bottoms are unchanged. No additional garment prefab is created or state table invented.

**0.5.6 — lower-garment visibility:** the clothing panel always displays all nine native clothing types, including the bottom garment. A slot without an equipped item displays “未装备”; a currently masked/unsupported slot displays its current state but cannot invoke an invalid native state transition. Equipped lower garments no longer disappear merely because IsClothesStateKind temporarily reports false (e.g. a covering top). Explicitly supported native states remain the only writable actions. Presets still store only real equipped clothing.

**0.5.5 — wardrobe layout polish (no feature refactor):** all dropdown selectors in ClothingControl are now native MaximumUI cycling buttons: left/right arrows and a clickable current-value button for character, coordinate, preset, preset scope, accessory group and valid per-garment wear states. Disabled/busy states and allowed wear-state filtering are preserved. The tab order is now **Clothing first, Accessories second**; the default page (index 0) opens Clothing. The nine-or-fewer clothing rows are rendered in a fixed VStack rather than an internally scrolling VirtualList. **Shoe mode** and **Restore full outfit** appear immediately below the clothing rows. Default and minimum window height both grow from 720/700 to 900 UI units, ensuring the nine native garment rows and lower preset controls never overlap when users resize the window; accessories retain VirtualList virtualization for many accessory slots. No changes to garment operations, XML, MaximumUI core, startup or keybindings.


**0.5.4 — RX18 Mono runtime type-load fix:** `Koikatu_Data/output_log.txt` confirmed a startup-time `TypeLoadException: Could not load type System.IO.InvalidDataException from assembly KK_ClothingControl` inside `ClothingControlPlugin.Awake()`. All ten `InvalidDataException` references in the production clothing preset models were replaced by legacy-Mono-compatible `System.IO.IOException`, without changing validation messages, catch paths, XML format, window layout, or game actions. This prevents the Unity engine from disabling the plugin at frame 0 before OnEnable/Update and KKAPI Maker registration. The accidental `Show menu = LeftShift` shortcut produced when Config Manager consumed Tab now migrates to the original `Tab + LeftShift` on startup. Previous F7 default migration is retained. Test both the native Maker sidebar and Shift+Tab with the same DLL; if it still fails, inspect Unity's `Koikatu_Data/output_log.txt` for any other TypeLoadException, not only `BepInEx/LogOutput.log`.


**0.5.3**：解决用户在 RX18 Maker 中确认 0.5.2 已成功加载、快捷键也正确，但仍无窗体和按键诊断的情况。KKAPI 的 Maker/角色事件订阅现改为在 `Awake` 初始化，并仅在插件 `OnDestroy` 时取消，与仓库中已工作的 AccessoryBatchManager、ClothingCompression 一致，避免 BepInEx 启动阶段立即发生的 `OnDisable` 清空订阅。新增 **KKAPI MakerFinishedLoading 直接启动的恢复协程**，在 Maker 完全就绪后按 `Show in Maker` 已保存的值调用现有 `Open`，不再只依赖 `Update` 每帧检查；F1 配置中修改 `Show in Maker` 也会立即打开/关闭已加载 Maker 的 MaximumUI 窗口。新增 Unity `OnEnable`、`OnDisable`、`Update` 首帧及 Maker 生命周期日志，用于区别组件未执行、KKAPI 回调未触发及窗口被隐藏。其余快捷键、原版服装读写、预设和 MaximumUI 布局均不改变。\n\n**0.5.2**：修复部分高插件量 RX18 Maker 环境中 Shift+Tab 虽已配置却未打开 MaximumUI 窗口的问题。保留原有 BepInEx KeyboardShortcut 路径，对原版默认 Shift+Tab 添加 Unity `Input.GetKeyDown` 和 `OnGUI KeyDown` 双兜底；按键同帧去重，不影响自定义快捷键。启动、Maker 载入、原生侧边栏开关、按键进入、打开／关闭和 MaximumUI mount 可见状态均增加有限诊断日志。未改服装功能、UI 组件或预设结构。若无按键日志，检查输入是否被其他插件/游戏捕获；若有“opening result”但界面不可见，则针对实际 MaximumUI 状态调查。


**0.5.1**：默认开关快捷键改为与原版 Clothing State Menu 一致的 **LeftShift+Tab**。升级时若现有配置仍为旧版默认的 `F7`，会一次性迁移并保存为 `LeftShift+Tab`；用户自定义的其他快捷键不变。按键被原生 Maker 隐藏状态、MaximumUI 输入框／对话框或不支持的场景拦截时，现在会在 BepInEx 日志中说明原因。只修复入口，不改 MaximumUI 窗口及服装功能。若保留原版 `KK_ClothingStateMenu.dll`，两者会响应同一快捷键；用于替代时应禁用原版插件，避免两个窗口同时打开。

**0.5.0**：新增 **Free H** 使用入口（默认 F7）。角色选择读取游戏当前 `HSceneProc` 的 `lstFemale` 及单独的 `male`，包括普通 3P 和具有 `male1` 字段的 MMF 场景第二位男性；字段不存在时安全跳过，不扫描不相关的 NPC。当前角色的九类衣物原生状态、饰品可见性和 XML 预设可以在播放期间管理。为了不破坏动画，Free H 不允许主动切换整套配装；不匹配当前配装的预设会拒绝应用，而不是强制角色重新加载。Maker 与 Studio 原有配装切换行为不变。

**0.4.0**：原生服装控制现覆盖全部九类 ClothesKind（上衣、下装、胸衣、内裤、手套、连裤袜、袜子、室内鞋、室外鞋）。每类衣物的 0–3 状态只提供当前物品实际支持的档位，并通过 `SetClothesState` 和读回确认应用。预设存储精确状态且继续验证服装身份；旧 XML 没有 State 字段时沿用原有 Visible=true/false → 0/3 的含义，不改变其他已保存信息。

**0.3.0**：Studio 新增独立的角色/场景预设选择。场景保存包含所有可唯一识别的角色；发现重复身份或尚未就绪的状态时拒绝写入。读取时按角色名与角色卡文件 basename（可用时）匹配，而非列表顺序或不稳定的 Studio 对象编号；同一身份出现多次、未找到、角色被替换、场景变化时跳过或取消。每个角色依次经过配装加载屏障，逐项核对已应用的饰品状态。支持取消剩余操作，已完成的改变不会回滚。XML 保留旧角色预设，新场景预设与角色预设可同名、独立保存/覆盖/删除。此版本不保证区分使用同一张卡且同名的多个实例，不记录完整角色卡路径，也不支持旧版写回含新字段的 XML。

**0.2.0**：窗口增加服装与饰品两个页面。服装页可恢复完整穿着，控制当前配装中的手套、袜子及室内外鞋，并在 Maker 使用原生鞋型切换；饰品页按主/副类别及名称/槽位筛选，批量显示或隐藏当前筛选结果。预设包含这些服装状态及可读鞋型；旧 XML 不含这些字段时不修改对应状态。未知服装、已替换物品以及读取失败的状态不写回。仍不包含特殊场景或全部 ClothingStateMenu 功能。

**0.1.1**：角色、配装和预设选择增加前后箭头；生产窗口纳入真实 UI 预览和最小尺寸回归。配装切换期间锁定编辑，等待目标角色与配装内容读回一致；KKAPI 重载回调加速完成判断，无回调时需要至少 0.75 秒的稳定匹配。超时或目标变化时不写饰品状态。外部角色身份/配装变化会使旧回调失效，未变化的窗口不再周期性重建。

Koikatsu / Koikatsu Party 的常规配装与饰品状态窗口，采用本仓 MaximumUI。默认 Shift+Tab（与原版 Clothing State Menu 一致），可在 BepInEx 配置中更改。Maker 原生右侧控制栏提供 ClothingControl 开关，与快捷键及窗口关闭操作双向同步；`Show in Maker` 记忆显示偏好。原生界面被隐藏时仅暂停窗口显示，不改变该偏好。退出 Maker 时释放原生开关订阅并取消未完成操作。

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
