using System;
using System.IO.Ports;
using System.Linq;
using LongerControl;
class ChannelProbe {
 static void Main(){using(var c=new PumpClient(new SerialWire("COM8",115200,Parity.None),1)){
  var baseline=c.Settings();var channel=c.Read(0x2060,30);Console.WriteLine("Channel1 same="+channel.SequenceEqual(baseline));if(!channel.SequenceEqual(baseline))return;
  try {c.Write(0x206E,44);Console.WriteLine("channel write accepted force="+c.Read(0x206E,1)[0]+" base="+c.Read(0x6E,1)[0]);}
  catch(Exception ex){Console.WriteLine(ex.Message);}finally{if(c.Read(0x206E,1)[0]!=baseline[14])c.Write(0x206E,baseline[14]);Console.WriteLine("restored force="+c.Read(0x6E,1)[0]+" state="+c.Status().State);}
 }}
}
