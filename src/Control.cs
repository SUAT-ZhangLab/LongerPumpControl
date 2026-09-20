using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LongerControl {
    public enum PumpAction { Start, Pause, Resume, Stop }
    public sealed class PumpSnapshot {
        public string Name,Port; public byte Address; public int Connection;
        public PumpIdentity Identity; public PumpStatus Status; public ushort[] Settings;
        public DateTime CapturedAt;
        public bool SameConfiguration(PumpSnapshot other) {
            return other!=null && Connection==other.Connection && Port==other.Port && Address==other.Address &&
                Identity.Serial==other.Identity.Serial && Identity.Model==other.Identity.Model && Settings.SequenceEqual(other.Settings);
        }
    }
    public interface IControlledPump {
        string Name {get;} bool Connected {get;}
        Task AcquireAsync(); void Release();
        Task<PumpSnapshot> ReadLockedAsync();
        Task CommandLockedAsync(PumpAction action);
    }
    public sealed class PumpOutcome {
        public string Name; public bool Success; public string Message;
    }
    public sealed class GroupResult {
        public bool Success,Sent,Interrupted; public string Message;
        public PumpOutcome[] Commands=new PumpOutcome[0],Stops=new PumpOutcome[0];
    }
    public static class GroupControl {
        public static void CheckState(PumpSnapshot s,PumpAction action) {
            if(action==PumpAction.Stop)return;
            if(s.Status.Errors!=0)throw new InvalidOperationException(s.Name+"报告故障："+s.Status.ErrorText);
            int state=s.Status.State;
            if(action==PumpAction.Start&&state!=3)throw new InvalidOperationException(s.Name+"尚未停止，请先停止再启动。");
            if(action==PumpAction.Resume&&state!=2)throw new InvalidOperationException(s.Name+"尚未暂停，不能联合继续。");
            if(action==PumpAction.Pause&&state!=1&&state!=4&&state!=5)throw new InvalidOperationException(s.Name+"当前不在运行中。");
        }
        static void CheckPumps(IControlledPump[] pumps) {
            if(pumps.Length<1||pumps.Length>2||pumps.Distinct().Count()!=pumps.Length)throw new ArgumentException("请选择一台或两台不同的泵。");
            foreach(var p in pumps)if(!p.Connected)throw new InvalidOperationException(p.Name+"尚未连接。");
        }
        public static async Task<PumpSnapshot[]> PrepareAsync(IControlledPump[] pumps,PumpAction action,CancellationToken token) {
            CheckPumps(pumps);int acquired=0;
            try {
                foreach(var p in pumps){token.ThrowIfCancellationRequested();await p.AcquireAsync();acquired++;}
                token.ThrowIfCancellationRequested();var values=await Task.WhenAll(pumps.Select(p=>p.ReadLockedAsync()));
                token.ThrowIfCancellationRequested();foreach(var s in values)CheckState(s,action);return values;
            } finally {for(int i=acquired-1;i>=0;i--)pumps[i].Release();}
        }
        static async Task<PumpOutcome> Attempt(IControlledPump p,PumpAction action) {
            try{await p.CommandLockedAsync(action);return new PumpOutcome{Name=p.Name,Success=true,Message="状态回读已确认"};}
            catch(Exception e){return new PumpOutcome{Name=p.Name,Success=false,Message=e.Message};}
        }
        public static async Task<GroupResult> ExecuteAsync(IControlledPump[] pumps,PumpSnapshot[] approved,PumpAction action,CancellationToken token) {
            CheckPumps(pumps);
            if(approved==null||approved.Length!=pumps.Length)throw new ArgumentException("缺少完整的参数确认记录。");
            int acquired=0;var result=new GroupResult();
            try {
                foreach(var p in pumps){token.ThrowIfCancellationRequested();await p.AcquireAsync();acquired++;}
                token.ThrowIfCancellationRequested();
                // Revalidate BOTH devices before sending the first command. Polling does not hold these locks while the dialog is open.
                var fresh=await Task.WhenAll(pumps.Select(p=>p.ReadLockedAsync()));
                for(int i=0;i<pumps.Length;i++){
                    if(!approved[i].SameConfiguration(fresh[i]))throw new InvalidOperationException(pumps[i].Name+"的连接或参数已变化，请重新确认。");
                    CheckState(fresh[i],action);
                }
                token.ThrowIfCancellationRequested();result.Sent=true;
                result.Commands=await Task.WhenAll(pumps.Select(p=>Attempt(p,action)));
                result.Interrupted=token.IsCancellationRequested;
                result.Success=result.Commands.All(r=>r.Success)&&!result.Interrupted;
                if(!result.Success){
                    // An acknowledgement can be lost AFTER motion began. Stop every participant, including the failed side.
                    result.Stops=await Task.WhenAll(pumps.Select(p=>Attempt(p,PumpAction.Stop)));
                    result.Message=result.Interrupted?"操作已取消，已向参与泵发送停止。":"联合操作未全部成功，已向参与泵发送停止。";
                } else result.Message="两路操作均已回读确认。";
                return result;
            } finally {for(int i=acquired-1;i>=0;i--)pumps[i].Release();}
        }
    }
    public sealed class PumpSession : IControlledPump {
        readonly SemaphoreSlim gate=new SemaphoreSlim(1,1);readonly bool demo;readonly int index;
        PumpClient client;int busy,generation;
        public event Action Changed;public event Action<string> Notice;public event Action<PumpSnapshot> Sampled;
        public string Name {get;private set;}
        public string Port="";public int Baud=115200;public Parity Parity=Parity.None;public byte Address=1;
        public bool Connected {get{return client!=null;}} public bool Busy {get{return busy>0;}}
        public PumpSnapshot Current {get;private set;}public DateTime LastSample {get;private set;}public DateTime LastConfiguration {get;private set;}
        public string Error {get;private set;}
        public bool Fresh {get{return Connected&&Current!=null&&Error==null&&(DateTime.UtcNow-LastSample).TotalSeconds<4;}}
        public bool Simulation {get{return demo;}}
        public PumpSession(int index,bool demo){this.index=index;this.demo=demo;Name="泵 "+(index+1);}
        void Notify(){if(Changed!=null)Changed();}
        void Report(string text){if(Notice!=null)Notice(text);}
        void Fail(Exception ex){Error=ex.Message;Notify();Report(ex.Message);}
        public async Task AcquireAsync(){busy++;Notify();try{await gate.WaitAsync();}catch{busy--;Notify();throw;}}
        public void Release(){gate.Release();busy--;Notify();}
        public async Task ConnectAsync(){
            if(Connected||Busy)return;await AcquireAsync();PumpClient candidate=null;
            try {
                var configPort=Port;var configBaud=Baud;var configParity=Parity;var configAddress=Address;
                var snapshot=await Task.Run(()=>{
                    candidate=new PumpClient(demo?(IWire)new DemoWire(index):new SerialWire(configPort,configBaud,configParity),configAddress);
                    var identity=candidate.Identity();if(identity.Model!="LSP12-1B")throw new IOException("检测到型号 "+identity.Model+"；本版本仅适配 LSP12-1B。");
                    return new PumpSnapshot{Name=Name,Port=configPort,Address=configAddress,Identity=identity,Settings=candidate.Settings(),Status=candidate.Status(),CapturedAt=DateTime.UtcNow};
                });
                client=candidate;candidate=null;snapshot.Connection=++generation;LastConfiguration=DateTime.UtcNow;Publish(snapshot);
                Report("已连接 "+snapshot.Identity.Model+" · SN "+snapshot.Identity.Serial+" · 固件 "+snapshot.Identity.Firmware);
            }catch(Exception ex){if(candidate!=null)candidate.Dispose();Fail(ex);throw;}finally{Release();}
        }
        public async Task DisconnectAsync(){await AcquireAsync();try{var old=client;client=null;generation++;if(old!=null)await Task.Run(()=>old.Dispose());Current=null;Error=null;Notify();Report("已断开连接");}finally{Release();}}
        PumpSnapshot Snapshot(PumpStatus status,ushort[] settings,PumpIdentity id){return new PumpSnapshot{Name=Name,Port=Port,Address=Address,Connection=generation,Identity=id,Status=status,Settings=settings,CapturedAt=DateTime.UtcNow};}
        void Publish(PumpSnapshot value){Current=value;LastSample=value.CapturedAt;Error=null;Notify();if(Sampled!=null)Sampled(value);}
        public async Task<PumpSnapshot> ReadLockedAsync(){
            if(client==null)throw new IOException(Name+"连接已断开。");
            try {var snapshot=await Task.Run(()=>Snapshot(client.Status(),client.Settings(),client.Identity()));LastConfiguration=DateTime.UtcNow;Publish(snapshot);return snapshot;}
            catch(Exception ex){Fail(ex);throw;}
        }
        public async Task RefreshAsync(){await AcquireAsync();try{await ReadLockedAsync();}finally{Release();}}
        public async Task PollAsync(){
            // Background telemetry must NEVER toggle Busy/Enabled: that was the source of periodic flashing.
            if(!Connected||!gate.Wait(0))return;
            try {var status=await Task.Run(()=>client.Status());Publish(Snapshot(status,Current.Settings,Current.Identity));}
            catch(Exception ex){bool newError=Error!=ex.Message;Error=ex.Message;Notify();if(newError)Report(ex.Message);}
            finally{gate.Release();}
        }
        public async Task CommandLockedAsync(PumpAction action){
            if(client==null)throw new IOException(Name+"连接已断开，无法确认停止或运行状态。");
            try {
                ushort reg=(ushort)(action==PumpAction.Start||action==PumpAction.Stop?1:13);
                ushort value=(ushort)(action==PumpAction.Stop?0:action==PumpAction.Resume?2:1);
                await Task.Run(()=>client.Command(reg,value));var status=await Task.Run(()=>client.Status());
                Publish(Snapshot(status,Current.Settings,Current.Identity));Report(ActionName(action)+"完成 · "+status.StateText);
            }catch(Exception ex){Fail(ex);throw;}
        }
        public async Task StopAsync(){await AcquireAsync();try{await CommandLockedAsync(PumpAction.Stop);}finally{Release();}}
        public async Task ApplyAsync(PumpSnapshot reviewed,SortedDictionary<ushort,ushort[]> changes){
            await AcquireAsync();try{var now=await ReadLockedAsync();if(!reviewed.SameConfiguration(now))throw new IOException("参数已变化，请重新读取。");await Task.Run(()=>client.Apply(changes));await ReadLockedAsync();Report("参数写入已回读校验");}catch(Exception ex){Fail(ex);throw;}finally{Release();}
        }
        public static string ActionName(PumpAction action){return action==PumpAction.Start?"启动":action==PumpAction.Pause?"暂停":action==PumpAction.Resume?"继续":"停止";}
    }
}
