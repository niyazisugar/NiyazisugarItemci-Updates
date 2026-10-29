using System.Drawing.Drawing2D;
namespace NiyazisugarItemci;

// "Graphite" sayfa iskeleti: solda başlık + kısa açıklama, sağda filtreler; altında tek parça
// KPI kartı (bölücülü hücreler), ardından başlığı ve araması kendi içinde olan tek tablo kartı.
internal static class PageLayout
{
    internal static Padding PagePadding(Control owner) => new(owner.LogicalToDeviceUnits(36), owner.LogicalToDeviceUnits(28), owner.LogicalToDeviceUnits(36), owner.LogicalToDeviceUnits(20));

    /// <summary>Başlık + açıklama solda, filtre ve işlemler sağda tek satırda.</summary>
    internal static TableLayoutPanel Header(Control owner, string title, Label subtitle, params Control[] controls)
    {
        var header = new TableLayoutPanel { Dock = DockStyle.Top, Height = owner.LogicalToDeviceUnits(58), ColumnCount = 2, RowCount = 1, BackColor = ReferenceUi.Background, Margin = new Padding(0) };
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var headings = new Panel { Dock = DockStyle.Fill, BackColor = ReferenceUi.Background, Margin = new Padding(0) };
        var name = ReferenceUi.Label(title, 15); name.Font = ReferenceUi.Semibold(15); name.Dock = DockStyle.Top; name.Height = owner.LogicalToDeviceUnits(32);
        subtitle.Font = ReferenceUi.Font(9); subtitle.ForeColor = ReferenceUi.Muted; subtitle.BackColor = Color.Transparent; subtitle.AutoEllipsis = true; subtitle.Dock = DockStyle.Top; subtitle.Height = owner.LogicalToDeviceUnits(20);
        headings.Controls.Add(subtitle); headings.Controls.Add(name);
        header.Controls.Add(headings, 0, 0);
        var tools = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, FlowDirection = FlowDirection.LeftToRight, BackColor = ReferenceUi.Background, Margin = new Padding(0, owner.LogicalToDeviceUnits(10), 0, 0), Anchor = AnchorStyles.Top | AnchorStyles.Right };
        foreach (var control in controls)
        {
            control.Height = owner.LogicalToDeviceUnits(36);
            control.Margin = new Padding(owner.LogicalToDeviceUnits(8), 0, 0, 0);
            tools.Controls.Add(control);
        }
        header.Controls.Add(tools, 1, 0);
        return header;
    }

    /// <summary>Başlık + açıklama solda, hemen yanında sol araçlar (ör. sunucu seçimi), en sağda işlemler.</summary>
    internal static TableLayoutPanel HeaderLeft(Control owner, string title, Label subtitle, Control[] left, Control[] right)
    {
        var header = new TableLayoutPanel { Dock = DockStyle.Top, Height = owner.LogicalToDeviceUnits(56), ColumnCount = 4, RowCount = 1, BackColor = ReferenceUi.Background, Margin = new Padding(0) };
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var name = ReferenceUi.Label(title, 15); name.Font = ReferenceUi.Semibold(15); name.Dock = DockStyle.Top; name.Height = owner.LogicalToDeviceUnits(32);
        subtitle.Font = ReferenceUi.Font(9); subtitle.ForeColor = ReferenceUi.Muted; subtitle.BackColor = Color.Transparent; subtitle.AutoEllipsis = true; subtitle.Dock = DockStyle.Top; subtitle.Height = owner.LogicalToDeviceUnits(20);
        int width = Math.Max(TextRenderer.MeasureText(title, name.Font).Width, TextRenderer.MeasureText(subtitle.Text, subtitle.Font).Width) + owner.LogicalToDeviceUnits(8);
        var headings = new Panel { Width = width, Height = owner.LogicalToDeviceUnits(52), BackColor = ReferenceUi.Background, Margin = new Padding(0) };
        headings.Controls.Add(subtitle); headings.Controls.Add(name);
        header.Controls.Add(headings, 0, 0);
        FlowLayoutPanel Tools(Control[] controls, bool leading)
        {
            var tools = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, BackColor = ReferenceUi.Background, Margin = new Padding(leading ? owner.LogicalToDeviceUnits(20) : 0, owner.LogicalToDeviceUnits(9), 0, 0), Anchor = AnchorStyles.Top | (leading ? AnchorStyles.Left : AnchorStyles.Right) };
            foreach (var control in controls) { control.Height = owner.LogicalToDeviceUnits(36); control.Margin = new Padding(leading ? 0 : owner.LogicalToDeviceUnits(8), 0, leading ? owner.LogicalToDeviceUnits(8) : 0, 0); tools.Controls.Add(control); }
            return tools;
        }
        header.Controls.Add(Tools(left, true), 1, 0);
        header.Controls.Add(Tools(right, false), 3, 0);
        return header;
    }

    internal static ReferenceButton Action(string text, string icon, System.Action click, int width, bool primary = false)
    {
        var button = ReferenceUi.Button(text, icon, click, width, primary);
        button.Font = primary ? ReferenceUi.Semibold(9) : ReferenceUi.Font(9);
        return button;
    }

    /// <summary>Kare, yalnızca ikonlu ikincil düğme (ör. Yenile).</summary>
    internal static ReferenceButton IconButton(Control owner, string icon, string tooltip, System.Action click, ToolTip tips)
    {
        var button = ReferenceUi.Button("", icon, click, owner.LogicalToDeviceUnits(36));
        button.IconOnly = true; button.AccessibleName = tooltip; tips.SetToolTip(button, tooltip);
        return button;
    }

    /// <summary>Tablo kartının üst satırı: başlık solda, arama veya açıklama sağda, altında ince çizgi.</summary>
    internal static Panel CardHeader(Control owner, string title, Control? right = null, int rightWidth = 260)
    {
        var bar = new Panel { Dock = DockStyle.Top, Height = owner.LogicalToDeviceUnits(54), BackColor = ReferenceUi.Surface, Padding = new Padding(owner.LogicalToDeviceUnits(20), owner.LogicalToDeviceUnits(10), owner.LogicalToDeviceUnits(12), owner.LogicalToDeviceUnits(10)) };
        bar.Paint += (_, e) => { using var pen = new Pen(ReferenceUi.Line); e.Graphics.DrawLine(pen, 0, bar.Height - 1, bar.Width, bar.Height - 1); };
        var caption = ReferenceUi.Label(title, 10.5f); caption.Font = ReferenceUi.Semibold(10.5f); caption.Dock = DockStyle.Fill; caption.TextAlign = ContentAlignment.MiddleLeft; caption.BackColor = ReferenceUi.Surface;
        bar.Controls.Add(caption);
        if (right is not null) { right.Dock = DockStyle.Right; right.Width = owner.LogicalToDeviceUnits(rightWidth); right.Margin = new Padding(0); bar.Controls.Add(right); }
        return bar;
    }

    /// <summary>Kartın altındaki küçük, sönük açıklama satırı.</summary>
    internal static Label Note(Control owner, Label model)
    {
        model.Dock = DockStyle.Bottom; model.Height = owner.LogicalToDeviceUnits(28); model.Font = ReferenceUi.Font(8.5f); model.ForeColor = ReferenceUi.Muted; model.BackColor = ReferenceUi.Background; model.TextAlign = ContentAlignment.BottomLeft; model.AutoEllipsis = true; model.Visible = true;
        return model;
    }

    internal static Panel Spacer(Control owner, int height) => new() { Dock = DockStyle.Top, Height = owner.LogicalToDeviceUnits(height), BackColor = ReferenceUi.Background, Margin = new Padding(0) };
}

