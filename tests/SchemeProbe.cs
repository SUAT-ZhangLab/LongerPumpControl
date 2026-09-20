using System;
using System.IO.Ports;
using LongerControl;
class SchemeProbe {
 static void Main(){using(var c=new PumpClient(new SerialWire("COM8",115200,Parity.None),1)){
  ushort selected=c.Read(0x5E,1)[0];Console.WriteLine("active="+selected+" edit selector="+c.Read(0x26E,1)[0]);
  try{c.Write(0x26E,selected);Console.WriteLine("edit selector written="+c.Read(0x26E,1)[0]);}catch(Exception ex){Console.WriteLine("selector rejected: "+ex.Message);return;}
  var saved=c.Read(0x270,30);Console.WriteLine("saved force="+saved[14]);
  try{c.Write(0x27E,44);Console.WriteLine("edit force="+c.Read(0x27E,1)[0]);}catch(Exception ex){Console.WriteLine("edit rejected: "+ex.Message);}
  finally{if(c.Read(0x27E,1)[0]!=saved[14])c.Write(0x27E,saved[14]);Console.WriteLine("final stored force="+c.Read(0x27E,1)[0]+" current force="+c.Read(0x6E,1)[0]);}
 }}
}
