using System.Drawing.Drawing2D;
namespace NiyazisugarItemci;

internal static class ReferenceUi
{
    internal static readonly Color Background=Color.FromArgb(13,18,23), Surface=Color.FromArgb(19,25,31), Field=Color.FromArgb(22,29,36), Line=Color.FromArgb(45,56,66), Text=Color.FromArgb(239,242,246), Muted=Color.FromArgb(155,167,180), Gold=Color.FromArgb(225,193,136), Aqua=Color.FromArgb(36,214,197), Coral=Color.FromArgb(243,137,119);
    private static readonly Dictionary<float,Font> Fonts=new(),BoldFonts=new();
    internal static Font Bold(float size) {if(!BoldFonts.TryGetValue(size,out var f))BoldFonts[size]=f=new Font("Segoe UI",size,FontStyle.Bold);return f;}
    internal static Color Host(Control control) {for(var p=control.Parent;p is not null;p=p.Parent)if(p.BackColor.A==255)return p.BackColor;return Background;}
    internal static Font Font(float size) {if(!Fonts.TryGetValue(size,out var f))Fonts[size]=f=new Font("Segoe UI",size,FontStyle.Regular);return f;}
    internal static GraphicsPath Shape(RectangleF r,float radius=10) {var p=new GraphicsPath();float d=Math.Max(1,Math.Min(radius*2,Math.Min(r.Width,r.Height)));p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
    internal static void SurfacePaint(Graphics g,Rectangle r,Color color,bool border=true,float radius=10) {if(r.Width<2||r.Height<2)return;g.SmoothingMode=SmoothingMode.AntiAlias;var rect=new RectangleF(r.X+.5f,r.Y+.5f,r.Width-1,r.Height-1);using var path=Shape(rect,radius);using var fill=new LinearGradientBrush(r,color,Color.FromArgb(Math.Max(0,color.R-3),Math.Max(0,color.G-3),Math.Max(0,color.B-3)),90f);g.FillPath(fill,path);if(border){using var line=new Pen(Line);g.DrawPath(line,path);}}
    internal static ReferenceButton Button(string text,string icon,Action click,int width=140,bool primary=false) {var b=new ReferenceButton {Text=text,Icon=icon,Width=width,Height=44,Primary=primary,Margin=new Padding(0,0,12,0)};b.Click+=(_,_)=>click();return b;}
    internal static Control Search(TextBox model,int width=300) {return new ReferenceSearch(model) { Width=width,Height=46,Margin=new Padding(0,0,14,0) };}
    internal static Control Select(ComboBox model,int width=230,string icon="server") {return new ReferenceSelect(model) {Width=width,Height=46,Icon=icon,Margin=new Padding(0,0,14,0)};}
    internal static Panel Metric(string title,Label value,string icon,Color color,bool financial=false) {return new ReferenceMetric(title,value,icon,color,financial);}
    internal static Label Label(string text,float size=11,bool bold=false) => new() { Text=text,ForeColor=Text,BackColor=Color.Transparent,Font=bold?Bold(size):Font(size),AutoEllipsis=true };
    internal static void Icon(Graphics g,string icon,RectangleF r,Color color)
    {
        var state=g.Save();g.TranslateTransform(r.X,r.Y);g.ScaleTransform(r.Width/24,r.Height/24);g.SmoothingMode=SmoothingMode.AntiAlias;using var p=new Pen(color,1.6f){StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round};
        void Line(float a,float b,float c,float d)=>g.DrawLine(p,a,b,c,d);void Rect(float x,float y,float w,float h)=>g.DrawRectangle(p,x,y,w,h);void Circle(float x,float y,float d)=>g.DrawEllipse(p,x,y,d,d);
        switch(icon) {
            case "status":using(var dot=new SolidBrush(Color.FromArgb(61,225,146)))g.FillEllipse(dot,7,7,10,10);break;
            case "search":Circle(3,3,13);Line(15,15,21,21);break;
            case "message":Rect(3,5,18,14);Line(3,5,12,12);Line(12,12,21,5);break;
            case "store":Rect(4,10,16,11);Line(3,10,5,3);Line(5,3,19,3);Line(19,3,21,10);Rect(9,15,6,6);break;
            case "list":Rect(5,3,14,18);Line(8,7,16,7);Line(8,12,16,12);Line(8,17,14,17);break;
            case "server":Rect(3,3,18,7);Rect(3,14,18,7);Circle(6,5.5f,2);Circle(6,16.5f,2);Line(12,7,17,7);Line(12,18,17,18);break;
            case "clock":Circle(2,2,20);Line(12,6,12,12);Line(12,12,16,14);break;
            case "chart":Rect(3,13,3,8);Rect(10,3,3,18);Rect(17,8,3,13);break;
            case "bag":Rect(4,7,16,14);g.DrawArc(p,8,2,8,10,180,180);break;
            case "cart":Line(2,3,5,3);Line(5,3,8,16);Line(8,16,19,16);Line(6,7,22,7);Line(22,7,20,13);Line(20,13,8,13);Circle(8,19,2);Circle(18,19,2);break;
            case "copy":Rect(8,7,12,14);Line(4,17,4,3);Line(4,3,15,3);break;
            case "open":Line(13,3,21,3);Line(21,3,21,11);Line(21,3,10,14);Line(10,6,4,6);Line(4,6,4,21);Line(4,21,19,21);Line(19,21,19,15);break;
            case "refresh":g.DrawArc(p,3,3,18,18,30,285);Line(21,4,21,10);Line(21,10,15,10);break;
            case "calendar":Rect(3,5,18,16);Line(3,10,21,10);Line(7,2,7,7);Line(17,2,17,7);break;
            case "coins":g.DrawEllipse(p,10,3,11,5);Line(10,5,10,16);Line(21,5,21,16);g.DrawArc(p,10,13,11,6,0,180);g.DrawEllipse(p,3,12,10,5);Line(3,14,3,20);Line(13,15,13,20);g.DrawArc(p,3,17,10,5,0,180);break;
            case "wallet":Rect(3,5,18,15);Rect(15,10,7,6);Circle(17,12,2);break;
            case "percent":Circle(3,3,6);Circle(15,15,6);Line(5,20,20,5);break;
            case "chevron":Line(6,9,12,15);Line(12,15,18,9);break;
            case "plus":Line(12,4,12,20);Line(4,12,20,12);break;
            case "back":Line(18,12,4,12);Line(4,12,10,6);Line(4,12,10,18);break;
            case "next":Line(4,12,20,12);Line(20,12,14,6);Line(20,12,14,18);break;
            case "gear":Circle(7,7,10);Circle(10,10,4);for(int i=0;i<8;i++){float t=i*(float)Math.PI/4;Line(12+8*(float)Math.Cos(t),12+8*(float)Math.Sin(t),12+10*(float)Math.Cos(t),12+10*(float)Math.Sin(t));}break;
            case "grid":Rect(3,3,7,7);Rect(14,3,7,7);Rect(3,14,7,7);Rect(14,14,7,7);break;
            case "close":Line(6,6,18,18);Line(18,6,6,18);break;
            case "shield":g.DrawPolygon(p,new PointF[]{new(12,2),new(21,6),new(20,15),new(12,22),new(4,15),new(3,6)});break;
            default:Circle(3,3,18);Line(12,7,12,13);Circle(11.5f,16,1);break;
        }g.Restore(state);
    }
}
internal sealed class ReferenceButton : Button
{
    internal string Icon {get;set;}="";internal bool IconOnly {get;set;}internal bool Primary {get;set;}internal bool Navigation {get;set;}internal bool GoldOutline {get;set;}internal bool Selected {get;set;}private bool _hover;
    internal ReferenceButton(){FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;Font=ReferenceUi.Font(11);Cursor=Cursors.Hand;BackColor=ReferenceUi.Surface;ForeColor=ReferenceUi.Text;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
    protected override void OnMouseEnter(EventArgs e){base.OnMouseEnter(e);_hover=true;Invalidate();}protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);_hover=false;Invalidate();}
    protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.Clear(ReferenceUi.Host(this));float s=DeviceDpi/96f;var tone=Enabled?(Primary?Color.FromArgb(8,35,34):Selected||GoldOutline?ReferenceUi.Gold:ForeColor):ReferenceUi.Muted;
        if(!Navigation){ReferenceUi.SurfacePaint(g,ClientRectangle,Primary?ReferenceUi.Aqua:_hover?Color.FromArgb(30,38,45):ReferenceUi.Field,true,8*s);if(GoldOutline){using var path=ReferenceUi.Shape(new RectangleF(.5f,.5f,Width-1,Height-1),8*s);using var pen=new Pen(ReferenceUi.Gold);g.DrawPath(pen,path);}}else if(_hover){using var fill=new SolidBrush(Color.FromArgb(26,33,39));g.FillRectangle(fill,ClientRectangle);}
        int iconWidth=string.IsNullOrEmpty(Icon)?0:(int)(24*s),gap=iconWidth==0?0:(int)(10*s);var display=IconOnly?"":Text;var measured=TextRenderer.MeasureText(display,Font);int total=measured.Width+iconWidth+gap;int left=IconOnly?(int)((Width-22*s)/2):Math.Max((int)(10*s),(Width-total)/2);
        if(iconWidth>0)ReferenceUi.Icon(g,Icon,new RectangleF(left,(Height-22*s)/2,22*s,22*s),tone);
        TextRenderer.DrawText(g,display,Font,new Rectangle(left+iconWidth+gap,0,Math.Max(0,Width-left-iconWidth-gap-6),Height),tone,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix|TextFormatFlags.SingleLine);
        if(Navigation&&Selected){using var pen=new Pen(ReferenceUi.Gold,2*s);g.DrawLine(pen,5*s,Height-2*s,Width-5*s,Height-2*s);}if(Focused&&!Navigation){using var path=ReferenceUi.Shape(new RectangleF(3,3,Width-6,Height-6),6*s);using var pen=new Pen(ReferenceUi.Gold);g.DrawPath(pen,path);}
    }
}
internal class ReferencePanel : Panel
{
    internal ReferencePanel(){BackColor=ReferenceUi.Surface;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
    protected override void OnPaintBackground(PaintEventArgs e){e.Graphics.Clear(ReferenceUi.Host(this));ReferenceUi.SurfacePaint(e.Graphics,ClientRectangle,BackColor,true,10*DeviceDpi/96f);}
}
internal sealed class ReferenceMetric : ReferencePanel
{
    private readonly string _title,_icon;private readonly Label _model;private readonly Color _tone;private readonly bool _finance;
    internal ReferenceMetric(string title,Label model,string icon,Color tone,bool finance){_title=title;_model=model;_icon=icon;_tone=tone;_finance=finance;model.Visible=false;Controls.Add(model);model.TextChanged+=Changed;Dock=DockStyle.Fill;Margin=new Padding(0,0,14,0);}
    private void Changed(object? sender,EventArgs e)=>Invalidate();
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);float s=DeviceDpi/96f;var g=e.Graphics;var box=new RectangleF(20*s,(Height-54*s)/2,54*s,54*s);using var fill=new SolidBrush(Color.FromArgb(43,42,34));if(_finance){using var pen=new Pen(_tone);g.DrawEllipse(pen,box);}else{using var path=ReferenceUi.Shape(box,12*s);g.FillPath(fill,path);}ReferenceUi.Icon(g,_icon,new RectangleF(box.X+13*s,box.Y+13*s,28*s,28*s),_tone);
        int x=(int)(92*s),width=Math.Max(10,Width-x-(int)(16*s));TextRenderer.DrawText(g,_title,ReferenceUi.Font(11),new Rectangle(x,(int)(Height/2-30*s),width,(int)(26*s)),ReferenceUi.Muted,TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
        float size=_finance?22:_model.Text.Length<7?20:16;while(size>12&&TextRenderer.MeasureText(_model.Text,ReferenceUi.Font(size)).Width>width)size-=1;
        var bold=ReferenceUi.Bold(size);TextRenderer.DrawText(g,_model.Text,bold,new Rectangle(x,(int)(Height/2-4*s),width,(int)(38*s)),_finance?_tone:ReferenceUi.Text,TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
    }
    protected override void Dispose(bool disposing){if(disposing)_model.TextChanged-=Changed;base.Dispose(disposing);}
}
internal sealed class ReferenceSearch : ReferencePanel
{
    private readonly TextBox _model;internal string Icon {get;set;}="search";
    internal ReferenceSearch(TextBox model){_model=model;BackColor=ReferenceUi.Field;model.Dock=DockStyle.None;model.BorderStyle=BorderStyle.None;model.BackColor=ReferenceUi.Field;model.ForeColor=ReferenceUi.Text;model.Font=ReferenceUi.Font(11);Controls.Add(model);Resize+=(_,_)=>LayoutInput();LayoutInput();}
    private void LayoutInput(){int s=LogicalToDeviceUnits(1);_model.Location=new Point(LogicalToDeviceUnits(48),Math.Max(0,(Height-_model.PreferredHeight)/2));_model.Width=Math.Max(20,Width-LogicalToDeviceUnits(64));}
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);ReferenceUi.Icon(e.Graphics,Icon,new RectangleF(LogicalToDeviceUnits(16),(Height-LogicalToDeviceUnits(22))/2,LogicalToDeviceUnits(22),LogicalToDeviceUnits(22)),ReferenceUi.Muted);}
}
internal sealed class ReferenceSelect : Control
{
    private readonly ComboBox _model;internal string Icon {get;set;}="server";
    internal ReferenceSelect(ComboBox model){_model=model;model.SelectedIndexChanged+=Changed;BackColor=ReferenceUi.Field;Font=ReferenceUi.Font(11);Cursor=Cursors.Hand;TabStop=true;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
    private void Changed(object? sender,EventArgs e)=>Invalidate();
    protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.Clear(ReferenceUi.Host(this));float s=DeviceDpi/96f;ReferenceUi.SurfacePaint(g,ClientRectangle,ReferenceUi.Field,true,8*s);ReferenceUi.Icon(g,Icon,new RectangleF(16*s,(Height-22*s)/2,22*s,22*s),ReferenceUi.Muted);TextRenderer.DrawText(g,_model.GetItemText(_model.SelectedItem),Font,new Rectangle((int)(50*s),0,Math.Max(0,Width-(int)(86*s)),Height),ReferenceUi.Text,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);ReferenceUi.Icon(g,"chevron",new RectangleF(Width-30*s,(Height-18*s)/2,18*s,18*s),ReferenceUi.Muted);}
    protected override void OnClick(EventArgs e){base.OnClick(e);OpenChoices();}
    private void OpenChoices(){var menu=new ContextMenuStrip {BackColor=ReferenceUi.Surface,ForeColor=ReferenceUi.Text,Font=ReferenceUi.Font(11),ShowImageMargin=false};for(int i=0;i<_model.Items.Count;i++){int index=i;var item=new ToolStripMenuItem(_model.GetItemText(_model.Items[i])) {Checked=i==_model.SelectedIndex};item.Click+=(_,_)=>_model.SelectedIndex=index;menu.Items.Add(item);}menu.Closed+=(_,_)=>menu.Dispose();menu.Show(this,new Point(0,Height));}
    protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);if(e.KeyCode is Keys.Enter or Keys.Space){OpenChoices();e.Handled=true;}else if(e.KeyCode is Keys.Up or Keys.Down&&_model.Items.Count>0){_model.SelectedIndex=Math.Clamp(_model.SelectedIndex+(e.KeyCode==Keys.Down?1:-1),0,_model.Items.Count-1);e.Handled=true;}}
    protected override void Dispose(bool disposing){if(disposing)_model.SelectedIndexChanged-=Changed;base.Dispose(disposing);}
}
internal sealed class ReferenceBadge : Control
{
    internal bool Status {get;set;}internal ReferenceBadge(){Height=32;BackColor=ReferenceUi.Surface;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
    protected override void OnPaint(PaintEventArgs e){var color=Status?Color.FromArgb(68,224,153):ReferenceUi.Gold;var background=Status?Color.FromArgb(18,49,38):Color.FromArgb(49,44,32);var measured=TextRenderer.MeasureText(Text,ReferenceUi.Font(10));int w=Math.Min(Width-2,measured.Width+(Status?36:24));int h=Math.Min(LogicalToDeviceUnits(30),Height-6);var r=new Rectangle((Width-w)/2,(Height-h)/2,w,h);using var path=ReferenceUi.Shape(r,15);using var fill=new SolidBrush(background);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;e.Graphics.FillPath(fill,path);using var pen=new Pen(Status?Color.FromArgb(29,65,48):Color.FromArgb(100,85,50));e.Graphics.DrawPath(pen,path);if(Status){using var dot=new SolidBrush(color);e.Graphics.FillEllipse(dot,r.X+10,r.Y+r.Height/2-4,8,8);}TextRenderer.DrawText(e.Graphics,Text,ReferenceUi.Font(10),new Rectangle(r.X+(Status?21:4),r.Y,r.Width-(Status?25:8),r.Height),color,TextFormatFlags.VerticalCenter|TextFormatFlags.HorizontalCenter|TextFormatFlags.EndEllipsis);}
}
internal sealed class ReferenceRow : Panel
{
    internal ReferenceRow(){BackColor=ReferenceUi.Surface;SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);using var pen=new Pen(ReferenceUi.Line);e.Graphics.DrawLine(pen,0,Height-1,Width,Height-1);}
}
internal sealed class ReferenceBrand : Control
{
    internal string Subtitle {get;set;}="Satıcı Çalışma Alanı";
    internal ReferenceBrand(){BackColor=ReferenceUi.Background;Controls.Add(new BrandMark {Location=new Point(0,4),Size=new Size(48,48)});SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);float s=DeviceDpi/96f;var font=ReferenceUi.Bold(14);int x=(int)(62*s);var name="Niyazi";int w=TextRenderer.MeasureText(name,font,Size.Empty,TextFormatFlags.NoPadding).Width;TextRenderer.DrawText(e.Graphics,name,font,new Point(x,(int)(8*s)),ReferenceUi.Text,TextFormatFlags.NoPadding);TextRenderer.DrawText(e.Graphics,"SUGAR",font,new Point(x+w,(int)(8*s)),ReferenceUi.Gold,TextFormatFlags.NoPadding);TextRenderer.DrawText(e.Graphics,Subtitle,ReferenceUi.Font(9),new Point(x,(int)(35*s)),ReferenceUi.Muted,TextFormatFlags.NoPadding);}
}
internal sealed class ReferenceListHeading : Control
{
    internal ReferenceListHeading(){BackColor=ReferenceUi.Surface;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);var names=new[]{"İlan","Sunucu","Fiyat","Durum","İşlemler"};var widths=new[]{.40f,.12f,.14f,.10f,.24f};float x=14;for(int i=0;i<5;i++){int width=(int)((Width-28)*widths[i]);TextRenderer.DrawText(e.Graphics,names[i],ReferenceUi.Font(10),new Rectangle((int)x+4,0,width-8,Height),ReferenceUi.Muted,TextFormatFlags.VerticalCenter|(i==0?TextFormatFlags.Left:TextFormatFlags.HorizontalCenter));x+=width;}using var pen=new Pen(ReferenceUi.Line);e.Graphics.DrawLine(pen,0,Height-1,Width,Height-1);}
}
internal sealed class ReferenceSalesFooter : Control
{
    private readonly DataGridView _grid;private readonly Label _gross,_commission,_net,_trigger;
    internal ReferenceSalesFooter(DataGridView grid,Label gross,Label commission,Label net,Label trigger){_grid=grid;_gross=gross;_commission=commission;_net=net;_trigger=trigger;trigger.Visible=false;Controls.Add(trigger);trigger.TextChanged+=Changed;BackColor=ReferenceUi.Surface;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
    private void Changed(object? s,EventArgs e)=>Invalidate();
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);using var pen=new Pen(ReferenceUi.Line);e.Graphics.DrawLine(pen,0,0,Width,0);var font=ReferenceUi.Bold(13);TextRenderer.DrawText(e.Graphics,"Toplam",font,new Rectangle(20,0,180,Height),ReferenceUi.Text,TextFormatFlags.VerticalCenter);foreach(var part in new[]{("Gross",_gross.Text,ReferenceUi.Gold),("Commission",_commission.Text,ReferenceUi.Coral),("Net",_net.Text,ReferenceUi.Aqua)}) {var col=_grid.Columns[part.Item1];if(col is null)continue;var r=_grid.GetColumnDisplayRectangle(col.Index,true);r.X+=_grid.Left;r.Y=0;r.Height=Height;TextRenderer.DrawText(e.Graphics,part.Item2,font,r,part.Item3,TextFormatFlags.VerticalCenter|TextFormatFlags.HorizontalCenter|TextFormatFlags.EndEllipsis);}}
    protected override void Dispose(bool disposing){if(disposing)_trigger.TextChanged-=Changed;base.Dispose(disposing);}
}
internal sealed class ReferenceMiniMetric : ReferencePanel
{
    private readonly Label _model;private readonly string _title,_icon;
    internal ReferenceMiniMetric(Label model,string title,string icon){_model=model;_title=title;_icon=icon;model.Visible=false;Controls.Add(model);model.TextChanged+=Changed;}
    private void Changed(object? s,EventArgs e)=>Invalidate();
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);ReferenceUi.Icon(e.Graphics,_icon,new RectangleF(14,16,22,22),ReferenceUi.Gold);TextRenderer.DrawText(e.Graphics,_title,ReferenceUi.Font(8),new Rectangle(44,13,Math.Max(10,Width-54),32),ReferenceUi.Muted,TextFormatFlags.WordBreak);var bold=ReferenceUi.Bold(18);TextRenderer.DrawText(e.Graphics,_model.Text,bold,new Rectangle(14,44,Width-28,36),ReferenceUi.Text,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);}
    protected override void Dispose(bool disposing){if(disposing)_model.TextChanged-=Changed;base.Dispose(disposing);}
}
internal sealed class ReferenceEmptySale : ReferencePanel
{
    internal ReferenceEmptySale(){Height=280;}
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);int y=Math.Max(30,Height/2-80);ReferenceUi.Icon(e.Graphics,"bag",new RectangleF((Width-54)/2,y,54,54),ReferenceUi.Gold);var bold=ReferenceUi.Bold(12);TextRenderer.DrawText(e.Graphics,"Henüz satış tespit edilmedi",bold,new Rectangle(12,y+80,Width-24,34),ReferenceUi.Text,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);TextRenderer.DrawText(e.Graphics,"Tespit edilen satışlar burada listelenecek.",ReferenceUi.Font(9),new Rectangle(16,y+120,Width-32,55),ReferenceUi.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.WordBreak);}
}
internal sealed class ReferenceTab : ReferencePanel
{
    private readonly Func<bool> _selected;internal ReferenceTab(Func<bool> selected){_selected=selected;}
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);if(_selected()){using var p=new Pen(ReferenceUi.Gold,2);e.Graphics.DrawLine(p,10,1,Width-10,1);}}
}
