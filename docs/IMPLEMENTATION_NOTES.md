# IMPLEMENTATION_NOTES

实施过程中的结论与决策记录(SPEC 要求:`[待实测]` 项先实测再定,并记录于此)。

## 2026-10-01 — 基础架构搭建(M1 部分)

### 环境
- 构建机:Windows 10 Pro 19045,.NET SDK 9.0.313,.NET Framework 4.8 参考程序集已安装。
- SDK 风格 csproj,目标 `net48`,`dotnet build` / `dotnet test` 可用。

### 已确认的决策(与 SPEC 的差异均在此登记)

1. **UI 框架:WinForms → WPF**(需求方 2026-10-01 拍板,浅色现代主题)。
   - 仍为 .NET Framework 4.8、单 exe、客户机零运行时依赖;Core 类库与 UI 严格分离不变。
   - SPEC §2/§6 中控件名(如 `StepPageLayout`)对应实现为 WPF UserControl。
2. **`sqllocaldb create` 的版本参数是位置参数,不是 `-v` 开关**。
   - 实际 CLI 语法:`SqlLocalDB.exe create "<name>" <version>`(例如 `create "SteelConnections2022" 12.0`),sqllocaldb 并没有 `-v` 选项;SPEC §3.3/§5.2-2.4 写作 `-v` 是笔误,其意图(必须显式锁定版本,防止被更高版本引擎创建导致数据库不可回退升级)保持不变,硬规则 §9-3 照常生效。
   - 实现:`SqlLocalDbClient.Create(instance, majorVersion)` 强制要求版本号,拼为位置参数。待真机验证输出文案(`created with version ...`)。
3. **双语资源:resx → WPF ResourceDictionary**(`Resources/Strings.en.xaml` / `Strings.zh.xaml`)。
   - 原因:配合 `DynamicResource` 可运行时整窗即时切换语言,无需重启;默认跟随系统(zh → 中文,否则英文),选择持久化到 `%LOCALAPPDATA%\RevitLocalDbFix\ui-language.txt`。
4. **state.json 序列化用 DataContractJsonSerializer**(内置),不引入 Newtonsoft.Json,保持"客户机零依赖"。
5. **新增 `Core/Infrastructure/`**(`IRegistryReader`/`IFileSystemProbe`/`FileLogger`):SPEC §11.1 要求注册表逻辑可用假数据单测,故抽象为接口;SPEC §2 目录清单未列出此文件夹,属实现层新增。
6. **状态机**:2.5 六子步用 `WizardSession.ReinstallSubStep`(int 1..6)+ 单一 `Step2_Reinstall` 状态表达,不为每个子步单设枚举值。
7. **命令输出编码**:`CommandRunner` 以 OEM 代码页(`GetOEMCP`)解码子进程输出,避免中文系统下 sqllocaldb/fsutil 输出乱码。

### `[待实测]` 清单状态(SPEC §13)

| # | 项 | 状态 |
|---|---|---|
| 1 | Revit 注册表值名 `InstallationLocation` 各版本一致性 | 未验证;实现含 Uninstall 兜底 + Revit.exe 存在性校验 |
| 2 | `SetSectorSize.reg` 实际内容 | 未验证(2026-10-01 需求方暂缓联网核查);当前按 SPEC `* 4095` 实现,常量集中在 `SectorInfoChecker` |
| 3 | SQL 2019 SSEI 直链与静默参数 | 未验证;`Download2019` 暂返回失败并引导手动指定 msi(符合 §9-10 兜底) |
| 4 | msiexec 卸载退出码/重启要求 | 未验证;3010 按需重启处理已实现 |
| 5 | System.Data.SqlClient 连 v15 | 未验证;`LocalDbConnectivityTester` 已按 SPEC 连接串实现 |
| 6 | Program Files LocalDB 目录何时被锁 | 未验证;`LocalDbFolderArchiver` 失败时报错并提示重启续跑 |

## 2026-10-01 — 真机只读检验(开发机,Win10 19045,中文系统,未提权)

