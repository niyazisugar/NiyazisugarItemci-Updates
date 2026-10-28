using System.Drawing.Drawing2D;
namespace NiyazisugarItemci;

// "Dim Paper" sayfa iskeleti: küçük üst etiket + sade başlık, tek satır bilgi şeridi,
// tek satır filtreler, tek tablo kartı, sade boş durum ve ince bilgi bandı.
internal static class PageLayout
{
    internal static Padding PagePadding(Control owner) => new(owner.LogicalToDeviceUnits(36), owner.LogicalToDeviceUnits(26), owner.LogicalToDeviceUnits(36), owner.LogicalToDeviceUnits(22));

    /// <summary>Üst etiket + başlık solda, işlem düğmeleri sağda.</summary>
    internal static TableLayoutPanel Header(Control owner, string eyebrow, string title, params Control[] actions)
    {
        var header = new TableLayoutPanel { Dock = DockStyle.Top, Height = owner.LogicalToDeviceUnits(62), ColumnCount = 2, RowCount = 1, BackColor = ReferenceUi.Background, Margin = new Padding(0) };
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var headings = new Panel { Dock = DockStyle.Fill, BackColor = ReferenceUi.Background, Margin = new Padding(0) };
        var top = ReferenceUi.Label(eyebrow, 8.5f); top.ForeColor = ReferenceUi.Muted; top.Dock = DockStyle.Top; top.Height = owner.LogicalToDeviceUnits(20);
        var name = ReferenceUi.Label(title, 15, true); name.Dock = DockStyle.Top; name.Height = owner.LogicalToDeviceUnits(34);
        headings.Controls.Add(name); headings.Controls.Add(top);
        header.Controls.Add(headings, 0, 0);
        var buttons = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, FlowDirection = FlowDirection.LeftToRight, BackColor = ReferenceUi.Background, Margin = new Padding(0, owner.LogicalToDeviceUnits(8), 0, 0), Anchor = AnchorStyles.Top | AnchorStyles.Right };
        foreach (var action in actions)
        {
            action.Height = owner.LogicalToDeviceUnits(36);
            action.Margin = new Padding(owner.LogicalToDeviceUnits(8), 0, 0, 0);
            buttons.Controls.Add(action);
        }
        header.Controls.Add(buttons, 1, 0);
        return header;
    }

    internal static ReferenceButton Action(string text, string icon, System.Action click, int width, bool primary = false)
    {
        var button = ReferenceUi.Button(text, icon, click, width, primary);
        button.Font = primary ? ReferenceUi.Bold(9) : ReferenceUi.Font(9);
        return button;
    }

    internal static Panel Spacer(Control owner, int height) => new() { Dock = DockStyle.Top, Height = owner.LogicalToDeviceUnits(height), BackColor = ReferenceUi.Background, Margin = new Padding(0) };
}

/// <summary>"Aktif 0 · Sunucu Tümü · Son güncelleme 00:41" biçiminde tek satırlık bilgi şeridi.</summary>
internal sealed class ReferenceStatStrip : Control
{
    private readonly List<(string Title, Label Model, Color? Tone)> _items = new();
    private Label? _right;
    internal ReferenceStatStrip()
    {
        BackColor = ReferenceUi.Background; Dock = DockStyle.Top;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }
    internal ReferenceStatStrip Add(string title, Label model, Color? tone = null)
    {
        _items.Add((title, model, tone)); model.TextChanged += Changed; return this;
    }
    internal ReferenceStatStrip Right(Label model) { _right = model; model.TextChanged += Changed; return this; }
    private void Changed(object? sender, EventArgs e) => Invalidate();
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics; float s = DeviceDpi / 96f; int x = 0; int line = Height - (int)(14 * s);
        var title = ReferenceUi.Font(9); var value = ReferenceUi.Bold(9);
        var flags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter;
        var band = new Rectangle(0, 0, Width, line);
        int rightWidth = 0;
        if (_right is not null && !string.IsNullOrWhiteSpace(_right.Text))
        {
            rightWidth = Math.Min(Width / 2, TextRenderer.MeasureText(_right.Text, title, Size.Empty, flags).Width + 4);
            TextRenderer.DrawText(g, _right.Text, title, new Rectangle(Width - rightWidth, 0, rightWidth, line), ReferenceUi.Muted, flags | TextFormatFlags.Right | TextFormatFlags.EndEllipsis);
        }
        foreach (var item in _items)
        {
            if (x > Width - rightWidth - 40) break;
            var t = TextRenderer.MeasureText(item.Title, title, Size.Empty, flags).Width;
            TextRenderer.DrawText(g, item.Title, title, new Rectangle(x, 0, t + 2, line), ReferenceUi.Muted, flags);
            x += t + (int)(5 * s);
            var text = string.IsNullOrWhiteSpace(item.Model.Text) ? "—" : item.Model.Text.Replace('\n', ' ');
            var v = TextRenderer.MeasureText(text, value, Size.Empty, flags).Width;
            int room = Math.Max(0, Width - rightWidth - x - (int)(16 * s));
            TextRenderer.DrawText(g, text, value, new Rectangle(x, 0, Math.Min(v + 2, room), line), item.Tone ?? ReferenceUi.Text, flags | TextFormatFlags.EndEllipsis);
            x += Math.Min(v, room) + (int)(24 * s);
        }
        using var pen = new Pen(ReferenceUi.Line);
        g.DrawLine(pen, 0, Height - 1, Width, Height - 1);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { foreach (var item in _items) item.Model.TextChanged -= Changed; if (_right is not null) _right.TextChanged -= Changed; }
        base.Dispose(disposing);
    }
}

