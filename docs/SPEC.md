# RevitLocalDbFix — 开发文档 (SPEC)

> 本文档是完整的开发规格，交付给 Claude Code 会话实施。所有设计决策已与需求方确认，实施时**不要改动流程顺序、实例名映射和安全规则**；遇到文中标注 `[待实测]` 的项，先实测再定，并在 `docs/IMPLEMENTATION_NOTES.md` 中记录结论。

---

## 1. 项目概述

### 1.1 背景
Revit（钢结构连接 Steel Connections 模块）和 Advance Steel 依赖 **Microsoft SQL Server Express LocalDB** 承载钢连接数据库。LocalDB 异常时 Revit 表现为：打开/同步/链接模型卡死或崩溃、样板加载卡住、IFC 导入失败、启动时黑色命令行窗口闪现（`SQLDUMPER.EXE`）。Journal 中出现：

```
DBG_WARN: SQLLocalDB instance is malfunctioning. 'Expect crashes when using steel functionalities in Revit. ' Please stop the current Revit session !
```

### 1.2 目标
一个 Windows 桌面向导工具，供 Autodesk 技术支持工程师和最终用户使用：
1. 用户选择 Revit 版本；
2. **第 1 步（隔离试验）**：自动定位该版本安装目录下的 `AddIns\SteelConnections`，zip 备份后摘除，让用户试运行 Revit 以确认病因；不需要钢连接的用户到此即可结束；
3. **第 2 步（逐步确认修复）**：严格按 Autodesk 文章的排查顺序，逐步执行 `sqllocaldb` 命令、磁盘扇区检查、实例重建、清洁重装；**每一步把原始命令输出展示给用户，并与原文预期对照，用户确认后才进入下一步**；
4. 修复完成后自动还原 SteelConnections，最终验证并生成可转发的报告。

### 1.3 依据文章（界面和报告中必须显示可点击链接）

| ID | 标题 | URL |
|---|---|---|
| `ART_REVIT_HANG` | Revit: Hangs or Crashes Due to SQLLocalDB Issues When Working with Models and Steel Connections | https://www.autodesk.com/support/technical/article/caas/sfdcarticles/sfdcarticles/Revit-Journal-records-SQLLocalDB-instance-is-malfunctioning.html |
| `ART_LOCALDB_INVESTIGATE` | Investigating SQL Server LocalDB installation when working with Advance Steel / Revit | https://www.autodesk.com/support/technical/article/caas/sfdcarticles/sfdcarticles/Advance-Steel-is-not-able-to-start-Failed-to-connect-to-all-AS-databases.html |
| `DL_LOCALDB_2014` | Microsoft SQL Server 2014 Express with SP3 SqlLocalDB (msi 直链) | https://download.microsoft.com/download/3/9/F/39F968FA-DEBB-4960-8F9E-0E7BB3035959/ENU/x64/SqlLocalDB.msi |
| `DL_LOCALDB_2019` | Microsoft SQL Server 2019 Express (下载页) | https://www.microsoft.com/en-us/download/details.aspx?id=101064 |
| `DL_SECTOR_REG` | Autodesk SetSectorSize.reg | https://help.autodesk.com/sfdcarticles/attachments/SetSectorSize.reg |

所有链接集中在 `Core/Config/Links.cs`，只改一处。

---

## 2. 技术栈与项目结构

