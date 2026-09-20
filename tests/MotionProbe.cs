using System;
using System.IO;
using System.IO.Ports;
using System.Threading;
using LongerControl;
class MotionProbe {
 static int Main(string[] args){bool ok=true;foreach(var port in args){using(var c=new PumpClient(new SerialWire(port,115200,Parity.None),1)){
  var before=c.Status();if(before.State!=3||before.Errors!=0)throw new Exception("not stopped and fault-free");
  try{c.Command(1,1);Thread.Sleep(150);var run=c.Status();string msg=port+" short start: state="+run.State+" fault="+run.Errors;Console.WriteLine(msg);File.AppendAllText("build/motion-results.txt",DateTime.Now.ToString("o")+" "+msg+Environment.NewLine);if(run.State!=1)ok=false;c.Command(13,1);Console.WriteLine(port+" pause confirmed state="+c.Status().State);Thread.Sleep(150);c.Command(13,2);Console.WriteLine(port+" resume confirmed state="+c.Status().State);File.AppendAllText("build/motion-results.txt",DateTime.Now.ToString("o")+" "+port+" pause=2 / resume=1 confirmed"+Environment.NewLine);}
  catch(Exception ex){ok=false;Console.WriteLine(port+" start failed: "+ex.Message);}
  finally{c.Command(1,0);Thread.Sleep(150);var stopped=c.Status();string msg=port+" final stop: state="+stopped.State+" fault="+stopped.Errors;Console.WriteLine(msg);File.AppendAllText("build/motion-results.txt",DateTime.Now.ToString("o")+" "+msg+Environment.NewLine);if(stopped.State!=3||stopped.Errors!=0)ok=false;}
 }}return ok?0:1;}
}
