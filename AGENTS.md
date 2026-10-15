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
| `origin` | `[https://github.com/GooGuTeam/g0v0.git](https://github.com/GooGuTeam/g0v0.git)` |
| `upstream` | `[https://github.com/ppy/osu.git](https://github.com/ppy/osu.git)`，只读，用于同步 |
| **`v2`** | **默认开发分支**，日常改动都落在这里；与上游的差距用 `git rev-list --left-right --count upstream/master...v2` 自查 |
| ~~`master`~~ | 已删除：GitHub 上该分支早已不存在，本地分支也已清理；仅剩失效的 `origin/master` 跟踪引用，**不要基于它开发** |
| 标签 | 发布标签形如 `v2026.910.2-g0v0`；同步前自行打基线标签（历史遗留的 `backup/*` 系列已于 2026-09 清理） |
| 资源仓库 | 资源已拆到独立仓库 `GooGuTeam/g0v0-resources`，以 NuGet 包 `g0v0.osu.Game.Resources` 被本仓库引用；主仓库用 `G0V0ResourcesVersion` 锁定版本 |

---

## 2. 环境与常用命令

需要 .NET SDK **10.0.100+**（`global.json` 固定 10.0.100，`rollForward: latestFeature`）；C# 12、`Nullable` 已开启。

```bash
dotnet restore osu.Desktop.slnf      # 自动从 nuget.org 还原 g0v0.osu.Game.Resources
dotnet build -c Debug -warnaserror osu.Desktop.slnf                                   # 与 CI 一致
dotnet build -c Debug -warnaserror osu.Desktop.slnf -p:EnforceCodeStyleInBuild=true   # 额外开启代码风格分析器

git config core.hooksPath .githooks  # 启用 pre-commit 钩子（自动运行轻量增量 InspectCode）

bash CheckSanity.sh <目录|文件>       # 代码规范体检（Windows 用 ./CheckSanity.ps1）
bash InspectCode.sh                  # 轻量增量检查工作区新增内容（Windows 用 ./InspectCode.ps1）
bash InspectCode.sh --staged         # 轻量增量检查暂存区新增内容（Windows 用 ./InspectCode.ps1 -Staged）
bash InspectCode.sh --full           # 全量 ReSharper 静态检查（Windows 用 ./InspectCode.ps1 -Full）
```

注意：

- `CheckSanity.sh` / `InspectCode.sh` 在 checkout 里若没有可执行位，请用 `bash <script>`，或先 `chmod +x`。
- 方案过滤器：`osu.Desktop.slnf`（桌面 + 测试）、`osu.Android.slnf`、`osu.iOS.slnf`。不要直接构建 `osu.sln`。
- `bin/`、`obj/` 已在 `.gitignore` 中，不要提交。

### 本地框架联调（`UseLocalFramework`）

根目录下的 `UseLocalFramework.ps1` / `UseLocalFramework.sh` 用于将本仓库对 NuGet 包 `ppy.osu.Framework`（含 Android / iOS）的依赖切换为同级目录下的本地源码仓库 `../g0v0-framework`，以便在修改底层框架时进行本地联调。

在使用本地框架开发时必须遵守以下规则：

1. **禁止使用条件编译或回退逻辑保留非本地框架路径**：在编写依赖框架改动的代码时，直接针对新框架 API 编写，严禁使用 `#if` 等条件编译指令、反射或兼容分支来保留使用非本地框架（旧版 NuGet 包）的代码路径。
2. **先发布框架再提交主仓库**：当 `g0v0` 的改动依赖 `g0v0-framework` 的修改时，必须先在 `g0v0-framework` 仓库完成提交并发布新版本包，随后在 `g0v0` 中更新框架包版本引用，最后再提交 `g0v0` 的代码。
3. **严禁提交 `UseLocalFramework` 修改的工程文件**：运行 `UseLocalFramework` 会修改 `osu.Game/osu.Game.csproj`、`osu.Android.props`、`osu.iOS.props`、`osu.sln` 以及 `osu.{Desktop,Android,iOS}.slnf`。提交代码前必须还原这些工程文件对 `../g0v0-framework` 的本地引用，绝对不要将更改后的 `.csproj`、`.props`、`.sln` 或 `.slnf` 提交上去。

---

## 3. 代码与设计规范

### 格式

`.editorconfig`：`.cs` 使用 **CRLF**、4 空格缩进、去行尾空白、文件末尾换行；`*.csproj|props|targets` 使用 **UTF-8 BOM + CRLF + 2 空格**。

### `CheckSanity.sh` 的四条硬性检查（逐 `.cs`）

