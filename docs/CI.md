# GitHub Actions 构建、验证与下载

工作流入口：[Build, test and package](https://github.com/maxmixalex/MaxIllusionPlugins/actions/workflows/ci.yml)。

## 下载并安装

1. 打开上面的 Actions 页面，选择目标分支最近一次绿色运行。正在开发的内容位于 `fix/plugin-audit-maximumui`；请核对运行页面的分支与提交。
2. 在运行页面底部的 **Artifacts** 下载 `plugin-packages-<运行序号>`。下载 Actions 产物通常需要登录 GitHub。
3. 解压这个下载文件，选择 `MaxIllusionPlugins-all-plugins.zip`，或按需选择单插件 ZIP。
4. 将所选 ZIP 内的 `BepInEx` 文件夹解压到游戏根目录。各包包含实际引用的本仓共享库，全部安装到 `BepInEx/plugins/MaxIllusionPlugins/`。

更新时，先退出游戏，并移除 `BepInEx/plugins` 其它目录里的同名旧插件 DLL、旧 `MaximumUI.dll` 和旧 `MaxIllusionPlugins.Common.dll`，避免重复加载。现有配置、XML、角色卡和存档不会随包覆盖。ClothingCompression 旧 XML 请自行由 `ClothingCompression.xml` 重命名为 `MaxMixAlex.ClothingCompression.xml`。

| 产物 | 内容 |
| --- | --- |
| `plugin-packages-…` | 五个功能插件单独的安装 ZIP、五插件合集、独立 Gallery ZIP、安装说明和程序集版本清单 |
| `maximumui-previews-…` | 真实 Gallery 的 Metro Dark / Light SVG，以及四个插件实际控件树的匿名示例预览；可以在浏览器中打开 |
| `build-test-logs-…` | 还原、编译、真实断言测试、托管微基准、预览和打包日志；失败时也会上传已生成的日志 |

独立 Gallery 不随五插件合集安装，需要测试控件时再安装其单独包；默认 `F10`、启动时隐藏，快捷键可在 Gallery 配置中调整。四个接入插件的独立包和合集均带上 `MaximumUI.dll`，无需另外安装 Gallery。产物保留 30 天；过期后可从新的运行下载。

## CI 实际执行内容

工作流位于 `.github/workflows/ci.yml`，在 push、pull_request 和手动 workflow_dispatch 时执行。使用 Ubuntu 24.04、.NET SDK 9，通过公开 NuGet 源恢复 net35 / Unity 5.6 编译引用，不需要上传本机游戏 DLL 或设置私有密钥。

1. 用临时假输出验证打包器的依赖包含与排除行为。
2. 还原并单节点 Rebuild 整个解决方案。
3. 执行 `python3 scripts/test.py --no-build --benchmark`，调用所有真实断言入口。单独运行 `dotnet test` 不会执行 MaximumUI 的原类库测试，工作流没有采用该路径。
4. 构建独立预览工具，从刚生成的 Gallery DLL 和实际插件 View 导出 SVG。插件数据由匿名工厂构造，不访问用户预设。
5. 调用 `scripts/package.py`，只从明确列出的生产项目目录选取主 DLL，并递归包含该项目的本仓 ProjectReference。
6. 上传安装包、预览和日志，在运行摘要中提供下载链接。

打包器不复制整个 `bin/Release/KK`，也不按 `*.dll` 通配输出；因此临时引用 DLL、测试程序集和旧汇总目录中的残留文件不会进入安装包。游戏、BepInEx、KKAPI 等第三方运行依赖需已经安装。`packages.json` 记录本仓程序集版本与源码提交，不生成散列校验清单。

所有带管道的日志命令均使用 GitHub 的显式 `bash` shell，其 `-e -o pipefail` 行为保留实际失败退出码；测试失败不能被 `tee` 变成绿色。

## 分支和手动触发

当前开发分支首次推送工作流即可通过 `push` 触发；已有 PR 更新也有 `pull_request` 入口。PR 默认检出 GitHub 生成的合并结果，安装包中的 `source_commit` 记录实际构建的提交。push 运行构建对应分支提交。

GitHub 的 **Run workflow** 按钮要求工作流文件位于默认分支。工作流尚未合入 main 时，使用开发分支的 push / PR 自动运行即可；无需为首次 CI 把功能改动提前合并。官方行为见 [触发工作流的事件](https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows)。

## 本地复现

```sh
dotnet restore MaxIllusionPlugins.sln
dotnet build MaxIllusionPlugins.sln -c Release --no-restore -t:Rebuild -m:1 -nodeReuse:false
python3 scripts/test.py --no-build --benchmark
python3 -m unittest discover -s scripts/tests -p 'test_package*.py' -v
python3 scripts/package.py --configuration Release --output artifacts/packages
```

CI 通过证明编译、托管断言及打包通过。Koikatsu 内的字体、输入、角色行为、动作、其它模组组合和长期帧耗时仍需运行游戏验证；SVG 预览也不等于 Unity 截图。
