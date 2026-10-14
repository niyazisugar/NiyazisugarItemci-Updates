using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace NiyazisugarItemci;
public sealed partial class MainForm
{
    private Panel? _liveMarket;
    private readonly ComboBox _liveServer = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 210 };
    private readonly FlowLayoutPanel _liveRows = new() { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
    private readonly Label _liveStatus = new() { Dock = DockStyle.Bottom, Height = 22, ForeColor = Color.FromArgb(150,157,170), Font = new Font("Segoe UI", 8) };
    private readonly System.Windows.Forms.Timer _liveTimer = new() { Interval = 120000 };
    private bool _liveBusy;
    private bool _liveCollapsed;
    private bool _updatingLiveServers;
    private bool _positioningLive;

    private void InstallLiveMarket()
    {
        _liveMarket = new Panel { Size = new Size(288, 340), BackColor = Card, Padding = new Padding(10), Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
        _liveMarket.Paint += (_, e) => DrawBorder(e.Graphics, _liveMarket.ClientRectangle);
        var header = new Panel { Dock = DockStyle.Top, Height = 62 };
        var title = new Button { Text = "PİYASA RADARI ▾", Width = 190, Height = 28, FlatStyle = FlatStyle.Flat, ForeColor = Accent, BackColor = Card, Font = new Font("Segoe UI",8,FontStyle.Bold) };
        title.Click += (_, _) => _topMarketMenu.Show(title, new Point(0,title.Height));
        _liveServer.Location = new Point(0, 30);
        _liveServer.SelectedIndexChanged += (_, _) => { if (!_updatingLiveServers) _ = RefreshLiveMarketAsync(); };
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
        RefreshLiveMarketServers();
        _liveStatus.Text = "Oturum hazırlanıyor · 2 dakikada bir yenilenir";
        _liveTimer.Tick += (_, _) => { PositionLiveMarket(); if (!_liveCollapsed && _browserPanel.Visible) _ = RefreshLiveMarketAsync(); };
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

    private async Task RefreshLiveMarketAsync()
    {
        if (_liveBusy || !_marketWebReady || _marketWebView.CoreWebView2 is null || _liveServer.SelectedItem is not MarketServerInfo server || IsDisposed) return;
        _liveBusy = true;
        var core = _marketWebView.CoreWebView2;
        var token = Guid.NewGuid().ToString("N");
        var result = new TaskCompletionSource<List<LiveListing>>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Message(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                using var json = JsonDocument.Parse(e.WebMessageAsJson);
                var root = json.RootElement;
                if (!root.TryGetProperty("liveToken",out var t) || t.GetString() != token) return;
                if (root.TryGetProperty("error",out var error)) result.TrySetException(new IOException(error.GetString()));
                else result.TrySetResult(root.GetProperty("rows").Deserialize<List<LiveListing>>() ?? new());
            }
            catch (Exception ex) { result.TrySetException(ex); }
        }
        core.WebMessageReceived += Message;
        try
        {
            _liveStatus.Text = server.Name + " · Güncel ilanlar okunuyor…";
            // NavigationCompleted already installs the analyzer; wait for that
            // installation instead of injecting a second copy concurrently.
            for (int attempt = 0; !_marketAnalyzerInjected && attempt < 100; attempt++) await Task.Delay(100);
            if (!_marketAnalyzerInjected) throw new IOException("Analiz motoru hazırlanamadı.");
            await core.ExecuteScriptAsync($"window.__NiyazisugarLiveListings({JsonSerializer.Serialize(server.Id)}).then(rows=>chrome.webview.postMessage({{liveToken:'{token}',rows}})).catch(e=>chrome.webview.postMessage({{liveToken:'{token}',error:String(e.message||e)}}));");
            var rows = await result.Task.WaitAsync(TimeSpan.FromSeconds(55));
            if (_liveServer.SelectedItem is not MarketServerInfo current || current.Id != server.Id) return;
            foreach (Control old in _liveRows.Controls.Cast<Control>().ToArray()) old.Dispose();
            foreach (var row in rows)
            {
                var card = new Panel { Width = _liveMarket!.Width - 26, Height = 46, BackColor = Color.FromArgb(21, 25, 33), Margin = new Padding(0,0,0,4), Cursor = Cursors.Hand };
                var name = new Label { Text = row.title.Length > 28 ? row.title[..28] + "…" : row.title, Dock = DockStyle.Top, Height = 22, ForeColor = Color.FromArgb(151,190,255), Font = new Font("Segoe UI",8.5F,FontStyle.Bold), Padding = new Padding(5,2,0,0) };
                var price = new Label { Text = $"{row.price:N2} TL", Dock = DockStyle.Left, Width = 94, ForeColor = Color.FromArgb(255,197,73), Font = new Font("Segoe UI",8.5F,FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(5,0,0,0) };
                var unit = new Label { Text = row.unitPrice.HasValue && !string.IsNullOrWhiteSpace(row.unitLabel) ? $"{row.unitPrice:N2} TL / 1 {row.unitLabel}" : "Birim: —", Dock = DockStyle.Fill, ForeColor = row.unitPrice.HasValue ? Color.FromArgb(67,222,169) : TextMuted, Font = new Font("Segoe UI",8,FontStyle.Bold), TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(0,0,5,0) };
                var values = new Panel { Dock = DockStyle.Fill };
                values.Controls.Add(unit);values.Controls.Add(price);unit.BringToFront();
                card.Controls.Add(values);card.Controls.Add(name);values.BringToFront();
                string detail = $"{row.title}\nİlan fiyatı: {row.price:N2} TL\n{row.seller} · {row.time}";
                if (row.unitPrice.HasValue) detail += $"\nMiktar: {row.quantity:N2} {row.unitLabel} · Birim fiyat: {row.unitPrice:N2} TL";
                foreach (Control target in new Control[] {card,name,price,unit,values})
                {
                    _sidebarToolTip.SetToolTip(target, detail);
                    target.Click += (_, _) => _ = OpenOrActivateBrowserTabAsync(row.url,row.title);
                }
                _liveRows.Controls.Add(card);
            }
            PositionLiveMarket();
            _liveStatus.Text = $"{DateTime.Now:HH:mm:ss} · {rows.Count} ilan · 2 dakikada bir yenilenir";
        }
        catch (Exception) { _liveStatus.Text = "Yenilenemedi · ↻ ile tekrar dene"; }
        finally { core.WebMessageReceived -= Message; _liveBusy = false; }
    }

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