用已构建的 Core 库在本机(健康机器,Revit 可正常使用)跑只读检测链路,**未执行任何修复动作**。机器装有 Revit 2021/2023/2024/2025/2026/2027(非默认路径 `D:\Autodesk\...`,其中 2026/2027 为 `D:\Autodesk\Revit\<year>\Revit <year>\` 嵌套布局)。

### 验证通过的部分

- 注册表定位器:6 个版本全部命中,非默认路径与嵌套布局均正确;`ProductName`/`InstallationLocation` 值名在 2021–2027 全部一致 → **§13-1 已验证**。
- Journal 扫描:6 个版本均 0 命中(健康机器,符合预期)。
- LocalDB 安装检查:`Installed Versions` 12.0/15.0 并存、SqlLocalDB.exe/sqlservr.exe 存在、卸载项定位正确。
- 卸载项 DisplayName 实测:2014 = `Microsoft SQL Server 2014 Express LocalDB`;2019 = `Microsoft SQL Server 2019 LocalDB`(**没有 "Express"**)——两个 per-year 正则均正确匹配且互不误伤。
- fsutil 未提权时优雅失败(中文错误文案"拒绝访问"),与 §4 非管理员只读路径设计一致。
- Revit.exe 运行中 → 2.3/2.6 测试被安全守卫跳过(§9-4 生效)。

### 真机发现的两个问题(已修复)

1. **12.0 版 SqlLocalDB.exe 的输出是本地化的**:中文系统输出 `名称:/版本:/状态: 已停止/自动创建: 否` 等中文标签,而同机的 15.0 版 exe 输出英文。原解析器按英文标签解析导致 2.2 误判 "LocalDB 未正确安装 → 引导 2.5 重装"(在健康机器上会引发不必要的重装!)。
   **修复**:`SqlLocalDbOutputParser` 增加 zh-CN 标签别名表(含全角冒号)、`是/否`、`正在运行/已停止`,started/stopped/deleted/created 消息同时匹配英文句式与中文动词(已启动/已停止/已删除/创建);真实中文输出已存为测试样例 `sqllocaldb_i_info_zh.txt`。注:zh 的 create 成功文案是推测格式,真机执行 create 时需再核对。
2. **实例名映射:2025+ 没有 v15 后缀**。SPEC §3.1 写 "2024+ → `SteelConnections<year>v15`",但本机实测:`SteelConnections2024v15`(有后缀)与 `SteelConnections2025/2026/2027`(无后缀,引擎 15.0.4382.1)。
   **修复**:`ProductProfiles` 改为仅 2024 带 v15 后缀,2025+ 用 `SteelConnections<year>`;待在第二台机器交叉验证。

### 其他实测数据

- 健康机器的 15.0 完整版本为 **15.0.4382.1**(CU 更新版),不在 SPEC 合法列表(15.0.2104.1)内,会被判 Warn → 已把 15.0.4382.1 加入 KnownFullVersions(主版本判绿规则不变)。
- 12.0 实例(2021/2023)版本 12.0.6024.0(SP3),在合法列表内 ✓。
- `sqllocaldb versions`(两个 exe 一致):`Microsoft SQL Server 2014 (12.0.6024.0)` / `Microsoft SQL Server 2019 (15.0.4382.1)`。
- 15.0 exe 可以查询 12.0 实例且输出英文(潜在的统一英文输出途径,暂不采用,仍按 §9-6 用对应版本 exe)。

## 2026-10-01 — 2.4 删除重建流程真机实测(仅 SteelConnections2021,需求方授权)

需求方授权对**一个版本**做真实删除+修复试验。目标选 SteelConnections2021(12.0 引擎,可同时验证中文输出)。守卫:Revit 2021 未运行(当时 Revit 2027 在运行,实例/引擎完全隔离)、目标实例处于"已停止"、删除前 zip 备份实例目录(备份保留在 `%LOCALAPPDATA%\RevitLocalDbFix\backups\Instance_SteelConnections2021_20261001_212315.zip`,3.4MB)。

**全流程成功**:stop → delete → `create "SteelConnections2021" 12.0` → info(12.0.6024.0,Pass)→ start → `SELECT @@VERSION`(94ms,SQL 2014 SP3)→ stop(恢复原"已停止"状态)。四个解析器对真实中文消息全部匹配。

### 捕获的真实中文消息(12.0 exe,zh-CN 系统;实例名用全角引号包裹)

| 命令 | 实测输出 | 退出码 |
|---|---|---|
| stop(对已停止实例) | `LocalDB 实例“X”已停止。` | **0(不是错误!)** |
| delete | `LocalDB 实例“X”已删除。` | 0 |
| create "X" 12.0 | `已使用版本 12.0.6024.0 创建 LocalDB 实例“X”。` | 0(耗时约 2.6s) |
| start | `LocalDB 实例“X”已启动。` | 0(约 1.2s) |
| stop(运行中实例) | `LocalDB 实例“X”已停止。` | 0(约 12s) |

### 结论

- **对已停止实例执行 stop 返回退出码 0**(输出"已停止"),不是错误 → 2.4 流程中"stop 失败才追加 -k"的分支只会在真正失败(如进程挂死)时触发,设计成立。
- `create` 位置参数语法 `create "<name>" 12.0` 实证可用,产出 12.0.6024.0(机器上安装的最高 12.0.x),**§13 新增项(create 语法)已验证**。
- 中文 create 消息格式与解析器实现完全一致,`TryParseCreatedVersion` 提取版本正确。
- **§13-5 部分验证**:System.Data.SqlClient 连接 `(localdb)\<12.0 实例>` 正常(94ms);v15 实例的连接验证待 Revit 关闭后补测。
- stop 一个运行中的实例耗时约 12 秒 → UI 执行按钮需要异步 + 忙状态指示(M3 实现注意)。

## 2026-10-02/03 — 2.5 清洁重装关键子步真机实测(仅 SQL 2014 引擎,需求方授权)

需求方授权"卸载一个版本的 SQL 实例引擎后重新修复"。目标选 **SQL Server 2014 Express LocalDB(12.0)**:正在运行的 Revit 2027 依赖 15.0 引擎,完全隔离;12.0 只服务已关闭的 Revit 2021/2023。实测覆盖 2.5.1(卸载)、2.5.5(下载)、2.5.6(安装)三个子步 + 完整验证;2.5.2–2.5.4(清 temp、目录改名 _OLD)未执行——它们会波及 15.0 实例,超出本次"只动一个版本"的授权范围。

**安全顺序(重要设计确认)**:先下载安装包并验证签名,**之后**才允许卸载,杜绝"卸载后装不回"的死局;受影响实例目录(2021/2023)先行 zip 备份。

### 实测数据(全部成功,§13-4 已验证)

| 子步 | 实测 |
|---|---|
| 2.5.5 下载 | 官方直链 `DL_LOCALDB_2014` 有效:45,678,592 字节,约 31s;`Get-AuthenticodeSignature` = Valid,CN=Microsoft Corporation |
| 2.5.1 卸载 | `msiexec /x {BAF67399-85CD-4555-9B49-1F80EB921C35} /qn /norestart /l*v` → **退出码 0,11 秒,无需重启**;sqlservr.exe / SqlLocalDB.exe / `Installed Versions\12.0` 键全部消失 |
| 2.5.6 安装 | `msiexec /i ... IACCEPTSQLLOCALDBLICENSETERMS=YES /qn /norestart /l*v` → **退出码 0,13 秒,无需重启**;文件与注册表全部恢复 |
| 验证 | `versions` 恢复双引擎;卸载项恢复(同 ProductCode);两实例 info→start→`SELECT @@VERSION`→stop 全链路通过,解析器全部匹配 |

### 关键发现

1. **引擎卸载/重装不碰用户实例**:`Instances\` 目录在卸载时原样保留(SteelConnections2021 的"上次启动时间"仍是前日 2.4 试验的时间戳),重装引擎后实例直接可用。含义:当故障根因在引擎本体时,"卸载+重装"即可修复而不丢实例;2.5.3(用户目录改名 _OLD)只在实例自身损坏时才有必要——工具 UI 应把这层区别讲给用户。
2. 卸载项 DisplayVersion 为 **12.3.6024.0**(SP3 的 Display 版本号是 12.**3**,引擎实际 12.0.6024.0)——判定逻辑不要用 DisplayVersion 推断引擎主版本。
3. MSSQLLocalDB 是**未实体化的自动实例**(查询返回"未创建自动实例"类消息,无实例目录),清点受影响实例时要能识别这种形态。
4. msiexec 卸载/安装在本机均未要求重启(退出码 0 而非 3010);3010 路径(RunOnce 续跑)仍保留待其他机器验证。
5. 非提权会话通过 `Start-Process -Verb RunAs` 单次 UAC 完成卸载+安装是可行的交互模式(对应 §4 提权设计)。

### 里程碑进度

- **M1(本次)**:Core 全部类骨架与可测逻辑(Config/Process/SqlLocalDb 解析器/Sector 解析/Revit 定位/隔离器/报告)+ 单元测试;App 向导壳(侧边导航、页头、状态栏、菜单)、第 0 步页、`StepPageLayout`、双语即时切换、浅色现代主题。
- 未实现(占位页标注里程碑):第 1 步页面交互(M2)、2.0–2.6 步骤页接线(M3)、2.5 下载与续跑(M4)、单 exe 合并与验收(M5)。
