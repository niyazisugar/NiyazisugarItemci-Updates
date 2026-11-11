using HtmlAgilityPack;
namespace NiyazisugarItemci;
public sealed partial class MainForm
{
    private List<ListingInfo> _designedListings = new();
    private readonly TextBox _listingSearch = new() { PlaceholderText = "İlan başlığı veya sunucu ara…", Width = 300 };
    private readonly ComboBox _listingServerFilter = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 210 };
    private readonly Label _listingSummary = new() { Dock = DockStyle.Top, Height = 40, ForeColor = PremiumTheme.Gold, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Label _listingCountValue=new(), _listingServerValue=new(), _listingUpdatedValue=new();
    private readonly Label _listingBumpSummary=new() { Dock=DockStyle.Bottom,Height=40,ForeColor=PremiumTheme.Gold };
    private readonly ComboBox _listingStatusFilter=new() {DropDownStyle=ComboBoxStyle.DropDownList};
    private bool _settingListingFilters;
    private int _listingVisibleLimit = 50;
    private readonly Label _listingSubtitle = new() { Text = "İlanlarını tek yerden görüntüle ve yönet" };
    private readonly Label _listingBumpValue = new() { Text = "—" };
    private void BuildListingsPanel()
    {
        _listingsPanel.Dock=DockStyle.Fill;_listingsPanel.BackColor=ReferenceUi.Background;_listingsPanel.Padding=PageLayout.PagePadding(this);
        _listingServerFilter.Items.Add("Tüm sunucular");_listingServerFilter.SelectedIndex=0;
        _listingSearch.TextChanged+=(_,_)=>{_listingVisibleLimit=50;RenderDesignedListings();};_listingServerFilter.SelectedIndexChanged+=(_,_)=>{if(!_settingListingFilters){_listingVisibleLimit=50;RenderDesignedListings();}};
        _listingStatusFilter.Items.AddRange(new object[]{"Aktif","Pasif","Tümü"});_listingStatusFilter.SelectedIndex=0;_listingStatusFilter.SelectedIndexChanged+=(_,_)=>{_listingVisibleLimit=50;RenderDesignedListings();};
        var status=new ReferenceSegmented(_listingStatusFilter) {Dot=i=>i==0?(Color?)ReferenceUi.Aqua:null};status.Width=status.PreferredWidthFor();
        var servers=ReferenceUi.Select(_listingServerFilter,190);
        var refresh=PageLayout.IconButton(this,"refresh","Yenile",()=>_=RefreshCurrentListingsAsync(true),_sidebarToolTip);
        var header=PageLayout.Header(this,"İlanlarım",_listingSubtitle,status,servers,refresh);
        _listingCountValue.Text="0";_listingServerValue.Text="Tümü";_listingUpdatedValue.Text="—";
        var kpi=new ReferenceKpiCard {Height=LogicalToDeviceUnits(84)}.Add("Aktif ilan",_listingCountValue,ReferenceUi.Gold,true).Add("Sunucu",_listingServerValue).Add("Son güncelleme",_listingUpdatedValue).Add("Ücretsiz yukarı taşıma",_listingBumpValue,ReferenceUi.Muted,false,true);
        _listingBumpSummary.Visible=false;
        var table=new ReferencePanel {Dock=DockStyle.Fill,Padding=new Padding(1)};
        var search=ReferenceUi.Search(_listingSearch,260);
        var cardHeader=PageLayout.CardHeader(this,"İlanlar",search,260);
        var columns=new ReferenceListHeading {Dock=DockStyle.Top,Height=LogicalToDeviceUnits(38)};
        _listingsList.Dock=DockStyle.Fill;_listingsList.FlowDirection=FlowDirection.TopDown;_listingsList.WrapContents=false;_listingsList.AutoScroll=true;_listingsList.Padding=new Padding(0);_listingsList.BackColor=ReferenceUi.Surface;
        _listingsList.SizeChanged+=(_,_)=>{foreach(Control row in _listingsList.Controls){row.Width=row is ReferenceEmptyState?Math.Max(260,_listingsList.ClientSize.Width-2):Math.Max(760,_listingsList.ClientSize.Width-(_listingsList.VerticalScroll.Visible?20:2));if(row is ReferenceEmptyState)row.Height=Math.Max(LogicalToDeviceUnits(240),_listingsList.ClientSize.Height-4);}};
        _listingSummary.Dock=DockStyle.Bottom;_listingSummary.Height=LogicalToDeviceUnits(34);_listingSummary.Padding=new Padding(LogicalToDeviceUnits(20),0,LogicalToDeviceUnits(18),0);_listingSummary.ForeColor=ReferenceUi.Muted;_listingSummary.BackColor=ReferenceUi.Surface;_listingSummary.Font=ReferenceUi.Font(8.5f);
        table.Controls.Add(_listingsList);table.Controls.Add(_listingSummary);table.Controls.Add(columns);table.Controls.Add(cardHeader);
        _listingsStatusLabel.Text="";
        var banner=new ReferenceInfoBanner(_listingsStatusLabel) {Height=LogicalToDeviceUnits(52)};
        _autoBumpStatus.TextChanged+=(_,_)=>{_listingBumpSummary.Text="Ücretsiz yukarı taşıma · "+_autoBumpStatus.Text;_listingBumpValue.Text=_autoBumpStatus.Text;};
        _listingsPanel.Controls.Add(table);_listingsPanel.Controls.Add(banner);_listingsPanel.Controls.Add(PageLayout.Spacer(this,16));_listingsPanel.Controls.Add(kpi);_listingsPanel.Controls.Add(PageLayout.Spacer(this,18));_listingsPanel.Controls.Add(header);
    }
    private ReferenceEmptyState ListingsEmptyState()
    {
        bool none=_designedListings.Count==0;
        var profile=PageLayout.Action("Profili aç","open",()=>{ShowPanel(_browserPanel);var url=string.IsNullOrWhiteSpace(_settings.OwnStoreUrl)?"https://itemci.com/":_settings.OwnStoreUrl;_=OpenOrActivateBrowserTabAsync(url,"Profil");},110);
        var add=PageLayout.Action("İlan ekle","plus",()=>_=ShowListingComposerAsync(),110,true);
        var state=new ReferenceEmptyState("list",none?"Aktif ilan bulunamadı":"Filtreye uyan ilan yok",none?"Itemci mağaza/profil linkin algılanamadıysa profil sayfanı bir kez açıp yenile ya da yeni bir ilan ekle.":"Aramayı, durumu veya sunucu seçimini değiştirerek tekrar dene.",profile,add);
        state.Width=Math.Max(260,_listingsList.ClientSize.Width-2);state.Height=Math.Max(LogicalToDeviceUnits(240),_listingsList.ClientSize.Height-4);state.Margin=new Padding(0);
        return state;
    }
    private async Task LoadListingThumbnailAsync(PictureBox target,string address)
    {
        var bitmap=await ListingThumbnails.LoadAsync(address,()=>!IsDisposed&&!target.IsDisposed);
        if(bitmap is null)return;if(IsDisposed||target.IsDisposed){bitmap.Dispose();return;}target.Image=bitmap;
    }
    private string ReadListingServer(HtmlNode container)
    {
        foreach(var node in container.Descendants("a"))
        {
            var href = NormalizeItemciUrl(node.GetAttributeValue("href","")) ?? "";
            var server = _marketServers.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s.Url) && (href.TrimEnd('/').Equals(s.Url.TrimEnd('/'),StringComparison.OrdinalIgnoreCase) || href.TrimEnd('/').Equals(s.Url.TrimEnd('/')+"-item-yang",StringComparison.OrdinalIgnoreCase)));
            if(server is null && Uri.TryCreate(href,UriKind.Absolute,out var categoryUri) && categoryUri.AbsolutePath.StartsWith("/categories/",StringComparison.OrdinalIgnoreCase)) server=_marketServers.FirstOrDefault(s=>NormalizeHtmlText(node.InnerText).Equals(s.Name,StringComparison.CurrentCultureIgnoreCase));
            if(server is not null) return server.Name;
        }
        var explicitName = container.GetAttributeValue("data-server","");
        return string.IsNullOrWhiteSpace(explicitName) ? "Bilinmeyen sunucu" : NormalizeHtmlText(explicitName);
    }
    private static string ReadListingImage(HtmlNode container)
    {
        var img = container.Descendants("img").FirstOrDefault();
        var value = img?.GetAttributeValue("data-src","");
        if(string.IsNullOrWhiteSpace(value)) value = img?.GetAttributeValue("src","");
        return value ?? "";
    }
    private void RenderCurrentListings(List<ListingInfo> listings)
    {
        _designedListings = listings;
        _listingUpdatedValue.Text=DateTime.Now.ToString("HH:mm");
        var selected = _listingServerFilter.SelectedItem?.ToString(); _settingListingFilters = true;
        _listingServerFilter.Items.Clear(); _listingServerFilter.Items.Add("Tüm sunucular");
        foreach(var name in listings.Select(x=>x.Server).Distinct().OrderBy(x=>x)) _listingServerFilter.Items.Add(name);
        _listingServerFilter.SelectedItem = selected; if(_listingServerFilter.SelectedIndex<0) _listingServerFilter.SelectedIndex=0;
        _settingListingFilters=false; RenderDesignedListings();
    }
    private void RenderDesignedListings()
    {
        var query=_listingSearch.Text.Trim(); var server=_listingServerFilter.SelectedItem?.ToString();
        var rows=_designedListings.Where(x=>(server is null or "Tüm sunucular" || x.Server==server) && (_listingStatusFilter.SelectedIndex==2 || (_listingStatusFilter.SelectedIndex==0)==(x.Status=="AKTİF")) && (query.Length==0 || x.Title.Contains(query,StringComparison.CurrentCultureIgnoreCase) || x.Server.Contains(query,StringComparison.CurrentCultureIgnoreCase))).ToList();
        _listingCountValue.Text=_designedListings.Count(x=>x.Status=="AKTİF").ToString(); _listingServerValue.Text=server is null or "Tüm sunucular"?"Tümü":server;
        _listingSummary.Text=$"{rows.Count} ilan gösteriliyor · İlan bilgileri son yenilemeye aittir.";
        _listingsList.SuspendLayout();
        // Dispose cards and their images when filters change; no hidden image cache.
        while(_listingsList.Controls.Count>0) { var old=_listingsList.Controls[0]; _listingsList.Controls.Remove(old); old.Dispose(); }
        _listingsList.AutoScroll=rows.Count>0;
        if(rows.Count==0) _listingsList.Controls.Add(ListingsEmptyState());
        foreach(var item in rows.Take(_listingVisibleLimit))
        {
            var row=new ReferenceRow {Width=Math.Max(760,_listingsList.ClientSize.Width-(_listingsList.VerticalScroll.Visible?20:2)),Height=LogicalToDeviceUnits(60),Margin=new Padding(0),Padding=new Padding(0,0,0,1)};
            var cells=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=5,RowCount=1,Margin=new Padding(0),Padding=new Padding(LogicalToDeviceUnits(16),0,LogicalToDeviceUnits(16),0),BackColor=ReferenceUi.Surface};cells.RowStyles.Add(new RowStyle(SizeType.Percent,100));foreach(float part in new[]{40f,12f,14f,10f,24f})cells.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,part));
            var product=new Panel {Dock=DockStyle.Fill,BackColor=ReferenceUi.Surface,Padding=new Padding(0,LogicalToDeviceUnits(9),8,LogicalToDeviceUnits(9))};
            var picture=new PictureBox {Dock=DockStyle.Left,Width=LogicalToDeviceUnits(52),SizeMode=PictureBoxSizeMode.Zoom,BackColor=ReferenceUi.Field};picture.Disposed+=(_,_)=>picture.Image?.Dispose();_=LoadListingThumbnailAsync(picture,item.ImageUrl);
            var title=ReferenceUi.Label(item.Title,9.5f,true);title.Dock=DockStyle.Fill;title.TextAlign=ContentAlignment.MiddleLeft;title.Padding=new Padding(LogicalToDeviceUnits(12),0,0,0);product.Controls.Add(title);product.Controls.Add(picture);cells.Controls.Add(product,0,0);
            var serverLabel=new ReferenceBadge {Text=item.Server,Dock=DockStyle.Fill};cells.Controls.Add(serverLabel,1,0);
            var price=ReferenceUi.Label(item.PriceText,10,true);price.ForeColor=ReferenceUi.Gold;price.Dock=DockStyle.Fill;price.TextAlign=ContentAlignment.MiddleCenter;cells.Controls.Add(price,2,0);
            var state=new ReferenceBadge {Text=item.Status=="AKTİF"?"Aktif":item.Status,Status=item.Status=="AKTİF",Dock=DockStyle.Fill};cells.Controls.Add(state,3,0);
            var actions=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Padding=new Padding(8,LogicalToDeviceUnits(13),0,LogicalToDeviceUnits(13)),BackColor=ReferenceUi.Surface};actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));actions.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            var copy=ReferenceUi.Button("Kopyala","copy",()=>{if(!string.IsNullOrWhiteSpace(item.Url))Clipboard.SetText(item.Url);});copy.Dock=DockStyle.Fill;copy.Margin=new Padding(0,0,8,0);copy.Font=ReferenceUi.Font(8.5f);
            var open=ReferenceUi.Button("Aç","open",()=>{ShowPanel(_browserPanel);_=OpenOrActivateBrowserTabAsync(item.Url,item.Title);});open.Dock=DockStyle.Fill;open.Margin=new Padding(0);open.Font=ReferenceUi.Font(8.5f);actions.Controls.Add(copy,0,0);actions.Controls.Add(open,1,0);cells.Controls.Add(actions,4,0);
            _sidebarToolTip.SetToolTip(copy,"İlanın gerçek bağlantısını kopyala");_sidebarToolTip.SetToolTip(open,"İlanı mevcut tarayıcıda aç");
            row.Controls.Add(cells);_listingsList.Controls.Add(row);
        }
        if(rows.Count>_listingVisibleLimit) { var more=ActionButton($"Kalan {rows.Count-_listingVisibleLimit} ilanı göster",()=>{_listingVisibleLimit+=50;RenderDesignedListings();}); _listingsList.Controls.Add(more); }
        _listingsList.ResumeLayout();
    }
}
