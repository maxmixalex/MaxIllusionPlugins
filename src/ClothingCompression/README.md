# Clothing Compression

Koikatsu 的局部身体网格形变插件，面向衣物、饰品造成的束口与勒肉效果。提供大腿、整腿定位、膝部、大臂和前臂形变，支持左右侧、束口位置、影响宽度、内收深度、两侧隆起和服装外扩参数。

## 使用

- 角色编辑器中的“勒肉形变”页提供形变选择、新建、复制、删除及参数控制。
- 默认 `F8` 打开 MaximumUI 高级编辑面板。可拖动、缩放；左侧固定宽度虚拟列表管理形变，右侧滚动编辑全部参数，底部依次为 `保存形变设置`、`从 XML 重载`，保持可见。
- 高级面板在 Maker 中跟随当前角色，在 Studio 中跟随选中角色；未选中角色时显示空状态。
- 可把形变绑定到服装或饰品，并从其他搭配追加形变副本。
- 保存范围可选“角色 + 服装/饰品”或“仅服装/饰品”。配置库保存在 `BepInEx/config/MaxMixAlex.ClothingCompression.xml`，写入时保留 `.bak`。已有配置请手动把 `ClothingCompression.xml` 重命名为新文件名；插件不读取或自动迁移旧文件名。
- 运行依赖 BepInEx 5、KKAPI、BepisPlugins/Sideloader；安装包需包含 `MaximumUI.dll` 和 `MaxIllusionPlugins.Common.dll`。

0.4.1 的高级面板使用亮色 MDL2 样式，并按迁移前界面恢复布局和文案。左侧 `+ 新增形变` 直接创建默认大腿形变，其后可复制、删除或用方向按钮排序。右侧 `大腿`、`整腿`、`膝部`、`大臂`、`前臂` 为同行直接选择项；保存级别也保持两个直接选择项。参数恢复为原滑块和只读数值，深度/隆起/服装外扩以厘米显示；保存继续使用原来的单位和 XML 字段。

`从其他服装搭配复制` 区域只保留 `来源搭配` 和 `复制形变`，一次追加来源搭配中全部已保存角色级形变。`从 XML 重载` 直接重载；无效 XML 会保留当前编辑并显示原因。早期迁移新增的种类下拉框、数字输入、单个来源形变搜索/复制以及重载确认步骤已移除。原绑定警告、`立即重算` 和 `取消绑定` 保留。逐项对照见 [业务 UI 复刻核对](../../docs/UI-Parity-2026-10-24.md)。

切换角色、搭配或所选形变后，旧界面排队的数值编辑不会写到新目标；来源搭配选择也检查当前角色和搭配身份。实际控制器回归覆盖原创建/复制/导入/重载动作、单位换算、绑定与保存级别、正常及最小窗口下的布局、旧回调失效和 XML 往返。

保留模式只在数据变化时重建。绑定物品目录与跨搭配导入副本单独缓存：切换角色/搭配或实际换装时刷新物品目录，改变导入来源或成功保存/重载库时刷新导入数据；拖动数值滑条不再反复扫描服饰渲染器、解析 Sideloader 标识或复制整份导入数据。Maker 原生“勒肉形变”页继续保留，与高级面板共用业务操作、种类切换逻辑和数据缓存实现。插件销毁时卸载 MaximumUI 挂载；关闭或禁用时清理浮层与输入。旧的自制皮肤和弹出列表已移除。

## 代码与构建

`CompressionController.cs` 负责角色状态和网格变形；`CompressionModels.cs` 负责配置模型及存储；`ItemCatalog.cs` 处理服装/饰品与 Sideloader 标识；`MeshAccessDetour.cs` 处理网格访问；`CompressionEditorView.cs` 构建声明式控件，`CompressionGui.cs` 负责挂载和角色跟踪，`CompressionEditorData.cs` 管理与参数编辑分离的数据快照，`MakerSideToggleRow.cs` 提供原生 Maker 控件。

```sh
dotnet build src/ClothingCompression/ClothingCompression.csproj -c Release
```

KKAPI 和 Sideloader 的编译引用通过 NuGet 还原，不再要求本机游戏路径。输出：`bin/Release/ClothingCompression/KK_ClothingCompression.dll`。不同身体及服装模型的实际形变、绑定和保存效果需要在游戏内分别验证。
