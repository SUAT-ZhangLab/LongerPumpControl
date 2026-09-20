using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text.RegularExpressions;

namespace LongerControl {
    public static class PortSelection {
        public static string[] Sort(IEnumerable<string> ports){return ports.Where(p=>!string.IsNullOrWhiteSpace(p)).Select(p=>p.ToUpperInvariant()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p=>{int n;return p.StartsWith("COM")&&int.TryParse(p.Substring(3),out n)?n:int.MaxValue;}).ThenBy(p=>p).ToArray();}
        public static string[] Assign(string[] available,string[] current,bool[] locked,string[] preferred,int changed=-1){
            var ports=Sort(available);var result=new string[current.Length];var used=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for(int i=0;i<current.Length;i++)if(locked[i]){result[i]=current[i];if(!string.IsNullOrEmpty(current[i]))used.Add(current[i]);}
            var order=Enumerable.Range(0,current.Length).OrderBy(i=>i==changed?0:1).ToArray();
            foreach(int i in order)if(!locked[i]&&ports.Contains(current[i],StringComparer.OrdinalIgnoreCase)&&used.Add(current[i]))result[i]=current[i].ToUpperInvariant();
            var ranked=Sort(preferred).Where(p=>ports.Contains(p)).Concat(ports).Distinct().ToArray();
            foreach(int i in order)if(result[i]==null){result[i]=ranked.FirstOrDefault(p=>!used.Contains(p))??"";if(result[i]!="")used.Add(result[i]);}return result;
        }
    }
    public sealed partial class MainForm {
        string[] availablePorts=new string[0],preferredPorts=new string[0];readonly bool[] portChosen=new bool[2];
        public bool PortDetectionInProgress {get{return detectingDrivers;}}
        void ReconcilePorts(int changed=-1,bool favorAdapters=false){
            if(demo)return;var old=Sessions.Select(p=>p.Port).ToArray();var requested=(string[])old.Clone();var locked=Sessions.Select(p=>p.Connected||p.Busy).ToArray();
            if(favorAdapters&&preferredPorts.Length>0)for(int i=0;i<2;i++)if(!portChosen[i]&&!locked[i])requested[i]="";
            var choices=PortSelection.Assign(availablePorts,requested,locked,preferredPorts,changed);for(int i=0;i<2;i++)Sessions[i].Port=choices[i];
            if(cards!=null){for(int i=0;i<2;i++)cards[i].SetPortOptions(availablePorts,choices[i]);if(!old.SequenceEqual(choices))SaveConfig();}
        }
        public void RefreshPorts(){if(demo)return;availablePorts=PortSelection.Sort(SerialPort.GetPortNames());ReconcilePorts();}
        void UseDriverPorts(DriverReport report){
            availablePorts=PortSelection.Sort(report.Ports);preferredPorts=PortSelection.Sort(report.Devices.Where(d=>d.Code==0).Select(d=>Regex.Match(d.Name??"",@"\bCOM\d+\b",RegexOptions.IgnoreCase).Value));ReconcilePorts(-1,true);
        }
        public void SelectPort(PumpSession pump,string chosen){int index=Array.IndexOf(Sessions,pump);if(index<0||pump.Connected||pump.Busy)return;
            if(Sessions.Any(p=>p!=pump&&(p.Connected||p.Busy)&&string.Equals(p.Port,chosen,StringComparison.OrdinalIgnoreCase))){SetBanner("该串口正在被另一台泵使用，请选择其他端口。",true);ReconcilePorts();return;}
            pump.Port=chosen;portChosen[index]=true;ReconcilePorts(index);SaveConfig();
        }
    }
}
