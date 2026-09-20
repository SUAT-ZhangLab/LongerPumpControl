using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
[assembly:System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]

namespace LongerControl {
    static class Program {
        [STAThread] static void Main(string[] args){
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException+=(s,e)=>MessageBox.Show(e.Exception.Message,"程序错误");
            try{Application.Run(new MainForm(args.Contains("--demo"),args.Contains("--screenshot")?args.Last():null,args.Contains("--ui-test")?args.Last():null));}
            catch(Exception ex){MessageBox.Show("程序无法继续运行："+ex.Message,"Longer Pump Control",MessageBoxButtons.OK,MessageBoxIcon.Error);}
        }
    }
    public sealed partial class MainForm : AdaptiveForm {
        public readonly PumpSession[] Sessions;
        readonly PumpCard[] cards;readonly TextBox log;readonly Label summary,banner;
        readonly Button connectBoth,readBoth,jointStart,jointPause,jointResume,stopAll;
        readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
        readonly string dataRoot,csvPath;readonly bool demo;bool closing,exiting,working,stopping;CancellationTokenSource operation;
        readonly Dictionary<string,string> previousStates=new Dictionary<string,string>();
        public bool Working {get{return working||stopping||exiting;}}
        public MainForm(bool demo,string screenshot,string testOutput){
            this.demo=demo;DoubleBuffered=true;Text="Longer Pump Control · 双泵工作台 0.2.1"+(demo?" [模拟模式]":"");Font=new Font("Microsoft YaHei UI",9);BackColor=Style.Canvas;ForeColor=Style.Ink;ClientSize=new Size(1220,860);LogicalMinimum=new Size(1124,812);StartPosition=FormStartPosition.CenterScreen;
            dataRoot=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"LongerPumpControl");if(demo&&testOutput!=null)dataRoot=Path.GetFullPath(testOutput);
            Directory.CreateDirectory(Path.Combine(dataRoot,"logs"));csvPath=Path.Combine(dataRoot,"logs",DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+(demo?"-demo":"")+".csv");
            File.WriteAllText(csvPath,"time,mode,pump,port,address,event,state,error,direction,infusion_ml_min,infused_ml,withdrawal_ml_min,withdrawn_ml,raw_registers\r\n",new System.Text.UTF8Encoding(true));
            Sessions=new[]{new PumpSession(0,demo),new PumpSession(1,demo)};LoadConfig();
            var root=Style.Rows(70,112,-100,70,30);Controls.Add(root);
            var header=Style.Columns(-100,280);header.Padding=new Padding(24,7,24,7);header.BackColor=Style.Navy;
            var title=Style.Rows(36,18);title.Controls.Add(Style.Label("Zhang Lab  /  双泵工作台",19,Color.White),0,0);title.Controls.Add(Style.Label("LSP12-1B     ·     连接 / 核对 / 运行",9,Color.FromArgb(167,191,216)),0,1);header.Controls.Add(title,0,0);
            var headStatus=Style.Rows(30,24);summary=Style.Label("0 / 2 台通信正常",11,Color.White);summary.TextAlign=ContentAlignment.MiddleRight;var modeLabel=Style.Label(demo?"模拟模式 · 不连接真实设备":"本地控制 · 每秒监测",9,demo?Color.FromArgb(255,202,104):Color.FromArgb(167,191,216));modeLabel.TextAlign=ContentAlignment.MiddleRight;headStatus.Controls.Add(summary,0,0);headStatus.Controls.Add(modeLabel,0,1);header.Controls.Add(headStatus,1,0);root.Controls.Add(header,0,0);
            var joint=Style.Rows(28,40,28);joint.Padding=new Padding(24,6,24,6);root.Controls.Add(joint,0,1);
            var jointTitle=Style.Columns(100,-100);jointTitle.Controls.Add(Style.Label("联合控制",13,Style.Ink),0,0);jointTitle.Controls.Add(Style.Label("01 连接两台    →    02 同窗核对参数    →    03 并行执行并回读",9,Style.Muted),1,0);joint.Controls.Add(jointTitle,0,0);
            var controls=Style.Columns(130,125,-100,118,118,158);
            connectBoth=Make("连接两台",Style.Blue,"link",true);readBoth=Make("读取两台参数",Style.Blue,null,true);jointStart=Make("核对参数并启动两台",Style.Blue,"play",false);jointPause=Make("暂停两台",Style.Ink,"pause",true);jointResume=Make("继续两台",Style.Teal,"play",true);stopAll=Make("停止全部",Style.Red,"stop",false);
            Button[] buttons={connectBoth,readBoth,jointStart,jointPause,jointResume,stopAll};for(int i=0;i<buttons.Length;i++)controls.Controls.Add(buttons[i],i,0);joint.Controls.Add(controls,0,1);
            connectBoth.Click+=async(s,e)=>await RunAsync(async()=>{CheckPorts(Sessions);await Task.WhenAll(Sessions.Where(p=>!p.Connected).Select(p=>p.ConnectAsync()));SaveConfig();SetBanner("连接完成，请核对两台参数。",false);});
            readBoth.Click+=async(s,e)=>await RunAsync(async()=>{await Task.WhenAll(Sessions.Select(p=>p.RefreshAsync()));SetBanner("两台参数已更新；可在同一窗口确认并启动。",false);});
            jointStart.Click+=async(s,e)=>await OperateAsync(Sessions,PumpAction.Start,false);jointPause.Click+=async(s,e)=>await OperateAsync(Sessions,PumpAction.Pause,false);jointResume.Click+=async(s,e)=>await OperateAsync(Sessions,PumpAction.Resume,false);stopAll.Click+=async(s,e)=>await StopAsync(Sessions);
            banner=Style.Label("请选择串口并连接。连接与读取参数不会启动泵。",9,Style.Muted);joint.Controls.Add(banner,0,2);
            var cardGrid=Style.Columns(-50,-50);cardGrid.Padding=new Padding(24,0,24,8);cards=new[]{new PumpCard(this,Sessions[0],0),new PumpCard(this,Sessions[1],1)};for(int i=0;i<2;i++)cardGrid.Controls.Add(cards[i],i,0);root.Controls.Add(cardGrid,0,2);
            var logBox=Style.Rows(25,-100);logBox.Padding=new Padding(24,0,24,0);var logHead=Style.Columns(-100,180);logHead.Controls.Add(Style.Label("操作记录",10,Style.Ink),0,0);var export=new LinkLabel{Text="导出本次 CSV 日志",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleRight};export.LinkClicked+=(s,e)=>Export();logHead.Controls.Add(export,1,0);logBox.Controls.Add(logHead,0,0);log=new TextBox{Dock=DockStyle.Fill,ReadOnly=true,Multiline=true,ScrollBars=ScrollBars.Vertical,BackColor=Color.White,BorderStyle=BorderStyle.None,Font=new Font("Microsoft YaHei UI",9)};logBox.Controls.Add(log,0,1);root.Controls.Add(logBox,0,3);
            var footer=Style.Columns(-100,120,174,100);footer.Padding=new Padding(24,0,24,0);footer.Controls.Add(Style.Label("两个独立串口 · 软件并行控制非硬件同步 · 参数写入仍为试验功能",8,Style.Muted),0,0);var help=new LinkLabel{Text="使用说明",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleRight};help.LinkClicked+=(s,e)=>{try{System.Diagnostics.Process.Start(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"使用说明.txt"));}catch(Exception ex){SetBanner(ex.Message,true);}};footer.Controls.Add(CreateZoomSelector(),1,0);footer.Controls.Add(help,3,0);ConfigureDriverHelp(footer);root.Controls.Add(footer,0,4);
            foreach(var session in Sessions){var p=session;p.Changed+=Render;p.Notice+=message=>Log(p,message,null,true);p.Sampled+=sample=>{string key=sample.Status.State+"/"+sample.Status.Errors;string old;if(!previousStates.TryGetValue(p.Name,out old)||old!=key){previousStates[p.Name]=key;Log(p,"状态："+sample.Status.StateText+" · "+sample.Status.ErrorText,sample,true);}else Log(p,"状态采样",sample,false);};}
            timer.Interval=1000;timer.Tick+=async(s,e)=>{await Task.WhenAll(Sessions.Select(p=>p.PollAsync()));Render();};timer.Start();Render();
            Shown+=async(s,e)=>{Log(null,demo?"模拟模式启动；所有操作均为演示。":"程序已启动，请确认串口与线缆。",null,true);if(demo){await RunAsync(async()=>await Task.WhenAll(Sessions.Select(p=>p.ConnectAsync())));if(testOutput!=null){await DemoChecks(testOutput);return;}if(screenshot!=null){await Task.Delay(1600);SaveCapture(screenshot);await CloseDemo();}}else await DetectDriversAsync();};FormClosing+=OnClosing;
        }
        static Button Make(string text,Color color,string symbol,bool quiet){var b=(ActionButton)Style.Button(text,color);b.Symbol=symbol;b.Quiet=quiet;b.Dock=DockStyle.Fill;return b;}
        public void SetBanner(string text,bool error){Style.Text(banner,text);Style.SetColor(banner,error?Style.Red:Style.Muted);}
        void CheckPorts(PumpSession[] selected){if(demo)return;if(detectingDrivers)throw new IOException("正在识别 USB 串口，请稍候再连接。");foreach(var p in selected){if(string.IsNullOrWhiteSpace(p.Port))throw new IOException(p.Name+"未选择串口。");if(Sessions.Any(other=>other!=p&&other.Port==p.Port&&(other.Connected||selected.Contains(other))))throw new IOException("两台泵需要选择不同的串口。");}}
        public async Task ConnectAsync(PumpSession p){await RunAsync(async()=>{CheckPorts(new[]{p});await p.ConnectAsync();SaveConfig();});}
        public async Task DisconnectAsync(PumpSession p){if((!p.Fresh||p.Current.Status.State!=3)&&MessageBox.Show("断开不会停止泵。确认断开 "+p.Name+"？","断开连接",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;await RunAsync(()=>p.DisconnectAsync());}
        public async Task RunAsync(Func<Task> work){if(Working)return;working=true;Render();try{await work();}catch(OperationCanceledException){SetBanner("操作已取消。",false);}catch(Exception ex){SetBanner(ex.Message,true);Log(null,ex.Message,null,true);}finally{working=false;Render();}}
        public async Task OperateAsync(PumpSession[] selected,PumpAction action,bool autoConfirm){
            if(autoConfirm&&!demo)throw new InvalidOperationException("自动确认仅限模拟模式。");
            await RunAsync(async()=>{operation=new CancellationTokenSource();try{
                SetBanner("正在读取并检查"+(selected.Length==2?"两台":"设备")+"参数…",false);var approved=await GroupControl.PrepareAsync(selected,action,operation.Token);
                if(action==PumpAction.Start||action==PumpAction.Resume){using(var dialog=new ReviewDialog(approved,action)){if(!autoConfirm&&dialog.ShowDialog(this)!=DialogResult.OK){SetBanner("已取消，未发送运行指令。",false);return;}}}
                var result=await GroupControl.ExecuteAsync(selected,approved,action,operation.Token);
                var lines=result.Commands.Select(r=>r.Name+"："+PumpSession.ActionName(action)+(r.Success?"已确认":"失败（"+r.Message+"）")).Concat(result.Stops.Select(r=>r.Name+"："+(r.Success?"已确认停止":"未确认停止（"+r.Message+"），请检查泵面板"))).ToArray();
                SetBanner(result.Success?string.Join("；",lines):"操作未全部成功；"+string.Join("；",result.Stops.Select(r=>r.Name+(r.Success?"已停止":"未确认停止，请检查面板"))),!result.Success);Log(null,string.Join("；",lines),null,true);
                if(!result.Success&&!autoConfirm)MessageBox.Show(result.Message+"\n\n"+string.Join("\n",lines),"操作结果",MessageBoxButtons.OK,MessageBoxIcon.Warning);
            }finally{operation.Dispose();operation=null;}});
        }
        public async Task StopAsync(PumpSession[] selected){
            if(stopping)return;stopping=true;if(operation!=null)operation.Cancel();Render();
            try{var results=await Task.WhenAll(selected.Where(p=>p.Connected).Select(async p=>{try{await p.StopAsync();return p.Name+"已确认停止";}catch(Exception ex){return p.Name+"未确认停止："+ex.Message;}}));bool failed=selected.Any(p=>p.Connected&&(!p.Fresh||p.Current.Status.State!=3));SetBanner(results.Length==0?"没有已连接的泵。":string.Join("；",results),failed);Log(null,string.Join("；",results),null,true);}finally{stopping=false;Render();}
        }
        public async Task EditAsync(PumpSession p){await RunAsync(async()=>{await p.RefreshAsync();var reviewed=p.Current;using(var dialog=new SettingsDialog((ushort[])reviewed.Settings.Clone()))if(dialog.ShowDialog(this)==DialogResult.OK)await p.ApplyAsync(reviewed,dialog.Changes);});}
        public void Render(){if(cards==null)return;foreach(var card in cards)card.Render();bool all=Sessions.All(p=>p.Connected),ready=Sessions.All(p=>p.Fresh&&p.Current.Status.Errors==0),idle=!Working;
            Style.Enabled(connectBoth,idle&&!detectingDrivers&&Sessions.Any(p=>!p.Connected));Style.Enabled(readBoth,idle&&all);Style.Enabled(jointStart,idle&&ready&&Sessions.All(p=>p.Current.Status.State==3));Style.Enabled(jointPause,idle&&ready&&Sessions.All(p=>new[]{1,4,5}.Contains(p.Current.Status.State)));Style.Enabled(jointResume,idle&&ready&&Sessions.All(p=>p.Current.Status.State==2));Style.Enabled(stopAll,!stopping&&Sessions.Any(p=>p.Connected));Style.Text(summary,Sessions.Count(p=>p.Fresh)+" / 2 台通信正常");}
        public void SaveConfig(){if(demo)return;try{new XDocument(new XElement("connections",Sessions.Select(p=>new XElement("pump",new XAttribute("port",p.Port),new XAttribute("baud",p.Baud),new XAttribute("parity",p.Parity),new XAttribute("address",p.Address))))).Save(Path.Combine(dataRoot,"connections.xml"));}catch(Exception ex){SetBanner("连接配置未保存："+ex.Message,true);}}
        void LoadConfig(){
            if(demo){for(int i=0;i<2;i++)Sessions[i].Port="DEMO"+(i+1);return;}
            availablePorts=PortSelection.Sort(SerialPort.GetPortNames());
            try{string path=Path.Combine(dataRoot,"connections.xml");if(File.Exists(path)){int i=0;foreach(var x in XDocument.Load(path).Root.Elements("pump").Take(2)){var p=Sessions[i];int b,a;Parity par;p.Port=(string)x.Attribute("port")??"";portChosen[i++]=availablePorts.Contains(p.Port);if(int.TryParse((string)x.Attribute("baud"),out b)&&new[]{115200,38400,19200,9600}.Contains(b))p.Baud=b;if(int.TryParse((string)x.Attribute("address"),out a)&&a>=1&&a<=247)p.Address=(byte)a;if(Enum.TryParse((string)x.Attribute("parity"),out par))p.Parity=par;}}}catch{}
            ReconcilePorts();
        }
        void Log(PumpSession p,string message,PumpSnapshot sample,bool visible){if(visible&&log!=null){log.AppendText(DateTime.Now.ToString("HH:mm:ss")+"  "+(p==null?"系统":p.Name)+"   "+message+Environment.NewLine);if(log.TextLength>60000)log.Text=log.Text.Substring(log.TextLength-40000);}
            var fields=new List<string>{DateTime.Now.ToString("o"),demo?"DEMO":"LIVE",p==null?"系统":p.Name,p==null?"":p.Port,p==null?"":p.Address.ToString(),message};if(sample!=null){var s=sample.Status;var r=s.Raw;fields.AddRange(new[]{s.StateText,s.ErrorText,s.Direction,Units.Flow(r[2],r[3]),Units.Volume(r[4],r[5]),Units.Flow(r[6],r[7]),Units.Volume(r[8],r[9]),string.Join(" ",r.Select(v=>v.ToString("X4")))});}try{File.AppendAllText(csvPath,string.Join(",",fields.Select(v=>"\""+v.Replace("\"","\"\"")+"\""))+"\r\n",System.Text.Encoding.UTF8);}catch(Exception ex){SetBanner("日志写入失败："+ex.Message,true);}}
        void Export(){using(var dialog=new SaveFileDialog{Filter="CSV 日志|*.csv",FileName=Path.GetFileName(csvPath)})if(dialog.ShowDialog(this)==DialogResult.OK)try{File.Copy(csvPath,dialog.FileName,true);SetBanner("日志已导出。",false);}catch(Exception ex){SetBanner(ex.Message,true);}}
        async void OnClosing(object sender,FormClosingEventArgs e){if(closing)return;e.Cancel=true;if(Working)return;exiting=true;Render();try{if(Sessions.Any(p=>p.Connected)){var choice=MessageBox.Show("退出前是否停止已连接的泵？\n\n是：停止并回读确认。\n否：断开连接，泵可能继续运行。\n取消：返回工作台。","退出工作台",MessageBoxButtons.YesNoCancel,MessageBoxIcon.Question);if(choice==DialogResult.Cancel)return;if(choice==DialogResult.Yes){await StopAsync(Sessions);if(Sessions.Any(p=>p.Connected&&(!p.Fresh||p.Current.Status.State!=3))){MessageBox.Show("至少一台泵未确认停止，请检查设备面板。程序保持打开。");return;}}}timer.Stop();SaveConfig();await Task.WhenAll(Sessions.Select(p=>p.DisconnectAsync()));closing=true;Close();}catch(Exception ex){SetBanner(ex.Message,true);}finally{exiting=false;Render();}}
    }
}
