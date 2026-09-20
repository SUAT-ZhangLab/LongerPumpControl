using System;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using LongerControl;
class ControlProbe {
    static string logPath;
    static void Log(string text){string line=DateTime.Now.ToString("o")+" "+text;Console.WriteLine(line);File.AppendAllText(logPath,line+Environment.NewLine);}
    static void Expect(PumpClient c,int expected){for(int i=0;i<10;i++){var s=c.Status();if(s.Errors!=0)throw new Exception("Pump fault "+s.Errors);if(s.State==expected){Log("state="+expected+" errors=0 confirmed");return;}Thread.Sleep(150);}throw new Exception("Expected state "+expected+" not reached");}
    static int Main(string[] args){logPath=Path.GetFullPath("build/control-results.txt");bool all=true;
        foreach(string port in args){Log("BEGIN "+port+" controlled motion test");ushort[] baseline=null;bool wrote=false;
            using(var c=new PumpClient(new SerialWire(port,115200,Parity.None),1)){
                try{Expect(c,3);baseline=c.Settings();string backup=Path.GetFullPath("build/"+port+"-before-control.txt");File.WriteAllLines(backup,baseline.Select((v,i)=>(0x60+i).ToString("X4")+"="+v.ToString("X4")));Log("baseline saved "+backup);
                    wrote=true;
                    c.Apply(new SortedDictionary<ushort,ushort[]>{{0x60,new ushort[]{1}},{0x68,new ushort[]{10,100}},{0x6A,new ushort[]{60,100}}});
                    Log("SET verified mode=infusion target=0.010 mL flow=0.060 mL/min; syringe geometry unchanged");
                    c.Command(1,1);Expect(c,1);Thread.Sleep(1800);var moving=c.Status();Log("RUN infusion="+Units.Flow(moving.Raw[2],moving.Raw[3])+" total="+Units.Volume(moving.Raw[4],moving.Raw[5]));
                    c.Command(13,1);Expect(c,2);Thread.Sleep(400);
                    c.Command(13,2);Expect(c,1);Thread.Sleep(1500);
                    c.Command(1,0);Expect(c,3);Log("PASS start/pause/resume/stop");
                }catch(Exception ex){all=false;Log("FAIL "+ex.Message);}
                finally{if(wrote){try{c.Command(1,0);Expect(c,3);c.Apply(new SortedDictionary<ushort,ushort[]>{{0x60,new ushort[]{baseline[0]}},{0x68,new ushort[]{baseline[8],baseline[9]}},{0x6A,new ushort[]{baseline[10],baseline[11]}}});
                            var restored=c.Settings();var diff=Enumerable.Range(0,30).Where(i=>restored[i]!=baseline[i]).ToArray();
                            if(diff.Length!=0){all=false;Log("RESTORE differences: "+string.Join(",",diff.Select(i=>"0x"+(0x60+i).ToString("X4")+" "+baseline[i]+" -> "+restored[i])));}else Log("RESTORE all 30 current-scheme registers match baseline");Expect(c,3);
                        }catch(Exception ex){all=false;Log("RESTORE/STOP FAILED: "+ex.Message);}}
                }
            }Log("END "+port);
            if(!all)break;
        }return all?0:1;
    }
}