1. 行尾必须是 CRLF（不允许出现孤立的 `\r`）；
2. 不允许 tab 字符；
3. 不允许行尾空格（`///` 文档注释行豁免）；
4. **文件名必须与文件内定义的类型名一致**（`Foo.cs` 中需有 `class/struct/record/interface/enum Foo`）。

`*.designer.cs` 与 `AssemblyInfo.cs` 跳过；扫描忽略 `.git`、`bin`、`obj`、`Migrations`、`packages`、`osu.Game.Resources`（本地独立资源仓库 clone，可选）。

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

### 分析、命名与文本规范

- `CodeAnalysis/osu.globalconfig` + `CodeAnalysis/BannedSymbols.txt`（被禁用的 API 会让构建报错）；CI 使用 `-warnaserror -p:EnforceCodeStyleInBuild=true`。
- 公共/受保护成员 PascalCase，私有成员 camelCase（禁止带 `_` 前缀），私有 `const` 与 `static readonly` 全小写加下划线（`lower_snake_case`）。
- **文本应该尽可能地本地化**：所有用户可见文本均应使用本地化字符串（`LocalisableString`），禁止直接硬编码英文字符串字面量；英文文本仅用于标识符、本地化 key（如 `getKey(@"snake_case_key")`）、本地化默认回退文本（在 `*Strings.cs` 内），以及内部日志、异常与调试文本等情况。
- 代码中直接书写的原始文本内容（非 `$""` 插值的普通字符串字面量）必须使用逐字字符串 `@"..."`（如 `@"snake_case_key"`、`@"default text"`），保持与上游代码风格一致。
- 代码中的注释（`//`、`///`）以及非用户可见的内部文本（日志、异常信息、调试文本等）都必须保持**英文**，拼写尽量沿用上游的英式英语习惯（如 `colour`、`initialise`、`localisable`、`behaviour`）。除非逻辑存在非直觉的特殊原因（解释 *Why*）或需要补充公共 API 文档，否则优先通过清晰的命名自解释，不要编写复述代码字面意思的冗余注释，也严禁在代码中留下诸如 `// Replaced B with A` 这类记录修改过程的注释。

### Pre-commit 与轻量版 `InspectCode` 检查

`InspectCode` 已作为 Git `pre-commit` 钩子运行，默认启用轻量级增量模式，**只检查新增的内容**：

```bash
bash InspectCode.sh --staged      # Linux / macOS：检查暂存区新增内容
./InspectCode.ps1 -Staged         # Windows PowerShell：检查暂存区新增内容
```

- 轻量级模式会拦截 `UseLocalFramework` 工程文件污染，对变更的 `.cs` 文件执行 `CheckSanity` 与新增行静态规则检查，并仅针对受影响的子项目运行单项目 Roslyn 代码风格分析（`EnforceCodeStyleInBuild=true`，只拦截编译错误以及落在本次 `git diff -U0` 新增代码行上的警告）。
- 不带参数运行时默认对比工作区与 `HEAD` 的增量改动；传入 `--full` / `-Full` 时才执行全量 `dotnet clean`、全量构建与 ReSharper `jb inspectcode` 扫描。

### 功能替代与架构演进

如果添加的新功能 A 是原有功能 B 的替代，除非用户显式说明需要保留旧功能，否则应当将功能 B **完全替换**为功能 A，并清理功能 B 的旧实现与调用路径。不要基于某种脆弱的条件判断在某些情况下将功能 A 路由回原有的功能 B，这不仅减少了可维护性，同时复杂化了整个系统。

### UI/UX 设计与交互规范

在编写或修改 UI 组件时，必须先参考仓库内其他的现有 UI，保持视觉风格与布局结构的一致性，不要做不一致的设计。

尝试使用不同的 `background`（背景色层级）对 UI 内容进行分组，就像 Material Design 的做法一样；在设置多层背景时注意保持 `background` 的色相（Hue）统一性，同时注意圆角大小（`CornerRadius`）与容器尺寸和层级的协调。

UI/UX 需要任何操作都应该有对应的过渡动画以及明显的视觉提示或者反馈设计，避免无反馈或生硬跳变的交互。

### 用户可见字符串（本地化与文案规范）

**文本应该尽可能地本地化**（使用 `LocalisableString`）。英文文本仅用于标识符与本地化 key 之类的情况；除此以外的所有用户可见文本（UI 控件、提示通知、对话框、设置项、Tooltip 等）均应在对应的 `*Strings.cs` 中统一定义为 `LocalisableString` 并在代码中引用，绝对不能因为本地缺少资源仓库就在 UI 中直接写死英文字符串。

说明性 UI 文本（如设置项描述、提示说明、Tooltip 等）不应该过长或者使用术语堆砌，应该以自然的口吻向用户描述。不要使用“不是（显而易见不是的东西，或者某个术语），而是（更精确的描述）”这类句式；即使描述是模糊的，如果能大体解释清楚含义，也是可以接受的。

