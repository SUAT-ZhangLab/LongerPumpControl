<div align="center">

# Longer Pump Control

**A desktop workspace for two Longer LSP12-1B syringe pumps**

[Install](#install) · [Capabilities](#capabilities) · [Workflow](#workflow) · [Build](#build) · [Validation](#validation)

[安装](#安装) · [功能](#功能) · [操作流程](#操作流程) · [构建](#构建) · [验证与限制](#验证与限制)

</div>

Longer Pump Control brings two syringe pumps into one Windows workspace. Read device status, compare both pumps' parameters in one confirmation window, issue coordinated commands, and record measurements as CSV files. The main window carries the **Zhang Lab** heading; the application and installer retain the Longer Pump Control name.

**双注射器泵桌面工作台**

在同一窗口监测两台注射器泵，核对双方参数，执行联合启停并保存 CSV 记录。主界面顶部显示 **Zhang Lab**，应用与安装包保留 Longer Pump Control 名称。

Windows 10 / 11 x64 · C# / Windows Forms · .NET Framework 4.8 · Chinese interface / 中文界面

---

## Install

Download the installer or portable ZIP from [Releases](https://github.com/SUAT-ZhangLab/LongerPumpControl/releases). The installer creates desktop and Start menu shortcuts. For the portable version, keep the entire extracted folder, including `LongerPumpControl.exe.config`.

The public package includes a driver detection assistant and a link to the [official FTDI driver page](https://ftdichip.com/drivers/). It does not redistribute the local DriverStore backup, vendor manual, or LabVIEW files. See [third-party materials](THIRD_PARTY.md).

## 安装

从 [Releases](https://github.com/SUAT-ZhangLab/LongerPumpControl/releases) 下载单文件安装包或便携 ZIP。安装包会创建桌面和开始菜单快捷方式；便携版请保留整个目录，尤其是 EXE 旁的 `.exe.config`。

新电脑缺少驱动时，打开应用右下角“驱动与串口助手”，通过 [FTDI 官方页面](https://ftdichip.com/drivers/) 获取并安装驱动，然后重新检测串口。公开版不附带本地驱动备份、厂商 PDF 或 LabVIEW 文件，详见 [第三方说明](THIRD_PARTY.md)。运行应用不需要 Python 或 LabVIEW；首次下载驱动需要互联网。

---

## Capabilities

| Task | What the application provides |
| --- | --- |
| Monitor two pumps | Device identity, status, faults, flow trends, and CSV records |
| Operate together | Joint start, pause, resume, and stop; one parameter review for both pumps |
| Connect on another computer | Distinct automatic COM selections and preservation of valid manual choices |
| Use different displays | Automatic DPI/window scaling, manual zoom, and reduced refresh flicker |
| Inspect settings | Parameter readback and an experimental parameter editor |
| Try without hardware | Demo mode with simulated pumps |

## 功能

| 任务 | 软件提供的功能 |
| --- | --- |
| 同时监测 | 显示两台身份、状态、故障、流量趋势，保存 CSV |
| 联合控制 | 启动、暂停、继续、停止；同窗核对两台参数 |
| 更换电脑 | 自动选择不同 COM 口，保留有效手动选择 |
| 更换显示器 | 自动适配 DPI 与窗口大小、手动缩放、减少刷新闪烁 |
| 查看设置 | 参数读取与实验性参数编辑 |
| 无硬件体验 | 模拟双泵的 Demo 模式 |

---

## Workflow

1. Connect each pump through its own USB-RS485 adapter. Select Modbus RTU on the pump panel.
2. Verify two different COM ports. Defaults: **115200 / 8N1 / address 1** on each independent port.
3. Connect and check the reported LSP12-1B model, identity, and status. Connecting does not start motion.
4. Review both pumps' parameters in the shared confirmation window before joint start or resume.
5. Watch status and trends; export the session CSV when needed.

## 操作流程

1. 每台泵使用独立 USB-RS485 转接器，在泵面板选择 Modbus RTU。
2. 核对两个不同 COM 口；默认均为 **115200 / 8N1 / 地址 1**。
3. 连接后确认 LSP12-1B 型号、设备身份与状态。连接只读取信息，不会启动运动。
4. 联合启动或继续前，在同一窗口核对两台参数。
5. 观察状态和趋势，按需导出本次 CSV。

The application does not automatically connect, start, or resume pumps. 当前支持两条独立串口、非组模式、通道 1；不会自动连接、启动或恢复运动。

[User guide / 使用说明](使用说明.txt) · [Protocol / 协议](docs/PROTOCOL.md)

---

## Build

Run in Windows PowerShell from the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Build.ps1
```

The script uses the Windows .NET Framework C# compiler, runs checks without hardware, and produces an installer and portable ZIP in `release/`. No NuGet, Python, or LabVIEW dependency is required.

## 构建

在仓库根目录执行上面的 PowerShell 命令。脚本使用 Windows .NET Framework C# 编译器，运行不连接实物的检查，并在 `release/` 生成安装包和便携 ZIP。

```powershell
# Explore the GUI without connecting hardware / 无硬件体验
.\build\LongerPumpControl.exe --demo

# GUI checks with simulated pumps / 模拟泵 GUI 检查
.\build\LongerPumpControl.exe --demo --ui-test .\build\ui-tests
```

| Location / 目录 | Contents / 内容 |
| --- | --- |
| `src/` | Application, protocol, GUI, driver assistant, installer / 应用与安装器源码 |
| `tests/` | Automated checks and historical hardware probes / 自动检查与历史硬件探针 |
| `docs/` | Protocol notes, test records, known limits / 协议、验收与限制 |
| `Build.ps1` | Compile, test, package / 编译、测试、打包 |
| `Install-Drivers.*` | Local offline migration support / 本地离线迁移脚本 |

`tests/*Probe.cs` includes tools that can move real pumps. They are not automatically executed by the build or application, and are not shipped in release packages. 部分 Probe 工具会控制实泵，请先阅读源码；它们不会在构建或应用启动时执行。

---

## Validation

Status and parameter reads, start, pause, resume, and stop were exercised on two LSP12-1B pumps. **Parameter writes are still rejected by the tested firmware 1.0.3.0 with Modbus exception 3; hardware acceptance of the settings feature is incomplete.**

Joint commands use parallel serial operations, not hardware synchronization. Software stop does not replace the physical stop control. Cross-monitor DPI switching still requires testing on the relevant hardware. This is an independently developed application, not official Longer software.

## 验证与限制

两台 LSP12-1B 已完成状态和参数读取、启动、暂停、继续、停止的短时实测。**参数写入仍被样机固件 1.0.3.0 拒绝（Modbus 异常码 3），设置功能尚未通过实机验收。**

联合控制采用并行串口操作，不保证硬件同步；软件停止不能替代物理停止操作。真实跨显示器 DPI 切换仍需对应硬件验证。本项目不是兰格官方软件。

[Display and COM checks / 显示与串口检查](docs/0.2.1显示与串口修复.md) · [Hardware report / 实机记录](docs/连接检测报告.md) · [Parameter-write investigation / 参数写入排查](docs/参数写入排查记录.md)

Logs and settings / 日志和配置：`%LOCALAPPDATA%\LongerPumpControl`。Historical offline-driver notes describe the local migration package / 历史离线驱动验收记录针对本地迁移包。

## License and third-party materials / 许可与第三方资料

No open-source license has been selected for the project yet. Public availability does not grant redistribution rights to third-party materials. See [THIRD_PARTY.md](THIRD_PARTY.md).

本项目目前未选择开源许可证；仓库公开不代表授予第三方资料的再分发权。
