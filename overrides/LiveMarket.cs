using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace NiyazisugarItemci;
public sealed partial class MainForm
{
    private Panel? _liveMarket;
    private readonly Panel _serverSalesPanel = new() {Dock=DockStyle.Fill,Visible=false};
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
        _liveMarket=new Panel {Dock=DockStyle.Fill,BackColor=ReferenceUi.Background,Padding=PageLayout.PagePadding(this)};
        var refresh=PageLayout.Action("Şimdi yenile","refresh",()=>_=RefreshLiveMarketAsync(),120);
        var header=PageLayout.Header(this,"Satıcı alanı","Satış Takibi",refresh);
        _liveCount.Text="0";_liveLast.Text="—";
        var strip=new ReferenceStatStrip {Height=LogicalToDeviceUnits(46)}.Add("Bugün tespit",_liveCount,ReferenceUi.Gold).Add("Son tespit",_liveLast).Right(_liveStatus);
        _liveStatus.Visible=false;
        var filters=new TableLayoutPanel {Dock=DockStyle.Top,Height=LogicalToDeviceUnits(58),ColumnCount=2,RowCount=1,BackColor=ReferenceUi.Background,Padding=new Padding(0,LogicalToDeviceUnits(14),0,LogicalToDeviceUnits(8))};
        filters.RowStyles.Add(new RowStyle(SizeType.Percent,100));filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,LogicalToDeviceUnits(280)));filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        _liveServer.SelectedIndexChanged+=(_,_)=>{if(!_updatingLiveServers)RenderServerSalesWatch();};var selector=ReferenceUi.Select(_liveServer,280,"server");selector.Dock=DockStyle.Fill;selector.Margin=new Padding(0);
        var legend=new Panel {Dock=DockStyle.Fill,BackColor=ReferenceUi.Background,Margin=new Padding(LogicalToDeviceUnits(16),0,0,0)};
        legend.Paint+=(_,e)=>{float k=DeviceDpi/96f;int x=0;foreach(var (text,color) in new[]{("Güçlü sinyal",ReferenceUi.Aqua),("Olası satış",ReferenceUi.Gold)}){using var dot=new SolidBrush(color);e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;e.Graphics.FillEllipse(dot,x,legend.Height/2-3*k,7*k,7*k);var w=TextRenderer.MeasureText(text,ReferenceUi.Font(9)).Width;TextRenderer.DrawText(e.Graphics,text,ReferenceUi.Font(9),new Rectangle(x+(int)(12*k),0,w,legend.Height),ReferenceUi.Muted,TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding);x+=w+(int)(30*k);}};
        filters.Controls.Add(selector,0,0);filters.Controls.Add(legend,1,0);
        var card=new ReferencePanel {Dock=DockStyle.Fill,Padding=new Padding(1)};
        var disclaimer=ReferenceUi.Label("Liste + sayaç eşleşmesi kesin satış değildir. Son görülen ilan fiyatı gösterilir.",8.5f);disclaimer.Dock=DockStyle.Bottom;disclaimer.Height=LogicalToDeviceUnits(32);disclaimer.ForeColor=ReferenceUi.Muted;disclaimer.BackColor=ReferenceUi.Surface;disclaimer.TextAlign=ContentAlignment.MiddleLeft;disclaimer.Padding=new Padding(LogicalToDeviceUnits(18),0,0,0);
        _liveRows.BackColor=ReferenceUi.Surface;_liveRows.Padding=new Padding(LogicalToDeviceUnits(10),LogicalToDeviceUnits(10),LogicalToDeviceUnits(10),0);
        card.Controls.Add(_liveRows);card.Controls.Add(disclaimer);
        _liveMarket.Controls.Add(card);_liveMarket.Controls.Add(filters);_liveMarket.Controls.Add(strip);_liveMarket.Controls.Add(PageLayout.Spacer(this,6));_liveMarket.Controls.Add(header);
        _serverSalesPanel.Controls.Add(_liveMarket);_content.Controls.Add(_serverSalesPanel);
        _liveRows.SizeChanged+=(_,_)=>{foreach(Control row in _liveRows.Controls){row.Width=Math.Max(40,_liveRows.ClientSize.Width-_liveRows.Padding.Horizontal-4);if(row is ReferenceEmptyState)row.Height=Math.Max(LogicalToDeviceUnits(240),_liveRows.ClientSize.Height-_liveRows.Padding.Vertical-6);}};
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