/// <summary>Tek parça KPI kartı: eşit genişlikte hücreler, aralarında ince bölücüler. "138.590,00 TL" gibi
/// değerlerde kuruş ve para birimi küçük ve sönük yazılır; vurgulu hücre hafif açık zeminde altın renkte.</summary>
internal sealed class ReferenceKpiCard : Control
{
    private readonly List<(string Title, Label Model, Color? Tone, bool Highlight, bool Compact)> _cells = new();
    internal ReferenceKpiCard()
    {
        Dock = DockStyle.Top; BackColor = ReferenceUi.Background;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }
    internal ReferenceKpiCard Add(string title, Label model, Color? tone = null, bool highlight = false, bool compact = false)
    {
        _cells.Add((title, model, tone, highlight, compact)); model.Visible = false; model.TextChanged += Changed; return this;
    }
    private void Changed(object? sender, EventArgs e) => Invalidate();
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.Clear(ReferenceUi.Host(this)); float s = DeviceDpi / 96f; g.SmoothingMode = SmoothingMode.AntiAlias;
        var card = new RectangleF(.5f, .5f, Width - 1, Height - 1);
        using var shape = ReferenceUi.Shape(card, ReferenceUi.PanelRadius * s);
        using (var fill = new SolidBrush(ReferenceUi.Surface)) g.FillPath(fill, shape);
        if (_cells.Count == 0) { using var p0 = new Pen(ReferenceUi.Line); g.DrawPath(p0, shape); return; }
        float w = (Width - 1) / (float)_cells.Count;
        var state = g.Save(); g.SetClip(shape);
        for (int i = 0; i < _cells.Count; i++)
        {
            var c = _cells[i]; float x = .5f + i * w;
            if (c.Highlight) { using var hl = new SolidBrush(Color.FromArgb(26, 27, 31)); g.FillRectangle(hl, x, 0, w, Height); }
            if (i > 0) { using var div = new Pen(ReferenceUi.Line); g.DrawLine(div, x, 0, x, Height); }
            int left = (int)(x + 20 * s), room = (int)(w - 36 * s);
            TextRenderer.DrawText(g, c.Title, ReferenceUi.Font(9), new Rectangle(left, (int)(16 * s), room, (int)(20 * s)), ReferenceUi.Muted, TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            var text = string.IsNullOrWhiteSpace(c.Model.Text) ? "—" : c.Model.Text.Replace('\n', ' ');
            var tone = c.Tone ?? ReferenceUi.Text; int top = (int)(40 * s);
            if (c.Compact) { TextRenderer.DrawText(g, text, ReferenceUi.Font(9), new Rectangle(left, (int)(38 * s), room, Height - (int)(44 * s)), tone, TextFormatFlags.NoPadding | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix); continue; }
            string main = text, tail = "";
            int comma = text.LastIndexOf(',');
            if (text.EndsWith(" TL") && comma > 0) { main = text[..comma]; tail = text[comma..]; }
            float size = 16; Font big;
            while (true) { big = ReferenceUi.Semibold(size); var need = TextRenderer.MeasureText(main, big, Size.Empty, TextFormatFlags.NoPadding).Width + (tail.Length > 0 ? TextRenderer.MeasureText(tail, ReferenceUi.Font(10), Size.Empty, TextFormatFlags.NoPadding).Width : 0); if (need <= room || size <= 10) break; size -= 1; }
            var mainSize = TextRenderer.MeasureText(main, big, Size.Empty, TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, main, big, new Rectangle(left, top, room, mainSize.Height + 2), tone, TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            if (tail.Length > 0 && mainSize.Width < room)
            {
                var small = ReferenceUi.Font(10); var tailSize = TextRenderer.MeasureText(tail, small, Size.Empty, TextFormatFlags.NoPadding);
                var tailTone = c.Highlight ? Color.FromArgb(168, 146, 92) : ReferenceUi.Muted;
                TextRenderer.DrawText(g, tail, small, new Rectangle(left + mainSize.Width, top + mainSize.Height - tailSize.Height - (int)(2 * s), room - mainSize.Width, tailSize.Height + 2), tailTone, TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            }
        }
        g.Restore(state);
        using var pen = new Pen(ReferenceUi.Line); g.DrawPath(pen, shape);
    }
    protected override void Dispose(bool disposing) { if (disposing) foreach (var c in _cells) c.Model.TextChanged -= Changed; base.Dispose(disposing); }
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
                using var fill = new SolidBrush(selected ? Color.FromArgb(40, 41, 47) : ReferenceUi.Hover); g.FillPath(fill, path);
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
        using (var path = ReferenceUi.Shape(r, ReferenceUi.ControlRadius * s)) { using var fill = new SolidBrush(Color.FromArgb(34, 30, 22)); g.FillPath(fill, path); using var pen = new Pen(Color.FromArgb(74, 62, 38)); g.DrawPath(pen, path); }
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

/// <summary>Satış takibi akışındaki tek satır: ürün, satıcı, fiyat, zaman ve sinyal.</summary>
internal sealed record WatchRowItem(string Title, string Seller, string Price, string Time, string Signal, bool Strong, string Tooltip, System.Action Open);

/// <summary>Tek kontrolde çizilen, sanal kaydırmalı yoğun liste: başlık satırı, 40 px satırlar,
/// üzerine gelince vurgulanan satır, sağda ince koyu kaydırma çubuğu (tekerlek + sürükleme).</summary>
internal sealed class ReferenceWatchList : Control
{
    private List<WatchRowItem> _items = new();
    private int _offset, _hover = -1, _dragStart = -1, _dragOffset;
    private bool _thumbHover;
    private readonly ToolTip _tips;
    private static readonly string[] Heads = { "Ürün", "Satıcı", "Son fiyat", "Tespit", "Sinyal" };
    private static readonly float[] Cols = { .40f, .18f, .13f, .10f, .19f };
    internal string EmptyTitle { get; set; } = "Henüz satış tespit edilmedi";
    internal string EmptyText { get; set; } = "Seçili sunucuda tespit edilen satışlar burada listelenecek.";
    internal ReferenceWatchList(ToolTip tips)
    {
        _tips = tips; BackColor = ReferenceUi.Surface; Dock = DockStyle.Fill; TabStop = true;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
    }
    private int RowH => LogicalToDeviceUnits(40);
    private int HeadH => LogicalToDeviceUnits(34);
    private int ViewH => Math.Max(0, Height - HeadH);
    private int MaxOffset => Math.Max(0, _items.Count * RowH - ViewH);
    internal void SetItems(List<WatchRowItem> items) { _items = items; _offset = Math.Clamp(_offset, 0, MaxOffset); _hover = -1; _tips.Hide(this); _tips.SetToolTip(this, ""); Cursor = Cursors.Default; Invalidate(); }
    private Rectangle Track => new(Width - LogicalToDeviceUnits(12), HeadH + LogicalToDeviceUnits(6), LogicalToDeviceUnits(6), Math.Max(0, ViewH - LogicalToDeviceUnits(12)));
    private Rectangle Thumb
    {
        get
        {
            var t = Track; int total = _items.Count * RowH; if (total <= ViewH || t.Height <= 0) return Rectangle.Empty;
            int h = Math.Max(LogicalToDeviceUnits(36), (int)(t.Height * (ViewH / (float)total)));
            int y = t.Y + (int)((t.Height - h) * (_offset / (float)Math.Max(1, MaxOffset)));
            return new Rectangle(t.X, y, t.Width, h);
        }
    }
    private void ScrollTo(int value) { var v = Math.Clamp(value, 0, MaxOffset); if (v != _offset) { _offset = v; Invalidate(); } }
    protected override void OnResize(EventArgs e) { base.OnResize(e); _offset = Math.Clamp(_offset, 0, MaxOffset); }
    protected override void OnMouseWheel(MouseEventArgs e) { base.OnMouseWheel(e); ScrollTo(_offset - Math.Sign(e.Delta) * RowH * 3); }
    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); if (!Focused && FindForm()?.ActiveControl is not TextBoxBase) Focus(); }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e); Focus();
        var thumb = Thumb;
        if (thumb.Contains(e.Location)) { _dragStart = e.Y; _dragOffset = _offset; Capture = true; return; }
        if (Track.Contains(e.Location) && !thumb.IsEmpty) { ScrollTo(_offset + (e.Y < thumb.Y ? -ViewH : ViewH)); }
    }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); if (_dragStart >= 0) { _dragStart = -1; Capture = false; return; } int i = RowAt(e.Location); if (i >= 0 && e.Button == MouseButtons.Left) _items[i].Open(); }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragStart >= 0) { var t = Track; var th = Thumb; float range = Math.Max(1, t.Height - th.Height); ScrollTo(_dragOffset + (int)((e.Y - _dragStart) / range * MaxOffset)); return; }
        bool onThumb = Thumb.Contains(e.Location); if (onThumb != _thumbHover) { _thumbHover = onThumb; Invalidate(); }
        int i = onThumb ? -1 : RowAt(e.Location);
        if (i != _hover) { _hover = i; Cursor = i >= 0 ? Cursors.Hand : Cursors.Default; _tips.SetToolTip(this, i >= 0 ? _items[i].Tooltip : ""); Invalidate(); }
    }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = -1; _thumbHover = false; _tips.Hide(this); Invalidate(); }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.KeyCode) { case Keys.Down: ScrollTo(_offset + RowH); break; case Keys.Up: ScrollTo(_offset - RowH); break; case Keys.PageDown: ScrollTo(_offset + ViewH); break; case Keys.PageUp: ScrollTo(_offset - ViewH); break; case Keys.Home: ScrollTo(0); break; case Keys.End: ScrollTo(MaxOffset); break; }
    }
    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Up or Keys.Down or Keys.PageUp or Keys.PageDown or Keys.Home or Keys.End || base.IsInputKey(keyData);
    private int RowAt(Point p) { if (p.Y < HeadH || p.X > Width - LogicalToDeviceUnits(16)) return -1; int i = (p.Y - HeadH + _offset) / RowH; return i >= 0 && i < _items.Count ? i : -1; }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.Clear(BackColor); float s = DeviceDpi / 96f;
        int pad = LogicalToDeviceUnits(20), right = LogicalToDeviceUnits(22), usable = Math.Max(10, Width - pad - right);
        var x = new int[Cols.Length + 1]; x[0] = pad; for (int c = 0; c < Cols.Length; c++) x[c + 1] = x[c] + (int)(usable * Cols[c]);
        var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
        for (int c = 0; c < Heads.Length; c++)
            TextRenderer.DrawText(g, Heads[c], ReferenceUi.Font(8.5f), new Rectangle(x[c], 0, x[c + 1] - x[c] - LogicalToDeviceUnits(12), HeadH), ReferenceUi.Muted, flags | (c == 2 ? TextFormatFlags.Right : TextFormatFlags.Default));
        using (var line = new Pen(ReferenceUi.Line)) g.DrawLine(line, 0, HeadH - 1, Width, HeadH - 1);
        if (_items.Count == 0)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias; int y = HeadH + Math.Max(LogicalToDeviceUnits(30), ViewH / 2 - LogicalToDeviceUnits(60));
            var tile = new RectangleF((Width - 40 * s) / 2, y, 40 * s, 40 * s);
            using (var path = ReferenceUi.Shape(tile, 12 * s)) { using var fill = new SolidBrush(ReferenceUi.Field); g.FillPath(fill, path); using var pen = new Pen(ReferenceUi.Line); g.DrawPath(pen, path); }
            ReferenceUi.Icon(g, "bag", new RectangleF(tile.X + 11 * s, tile.Y + 11 * s, 18 * s, 18 * s), ReferenceUi.Gold);
            TextRenderer.DrawText(g, EmptyTitle, ReferenceUi.Semibold(10), new Rectangle(0, (int)(y + 50 * s), Width, (int)(22 * s)), ReferenceUi.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(g, EmptyText, ReferenceUi.Font(9), new Rectangle(0, (int)(y + 74 * s), Width, (int)(20 * s)), ReferenceUi.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix);
            return;
        }
        g.SetClip(new Rectangle(0, HeadH, Width, ViewH));
        int first = _offset / RowH, last = Math.Min(_items.Count - 1, (_offset + ViewH) / RowH);
        using var sep = new Pen(Color.FromArgb(30, 31, 35));
        for (int i = first; i <= last; i++)
        {
            var it = _items[i]; int top = HeadH + i * RowH - _offset; var row = new Rectangle(0, top, Width - LogicalToDeviceUnits(16), RowH);
            if (i == _hover) { using var hb = new SolidBrush(Color.FromArgb(29, 30, 34)); g.FillRectangle(hb, row); }
            g.DrawLine(sep, pad, row.Bottom - 1, Width - right, row.Bottom - 1);
            int gap = LogicalToDeviceUnits(12);
            TextRenderer.DrawText(g, it.Title, ReferenceUi.Semibold(9), new Rectangle(x[0], top, x[1] - x[0] - gap, RowH), ReferenceUi.Text, flags);
            TextRenderer.DrawText(g, it.Seller, ReferenceUi.Font(9), new Rectangle(x[1], top, x[2] - x[1] - gap, RowH), ReferenceUi.Gold, flags);
            TextRenderer.DrawText(g, it.Price, ReferenceUi.Semibold(9), new Rectangle(x[2], top, x[3] - x[2] - gap, RowH), ReferenceUi.Text, flags | TextFormatFlags.Right);
            TextRenderer.DrawText(g, it.Time, ReferenceUi.Font(9), new Rectangle(x[3], top, x[4] - x[3] - gap, RowH), ReferenceUi.Muted, flags);
            // Sinyal: renkli nokta + kısa etiket, yuvarlak hap içinde.
            var tone = it.Strong ? ReferenceUi.Aqua : ReferenceUi.Gold; var font = ReferenceUi.Font(8.5f);
            int tw = TextRenderer.MeasureText(it.Signal, font, Size.Empty, TextFormatFlags.NoPadding).Width; int pw = Math.Min(x[5] - x[4] - gap, tw + LogicalToDeviceUnits(30)); int ph = LogicalToDeviceUnits(22);
            var pill = new RectangleF(x[4], top + (RowH - ph) / 2f, pw, ph);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = ReferenceUi.Shape(pill, ph / 2f)) { using var fill = new SolidBrush(it.Strong ? Color.FromArgb(22, 38, 30) : Color.FromArgb(40, 34, 22)); g.FillPath(fill, path); }
            using (var dot = new SolidBrush(tone)) g.FillEllipse(dot, pill.X + 10 * s, pill.Y + ph / 2f - 3 * s, 6 * s, 6 * s);
            g.SmoothingMode = SmoothingMode.None;
            TextRenderer.DrawText(g, it.Signal, font, new Rectangle((int)(pill.X + 22 * s), (int)pill.Y, (int)(pill.Width - 26 * s), ph), tone, flags);
        }
        g.ResetClip();
        var thumb = Thumb;
        if (!thumb.IsEmpty)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var tp = ReferenceUi.Shape(Track, Track.Width / 2f)) { using var tb = new SolidBrush(Color.FromArgb(26, 27, 31)); g.FillPath(tb, tp); }
            using var path = ReferenceUi.Shape(thumb, thumb.Width / 2f); using var fill = new SolidBrush(_thumbHover || _dragStart >= 0 ? Color.FromArgb(86, 88, 96) : Color.FromArgb(58, 59, 66)); g.FillPath(fill, path);
        }
    }
}
