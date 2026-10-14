using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace NiyazisugarItemci;

internal static class PremiumTheme
{
    internal static readonly Color Background = Color.FromArgb(17, 19, 24);
    internal static readonly Color Surface = Color.FromArgb(26, 29, 36);
    internal static readonly Color Outline = Color.FromArgb(48, 52, 62);
    internal static readonly Color Gold = Color.FromArgb(216, 180, 106);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    internal static void Apply(Form form)
    {
        // Keep the native title bar, resizing and window controls.
        void RoundWindow()
        {
            try { int preference = 2; DwmSetWindowAttribute(form.Handle, 33, ref preference, sizeof(int)); }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }
        form.HandleCreated += (_, _) => RoundWindow();
        if (form.IsHandleCreated) RoundWindow();
        Style(form);
    }

    private static Color Palette(Color color)
    {
        if (color == Color.FromArgb(12, 14, 18)) return Background;
        if (color == Color.FromArgb(24, 28, 35)) return Surface;
        if (color == Color.FromArgb(255, 173, 10)) return Gold;
        if (color == Color.FromArgb(242, 244, 247)) return Color.FromArgb(241, 242, 244);
        if (color == Color.FromArgb(145, 154, 168)) return Color.FromArgb(150, 157, 170);
        if (color == Color.FromArgb(13, 16, 21) || color == Color.FromArgb(17, 20, 26))
            return Color.FromArgb(21, 24, 30);
        return color;
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, object> Styled = new();

    private static void Style(Control control)
    {
        if (Styled.TryGetValue(control, out _)) return;
        Styled.Add(control, new object());
        control.BackColor = Palette(control.BackColor);
        control.ForeColor = Palette(control.ForeColor);
        if (control is Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(53, 48, 38);
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(68, 57, 38);
            Round(control, 11);
        }
        else if (control is Panel panel && panel.BackColor == Surface)
        {
            Round(panel, 16);
        }
        else if (control is DataGridView grid)
        {
            grid.BorderStyle = BorderStyle.None;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.GridColor = Outline;
            grid.BackgroundColor = Background;
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Surface;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(150, 157, 170);
            grid.DefaultCellStyle.BackColor = Color.FromArgb(23, 26, 32);
            grid.AlternatingRowsDefaultCellStyle.BackColor = Surface;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(65, 55, 37);
            grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(241, 242, 244);
        }
        // Style later-created navigation buttons, tabs and listing cards too.
        control.ControlAdded += (_, e) => { if (e.Control is not null) Style(e.Control); };
        foreach (Control child in control.Controls) Style(child);
    }

    private static GraphicsPath Shape(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        int diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        if (diameter <= 0) return path;
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static void Round(Control control, int radius)
    {
        void ResizeRegion()
        {
            if (control.Width <= 0 || control.Height <= 0) return;
            using var path = Shape(control.ClientRectangle, radius);
            var old = control.Region;
            control.Region = new Region(path);
            old?.Dispose();
        }
        control.SizeChanged += (_, _) => ResizeRegion();
        ResizeRegion();
    }

    internal static void DrawBorder(Graphics graphics, Rectangle rect)
    {
        rect.Inflate(-1, -1);
        if (rect.Width <= 0 || rect.Height <= 0) return;
        var previous = graphics.SmoothingMode;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = Shape(rect, 16);
        using var pen = new Pen(Outline);
        graphics.DrawPath(pen, path);
        graphics.SmoothingMode = previous;
    }
}