处理本地化资源前，先静默检查本地是否存在独立的 `g0v0-resources` 仓库：

```bash
ls -d ../g0v0-resources ../../g0v0-resources ../osu-resources ./osu.Game.Resources 2>/dev/null
```

- **无论本地是否克隆 `g0v0-resources`，本仓库代码中都必须尽可能使用本地化字符串**：在 `osu.Game/Localisation/` 对应的 `*Strings.cs` 中按现有模式定义 `getKey(@"snake_case_key")` + `@"..."` 英文默认回退值，业务代码中统一使用这些本地化属性。
- **如果用户没有克隆 `g0v0-resources`**：在代码中完成上述本地化字符串类的定义与使用即可，**不要向用户提起任何有关资源仓库的事情，也不要自行克隆外部仓库**。
- **如果本地已克隆 `g0v0-resources`**，则在完成代码改动后按以下顺序同时更新资源仓库：

1. **改类**。所有 `*Strings.cs` 都在 `osu.Game/Localisation/` 下（规则集的在 `Catch` / `HUD` / `Mania` / `Osu` / `SkinComponents` / `Taiko` 子目录），按现有模式加 `getKey(@"snake_case_key")` + `@"..."` 英文原文。类里的 `private const string prefix` 是**唯一权威**的命名空间来源，它最后一段就是目标 resx 的文件名：`osu.Game.Resources.Localisation.Catch.Action` → `Localisation/Catch/Action.resx`。
2. **补英文条目**。base resx（**文件名里只有一个点**的那个：`Common.resx` 是，`Common.zh.resx` 不是）由 `ppy.localisationanalyser.tools` 生成，不要手抄一遍：
   ```bash
   dotnet tool restore
   DOTNET_ROLL_FORWARD=LatestMajor dotnet localisation to-resx osu.Game/osu.Game.csproj --output /tmp/resx
   ```
   先输出到临时目录再比着改，别直接 `--output` 到资源仓库。碰到 `ChatStrings.cs` / `GeneralSettingsStrings.cs` 这两处含原始字符串字面量 `"""` 导致工具报错的文件，手工照格式补那一条 `<data>`。
3. **补中文**。在 `<X>.zh.resx` 和 `<X>.zh-Hant.resx` 里加同名 `<data name="...">` 并写译文 —— 新 key 至少同时进 base 与 `zh`，有把握就一并补 `zh-Hant`。上游那 40 多种语言由 Crowdin 维护，**不要手写**。resx 一律 **UTF-8 带 BOM**，保留 `<data>` 上的 `xml:space="preserve"`，条目顺序与 base 对齐，不要在资源仓库里顺手修改行尾或新增 `.gitattributes`。
4. **自查**。使用 `.agents/skills/localisation-sync/scripts/check-strings.sh` 或运行 `../g0v0-l10n-checker` 核对两个仓库的 key 是否一致。
5. **发版**。资源改动在 `g0v0-resources` 仓库提交、打 `v<version>-g0v0` 标签、发布新的 `g0v0.osu.Game.Resources` 包；主仓库再把 `G0V0ResourcesVersion` 更新到该版本（第 7 节）。

### 测试

**不要添加测试，除非用户显式同意；并且不要进行任何的 Smoke Test（冒烟测试）**，因为 Smoke Test 通常过度简化了复杂的框架和运行环境。

当用户显式要求编写或维护测试时，仓库采用 NUnit 4 + `osu.Framework` 的 `TestScene`：可视化测试放 `osu.Game.Tests/Visual/**`，纯逻辑测试放 `osu.Game.Tests/NonVisual/**`。fork 的破坏性改动使部分上游测试被 `[Ignore]`，恢复前先搞清楚原因（`git log` 里搜相关提交）。

---

## 4. 同步上游（例行操作，最容易踩坑）

1. `git fetch upstream`，把 `upstream/master` 合并进 `v2`；解冲突时**顺手丢弃品牌字符串**比事后清理省事。
2. 资源仓库单独同步：进入独立的 `g0v0-resources` clone，确认其 `upstream` 远端为 `[https://github.com/ppy/osu-resources](https://github.com/ppy/osu-resources)`，同样合并 `upstream/master`；如有资源变更，发新包并同步更新主仓库 `G0V0ResourcesVersion`。
3. 合并后检查 `.github/workflows/*.yml` 与 `.github/actions/*.sh` 中的 `dotnet-version` / `--framework`（例如 `net8.0-*` → `net10.0-*`）是否与 `global.json`、csproj 对齐 —— 不对齐会直接让 CI 构建失败（历史上发生过）。
4. 确认主仓库 `G0V0ResourcesVersion` 指向的 `g0v0.osu.Game.Resources` 固定版本包已经在 nuget.org 上存在；它不必与本次发布 tag 的版本号相同。
5. 同步**前**先打一个基线标签（如 `sync-baseline-YYYYMMDD`）再动手，方便 diff 与商标扫描；历史遗留的 `backup/*` 标签已在 2026-09 清理，`trademark-check` 会退回到 `git merge-base HEAD upstream/master`。

