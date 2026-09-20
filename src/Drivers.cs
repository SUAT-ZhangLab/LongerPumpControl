using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LongerControl {
    public sealed class DriverDevice {public string Name,Id;public int Code;}
    public sealed class DriverReport {
        public DriverDevice[] Devices=new DriverDevice[0];public string[] Ports=new string[0];public string Error;
        public bool Missing {get{return Devices.Any(d=>d.Code==28);}}
        public bool Problem {get{return Devices.Any(d=>d.Code!=0);}}
        public string Summary {get{return Error!=null?"未能完成驱动检测":Missing?"检测到 FTDI 转接器缺少驱动":Problem?"检测到 FTDI 转接器异常":Devices.Length>0?"FTDI 驱动状态正常":"尚未检测到 FTDI 转接器";}}
        public string Details {get{return Summary+"\r\n\r\n"+(Error??string.Join("\r\n",Devices.Select(d=>d.Name+" · "+(d.Code==0?"正常":d.Code==28?"未安装驱动":"设备错误 "+d.Code))))+"\r\n\r\n当前串口："+(Ports.Length==0?"无":string.Join("、",Ports))+"\r\n\r\n"+(Devices.Length==0?"请插入 USB-RS485 转接器后重新检测。":"安装后如果未出现串口，可拔插 USB 转接器后重新检测。");}}
    }
    public static class DriverSupport {
        public static bool IsFtdi(string id){return id!=null&&id.IndexOf("VID_0403",StringComparison.OrdinalIgnoreCase)>=0;}
        public static DriverReport Classify(IEnumerable<DriverDevice> devices,string[] ports){return new DriverReport{Devices=devices.Where(d=>IsFtdi(d.Id)).ToArray(),Ports=ports};}
        public static DriverReport Detect(){var report=new DriverReport();try{
            report.Ports=SerialPort.GetPortNames().OrderBy(p=>p).ToArray();var list=new List<DriverDevice>();
            using(var query=new ManagementObjectSearcher("root\\CIMV2","SELECT Name, PNPDeviceID, ConfigManagerErrorCode FROM Win32_PnPEntity WHERE PNPDeviceID LIKE '%VID_0403%'",new EnumerationOptions{Timeout=TimeSpan.FromSeconds(6),ReturnImmediately=false}))
            using(var results=query.Get())foreach(ManagementObject d in results)using(d){string id=Convert.ToString(d["PNPDeviceID"]);if(IsFtdi(id))list.Add(new DriverDevice{Name=Convert.ToString(d["Name"]),Id=id,Code=Convert.ToInt32(d["ConfigManagerErrorCode"])});}
            report.Devices=list.ToArray();
        }catch(Exception ex){report.Error=ex.Message;}return report;}
        public static bool PackagePresent(string root){return File.Exists(Path.Combine(root,"Install-Drivers.ps1"))&&new[]{"ftdibus","ftdiport"}.All(name=>File.Exists(Path.Combine(root,"drivers",name,name+".inf"))&&File.Exists(Path.Combine(root,"drivers",name,name+".cat")));}
        public static async Task<string> InstallAsync(string root){
            if(!PackagePresent(root))throw new IOException("离线驱动文件不完整。请使用完整安装包或便携包。");
            string logs=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"LongerPumpControl","logs");Directory.CreateDirectory(logs);string result=Path.Combine(logs,"driver-install-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+".txt");
            return await Task.Run(()=>{
                var start=new ProcessStartInfo{FileName=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell","v1.0","powershell.exe"),Arguments="-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \""+Path.Combine(root,"Install-Drivers.ps1")+"\" -FromApp -ResultPath \""+result+"\"",UseShellExecute=true,Verb="runas",WindowStyle=ProcessWindowStyle.Hidden,WorkingDirectory=root};
                try{using(var process=Process.Start(start)){process.WaitForExit();string details=File.Exists(result)?File.ReadAllText(result):"未收到安装结果。";if(process.ExitCode!=0&&process.ExitCode!=3010)throw new IOException("驱动安装未完成：\n"+details);return details;}}
                catch(Win32Exception ex){if(ex.NativeErrorCode==1223)throw new IOException("已取消管理员授权，驱动未安装。");throw;}
            });
        }
    }
    public sealed class DriverDialog : AdaptiveForm {
        readonly Label heading;readonly TextBox details;readonly Button install,detect;readonly string root;readonly bool simulation,canInstall;bool busy;
        public DriverDialog(bool simulation,DriverReport preview=null,bool canInstall=true){this.simulation=simulation;this.canInstall=canInstall;root=AppDomain.CurrentDomain.BaseDirectory;Text="驱动与串口助手";Font=new Font("Microsoft YaHei UI",10);ClientSize=new Size(660,510);StartPosition=FormStartPosition.CenterParent;BackColor=Style.Canvas;MinimizeBox=false;MaximizeBox=false;
            var layout=Style.Rows(46,-100,102,44);layout.Padding=new Padding(24,16,24,16);Controls.Add(layout);heading=Style.Label("正在检测 USB 串口驱动…",18,Style.Ink);layout.Controls.Add(heading,0,0);details=new TextBox{ReadOnly=true,Multiline=true,ScrollBars=ScrollBars.Vertical,Dock=DockStyle.Fill,BackColor=Color.White,BorderStyle=BorderStyle.None};layout.Controls.Add(details,0,1);
            layout.Controls.Add(Style.Label("公开版提供 FTDI 官方下载入口，请选择适合本机的驱动。\n本地离线包可安装随附驱动，安装前校验签名并请求管理员授权。\n正常工作的驱动无需重装；请遵循厂商安装说明与许可。",9,Style.Muted),0,2);
            var buttons=Style.Columns(144,-100,228);detect=Style.Button("重新检测串口",Style.Blue);((ActionButton)detect).Quiet=true;detect.Dock=DockStyle.Fill;detect.Click+=async(s,e)=>await DetectAsync();buttons.Controls.Add(detect,0,0);install=Style.Button(DriverSupport.PackagePresent(root)?"安装离线驱动…":"打开官方驱动下载",Style.Blue);install.Dock=DockStyle.Fill;install.Click+=async(s,e)=>await InstallAsync();buttons.Controls.Add(install,2,0);layout.Controls.Add(buttons,0,3);
            Shown+=async(s,e)=>{if(preview!=null)ShowReport(preview);else await DetectAsync();};FormClosing+=(s,e)=>{if(busy)e.Cancel=true;};
        }
        void ShowReport(DriverReport report){Style.Text(heading,report.Summary);Style.SetColor(heading,report.Missing||report.Problem?Style.Red:Style.Ink);details.Text=report.Details+(simulation?"\r\n\r\n模拟模式不会安装驱动。":!canInstall?"\r\n\r\n安装前请先断开已连接的泵，以免驱动更新中断通信。":"");Style.Enabled(install,!simulation&&canInstall);if(!simulation&&!DriverSupport.PackagePresent(root))details.AppendText("\r\n\r\n此公开版不含离线驱动。点击下方按钮打开 FTDI 官网，安装后重新检测串口。");}
        async Task DetectAsync(){if(busy)return;busy=true;install.Enabled=detect.Enabled=false;try{var report=await Task.Run(()=>DriverSupport.Detect());ShowReport(report);}finally{busy=false;detect.Enabled=true;}}
        async Task InstallAsync(){if(simulation||!canInstall||busy)return;if(!DriverSupport.PackagePresent(root)){try{Process.Start(new ProcessStartInfo("https://ftdichip.com/drivers/"){UseShellExecute=true});}catch(Exception ex){MessageBox.Show("无法打开官网："+ex.Message+"\n请访问 https://ftdichip.com/drivers/");}return;}if(MessageBox.Show("安装随软件附带的 FTDI 驱动？\n\n适用于 FTDI USB-RS485 转接器。继续表示你已阅读安装目录 drivers 中的原始 INF 许可与来源说明。\nWindows 将请求管理员授权。","确认驱动安装",MessageBoxButtons.OKCancel,MessageBoxIcon.Question)!=DialogResult.OK)return;
            busy=true;install.Enabled=detect.Enabled=false;Style.Text(heading,"正在安装驱动，请完成 Windows 授权…");
            try{string result=await DriverSupport.InstallAsync(root);var report=await Task.Run(()=>DriverSupport.Detect());ShowReport(report);details.AppendText("\r\n\r\n安装结果：\r\n"+result);}
            catch(Exception ex){Style.Text(heading,"安装未完成");details.Text=ex.Message;}
            finally{busy=false;detect.Enabled=true;install.Enabled=!simulation&&canInstall;}
        }
    }
    public sealed partial class MainForm {
        LinkLabel driverHelp;bool detectingDrivers;readonly System.Windows.Forms.Timer deviceTimer=new System.Windows.Forms.Timer();
        void ConfigureDriverHelp(Grid footer){driverHelp=new LinkLabel{Text="驱动与串口助手",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleRight};driverHelp.LinkClicked+=async(s,e)=>{if(Working)return;using(var dialog=new DriverDialog(demo,null,!Sessions.Any(p=>p.Connected)))dialog.ShowDialog(this);RefreshPorts();await DetectDriversAsync();};footer.Controls.Add(driverHelp,2,0);deviceTimer.Interval=900;deviceTimer.Tick+=async(s,e)=>{deviceTimer.Stop();await DetectDriversAsync();};}
        async Task DetectDriversAsync(){if(demo||detectingDrivers||closing||driverHelp==null)return;detectingDrivers=true;Render();try{var report=await Task.Run(()=>DriverSupport.Detect());if(IsDisposed||closing)return;Style.Text(driverHelp,report.Missing?"缺少驱动 · 点击安装":"驱动与串口助手");driverHelp.LinkColor=report.Missing?Style.Red:Style.Blue;if(report.Missing&&!Working)SetBanner("发现 FTDI 转接器缺少驱动，请点击右下角“缺少驱动 · 点击安装”。",true);UseDriverPorts(report);}finally{detectingDrivers=false;if(!IsDisposed)Render();}}
        protected override void WndProc(ref Message m){base.WndProc(ref m);if(m.Msg==0x219&&!demo&&driverHelp!=null&&!closing){deviceTimer.Stop();deviceTimer.Start();}}
    }
}
