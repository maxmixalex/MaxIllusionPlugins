# Clothing Compression

Koikatsu 的局部身体网格形变插件，面向衣物、饰品造成的束口与勒肉效果。提供大腿、整腿定位、膝部、大臂和前臂形变，支持左右侧、束口位置、影响宽度、内收深度、两侧隆起和服装外扩参数。

## 使用

- 角色编辑器中的“勒肉形变”页提供形变选择、新建、复制、删除及参数控制。
- 默认 `F8` 打开高级编辑面板。
- 可把形变绑定到服装或饰品，并从其他搭配追加形变副本。
- 保存范围可选“角色 + 服装/饰品”或“仅服装/饰品”。配置库保存在 `BepInEx/config/ClothingCompression.xml`，写入时保留 `.bak`。
- 运行依赖 BepInEx 5、KKAPI、BepisPlugins/Sideloader。

## 代码与构建

`CompressionController.cs` 负责角色状态和网格变形；`CompressionModels.cs` 负责配置模型及存储；`ItemCatalog.cs` 处理服装/饰品与 Sideloader 标识；`MeshAccessDetour.cs` 处理网格访问；`CompressionGui.cs` 与 `MakerSideToggleRow.cs` 提供 UI。

```sh
dotnet build src/ClothingCompression/ClothingCompression.csproj -c Release
```

KKAPI 和 Sideloader 的编译引用通过 NuGet 还原，不再要求本机游戏路径。输出：`bin/Release/ClothingCompression/KK_ClothingCompression.dll`。不同身体及服装模型的实际形变、绑定和保存效果需要在游戏内分别验证。
