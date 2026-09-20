using System;
using System.IO.Ports;
using LongerControl;
class SingleWriteProbe {
 static void Main(){using(var w=new SerialWire("COM8",115200,Parity.None)){var c=new PumpClient(w,1);var orig=c.Settings();ushort en=c.Read(0,1)[0];Console.WriteLine("scheme="+c.Read(0x5E,1)[0]);try {Console.WriteLine("06 enable="+BitConverter.ToString(w.Exchange(PumpClient.Frame(1,6,0,0,0,1),8)));System.Threading.Thread.Sleep(500);c.Write(0x6E,44);Console.WriteLine("changed force readback="+c.Read(0x6E,1)[0]);}catch(Exception ex){Console.WriteLine(ex.Message);}finally{if(c.Read(0x6E,1)[0]!=orig[14])c.Write(0x6E,orig[14]);Console.WriteLine("final force="+c.Read(0x6E,1)[0]+" state="+c.Status().State);Console.WriteLine("restore enable="+BitConverter.ToString(w.Exchange(PumpClient.Frame(1,6,0,0,0,(byte)en),8)));}}}
}