- 语言/框架：**C# 7.3+，.NET Framework 4.8**（客户机零运行时依赖）
- UI：**WinForms**，单 exe（`ILRepack` 或 `Costura.Fody` 合并类库；也可先不合并，二期处理）
- 类库与 UI 严格分离，类库不引用 `System.Windows.Forms`
- 单元测试：MSTest 或 xUnit，重点覆盖**所有文本解析器**（注册表/命令输出/fsutil），用固定样例字符串测试，不依赖真实环境
- 语言：zh-CN + en-US 资源文件，默认跟随系统，可在设置中切换
- 日志：`NLog` 或自写简单文件日志，写到 `%LOCALAPPDATA%\RevitLocalDbFix\logs\`

```
RevitLocalDbFix.sln
├─ src/
│  ├─ RevitLocalDbFix.Core/            # 类库
│  │  ├─ Config/       Links.cs, Paths.cs, ProductProfiles.cs
│  │  ├─ Revit/        RevitInstallation.cs, RevitInstallationLocator.cs, JournalScanner.cs
│  │  ├─ Isolation/    SteelConnectionsIsolator.cs
│  │  ├─ SqlLocalDb/   SqlLocalDbClient.cs, SqlLocalDbInstanceInfo.cs, SqlLocalDbOutputParser.cs, LocalDbInstallationInspector.cs, LocalDbConnectivityTester.cs
│  │  ├─ Sector/       SectorInfoChecker.cs, SectorRegistryFix.cs
│  │  ├─ Reinstall/    LocalDbUninstaller.cs, TempCleaner.cs, LocalDbFolderArchiver.cs, LocalDbDownloader.cs, LocalDbInstaller.cs
│  │  ├─ Process/      CommandRunner.cs, CommandResult.cs
│  │  ├─ Workflow/     StepId.cs, StepResult.cs, Verdict.cs, WizardSession.cs, SessionStateStore.cs
│  │  ├─ Report/       ReportBuilder.cs (Markdown + JSON)
│  │  └─ Elevation/    ElevationHelper.cs
│  └─ RevitLocalDbFix.App/             # WinForms
│     ├─ MainForm.cs                    # 向导壳：左侧步骤导航 + 右侧页面容器
│     ├─ Pages/                          # 每个步骤一个 UserControl，见 §6
│     ├─ Controls/StepPageLayout.cs      # 统一页面布局控件
│     └─ Resources/ Strings.zh-CN.resx, Strings.en-US.resx
├─ tests/RevitLocalDbFix.Core.Tests/
│  └─ Samples/                            # 命令输出样例文本
└─ docs/  SPEC.md (本文), IMPLEMENTATION_NOTES.md
```

---

## 3. 核心数据表（判定依据，必须完全一致）

### 3.1 Revit 版本 → LocalDB 版本 → 实例名

| Revit 年份 | 需要的 LocalDB | 引擎主版本 | 合法完整版本号 | 实例名 | 二进制目录 | SqlLocalDB.exe |
|---|---|---|---|---|---|---|
| 2018–2020 | SQL Server 2014 Express LocalDB (SP1/SP2/SP3) | 12.x | 12.0.4100.1 (SP1), 12.0.5000.0 (SP2), 12.0.6024.0 (SP3) | `MSSQLLocalDB` | `C:\Program Files\Microsoft SQL Server\120\LocalDB\Binn\` | `C:\Program Files\Microsoft SQL Server\120\Tools\Binn\SqlLocalDB.exe` |
| 2021–2023 | 同上 | 12.x | 同上 | `SteelConnections<year>`（如 `SteelConnections2022`） | 同上 | 同上 |
| 2024+ | SQL Server 2019 Express LocalDB | 15.x | 15.0.2104.1 | **`SteelConnections<year>v15`**（如 `SteelConnections2024v15`） | `...\150\LocalDB\Binn\` | `...\150\Tools\Binn\SqlLocalDB.exe` |

- Advance Steel 2021+ 实例名为 `AdvanceSteel<year>`；本工具一期只做 Revit，但 `ProductProfiles` 要预留 AS 条目。
- 版本判定规则：**按主版本 12 / 15 匹配判绿**；完整版本号显示给用户，不在合法列表内时判黄（Warn）而非红。
- **重要**：数据库一旦被更高版本的 LocalDB 实例附加过会被自动升级，且**不可回退**（原文警告）。因此工具创建实例时**必须**带 `-v <主版本>`。

### 3.2 注册表与路径

| 用途 | 位置 |
|---|---|
| Revit 安装位置 | `HKLM\SOFTWARE\Autodesk\Revit\<year>\REVIT-05:<lcid>` 的 `InstallationLocation`（如 `C:\Program Files\Autodesk\Revit 2024\`）`[待实测: 值名确认]`；兜底：`HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*` 中 `DisplayName` 匹配 `^Revit <year>$` 的 `InstallLocation`；最终以 `<路径>\Revit.exe` 存在为准 |
| SteelConnections 插件目录 | `<InstallationLocation>\AddIns\SteelConnections\`（**必须用注册表读到的实际路径，不得假设默认路径**） |
| Revit Journal | `%LOCALAPPDATA%\Autodesk\Revit\Autodesk Revit <year>\Journals\journal.*.txt` |
| 钢连接数据库文件（只读检查，信息性） | `%ProgramData%\Autodesk\Revit Steel Connections <year>\<lang>\` |
| LocalDB 已安装版本 | `HKLM\SOFTWARE\Microsoft\Microsoft SQL Server Local DB\Installed Versions\12.0` / `15.0` |
| LocalDB 卸载项 | `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{ProductCode}`，`DisplayName` 匹配正则 `Microsoft SQL Server 20(14\|19).*LocalDB`（同时扫 `WOW6432Node`） |
| LocalDB 用户实例目录 | `%LOCALAPPDATA%\Microsoft\Microsoft SQL Server Local DB\`，实例：`...\Instances\<实例名>\`，日志：`...\error.log` |
| 扇区修复注册表 | `HKLM\SYSTEM\CurrentControlSet\Services\stornvme\Parameters\Device`，值 `ForcedPhysicalSectorSizeInBytes` (REG_MULTI_SZ) = `* 4095` `[待实测: 下载 DL_SECTOR_REG 核对内容，以其为准]` |
| 工具自身数据 | `%LOCALAPPDATA%\RevitLocalDbFix\` 下 `state.json`、`backups\`、`downloads\`、`logs\`、`reports\` |

### 3.3 命令与原文预期输出（用于界面"原文预期"栏与判定）

```
> sqllocaldb i
AdvanceSteel2021
MSSQLLocalDB
SteelConnections2021
SteelConnections2022
SteelConnections2023
SteelConnections2024v15

