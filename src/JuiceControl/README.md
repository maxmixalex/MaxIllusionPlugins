# JuiceControl

Koikatsu 的角色皮肤效果手动编辑器，可调整原生身体部位效果、泪水和脸红等级，并对角色锁定效果值。支持保存角色预设及分组预设，通过运行时兼容代码集成可选的 SkinEffects。

- 默认按主键盘 `0` 显示或隐藏窗口，可在 BepInEx 配置中修改。
- 0.2.0 使用 MaximumUI Metro 界面：角色选择、效果与锁定、角色 / 场景预设分为清晰的页面，窗口支持拖动、缩放和折叠。
- 角色选择、效果编辑、锁定及预设逻辑保留在 `JuiceControl.cs`；`JuiceMaximumUi.cs` 对接游戏状态，`JuiceEditorView.cs` 使用真实声明式控件组成界面。
- 效果列表使用 VirtualList；界面按需重建，仅在窗口打开时每 0.2 秒比较一次外部角色和效果状态。预设名称可直接输入，Enter 保存、Escape 取消；文本输入或模态窗口打开时不会触发开关快捷键。
- 角色列表同时比较角色对象身份；同名角色替换会正确刷新。界面回调绑定角色、预设库和目标预设，换库、关闭、禁用或重新打开窗口后不会执行旧界面遗留的操作；正常效果数值刷新保留尚未应用的最后一次编辑。禁用组件会卸载窗口并释放命名对话框目标。
- 默认预设文件为 `BepInEx/config/MaxMixAlex.JuiceControl.presets.xml`，可通过配置修改。
- 支持旧版产生的空根元素及显式 `xsi:nil="true"` 根；载入这些合法空文档不会自动重写文件。
- 载入失败时，预设面板持续显示文件路径和底层解析原因，日志保留完整异常。仍可应用上次成功载入的预设，保存/删除按钮暂时停用；修复文件后点击“重载XML”即可恢复。零字节、截断、非法字符或错误根元素不会被当成空预设覆盖。
- 运行依赖 BepInEx 5、KKAPI；SkinEffects 为可选依赖。

```sh
dotnet build src/JuiceControl/JuiceControl.csproj -c Release
```

输出：`bin/Release/JuiceControl/KK_JuiceControl.dll`。安装时同时提供本次构建的 `MaximumUI.dll` 和 `MaxIllusionPlugins.Common.dll`；不需要安装 Gallery。现有 BepInEx 配置和 XML 格式保留。

实际生产 View 会链接进 MaximumUI 的回归宿主，验证命令分发、角色 / 场景预设选择、XML 失败时禁止写入、命名模态和虚拟列表。SkinEffects 的不同安装版本可能存在成员差异，兼容状态可从界面、配置与日志检查；真实字体、输入和皮肤效果仍需在对应游戏场景验证。
