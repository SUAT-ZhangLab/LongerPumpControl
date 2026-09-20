using System;
using System.IO.Ports;
using System.Linq;
using LongerControl;
class FinalStateProbe {
 static int Main(string[] args){try{if(args.Contains("--test-error-handler"))throw new InvalidOperationException("Simulated diagnostic failure; handled without a system dialog.");return Run();}catch(Exception ex){Console.Error.WriteLine("Diagnostic failed: "+ex.Message);return 1;}}
 static int Run(){foreach(string name in new[]{"COM8","COM9"})using(var c=new PumpClient(new SerialWire(name,115200,Parity.None),1)){
   if(name=="COM8")try{c.Write(0x26E,1);}catch(Exception ex){Console.WriteLine("Edit selector restore: "+ex.Message);} // Original selector was 1.
   var s=c.Status();var r=c.Settings();Console.WriteLine(name+" state="+s.State+" errors="+s.Errors+" device-enable="+c.Read(0,1)[0]+" edit-selector="+c.Read(0x26E,1)[0]);
   Console.WriteLine(string.Join(" ",r.Select(v=>v.ToString("X4"))));if(s.State!=3||s.Errors!=0)return 1;
 }return 0;}
}
