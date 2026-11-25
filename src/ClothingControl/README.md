# ClothingControl

**0.1.1**：角色、配装和预设选择增加前后箭头；生产窗口纳入真实 UI 预览和最小尺寸回归。配装切换期间锁定编辑，等待目标角色与配装内容读回一致；KKAPI 重载回调加速完成判断，无回调时需要至少 0.75 秒的稳定匹配。超时或目标变化时不写饰品状态。外部角色身份/配装变化会使旧回调失效，未变化的窗口不再周期性重建。

Koikatsu / Koikatsu Party 的常规配装与饰品状态窗口，采用本仓 MaximumUI。默认 F7，可在 BepInEx 配置中更改。

- Maker 支持当前角色；Studio 列出场景中的角色并可独立切换。
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
