using System;
using System.IO.Ports;
using LongerControl;
class Probe {
    static int Main(string[] args){bool ok=true;Console.WriteLine("Read-only hardware verification "+DateTime.Now.ToString("o"));
        foreach(string name in args){try{using(var c=new PumpClient(new SerialWire(name,115200,Parity.None),1)){
            var s=c.Status();var r=c.Settings();var id=c.Identity();Console.WriteLine(name+" model="+id.Model+" serial="+id.Serial+" hardware="+id.Hardware+" firmware="+id.Firmware);Console.WriteLine(name+" address=1 115200/8N1 CRC=OK state="+s.State+" errors="+s.Errors);
            Console.WriteLine("capacity="+Units.Volume(r[1],r[2])+" target="+Units.Volume(r[8],r[9])+" infusion="+Units.Flow(r[10],r[11])+" withdrawal="+Units.Flow(r[12],r[13])+" force="+r[14]);
            for(int i=0;i<10;i++){s=c.Status();System.Threading.Thread.Sleep(100);}Console.WriteLine("10 repeated status reads: PASS");
        }}catch(Exception ex){ok=false;Console.WriteLine(name+" FAIL "+ex.Message);}}
        return ok?0:1;
    }
}
