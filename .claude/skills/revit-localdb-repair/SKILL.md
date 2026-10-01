---
name: revit-localdb-repair
description: >
  Guided self-service diagnosis and repair of the Microsoft SQL Server Express LocalDB that
  Revit Steel Connections depends on, following Autodesk's official troubleshooting articles
  step by step — for users who want to fix the problem themselves in a terminal session
  instead of (or before) running the RevitLocalDbFix GUI tool. Use this skill whenever the
  user mentions: Revit hanging or crashing when opening/syncing/linking models, template
  loading stuck, IFC import failing, a black console window (SQLDUMPER.EXE) flashing at
  startup, "SQLLocalDB instance is malfunctioning" in a journal, steel connections errors,
  sqllocaldb commands, LocalDB instance problems, or asks to check or repair the Revit /
  Advance Steel steel connections database — including in Chinese: Revit 卡死 / 崩溃 /
  钢结构连接 / 钢连接 / LocalDB 修复 / 实例损坏 / 数据库检查.
---

# Revit LocalDB Guided Repair

You are walking a user through the official Autodesk troubleshooting procedure for the
SQL Server Express LocalDB behind Revit Steel Connections. This is the assistant-guided
twin of the RevitLocalDbFix GUI tool in this repository: same step order, same safety
rules, same field-verified facts. Respond in the user's language (Chinese users get
Chinese); keep commands and raw outputs verbatim.

The golden thread: **diagnose read-only first, repair from lightest to heaviest, show the
user every command and its raw output before and after running it, and never take a
destructive step without explicit confirmation.**

## Ground rules (safety — these protect real user data)

1. **Never delete user data.** SteelConnections folders, LocalDB instance directories,
   `.mdf`/`.ldf` files and `Program Files\...\LocalDB` folders are only ever renamed or
   zip-backed-up. The only deletions allowed are `%temp%` cleanup and `sqllocaldb delete`
   — and the latter only after zipping the instance directory.
2. **Refuse any repair action while Revit.exe is running** (`Get-Process Revit`). Read-only
   diagnosis is fine. Ask the user to close Revit first.
