using System;
using System.Linq;
using System.IO.Ports;
using LongerControl;
class WriteShapeProbe {
 static int Main(){using(var c=new PumpClient(new SerialWire("COM8",115200,Parity.None),1)){
  var baseline=c.Settings();if(c.Status().State!=3)throw new Exception("not stopped");
  ushort enable=c.Read(0,1)[0];Console.WriteLine("Read enable register 0x0000: "+enable);
  // Vendor aBuild node 973 has 21 values; input 7 is constant FFFF (node 1238).
  try {
  foreach(var pair in new[]{new[]{0x60,21}}){
   try{var vals=baseline.Skip(pair[0]-0x60).Take(pair[1]).ToArray();vals[7]=65535;c.Write((ushort)pair[0],vals);Console.WriteLine("ACCEPT unchanged write 0x"+pair[0].ToString("X4")+" count="+pair[1]);}
   catch(Exception ex){Console.WriteLine("REJECT unchanged write 0x"+pair[0].ToString("X4")+" count="+pair[1]+" "+ex.Message);}
   var after=c.Settings();if(!after.SequenceEqual(baseline)){Console.WriteLine("PARAMETER DIFFERENCE - STOP");return 1;}
  }
  Console.WriteLine("Baseline preserved, state="+c.Status().State);return 0;
  } finally {c.Write(0,enable);Console.WriteLine("Enable restored="+c.Read(0,1)[0]);}
 }}
}