/// <summary>ComboBox modeline bağlı, seçenekleri yan yana gösteren düğme grubu (Aktif · Pasif · Tümü).</summary>
internal sealed class ReferenceSegmented : Control
{
    private readonly ComboBox _model; private int _hover = -1;
    internal Func<int, Color?>? Dot { get; set; }
    internal ReferenceSegmented(ComboBox model)
    {
        _model = model; model.SelectedIndexChanged += Changed; Font = ReferenceUi.Font(9); Cursor = Cursors.Hand; TabStop = true;
        BackColor = ReferenceUi.Field; Margin = new Padding(0, 0, 10, 0);
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }
    private void Changed(object? sender, EventArgs e) => Invalidate();
    internal int PreferredWidthFor()
    {
        int w = LogicalToDeviceUnits(8);
        for (int i = 0; i < _model.Items.Count; i++) w += TextRenderer.MeasureText(_model.GetItemText(_model.Items[i]), Font).Width + LogicalToDeviceUnits(Dot?.Invoke(i) is null ? 22 : 34);
        return w;
    }
    private RectangleF[] Segments()
    {
        float s = DeviceDpi / 96f; var widths = new float[_model.Items.Count]; float total = 0;
        for (int i = 0; i < widths.Length; i++) { widths[i] = TextRenderer.MeasureText(_model.GetItemText(_model.Items[i]), Font).Width + (Dot?.Invoke(i) is null ? 22 : 34) * s; total += widths[i]; }
        float scale = total <= 0 ? 1 : Math.Max(1f, (Width - 8 * s) / total); float x = 4 * s; var result = new RectangleF[widths.Length];
        for (int i = 0; i < widths.Length; i++) { result[i] = new RectangleF(x, 4 * s, widths[i] * scale, Height - 8 * s); x += widths[i] * scale; }
        return result;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.Clear(ReferenceUi.Host(this)); float s = DeviceDpi / 96f; g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var path = ReferenceUi.Shape(new RectangleF(.5f, .5f, Width - 1, Height - 1), ReferenceUi.ControlRadius * s)) { using var fill = new SolidBrush(ReferenceUi.Field); g.FillPath(fill, path); using var pen = new Pen(ReferenceUi.Line); g.DrawPath(pen, path); }
        var parts = Segments();
        for (int i = 0; i < parts.Length; i++)
        {
            bool selected = i == _model.SelectedIndex; var r = parts[i];
            if (selected || i == _hover)
            {
                using var path = ReferenceUi.Shape(r, (ReferenceUi.ControlRadius - 3) * s);
                using var fill = new SolidBrush(selected ? Color.FromArgb(52, 48, 42) : ReferenceUi.Hover); g.FillPath(fill, path);
            }
            var dot = Dot?.Invoke(i); float textLeft = r.X;
            if (dot is Color color) { using var brush = new SolidBrush(color); g.FillEllipse(brush, r.X + 12 * s, r.Y + r.Height / 2 - 3 * s, 6 * s, 6 * s); textLeft += 12 * s; }
            TextRenderer.DrawText(g, _model.GetItemText(_model.Items[i]), Font, new Rectangle((int)textLeft, (int)r.Y, (int)(r.Right - textLeft), (int)r.Height), selected ? ReferenceUi.Text : ReferenceUi.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
        }
        if (Focused) { using var path = ReferenceUi.Shape(new RectangleF(2, 2, Width - 4, Height - 4), (ReferenceUi.ControlRadius - 2) * s); using var pen = new Pen(ReferenceUi.Gold); g.DrawPath(pen, path); }
    }
    private int HitTest(Point p) { var parts = Segments(); for (int i = 0; i < parts.Length; i++) if (parts[i].Contains(p)) return i; return -1; }
    protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); var h = HitTest(e.Location); if (h != _hover) { _hover = h; Invalidate(); } }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = -1; Invalidate(); }
    protected override void OnMouseClick(MouseEventArgs e) { base.OnMouseClick(e); var h = HitTest(e.Location); if (h >= 0 && h < _model.Items.Count) { if (h == _model.SelectedIndex) Reselected?.Invoke(h); else _model.SelectedIndex = h; } }
    /// <summary>Zaten seçili bir parçaya yeniden tıklanınca (ör. "Tarih seç" takvimini tekrar açmak için).</summary>
    internal event Action<int>? Reselected;
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_model.Items.Count == 0) return;
        if (e.KeyCode is Keys.Left or Keys.Right) { _model.SelectedIndex = Math.Clamp(_model.SelectedIndex + (e.KeyCode == Keys.Right ? 1 : -1), 0, _model.Items.Count - 1); e.Handled = true; }
        else if (e.KeyCode is Keys.Enter or Keys.Space && _model.SelectedIndex >= 0) { Reselected?.Invoke(_model.SelectedIndex); e.Handled = true; }
    }
    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Left or Keys.Right || base.IsInputKey(keyData);
    protected override void Dispose(bool disposing) { if (disposing) _model.SelectedIndexChanged -= Changed; base.Dispose(disposing); }
}

