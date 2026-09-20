using System;
using System.IO;
using LongerControl;
class DriverTests {
    static int count;
    static void Check(bool condition,string name){if(!condition)throw new Exception(name);count++;Console.WriteLine("PASS "+name);}
    static int Main(string[] args){try{
        var device=new DriverDevice{Name="FT232R USB UART",Id="USB\\VID_0403&PID_6001\\A",Code=28};
        var report=DriverSupport.Classify(new[]{device},new string[0]);Check(report.Missing&&report.Problem,"FTDI code 28 offers driver installation");
        device.Code=0;report=DriverSupport.Classify(new[]{device},new[]{"COM8"});Check(!report.Missing&&!report.Problem,"working FTDI not misclassified as missing");
        device.Code=43;report=DriverSupport.Classify(new[]{device},new string[0]);Check(!report.Missing&&report.Problem,"device error distinguished from missing driver");
        device.Id="USB\\VID_1A86&PID_7523";device.Code=28;report=DriverSupport.Classify(new[]{device},new string[0]);Check(!report.Missing&&report.Devices.Length==0,"non-FTDI missing driver not offered FTDI fix");
        Check(DriverSupport.IsFtdi("FTDIBUS\\VID_0403+PID_6001+BH00LSLEA\\0000"),"FTDI virtual COM device recognized");
        Check(!DriverSupport.PackagePresent(Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString())),"incomplete driver payload blocks installation");
        if(Array.IndexOf(args,"--optional-payload")>=0)Check(!DriverSupport.PackagePresent(args[0]) || File.Exists(Path.Combine(args[0],"Install-Drivers.ps1")),"public build supports absent driver payload");
        else Check(DriverSupport.PackagePresent(args[0]),"offline driver payload present");
        if(args.Length>1&&args[1]=="--detect"){report=DriverSupport.Detect();Console.WriteLine(report.Details);if(report.Error!=null)throw new Exception("Live detection failed: "+report.Error);}
        Console.WriteLine(count+" driver checks passed; no drivers installed");return 0;
    }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
}
