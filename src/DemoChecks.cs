using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace LongerControl {
    public sealed partial class MainForm {
        void SaveCapture(string path){using(var bmp=new Bitmap(Width,Height)){DrawToBitmap(bmp,new Rectangle(0,0,Width,Height));bmp.Save(path);}}
        async Task CloseDemo(){timer.Stop();await Task.WhenAll(Sessions.Select(p=>p.DisconnectAsync()));closing=true;Close();}
        static IEnumerable<Control> Descendants(Control root){foreach(Control c in root.Controls){yield return c;foreach(var child in Descendants(c))yield return child;}}
        async Task CaptureDialog(Form dialog,string path){using(dialog){dialog.Show(this);await Task.Delay(150);using(var bmp=new Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(bmp,new Rectangle(0,0,dialog.Width,dialog.Height));bmp.Save(path);}dialog.Close();}}
        async Task DemoChecks(string directory){var report=new List<string>();try{
            Directory.CreateDirectory(directory);timer.Stop();int enabledChanges=0,layouts=0;var buttons=Descendants(this).OfType<Button>().ToArray();foreach(var b in buttons)b.EnabledChanged+=(s,e)=>enabledChanges++;foreach(var c in cards)c.Layout+=(s,e)=>layouts++;
            if(!PerMonitorAware)throw new Exception("PerMonitorV2 DPI context was not activated");report.Add("PASS: native Windows PerMonitorV2 context");
            for(int i=0;i<6;i++){await Task.WhenAll(Sessions.Select(p=>p.PollAsync()));Render();await Task.Delay(200);}if(enabledChanges!=0||layouts!=0)throw new Exception("刷新引发布局或按钮状态变化："+enabledChanges+" / "+layouts);report.Add("PASS: 6 background polls; EnabledChanged=0; card Layout=0");
            SaveCapture(Path.Combine(directory,"dashboard-stopped.png"));var approved=await GroupControl.PrepareAsync(Sessions,PumpAction.Start,CancellationToken.None);await CaptureDialog(new ReviewDialog(approved,PumpAction.Start),Path.Combine(directory,"joint-review.png"));
            foreach(var action in new[]{PumpAction.Start,PumpAction.Pause,PumpAction.Resume}){await OperateAsync(Sessions,action,true);int expected=action==PumpAction.Pause?2:1;if(Sessions.Any(p=>!p.Fresh||p.Current.Status.State!=expected))throw new Exception("双泵 "+action+" 未达到预期状态");report.Add("PASS: joint "+action+" both state="+expected);if(action==PumpAction.Start){for(int i=0;i<5;i++){await Task.Delay(500);await Task.WhenAll(Sessions.Select(p=>p.PollAsync()));}SaveCapture(Path.Combine(directory,"dashboard-running.png"));}}
            await StopAsync(Sessions);if(Sessions.Any(p=>p.Current.Status.State!=3))throw new Exception("未确认停止");report.Add("PASS: stop all, both state=3");
            foreach(var layout in new[]{Tuple.Create(1280,680,96),Tuple.Create(1000,700,96),Tuple.Create(1920,1000,144),Tuple.Create(3000,1800,192),Tuple.Create(1124,812,96)}){TestLayout(new Size(layout.Item1,layout.Item2),layout.Item3);await Task.Delay(180);CheckViewport();foreach(var card in cards)card.CheckBounds();SaveCapture(Path.Combine(directory,"layout-"+layout.Item1+"x"+layout.Item2+"-dpi"+layout.Item3+".png"));report.Add("PASS: layout "+layout+" actual="+ClientSize+" scale="+DisplayScale);}
            SaveCapture(Path.Combine(directory,"dashboard-compact.png"));report.Add("PASS: compact card contents fit");
            TestZoom(1.5f);if(!HasScrollArea)throw new Exception("手动放大未提供滚动区域");TestZoom(0);CheckViewport();report.Add("PASS: manual zoom scrolls, automatic zoom restores fit");
            string original=Sessions[0].Port;cards[0].SetPortOptions(new[]{"COM7","COM8"},"");if(cards[0].SelectedPort!=""||Sessions[0].Port!=original)throw new Exception("空串口选择意外回到第一项");cards[0].SetPortOptions(new[]{"COM7","COM8"},"COM8");if(cards[0].SelectedPort!="COM8"||Sessions[0].Port!=original)throw new Exception("刷新列表修改了会话端口");cards[0].SetPortOptions(new[]{original},original);report.Add("PASS: selector refresh preserves empty/explicit choice without changing session");
            foreach(var tab in Descendants(this).OfType<TabControl>())tab.SelectedIndex=1;await Task.Delay(100);SaveCapture(Path.Combine(directory,"parameters.png"));await CaptureDialog(new SettingsDialog(Sessions[0].Current.Settings),Path.Combine(directory,"settings.png"));
            var times=ParameterView.Values(Sessions[0].Current);if(times[11]!="10 分钟 / 10 分钟"||times[12]!="1 秒 / 1 秒")throw new Exception("时间字段或单位转换错误");report.Add("PASS: time register offsets and manual units");
            var missing=DriverSupport.Classify(new[]{new DriverDevice{Name="FT232R USB UART",Id="USB\\VID_0403&PID_6001",Code=28}},new string[0]);await CaptureDialog(new DriverDialog(true,missing),Path.Combine(directory,"driver-helper.png"));report.Add("PASS: missing-driver guidance preview; installation disabled in demo");
        }catch(Exception ex){report.Add("FAIL: "+ex);Environment.ExitCode=1;}File.WriteAllLines(Path.Combine(directory,"ui-test-results.txt"),report);await CloseDemo();}
    }
}