/// <summary>Uyarı ve durum mesajları için ince, sıcak tonlu bilgi bandı. Metin boşken gizlenir.</summary>
internal sealed class ReferenceInfoBanner : Control
{
    private readonly Label _model;
    internal ReferenceInfoBanner(Label model)
    {
        _model = model; model.Visible = false; model.TextChanged += Changed; Dock = DockStyle.Bottom; BackColor = ReferenceUi.Background;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Visible = !string.IsNullOrWhiteSpace(model.Text);
    }
    private void Changed(object? sender, EventArgs e) { Visible = !string.IsNullOrWhiteSpace(_model.Text); Invalidate(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.Clear(ReferenceUi.Host(this)); float s = DeviceDpi / 96f; g.SmoothingMode = SmoothingMode.AntiAlias;
        var r = new RectangleF(.5f, 12 * s, Width - 1, Height - 12 * s - 1);
        using (var path = ReferenceUi.Shape(r, ReferenceUi.ControlRadius * s)) { using var fill = new SolidBrush(Color.FromArgb(40, 34, 24)); g.FillPath(fill, path); using var pen = new Pen(Color.FromArgb(82, 66, 38)); g.DrawPath(pen, path); }
        ReferenceUi.Icon(g, "info", new RectangleF(14 * s, r.Y + r.Height / 2 - 8 * s, 16 * s, 16 * s), ReferenceUi.Gold);
        TextRenderer.DrawText(g, _model.Text.Replace('\n', ' '), ReferenceUi.Font(9), new Rectangle((int)(40 * s), (int)r.Y, Width - (int)(54 * s), (int)r.Height), Color.FromArgb(228, 206, 160), TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
    }
    protected override void Dispose(bool disposing) { if (disposing) _model.TextChanged -= Changed; base.Dispose(disposing); }
}

/// <summary>İkon kutusu, başlık, açıklama ve en fazla iki düğmeden oluşan boş durum.</summary>
internal sealed class ReferenceEmptyState : Control
{
    private readonly string _icon, _title, _text;
    private readonly ReferenceButton? _first, _second;
    internal ReferenceEmptyState(string icon, string title, string text, ReferenceButton? first = null, ReferenceButton? second = null)
    {
        _icon = icon; _title = title; _text = text; _first = first; _second = second; BackColor = ReferenceUi.Surface;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        foreach (var b in new[] { first, second }) if (b is not null) { b.Height = LogicalToDeviceUnits(34); b.Font = b.Primary ? ReferenceUi.Bold(9) : ReferenceUi.Font(9); Controls.Add(b); }
    }
    private int Top0 => Math.Max(LogicalToDeviceUnits(24), Height / 2 - LogicalToDeviceUnits(96));
    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        int y = Top0 + LogicalToDeviceUnits(140), gap = LogicalToDeviceUnits(8);
        var buttons = new[] { _first, _second }.Where(b => b is not null).Cast<ReferenceButton>().ToArray();
        int total = buttons.Sum(b => b.Width) + gap * Math.Max(0, buttons.Length - 1); int x = (Width - total) / 2;
        foreach (var b in buttons) { b.Location = new Point(x, y); x += b.Width + gap; }
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.Clear(BackColor); float s = DeviceDpi / 96f; g.SmoothingMode = SmoothingMode.AntiAlias; int y = Top0;
        var tile = new RectangleF((Width - 44 * s) / 2, y, 44 * s, 44 * s);
        using (var path = ReferenceUi.Shape(tile, 12 * s)) { using var fill = new SolidBrush(ReferenceUi.Field); g.FillPath(fill, path); using var pen = new Pen(ReferenceUi.Line); g.DrawPath(pen, path); }
        ReferenceUi.Icon(g, _icon, new RectangleF(tile.X + 12 * s, tile.Y + 12 * s, 20 * s, 20 * s), ReferenceUi.Gold);
        TextRenderer.DrawText(g, _title, ReferenceUi.Bold(10.5f), new Rectangle(12, (int)(y + 58 * s), Width - 24, (int)(24 * s)), ReferenceUi.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        int textWidth = Math.Min(Width - 48, (int)(420 * s));
        TextRenderer.DrawText(g, _text, ReferenceUi.Font(9), new Rectangle((Width - textWidth) / 2, (int)(y + 86 * s), textWidth, (int)(46 * s)), ReferenceUi.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
    }
}
