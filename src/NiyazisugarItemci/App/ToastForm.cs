using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace NiyazisugarItemci;

// Küçük, yuvarlak köşeli bildirim. Hiçbir zaman odak almaz: tam ekran oyunu
// küçültmez, yazılan mesajı kesmez. Aynı anda yalnızca bir bildirim görünür.
internal sealed class ToastForm : Form
{
    private static readonly Color Surface = Color.FromArgb(22, 23, 26);
    private static readonly Color Line = Color.FromArgb(44, 45, 51);
    private static readonly Color TextColor = Color.FromArgb(231, 231, 234);
    private static readonly Color Muted = Color.FromArgb(140, 142, 150);
    private static readonly Color Gold = Color.FromArgb(212, 176, 98);
    private static readonly Color Coral = Color.FromArgb(229, 154, 122);

    private const int WS_EX_TOPMOST = 0x00000008;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WM_MOUSEACTIVATE = 0x0021;
    private const int MA_NOACTIVATE = 3;

    private static ToastForm? _current;

    private readonly string _title;
    private readonly string _message;
    private readonly Color _accent;
    private readonly Action? _onClick;
    private readonly System.Windows.Forms.Timer _timer = new();
    private readonly Font _titleFont = new("Segoe UI Semibold", 9.5F);
    private readonly Font _messageFont = new("Segoe UI", 8.75F);
    private Rectangle _closeBox;
    private bool _closeHover;
    private bool _hover;

    private ToastForm(string title, string message, bool warning, Action? onClick)
    {
        _title = title;
        _message = message;
        _accent = warning ? Coral : Gold;
        _onClick = onClick;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Surface;
        DoubleBuffered = true;
        Size = new Size(LogicalToDeviceUnits(330), LogicalToDeviceUnits(66));
        Cursor = onClick is null ? Cursors.Default : Cursors.Hand;

        _timer.Interval = warning ? 9000 : 6000;
        _timer.Tick += (_, _) => { _timer.Stop(); Close(); };
        FormClosed += (_, _) =>
        {
            _timer.Dispose();
            _titleFont.Dispose();
            _messageFont.Dispose();
            if (ReferenceEquals(_current, this)) _current = null;
        };
    }

    // Bildirimi göster; öncekini kapatır, üst üste yığılmaz.
    internal static void ShowToast(string title, string message, bool warning, Action? onClick = null)
    {
        try { _current?.Close(); } catch { }
        var toast = new ToastForm(title, message, warning, onClick);
        var area = Screen.FromPoint(Cursor.Position).WorkingArea;
        int margin = toast.LogicalToDeviceUnits(14);
        toast.Location = new Point(area.Right - toast.Width - margin, area.Bottom - toast.Height - margin);
        _current = toast;
        toast.Show();
        toast._timer.Start();
    }

    // Pencere oluşturulurken en üstte ve odaksız olarak işaretlenir; TopMost özelliği
    // kullanılmaz çünkü o yol pencereyi etkinleştirip tam ekran oyunları küçültüyor.
    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            return cp;
        }
    }

    protected override bool ShowWithoutActivation => true;

    protected override void WndProc(ref Message m)
    {
        // Tıklamak bile bildirimi etkinleştirmesin; tıklama yine işlenir.
        if (m.Msg == WM_MOUSEACTIVATE) { m.Result = (IntPtr)MA_NOACTIVATE; return; }
        base.WndProc(ref m);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        UpdateShape();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateShape();
        Invalidate();
    }

    private void UpdateShape()
    {
        if (Width <= 0 || Height <= 0) return;
        using var path = RoundRect(new RectangleF(0, 0, Width, Height), LogicalToDeviceUnits(12));
        var old = Region;
        Region = new Region(path);
        old?.Dispose();
    }

    private static GraphicsPath RoundRect(RectangleF r, float radius)
    {
        float d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        int pad = LogicalToDeviceUnits(14);

        using (var bg = new SolidBrush(_hover ? Color.FromArgb(27, 28, 32) : Surface)) g.FillRectangle(bg, ClientRectangle);
        using (var path = RoundRect(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), LogicalToDeviceUnits(12)))
        using (var pen = new Pen(Line)) g.DrawPath(pen, path);

        // Sol: küçük vurgu noktası.
        int dot = LogicalToDeviceUnits(8);
        using (var b = new SolidBrush(_accent))
            g.FillEllipse(b, pad, LogicalToDeviceUnits(18), dot, dot);

        int closeSize = LogicalToDeviceUnits(22);
        _closeBox = new Rectangle(Width - closeSize - LogicalToDeviceUnits(8), LogicalToDeviceUnits(8), closeSize, closeSize);
        int textLeft = pad + dot + LogicalToDeviceUnits(10);
        int textWidth = _closeBox.Left - textLeft - LogicalToDeviceUnits(6);

        var flags = TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;
        TextRenderer.DrawText(g, _title, _titleFont, new Rectangle(textLeft, LogicalToDeviceUnits(11), textWidth, LogicalToDeviceUnits(20)), TextColor, flags);
        TextRenderer.DrawText(g, _message, _messageFont,
            new Rectangle(textLeft, LogicalToDeviceUnits(31), _closeBox.Right - textLeft, Height - LogicalToDeviceUnits(36)),
            Muted, TextFormatFlags.Left | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

        if (_closeHover)
            using (var hb = new SolidBrush(Color.FromArgb(38, 39, 44)))
            using (var hp = RoundRect(_closeBox, LogicalToDeviceUnits(6))) g.FillPath(hb, hp);
        int c = LogicalToDeviceUnits(4);
        var mid = new Point(_closeBox.Left + _closeBox.Width / 2, _closeBox.Top + _closeBox.Height / 2);
        using (var xp = new Pen(_closeHover ? TextColor : Muted, Math.Max(1f, LogicalToDeviceUnits(1) * 1.2f)))
        {
            g.DrawLine(xp, mid.X - c, mid.Y - c, mid.X + c, mid.Y + c);
            g.DrawLine(xp, mid.X + c, mid.Y - c, mid.X - c, mid.Y + c);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        bool overClose = _closeBox.Contains(e.Location);
        if (overClose != _closeHover || !_hover) { _closeHover = overClose; _hover = true; Invalidate(); }
        Cursor = overClose || _onClick is not null ? Cursors.Hand : Cursors.Default;
        _timer.Stop(); // Üzerindeyken kaybolmasın.
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = false; _closeHover = false; Invalidate();
        if (!IsDisposed) _timer.Start();
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button != MouseButtons.Left) return;
        var click = _closeBox.Contains(e.Location) ? null : _onClick;
        Close();
        click?.Invoke();
    }

    // Tam ekran oyun, sunum veya "rahatsız etme" açıkken bildirim penceresi gösterilmez.
    [DllImport("shell32.dll")]
    private static extern int SHQueryUserNotificationState(out int state);

    internal static bool UserAcceptsPopups()
    {
        try
        {
            // 5 = QUNS_ACCEPTS_NOTIFICATIONS; diğerleri meşgul, tam ekran, sunum veya sessiz saat.
            if (SHQueryUserNotificationState(out var state) == 0) return state == 5;
        }
        catch { }
        return true;
    }
}
