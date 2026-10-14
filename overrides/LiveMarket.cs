using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace NiyazisugarItemci;
public sealed partial class MainForm
{
    private Panel? _liveMarket;
    private readonly ComboBox _liveServer = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 175 };
    private readonly FlowLayoutPanel _liveRows = new() { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
    private readonly Label _liveStatus = new() { Dock = DockStyle.Bottom, Height = 22, ForeColor = Color.FromArgb(150,157,170), Font = new Font("Segoe UI", 8) };
    private readonly System.Windows.Forms.Timer _liveTimer = new() { Interval = 120000 };
    private bool _liveBusy;
    private bool _liveCollapsed;
    private bool _updatingLiveServers;

    private void InstallLiveMarket()
    {
        _liveMarket = new Panel { Size = new Size(360, 280), BackColor = Card, Padding = new Padding(10), Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
        _liveMarket.Paint += (_, e) => DrawBorder(e.Graphics, _liveMarket.ClientRectangle);
        var header = new Panel { Dock = DockStyle.Top, Height = 36 };
        var title = new Button { Text = "PİYASA ▾", Width = 92, Height = 28, FlatStyle = FlatStyle.Flat, ForeColor = Accent, BackColor = Card, Font = new Font("Segoe UI",8,FontStyle.Bold) };
        title.Click += (_, _) => _topMarketMenu.Show(title, new Point(0,title.Height));
        _liveServer.Location = new Point(94, 1);
        _liveServer.SelectedIndexChanged += (_, _) => { if (!_updatingLiveServers) _ = RefreshLiveMarketAsync(); };
        var refresh = new Button { Text = "↻", Location = new Point(274,0), Size = new Size(28,28), FlatStyle = FlatStyle.Flat, ForeColor = TextMain, BackColor = Card };
        refresh.Click += (_, _) => _ = RefreshLiveMarketAsync();
        var collapse = new Button { Text = "−", Location = new Point(306,0), Size = new Size(26,28), FlatStyle = FlatStyle.Flat, ForeColor = TextMain, BackColor = Card };
        collapse.Click += (_, _) => { _liveCollapsed = !_liveCollapsed; _liveMarket.Height = _liveCollapsed ? 56 : 280; _liveRows.Visible = !_liveCollapsed; _liveStatus.Visible = !_liveCollapsed; collapse.Text = _liveCollapsed ? "+" : "−"; PositionLiveMarket(); };
        header.Controls.Add(title);header.Controls.Add(_liveServer);header.Controls.Add(refresh);header.Controls.Add(collapse);
        _liveMarket.Controls.Add(_liveRows);_liveMarket.Controls.Add(_liveStatus);_liveMarket.Controls.Add(header);
        _liveRows.BringToFront();
        _browserPanel.Controls.Add(_liveMarket);
        _browserPanel.SizeChanged += (_, _) => PositionLiveMarket();
        PositionLiveMarket();
        _liveMarket.BringToFront();
        RefreshLiveMarketServers();
        _liveStatus.Text = "Oturum hazırlanıyor · 2 dakikada bir yenilenir";
        _liveTimer.Tick += (_, _) => { if (!_liveCollapsed && _browserPanel.Visible) _ = RefreshLiveMarketAsync(); };
        _liveTimer.Start();
        _browserPanel.VisibleChanged += (_, _) => { if (_browserPanel.Visible) { _liveMarket.BringToFront(); _ = RefreshLiveMarketAsync(); } };
        _marketWebView.NavigationCompleted += (_, e) => { if (e.IsSuccess) _ = RefreshLiveMarketAsync(); };
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

    private void PositionLiveMarket()
    {
        if (_liveMarket is null) return;
        _liveMarket.Location = new Point(12, Math.Max(100, _browserPanel.ClientSize.Height - _liveMarket.Height - 12));
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
                var button = new Button { Width = 318, Height = 29, Text = $"{row.price:N2} TL · {row.title}", FlatStyle = FlatStyle.Flat, TextAlign = ContentAlignment.MiddleLeft, ForeColor = TextMain, BackColor = Card, Font = new Font("Segoe UI",8), AutoEllipsis = true, Margin = new Padding(0,0,0,2), Cursor = Cursors.Hand };
                _sidebarToolTip.SetToolTip(button, $"{row.title}\n{row.seller} · {row.time}");
                button.Click += (_, _) => _ = OpenOrActivateBrowserTabAsync(row.url,row.title);
                _liveRows.Controls.Add(button);
            }
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
    }
}
