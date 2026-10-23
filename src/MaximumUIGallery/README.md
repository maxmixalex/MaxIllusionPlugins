# MaximumUI Gallery

MaximumUI 的游戏内示例插件，展示控件、布局、表格、对话框、主题、自定义绘制及调试工具。主要代码在 `MaximumUIGalleryPlugin.cs`，可作为使用 `MaximumUIRuntime.Mount` 和声明式控件的参考。

- 默认显示窗口，按 `F8` 切换可见性。
- `Ctrl+1` 至 `Ctrl+5` 切换示例区域，`Ctrl+L` 操作日志示例。
- 运行需要 BepInEx 及 `MaximumUI.dll`。
- 默认 `F8` 与 ClothingCompression 的高级面板相同，联合测试时注意快捷键同时触发。

```sh
dotnet build src/MaximumUIGallery/MaximumUIGallery.csproj -c Release
```

构建输出为 `bin/Release/MaximumUIGallery/KK_MaximumUIGallery.dll`，安装时同时提供 `MaximumUI.dll`。Gallery 是开发示例，不是其他功能插件的必装依赖。