---

## 5. 品牌与商标（硬性规则）

> **删除 / 重命名任何 osu!、ppy 商标内容之前，必须先获得人类确认。**

这个 fork 以 **g0v0!** 名义发布，不能把上游品牌呈现为自己的；但许可证要求的署名必须保留。

- 流程、报告模板与只读检测脚本都在 `.agents/skills/trademark-check/`：
  - `SKILL.md` —— 何时使用、clean/keep 对照表、报告格式；
  - `scripts/detect.sh` —— 只读检测，支持 `--base <ref>`、`--incoming`；
  - `references/known-outstanding.md` —— 已分类的既有项，避免重复上报。
- 触发时机：从上游同步后、更新并发布资源包后、打 `v*-g0v0` 发布标签前、新增用户可见字符串或图片素材时。
- 口径速记 —— **要清理**：用户可见的 `osu!` 字样、吉祥物 / logo / 宣传素材、商业字体、上游推广与联系链接；**要保留**：MIT 版权头、NuGet 包名 `ppy.osu.Framework` / 本 fork 自己的 `g0v0.osu.Game.Resources`（上游对应包 `ppy.osu.Game.Resources` 仅作来源说明）、`osu.Game.*` 命名空间与类型名、`.osu` 格式以及 `osu!stable` / `osu!direct` 这类 nominative 用法。
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

改到 `g0v0-resources` 仓库里的素材或 `.resx` 时，记住它是要单独提交推送、单独打 tag 发布 NuGet 的另一个仓库；主仓库只通过 `G0V0ResourcesVersion` 引用发布后的包。

---

## 7. CI 与发布

- `.github/workflows/ci.yml`：`inspect-code`（构建 + CheckSanity + InspectCode）、`test`（Windows/Linux × 两种线程模型，120 分钟超时）、`build-only-android` 等。
- `.github/workflows/localisation.yml`：把本仓库的 `*Strings.cs` 与资源仓库的 `.resx` 交叉校验（实现放在 `GooGuTeam/g0v0-l10n-checker`，本文件只说谁在什么时机调用它）。结果同时给出行内注解、job summary 和 code scanning 告警；资源仓库侧有一份镜像调用，两边各自上传自己那一半。历史上已知的差异记在 `.localisation-baseline.json`，只挡新增问题。
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

- 改完代码后通过轻量级增量 `InspectCode`（`bash InspectCode.sh` 或 `./InspectCode.ps1`，已集成至 `pre-commit`）验证新增内容；除非用户显式同意，否则不要添加任何测试，且严禁运行或编写任何 Smoke Test。
- 新功能替代旧功能时，默认直接完全替换旧功能，不要保留脆弱的回退或条件路由逻辑。
- 使用 `UseLocalFramework` 联调时，严禁用条件编译保留非本地框架路径；涉及框架改动时必须先提交并发布 `g0v0-framework`，且切勿将 `UseLocalFramework` 修改过的 `.csproj`、`.props`、`.sln`、`.slnf` 提交到仓库。
- 英文文本仅用于标识符、本地化 key（代码中直接书写的原始文本统一使用逐字字符串 `@"..."`）以及内部注释/日志；代码注释与非用户可见的内部文本一律保持英文（英式拼写），注释只解释非直觉的设计原因，不要写复述代码或记录修改过程的冗余注释。
- 判断某处差异是不是「fork 有意为之」时，查 `git log`、同步基线标签、`.agents/skills/trademark-check/references/known-outstanding.md`，别凭感觉改回去。
- 编写 UI 时参考现有界面，用统一色相（Hue）的不同明度 `background` 进行内容分组，注意圆角大小，并为所有操作提供动画与明显反馈；文案保持自然口吻，避免过长、堆砌术语或使用“不是……而是……”句式。
- 文本应该尽可能地本地化：无论本地是否克隆 `g0v0-resources`，所有用户可见文本均必须使用 `LocalisableString`（在 `*Strings.cs` 中集中定义，提供 key 与英文回退文本）；若本地存在 `g0v0-resources` 则同步修改类、base resx 与 `zh`；若用户没有克隆 `g0v0-resources`，不要向用户提及资源仓库或自行克隆，代码中仍照常定义并使用 `*Strings.cs`。
- 上游大版本升级后，优先核对 `global.json`、csproj 的 `TargetFramework` 与 CI 里的 SDK / framework 版本三者一致。
