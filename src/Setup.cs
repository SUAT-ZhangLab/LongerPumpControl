using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;
class Setup : Form {
    readonly string target=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","LongerPumpControl");
    public Setup(){Text="安装 Longer Pump Control";ClientSize=new Size(610,315);Font=new Font("Microsoft YaHei UI",10);StartPosition=FormStartPosition.CenterScreen;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;
        var title=new Label{Text="Longer Pump Control  0.2.1",Font=new Font(Font.FontFamily,18),AutoSize=true,Location=new Point(26,24)};Controls.Add(title);
        Controls.Add(new Label{Text="双泵控制工作台 · Windows 10 / 11 x64\n\n安装到当前用户目录，无需安装 Python 或 LabVIEW。\n应用内提供驱动检测和 FTDI 官方下载入口。\n公开安装包不含厂商驱动备份或原版说明书。\n\n安装位置：\n"+target,Location=new Point(26,80),Size=new Size(560,166)});
        var button=new Button{Text="安装",Location=new Point(420,255),Size=new Size(150,36)};button.Click+=(s,e)=>Install(button);Controls.Add(button);
    }
    void Install(Button button){try{button.Enabled=false;
        if(Process.GetProcessesByName("LongerPumpControl").Length>0)throw new Exception("请先关闭正在运行的 Longer Pump Control 后重试。");
        Extract(target);
        string exe=Path.Combine(target,"LongerPumpControl.exe");
        // Shell COM creates a real shortcut without shell command interpolation.
        var shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
        string[] links={Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"Longer Pump Control.lnk"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),"Longer Pump Control.lnk")};
        foreach(var link in links){object shortcut=shell.GetType().InvokeMember("CreateShortcut",BindingFlags.InvokeMethod,null,shell,new object[]{link});var t=shortcut.GetType();t.InvokeMember("TargetPath",BindingFlags.SetProperty,null,shortcut,new object[]{exe});t.InvokeMember("WorkingDirectory",BindingFlags.SetProperty,null,shortcut,new object[]{target});t.InvokeMember("Save",BindingFlags.InvokeMethod,null,shortcut,null);}
        using(var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\LongerPumpControl")){key.SetValue("DisplayName","Longer Pump Control");key.SetValue("DisplayVersion","0.2.1");key.SetValue("InstallLocation",target);key.SetValue("DisplayIcon",exe);key.SetValue("UninstallString","powershell.exe -NoProfile -ExecutionPolicy Bypass -File \""+Path.Combine(target,"Uninstall.ps1")+"\"");key.SetValue("NoModify",1);key.SetValue("NoRepair",1);}
        MessageBox.Show("安装完成。桌面与开始菜单已创建快捷方式。\n\n如新电脑未识别串口，请使用应用内“驱动与串口助手”。\n软件启动后不会自动连接或启动泵。","安装完成");Close();
    }catch(Exception ex){MessageBox.Show("安装未完成："+ex.Message,"安装错误");button.Enabled=true;}}
    static void Extract(string destination){string root=Path.GetFullPath(destination)+Path.DirectorySeparatorChar;Directory.CreateDirectory(root);using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip"))using(var zip=new ZipArchive(stream,ZipArchiveMode.Read)){foreach(var entry in zip.Entries){string path=Path.GetFullPath(Path.Combine(root,entry.FullName));if(!path.StartsWith(root,StringComparison.OrdinalIgnoreCase))throw new IOException("安装包路径无效");if(string.IsNullOrEmpty(entry.Name)){Directory.CreateDirectory(path);continue;}Directory.CreateDirectory(Path.GetDirectoryName(path));using(var source=entry.Open())using(var output=File.Create(path))source.CopyTo(output);}}}
    [STAThread] static void Main(string[] args){if(args.Length==2&&args[0]=="--extract"){Extract(args[1]);return;}Application.EnableVisualStyles();Application.Run(new Setup());}
}
