using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LongerControl;
class JointHardwareProbe {
    static int Main(string[] args){try{if(args.Length!=1||args[0]!="--authorized-motion")throw new Exception("Explicit authorized-motion flag required");return Run().GetAwaiter().GetResult();}catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
    static async Task<int> Run(){
        var pumps=new[]{new PumpSession(0,false){Port="COM8"},new PumpSession(1,false){Port="COM9"}};bool motion=false;int code=0;PumpSnapshot[] before=null;
        foreach(var session in pumps){var p=session;p.Notice+=m=>Console.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff")+" "+p.Name+" "+m);}
        try{
            await Task.WhenAll(pumps.Select(p=>p.ConnectAsync()));before=await GroupControl.PrepareAsync(pumps,PumpAction.Start,CancellationToken.None);
            if(before[0].Identity.Serial!="202604030004"||before[1].Identity.Serial!="202604030005")throw new Exception("Unexpected pump identity; motion not authorized for this pair");
            Console.WriteLine("Original settings captured; no parameter writes will be made.");
            foreach(var action in new[]{PumpAction.Start,PumpAction.Pause,PumpAction.Resume}){
                var approved=await GroupControl.PrepareAsync(pumps,action,CancellationToken.None);motion=true;var result=await GroupControl.ExecuteAsync(pumps,approved,action,CancellationToken.None);if(!result.Success)throw new Exception("Joint "+action+" failed: "+string.Join("; ",result.Commands.Select(r=>r.Name+" "+r.Message)));
                Console.WriteLine("PASS joint "+action+" states="+string.Join(",",pumps.Select(p=>p.Current.Status.State))+" faults="+string.Join(",",pumps.Select(p=>p.Current.Status.Errors)));await Task.Delay(250);
            }
        }catch(Exception ex){Console.Error.WriteLine("TEST FAILED: "+ex.Message);code=1;}
        if(motion){var results=await Task.WhenAll(pumps.Where(p=>p.Connected).Select(async p=>{try{await p.StopAsync();await p.RefreshAsync();return p.Fresh&&p.Current.Status.State==3&&p.Current.Status.Errors==0;}catch(Exception ex){Console.Error.WriteLine(p.Name+" STOP NOT CONFIRMED: "+ex.Message);return false;}}));if(results.Length!=2||results.Any(ok=>!ok))code=1;else Console.WriteLine("PASS both pumps stopped, faults=0");}
        if(before!=null)for(int i=0;i<2;i++){if(!before[i].SameConfiguration(pumps[i].Current)){Console.Error.WriteLine("Settings changed on "+pumps[i].Name);code=1;}else Console.WriteLine("PASS "+pumps[i].Name+" all 30 configuration registers unchanged");}
        await Task.WhenAll(pumps.Select(p=>p.DisconnectAsync()));return code;
    }
}
