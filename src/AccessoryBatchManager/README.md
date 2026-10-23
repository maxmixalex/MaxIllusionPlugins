# Accessory Batch Manager

Koikatsu 角色编辑器饰品批量管理插件。依赖 KKAPI 与 MoreAccessories，UI 使用独立的 Unity IMGUI，不依赖 MaximumUI。

## 使用

- 在角色编辑器中按 `+`（默认 `Shift+=`，也支持小键盘 `+`）打开或关闭窗口。
- **批量管理**：多选后清空饰品；一键整理会保持饰品顺序并把空槽移动到末尾。
- **复制/移动**：顶部选择来源与目标搭配；相同搭配执行搭配内操作，不同搭配执行跨搭配操作。来源和目标均可多选，支持同步滚动；数量相同时按槽位编号一一对应，一个来源也可复制到多个目标。支持整槽复制/移动、左右父级交换、调整 1/2 复制、左右反转和上下反转。
- **设置**：手动重建饰品模型、开启操作后立即同步，以及控制选择高亮；最近选择的槽位为橙色，其余已选槽位为蓝色。
- 在游戏原生“服装读入”页选中服装卡时，可将该卡作为复制来源（包括 MoreAccessories 扩展槽位）。
- MoreAccessories 原生饰品列表最顶部提供 `+1 到最前` 和 `+10 到最前`，直接执行且不弹确认。

所有写入操作都会先显示二次确认。

整理采用稳定的“非空槽前移、空槽补到末尾”方式，不把槽位数组缩到 20 以下。这样能保持 MoreAccessories 对七套搭配的数组同步和原版存档兼容性。

## 构建产物

```sh
dotnet build src/AccessoryBatchManager/AccessoryBatchManager.csproj -c Release
```

`bin/Release/AccessoryBatchManager/KK_AccessoryBatchManager.dll`

可选兼容包括 ClothesToAccessories、SliderHighlight 与材质编辑相关桥接。构建引用通过 NuGet 还原；实际跨搭配数据、扩展槽位及材质效果仍需在对应插件组合下验证。
