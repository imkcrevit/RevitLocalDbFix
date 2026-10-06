# RevitLocalDbFix

**独立运行的 Windows 桌面修复工具 —— 不是 Revit 插件。**
**A standalone Windows desktop repair tool — NOT a Revit add-in.**

RevitLocalDbFix 按 Autodesk 官方支持文章的排查顺序,逐步修复 Revit 钢结构连接(Steel Connections)所依赖的 Microsoft SQL Server Express LocalDB。它不向 Revit 安装任何内容,单独运行、单个 exe、客户机只需 .NET Framework 4.8(Windows 10/11 自带)。

RevitLocalDbFix repairs the Microsoft SQL Server Express LocalDB used by Revit Steel Connections, following the official Autodesk support articles step by step. It installs nothing into Revit, runs on its own as a single exe, and requires only .NET Framework 4.8 (built into Windows 10/11).

## 典型症状 / Symptoms

- 打开/同步/链接模型时卡死或崩溃;样板加载卡住;IFC 导入失败
- 启动时黑色命令行窗口闪现(`SQLDUMPER.EXE`)
- Journal 中出现:`DBG_WARN: SQLLocalDB instance is malfunctioning.`

## 依据文章 / Based on

| 文章 | 链接 |
|---|---|
| Revit: Hangs or Crashes Due to SQLLocalDB Issues When Working with Models and Steel Connections | [autodesk.com](https://www.autodesk.com/support/technical/article/caas/sfdcarticles/sfdcarticles/Revit-Journal-records-SQLLocalDB-instance-is-malfunctioning.html) |
| Investigating SQL Server LocalDB installation when working with Advance Steel / Revit | [autodesk.com](https://www.autodesk.com/support/technical/article/caas/sfdcarticles/sfdcarticles/Advance-Steel-is-not-able-to-start-Failed-to-connect-to-all-AS-databases.html) |

## 修复流程 / Repair flow

```
第0步 选择 Revit 版本(注册表检测 + journal 扫描)
第1步 隔离试验:zip 备份 + 摘除 SteelConnections,用户试运行 Revit 确认病因
第2步 逐步确认修复(每一步显示命令、原始输出、原文预期,用户确认才继续):
  2.0 磁盘扇区检查 → 2.1 实例列表 → 2.2 实例详情 → 2.3 启停测试
  → 2.4 删除重建实例 → 2.5 清洁重装(六子步) → 2.6 真连接验证
第3步 还原 SteelConnections
第4步 最终验证 + 生成可转发报告(Markdown + JSON)
```

安全规则:永不删除用户数据(只重命名/压缩备份)、只操作目标实例与目标版本、所有破坏性动作二次确认、`sqllocaldb create` 必须锁定版本号。

## 当前状态与测试程度 / Status &amp; Test Coverage

**开发阶段:Alpha(里程碑 M1 完成,2026-10-01)** — 核心检测/修复逻辑与 UI 框架已就绪并经真机验证,但向导交互页面仍在开发中,**尚不适合最终用户直接使用**。
**Stage: Alpha (milestone M1).** Core logic is field-verified on a real machine, but the wizard interaction pages are still under construction — not ready for end users yet.

### 已实现 / Implemented

- Core 后端类库全量:Revit **与 Advance Steel** 版本映射表、注册表定位、Journal 扫描、SqlLocalDB 客户端(固定路径调用)与**中/英文输出解析**、磁盘扇区检查、SteelConnections 隔离器(zip 备份/摘除/还原)、清洁重装六子步、**实例检查与增加**、Markdown+JSON 报告、会话状态持久化、UAC 提升辅助
- WPF 向导壳:侧边步骤导航(✓/✗ 状态图标)、管理员徽章、**中英双语即时切换**、浅色现代主题、第 0 步版本选择页(注册表枚举 + 系统信息 + journal 扫描 + 手动定位)
- **工具页"检查与增加实例"**:列出本机每个 Revit / Advance Steel 版本应有的专用实例,判定"正常 / 缺失 / 引擎版本不符 / 无法读取 / 引擎未安装";缺失的实例可一键按锁定版本补建(先确认、先备份残留目录、绝不替换已有实例);能识别照文章"2024 起带 v15"误建的实例(如 `SteelConnections2025v15`),只报告不删除
- 第 1 步与 2.x 步骤交互页为占位,按里程碑 M2–M4 推进

### 测试程度 / Test coverage(截至 2026-10-06)

| 层级 | 程度 |
|---|---|
| 单元测试 | **62 个全部通过**:sqllocaldb 输出解析(含真机捕获的中文样例)、fsutil 解析、版本映射表锁定(含 Advance Steel)、注册表定位(假数据)、实例检查与增加(模拟 sqllocaldb)、报告生成 |
| 真机只读检验 | Win10 19045 **中文系统**,Revit **2021/2023/2024/2025/2026/2027** 六版本:注册表定位(非默认路径)、Journal 扫描、LocalDB 安装检查、实例列表与详情、"检查与增加实例"页(UI 自动化打开,6 行全部"正常") — **全部正确** |
| 2.4 删除重建 | 真机实测两次成功(SteelConnections2021 锁定 12.0;SteelConnections2024v15 锁定 15.0,且是在残留目录存在的情况下) |
| 2.5 清洁重装 | 真机实测卸载 / 下载验签 / 重装子步:SQL 2014 与 SQL 2019 两个引擎各做过,退出码 0、无需重启,用户实例保留。改名 `_OLD` 子步(2.5.2–2.5.4)未执行 |
| 端到端故障重现 | 卸载 15.0 引擎 → 打开 Revit 2024 → 钢连接 `DatabaseConnectionErrors.log` 记录 LocalDB 错误(journal 无官方警告)→ 重装引擎 + 补建丢失的实例 → 对照运行无新错误 |
| 尚未覆盖 | 实例增加按钮的真机点击、Advance Steel 真机(本机未安装)、第 1 步隔离实操、扇区注册表写入、提权与重启续跑、英文系统、Windows 11、Revit 2021/2023 的 Revit 侧验证(本机许可受限) |

真机检验修正了几处规格/官方文章与现实的偏差:① 2014 版 SqlLocalDB.exe 在中文系统输出中文标签(原英文解析会把健康机器误判为需重装);② 2025+ 实例名实际**无** v15 后缀(仅 2024 为 `SteelConnections2024v15`,官方文章写的是"2024 起都带 v15");③ 引擎缺失时 journal 不一定有官方警告,证据在钢连接自己的 `DatabaseConnectionErrors.log`。全部实测证据与决策记录见 [docs/IMPLEMENTATION_NOTES.md](docs/IMPLEMENTATION_NOTES.md)。

## 项目结构 / Structure

```
src/RevitLocalDbFix.Core/        后端类库(检测/修复逻辑,不引用任何 UI)
src/RevitLocalDbFix.App/         WPF 前端(向导壳、浅色现代主题、中英双语)
tests/                           单元测试(解析器/映射表/定位器,固定样例,不依赖真实环境)
docs/                            SPEC 与实施笔记
.claude/skills/
  revit-localdb-repair/          AI 陪跑自助修复技能(见下)
```

## 两种使用方式 / Two ways to get help

| 方式 | 适合 | 说明 |
|---|---|---|
| **GUI 向导工具**(本仓库主体) | 希望点按钮完成修复的最终用户 | 独立 exe,逐步执行官方 SOP,每步展示命令/输出/判定 |
| **AI 陪跑技能** `revit-localdb-repair` | **希望自己动手**、在终端里理解每一步的用户 | 克隆本仓库后在 [Claude Code](https://claude.com/claude-code) 中打开,说出症状(如"Revit 打开模型卡死"),AI 会按同一套官方 SOP 与安全规则陪你逐条命令诊断和修复:只读诊断优先、修复由轻到重、每个破坏性动作先备份并征得确认 |

技能与 GUI 工具共享同一套版本映射表与真机实证数据(中文输出解析、2025+ 实例名规则、create 锁版本语法),见 `.claude/skills/revit-localdb-repair/SKILL.md`。

## 构建 / Build

```
dotnet build RevitLocalDbFix.sln -c Release
dotnet test RevitLocalDbFix.sln
```

要求 .NET SDK(8+)与 .NET Framework 4.8 开发者包。产物:`src/RevitLocalDbFix.App/bin/Release/net48/RevitLocalDbFix.exe`。

## 语言 / Language

界面默认跟随系统语言(中文系统显示中文),可在菜单 `Language / 语言` 中随时切换 English / 中文,无需重启。

## 免责声明 / Disclaimer

工具在每次更改前都会自动备份,但请自行保留重要数据的备份。本工具与 Autodesk、Microsoft 无隶属关系;文章内容版权归其原作者所有。
