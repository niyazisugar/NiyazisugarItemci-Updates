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
    private readonly Label _liveCount=new() { Dock=DockStyle.Fill,ForeColor=PremiumTheme.Gold,TextAlign=ContentAlignment.MiddleCenter,Text="Bugün\n0 tespit" };
    private readonly Label _liveLast=new() { Dock=DockStyle.Fill,ForeColor=PremiumTheme.Turquoise,TextAlign=ContentAlignment.MiddleCenter,Text="Son tespit\n—" };
    private bool _liveBusy;
    private bool _liveCollapsed;
    private bool _updatingLiveServers;

    private void InstallLiveMarket()
    {
        _liveMarket = new Panel { Dock = DockStyle.Right, Width = 300, BackColor = Card, Padding = new Padding(12) };
        _liveMarket.Paint += (_, e) => DrawBorder(e.Graphics, _liveMarket.ClientRectangle);
        var header = new Panel { Dock = DockStyle.Top, Height = 150 };
        var title = new Label { Text = "Satış Takibi", Dock = DockStyle.Top, Height = 32, ForeColor = PremiumTheme.Gold, Font = new Font("Segoe UI",12,FontStyle.Bold) };
        var collapse = new Button { Text = "›", Dock = DockStyle.Right, Width = 30, FlatStyle = FlatStyle.Flat, ForeColor = TextMain, BackColor = Card };
        var metrics=new TableLayoutPanel { Dock=DockStyle.Top,Height=58,ColumnCount=2,RowCount=1,Margin=new Padding(0,8,0,8) };
        metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));metrics.Controls.Add(_liveCount,0,0);metrics.Controls.Add(_liveLast,1,0);
        collapse.Click += (_, _) => { _liveCollapsed = !_liveCollapsed; _liveMarket.Width = _liveCollapsed ? 48 : 300; _liveRows.Visible = _liveStatus.Visible = _liveServer.Visible = title.Visible = !_liveCollapsed; metrics.Visible = !_liveCollapsed; header.Height=_liveCollapsed ? 48 : 150; collapse.Text = _liveCollapsed ? "‹" : "›"; };
        _liveServer.Dock = DockStyle.Top;
        _liveServer.SelectedIndexChanged += (_, _) => { if (!_updatingLiveServers) RenderServerSalesWatch(); };
        var refresh = new Button { Text = "Yenile", Dock = DockStyle.Bottom, Height = 32, FlatStyle = FlatStyle.Flat, ForeColor = PremiumTheme.Gold, BackColor = Card };
        refresh.Click += (_, _) => _ = RefreshLiveMarketAsync();
        header.Controls.Add(metrics);header.Controls.Add(_liveServer);header.Controls.Add(title);header.Controls.Add(collapse);
        _liveStatus.Height = 58;
        _liveMarket.Controls.Add(_liveRows);_liveMarket.Controls.Add(_liveStatus);_liveMarket.Controls.Add(refresh);_liveMarket.Controls.Add(header);
        _browserPanel.Controls.Add(_liveMarket);
        _browserPanel.Controls.SetChildIndex(_liveMarket, _browserPanel.Controls.Count-1);
        _liveRows.SizeChanged += (_, _) => { foreach (Control row in _liveRows.Controls) row.Width = Math.Max(40,_liveRows.ClientSize.Width-6); };
        _liveMarket.SizeChanged += (_, _) => { foreach (Control row in _liveRows.Controls) row.Width = Math.Max(40,_liveRows.ClientSize.Width-6); };
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

    private void PositionLiveMarket() { /* Docking reserves space without probing the website. */ }

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
