# MaximumUI Gallery

MaximumUI 的游戏内示例插件，展示控件、布局、表格、对话框、主题、自定义绘制及调试工具。主要代码在 `MaximumUIGalleryPlugin.cs`，可作为使用 `MaximumUIRuntime.Mount` 和声明式控件的参考。

- 默认显示窗口，按 `F8` 切换可见性。
- `Ctrl+1` 至 `Ctrl+5` 切换示例区域，`Ctrl+L` 操作日志示例。
- 运行需要 BepInEx 及 `MaximumUI.dll`。
- 默认 `F8` 与 ClothingCompression 的高级面板相同，联合测试时注意快捷键同时触发。
- 默认使用 `CleanLight` 主题，按状态失效构建，只创建当前标签页的内容；调试页每 0.25 秒刷新指标。

```sh
dotnet build src/MaximumUIGallery/MaximumUIGallery.csproj -c Release
```

构建输出为 `bin/Release/MaximumUIGallery/KK_MaximumUIGallery.dll`，安装时同时提供 `MaximumUI.dll`。Gallery 是开发示例，不是其他功能插件的必装依赖。

回归时检查输入文字是否清晰、滚动内容是否可到达末行、拖动结束后鼠标是否正常释放，以及打开/关闭/重入多个对话框后输入是否仍只由顶层处理。性能测试应分别观察静止页面、持续输入和打开大量选项时的帧耗时；无界面构建/布局基准参见 [MaximumUI README](../MaximumUI/README.md)。