3. **Only touch the target instance and the target year's LocalDB version.** Other
   instances (other Revit years, AdvanceSteel*, Visual Studio's MSSQLLocalDB uses) are
   other software's data.
4. **`sqllocaldb create` must always pin the version** (positional argument, e.g.
   `create "SteelConnections2022" 12.0`). Without it, LocalDB silently uses the highest
   installed engine, and a database touched by a newer engine is upgraded irreversibly.
   (Note: there is no `-v` flag; the version is positional.)
5. **Call SqlLocalDB.exe by full path** (see mapping table), never via PATH — machines
   often have both 120 and 150 installed.
6. Before each destructive step, show exactly what will change (paths, registry keys,
   commands) and get an explicit yes. A skipped step is "skipped", never "passed".
7. Keep a running log of every command, output and verdict — it becomes the report.

## Step 0 — Identify the environment

Ask (or detect) which Revit year is affected, then resolve the profile from this table.

**Version mapping (field-verified 2026-10 on a machine with Revit 2021–2027):**

| Revit year | Required LocalDB | Major | Instance name | SqlLocalDB.exe |
|---|---|---|---|---|
| 2018–2020 | SQL 2014 Express (SP1/2/3) | 12.0 | `MSSQLLocalDB` | `C:\Program Files\Microsoft SQL Server\120\Tools\Binn\SqlLocalDB.exe` |
| 2021–2023 | SQL 2014 Express (SP1/2/3) | 12.0 | `SteelConnections<year>` | same 120 path |
| 2024 | SQL 2019 Express | 15.0 | `SteelConnections2024v15` (note the suffix) | `C:\Program Files\Microsoft SQL Server\150\Tools\Binn\SqlLocalDB.exe` |
| 2025+ | SQL 2019 Express | 15.0 | `SteelConnections<year>` (NO v15 suffix) | same 150 path |

Known-good full versions: 12.0.4100.1 (SP1), 12.0.5000.0 (SP2), 12.0.6024.0 (SP3),
15.0.2104.1, 15.0.4382.1 (CU). A different full version with the right major is a
warning, not a failure. Advance Steel 2021+ uses `AdvanceSteel<year>` — same procedure.

Detect installed Revit versions from the registry (works without admin):

```powershell
Get-ChildItem 'HKLM:\SOFTWARE\Autodesk\Revit' -ErrorAction SilentlyContinue |
  ForEach-Object { Get-ChildItem $_.PSPath } |
  Where-Object { $_.PSChildName -like 'REVIT-*' } |
  ForEach-Object { Get-ItemProperty $_.PSPath | Select-Object ProductName, InstallationLocation }
```

Confirm `<InstallationLocation>\Revit.exe` exists. The SteelConnections add-in lives at
`<InstallationLocation>\AddIns\SteelConnections`.

Check the journals for the tell-tale warning (read-only, strong evidence):

```powershell
Get-ChildItem "$env:LOCALAPPDATA\Autodesk\Revit\Autodesk Revit <year>\Journals\journal*.txt" |
  Sort-Object LastWriteTime -Descending | Select-Object -First 20 |
  Select-String -SimpleMatch 'SQLLocalDB instance is malfunctioning' -List
```

Hits confirm the LocalDB diagnosis. No hits does not rule it out.

## Localized output — do not misdiagnose

**The 12.0 (SQL 2014) SqlLocalDB.exe speaks the OS language; the 15.0 exe speaks English
even on the same machine.** On a Chinese system expect `名称:/版本:/状态: 已停止/自动创建: 否`,
instance names wrapped in full-width quotes (“...”), and messages like
`LocalDB 实例“X”已启动。` / `已停止。` / `已删除。` /
`已使用版本 12.0.6024.0 创建 LocalDB 实例“X”。` — all captured verbatim from a real
machine. Treat these exactly like their English counterparts. Also field-verified:
`stop` on an already-stopped instance returns **exit code 0** (not an error).

## Diagnostic ladder (read-only — run in order)

Use `$sql = "C:\Program Files\Microsoft SQL Server\1x0\Tools\Binn\SqlLocalDB.exe"` for the
year's engine, then `& $sql <args>`.

**D1. Engine installed?** `& $sql versions` — the required engine (2014 or 2019) must be
listed, `SqlLocalDB.exe` and `...\1x0\LocalDB\Binn\sqlservr.exe` must exist. Missing
engine → go straight to R2 (clean reinstall).

**D2. Instance exists?** `& $sql i` — one instance name per line. Target missing →
repair R1 (create branch). Present → continue.

**D3. Instance details.** `& $sql i "<InstanceName>"` — check `Version`:
major ≠ required major → R1 (recreate with pinned version); unparseable garbage →
R2. Healthy example: `Version: 12.0.6024.0`, `State: Stopped`.

**D4. Start/stop test** (non-destructive; restore the state you found):
`& $sql start "<name>"` then `& $sql stop "<name>"`. Expected: the started/stopped
messages (en or zh). Failure → show the last ~40 lines of
`%LOCALAPPDATA%\Microsoft\Microsoft SQL Server Local DB\Instances\<name>\error.log`,
then → R1. A stop of a running instance can take 10+ seconds — that is normal.

**D5. Real connection** (proves what Revit actually does):

```powershell
$cn = New-Object System.Data.SqlClient.SqlConnection "Server=(localdb)\<name>;Integrated Security=True;Connection Timeout=30"
$cn.Open(); $cmd = $cn.CreateCommand(); $cmd.CommandText = 'SELECT @@VERSION'
$cmd.ExecuteScalar(); $cn.Close()
```

Success on D1–D5 means LocalDB is healthy — the problem is likely elsewhere (consider the
isolation test below, or sector check on Windows 11).

**D6. Disk sector check (Windows 11 especially; needs admin):**
`fsutil fsinfo sectorinfo C:` — if `PhysicalBytesPerSectorForAtomicity` or
`...ForPerformance` > 4096, SQL Server is affected. Fix per the official article: import
Autodesk's `SetSectorSize.reg` (https://help.autodesk.com/sfdcarticles/attachments/SetSectorSize.reg
— sets `HKLM\SYSTEM\CurrentControlSet\Services\stornvme\Parameters\Device\ForcedPhysicalSectorSizeInBytes`),
confirm with the user first, then reboot and re-check.

