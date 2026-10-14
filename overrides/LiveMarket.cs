using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace NiyazisugarItemci;
public sealed partial class MainForm
{
    private Panel? _liveMarket;
    private readonly ComboBox _liveServer = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 210 };
    private readonly FlowLayoutPanel _liveRows = new() { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
    private readonly Label _liveStatus = new() { Dock = DockStyle.Bottom, Height = 22, ForeColor = Color.FromArgb(150,157,170), Font = new Font("Segoe UI", 8) };
    private readonly System.Windows.Forms.Timer _liveTimer = new() { Interval = 60000 };
    private bool _liveBusy;
    private bool _liveCollapsed;
    private bool _updatingLiveServers;
    private bool _positioningLive;

    private void InstallLiveMarket()
    {
        _liveMarket = new Panel { Size = new Size(288, 340), BackColor = Card, Padding = new Padding(10), Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
        _liveMarket.Paint += (_, e) => DrawBorder(e.Graphics, _liveMarket.ClientRectangle);
        var header = new Panel { Dock = DockStyle.Top, Height = 62 };
        var title = new Button { Text = "SERVER SATIŞLARI", Width = 190, Height = 28, FlatStyle = FlatStyle.Flat, ForeColor = Accent, BackColor = Card, Font = new Font("Segoe UI",8,FontStyle.Bold) };
        title.Click += (_, _) => _topMarketMenu.Show(title, new Point(0,title.Height));
        _liveServer.Location = new Point(0, 30);
        _liveServer.SelectedIndexChanged += (_, _) => { if (!_updatingLiveServers) RenderServerSalesWatch(); };
        var refresh = new Button { Text = "↻", Location = new Point(220,28), Size = new Size(28,28), FlatStyle = FlatStyle.Flat, ForeColor = TextMain, BackColor = Card };
        refresh.Click += (_, _) => _ = RefreshLiveMarketAsync();
        var collapse = new Button { Text = "−", Location = new Point(246,0), Size = new Size(26,28), FlatStyle = FlatStyle.Flat, ForeColor = TextMain, BackColor = Card };
        collapse.Click += (_, _) => { _liveCollapsed = !_liveCollapsed; _liveMarket.Height = _liveCollapsed ? 82 : 340; _liveRows.Visible = !_liveCollapsed; _liveStatus.Visible = !_liveCollapsed; collapse.Text = _liveCollapsed ? "+" : "−"; PositionLiveMarket(); };
        header.Controls.Add(title);header.Controls.Add(_liveServer);header.Controls.Add(refresh);header.Controls.Add(collapse);
        _liveMarket.Controls.Add(_liveRows);_liveMarket.Controls.Add(_liveStatus);_liveMarket.Controls.Add(header);
        _liveRows.BringToFront();
        _browserPanel.Controls.Add(_liveMarket);
        _browserPanel.SizeChanged += (_, _) => PositionLiveMarket();
        _browserAddressBox.TextChanged += (_, _) => PositionLiveMarket();
        PositionLiveMarket();
        _liveMarket.BringToFront();
        LoadServerSalesWatch();
        RefreshLiveMarketServers();
        RenderServerSalesWatch();
        _liveStatus.Text = "Tüm sunucular izleniyor · 60 saniyede bir kontrol";
        _liveTimer.Tick += (_, _) => { PositionLiveMarket(); _ = RefreshLiveMarketAsync(); };
        _liveTimer.Start();
        _browserPanel.VisibleChanged += (_, _) => { if (_browserPanel.Visible) { _liveMarket.BringToFront(); _ = RefreshLiveMarketAsync(); } };
        _marketWebView.NavigationCompleted += (_, e) => { if (e.IsSuccess) { PositionLiveMarket(); _ = RefreshLiveMarketAsync(); } };
        FormClosed += (_, _) => _liveTimer.Dispose();
        // Dashboard is now reached from Settings, not the top navigation.
        var heading = _settingsPanel.Controls.OfType<Label>().FirstOrDefault(l => l.Text == "Ayarlar");
        if (heading is not null)
        {
            var dashboard = ActionButton("ANA PANELİ AÇ", () => ShowPanel(_homePanel));
            dashboard.Size = new Size(160, 36);
            dashboard.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            dashboard.Location = new Point(Math.Max(0, heading.Width - 170), 8);
            heading.Controls.Add(dashboard);
        }
    }

    private void PositionLiveMarket() => _ = PositionLiveMarketAsync();

    private async Task PositionLiveMarketAsync()
    {
        if (_positioningLive || _liveMarket is null) return;
        _positioningLive = true;
        try
        {
            var core = _webView.CoreWebView2;
            if (core is null) { _liveMarket.Visible = false; return; }
            // Find the real sidebar boundary, rather than covering the site's menu.
            var raw = await core.ExecuteScriptAsync("""
                (() => {
                    const norm = s => String(s || '').toLocaleLowerCase('tr-TR').normalize('NFD').replace(/[\u0300-\u036f]/g,'').replace(/ı/g,'i').trim();
                    let left = null;
                    for (const node of document.querySelectorAll('a,span')) {
                        if (norm(node.textContent) !== 'kullanici paneli') continue;
                        for (let p = node.parentElement, i = 0; p && i < 10; p = p.parentElement, i++) {
                            const r = p.getBoundingClientRect();
                            if (r.width >= 160 && r.width <= 420 && r.height >= 200) { left = r.left; break; }
                        }
                        if (left !== null) break;
                    }
                    if (left === null) {
                        const container = [...document.querySelectorAll('main .container,main [class*="max-w-7xl"],header .container')]
                            .map(el=>el.getBoundingClientRect()).find(r=>r.width>600 && r.left>0);
                        left = container?.left ?? 0;
                    }
                    return {left, viewport: innerWidth};
                })()
                """);
            using var data = JsonDocument.Parse(raw);
            var viewport = data.RootElement.GetProperty("viewport").GetDouble();
            var margin = data.RootElement.GetProperty("left").GetDouble() * _browserHost.ClientSize.Width / Math.Max(1, viewport);
            // On narrow windows there is no unused gutter. Hide the floating panel;
            // the full analysis remains available in the top menu.
            _liveMarket.Visible = margin >= 260;
            if (_liveMarket.Visible)
            {
                _liveMarket.Width = Math.Min(288, (int)margin - 24);
                var origin = _browserPanel.PointToClient(_browserHost.PointToScreen(Point.Empty));
                _liveMarket.Location = new Point(origin.X + 12, Math.Max(origin.Y + 12, origin.Y + _browserHost.Height - _liveMarket.Height - 12));
                foreach (Control row in _liveRows.Controls) row.Width = _liveMarket.Width - 26;
                _liveMarket.BringToFront();
            }
        }
        catch { _liveMarket.Visible = false; }
        finally { _positioningLive = false; }
    }

    private void RefreshLiveMarketServers()
    {
        if (_liveMarket is null) return;
        var id = (_liveServer.SelectedItem as MarketServerInfo)?.Id;
        _updatingLiveServers = true;
        try
        {
            _liveServer.Items.Clear();
            foreach (var server in _marketServers) _liveServer.Items.Add(server);
            _liveServer.DisplayMember = "Name";
            _liveServer.SelectedItem = _marketServers.FirstOrDefault(s => s.Id == id) ?? _marketServers.FirstOrDefault();
        }
        finally { _updatingLiveServers = false; }
    }

    private Task RefreshLiveMarketAsync() => ScanAllServerSalesAsync();

    private sealed class LiveListing
    {
        public string title { get; set; } = "";
        public decimal price { get; set; }
        public string seller { get; set; } = "";
        public string time { get; set; } = "";
        public string url { get; set; } = "";
        public decimal? quantity { get; set; }
        public decimal? unitPrice { get; set; }
        public string unitLabel { get; set; } = "";
        public string categoryName { get; set; } = "";
    }
}