> sqllocaldb i SteelConnections2022
Name:               SteelConnections2022
Version:            12.0.4100.1
Shared name:
Owner:              DOMAIN\user
Auto-create:        Yes
State:              Stopped
Last start time:    23/10/2018 20:39:51
Instance pipe name:

> sqllocaldb start SteelConnections2022
LocalDB instance "SteelConnections2022" started.

> sqllocaldb stop SteelConnections2022
LocalDB instance "SteelConnections2022" stopped.

> sqllocaldb stop SteelConnections2022 -k        (stop 失败时强制)

> sqllocaldb delete SteelConnections2022
LocalDB instance "SteelConnections2022" deleted.

> sqllocaldb create SteelConnections2022 -v 12.0   (原文无 -v，见 §5.4 说明)
LocalDB instance "SteelConnections2022" created with version 12.0.4100.1.

> fsutil fsinfo sectorinfo C:
...
PhysicalBytesPerSectorForAtomicity : 4096
PhysicalBytesPerSectorForPerformance : 4096
...
```

---

## 4. 权限模型

- exe 清单 `requestedExecutionLevel = asInvoker`。
- 第 0 步和所有**只读检测**在非管理员下可运行（便于远程让用户先跑检测发回报告）。
- 进入第 1 步（改 Program Files）或任何修复动作前，`ElevationHelper.EnsureElevated()`：未提升则用 `runas` 重新启动自身并带 `--resume`，当前进程退出。会话状态已持久化，新进程从状态恢复。
- 需要重启的步骤（扇区注册表修复、卸载/安装要求重启时）：写 `HKLM\...\RunOnce\RevitLocalDbFix = "<exe>" --resume`，提示用户重启；重启后自动续跑到下一步。

---

## 5. 流程详细设计

流程总览：

```
第0步 选版本 → 第1步 隔离试验(备份+摘除) → 用户试Revit → 反馈
   ├─ 正常 & 不需要钢连接 → 结束(保留备份, 提供还原入口)
   ├─ 正常 & 需要钢连接   → 第2步
   └─ 没变化             → 自动还原 → 结束(报告: 非LocalDB问题)
第2步 逐步确认向导:
   2.0 扇区(Win11) → 2.1 实例列表 → 2.2 实例详情 → 2.3 启停测试
   → (失败) 2.4 删除重建 → (仍失败) 2.5 清洁重装(6子步) → 回2.1
   → (2.3通过) 2.6 真连接验证 → 第3步 还原SteelConnections → 第4步 最终验证 → 报告
