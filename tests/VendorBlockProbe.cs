using System;
using System.IO;
using System.IO.Ports;
using System.Linq;
using LongerControl;
class VendorBlockProbe {
 static int Main(){try{using(var c=new PumpClient(new SerialWire("COM8",115200,Parity.None),1)){
  var before=c.Settings();if(c.Status().State!=3)return 1;ushort enable=c.Read(0,1)[0];c.Write(0,1);System.Threading.Thread.Sleep(200);var test=before.Take(25).ToArray();test[7]=65535;test[8]=10;test[9]=100;test[10]=60;test[11]=100;test[12]=60;test[13]=100;test[21]=10;test[22]=100;test[23]=10;test[24]=100;
  try{c.Write(0x60,test);Console.WriteLine("VENDOR BLOCK ACCEPTED: "+string.Join(" ",c.Settings().Select(x=>x.ToString("X4"))));}
  catch(Exception ex){Console.WriteLine("VENDOR BLOCK REJECTED: "+ex.Message);}
  finally{var after=c.Settings();if(!after.SequenceEqual(before)){
    var restore=before.Take(25).ToArray();restore[7]=65535;
    foreach(int i in new[]{10,12})if(restore[i]>9999){restore[i]=(ushort)Math.Round(restore[i]/10.0);restore[i+1]++;}
    c.Write(0x60,restore);Console.WriteLine("RESTORE "+string.Join(" ",c.Settings().Select(x=>x.ToString("X4"))));
   }else Console.WriteLine("original parameters unchanged");c.Write(0,enable);Console.WriteLine("enable restored="+c.Read(0,1)[0]);}
 }return 0;}catch(Exception ex){Console.WriteLine("ERROR: "+ex.Message);return 1;}}
}
