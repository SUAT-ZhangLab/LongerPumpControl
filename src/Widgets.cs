using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace LongerControl {
    public class BufferedPanel : Panel {public BufferedPanel(){DoubleBuffered=true;ResizeRedraw=true;}}
    public class Grid : TableLayoutPanel {public Grid(){DoubleBuffered=true;Margin=Padding.Empty;}}
    public class StableLabel : Label {public StableLabel(){DoubleBuffered=true;AutoSize=false;UseMnemonic=false;TextAlign=ContentAlignment.MiddleLeft;}}
    public class ChoiceBox : ComboBox {
        public ChoiceBox(){DrawMode=DrawMode.OwnerDrawFixed;}
        protected override void OnFontChanged(EventArgs e){base.OnFontChanged(e);ItemHeight=Math.Max(12,TextRenderer.MeasureText("示例",Font).Height+2);}
        protected override void OnDrawItem(DrawItemEventArgs e){e.DrawBackground();string value=e.Index>=0&&e.Index<Items.Count?GetItemText(Items[e.Index]):Text;TextRenderer.DrawText(e.Graphics,value,Font,e.Bounds,Enabled?ForeColor:SystemColors.GrayText,TextFormatFlags.VerticalCenter|TextFormatFlags.Left|TextFormatFlags.EndEllipsis);e.DrawFocusRectangle();}
    }
    public static class Style {
        public static readonly Color Ink=Color.FromArgb(26,44,64),Muted=Color.FromArgb(103,119,137),Blue=Color.FromArgb(35,100,214),Teal=Color.FromArgb(0,130,121),Red=Color.FromArgb(192,52,65),Canvas=Color.FromArgb(241,245,249),Line=Color.FromArgb(224,232,240),Navy=Color.FromArgb(21,38,59);
        public static Label Label(string text,float size,Color color){return new StableLabel{Text=text,Font=new Font("Microsoft YaHei UI",size),ForeColor=color,Height=30,Dock=DockStyle.Fill,Margin=Padding.Empty};}
        public static Button Button(string text,Color color){return new ActionButton{Text=text,Accent=color,Width=114,Height=36,Margin=new Padding(0,0,8,0)};}
        public static Grid Rows(params float[] rows){var g=new Grid{Dock=DockStyle.Fill,ColumnCount=1,RowCount=rows.Length};g.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));foreach(var r in rows)g.RowStyles.Add(new RowStyle(r<0?SizeType.Percent:SizeType.Absolute,r<0?-r:r));return g;}
        public static Grid Columns(params float[] widths){var g=new Grid{Dock=DockStyle.Fill,ColumnCount=widths.Length,RowCount=1};g.RowStyles.Add(new RowStyle(SizeType.Percent,100));foreach(var w in widths)g.ColumnStyles.Add(new ColumnStyle(w<0?SizeType.Percent:SizeType.Absolute,w<0?-w:w));return g;}
        public static void Text(Control c,string text){if(c.Text!=text)c.Text=text;}
        public static void SetColor(Control c,Color color){if(c.ForeColor!=color)c.ForeColor=color;}
        public static void Enabled(Control c,bool enabled){if(c.Enabled!=enabled)c.Enabled=enabled;}
    }
    public class ActionButton : Button {
        public float VisualScale=1;
        Color accent=Style.Blue;bool hover;
        public Color Accent {get{return accent;}set{accent=value;Invalidate();}}
        public bool Quiet {get;set;}
        public string Symbol {get;set;}
        public ActionButton(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;Cursor=Cursors.Hand;Font=new Font("Microsoft YaHei UI",9);UseMnemonic=false;}
        protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
        protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
        protected override void OnPaint(PaintEventArgs e){
            float z=VisualScale;Func<float,int> S=v=>(int)Math.Round(v*z);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(Parent==null?Style.Canvas:Parent.BackColor);
            var rect=new Rectangle(1,1,Width-3,Height-3);int d=Math.Max(2,S(12));using(var path=new GraphicsPath()){
                path.AddArc(rect.X,rect.Y,d,d,180,90);path.AddArc(rect.Right-d,rect.Y,d,d,270,90);path.AddArc(rect.Right-d,rect.Bottom-d,d,d,0,90);path.AddArc(rect.X,rect.Bottom-d,d,d,90,90);path.CloseFigure();
                Color fill=!Enabled?Color.FromArgb(233,238,244):Quiet?(hover?Color.FromArgb(228,237,249):Color.White):(hover?ControlPaint.Dark(Accent,.08f):Accent);
                using(var brush=new SolidBrush(fill))g.FillPath(brush,path);if(Quiet)using(var pen=new Pen(Enabled?Style.Line:Color.FromArgb(232,236,241)))g.DrawPath(pen,path);
            }
            Color ink=!Enabled?Color.FromArgb(150,164,180):Quiet?Accent:Color.White;
            int icon=string.IsNullOrEmpty(Symbol)?0:S(22);var size=TextRenderer.MeasureText(Text,Font);int x=Math.Max(S(10),(Width-size.Width-icon)/2);
            using(var pen=new Pen(ink,1.8f*z))using(var brush=new SolidBrush(ink)){
                float y=Height/2f;
                if(Symbol=="play")g.FillPolygon(brush,new[]{new PointF(x,y-S(6)),new PointF(x,y+S(6)),new PointF(x+S(10),y)});
                if(Symbol=="pause"){g.FillRectangle(brush,x,y-S(6),S(3),S(12));g.FillRectangle(brush,x+S(7),y-S(6),S(3),S(12));}
                if(Symbol=="stop")g.FillRectangle(brush,x,y-S(5),S(10),S(10));
                if(Symbol=="link"){g.DrawEllipse(pen,x,y-S(5),S(8),S(7));g.DrawEllipse(pen,x+S(5),y-S(1),S(8),S(7));}
            }
            TextRenderer.DrawText(g,Text,Font,new Rectangle(icon==0?S(6):x+icon,0,icon==0?Width-S(12):Width-x-icon-S(6),Height),ink,TextFormatFlags.VerticalCenter|(icon==0?TextFormatFlags.HorizontalCenter:TextFormatFlags.Left)|TextFormatFlags.EndEllipsis);
            if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(g,new Rectangle(5,5,Width-10,Height-10),ink,Color.Transparent);
        }
    }
    public sealed class FlowChart : Control {
        public float VisualScale=1;
        struct Sample {public DateTime At;public double A,B;}
        readonly List<Sample> samples=new List<Sample>();
        public FlowChart(){DoubleBuffered=true;ResizeRedraw=true;Dock=DockStyle.Fill;Margin=Padding.Empty;AccessibleName="最近 60 秒流量趋势，蓝色为注射，绿色为抽取";}
        public void Add(PumpSnapshot s){try{var r=s.Status.Raw;samples.Add(new Sample{At=s.CapturedAt,A=Units.Decode(r[2],r[3],true),B=Units.Decode(r[6],r[7],true)});}catch{return;}samples.RemoveAll(p=>(s.CapturedAt-p.At).TotalSeconds>60);Invalidate();}
        public void ClearSamples(){samples.Clear();Invalidate();}
        protected override void OnPaint(PaintEventArgs e){Func<int,int> S=v=>(int)Math.Round(v*VisualScale);var g=e.Graphics;g.Clear(Color.White);g.SmoothingMode=SmoothingMode.AntiAlias;var area=new Rectangle(S(48),S(28),Math.Max(S(20),Width-S(62)),Math.Max(S(20),Height-S(52)));
            TextRenderer.DrawText(g,"流量趋势  ·  mL/min",Font,new Point(0,S(4)),Style.Muted);
            TextRenderer.DrawText(g,"━ 注射",Font,new Point(Math.Max(S(180),Width-S(140)),S(4)),Style.Blue);TextRenderer.DrawText(g,"━ 抽取",Font,new Point(Math.Max(S(244),Width-S(70)),S(4)),Style.Teal);
            double max=samples.Count==0?1:Math.Max(.001,samples.Max(p=>Math.Max(p.A,p.B))*1.15);using(var pen=new Pen(Style.Line))for(int i=0;i<3;i++){int y=area.Top+area.Height*i/2;g.DrawLine(pen,area.Left,y,area.Right,y);TextRenderer.DrawText(g,(max*(2-i)/2).ToString("0.####"),Font,new Rectangle(0,y-S(8),S(44),S(18)),Style.Muted,TextFormatFlags.Right);}
            if(samples.Count>1){DateTime end=DateTime.UtcNow;for(int j=0;j<2;j++)using(var pen=new Pen(j==0?Style.Blue:Style.Teal,2*VisualScale)){for(int i=1;i<samples.Count;i++){var a=samples[i-1];var b=samples[i];if((b.At-a.At).TotalSeconds>3)continue;float x1=area.Right-(float)(end-a.At).TotalSeconds/60*area.Width,x2=area.Right-(float)(end-b.At).TotalSeconds/60*area.Width;float y1=area.Bottom-(float)((j==0?a.A:a.B)/max)*area.Height,y2=area.Bottom-(float)((j==0?b.A:b.B)/max)*area.Height;if(x1>=area.Left&&x2<=area.Right)g.DrawLine(pen,x1,y1,x2,y2);}}}
            TextRenderer.DrawText(g,"−60 秒",Font,new Point(area.Left,area.Bottom+S(4)),Style.Muted);TextRenderer.DrawText(g,"现在",Font,new Point(area.Right-S(34),area.Bottom+S(4)),Style.Muted);
            if(samples.Count<2)TextRenderer.DrawText(g,"连接后显示实时采样",Font,area,Style.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
        }
    }
}
