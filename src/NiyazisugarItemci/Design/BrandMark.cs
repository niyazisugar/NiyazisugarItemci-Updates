using System.Drawing.Drawing2D;
namespace NiyazisugarItemci;
internal sealed class BrandMark : Control
{
    internal BrandMark() { SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); BackColor=Color.Transparent; }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var state = e.Graphics.Save(); e.Graphics.ScaleTransform(Width/100f, Height/100f);
        using var gold = new LinearGradientBrush(new Rectangle(0,0,100,100), Color.FromArgb(240,224,182),Color.FromArgb(160,124,61),45f);
        e.Graphics.FillPolygon(gold, new PointF[] { new(20,80),new(20,20),new(34,20),new(66,59),new(66,20),new(80,20),new(80,80),new(66,80),new(34,41),new(34,80) });
        e.Graphics.Restore(state);
    }
}
