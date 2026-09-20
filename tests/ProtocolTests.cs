using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using LongerControl;
class Tests {
    class Wire:IWire {
        public byte[] Request;public Func<byte[],byte[]> Reply;
        public byte[] Exchange(byte[] q,int n){Request=q;return Reply(q);}
        public void Dispose(){}
    }
    static int passed;
    static void Check(bool condition,string name){if(!condition)throw new Exception("FAIL "+name);passed++;Console.WriteLine("PASS "+name);}
    static void Reject(Action act,string name){try{act();}catch{Check(true,name);return;}throw new Exception("FAIL expected rejection: "+name);}
    static byte[] Hex(string str){return str.Split(' ').Select(x=>Convert.ToByte(x,16)).ToArray();}
    static int Main(){try{Run();return 0;}catch(Exception ex){Console.Error.WriteLine(ex.Message);return 1;}}
    static void Run(){
        Check(PumpClient.Frame(1,3,1,0,0,17).SequenceEqual(Hex("01 03 01 00 00 11 84 3A")),"standard CRC request vector");
        byte[] captured=Hex("01 03 22 00 00 00 03 00 00 00 64 00 4B 00 65 00 00 00 64 00 4B 00 65 00 01 05 DC 00 00 00 00 00 00 00 00 00 00 BF 93");
        var w=new Wire{Reply=q=>captured};var c=new PumpClient(w,1);var s=c.Status();
        Check(s.State==3&&s.Errors==0,"captured COM8 CRC and stopped status");
        Check(s.Raw[11]==1500,"16 bit word high byte preserved");
        Check(Math.Abs(Units.Decode(s.Raw[4],s.Raw[5],false)-0.75)<1e-9,"captured total volume 0.75 mL");
        Check(Math.Abs(Units.Decode(14997,100,true)-14.997)<1e-9,"flow 14997 uL/min");
        Check(Math.Abs(Units.Decode(1,203,true)-60)<1e-9,"mL/sec to mL/min");
        Check(Math.Abs(Units.Decode(60,303,true)-1)<1e-9,"mL/hour to mL/min");
        Check(Units.Encode(1234,94,106,100,9999).SequenceEqual(new ushort[]{1234,100}),"flow encode preserves precision");
        Check(Units.Encode(5*1000,92,106,100,9999).SequenceEqual(new ushort[]{5000,100}),"volume encode");
        Reject(()=>Units.Encode(double.NaN,94,106,100,9999),"NaN rejected");
        Reject(()=>Units.Encode(double.PositiveInfinity,94,106,100,9999),"infinity rejected");
        Reject(()=>Units.Encode(0,94,106,100,9999),"zero rate rejected");
        Reject(()=>Units.Encode(14997,94,106,100,9999),"unrepresentable precision rejected");
        Reject(()=>Units.Decode(1,500,true),"unknown units rejected");
        byte[] corrupt=(byte[])captured.Clone();corrupt[4]=5;w.Reply=q=>corrupt;Reject(()=>c.Status(),"CRC corruption rejected");
        w.Reply=q=>PumpClient.Frame(2,3,2,0,3);Reject(()=>c.Read(0x100,1),"wrong slave rejected");
        w.Reply=q=>PumpClient.Frame(1,4,2,0,3);Reject(()=>c.Read(0x100,1),"wrong function rejected");
        w.Reply=q=>PumpClient.Frame(1,3,4,0,3);Reject(()=>c.Read(0x100,1),"wrong byte count rejected");
        w.Reply=q=>PumpClient.Frame(1,0x83,2);Reject(()=>c.Read(0x100,1),"Modbus exception rejected");
        w.Reply=q=>new byte[]{1,3};Reject(()=>c.Status(),"truncated response rejected");
        w.Reply=q=>q;Reject(()=>c.Write(1,1),"transmit echo cannot acknowledge write");
        w.Reply=q=>PumpClient.Frame(1,16,0,1,0,1);c.Write(1,1);Check(w.Request[1]==16&&w.Request.Length==11,"command uses function16");
        w.Reply=q=>PumpClient.Frame(1,16,0,2,0,1);Reject(()=>c.Write(1,1),"wrong write acknowledgement register");
        using(var demo=new PumpClient(new DemoWire(0),1)){
            var id=demo.Identity();Check(id.Model=="LSP12-1B"&&id.Serial=="DEMO0001"&&id.Hardware=="A-01"&&id.Firmware=="1.0.3.0","identity field boundaries");
            Check(demo.Status().State==3,"demo initial stop");demo.Command(1,1);Check(demo.Status().State==1,"demo start");
            Reject(()=>demo.Apply(new SortedDictionary<ushort,ushort[]>{{0x68,new ushort[]{2,103}}}),"settings rejected while running");
            demo.Command(13,1);Check(demo.Status().State==2,"demo pause");demo.Command(13,2);Check(demo.Status().State==1,"demo continue");demo.Command(1,0);Check(demo.Status().State==3,"demo stop");
            demo.Apply(new SortedDictionary<ushort,ushort[]>{{0x68,new ushort[]{2,103}}});Check(demo.Read(0x68,2).SequenceEqual(new ushort[]{2,103}),"paired unit write and readback");
        }
        int writes=0;w.Reply=q=>{if(q[1]==3&&q[2]==1)return captured;if(q[1]==16){writes++;return PumpClient.Frame(q[0],16,q[2],q[3],q[4],q[5]);}return PumpClient.Frame(1,3,4,0,9,0,103);};
        Reject(()=>c.Apply(new SortedDictionary<ushort,ushort[]>{{0x68,new ushort[]{2,103}},{0x6A,new ushort[]{1,103}}}),"readback mismatch aborts remaining writes");Check(writes==1,"no retry and no later write after mismatch");
        Reject(()=>new PumpClient(w,0),"broadcast address forbidden");Reject(()=>c.Read(0,126),"read quantity bounds");
        int polls=0;w.Reply=q=>{if(q[1]==16)return PumpClient.Frame(q[0],16,q[2],q[3],q[4],q[5]);polls++;var body=captured.Take(captured.Length-2).ToArray();body[6]=(byte)(polls<3?1:3);return PumpClient.Frame(body);};
        c.Command(1,0);Check(polls==3,"stop waits for device state, not just ACK");
        Reject(()=>new PumpIdentity(new ushort[27]),"incomplete identity rejected");
        Console.WriteLine("All "+passed+" checks passed.");
    }
}