```

### 5.0 第 0 步：选择版本
- `RevitInstallationLocator.Enumerate()` 返回所有已安装 Revit（年份、产品名、安装路径、Revit.exe 版本号、SteelConnections 目录是否存在）。
- 列表展示；未检测到任何 Revit 时允许手动指定 `Revit.exe`。
- 选定后 `WizardSession` 固化：`Year`、`InstallPath`、`SteelConnectionsPath`、`Profile`（来自 §3.1：LocalDB 主版本、实例名、SqlLocalDB.exe 路径、二进制目录、下载链接 ID）。
- 同页显示系统信息：Windows 版本/build（用于判断 Win11）、是否管理员、当前用户。
- 附加只读扫描：最近 20 个 journal 中是否命中 `SQLLocalDB instance is malfunctioning`，命中则显示文件名与行号（辅助信息，不阻断）。

### 5.1 第 1 步：隔离试验（备份 + 摘除）

**前置检查（任一失败则阻断并说明）**
1. 无 `Revit.exe` 进程运行；
2. 已提升权限；
3. `SteelConnectionsPath` 存在；若不存在，判定"该版本未安装钢连接模块或已被摘除"，检查是否存在 `SteelConnections_disabled_*`，有则提供还原入口，否则跳过第 1 步直接进第 2 步（并在报告中注明）；
4. 备份目标盘剩余空间 ≥ 文件夹大小 × 1.2。

**执行**
1. `SteelConnectionsIsolator.Backup()`：用 `System.IO.Compression.ZipFile` 将整个目录压缩到 `%LOCALAPPDATA%\RevitLocalDbFix\backups\SteelConnections_<year>_<yyyyMMdd_HHmmss>.zip`；完成后打开 zip 校验条目数 == 源文件数，记录总大小与 SHA256；
2. `SteelConnectionsIsolator.Disable()`：将目录重命名为 `SteelConnections_disabled_<yyyyMMdd_HHmmss>`（**不删除**，zip 为双保险）；
3. `%ProgramData%\Autodesk\Revit\Addins\<year>\` 中**没有** SteelConnections 清单文件，无需处理（已确认）；
4. 页面显示：备份文件路径、大小、校验结果、重命名后的目录名；
5. 状态持久化为 `AwaitingIsolationFeedback`。

**用户反馈页**
- 文字提示：请启动 Revit 并重现之前出问题的操作（打开该模型 / 同步 / 加载样板 / 导入 IFC）。
- 三个按钮：
  - **正常了，我不需要钢连接功能** → 结束页：说明后果（Structure/Steel 选项卡的连接功能不可用；Revit 更新可能重新装回该目录，届时重新运行本工具），保留备份，提供"一键还原"按钮；生成报告。
  - **正常了，但我需要钢连接功能** → 记录"病因确认：LocalDB"，进入第 2 步。
  - **没有变化** → 自动 `Restore()`（重命名回 `SteelConnections`，校验），结束页说明"问题与 LocalDB 无关"，生成报告。
- 辅助判定（不替代按钮）：扫描第 1 步之后新生成的 journal，若仍出现 `SQLLocalDB instance is malfunctioning` 则在页面上黄色提示"新 journal 中仍检测到该警告"。

**还原** `SteelConnectionsIsolator.Restore()`：优先重命名 `_disabled` 目录回原名；目录不存在时从 zip 解压；还原后校验文件数。

### 5.2 第 2 步：逐步确认向导

通用规则：
- 每页先显示**将要执行的完整命令**（含实际使用的 `SqlLocalDB.exe` 全路径），用户点"执行"才运行；
- 执行后左侧显示**原始 stdout+stderr**（等宽字体、可复制、可全选），右侧显示**原文预期**（§3.3 中的样例）+ **工具判定** + **依据文章链接**；
- 按钮：`执行` / `确认并继续` / `跳过此步` / `复制输出` / `上一步`；
- 跳过的步骤 `Verdict = Skipped`，报告中标"用户跳过"，不算通过；
- 每步 `StepResult` 写入会话状态并持久化。
- `SqlLocalDbClient` **只调用 `Profile.SqlLocalDbExePath`**（120 或 150 下的 Tools\Binn），不依赖 PATH；该 exe 不存在时 2.1–2.4 直接标红并引导到 2.5。

#### 2.0 磁盘扇区检查（依据 `ART_REVIT_HANG`）
- Win11 上为必做；Win10 上执行但标注"参考"。
- 命令：`fsutil fsinfo sectorinfo <系统盘>`（需要管理员）。
- 解析 `PhysicalBytesPerSectorForAtomicity`、`PhysicalBytesPerSectorForPerformance`，两行高亮。
- 判定：任一 > 4096 且注册表补丁未应用 → Fail；已应用但未重启 → Warn（提示重启）；否则 Pass。
- Fail 时的修复动作页：显示将写入的注册表键值（与 `DL_SECTOR_REG` 内容一致），用户确认后写入，登记 RunOnce，提示重启；重启续跑后重新执行 2.0。

#### 2.1 实例列表（依据 `ART_LOCALDB_INVESTIGATE`）
- 命令：`sqllocaldb i`
- 显示全部实例名，目标实例（`Profile.InstanceName`）高亮。
- 同时显示 `sqllocaldb versions` 输出作为补充信息。
- 判定：目标实例存在 → Pass；不存在 → Fail，引导到 2.4（走"创建"分支）。

#### 2.2 实例详情
- 命令：`sqllocaldb i <InstanceName>`
- `SqlLocalDbOutputParser.ParseInstanceInfo()` 解析为 `SqlLocalDbInstanceInfo { Name, Version, SharedName, Owner, AutoCreate, State, LastStartTime, InstancePipeName }`，以表格显示；右侧并列原文示例。
- 判定：
  - 输出无法解析成上述格式 → Fail：**"LocalDB 未正确安装"** → 引导 2.5；
  - 主版本 ≠ `Profile.MajorVersion` → Fail → 引导 2.4（重建为正确版本）；
  - 版本号不在合法列表 → Warn；
  - 其余 → Pass。

#### 2.3 启停测试
- 命令 1：`sqllocaldb start <InstanceName>` 预期 `LocalDB instance "<name>" started.`
- 命令 2：`sqllocaldb stop <InstanceName>` 预期 `LocalDB instance "<name>" stopped.`
- 两条都匹配 → Pass → 进 2.6；否则 Fail，显示实例目录 `error.log` 末尾 40 行，引导 2.4。

#### 2.4 删除并重建实例
- 显示三条命令，用户确认后顺序执行：
  1. `sqllocaldb stop <name>`；若返回错误，自动追加执行 `sqllocaldb stop <name> -k` 并显示；
  2. `sqllocaldb delete <name>` 预期 `... deleted.`（实例本不存在时跳过 1、2，直接 3）；
  3. `sqllocaldb create <name> -v <Profile.MajorVersion>` 预期 `... created with version <x>.`
- **`-v` 差异说明**（页面上必须显示）：原文命令为 `sqllocaldb create <name>`（无 `-v`）。不带 `-v` 时 LocalDB 会使用机器上安装的**最高版本**创建实例；若机器上还装有 Visual Studio 附带的更高版本 LocalDB，实例会被建成错误版本，钢连接数据库随即被升级且不可回退。因此本工具显式指定版本。
- 执行前自动备份 `...\Instances\<name>\` 目录到 `backups\Instance_<name>_<时间戳>.zip`（若存在）。
- 完成后自动回 2.3 复测；复测仍失败 → 引导 2.5。

#### 2.5 清洁重装（六子步，各一页各确认；依据 `ART_LOCALDB_INVESTIGATE`）

前置：无 `Revit.exe`；结束所有 LocalDB 引擎进程（`sqlservr.exe` 且路径在 `Microsoft SQL Server\1x0\LocalDB\Binn`），先 `sqllocaldb stop <所有实例> -k`。

| 子步 | 原文 | 工具动作 | 页面显示 | 验证 |
|---|---|---|---|---|
| 2.5.1 卸载 | Control Panel 卸载 `Microsoft SQL Server 201x Express LocalDB` | 从 §3.2 卸载项取 ProductCode，`msiexec /x {ProductCode} /qn /norestart /l*v <log>`；**只卸载与 Profile 对应的年份版本**；未找到 → 显示"未安装（与原文注释一致）"并跳过 | 找到的卸载项、命令、退出码、日志路径 | 卸载项消失 |
| 2.5.2 清 %temp% | 清空 `%temp%` | 遍历删除；被占用/拒绝访问的跳过并计数（对应原文"弹窗则跳过"） | 删除数 / 跳过数 / 释放空间 | — |
| 2.5.3 用户目录 `_OLD` | `%localappdata%\Microsoft\Microsoft SQL Server Local DB` → `_OLD` | 存在则重命名；`_OLD` 已存在则用 `_OLD_<时间戳>` | **醒目警告**：此操作会作废当前用户的全部 LocalDB 实例（含 Visual Studio 等其他程序创建的） | 目录已改名 |
| 2.5.4 程序目录 `_OLD` | `C:\Program Files\Microsoft SQL Server\120\LocalDB`（2014）、`...\150\LocalDB`（2019）→ `_OLD` | 两个目录都检查并改名（原文要求两个都处理）；被锁时提示先重启再续跑 | 各目录处理结果 | 目录已改名 |
| 2.5.5 下载 | 2014：直链下载 `SqlLocalDB.msi`；2019：下载页 → `SQL2019-SSEI-Expr.exe` → Download Media → LocalDB | 2014：`LocalDbDownloader` 直接下载 `DL_LOCALDB_2014` 到 `downloads\`，显示大小、校验 Authenticode 签名为 Microsoft；2019：下载 SSEI 引导程序 `[待实测: 直链 https://go.microsoft.com/fwlink/?linkid=866658]`，然后静默拉取媒体 `SQL2019-SSEI-Expr.exe /ACTION=Download /MEDIATYPE=LocalDB /MEDIAPATH=<downloads> /QUIET` `[待实测: 参数与产物文件名]`；**两条路都提供"我已有 msi，手动指定"入口**和可点击的原文下载链接作为兜底 | 进度、文件路径、大小、签名结果 | msi 存在且签名有效 |
| 2.5.6 安装 | 运行下载的文件 | `msiexec /i "<msi>" IACCEPTSQLLOCALDBLICENSETERMS=YES /qn /norestart /l*v <log>`；退出码 3010 = 需重启 → 登记 RunOnce 并提示 | 命令、退出码、日志路径 | `sqllocaldb versions` 出现对应版本；注册表 `Installed Versions\1x.0` 存在；`sqlservr.exe` 存在 |

2.5 完成后自动回 2.1 从头走一遍。

#### 2.6 真连接验证（工具补充，非原文）
- `LocalDbConnectivityTester`：`System.Data.SqlClient`，连接串 `Server=(localdb)\<InstanceName>;Integrated Security=True;Connection Timeout=30`，执行 `SELECT @@VERSION`。
- 显示返回的版本字符串；失败显示异常与 `error.log` 末尾。
- Pass → 第 3 步。

### 5.3 第 3 步：还原 SteelConnections
- 若第 1 步摘除过：`Restore()`，显示结果与校验；未摘除过则跳过。

### 5.4 第 4 步：最终验证
- 自动依次重跑 2.1、2.2、2.3、2.6（只读/无损），汇总为一张红绿表。
- 提示用户再次启动 Revit 验证，按钮：`已解决` / `仍有问题`（记录到报告，仍有问题时提示联系 Autodesk 支持并附报告）。
- 生成报告（§8），提供"打开报告目录"和"复制报告内容"。

---

## 6. UI 规范

### 6.1 向导壳 `MainForm`
- 左侧：步骤导航列表（第 0 步 … 第 4 步，第 2 步展开显示 2.0–2.6），当前步高亮，已完成显示 ✓/✗/跳过 图标。
- 顶部：所选 Revit 版本、实例名、LocalDB 目标版本、是否管理员（未提升显示"以管理员身份重新启动"按钮）。
- 底部状态栏：会话 ID、日志路径。
- 菜单：`文件`（导出报告、打开备份目录、还原 SteelConnections）、`语言`、`帮助`（两篇原文链接、关于）。

### 6.2 统一页面布局 `StepPageLayout`
```
┌ 标题：2.2 实例详情                         依据：[Investigating SQL Server LocalDB…] (可点击) ┐
│ 将执行的命令：                                                                             │
│   "C:\Program Files\Microsoft SQL Server\120\Tools\Binn\SqlLocalDB.exe" i SteelConnections2022 │
├───────────────────────────────┬────────────────────────────────────────────────────────┤
│ 原始输出 (等宽, 可复制)          │ 原文预期 (等宽)                                          │
│                               │ 工具判定: ● Pass / ● Warn / ● Fail  + 一句话原因            │
│                               │ 说明/警告 (如 -v 差异、不可回退警告)                          │
├───────────────────────────────┴────────────────────────────────────────────────────────┤
│ [上一步]   [执行]   [复制输出]   [跳过此步]                              [确认并继续 →]   │
└────────────────────────────────────────────────────────────────────────────────────────┘
```
- 链接用 `LinkLabel`，点击 `Process.Start(url)`。
- 破坏性动作（重命名 `_OLD`、卸载、写注册表、删除实例）在"执行"前弹二次确认框，列出将改动的具体路径/键。
- 判定颜色：Pass 绿、Warn 黄、Fail 红、Skipped 灰、NotRun 无。

### 6.3 首页/关于
- 工具说明、两篇原文完整标题与链接、版本号、免责声明（操作前已自动备份，但请确保重要数据已另行备份）。

---

## 7. 类库 API 设计（关键签名）

```csharp
// Process
public sealed class CommandResult { string ExePath; string Arguments; int ExitCode; string StdOut; string StdErr; TimeSpan Duration; DateTime StartedAt; string CombinedOutput {get;} }
public interface ICommandRunner { CommandResult Run(string exePath, string arguments, TimeSpan? timeout = null); }

// Config
public sealed class ProductProfile { int Year; string InstanceName; string LocalDbMajorVersion /*"12.0"|"15.0"*/; string[] KnownFullVersions; string SqlLocalDbExePath; string EngineBinnPath; string ProgramFilesLocalDbFolder /*...\120\LocalDB*/; string DownloadLinkId; string UninstallDisplayNameRegex; }
public static class ProductProfiles { ProductProfile ForRevit(int year); }
public static class Links { const string ART_REVIT_HANG, ART_LOCALDB_INVESTIGATE, DL_LOCALDB_2014, DL_LOCALDB_2019, DL_SECTOR_REG; }

// Revit
public sealed class RevitInstallation { int Year; string ProductName; string InstallPath; string ExePath; string ExeVersion; string SteelConnectionsPath; bool SteelConnectionsExists; string DisabledSteelConnectionsPath /*null if none*/; }
public sealed class RevitInstallationLocator { IReadOnlyList<RevitInstallation> Enumerate(); RevitInstallation FromExePath(string exePath); }
public sealed class JournalScanner { IReadOnlyList<JournalHit> FindMalfunctionWarnings(int year, int maxFiles = 20, DateTime? newerThan = null); }

// Isolation
public sealed class SteelConnectionsIsolator {
  BackupResult Backup(RevitInstallation r, string backupDir);          // zip + 校验
  DisableResult Disable(RevitInstallation r);                          // rename -> _disabled_<ts>
  RestoreResult Restore(RevitInstallation r, string zipFallback);      // rename back, else unzip
  PreflightResult Preflight(RevitInstallation r, string backupDir);    // 进程/权限/空间/存在性
}

// SqlLocalDb
public sealed class SqlLocalDbClient {
  SqlLocalDbClient(string exePath, ICommandRunner runner);
  bool ExeExists {get;}
  CommandResult Versions();
  CommandResult ListInstances();
  CommandResult Info(string instance);
  CommandResult Start(string instance);
  CommandResult Stop(string instance, bool kill = false);
  CommandResult Delete(string instance);
  CommandResult Create(string instance, string majorVersion);   // 必须带 -v
}
public static class SqlLocalDbOutputParser {
  IReadOnlyList<string> ParseInstanceList(string output);
  bool TryParseInstanceInfo(string output, out SqlLocalDbInstanceInfo info);
  bool IsStartedMessage(string output, string instance);   // LocalDB instance "X" started.
  bool IsStoppedMessage(...); bool IsDeletedMessage(...); bool TryParseCreatedVersion(string output, string instance, out string version);
}
public sealed class LocalDbInstallationInspector { LocalDbInstallState Inspect(ProductProfile p); } // 注册表 Installed Versions、sqlservr.exe、SqlLocalDB.exe、卸载项、其他共存版本
public sealed class LocalDbConnectivityTester { ConnectivityResult Test(string instanceName, TimeSpan timeout); }
public static class LocalDbInstancePaths { string InstanceDir(string name); string ErrorLog(string name); string[] TailErrorLog(string name, int lines); }

// Sector
public sealed class SectorInfoChecker { SectorInfoResult Check(string driveLetter); } // 运行 fsutil, 解析两值, 读注册表补丁状态
public sealed class SectorRegistryFix { void Apply(); bool IsApplied(); string DescribeChange(); }

// Reinstall
public sealed class LocalDbUninstaller { UninstallEntry Find(ProductProfile p); CommandResult Uninstall(UninstallEntry e, string logPath); }
public sealed class TempCleaner { TempCleanResult Clean(); }
public sealed class LocalDbFolderArchiver { ArchiveResult RenameToOld(string path); } // _OLD / _OLD_<ts>
public sealed class LocalDbDownloader { Task<DownloadResult> Download2014(string dir, IProgress<double> p); Task<DownloadResult> Download2019(string dir, IProgress<double> p); bool VerifyMicrosoftSignature(string file); }
public sealed class LocalDbInstaller { CommandResult Install(string msiPath, string logPath); /* 3010 => RebootRequired */ }

// Workflow
public enum Verdict { NotRun, Pass, Warn, Fail, Skipped }
public sealed class StepResult { StepId Id; Verdict Verdict; string Command; string RawOutput; string Expected; string Reason; string ArticleLinkId; DateTime At; IDictionary<string,string> Data; }
public sealed class WizardSession { Guid Id; RevitInstallation Revit; ProductProfile Profile; WizardState State; List<StepResult> Steps; string BackupZipPath; string DisabledFolderPath; bool IsolationConfirmedCause; bool UserNeedsSteelConnections; }
public sealed class SessionStateStore { WizardSession Load(); void Save(WizardSession s); void Clear(); } // %LOCALAPPDATA%\RevitLocalDbFix\state.json
public static class ElevationHelper { bool IsElevated(); void RelaunchElevated(string args); void RegisterRunOnceResume(); }

// Report
public sealed class ReportBuilder { string BuildMarkdown(WizardSession s); string BuildJson(WizardSession s); void Save(WizardSession s, string dir); }
```

---

## 8. 报告格式

保存到 `%LOCALAPPDATA%\RevitLocalDbFix\reports\Report_<year>_<yyyyMMdd_HHmmss>.md` 与 `.json`。

Markdown 结构（每节对应一步，与文章结构一一对应，便于贴进 Autodesk 支持案例）：

```
# RevitLocalDbFix 报告
- 生成时间 / 工具版本 / 会话 ID
- 系统：Windows 版本、build、是否管理员、用户名（可选脱敏）
- Revit：年份、安装路径、Revit.exe 版本
- 目标：LocalDB 主版本、实例名
- 依据：[ART_REVIT_HANG](url) / [ART_LOCALDB_INVESTIGATE](url)

## 第 0 步 …
## 第 1 步 隔离试验 — 判定 / 备份路径 / 用户反馈
## 2.0 … 2.6  每节：依据链接 / 命令 / 原始输出(代码块) / 原文预期 / 判定 / 用户操作(执行|跳过)
## 第 3 步 还原
## 第 4 步 最终验证汇总表
## 结论 与 建议下一步
```

---

## 9. 安全边界与硬规则（实现时不得违反）

1. **永远不删除**：SteelConnections 目录、LocalDB 实例目录、`.mdf/.ldf`、`120|150\LocalDB` 目录一律**只重命名或压缩备份**。唯一删除操作是 `%temp%` 清理和 `sqllocaldb delete`（后者先备份实例目录）。
2. **只碰目标**：只操作 `Profile.InstanceName` 对应的实例；只卸载与目标年份对应的 LocalDB 版本。2.5.3 会影响其他实例，因此必须醒目警告并二次确认。
3. **`sqllocaldb create` 必须带 `-v`**。
4. **Revit 运行中拒绝任何修复动作**（第 1 步、2.4、2.5、第 3 步）。
5. **只读检测不写任何东西**（第 0 步、2.0 的检测部分、2.1–2.3、2.6、第 4 步）。
6. **调用固定路径的 `SqlLocalDB.exe`**，不用 PATH。
7. 破坏性动作前二次确认，并把将改动的具体路径/键列出。
8. 跳过 ≠ 通过。
9. 所有命令、输出、判定、用户操作进日志与报告。
10. 网络下载失败不阻断：提供手动指定 msi 与原文链接兜底。

---

## 10. 状态机

```
Idle → VersionSelected → IsolationPreflight → IsolationDone → AwaitingIsolationFeedback
  → (NoSteelNeeded) Finished
  → (NoChange) Restoring → Finished
  → (CauseConfirmed) Step2_Sector → Step2_List → Step2_Info → Step2_StartStop
       → Step2_Recreate → Step2_StartStop
       → Step2_Reinstall_1..6 → Step2_List
       → Step2_Connect → Restoring → FinalVerify → Finished
任何状态 ↔ AwaitingReboot (RunOnce --resume 后回到记录的下一状态)
```
`state.json` 记录：当前状态、下一状态、所有 StepResult、备份路径、`_disabled` 路径、用户选择。启动时若存在未完成会话，询问"继续上次会话 / 放弃并还原 / 放弃"。

---

## 11. 测试与验收

### 11.1 单元测试（必须）
- `SqlLocalDbOutputParser`：§3.3 全部样例 + 空输出 + 英文/中文系统的错误输出 + 带 BOM/CRLF；
- `SectorInfoChecker` 解析：Win10/Win11 的 fsutil 样例、缺行、数值 512/4096/8192；
- `RevitInstallationLocator`：注册表读取逻辑抽象为接口，用假数据测试；
- `ReportBuilder`：给定会话输出的 Markdown 含所有链接与代码块。

### 11.2 手工验收清单
- [ ] 第 0 步能列出多版本 Revit，路径来自注册表（非默认路径的安装也能定位）
- [ ] 第 1 步 zip 备份可打开、条目数正确；目录已改名；Revit 启动无插件加载错误
- [ ] 三个反馈按钮各自流程正确；"没有变化"能自动还原
- [ ] 2.x 每页显示命令、原始输出、原文预期、判定、链接；跳过标记正确
- [ ] 2.4 生成的实例版本与 Revit 年份匹配（用 `sqllocaldb i <name>` 复核）
- [ ] 2.5 六子步在"未安装 LocalDB"和"已安装错误版本"两种机器上都能走通
- [ ] 需要重启的路径能在重启后自动续跑
- [ ] 非管理员启动时只读检测可用，进入修复时正确提升
- [ ] 报告可直接贴进支持案例，含所有链接
- [ ] zh-CN / en-US 切换完整

---

## 12. 里程碑

| M | 内容 |
|---|---|
| M1 | Core：Config/Revit/Process/SqlLocalDb 解析器 + 单元测试；App：第 0 步页 + 向导壳 + StepPageLayout |
| M2 | 第 1 步（备份/摘除/反馈/还原）+ 状态持久化 + 提升 |
| M3 | 2.0–2.4、2.6、第 3/4 步 + 报告 |
| M4 | 2.5 六子步（含下载与 `[待实测]` 项验证）+ RunOnce 续跑 |
| M5 | 双语、单 exe 打包、验收清单走完 |

---

## 13. `[待实测]` 汇总（实施时优先验证并记录到 IMPLEMENTATION_NOTES.md）

1. Revit 安装位置注册表值名（`InstallationLocation`）与键结构在 2018–2026 各版本是否一致；
2. `SetSectorSize.reg` 的实际内容（下载 `DL_SECTOR_REG` 核对键、值名、数据）；
3. SQL 2019 SSEI 引导程序直链与静默下载参数、产物文件名；
4. `msiexec /x` 卸载 LocalDB 2014/2019 的退出码与是否要求重启；
5. `System.Data.SqlClient` 在仅安装 LocalDB 2019 的机器上连接 `(localdb)\...v15` 是否需要额外驱动；
6. 2.5.4 改名 `Program Files\...\LocalDB` 在何种情况下被锁（服务/进程），是否需要先重启。
