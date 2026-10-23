# Clothing Compression

Koikatsu 的局部身体网格形变插件，面向衣物、饰品造成的束口与勒肉效果。提供大腿、整腿定位、膝部、大臂和前臂形变，支持左右侧、束口位置、影响宽度、内收深度、两侧隆起和服装外扩参数。

## 使用

- 角色编辑器中的“勒肉形变”页提供形变选择、新建、复制、删除及参数控制。
- 默认 `F8` 打开 MaximumUI 高级编辑面板。可拖动、缩放；左侧虚拟列表管理形变，右侧滚动编辑全部参数，底部保存和重载按钮保持可见。
- 高级面板在 Maker 中跟随当前角色，在 Studio 中跟随选中角色；未选中角色时显示空状态。
- 可把形变绑定到服装或饰品，并从其他搭配追加形变副本。
- 保存范围可选“角色 + 服装/饰品”或“仅服装/饰品”。配置库保存在 `BepInEx/config/MaxMixAlex.ClothingCompression.xml`，写入时保留 `.bak`。已有配置请手动把 `ClothingCompression.xml` 重命名为新文件名；插件不读取或自动迁移旧文件名。
- 运行依赖 BepInEx 5、KKAPI、BepisPlugins/Sideloader；安装包需包含 `MaximumUI.dll` 和 `MaxIllusionPlugins.Common.dll`。

0.4.0 的高级面板使用真实 MaximumUI 控件，保留新增、复制、删除、排序、左右侧、五类形变、物品绑定、保存级别、立即重算和取消绑定。参数支持滑条与直接输入，深度/隆起/服装外扩以厘米显示；保存仍使用原来的单位和 XML 字段。绑定对象及跨搭配来源形变支持搜索，可复制单个形变或来源搭配中的全部已保存形变。切换角色、搭配或所选形变后，旧界面排队的数值编辑不会写到新的目标。

保留模式只在数据变化时重建。绑定物品目录与跨搭配导入副本单独缓存：切换角色/搭配或实际换装时刷新物品目录，改变导入来源或成功保存/重载库时刷新导入数据；拖动数值滑条不再反复扫描服饰渲染器、解析 Sideloader 标识或复制整份导入数据。Maker 原生“勒肉形变”页继续保留，与高级面板共用业务操作、种类切换逻辑和数据缓存实现。插件销毁时卸载 MaximumUI 挂载；关闭或禁用时清理浮层与输入。旧的自制皮肤和弹出列表已移除。

## 代码与构建

`CompressionController.cs` 负责角色状态和网格变形；`CompressionModels.cs` 负责配置模型及存储；`ItemCatalog.cs` 处理服装/饰品与 Sideloader 标识；`MeshAccessDetour.cs` 处理网格访问；`CompressionEditorView.cs` 构建声明式控件，`CompressionGui.cs` 负责挂载和角色跟踪，`CompressionEditorData.cs` 管理与参数编辑分离的数据快照，`MakerSideToggleRow.cs` 提供原生 Maker 控件。

```sh
dotnet build src/ClothingCompression/ClothingCompression.csproj -c Release
```

KKAPI 和 Sideloader 的编译引用通过 NuGet 还原，不再要求本机游戏路径。输出：`bin/Release/ClothingCompression/KK_ClothingCompression.dll`。不同身体及服装模型的实际形变、绑定和保存效果需要在游戏内分别验证。
