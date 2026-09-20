using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LongerControl {
    // Layout values are captured once in 96-DPI design units. Every resize starts
    // from this baseline, avoiding cumulative rounding and repeated font scaling.
    public class AdaptiveForm : Form {
        sealed class Metric {
            public Control Control;public Font Font;public Padding Padding,Margin;public Size Size;public Point Location;public float[] Rows,Columns;public Point TabPadding;
        }
        readonly List<Metric> metrics=new List<Metric>();readonly List<Font> scaledFonts=new List<Font>();
        readonly System.Windows.Forms.Timer resizeTimer=new System.Windows.Forms.Timer{Interval=120};
        Panel viewport;Control canvas;Size designMinimum;float scale=-1,zoom,lastNativeDpi;bool applying;int testDpi;
        [StructLayout(LayoutKind.Sequential)]struct NativeRect {public int Left,Top,Right,Bottom;}
        [DllImport("user32.dll")]static extern bool GetClientRect(IntPtr window,out NativeRect rect);
        [DllImport("user32.dll")]static extern IntPtr GetThreadDpiAwarenessContext();
        [DllImport("user32.dll")]static extern bool AreDpiAwarenessContextsEqual(IntPtr first,IntPtr second);
        public bool PerMonitorAware {get{try{return AreDpiAwarenessContextsEqual(GetThreadDpiAwarenessContext(),new IntPtr(-4));}catch(EntryPointNotFoundException){return false;}}}
        public Size LogicalMinimum {get;set;}
        public float DisplayScale {get{return scale;}}
        public AdaptiveForm(){AutoScaleMode=AutoScaleMode.None;resizeTimer.Tick+=(s,e)=>{resizeTimer.Stop();ApplyLayout();};Resize+=(s,e)=>QueueLayout();}
        static IEnumerable<Control> Walk(Control root){yield return root;foreach(Control c in root.Controls)foreach(var child in Walk(c))yield return child;}
        protected override void OnLoad(EventArgs e){
            if(Controls.Count==1){Size desired=ClientSize;designMinimum=LogicalMinimum.IsEmpty?desired:LogicalMinimum;canvas=Controls[0];Controls.Remove(canvas);viewport=new BufferedPanel{Dock=DockStyle.Fill,AutoScroll=true,Margin=Padding.Empty};Controls.Add(viewport);viewport.Controls.Add(canvas);canvas.Dock=DockStyle.None;
                foreach(var c in Walk(canvas)){var g=c as TableLayoutPanel;var t=c as TabControl;metrics.Add(new Metric{Control=c,Font=c.Font,Padding=c.Padding,Margin=c.Margin,Size=c.Size,Location=c.Location,Rows=g==null?null:g.RowStyles.Cast<RowStyle>().Select(r=>r.SizeType==SizeType.Absolute?r.Height:-1).ToArray(),Columns=g==null?null:g.ColumnStyles.Cast<ColumnStyle>().Select(r=>r.SizeType==SizeType.Absolute?r.Width:-1).ToArray(),TabPadding=t==null?Point.Empty:t.Padding});}
                var area=Screen.FromControl(this).WorkingArea;float dpi=NativeDpi();MinimumSize=new Size(Math.Min(760,area.Width),Math.Min(520,area.Height));
                Size=new Size(Math.Min((int)(desired.Width*dpi)+16,area.Width-24),Math.Min((int)(desired.Height*dpi)+40,area.Height-24));Location=new Point(area.Left+(area.Width-Width)/2,area.Top+(area.Height-Height)/2);ApplyLayout();
            }base.OnLoad(e);
        }
        float NativeDpi(){using(var g=CreateGraphics())return g.DpiY/96f;}
        public static float FitScale(Size client,Size minimum,float dpi,float zoom){return zoom>0?dpi*zoom:Math.Max(.65f,Math.Min(dpi,Math.Min((float)client.Width/minimum.Width,(float)client.Height/minimum.Height)));}
        void QueueLayout(){if(viewport!=null&&!applying&&!IsDisposed){resizeTimer.Stop();resizeTimer.Start();}}
        static Padding Scaled(Padding p,float s){return new Padding((int)Math.Round(p.Left*s),(int)Math.Round(p.Top*s),(int)Math.Round(p.Right*s),(int)Math.Round(p.Bottom*s));}
        void ApplyLayout(){if(viewport==null||applying||WindowState==FormWindowState.Minimized||ClientSize.Width<1)return;applying=true;try{
            NativeRect rect;if(GetClientRect(Handle,out rect)&&rect.Right>0&&rect.Bottom>0){var actual=new Size(rect.Right,rect.Bottom);if(ClientSize!=actual)ClientSize=actual;}
            float native=NativeDpi(),dpi=testDpi>0?testDpi/96f:native;float next=FitScale(ClientSize,designMinimum,dpi,zoom);next=(float)Math.Floor(next*100)/100;
            if(Math.Abs(next-scale)>.005||Math.Abs(native-lastNativeDpi)>.005){foreach(var m in metrics)m.Control.SuspendLayout();var oldFonts=scaledFonts.ToArray();scaledFonts.Clear();var fonts=new Dictionary<string,Font>();
                foreach(var m in metrics){var c=m.Control;string key=m.Font.Name+"/"+m.Font.SizeInPoints+"/"+m.Font.Style;Font font;if(!fonts.TryGetValue(key,out font)){font=new Font(m.Font.FontFamily,m.Font.SizeInPoints*next/native,m.Font.Style,GraphicsUnit.Point);fonts[key]=font;scaledFonts.Add(font);}c.Font=font;c.Padding=Scaled(m.Padding,next);c.Margin=Scaled(m.Margin,next);
                    if(c!=canvas&&c.Dock==DockStyle.None){c.Size=new Size((int)(m.Size.Width*next),(int)(m.Size.Height*next));c.Location=new Point((int)(m.Location.X*next),(int)(m.Location.Y*next));}
                    var grid=c as TableLayoutPanel;if(grid!=null){for(int i=0;i<m.Rows.Length;i++)if(m.Rows[i]>=0)grid.RowStyles[i].Height=m.Rows[i]*next;for(int i=0;i<m.Columns.Length;i++)if(m.Columns[i]>=0)grid.ColumnStyles[i].Width=m.Columns[i]*next;}
                    var tab=c as TabControl;if(tab!=null)tab.Padding=new Point((int)(m.TabPadding.X*next),(int)(m.TabPadding.Y*next));var button=c as ActionButton;if(button!=null)button.VisualScale=next;var chart=c as FlowChart;if(chart!=null)chart.VisualScale=next;
                }
                scale=next;lastNativeDpi=native;for(int i=metrics.Count-1;i>=0;i--)metrics[i].Control.ResumeLayout(true);foreach(var font in oldFonts)font.Dispose();
            }
            canvas.Location=new Point(viewport.AutoScrollPosition.X,viewport.AutoScrollPosition.Y);canvas.Size=new Size(Math.Max(viewport.ClientSize.Width,(int)Math.Ceiling(designMinimum.Width*scale)),Math.Max(viewport.ClientSize.Height,(int)Math.Ceiling(designMinimum.Height*scale)));viewport.AutoScrollMinSize=new Size((int)Math.Ceiling(designMinimum.Width*scale),(int)Math.Ceiling(designMinimum.Height*scale));
        }finally{applying=false;}}
        public ComboBox CreateZoomSelector(){var box=new ChoiceBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList,Margin=new Padding(6,0,6,0),AccessibleName="显示缩放"};box.Items.AddRange(new object[]{"缩放：自动","缩放：100%","缩放：125%","缩放：150%"});box.SelectedIndex=0;box.SelectionChangeCommitted+=(s,e)=>{zoom=box.SelectedIndex==0?0:box.SelectedIndex==1?1:box.SelectedIndex==2?1.25f:1.5f;ApplyLayout();};return box;}
        public void TestLayout(Size size,int dpi){testDpi=dpi;ClientSize=size;ApplyLayout();}
        public void TestZoom(float value){zoom=value;ApplyLayout();}
        public bool HasScrollArea {get{return viewport.HorizontalScroll.Visible||viewport.VerticalScroll.Visible;}}
        public void CheckViewport(){if(zoom==0&&scale>.65f&&(canvas.Right>viewport.ClientSize.Width+1||canvas.Bottom>viewport.ClientSize.Height+1))throw new InvalidOperationException("自动缩放内容超出视口："+canvas.Bounds+" / "+viewport.ClientSize);}
        protected override void WndProc(ref Message m){base.WndProc(ref m);if(m.Msg==0x02E0||m.Msg==0x007E){
            if(viewport!=null&&WindowState==FormWindowState.Normal){var area=Screen.FromControl(this).WorkingArea;Rectangle suggested=Bounds;if(m.Msg==0x02E0&&m.LParam!=IntPtr.Zero){var r=(NativeRect)Marshal.PtrToStructure(m.LParam,typeof(NativeRect));suggested=Rectangle.FromLTRB(r.Left,r.Top,r.Right,r.Bottom);}int w=Math.Min(suggested.Width,area.Width-16),h=Math.Min(suggested.Height,area.Height-16);Bounds=new Rectangle(Math.Max(area.Left,Math.Min(suggested.Left,area.Right-w)),Math.Max(area.Top,Math.Min(suggested.Top,area.Bottom-h)),w,h);}
            QueueLayout();}}
        protected override void Dispose(bool disposing){if(disposing){resizeTimer.Dispose();foreach(var font in scaledFonts)font.Dispose();}base.Dispose(disposing);}
    }
}
