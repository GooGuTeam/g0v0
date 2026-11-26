# AGENTS.md

面向在本仓库工作的 AI 编码代理（以及人类贡献者）。
本文件只写**在这个 fork 里必须知道、且从上游 osu! 资料看不出来**的内容；通用架构与玩法逻辑请直接读代码。

---

## 1. 这是什么项目

- `g0v0.osu.Game` 是 [ppy/osu](https://github.com/ppy/osu)（osu!lazer）的 fork，由 **GooGuTeam** 维护，客户端对外名称是 **g0v0!**，对接 [g0v0-server](https://github.com/GooGuTeam/g0v0-server)。
- 相比上游的主要改动：自定义 API 服务器、Relax / Autopilot 特殊规则集、规则集哈希校验、服务器信息通知、更新源与错误上报切换到 GooGuTeam。逐条说明见 `README.md`，代码位置见第 6 节。
- 许可：代码 MIT（上游部分见 `LICENCE-OSU`，本 fork 新增部分见 `LICENCE`）；游戏资源 CC-BY-NC 4.0，仅限非商业使用。

### Git 布局（重要）

| 远端 / 分支 | 说明 |
|---|---|
| `origin` | `https://github.com/GooGuTeam/g0v0.git` |
| `upstream` | `https://github.com/ppy/osu.git`，只读，用于同步 |
| **`v2`** | **默认开发分支**，日常改动都落在这里；与上游的差距用 `git rev-list --left-right --count upstream/master...v2` 自查 |
| ~~`master`~~ | 已删除：GitHub 上该分支早已不存在，本地分支也已清理；仅剩失效的 `origin/master` 跟踪引用，**不要基于它开发** |
| 标签 | 发布标签形如 `v2026.910.2-g0v0`；同步前自行打基线标签（历史遗留的 `backup/*` 系列已于 2026-09 清理） |
| 资源仓库 | 资源已拆到独立仓库 `GooGuTeam/g0v0-resources`，以 NuGet 包 `g0v0.osu.Game.Resources` 被本仓库引用；主仓库用 `G0V0ResourcesVersion` 锁定版本 |

---

## 2. 环境与常用命令

需要 .NET SDK **10.0.100+**（`global.json` 固定 10.0.100，`rollForward: latestFeature`）；语言版本 **C# 14**（`Directory.Build.props` 的 `LangVersion`），`Nullable` 已开启。

```bash
dotnet restore osu.Desktop.slnf      # 自动从 nuget.org 还原 g0v0.osu.Game.Resources
dotnet build -c Debug -warnaserror osu.Desktop.slnf                                   # 与 CI 一致
dotnet build -c Debug -warnaserror osu.Desktop.slnf -p:EnforceCodeStyleInBuild=true   # 额外开启代码风格分析器

bash CheckSanity.sh <目录|文件>   # 代码规范体检（Windows 用 ./CheckSanity.ps1）
bash InspectCode.sh              # ReSharper 静态检查（Windows 用 ./InspectCode.ps1）

# 测试：先构建，再跑（CI 的做法：dotnet test 整个方案过滤器）
OSU_EXECUTION_MODE=SingleThread dotnet test osu.Desktop.slnf -- NUnit.ConsoleOut=0

# 只跑一个夹具（改 TestScene 时最常用，比全量快一个数量级）
OSU_EXECUTION_MODE=SingleThread dotnet test osu.Game.Tests/bin/Debug/net10.0/osu.Game.Tests.dll --filter "FullyQualifiedName~<夹具名>"
```

注意：

- `CheckSanity.sh` / `InspectCode.sh` 在 git 里是 `100755`，可直接 `./CheckSanity.sh`，`bash <script>` 也行。Windows 上 `.ps1` 若被执行策略拦住，先 `Set-ExecutionPolicy -Scope Process Bypass`（或改用 `powershell -File ./CheckSanity.ps1 <目录>`）。
- **框架是 GooGuTeam 自己的包，不是上游 ppy 的**：`g0v0.Framework` / `g0v0.Framework.Android` / `g0v0.Framework.iOS`（GooGuTeam 维护的 osu-framework fork）以 NuGet 包引用，版本在 `osu.Game/osu.Game.csproj`、`osu.Android.props`、`osu.iOS.props` **三处必须相同**。同步上游时不要把版本「合回」上游那个：不报冲突，但整条 CI（含 Android/iOS）会 CS0246。自查：
  ```bash
  grep -rn "g0v0.Framework" osu.Game/osu.Game.csproj osu.Android.props osu.iOS.props
  ```
  （想直接改框架源码才需要切项目引用；`UseLocalFramework.ps1` / `.sh` 仍是「移除 `ppy.osu.Framework` 项目引用」的老写法，与现在的包引用对不上，不能直接跑。）
- 方案过滤器：`osu.Desktop.slnf`（桌面 + 测试）、`osu.Android.slnf`、`osu.iOS.slnf`，不要直接构建主方案 `osu.slnx`。
- `osu.sln.DotSettings` **文件名刻意不改**：重命名该文件会在与上游改动同步时带来麻烦，并已验证本项目使用的工具链对 `.sln` 与 `.slnx` 后缀都识别。但两者同时存在时 `.slnx` 那份优先、且**不合并**——所以不要新建 `osu.slnx.DotSettings`（`.user` 同理）。
- 完整测试项目清单与矩阵见 `.github/workflows/ci.yml`：`osu.Game.Tests`、`osu.Game.Rulesets.{Osu,Taiko,Catch,Mania}.Tests`、`osu.Game.Tournament.Tests`、`Templates/**/*.Tests`，跑 Windows + Linux × `SingleThread`/`MultiThreaded` 两种线程模型。
- `bin/`、`obj/` 已在 `.gitignore` 中，不要提交。

---

## 3. 代码规范（CI 会拦）

### 格式

`.editorconfig`：`.cs` 使用 **CRLF**、4 空格缩进、去行尾空白、文件末尾换行；`*.csproj|props|targets` 使用 **UTF-8 BOM + CRLF + 2 空格**。

### `CheckSanity.sh` 的四条硬性检查（逐 `.cs`）

1. 行尾必须是 CRLF（不允许出现孤立的 `\r`）；
2. 不允许 tab 字符；
3. 不允许行尾空格（`///` 文档注释行豁免）；
4. **文件名必须与文件内定义的类型名一致**（`Foo.cs` 中需有 `class/struct/record/interface/enum Foo`）。

`*.designer.cs` 与 `AssemblyInfo.cs` 跳过；扫描忽略 `.git`、`bin`、`obj`、`Migrations`、`packages`、`osu.Game.Resources`（本地独立资源仓库 clone，可选）。

版权头**不在**这四条里：仓库里没有 `*.licenseheader`，脚本的头检查实际是空操作，所以「CheckSanity 过了」不代表头是对的——按下面三种自己抄。

### 版权头（按文件来源三种）

```csharp
// 1) GooGuTeam 全新创建的文件
// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

// 2) 修改了上游文件
// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

// 3) 未改动的上游文件（版权行保留 ppy 原文，第二行指向 LICENCE-OSU）
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.
```

- 全仓库统一**英式拼写**（`Licence` / `LICENCE`），与上游 ppy/osu 一致；不要写成美式 `License` / `LICENSE`。
- 根目录许可文件是 `LICENCE`（GooGuTeam 部分）与 `LICENCE-OSU`（上游部分），头里的引用必须与文件名一致：GooGuTeam 自己的文件指 `LICENCE`，纯上游文件指 `LICENCE-OSU`，两者都涉及就都写。
- 第 3 类与上游字面上只差第二行的文件名，这是有意为之；同步时若该行产生冲突，按本文件的口径解决。
- ppy 的版权署名是 MIT 的要求，**不是**商标问题，不要删（详见第 5 节）。

### 分析与命名

- `CodeAnalysis/osu.globalconfig` + `CodeAnalysis/BannedSymbols.txt`（被禁用的 API 会让构建报错）；CI 使用 `-warnaserror -p:EnforceCodeStyleInBuild=true`。
- 公共/受保护成员 PascalCase，私有成员 camelCase，私有 `const` 与 `static readonly` 全小写加下划线。

### 提交前自检（对应 CI 的 `inspect-code`）

```bash
bash CheckSanity.sh <改动目录>    # 规范体检；Windows: ./CheckSanity.ps1
bash InspectCode.sh               # ReSharper 静态检查；Windows: ./InspectCode.ps1
```

- 两个脚本都会在 `jb inspectcode` 之前自己跑一遍 `CheckSanity`（`.ps1` 直接调，`.sh` 是 `./CheckSanity.sh`），所以跑过 `InspectCode` 就不必再单独跑一次；反过来，只改了文案或格式时单跑 `CheckSanity` 更快，因为 `InspectCode` 会先 `dotnet clean` 再完整重编。
- 不用事先自己构建：脚本内部是 `dotnet tool restore` → `dotnet clean` → `dotnet build -warnaserror -p:EnforceCodeStyleInBuild` → `CheckSanity` → `jb inspectcode --no-build`。`--no-build` 只对最后一步生效，前面的 clean 是刻意的（不清会复用缓存，代码风格错误不再报）。
- 不再有 nvika 解析报告这一步：脚本以 `dotnet jb inspectcode` 的退出码为准，CI 的 `inspect-code` job 则用 `JetBrains/ReSharper-InspectCode` action 跑同一个 `osu.Desktop.slnf`。工具版本锁在 `.config/dotnet-tools.json`，脚本自带 `dotnet tool restore`，首次运行需要联网拉取工具。
- 改动很小时至少跑 `CheckSanity.sh`；碰到公共 API、可空性、性能写法、分析器规则时，把 `InspectCode` 一起跑掉再提交。

### 用户可见字符串（本地化）

**改到用户看得见的文案（标签、提示、tooltip、报错信息……）时，一次改动要同时落到两个仓库**：本仓库里的
`*Strings.cs` 类，和资源仓库 `g0v0-resources` 里的 `.resx`。只改前者，用户要么继续看到旧译文、要么看到
英文 fallback；只改后者，那个 key 立刻变成孤儿。

资源仓库是**独立 clone**，不在本仓库里（`osu.Game.Resources.Custom/` 只是遗留的构建产物目录，不是它）。
动手前先定位：

```bash
ls -d ../g0v0-resources ../../g0v0-resources ../osu-resources ./osu.Game.Resources 2>/dev/null
```

**找不到就问用户**——不要在本仓库里自己 clone 一份，也不要凭印象编 resx 路径。请用户执行：

```bash
git clone https://github.com/GooGuTeam/g0v0-resources.git ../g0v0-resources
```

找到之后的顺序：

1. **改类**。所有 `*Strings.cs` 都在 `osu.Game/Localisation/` 下（规则集的在
   `Catch` / `HUD` / `Mania` / `Osu` / `SkinComponents` / `Taiko` 子目录），按现有模式加
   `getKey(@"snake_case_key")` + 英文原文。类里的 `private const string prefix` 是**唯一权威**的
   命名空间来源，它最后一段就是目标 resx 的文件名：
   `osu.Game.Resources.Localisation.Catch.Action` → `Localisation/Catch/Action.resx`。
2. **补英文条目**。base resx（**文件名里只有一个点**的那个：`Common.resx` 是，`Common.zh.resx` 不是）
   由 `ppy.localisationanalyser.tools` 生成，不要手抄一遍：
   ```bash
   dotnet tool restore
   DOTNET_ROLL_FORWARD=LatestMajor dotnet localisation to-resx osu.Game/osu.Game.csproj --output /tmp/resx
   ```
   `DOTNET_ROLL_FORWARD` 是因为这个工具标的是 net8、开发机上只有 net10 运行时。
   先输出到临时目录再比着改，别直接 `--output` 到资源仓库：那会重写资源仓库里全部 base resx，而且这个工具在
   `2026.611.0` 上**跑不完**——读到 `ChatStrings.cs`（里面有 C# 原始字符串字面量 `"""`）会
   `NullReferenceException` 崩掉，之前已经写出的十几个文件留在原地。碰到 `ChatStrings.cs` /
   `GeneralSettingsStrings.cs` 这两处，那一条 `<data>` 手工照格式补。
3. **补中文**。在 `<X>.zh.resx` 和 `<X>.zh-Hant.resx` 里加同名 `<data name="...">` 并写译文 ——
   **双语是硬要求**：新 key 至少同时进 base 与 `zh`（fork 现状 base 与 `zh` 一一齐平、`zh-Hant` 略少，
   自查用 `find . -name '*.resx' ! -name '*.*.resx' | wc -l` 比 `find . -name '*.zh.resx' | wc -l`；有把握
   就一并补 `zh-Hant`）。上游那 40 多种语言由 Crowdin 维护（`l10n_master` 分支），**不要手写**；
   `zh` / `zh-Hant` 是本 fork 自己维护的，就是要手写。
   - resx 一律 **UTF-8 带 BOM**；保留 `<data>` 上的 `xml:space="preserve"`，条目顺序与 base 对齐。
   - 行尾不要操心：两个仓库都没有 `.gitattributes`，本机 `core.autocrlf=true` 会在 checkout 时写出
     CRLF、提交时收回 LF。**不要**在资源仓库里「顺手修正」行尾，也不要为它新增 `.gitattributes`。
4. **自查**。`.agents/skills/localisation-sync/` 里有 SKILL.md 和只读脚本
   `scripts/check-strings.sh`：它从 diff 里取出本次新增/改动的 key，逐个核对两个仓库是否都有。
   交叉校验器也能在本地跑：
   ```bash
   dotnet run --project ../g0v0-l10n-checker/src/g0v0.LocalisationChecker -- \
     --osu . --resources ../g0v0-resources --format human
   ```
5. **发版**。资源改动在 `g0v0-resources` 仓库提交、打 `v<version>-g0v0` 标签、发布新的
   `g0v0.osu.Game.Resources` 包；主仓库再把 `G0V0ResourcesVersion` 更新到该版本（第 7 节）。

### 测试

NUnit 4 + `osu.Framework` 的 `TestScene`：可视化测试放 `osu.Game.Tests/Visual/**`，纯逻辑测试放 `osu.Game.Tests/NonVisual/**`。fork 的破坏性改动使部分上游测试被 `[Ignore]`，恢复前先搞清楚原因（`git log` 里搜相关提交）。

- `TestScene` 每 step 推进 200ms 虚拟时间，`AddUntilStep` 也是每 200ms 求值；写完断言先做一次变异测试（把实现改坏，确认它真的 Failed），否则无法证明断言被执行过。

---

## 4. 同步上游（例行操作，最容易踩坑）

1. `git fetch upstream`，把 `upstream/master` 合并进 `v2`；解冲突时**顺手丢弃品牌字符串**比事后清理省事。
2. 资源仓库单独同步：进入独立的 `g0v0-resources` clone，确认其 `upstream` 远端为 `https://github.com/ppy/osu-resources`，同样合并 `upstream/master`；如有资源变更，发新包并同步更新主仓库 `G0V0ResourcesVersion`。
3. 合并后检查 `.github/workflows/*.yml` 与 `.github/actions/*.sh` 中的 `dotnet-version` / `--framework`（例如 `net8.0-*` → `net10.0-*`）是否与 `global.json`、csproj 对齐 —— 不对齐会直接让 CI 构建失败（历史上发生过）。同一趟还要确认三处 `g0v0.Framework*` 版本跟 `v2` 走（第 2 节）：分支上旧 pin 合进来不产生冲突，但会让 CI 全平台 CS0246。`.agents/skills/ui-design/tools/Previewer/Previewer.csproj` 还自己 pin 了一份 `G0V0FrameworkVersion`，同样要跟这三处对齐，慢了只影响预览器（restore 阶段 NU1605 降级错误），CI 不会提醒。
4. 确认主仓库 `G0V0ResourcesVersion` 指向的 `g0v0.osu.Game.Resources` 固定版本包已经在 nuget.org 上存在；它不必与本次发布 tag 的版本号相同。
5. 同步**前**先打一个基线标签（如 `sync-baseline-YYYYMMDD`）再动手，方便 diff 与商标扫描；历史遗留的 `backup/*` 标签已在 2026-09 清理，`trademark-check` 会退回到 `git merge-base HEAD upstream/master`。
6. 上游仍用 `osu.sln`（`upstream/master` 上最后一次改动是 2024-12-05，近年基本不动）。一旦某次合并**动到了 `osu.sln`**（典型场景：上游新增测试项目），必须进行以下处理：
   - 把新增项目手动加进 `osu.slnx` 与 相关项目的 `.slnf` 文件；
   - 删掉被合并回来的 `osu.sln`；
   - 核对三个 `*.slnf` 的 `path` 仍指向 `osu.slnx`。

---

## 5. 品牌与商标（硬性规则）

> **删除 / 重命名任何 osu!、ppy 商标内容之前，必须先获得人类确认。**

这个 fork 以 **g0v0!** 名义发布，不能把上游品牌呈现为自己的；但许可证要求的署名必须保留。

- 流程、报告模板与只读检测脚本都在 `.agents/skills/trademark-check/`：
  - `SKILL.md` —— 何时使用、clean/keep 对照表、报告格式；
  - `scripts/detect.sh` —— 只读检测，支持 `--base <ref>`、`--incoming`；
  - `references/known-outstanding.md` —— 已分类的既有项，避免重复上报。
- 触发时机：从上游同步后、更新并发布资源包后、打 `v*-g0v0` 发布标签前、新增用户可见字符串或图片素材时。
- 口径速记 —— **要清理**：用户可见的 `osu!` 字样、吉祥物 / logo / 宣传素材、商业字体、上游推广与联系链接；**要保留**：MIT 版权头、NuGet 包名（`g0v0.Framework` / `g0v0.Framework.Android` / `g0v0.Framework.iOS`、仍依赖的 `ppy.osu.Framework.NativeLibs`、本 fork 自己的 `g0v0.osu.Game.Resources`；上游对应包 `ppy.osu.Game.Resources` 仅作来源说明）、`osu.Game.*` 命名空间与类型名、`.osu` 格式以及 `osu!stable` / `osu!direct` 这类 nominative 用法。
- 产出永远是「清单 + 建议 + 提问」，拿到明确范围后才动文件。

---

## 6. Fork 改动分布（代码地图）

| 功能 | 主要位置 |
|---|---|
| 生产端点（`lazer.g0v0.top`） | `osu.Game/Online/ProductionEndpointConfiguration.cs`、`DevelopmentEndpointConfiguration.cs` |
| 自定义 API 服务器设置 | `osu.Game/OsuGameBase.cs`、`osu.Game/Configuration/OsuConfigManager.cs`、`osu.Game/Online/TrustedDomainOnlineStore.cs`、`osu.Game/Overlays/Settings/Sections/Online/ContentDownloadSettings.cs` |
| 规则集哈希校验 | `osu.Game/Rulesets/RulesetHashCache.cs`、`osu.Game/Online/API/APIAccess.cs`、`osu.Game/Online/HubClientConnector.cs`、`osu.Game/Screens/Play/{SubmittingPlayer,SoloPlayer,RoomSubmittingPlayer}.cs` |
| Relax / Autopilot | `osu.Game/Rulesets/Mods/ModRelax.cs`、`osu.Game.Rulesets.Osu/Difficulty/Skills/Relax.cs`、`osu.Game.Rulesets.Osu/Difficulty/Evaluators/Relax{Aim,Rhythm}Evaluator.cs` |
| 更新源（GooGuTeam/g0v0，双通道 g0v0/robin） | `osu.Game/Updater/{MobileUpdateNotifier,NoActionUpdateManager}.cs`（查 `GooGuTeam/g0v0` Releases），桌面侧用 Velopack（`osu.Desktop`） |
| 错误上报 | `osu.Game/Utils/SentryLogger.cs`（仅当连接的是 g0v0.top 时启用） |
| 程序名 / 图标 | `osu.Desktop/osu.Desktop.csproj`（`AssemblyName` = `g0v0!`） |
| 奖章素材与新增奖章 | `osu.Game/Users/Medal.cs`（`lazer-data.g0v0.top`）、`osu.Game/Medals/Awarders/` |
| 图像预览 / 截图管理 / 顶栏图标自定义 | `osu.Game/Overlays/ImagePreview/`、`osu.Game/Overlays/Screenshots/`、`osu.Game/Overlays/Toolbar/`（Markdown 图片入口在 `osu.Game/Graphics/Containers/Markdown/OsuMarkdownImage.cs`） |
| 导入其它 lazer 安装 | `osu.Game/Overlays/FirstRunSetup/ScreenImport.cs`、`osu.Game/IO/LazerStorage.cs` |
| 网络请求失败占位 | `osu.Game/Overlays/OnlineErrorPlaceholder.cs` |

改到 `g0v0-resources` 仓库里的素材或 `.resx` 时，记住它是要单独提交推送、单独打 tag 发布 NuGet 的另一个仓库；主仓库只通过 `G0V0ResourcesVersion` 引用发布后的包。

---

## 7. CI 与发布

- `.github/workflows/ci.yml`：`inspect-code`（构建 + CheckSanity + InspectCode）、`test`（Windows/Linux × 两种线程模型，120 分钟超时）、`build-only-android` 等。
- `.github/workflows/localisation.yml`：把本仓库的 `*Strings.cs` 与资源仓库的 `.resx` 交叉校验（实现放在 `GooGuTeam/g0v0-l10n-checker`，本文件只说谁在什么时机调用它）。它的触发条件是 PR、push 到 `v2`、手动 dispatch。结果发到 code scanning，所以调用方必须授予 `security-events: write`（缺了不报错，只是不上传）；资源仓库侧有一份镜像调用，两边各自上传自己那一半。
- 主仓库 `.github/workflows/release.yml`：
  - **完整发布**：`v*-g0v0` 标签（如 `v2026.913.1-g0v0`，稳定通道）或 `v*-robin` 标签（如 `v2026.1013.0-robin`，不稳定通道，GitHub Release 会标记为 prerelease）触发全平台构建 + NuGet + GitHub Release。双通道的实现与上游 lazer/tachyon 同构：通道编码进版本后缀，Velopack nupkg 版本带 `-g0v0`/`-robin` 后缀。NuGet 任务会先校验 `g0v0.osu.Game.Resources` 的 `G0V0ResourcesVersion` 固定版本包已发布，再发 `g0v0.osu.Game` 和全部 ruleset（`Osu` / `Catch` / `Mania` / `Taiko`）。
  - **仅 NuGet / 不重跑平台构建**：`v*-g0v0-nuget-only` 标签（如 `v2026.913.2-g0v0-nuget-only`）只跑 `version` 和 `nuget` 两个 job，**不跑平台构建、不创建 GitHub Release**。版本号同样取 tag 中的数字（仍要取下一个可用版本号；`g0v0.osu.Game.Resources` 的 `G0V0ResourcesVersion` 固定版本必须已发布，可与本 tag 版本不同）。
- 资源仓库 `GooGuTeam/g0v0-resources` 的 `.github/workflows/release.yml`：同样由 **`v*-g0v0`** 标签触发（发布资源包时也手动 dispatch 版本），使用 NuGet trusted publisher 发布 `g0v0.osu.Game.Resources`。**固定版本资源包首次使用时先发资源包；之后的规则集/主包发布无需每次重新发资源包。**
- **推送标签时只推 `-g0v0` 的发布标签。** 绝不要用 `git push --tags`：本地约 930 个标签里绝大多数是随 `upstream` 一起 fetch 下来的上游历史标签，一条命令就会把它们全部推到 `origin`。始终用显式标签名：
  ```bash
  git tag -l 'v*-g0v0*'                 # 本仓库自己的发布标签（含 -nuget-only）
  git push origin v2026.913.2-g0v0              # 完整发布：只推这一个
  git push origin v2026.913.2-g0v0-nuget-only   # 仅 NuGet：只推这一个
  ```
  推错标签既不会触发 release 构建，又会污染 `origin` 的标签空间（`origin` 目前只有 8 个 `v*-g0v0` 标签，保持这样）。
- 依赖与 Actions 版本由 Dependabot 维护（`.github/dependabot.yml`）。
- 打标签、推送远端、发布这类对外可见且不可逆的动作，先问人。

---

## 8. 提交与协作

- 提交信息沿用上游风格：祈使句、首字母大写、句末不加句号，例如 `Fix code sanity failures`；来自上游的提交保持原样并保留 `(#PR号)`，便于对账。
- 新分支从 `v2` 切出，PR 提到 `GooGuTeam/g0v0`。
- 大范围重格式化提交要记入 `.git-blame-ignore-revs`，避免污染 blame。

---

## 9. 代理工作方式

- 改完先跑 `bash CheckSanity.sh <改动目录>`；涉及公共 API 或分析器规则时改跑 `InspectCode`（它内部会带着 `CheckSanity`，不必跑两遍），然后跑与改动相关的测试项目，不要动辄全量。
- **改到界面外观时先看图再动手**。`.agents/skills/ui-design/SKILL.md` 带一个离屏 GPU 预览器（`tools/Previewer`，跑在 `OffscreenGameHost` 上，不开可见窗口），能把任意 `TestScene*`、规则集测试场景或编辑器屏幕渲成 PNG —— 用法是「同参数截 before → 改代码 → 同参数截 after → 读图对比」，不要只读布局代码就宣布改好了。缺陷先落成具体条目（对齐、溢出、间距、圆角、对比度、该用 tooltip 的长说明）再改。**两次截图必须用不同 `--out`**（如 `captures/<改动>-before` / `-after`）：同参数同目录时 after 会覆盖 before，唯一能对比的图就没了；after 那次还要显式加 `--build` / `-Build`（脚本默认复用旧可执行文件，改了 UI 却复用就等于拍修改前的界面），before 那次若不确定现有构建是否落后于源码也一样加。截图输出在 `tools/Previewer/captures/`（已 git-ignore），两边都不要提交；如果截到加载 spinner 或占位字形，那是 `--wait` 不够，加大重截再判断。（`tools/Previewer/Previewer.csproj` 自己 pin 了一份 `G0V0FrameworkVersion`，升框架版本时要跟第 2 节那三处一起改。）
- 判断某处差异是不是「fork 有意为之」时，查 `git log`、同步基线标签、`.agents/skills/trademark-check/references/known-outstanding.md`，别凭感觉改回去。
- 碰到用户可见文案，先按第 3 节「用户可见字符串」把类、base resx、`zh` 三处一起改；资源仓库没 clone 就直接问用户要路径，别跳过、也别自己往本仓库里 clone。核对时用 `.agents/skills/localisation-sync/`。
- 上游大版本升级后，优先核对 `global.json`、csproj 的 `TargetFramework` 与 CI 里的 SDK / framework 版本三者一致。
