using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace NiyazisugarItemci;
public sealed partial class MainForm
{
    private Panel? _liveMarket;
    private ReferenceWatchList? _liveList;
    private readonly Panel _serverSalesPanel = new() {Dock=DockStyle.Fill,Visible=false};
    private readonly ComboBox _liveServer = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 210 };
    private readonly FlowLayoutPanel _liveRows = new() { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
    private readonly Label _liveStatus = new() { Dock = DockStyle.Bottom, Height = 22, ForeColor = Color.FromArgb(150,157,170), Font = new Font("Segoe UI", 8) };
    private readonly System.Windows.Forms.Timer _liveTimer = new() { Interval = 60000 };
    private readonly Label _liveCount=new() { Dock=DockStyle.Fill,ForeColor=PremiumTheme.Gold,TextAlign=ContentAlignment.MiddleCenter,Text="Bugün\n0 tespit" };
    private readonly Label _liveLast=new() { Dock=DockStyle.Fill,ForeColor=PremiumTheme.Turquoise,TextAlign=ContentAlignment.MiddleCenter,Text="Son tespit\n—" };
    private readonly Label _liveVolume=new();
    private readonly Label _livePossible=new();
    private readonly ComboBox _watchPeriod=new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private ReferenceSellerBoard? _sellerBoard;
    private Label? _sellerBoardTitle;
    private string _watchPinnedSeller="";
    private bool _liveBusy;
    private bool _liveCollapsed;
    private bool _updatingLiveServers;

    private void InstallLiveMarket()
    {
        _liveMarket=new Panel {Dock=DockStyle.Fill,BackColor=ReferenceUi.Background,Padding=new Padding(LogicalToDeviceUnits(28),LogicalToDeviceUnits(18),LogicalToDeviceUnits(28),LogicalToDeviceUnits(16))};
        _liveServer.SelectedIndexChanged+=(_,_)=>{if(!_updatingLiveServers){_watchPinnedSeller="";RenderServerSalesWatch();}};var selector=ReferenceUi.Select(_liveServer,250,"server");
        _watchPeriod.Items.AddRange(new object[]{"Bugün","Son 7 gün","Son 30 gün"});_watchPeriod.SelectedIndex=0;
        _watchPeriod.SelectedIndexChanged+=(_,_)=>{_serverWatchRenderedKey="";RenderServerSalesWatch();};var periodSelect=ReferenceUi.Select(_watchPeriod,170,"calendar");
        var refresh=PageLayout.IconButton(this,"refresh","Şimdi yenile",()=>_=RefreshLiveMarketAsync(),_sidebarToolTip);
        var header=PageLayout.HeaderLeft(this,"Satış Takibi",new Label {Text="Sunucu bazında satış hareketleri"},new Control[]{selector,periodSelect},new Control[]{refresh});
        _liveCount.Text="0";_liveLast.Text="—";_liveVolume.Text="0,00 TL";_livePossible.Text="0,00 TL";
        var kpi=new ReferenceKpiCard {Height=LogicalToDeviceUnits(70)}.Add("Tespit edilen satış",_liveCount,ReferenceUi.Gold,true).Add("Tahmini satış hacmi",_liveVolume,ReferenceUi.Aqua).Add("Olası satışlar",_livePossible,ReferenceUi.Gold).Add("Son tespit",_liveLast).Add("Takip durumu",_liveStatus,ReferenceUi.Muted,false,true);
        var legend=new Panel {BackColor=ReferenceUi.Surface,Margin=new Padding(0)};
        legend.Paint+=(_,e)=>{float k=DeviceDpi/96f;var items=new[]{("Kesin / güçlü",ReferenceUi.Aqua),("Olası",ReferenceUi.Gold)};int total=0;foreach(var (text,_) in items)total+=TextRenderer.MeasureText(text,ReferenceUi.Font(9)).Width+(int)(30*k);int x=Math.Max(0,legend.Width-total);e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;foreach(var (text,color) in items){using var dot=new SolidBrush(color);e.Graphics.FillEllipse(dot,x,legend.Height/2-3*k,7*k,7*k);var w=TextRenderer.MeasureText(text,ReferenceUi.Font(9)).Width;TextRenderer.DrawText(e.Graphics,text,ReferenceUi.Font(9),new Rectangle(x+(int)(12*k),0,w,legend.Height),ReferenceUi.Muted,TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding);x+=w+(int)(30*k);}};
        var card=new ReferencePanel {Dock=DockStyle.Fill,Padding=new Padding(1)};
        var cardHeader=PageLayout.CardHeader(this,"Tespit akışı",legend,240);cardHeader.Height=LogicalToDeviceUnits(46);
        _liveList=new ReferenceWatchList(_sidebarToolTip) {SellerClicked=ToggleWatchSeller};
        card.Controls.Add(_liveList);card.Controls.Add(cardHeader);
        _liveRows.Visible=false;
        // Sağda satıcı sıralaması: kim ne kadarlık satış yaptı; tıklanan satıcı akışta üste alınır.
        var sellerCard=new ReferencePanel {Dock=DockStyle.Right,Width=LogicalToDeviceUnits(330),Padding=new Padding(1)};
        _sellerBoardTitle=new Label {Text="Tıkla: satıcının satışları üste gelir",Font=ReferenceUi.Font(8.5f),ForeColor=ReferenceUi.Muted,BackColor=ReferenceUi.Surface,TextAlign=ContentAlignment.MiddleRight,AutoEllipsis=true};
        var sellerHeader=PageLayout.CardHeader(this,"Satıcılar",_sellerBoardTitle,190);sellerHeader.Height=LogicalToDeviceUnits(46);
        _sellerBoard=new ReferenceSellerBoard {Clicked=ToggleWatchSeller};
        sellerCard.Controls.Add(_sellerBoard);sellerCard.Controls.Add(sellerHeader);
        var body=new Panel {Dock=DockStyle.Fill,BackColor=ReferenceUi.Background};
        body.Controls.Add(card);body.Controls.Add(new Panel {Dock=DockStyle.Right,Width=LogicalToDeviceUnits(12),BackColor=ReferenceUi.Background});body.Controls.Add(sellerCard);
        _liveMarket.Controls.Add(body);_liveMarket.Controls.Add(PageLayout.Spacer(this,12));_liveMarket.Controls.Add(kpi);_liveMarket.Controls.Add(PageLayout.Spacer(this,12));_liveMarket.Controls.Add(header);
        _serverSalesPanel.Controls.Add(_liveMarket);_content.Controls.Add(_serverSalesPanel);
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

    private void ToggleWatchSeller(string seller)
    {
        _watchPinnedSeller=string.Equals(_watchPinnedSeller,seller,StringComparison.OrdinalIgnoreCase)?"":seller;
        _serverWatchRenderedKey="";RenderServerSalesWatch();
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
