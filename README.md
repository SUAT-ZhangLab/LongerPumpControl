<div align="center">

# Longer Pump Control

**Windows control software for two Longer LSP12-1B syringe pumps**

[Install](#install--安装) · [Use](#use--使用) · [Build](#build--构建) · [Known limitations](#known-limitations--已知限制)

[安装](#install--安装) · [使用](#use--使用) · [构建](#build--构建) · [已知限制](#known-limitations--已知限制)

</div>

Longer Pump Control is a Windows application for operating two LSP12-1B syringe pumps through independent USB-RS485 connections. It displays pump status and parameters, supports individual and joint control, and records monitoring data as CSV files.

Longer Pump Control 通过两条独立的 USB-RS485 连接控制两台 LSP12-1B 注射器泵，可查看设备状态与参数、执行单泵或双泵操作，并将监测数据保存为 CSV 文件。界面为中文。

## Features / 功能

| Function | 功能 |
| --- | --- |
| Status, faults, flow, delivered volume, and flow trends | 状态、故障、流量、累计液量与趋势显示 |
| Individual or joint start, pause, resume, and stop | 单泵或双泵启动、暂停、继续、停止 |
| Both pumps' parameters in one confirmation window | 在同一窗口核对两台泵的参数 |
| Distinct COM selection for each pump | 自动为两台泵选择不同串口 |
| Automatic display scaling and manual zoom | 自动适配显示大小，支持手动缩放 |
| Driver detection and FTDI download access | 驱动检测与 FTDI 官方下载入口 |
| CSV logging and hardware-free demo mode | CSV 记录与无硬件模拟模式 |

## Install / 安装

**Requirements:** Windows 10 / 11 x64 and .NET Framework 4.8. No Python or LabVIEW installation is needed.

**系统要求：** Windows 10 / 11 x64、.NET Framework 4.8，无需安装 Python 或 LabVIEW。

Download from [Releases](https://github.com/SUAT-ZhangLab/LongerPumpControl/releases):

- **Setup.exe** — installs the application and creates shortcuts. 安装软件并创建快捷方式。
- **portable.zip** — extract and run `LongerPumpControl.exe`. 解压后运行，保留整个目录和 EXE 旁的 `.exe.config`。
- **SHA256.txt** — checksums for the downloads. 下载文件校验值。

If the adapter is not recognized, open the driver assistant and follow the [FTDI driver download](https://ftdichip.com/drivers/) instructions. Public packages do not include an offline driver backup or vendor manuals; see [THIRD_PARTY.md](THIRD_PARTY.md).

若电脑未识别转接器，打开右下角“驱动与串口助手”，通过 FTDI 官网获取并安装对应驱动，再重新检测串口。公开安装包不含离线驱动备份或厂商原版说明书。

## Use / 使用

1. Connect each pump through a separate USB-RS485 adapter and select Modbus RTU on the pump panel. 每台泵使用独立转接器，泵面板选择 Modbus RTU。
2. Check the two COM ports. Defaults are **115200 / 8N1 / address 1** for each independent connection. 核对两个不同的 COM 口及通信参数。
3. Connect and check device identity, status, and parameters. 连接后核对设备身份、状态和参数；连接本身不会启动泵。
4. Use the controls on each pump card, or review both pumps' parameters before a joint start or resume. 使用单泵按钮，或确认两台参数后执行联合启动、继续。
5. Export the session CSV when needed. 按需导出本次 CSV 记录。

Logs and connection settings are stored in `%LOCALAPPDATA%\LongerPumpControl`. The application does not automatically connect or start pumps.

日志与连接设置保存在上述目录。软件不会自动连接或自动启动泵。详细操作见 [使用说明](使用说明.txt)。

## Build / 构建

Run from the repository root in Windows PowerShell. 在仓库根目录执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Build.ps1
```

The script uses the .NET Framework C# compiler, runs automated checks without hardware, and writes the installer and portable ZIP to `release/`.

脚本使用 .NET Framework C# 编译器，执行不连接实物的自动检查，并将安装包和便携 ZIP 输出到 `release/`。

```powershell
# Demo mode / 模拟模式
.\build\LongerPumpControl.exe --demo

# GUI checks with simulated pumps / 模拟泵界面检查
.\build\LongerPumpControl.exe --demo --ui-test .\build\ui-tests
```

| Directory | 内容 |
| --- | --- |
| `src/` | Application, protocol, GUI, and installer / 应用、通信协议、界面与安装器 |
| `tests/` | Automated checks and hardware diagnostic tools / 自动检查与硬件诊断工具 |
| `docs/` | Protocol notes and test records / 协议说明与测试记录 |

Some `tests/*Probe.cs` tools send motion or setting commands to real pumps. They are not run by the build. 部分 Probe 工具会控制实泵或改写参数，使用前请阅读源码；构建脚本不会执行它们。

## Known limitations / 已知限制

- **Parameter writes are not hardware-validated.** The tested firmware 1.0.3.0 returns Modbus exception 3. **参数写入尚未通过实机验收**，样机固件 1.0.3.0 返回异常码 3。
- Supported configuration: two independent serial ports, non-group mode, channel 1. 当前支持两条独立串口、非组模式、通道 1。
- Joint commands are parallel serial operations, not hardware synchronization. 联合控制不保证硬件同步；软件停止不能替代设备物理停止操作。
- Display scaling has been checked at multiple window sizes; cross-monitor DPI switching requires further hardware testing. 已检查多种窗口尺寸，跨显示器 DPI 切换仍需实机验证。

Readback, start, pause, resume, and stop have been tested on two LSP12-1B pumps. 读取及启停操作已在两台实泵上完成短时测试。

[Protocol / 协议](docs/PROTOCOL.md) · [Hardware tests / 实机测试](docs/连接检测报告.md) · [Parameter writes / 参数写入](docs/参数写入排查记录.md) · [Display and COM / 显示与串口](docs/0.2.1显示与串口修复.md)

This is independently developed software, not an official Longer product. No open-source license has been selected. 本项目为独立开发的软件，非兰格官方产品，目前未选择开源许可证。第三方资料说明见 [THIRD_PARTY.md](THIRD_PARTY.md)。
