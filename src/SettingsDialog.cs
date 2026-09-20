using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
namespace LongerControl {
    public sealed class SettingsDialog : AdaptiveForm {
        public SortedDictionary<ushort,ushort[]> Changes=new SortedDictionary<ushort,ushort[]>();
        readonly ushort[] original;readonly Dictionary<ushort,TextBox> inputs=new Dictionary<ushort,TextBox>();readonly Dictionary<ushort,double> initial=new Dictionary<ushort,double>();readonly ComboBox mode;
        public static string ModeName(int mode){return mode==1?"注射":mode==2?"抽取":mode==3?"先注射后抽取":mode==4?"先抽取后注射":"未知模式 "+mode;}
        public SettingsDialog(ushort[] r){original=r;Text="设置当前方案 · 保存后回读校验";Font=new Font("Microsoft YaHei UI",9);ClientSize=new Size(540,610);StartPosition=FormStartPosition.CenterParent;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;
            var grid=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(24),ColumnCount=2,RowCount=10,AutoScroll=true};grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,260));grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));Controls.Add(grid);for(int i=0;i<8;i++)grid.RowStyles.Add(new RowStyle(SizeType.Absolute,42));grid.RowStyles.Add(new RowStyle(SizeType.Absolute,150));grid.RowStyles.Add(new RowStyle(SizeType.Absolute,42));
            grid.Controls.Add(Style.Label("运行模式",10,Style.Ink));mode=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=200};mode.Items.AddRange(new object[]{"注射","抽取","先注射后抽取","先抽取后注射"});mode.SelectedIndex=r[0]>=1&&r[0]<=4?r[0]-1:-1;grid.Controls.Add(mode);
            Add(grid,0x61,"注射器容量（mL）",Units.Decode(r[1],r[2],false));
            Add(grid,0x63,"注射器截面积（mm²）",r[3]*Math.Pow(10,r[4]-100));
            Add(grid,0x68,"目标液量（mL）",Units.Decode(r[8],r[9],false));
            Add(grid,0x6A,"注射流量（mL/min）",Units.Decode(r[10],r[11],true));
            Add(grid,0x6C,"抽取流量（mL/min）",Units.Decode(r[12],r[13],true));
            Add(grid,0x6E,"驱动力（0–100%）",r[14]);
            Add(grid,0x74,"循环次数（0 表示无限）",r[20]);
            var note=Style.Label("参数编辑为试验功能：固件 1.0.3.0 样机拒绝写入，\n当前建议在泵面板设置，再使用本软件读取核对。\n\n截面积 = π × 内径² ÷ 4，必须与实际注射器一致。\n保存只写已改项；时间与间隔保留泵面板当前值。\n多项写入中断后，部分参数可能已生效，请重新读取。",9,Style.Muted);note.Height=140;note.MaximumSize=new Size(480,140);grid.Controls.Add(note);grid.SetColumnSpan(note,2);
            var save=Style.Button("保存并校验",Style.Blue);save.Click+=(s,e)=>Save();grid.Controls.Add(save);var cancel=Style.Button("取消",Style.Muted);cancel.DialogResult=DialogResult.Cancel;grid.Controls.Add(cancel);CancelButton=cancel;
        }
        void Add(TableLayoutPanel grid,ushort reg,string label,double value){grid.Controls.Add(Style.Label(label,10,Style.Ink));var box=new TextBox{Text=Units.Number(value),Width=200};inputs[reg]=box;initial[reg]=value;grid.Controls.Add(box);}
        void Save(){try{Changes.Clear();if(mode.SelectedIndex<0)throw new ArgumentException("请选择运行模式");if(mode.SelectedIndex+1!=original[0])Changes[0x60]=new[]{(ushort)(mode.SelectedIndex+1)};
                var parsed=new Dictionary<ushort,double>();foreach(var item in inputs){double val;if(!double.TryParse(item.Value.Text,NumberStyles.Float,CultureInfo.InvariantCulture,out val)||double.IsNaN(val)||double.IsInfinity(val))throw new ArgumentException("请输入有效数字，小数点使用 .");parsed[item.Key]=val;if(val==initial[item.Key])continue;
                    if(item.Key==0x6E||item.Key==0x74){int max=item.Key==0x6E?100:30000;if(val<0||val>max||val!=Math.Truncate(val))throw new ArgumentException("驱动力为 0–100 整数，循环次数为 0–30000 整数。");Changes[item.Key]=new[]{(ushort)val};}
                    else if(item.Key==0x63)Changes[item.Key]=Units.Encode(val,94,100,100,9999);
                    else Changes[item.Key]=Units.Encode(val*1000,item.Key==0x6A||item.Key==0x6C?94:92,106,100,9999);
                }
                if(parsed[0x68]>parsed[0x61]&&(Changes.ContainsKey(0x68)||Changes.ContainsKey(0x61)))throw new ArgumentException("目标液量不可超过所设注射器容量。");
                if(Changes.Count==0){DialogResult=DialogResult.Cancel;Close();return;}
                if(MessageBox.Show("将写入 "+Changes.Count+" 项参数并读取验证。确认泵面板处于停止状态。","确认参数设置",MessageBoxButtons.OKCancel)!=DialogResult.OK)return;DialogResult=DialogResult.OK;Close();
            }catch(Exception ex){MessageBox.Show(ex.Message,"参数检查");}}
    }
}

