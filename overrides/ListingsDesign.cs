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
    private bool _settingListingFilters;
    private int _listingVisibleLimit = 50;
    private void BuildListingsPanel()
    {
        _listingsPanel.Dock=DockStyle.Fill;_listingsPanel.BackColor=ReferenceUi.Background;_listingsPanel.Padding=new Padding(30,24,30,20);
        var header=new TableLayoutPanel {Dock=DockStyle.Top,Height=98,ColumnCount=2,RowCount=1};header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,132));header.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        var headings=new Panel {Dock=DockStyle.Fill,BackColor=ReferenceUi.Background};var title=ReferenceUi.Label("İlanlarım",28,true);title.Dock=DockStyle.Top;title.Height=52;var subtitle=ReferenceUi.Label("İlanlarını tek yerden görüntüle ve yönet.",12);subtitle.ForeColor=ReferenceUi.Muted;subtitle.Dock=DockStyle.Bottom;subtitle.Height=35;headings.Controls.Add(title);headings.Controls.Add(subtitle);header.Controls.Add(headings,0,0);
        var refresh=ReferenceUi.Button("Yenile","refresh",()=>_=RefreshCurrentListingsAsync(true),120);refresh.Anchor=AnchorStyles.Right|AnchorStyles.Top;refresh.Margin=new Padding(0,15,0,0);header.Controls.Add(refresh,1,0);
        var metrics=new TableLayoutPanel {Dock=DockStyle.Top,Height=108,ColumnCount=3,RowCount=1,Padding=new Padding(0,0,0,14)};metrics.RowStyles.Add(new RowStyle(SizeType.Percent,100));for(int i=0;i<3;i++)metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,33.333f));
        _listingCountValue.Text="0";_listingServerValue.Text="Tüm Sunucular";_listingUpdatedValue.Text="Henüz yenilenmedi";
        metrics.Controls.Add(ReferenceUi.Metric("Aktif İlanlar",_listingCountValue,"list",ReferenceUi.Gold),0,0);metrics.Controls.Add(ReferenceUi.Metric("Seçili Sunucu",_listingServerValue,"server",ReferenceUi.Gold),1,0);metrics.Controls.Add(ReferenceUi.Metric("Son Güncelleme",_listingUpdatedValue,"clock",ReferenceUi.Gold),2,0);metrics.Controls[2].Margin=new Padding(0,0,0,0);
        var filters=new FlowLayoutPanel {Dock=DockStyle.Top,Height=64,WrapContents=true,BackColor=ReferenceUi.Background};
        _listingServerFilter.Items.Add("Tüm sunucular");_listingServerFilter.SelectedIndex=0;
        _listingSearch.TextChanged+=(_,_)=>{_listingVisibleLimit=50;RenderDesignedListings();};_listingServerFilter.SelectedIndexChanged+=(_,_)=>{if(!_settingListingFilters){_listingVisibleLimit=50;RenderDesignedListings();}};
        var search=ReferenceUi.Search(_listingSearch,560);var servers=ReferenceUi.Select(_listingServerFilter,300);filters.Controls.Add(search);filters.Controls.Add(servers);var status=new ReferenceBadge {Text="Aktif İlanlar",Status=true,Width=150,Height=46};filters.Controls.Add(status);
        filters.SizeChanged+=(_,_)=>{search.Width=Math.Max(270,filters.Width-480);filters.Height=filters.Width<850?112:64;};
        var table=new ReferencePanel {Dock=DockStyle.Fill,Padding=new Padding(1)};
        var columns=new ReferenceListHeading {Dock=DockStyle.Top,Height=44};
        _listingsList.Dock=DockStyle.Fill;_listingsList.FlowDirection=FlowDirection.TopDown;_listingsList.WrapContents=false;_listingsList.AutoScroll=true;_listingsList.Padding=new Padding(0);_listingsList.BackColor=ReferenceUi.Surface;
        _listingsList.SizeChanged+=(_,_)=>{foreach(Control row in _listingsList.Controls)row.Width=Math.Max(920,_listingsList.ClientSize.Width-(_listingsList.VerticalScroll.Visible?20:2));};
        _listingSummary.Dock=DockStyle.Bottom;_listingSummary.Height=40;_listingSummary.Padding=new Padding(18,0,18,0);_listingSummary.ForeColor=ReferenceUi.Muted;_listingSummary.Font=ReferenceUi.Font(10);
        table.Controls.Add(_listingsList);table.Controls.Add(_listingSummary);table.Controls.Add(columns);
        _listingsStatusLabel.Dock=DockStyle.Bottom;_listingsStatusLabel.Height=25;_listingsStatusLabel.ForeColor=ReferenceUi.Muted;_listingsStatusLabel.Font=ReferenceUi.Font(9);_listingsStatusLabel.Text="İlan bilgileri son yenilemeye aittir.";
        _listingBumpSummary.Dock=DockStyle.Bottom;_listingBumpSummary.Height=24;_listingBumpSummary.ForeColor=ReferenceUi.Gold;_listingBumpSummary.Font=ReferenceUi.Font(9);
        _autoBumpStatus.TextChanged+=(_,_)=>_listingBumpSummary.Text="Ücretsiz yukarı çıkarma · "+_autoBumpStatus.Text;
        _listingsPanel.Controls.Add(table);_listingsPanel.Controls.Add(_listingsStatusLabel);_listingsPanel.Controls.Add(_listingBumpSummary);_listingsPanel.Controls.Add(filters);_listingsPanel.Controls.Add(metrics);_listingsPanel.Controls.Add(header);
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
        var rows=_designedListings.Where(x=>(server is null or "Tüm sunucular" || x.Server==server) && (query.Length==0 || x.Title.Contains(query,StringComparison.CurrentCultureIgnoreCase) || x.Server.Contains(query,StringComparison.CurrentCultureIgnoreCase))).ToList();
        _listingCountValue.Text=_designedListings.Count.ToString(); _listingServerValue.Text=server??"Tüm sunucular";
        _listingSummary.Text=$"{rows.Count} ilan gösteriliyor · İlan bilgileri son yenilemeye aittir.";
        _listingsList.SuspendLayout();
        // Dispose cards and their images when filters change; no hidden image cache.
        while(_listingsList.Controls.Count>0) { var old=_listingsList.Controls[0]; _listingsList.Controls.Remove(old); old.Dispose(); }
        if(rows.Count==0) _listingsList.Controls.Add(new Label { Text="Gösterilecek ilan bulunamadı.",Width=Math.Max(260,_listingsList.ClientSize.Width-24),Height=100,TextAlign=ContentAlignment.MiddleCenter,ForeColor=TextMuted });
        foreach(var item in rows.Take(_listingVisibleLimit))
        {
            var row=new ReferenceRow {Width=Math.Max(920,_listingsList.ClientSize.Width-(_listingsList.VerticalScroll.Visible?20:2)),Height=82,Margin=new Padding(0)};
            var cells=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=5,RowCount=1,Margin=new Padding(0),Padding=new Padding(14,0,14,0),BackColor=ReferenceUi.Surface};cells.RowStyles.Add(new RowStyle(SizeType.Percent,100));foreach(float part in new[]{40f,12f,14f,10f,24f})cells.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,part));
            var product=new Panel {Dock=DockStyle.Fill,BackColor=ReferenceUi.Surface,Padding=new Padding(0,8,8,8)};
            var picture=new PictureBox {Dock=DockStyle.Left,Width=66,SizeMode=PictureBoxSizeMode.Zoom,BackColor=ReferenceUi.Field};picture.Disposed+=(_,_)=>picture.Image?.Dispose();_=LoadListingThumbnailAsync(picture,item.ImageUrl);
            var title=ReferenceUi.Label(item.Title,11,true);title.Dock=DockStyle.Fill;title.TextAlign=ContentAlignment.MiddleLeft;title.Padding=new Padding(16,0,0,0);product.Controls.Add(title);product.Controls.Add(picture);cells.Controls.Add(product,0,0);
            var serverLabel=new ReferenceBadge {Text=item.Server,Dock=DockStyle.Fill};cells.Controls.Add(serverLabel,1,0);
            var price=ReferenceUi.Label(item.PriceText,13,true);price.ForeColor=ReferenceUi.Gold;price.Dock=DockStyle.Fill;price.TextAlign=ContentAlignment.MiddleCenter;cells.Controls.Add(price,2,0);
            var state=new ReferenceBadge {Text=item.Status=="AKTİF"?"Aktif":item.Status,Status=item.Status=="AKTİF",Dock=DockStyle.Fill};cells.Controls.Add(state,3,0);
            var actions=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Padding=new Padding(8,18,0,18),BackColor=ReferenceUi.Surface};actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));actions.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            var copy=ReferenceUi.Button("Linki Kopyala","copy",()=>{if(!string.IsNullOrWhiteSpace(item.Url))Clipboard.SetText(item.Url);});copy.Dock=DockStyle.Fill;copy.Margin=new Padding(0,0,8,0);copy.Font=ReferenceUi.Font(9);
            var open=ReferenceUi.Button("İlanı Aç","open",()=>{ShowPanel(_browserPanel);_=OpenOrActivateBrowserTabAsync(item.Url,item.Title);});open.Dock=DockStyle.Fill;open.Margin=new Padding(0);open.Font=ReferenceUi.Font(9);actions.Controls.Add(copy,0,0);actions.Controls.Add(open,1,0);cells.Controls.Add(actions,4,0);
            row.Controls.Add(cells);_listingsList.Controls.Add(row);
        }
        if(rows.Count>_listingVisibleLimit) { var more=ActionButton($"Kalan {rows.Count-_listingVisibleLimit} ilanı göster",()=>{_listingVisibleLimit+=50;RenderDesignedListings();}); _listingsList.Controls.Add(more); }
        _listingsList.ResumeLayout();
    }
}