## Repair ladder (confirm each step; Revit must be closed)

### R1 — Delete and recreate the instance (light; field-tested end to end)

1. **Back up the instance directory first**:
   ```powershell
   Compress-Archive "$env:LOCALAPPDATA\Microsoft\Microsoft SQL Server Local DB\Instances\<name>" `
     "$env:LOCALAPPDATA\RevitLocalDbFix\backups\Instance_<name>_$(Get-Date -Format yyyyMMdd_HHmmss).zip"
   ```
2. `& $sql stop "<name>"` (if it errors on a hung instance, retry with `-k`)
3. `& $sql delete "<name>"`
4. `& $sql create "<name>" <major>`  ← version pinned, e.g. `12.0` or `15.0`
5. Re-run D3–D5. Expected create output: `created with version 12.0.6024.0` (or the zh
   equivalent). Then have the user start Revit and try the failing operation.

This does NOT uninstall anything and does not touch the steel databases in
`%ProgramData%\Autodesk\Revit Steel Connections <year>\`.

### R2 — Clean reinstall (heavy; only when R1 failed or the engine is broken/missing)

Follow the Autodesk "Investigating SQL Server LocalDB installation" article, in order,
confirming each sub-step:

1. **Uninstall** only the target engine: find the uninstall entry whose DisplayName
   matches `Microsoft SQL Server 2014 Express LocalDB` or `Microsoft SQL Server 2019
   LocalDB` (2019 has no "Express" in the name), then
   `msiexec /x {ProductCode} /qn /norestart /l*v <log>`. Exit 3010 = reboot needed.
2. **Clean `%temp%`** — skip locked files, that is expected.
3. Rename `%LOCALAPPDATA%\Microsoft\Microsoft SQL Server Local DB` → `_OLD`.
   **Warn loudly**: this invalidates ALL of the user's LocalDB instances, including ones
   created by Visual Studio or other software.
4. Rename `C:\Program Files\Microsoft SQL Server\120\LocalDB` and `...\150\LocalDB` →
   `_OLD` (if locked, reboot first and retry).
5. **Download**: SQL 2014 SP3 LocalDB direct msi:
   https://download.microsoft.com/download/3/9/F/39F968FA-DEBB-4960-8F9E-0E7BB3035959/ENU/x64/SqlLocalDB.msi
   — SQL 2019: https://www.microsoft.com/en-us/download/details.aspx?id=101064 (run the
   SSEI bootstrapper → Download Media → LocalDB). Verify the file is signed by Microsoft.
6. **Install**: `msiexec /i SqlLocalDB.msi IACCEPTSQLLOCALDBLICENSETERMS=YES /qn /norestart /l*v <log>`,
   then re-run the whole diagnostic ladder from D1.

### Isolation test (root-cause confirmation when diagnosis is ambiguous)

With Revit closed and admin rights: zip-backup `<InstallPath>\AddIns\SteelConnections`,
rename it to `SteelConnections_disabled_<timestamp>`, have the user reproduce the problem.
Problem gone → LocalDB/steel is the cause (restore the folder after repairing). Problem
unchanged → restore the folder immediately; the cause is elsewhere — do not continue down
this ladder.

## Wrap up

- Re-run D2–D5 as the final verification and have the user confirm in Revit.
- Produce a report the user can paste into an Autodesk support case: system info, Revit
  year/path, target instance, every command with raw output and verdict, conclusion.
- Not resolved after R2 → point the user to Autodesk support with that report, or to the
  RevitLocalDbFix GUI tool in this repository (standalone, not a Revit add-in).

## Official references (cite them to the user)

- Revit: Hangs or Crashes Due to SQLLocalDB Issues When Working with Models and Steel
  Connections — https://www.autodesk.com/support/technical/article/caas/sfdcarticles/sfdcarticles/Revit-Journal-records-SQLLocalDB-instance-is-malfunctioning.html
- Investigating SQL Server LocalDB installation when working with Advance Steel / Revit —
  https://www.autodesk.com/support/technical/article/caas/sfdcarticles/sfdcarticles/Advance-Steel-is-not-able-to-start-Failed-to-connect-to-all-AS-databases.html
