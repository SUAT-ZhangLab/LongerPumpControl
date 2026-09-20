# Tests / 测试

`Build.ps1` runs ProtocolTests, GroupTests, DriverTests, and LayoutPortTests without connecting hardware.

`*Probe.cs` files are historical hardware diagnostic tools. Some send motion or setting commands. Read the source and verify the equipment before running them; they are not automatic tests.

构建自动运行的测试不连接实物。Probe 工具可能驱动实泵或改写参数，不会自动执行，使用前须阅读源码并核对设备条件。
