using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LongerControl;

class GroupTests {
    sealed class Fake : IControlledPump {
        readonly SemaphoreSlim gate=new SemaphoreSlim(1,1);
        public string Name {get;set;}public bool Connected {get;set;}
        public PumpSnapshot Value;public int Locks,Reads;public bool FailRead,FailStart,FailStop;
        public Func<PumpAction,Task> During;public List<PumpAction> Commands=new List<PumpAction>();
        public Fake(int i){Name="Pump "+i;Connected=true;using(var client=new PumpClient(new DemoWire(i-1),1)){Value=new PumpSnapshot{Name=Name,Port="DEMO"+i,Address=1,Connection=1,Identity=client.Identity(),Settings=client.Settings(),Status=client.Status(),CapturedAt=DateTime.UtcNow};}}
        public async Task AcquireAsync(){await gate.WaitAsync();Locks++;}
        public void Release(){Locks--;gate.Release();}
        public Task<PumpSnapshot> ReadLockedAsync(){if(Locks!=1)throw new Exception("read without lock");Reads++;if(FailRead)throw new Exception("read failed");return Task.FromResult(new PumpSnapshot{Name=Name,Port=Value.Port,Address=Value.Address,Connection=Value.Connection,Identity=Value.Identity,Settings=(ushort[])Value.Settings.Clone(),Status=new PumpStatus((ushort[])Value.Status.Raw.Clone())});}
        public async Task CommandLockedAsync(PumpAction action){if(Locks!=1)throw new Exception("command without lock");Commands.Add(action);if(During!=null)await During(action);Value.Status.Raw[1]=(ushort)(action==PumpAction.Pause?2:action==PumpAction.Stop?3:1);if(action==PumpAction.Start&&FailStart)throw new Exception("ACK lost after start");if(action==PumpAction.Stop&&FailStop)throw new Exception("stop readback lost");}
    }
    static int passed;
    static void Check(bool ok,string name){if(!ok)throw new Exception("FAIL "+name);passed++;Console.WriteLine("PASS "+name);}
    static async Task Reject(Func<Task> action,string name){try{await action();}catch{Check(true,name);return;}throw new Exception("FAIL expected rejection: "+name);}
    static Fake[] Pair(){return new[]{new Fake(1),new Fake(2)};}
    static Task<PumpSnapshot[]> Prepare(Fake[] p,PumpAction action=PumpAction.Start){return GroupControl.PrepareAsync(p,action,CancellationToken.None);}
    static Task<GroupResult> Execute(Fake[] p,PumpSnapshot[] values,PumpAction action=PumpAction.Start){return GroupControl.ExecuteAsync(p,values,action,CancellationToken.None);}
    static int Main(){try{Run().GetAwaiter().GetResult();Console.WriteLine(passed+" group checks passed");return 0;}catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
    static async Task Run(){
        var p=Pair();var approved=await Prepare(p);Check(p.All(x=>x.Reads==1&&x.Commands.Count==0&&x.Locks==0),"preparation reads both and releases locks without commands");
        var bothEntered=new TaskCompletionSource<bool>();int entered=0;foreach(var f in p)f.During=async a=>{if(a!=PumpAction.Start)return;Check(p.All(x=>x.Reads==2),"both revalidated before motion");if(Interlocked.Increment(ref entered)==2)bothEntered.TrySetResult(true);if(await Task.WhenAny(bothEntered.Task,Task.Delay(1500))!=bothEntered.Task)throw new Exception("commands were not dispatched concurrently");};
        var result=await Execute(p,approved);Check(result.Success&&entered==2&&p.All(x=>x.Locks==0),"parallel dispatch succeeds, all locks released");
        foreach(var action in new[]{PumpAction.Pause,PumpAction.Resume}){approved=await Prepare(p,action);result=await Execute(p,approved,action);Check(result.Success,"joint "+action+" succeeds");}
        p=Pair();approved=await Prepare(p);p[1].Value.Settings[8]++;await Reject(()=>Execute(p,approved),"changed second pump target aborts");Check(p.All(x=>x.Commands.Count==0&&x.Locks==0),"changed parameters send no commands to either pump");
        p=Pair();approved=await Prepare(p);p[1].Value.Connection++;await Reject(()=>Execute(p,approved),"reconnection invalidates confirmation");Check(p.All(x=>x.Commands.Count==0),"reconnection sends no start");
        p=Pair();p[1].Connected=false;await Reject(()=>Prepare(p),"disconnected participant rejected");p=Pair();p[1].Value.Status.Raw[0]=1;await Reject(()=>Prepare(p),"device fault rejected");Check(p.All(x=>x.Locks==0),"fault releases locks");
        p=Pair();p[1].Value.Status.Raw[1]=2;await Reject(()=>Prepare(p),"mixed stopped/paused states rejected");
        p=Pair();approved=await Prepare(p);p[1].Value.Status.Raw[1]=1;await Reject(()=>Execute(p,approved),"state changes while reviewing abort");Check(p.All(x=>x.Commands.Count==0),"state change prevents both commands");
        p=Pair();approved=await Prepare(p);p[1].FailRead=true;await Reject(()=>Execute(p,approved),"preflight read failure rejected");Check(p.All(x=>x.Locks==0&&x.Commands.Count==0),"read failure releases locks, sends no motion");
        p=Pair();approved=await Prepare(p);p[1].FailStart=true;result=await Execute(p,approved);Check(!result.Success&&result.Commands[0].Success&&!result.Commands[1].Success,"lost ACK is not reported as success");Check(result.Stops.All(x=>x.Success)&&p.All(x=>x.Commands.SequenceEqual(new[]{PumpAction.Start,PumpAction.Stop})&&x.Value.Status.State==3),"stop sent to BOTH including lost-ACK side");
        p=Pair();approved=await Prepare(p);p[1].FailStart=true;p[0].FailStop=true;result=await Execute(p,approved);Check(!result.Stops[0].Success&&result.Stops[1].Success,"failed stop reported separately");
        p=Pair();approved=await Prepare(p);var cancel=new CancellationTokenSource();cancel.Cancel();await Reject(()=>GroupControl.ExecuteAsync(p,approved,PumpAction.Start,cancel.Token),"cancel before dispatch aborts");Check(p.All(x=>x.Commands.Count==0&&x.Locks==0),"cancel before dispatch sends nothing");
        p=Pair();approved=await Prepare(p);cancel=new CancellationTokenSource();p[0].During=a=>{cancel.Cancel();return Task.FromResult(0);};result=await GroupControl.ExecuteAsync(p,approved,PumpAction.Start,cancel.Token);Check(result.Interrupted&&!result.Success&&result.Stops.All(x=>x.Success),"cancel during dispatch stops both");
        p=Pair();await Reject(()=>GroupControl.PrepareAsync(new[]{p[0],p[0]},PumpAction.Start,CancellationToken.None),"duplicate participant rejected");
        var session=new PumpSession(0,true);await session.ConnectAsync();int busyEvents=0;session.Changed+=()=>{if(session.Busy)busyEvents++;};for(int i=0;i<6;i++)await session.PollAsync();Check(busyEvents==0&&session.Fresh,"background polling never enters foreground Busy");await session.DisconnectAsync();
    }
}
