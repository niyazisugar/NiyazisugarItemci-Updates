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
        _listingsPanel.Dock = DockStyle.Fill; _listingsPanel.BackColor = Bg; _listingsPanel.Padding = new Padding(24);
        var header = new TableLayoutPanel { Dock = DockStyle.Top, Height = 64, ColumnCount = 2 };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,150));
        header.Controls.Add(new Label { Text = "İlanlarım", Dock = DockStyle.Fill, ForeColor = TextMain, Font = new Font("Segoe UI",22,FontStyle.Bold) },0,0);
        var refresh = ActionButton("İlanları Yenile", () => _ = RefreshCurrentListingsAsync(true)); refresh.Dock = DockStyle.Fill; header.Controls.Add(refresh,1,0);
        var filters = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Padding = new Padding(0,8,0,12) };
        _listingServerFilter.Items.Add("Tüm sunucular"); _listingServerFilter.SelectedIndex = 0;
        _listingSearch.TextChanged += (_,_) => { _listingVisibleLimit=50; RenderDesignedListings(); };
        _listingServerFilter.SelectedIndexChanged += (_,_) => { if (!_settingListingFilters) { _listingVisibleLimit=50; RenderDesignedListings(); } };
        filters.Controls.Add(_listingSearch); filters.Controls.Add(_listingServerFilter);
        var create = ActionButton("＋ Hızlı İlan Ekle", () => _ = ShowListingComposerAsync()); create.BackColor = PremiumTheme.Turquoise; create.ForeColor = Color.FromArgb(10,38,38); filters.Controls.Add(create);
        _listingsStatusLabel.Dock = DockStyle.Bottom; _listingsStatusLabel.Height = 38; _listingsStatusLabel.ForeColor = TextMuted; _listingsStatusLabel.Text = "Itemci hesabındaki aktif ilanlar yüklenmeyi bekliyor.";
        _listingsList.Dock = DockStyle.Fill; _listingsList.FlowDirection = FlowDirection.TopDown; _listingsList.WrapContents = false; _listingsList.AutoScroll = true;
        _listingsList.SizeChanged += (_,_) => { foreach(Control row in _listingsList.Controls) row.Width = Math.Max(260,_listingsList.ClientSize.Width-24); };
        var metrics=new TableLayoutPanel { Dock=DockStyle.Top,Height=110,ColumnCount=3,RowCount=1 };
        for(var i=0;i<3;i++)metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,33.333f));
        var cards=new[]{SalesMetricCard("AKTİF İLANLAR",_listingCountValue,"0",PremiumTheme.Gold),SalesMetricCard("SEÇİLİ SUNUCU",_listingServerValue,"Tüm sunucular",TextMain),SalesMetricCard("SON GÜNCELLEME",_listingUpdatedValue,"Henüz yenilenmedi",TextMuted)};
        for(var i=0;i<3;i++){cards[i].Dock=DockStyle.Fill;cards[i].Margin=new Padding(0,0,i<2?12:0,10);metrics.Controls.Add(cards[i],i,0);}
        _listingServerValue.Font=new Font("Segoe UI",11,FontStyle.Bold);_listingUpdatedValue.Font=new Font("Segoe UI",11,FontStyle.Bold);
        _autoBumpStatus.TextChanged+=(_,_)=>{_listingBumpSummary.Text="Ücretsiz yukarı çıkarma · "+_autoBumpStatus.Text;};
        _listingsPanel.Controls.Add(_listingBumpSummary);
        _listingsPanel.Controls.Add(_listingsList); _listingsPanel.Controls.Add(_listingsStatusLabel); _listingsPanel.Controls.Add(_listingSummary); _listingsPanel.Controls.Add(filters); _listingsPanel.Controls.Add(metrics); _listingsPanel.Controls.Add(header);
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
            var server = _marketServers.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s.Url) && href.TrimEnd('/').Equals(s.Url.TrimEnd('/'),StringComparison.OrdinalIgnoreCase));
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
        _listingUpdatedValue.Text=DateTime.Now.ToString("dd.MM.yyyy HH:mm");
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
        _listingSummary.Text = $"{_designedListings.Count} aktif ilan   ·   {rows.Count} eşleşme   ·   Ücretsiz yukarı çıkarma: {(_settings.AutoFreeBumpEnabled ? "Açık" : "Kapalı")}";
        _listingsList.SuspendLayout();
        // Dispose cards and their images when filters change; no hidden image cache.
        while(_listingsList.Controls.Count>0) { var old=_listingsList.Controls[0]; _listingsList.Controls.Remove(old); old.Dispose(); }
        if(rows.Count==0) _listingsList.Controls.Add(new Label { Text="Gösterilecek ilan bulunamadı.",Width=Math.Max(260,_listingsList.ClientSize.Width-24),Height=100,TextAlign=ContentAlignment.MiddleCenter,ForeColor=TextMuted });
        foreach(var item in rows.Take(_listingVisibleLimit))
        {
            var row=new TableLayoutPanel { Width=Math.Max(260,_listingsList.ClientSize.Width-24),Height=104,ColumnCount=4,BackColor=Card,Padding=new Padding(12),Margin=new Padding(0,0,0,10) };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,86)); row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,120)); row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,128));
            row.Paint += (_,e)=>DrawBorder(e.Graphics,row.ClientRectangle);
            var picture=new PictureBox { Dock=DockStyle.Fill,SizeMode=PictureBoxSizeMode.Zoom,BackColor=Bg };
            picture.Disposed += (_,_)=>picture.Image?.Dispose();
            _=LoadListingThumbnailAsync(picture,item.ImageUrl);
            row.Controls.Add(picture,0,0);
            var description=new TableLayoutPanel { Dock=DockStyle.Fill,RowCount=2,ColumnCount=1,Padding=new Padding(10,4,8,0) };
            description.RowStyles.Add(new RowStyle(SizeType.Percent,60)); description.RowStyles.Add(new RowStyle(SizeType.Percent,40));
            description.Controls.Add(new Label { Text=item.Title,Dock=DockStyle.Fill,AutoEllipsis=true,ForeColor=TextMain,Font=new Font("Segoe UI",10,FontStyle.Bold) },0,0);
            description.Controls.Add(new Label { Text=item.Server,Dock=DockStyle.Fill,AutoEllipsis=true,ForeColor=PremiumTheme.Gold },0,1); row.Controls.Add(description,1,0);
            var amounts=new Label { Text=item.PriceText+"\n"+item.Status,Dock=DockStyle.Fill,ForeColor=PremiumTheme.Gold,TextAlign=ContentAlignment.MiddleCenter }; row.Controls.Add(amounts,2,0);
            var actions=new FlowLayoutPanel { Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false };
            var copy=ActionButton("Linki Kopyala",()=>{ if(!string.IsNullOrWhiteSpace(item.Url)) Clipboard.SetText(item.Url); }); copy.Width=116; copy.Height=30;
            var open=ActionButton("İlanı Aç",()=>{ ShowPanel(_browserPanel); _=OpenOrActivateBrowserTabAsync(item.Url,item.Title); });open.Width=116;open.Height=30;
            actions.Controls.Add(copy);actions.Controls.Add(open);row.Controls.Add(actions,3,0); _listingsList.Controls.Add(row);
        }
        if(rows.Count>_listingVisibleLimit) { var more=ActionButton($"Kalan {rows.Count-_listingVisibleLimit} ilanı göster",()=>{_listingVisibleLimit+=50;RenderDesignedListings();}); _listingsList.Controls.Add(more); }
        _listingsList.ResumeLayout();
    }
}
