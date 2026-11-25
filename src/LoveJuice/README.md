# LoveJuice

独立的 Koikatsu / Koikatsu Party BepInEx 插件工程。

## 当前状态

0.1.0 为工程框架：包含插件元数据、游戏进程过滤、BepInEx 配置、加载日志及统一构建入口。当前不会修改游戏场景、角色或其他插件的状态，也不会生成任何视觉效果。

- 插件 GUID：`MaxMixAlex.LoveJuice`
- 目标：.NET Framework 3.5 / Unity 5.6 / BepInEx 5
- 运行依赖：KKAPI
- 配置：`BepInEx/config/MaxMixAlex.LoveJuice.cfg`，`[General] Enabled` 默认关闭。当前为预留总开关。
- 不依赖 JuiceControl、FemboyMode 或 MaximumUI。

## 构建

在仓库根目录执行：

```sh
dotnet build src/LoveJuice/LoveJuice.csproj -c Release
```

使用仓库的集中 NuGet 版本及统一输出设置，预期输出 `bin/Release/LoveJuice/KK_LoveJuice.dll` 和 `bin/Release/KK/KK_LoveJuice.dll`。

## 加载验收

在安装 BepInEx 5 和 KKAPI 的 Koikatsu / Koikatsu Party 中，将 DLL 放入 `BepInEx/plugins/`，启动游戏后检查 `LogOutput.log` 的 LoveJuice 加载消息和配置文件。Studio 不应加载此插件。0.1.0 不应产生场景变化。

GitHub Actions 的 `LoveJuice scaffold build` 工作流负责编译、检查两个输出目录的 DLL 一致性，并生成只含本仓插件及许可/安装说明的安装产物。编译验证不代表已经完成游戏内加载验收。

本次框架尚未经过游戏内验收。
