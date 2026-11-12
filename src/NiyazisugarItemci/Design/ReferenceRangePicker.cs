using System.Drawing.Drawing2D;
using System.Globalization;

namespace NiyazisugarItemci;

// Koyu, yuvarlak köşeli tarih aralığı seçici. İlk tıklama başlangıç, ikinci tıklama
// bitiş günüdür; soldaki kısayollar tek tıkla aralığı uygular.
internal sealed class ReferenceRangePicker : Form
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
    private static readonly string[] WeekDays = { "Pzt", "Sal", "Çar", "Per", "Cum", "Cmt", "Paz" };
    private static readonly Color RangeFill = Color.FromArgb(44, 39, 27);

    private readonly DateTime _today;
    private DateTime _month;
    private DateTime? _start, _end, _hoverDay;
    private string _hot = "";
    private readonly List<(string Text, DateTime From, DateTime To)> _presets = new();
    private readonly Dictionary<string, Rectangle> _boxes = new();
    private readonly Dictionary<DateTime, Rectangle> _dayBoxes = new();

    internal DateTime? ResultStart { get; private set; }
    internal DateTime? ResultEnd { get; private set; }

    internal ReferenceRangePicker(DateTime today, DateTime? start, DateTime? end)
    {
        _today = today.Date;
        _start = start?.Date;
        _end = end?.Date;
        _month = new DateTime((_start ?? _today).Year, (_start ?? _today).Month, 1);

        var monday = _today.AddDays(-(((int)_today.DayOfWeek + 6) % 7));
        var thisMonth = new DateTime(_today.Year, _today.Month, 1);
        _presets.Add(("Bugün", _today, _today));
        _presets.Add(("Dün", _today.AddDays(-1), _today.AddDays(-1)));
        _presets.Add(("Son 7 gün", _today.AddDays(-6), _today));
        _presets.Add(("Bu hafta", monday, _today));
        _presets.Add(("Bu ay", thisMonth, _today));
        _presets.Add(("Geçen ay", thisMonth.AddMonths(-1), thisMonth.AddDays(-1)));

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        KeyPreview = true;
        DoubleBuffered = true;
        BackColor = ReferenceUi.Surface;
        Size = new Size(S(476), S(338));
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); } if (e.KeyCode == Keys.Enter) Apply(); };
    }

    private int S(int value) => LogicalToDeviceUnits(value);

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        using var path = Round(new RectangleF(0, 0, Width, Height), S(12));
        Region = new Region(path);
    }

    private static GraphicsPath Round(RectangleF r, float radius)
    {
        float d = radius * 2;
        var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    private void Apply()
    {
        if (_start is null) return;
        ResultStart = _start;
        ResultEnd = _end ?? _start;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void Pick(DateTime day)
    {
        if (_start is null || _end is not null) { _start = day; _end = null; }
        else if (day < _start) { _end = _start; _start = day; }
        else _end = day;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        _boxes.Clear();
        _dayBoxes.Clear();

        using (var border = new Pen(ReferenceUi.Line))
        using (var outline = Round(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), S(12)))
            g.DrawPath(border, outline);

        // Sol: kısayollar.
        int left = S(14), top = S(16), presetWidth = S(122);
        TextRenderer.DrawText(g, "Kısayollar", ReferenceUi.Font(8), new Rectangle(left + S(8), top, presetWidth, S(18)), ReferenceUi.Muted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        for (int i = 0; i < _presets.Count; i++)
        {
            var box = new Rectangle(left, top + S(24) + i * S(34), presetWidth, S(30));
            var key = "preset" + i;
            _boxes[key] = box;
            bool active = _start == _presets[i].From && (_end ?? _start) == _presets[i].To;
            if (active || _hot == key)
                using (var b = new SolidBrush(active ? ReferenceUi.Field : ReferenceUi.Hover))
                using (var p = Round(box, S(8))) g.FillPath(b, p);
            TextRenderer.DrawText(g, _presets[i].Text, ReferenceUi.Font(9), new Rectangle(box.X + S(10), box.Y, box.Width - S(10), box.Height), active ? ReferenceUi.Gold : ReferenceUi.Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        }
        using (var sep = new Pen(ReferenceUi.Line))
            g.DrawLine(sep, left + presetWidth + S(10), S(14), left + presetWidth + S(10), Height - S(62));

        // Sağ: ay başlığı ve oklar.
        int calLeft = left + presetWidth + S(22), cell = S(42), rowH = S(32);
        int calWidth = cell * 7;
        _boxes["prev"] = new Rectangle(calLeft, top - S(2), S(28), S(28));
        _boxes["next"] = new Rectangle(calLeft + calWidth - S(28), top - S(2), S(28), S(28));
        foreach (var key in new[] { "prev", "next" })
        {
            var box = _boxes[key];
            if (_hot == key) using (var b = new SolidBrush(ReferenceUi.Hover)) using (var p = Round(box, S(7))) g.FillPath(b, p);
            using var pen = new Pen(ReferenceUi.Text, Math.Max(1.2f, S(1) * 1.5f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            int cx = box.X + box.Width / 2, cy = box.Y + box.Height / 2, a = S(4);
            if (key == "prev") { g.DrawLine(pen, cx + a / 2, cy - a, cx - a / 2, cy); g.DrawLine(pen, cx - a / 2, cy, cx + a / 2, cy + a); }
            else { g.DrawLine(pen, cx - a / 2, cy - a, cx + a / 2, cy); g.DrawLine(pen, cx + a / 2, cy, cx - a / 2, cy + a); }
        }
        var monthText = Tr.TextInfo.ToTitleCase(_month.ToString("MMMM yyyy", Tr));
        TextRenderer.DrawText(g, monthText, ReferenceUi.Semibold(10), new Rectangle(calLeft, top - S(2), calWidth, S(28)), ReferenceUi.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

        int gridTop = top + S(34);
        for (int d = 0; d < 7; d++)
            TextRenderer.DrawText(g, WeekDays[d], ReferenceUi.Font(8), new Rectangle(calLeft + d * cell, gridTop, cell, S(20)), ReferenceUi.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

        var first = _month.AddDays(-(((int)_month.DayOfWeek + 6) % 7));
        DateTime? from = _start, to = _end ?? (_start is not null && _hoverDay is not null ? _hoverDay : _start);
        if (from is not null && to is not null && to < from) (from, to) = (to, from);
        for (int i = 0; i < 42; i++)
        {
            var day = first.AddDays(i);
            var box = new Rectangle(calLeft + (i % 7) * cell, gridTop + S(24) + (i / 7) * rowH, cell, rowH);
            _dayBoxes[day] = box;
            bool inMonth = day.Month == _month.Month;
            bool edge = day == from || day == to;
            bool inside = from is not null && to is not null && day > from && day < to;
            var inner = Rectangle.Inflate(box, -S(3), -S(2));
            if (inside) using (var b = new SolidBrush(RangeFill)) g.FillRectangle(b, new Rectangle(box.X, inner.Y, box.Width, inner.Height));
            if (edge)
            {
                if (from != to)
                    using (var b = new SolidBrush(RangeFill))
                        g.FillRectangle(b, day == from ? new Rectangle(box.X + box.Width / 2, inner.Y, box.Width / 2, inner.Height) : new Rectangle(box.X, inner.Y, box.Width / 2, inner.Height));
                using (var b = new SolidBrush(ReferenceUi.Gold)) using (var p = Round(inner, S(8))) g.FillPath(b, p);
            }
            else if (_hoverDay == day)
                using (var b = new SolidBrush(ReferenceUi.Hover)) using (var p = Round(inner, S(8))) g.FillPath(b, p);
            if (day == _today && !edge)
                using (var pen = new Pen(ReferenceUi.Gold)) using (var p = Round(inner, S(8))) g.DrawPath(pen, p);
            var color = edge ? ReferenceUi.PrimaryText : !inMonth ? Color.FromArgb(78, 80, 88) : day > _today ? Color.FromArgb(110, 112, 120) : ReferenceUi.Text;
            TextRenderer.DrawText(g, day.Day.ToString(), edge ? ReferenceUi.Semibold(9) : ReferenceUi.Font(9), box, color, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        // Alt: seçili aralık ve düğmeler.
        int footerY = Height - S(50);
        using (var sep = new Pen(ReferenceUi.Line)) g.DrawLine(sep, S(1), footerY - S(8), Width - S(2), footerY - S(8));
        string summary = _start is null ? "Başlangıç gününü seç" : _end is null ? _start.Value.ToString("d MMM yyyy", Tr) + " · bitiş gününü seç" : RangeText(_start.Value, _end.Value);
        TextRenderer.DrawText(g, summary, ReferenceUi.Font(9), new Rectangle(S(22), footerY, S(240), S(34)), _start is null ? ReferenceUi.Muted : ReferenceUi.Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        _boxes["apply"] = new Rectangle(Width - S(106), footerY, S(92), S(34));
        _boxes["clear"] = new Rectangle(Width - S(206), footerY, S(92), S(34));
        DrawButton(g, "clear", "Vazgeç", false);
        DrawButton(g, "apply", "Uygula", true);
    }

    private void DrawButton(Graphics g, string key, string text, bool primary)
    {
        var box = _boxes[key];
        bool enabled = !primary || _start is not null;
        using (var p = Round(box, S(9)))
        {
            if (primary)
                using (var b = new SolidBrush(enabled ? (_hot == key ? Color.FromArgb(226, 192, 116) : ReferenceUi.Gold) : Color.FromArgb(70, 63, 45))) g.FillPath(b, p);
            else
            {
                using (var b = new SolidBrush(_hot == key ? ReferenceUi.Hover : ReferenceUi.Field)) g.FillPath(b, p);
                using (var pen = new Pen(ReferenceUi.Line)) g.DrawPath(pen, p);
            }
        }
        TextRenderer.DrawText(g, text, ReferenceUi.Semibold(9), box, primary ? ReferenceUi.PrimaryText : ReferenceUi.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    internal static string RangeText(DateTime from, DateTime to)
    {
        if (from == to) return from.ToString("d MMMM yyyy", Tr);
        if (from.Year == to.Year && from.Month == to.Month) return $"{from.Day} – {to.ToString("d MMMM yyyy", Tr)}";
        if (from.Year == to.Year) return $"{from.ToString("d MMM", Tr)} – {to.ToString("d MMM yyyy", Tr)}";
        return $"{from.ToString("d MMM yyyy", Tr)} – {to.ToString("d MMM yyyy", Tr)}";
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var hot = _boxes.FirstOrDefault(x => x.Value.Contains(e.Location)).Key ?? "";
        var hit = _dayBoxes.FirstOrDefault(x => x.Value.Contains(e.Location));
        DateTime? day = hit.Value != Rectangle.Empty ? hit.Key : null;
        if (hot != _hot || day != _hoverDay) { _hot = hot; _hoverDay = day; Invalidate(); }
        Cursor = hot.Length > 0 || day is not null ? Cursors.Hand : Cursors.Default;
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hot = ""; _hoverDay = null; Invalidate();
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button != MouseButtons.Left) return;
        var key = _boxes.FirstOrDefault(x => x.Value.Contains(e.Location)).Key;
        if (key == "prev") { _month = _month.AddMonths(-1); Invalidate(); return; }
        if (key == "next") { _month = _month.AddMonths(1); Invalidate(); return; }
        if (key == "apply") { Apply(); return; }
        if (key == "clear") { DialogResult = DialogResult.Cancel; Close(); return; }
        if (key is not null && key.StartsWith("preset"))
        {
            var preset = _presets[int.Parse(key[6..])];
            _start = preset.From; _end = preset.To; Apply();
            return;
        }
        foreach (var entry in _dayBoxes)
            if (entry.Value.Contains(e.Location)) { Pick(entry.Key); if (entry.Key.Month != _month.Month) _month = new DateTime(entry.Key.Year, entry.Key.Month, 1); return; }
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        if (Visible && DialogResult == DialogResult.None) { DialogResult = DialogResult.Cancel; Close(); }
    }
}
