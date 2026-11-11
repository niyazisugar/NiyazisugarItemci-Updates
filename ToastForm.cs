namespace NiyazisugarItemci;

internal sealed class ToastForm : Form
{
    private static readonly Color Bg = Color.FromArgb(20, 24, 31);
    private static readonly Color Border = Color.FromArgb(255, 173, 10);
    private static readonly Color TextMain = Color.FromArgb(244, 246, 249);
    private static readonly Color TextMuted = Color.FromArgb(167, 176, 189);

    private readonly System.Windows.Forms.Timer _timer = new();
    private readonly Action? _onClick;

    public ToastForm(string title, string message, bool warning, Action? onClick = null)
    {
        _onClick = onClick;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Bg;
        ForeColor = TextMain;
        Size = new Size(430, 158);

        var accent = warning
            ? Color.FromArgb(255, 108, 108)
            : Border;

        var outer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Bg,
            Padding = new Padding(1)
        };

        outer.Paint += (_, e) =>
        {
            using var pen = new Pen(accent, 1);
            var r = outer.ClientRectangle;
            r.Width -= 1;
            r.Height -= 1;
            e.Graphics.DrawRectangle(pen, r);
        };

        var icon = new Label
        {
            Text = warning ? "!" : "N",
            AutoSize = false,
            Size = new Size(46, 46),
            Location = new Point(16, 18),
            BackColor = Color.FromArgb(34, 39, 49),
            ForeColor = accent,
            Font = new Font("Segoe UI", 18F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            Cursor = Cursors.Hand
        };

        var titleLabel = new Label
        {
            Text = title,
            AutoSize = false,
            Location = new Point(76, 16),
            Size = new Size(270, 26),
            ForeColor = TextMain,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };

        var messageLabel = new Label
        {
            Text = message,
            AutoSize = false,
            Location = new Point(76, 45),
            Size = new Size(326, 76),
            ForeColor = TextMuted,
            Font = new Font("Segoe UI", 9F),
            Cursor = Cursors.Hand
        };

        var close = new Button
        {
            Text = "×",
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Transparent,
            ForeColor = TextMuted,
            Location = new Point(392, 8),
            Size = new Size(28, 28),
            Cursor = Cursors.Hand,
            TabStop = false
        };
        close.FlatAppearance.BorderSize = 0;
        close.Click += (_, _) => Close();

        var hint = new Label
        {
            Text = onClick is null ? "Niyazisugar Itemci" : "Tıklayınca mesajlara gider",
            AutoSize = false,
            Location = new Point(76, 130),
            Size = new Size(310, 18),
            ForeColor = Color.FromArgb(108, 118, 132),
            Font = new Font("Segoe UI", 7.5F),
            Cursor = onClick is null ? Cursors.Default : Cursors.Hand
        };

        void InvokeClick(object? sender, EventArgs e)
        {
            if (_onClick is null) return;
            Close();
            _onClick();
        }

        if (_onClick is not null)
        {
            icon.Click += InvokeClick;
            titleLabel.Click += InvokeClick;
            messageLabel.Click += InvokeClick;
            hint.Click += InvokeClick;
            outer.Cursor = Cursors.Hand;
            outer.Click += InvokeClick;
        }

        outer.Controls.Add(icon);
        outer.Controls.Add(titleLabel);
        outer.Controls.Add(messageLabel);
        outer.Controls.Add(hint);
        outer.Controls.Add(close);
        Controls.Add(outer);

        _timer.Interval = warning ? 10000 : 6500;
        _timer.Tick += (_, _) =>
        {
            _timer.Stop();
            Close();
        };

        Shown += (_, _) =>
        {
            var area = Screen.FromPoint(Cursor.Position).WorkingArea;
            Location = new Point(
                area.Right - Width - 18,
                area.Bottom - Height - 18
            );

            _timer.Start();
        };

        FormClosed += (_, _) => _timer.Dispose();
    }

    protected override bool ShowWithoutActivation => true;
}
