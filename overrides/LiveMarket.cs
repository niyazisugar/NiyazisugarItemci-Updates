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
        _liveMarket=new ReferencePanel {Dock=DockStyle.Right,Width=360,Padding=new Padding(18),BackColor=ReferenceUi.Background};
        var header=new Panel {Dock=DockStyle.Top,Height=228,BackColor=ReferenceUi.Background};
        var titleRow=new Panel {Dock=DockStyle.Top,Height=42,BackColor=ReferenceUi.Background};var title=ReferenceUi.Label("Satış Takibi",16,true);title.Dock=DockStyle.Fill;title.Padding=new Padding(42,0,0,0);title.TextAlign=ContentAlignment.MiddleLeft;titleRow.Paint+=(_,e)=>{if(!_liveCollapsed)ReferenceUi.Icon(e.Graphics,"chart",new RectangleF(4,9,26,26),ReferenceUi.Gold);};titleRow.Controls.Add(title);
        var collapse=ReferenceUi.Button("","chevron",()=>{},28);collapse.Dock=DockStyle.Right;collapse.Navigation=true;collapse.Margin=new Padding(0);titleRow.Controls.Add(collapse);
        var subtitle=ReferenceUi.Label("Sunucu bazında satış hareketleri",10);subtitle.ForeColor=ReferenceUi.Muted;subtitle.Dock=DockStyle.Top;subtitle.Height=38;
        _liveServer.SelectedIndexChanged+=(_,_)=>{if(!_updatingLiveServers)RenderServerSalesWatch();};var selector=ReferenceUi.Select(_liveServer,300,"server");selector.Dock=DockStyle.Top;
        var space=new Panel {Dock=DockStyle.Top,Height=16,BackColor=ReferenceUi.Background};
        var metrics=new TableLayoutPanel {Dock=DockStyle.Top,Height=88,ColumnCount=2,RowCount=1,BackColor=ReferenceUi.Background};metrics.RowStyles.Add(new RowStyle(SizeType.Percent,100));metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));metrics.Controls.Add(new ReferenceMiniMetric(_liveCount,"Bugün Tespit Edilen","chart") {Dock=DockStyle.Fill,Margin=new Padding(0,0,7,0)},0,0);metrics.Controls.Add(new ReferenceMiniMetric(_liveLast,"Son Satış Tespiti","clock") {Dock=DockStyle.Fill,Margin=new Padding(7,0,0,0)},1,0);
        header.Controls.Add(metrics);header.Controls.Add(space);header.Controls.Add(selector);header.Controls.Add(subtitle);header.Controls.Add(titleRow);
        var refresh=ReferenceUi.Button("Şimdi Yenile","refresh",()=>_=RefreshLiveMarketAsync());refresh.Dock=DockStyle.Bottom;refresh.Height=42;refresh.GoldOutline=true;
        var disclaimer=ReferenceUi.Label("ⓘ  Sayaç artışıyla eşleşen hareketler güçlü satış sinyalidir. Son ilan fiyatı ve tespit zamanı gösterilir.",8);disclaimer.Dock=DockStyle.Bottom;disclaimer.Height=48;disclaimer.ForeColor=ReferenceUi.Muted;disclaimer.TextAlign=ContentAlignment.MiddleLeft;
        _liveStatus.Dock=DockStyle.Bottom;_liveStatus.Height=76;_liveStatus.ForeColor=Color.FromArgb(57,214,151);_liveStatus.Font=ReferenceUi.Font(9);_liveStatus.BackColor=ReferenceUi.Background;
        _liveRows.BackColor=ReferenceUi.Background;_liveRows.Padding=new Padding(0,14,0,4);
        collapse.Click+=(_,_)=>{_liveCollapsed=!_liveCollapsed;_liveMarket.Width=LogicalToDeviceUnits(_liveCollapsed?52:360);_liveMarket.Padding=new Padding(LogicalToDeviceUnits(_liveCollapsed?4:18));_liveRows.Visible=_liveStatus.Visible=refresh.Visible=disclaimer.Visible=metrics.Visible=selector.Visible=subtitle.Visible=title.Visible=!_liveCollapsed;header.Height=LogicalToDeviceUnits(_liveCollapsed?42:228);};
        _liveMarket.Controls.Add(_liveRows);_liveMarket.Controls.Add(_liveStatus);_liveMarket.Controls.Add(refresh);_liveMarket.Controls.Add(disclaimer);_liveMarket.Controls.Add(header);
        _browserPanel.Controls.Add(_liveMarket);_browserPanel.Controls.SetChildIndex(_liveMarket,_browserPanel.Controls.Count-1);
        _liveRows.SizeChanged+=(_,_)=>{foreach(Control row in _liveRows.Controls)row.Width=Math.Max(40,_liveRows.ClientSize.Width-4);};
        LoadServerSalesWatch();
        RefreshLiveMarketServers();
        RenderServerSalesWatch();
        _liveStatus.Text = "İlk 3 sayfa izleniyor · Sunucular sırayla kontrol edilir";
        _liveTimer.Tick += (_, _) => { PositionLiveMarket(); _ = RefreshLiveMarketAsync(); };
        _liveTimer.Start();
        _browserPanel.VisibleChanged += (_, _) => { if (_browserPanel.Visible) { _ = RefreshLiveMarketAsync(); } };
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
        public string sellerUrl { get; set; } = "";
        public string time { get; set; } = "";
        public string url { get; set; } = "";
        public decimal? quantity { get; set; }
        public decimal? unitPrice { get; set; }
        public string unitLabel { get; set; } = "";
        public string categoryName { get; set; } = "";
    }
}
