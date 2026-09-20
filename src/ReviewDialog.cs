using System;
using System.Drawing;
using System.IO.Ports;
using System.Windows.Forms;

namespace LongerControl {
    public static class ParameterView {
        public static string[] Names={"设备 / 序列号","串口 / 地址","当前状态","运行模式","注射器容量","截面积 / 内径","目标液量","注射流量","抽取流量","驱动力","循环次数","注射时间 / 抽取时间","分配间隔 / 循环间隔"};
        public static string[] Values(PumpSnapshot s){var r=s.Settings;double area=r[3]*Math.Pow(10,r[4]-100);return new[]{s.Identity.Model+" / "+s.Identity.Serial,s.Port+" / "+s.Address,s.Status.StateText,SettingsDialog.ModeName(r[0]),Units.Volume(r[1],r[2]),Units.Number(area)+" mm² / "+Math.Sqrt(area*4/Math.PI).ToString("0.###")+" mm",Units.Volume(r[8],r[9]),Units.Flow(r[10],r[11]),Units.Flow(r[12],r[13]),r[14]+" %",r[20]==0?"无限循环":r[20]+" 次",Time(r[21],r[22])+" / "+Time(r[23],r[24]),Time(r[16],r[17])+" / "+Time(r[18],r[19])};}
        // Manual Appendix A, printed page 53: time units are not a single decimal scale.
        static string Time(ushort v,ushort u){if(u>=97&&u<=100)return Units.Number(v*Math.Pow(10,u-100))+" 秒";if(u==101||u==102)return Units.Number(v*(u==101?.1:1))+" 分钟";if(u==103||u==104)return Units.Number(v*(u==103?.1:1))+" 小时";return "请在泵面板核对";}
    }
    public sealed class ReviewDialog : AdaptiveForm {
        public ReviewDialog(PumpSnapshot[] values,PumpAction action){
            bool dual=values.Length==2;Text=dual?"双泵参数确认":"运行参数确认";Font=new Font("Microsoft YaHei UI",9);ForeColor=Style.Ink;BackColor=Style.Canvas;StartPosition=FormStartPosition.CenterParent;ClientSize=new Size(dual?960:700,710);MinimumSize=Size;MaximizeBox=false;MinimizeBox=false;
            var root=Style.Rows(48,52,-100,64,48);root.Padding=new Padding(24,16,24,16);Controls.Add(root);
            root.Controls.Add(Style.Label(dual?"一次核对，同时"+PumpSession.ActionName(action)+"两台泵":"核对参数后"+PumpSession.ActionName(action),20,Style.Ink),0,0);
            root.Controls.Add(Style.Label("以下为本次从设备读取的参数。确认前请核对注射器、流量和目标液量。\n需要调整时，先在泵面板修改，再返回工作台重新读取。",10,Style.Muted),0,1);
            var grid=new Grid{Dock=DockStyle.Fill,ColumnCount=values.Length+1,RowCount=ParameterView.Names.Length+1,BackColor=Color.White,Padding=new Padding(12)};
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,172));for(int j=0;j<values.Length;j++)grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100f/values.Length));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute,36));grid.Controls.Add(Style.Label("核对项目",10,Style.Muted),0,0);
            for(int j=0;j<values.Length;j++){grid.Controls.Add(Style.Label(values[j].Name,13,j==0?Style.Blue:Style.Teal),j+1,0);var v=ParameterView.Values(values[j]);for(int i=0;i<v.Length;i++){var label=Style.Label(v[i],i==6||i==7?11:9,Style.Ink);if(i%2==0)label.BackColor=Style.Canvas;grid.Controls.Add(label,j+1,i+1);}}
            for(int i=0;i<ParameterView.Names.Length;i++){grid.RowStyles.Add(new RowStyle(SizeType.Percent,100f/ParameterView.Names.Length));grid.Controls.Add(Style.Label(ParameterView.Names[i],9,Style.Muted),0,i+1);}root.Controls.Add(grid,0,2);
            root.Controls.Add(Style.Label(dual?"两个串口并行发送指令，不保证硬件级同步。任一路失败将尝试停止两台，并报告各自结果。\n确认后会再次核对两台的连接、参数与状态；发生变化时不执行。":"确认后会再次核对连接、参数与状态；发生变化时不执行。",9,Style.Muted),0,3);
            var buttons=Style.Columns(-100,120,240);var cancel=Style.Button("返回修改",Style.Muted);((ActionButton)cancel).Quiet=true;cancel.Dock=DockStyle.Fill;cancel.DialogResult=DialogResult.Cancel;var go=Style.Button("确认并"+PumpSession.ActionName(action)+(dual?"两台":""),Style.Blue);((ActionButton)go).Symbol="play";go.Dock=DockStyle.Fill;go.DialogResult=DialogResult.OK;buttons.Controls.Add(cancel,1,0);buttons.Controls.Add(go,2,0);root.Controls.Add(buttons,0,4);CancelButton=cancel;
            // Do not assign AcceptButton: Enter pressed while opening the review must not start pumps.
        }
    }
    public sealed class ConnectionDialog : AdaptiveForm {
        public ConnectionDialog(PumpSession session){Text=session.Name+" · 通信设置";Font=new Font("Microsoft YaHei UI",10);ClientSize=new Size(430,260);StartPosition=FormStartPosition.CenterParent;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;
            var root=Style.Rows(42,42,42,54,38);root.Padding=new Padding(20);Controls.Add(root);
            var baud=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill};baud.Items.AddRange(new object[]{115200,38400,19200,9600});baud.SelectedItem=session.Baud;
            var parity=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill};parity.Items.AddRange(new object[]{"None","Even","Odd"});parity.SelectedItem=session.Parity.ToString();var address=new NumericUpDown{Minimum=1,Maximum=247,Value=session.Address,Dock=DockStyle.Fill};
            string[] names={"波特率","校验","设备地址"};Control[] fields={baud,parity,address};for(int i=0;i<3;i++){var row=Style.Columns(140,-100);row.Controls.Add(Style.Label(names[i],10,Style.Muted),0,0);row.Controls.Add(fields[i],1,0);root.Controls.Add(row,0,i);}
            root.Controls.Add(Style.Label("与泵面板的通信设置保持一致；数据位 8，停止位 1。",9,Style.Muted),0,3);var save=Style.Button("保存",Style.Blue);save.Dock=DockStyle.Right;save.Click+=(s,e)=>{session.Baud=(int)baud.SelectedItem;session.Parity=(Parity)Enum.Parse(typeof(Parity),parity.Text);session.Address=(byte)address.Value;DialogResult=DialogResult.OK;};root.Controls.Add(save,0,4);
        }
    }
}
