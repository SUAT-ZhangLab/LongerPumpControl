# 第三方资料与驱动

公开仓库及公开安装包不包含 FTDI DriverStore 备份、厂商说明书 PDF 或厂商 LabVIEW 文件。它们不属于本项目原创源码。

- FTDI 官方驱动下载：https://ftdichip.com/drivers/
- 本地开发使用 FTDI 2.12.36.20 驱动、LSP100 中文说明书附录 A 和 LSP1x Modbus 协议资料；相关资料请向设备厂商获取。
- FTDI 原始 INF 的第 1.3、3.1.7 条限制分发范围，公开版本不附带该备份。驱动安装须遵循厂商许可，只适用于对应 FTDI 硬件。
- 若具备相应使用与分发权限，可在本地放置 `drivers/ftdibus`、`drivers/ftdiport` 与 `LSP100_manual_cn.pdf`，使用 `Build.ps1 -IncludeLocalThirdPartyFiles` 生成离线迁移包。不要将该包直接作为本项目公开发行资产。

本仓库公开源代码不代表授予第三方资料的再分发权；目前未选择开源许可证。
