# JuiceControl

Koikatsu 的角色皮肤效果手动编辑器，可调整原生身体部位效果、泪水和脸红等级，并对角色锁定效果值。支持保存角色预设及分组预设，通过运行时兼容代码集成可选的 SkinEffects。

- 默认按主键盘 `0` 显示或隐藏窗口，可在 BepInEx 配置中修改。
- 角色选择、效果编辑、锁定及预设逻辑位于 `JuiceControl.cs`。
- `JuiceGui.cs` 提供本插件的 IMGUI 皮肤与绘制辅助，不依赖 MaximumUI。
- 默认预设文件为 `BepInEx/config/MaxMixAlex.JuiceControl.presets.xml`，可通过配置修改。
- 支持旧版产生的空根元素及显式 `xsi:nil="true"` 根；载入这些合法空文档不会自动重写文件。
- 载入失败时，预设面板持续显示文件路径和底层解析原因，日志保留完整异常。仍可应用上次成功载入的预设，保存/删除按钮暂时停用；修复文件后点击“重载XML”即可恢复。零字节、截断、非法字符或错误根元素不会被当成空预设覆盖。
- 运行依赖 BepInEx 5、KKAPI；SkinEffects 为可选依赖。

```sh
dotnet build src/JuiceControl/JuiceControl.csproj -c Release
```

输出：`bin/Release/JuiceControl/KK_JuiceControl.dll`。SkinEffects 的不同安装版本可能存在成员差异，兼容状态可从配置与日志检查；实际效果需在对应游戏场景验证。
