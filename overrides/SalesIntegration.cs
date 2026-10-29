using System.Globalization;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace NiyazisugarItemci;

public sealed partial class MainForm
{
    private readonly DateTimePicker _salesDate = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "dd.MM.yyyy", Width = 145 };
    private readonly ComboBox _salesServerFilter = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 190 };
    private readonly TextBox _salesSearch = new() { PlaceholderText = "Satılan ürün ara…", Width = 230 };
    private readonly Label _salesTotals = new() { Dock = DockStyle.Bottom, Height = 54, ForeColor = PremiumTheme.Gold, TextAlign = ContentAlignment.MiddleRight };
    private bool _salesFiltering;
    private bool _salesAllTime = true;
    private ReferenceButton? _salesAllTimeButton;
    private Label? _salesPeriodLabel;
    private WebView2? _soldScanView;
    private bool _soldScanBusy;
    private DateTime _salesNextRefreshUtc;
    private bool _soldCacheLoaded;
    private int _salesResumePage = 1;
    private readonly HashSet<string> _salesPageProcessed = new();
    private string _salesPreviousPage = "";
    private bool _salesCycleHadErrors;
    private void ResetSalesScanCursor()
    {
        _salesResumePage=1;_salesPageProcessed.Clear();_salesPreviousPage="";_salesCycleHadErrors=false;
    }
    private void AdvanceSalesScanCursor(SoldScanResult result)
    {
        foreach(var id in result.ProcessedIds)_salesPageProcessed.Add(id);
        if(result.PageComplete){_salesPreviousPage=string.Join("|",result.ListingIds);_salesResumePage++;_salesPageProcessed.Clear();}
    }
    private static DateTime TurkeyToday
    {
        get
        {
            try { return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time")).Date; }
            catch { return DateTime.Today; }
        }
    }

    private readonly ComboBox _salesPeriodModel = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private bool _salesPeriodSync;
    private void OpenSalesCalendar(Control anchor)
    {
        using var picker=new Form {Text="Satış günü",FormBorderStyle=FormBorderStyle.FixedToolWindow,StartPosition=FormStartPosition.Manual,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,BackColor=ReferenceUi.Surface,Padding=new Padding(8),ShowInTaskbar=false};
        var calendar=new MonthCalendar {MaxSelectionCount=1,SelectionStart=_salesDate.Value,SelectionEnd=_salesDate.Value};
        calendar.DateSelected+=(_,e)=>{_salesDate.Value=e.Start;SetSalesPeriod(false);picker.Close();};picker.Controls.Add(calendar);
        var point=anchor.PointToScreen(new Point(0,anchor.Height));var area=Screen.FromControl(anchor).WorkingArea;picker.Location=new Point(Math.Min(point.X,area.Right-270),Math.Min(point.Y,area.Bottom-240));
        picker.ShowDialog(this);
        RenderSoldSalesPanel();
    }
    private void BuildSoldSalesPanel()
    {
        _salesPanel.Dock=DockStyle.Fill;_salesPanel.BackColor=ReferenceUi.Background;_salesPanel.Padding=PageLayout.PagePadding(this);
        _salesPeriodLabel=new Label {Text="Kayıtlı tüm satışlar · sunucu ve arama filtreleri uygulanır"};
        foreach(var label in new[]{_salesGrossLabel,_salesCommissionLabel,_salesNetLabel})label.Text="0,00 TL";_salesCountLabel.Text="0";
        _salesStatusLabel.Text="Satış geçmişi yüklenmeyi bekliyor";
        _salesDate.Value=TurkeyToday;_salesDate.ValueChanged+=(_,_)=>SetSalesPeriod(false);
        _salesPeriodModel.Items.AddRange(new object[]{"Tümü","Bugün","Tarih seç"});_salesPeriodModel.SelectedIndex=0;
        var period=new ReferenceSegmented(_salesPeriodModel);
        _salesPeriodModel.SelectedIndexChanged+=(_,_)=>{if(_salesPeriodSync)return;switch(_salesPeriodModel.SelectedIndex){case 0:SetSalesPeriod(true);break;case 1:_salesDate.Value=TurkeyToday;SetSalesPeriod(false);break;case 2:OpenSalesCalendar(period);break;}};
        period.Reselected+=i=>{if(i==2)OpenSalesCalendar(period);};
        period.Width=period.PreferredWidthFor()+LogicalToDeviceUnits(40);
        _salesServerFilter.Items.Add("Tüm sunucular");_salesServerFilter.SelectedIndex=0;_salesServerFilter.SelectedIndexChanged+=(_,_)=>{if(!_salesFiltering)RenderSoldSalesPanel();};_salesSearch.TextChanged+=(_,_)=>RenderSoldSalesPanel();
        var servers=ReferenceUi.Select(_salesServerFilter,190);
        var history=PageLayout.IconButton(this,"clock","Tüm geçmişi tara",()=>{SetSalesPeriod(true);_=RefreshSoldListingsAsync(true,true);},_sidebarToolTip);
        var refresh=PageLayout.IconButton(this,"refresh","Yenile",()=>_=RefreshSoldListingsAsync(true),_sidebarToolTip);
        var header=PageLayout.Header(this,"Satışlar ve Kazanç",_salesPeriodLabel,period,servers,history,refresh);
        var kpi=new ReferenceKpiCard {Height=LogicalToDeviceUnits(84)}.Add("Satış toplamı",_salesGrossLabel).Add("Komisyon · %10",_salesCommissionLabel,Color.FromArgb(201,202,207)).Add("Net kazanç",_salesNetLabel,ReferenceUi.Gold,true).Add("Satış adedi",_salesCountLabel);
        var search=ReferenceUi.Search(_salesSearch,260);
        _salesGrid.Dock=DockStyle.Fill;_salesGrid.Tag="reference";_salesGrid.ReadOnly=true;_salesGrid.AllowUserToAddRows=false;_salesGrid.AllowUserToDeleteRows=false;_salesGrid.AllowUserToResizeRows=false;_salesGrid.RowHeadersVisible=false;_salesGrid.BorderStyle=BorderStyle.None;_salesGrid.BackgroundColor=ReferenceUi.Surface;_salesGrid.GridColor=ReferenceUi.Line;_salesGrid.CellBorderStyle=DataGridViewCellBorderStyle.SingleHorizontal;_salesGrid.ColumnHeadersBorderStyle=DataGridViewHeaderBorderStyle.None;_salesGrid.EnableHeadersVisualStyles=false;_salesGrid.ColumnHeadersDefaultCellStyle=new DataGridViewCellStyle {BackColor=ReferenceUi.Surface,ForeColor=ReferenceUi.Muted,Font=ReferenceUi.Font(9),Padding=new Padding(8),Alignment=DataGridViewContentAlignment.MiddleLeft};_salesGrid.DefaultCellStyle=new DataGridViewCellStyle {BackColor=ReferenceUi.Surface,ForeColor=ReferenceUi.Text,Font=ReferenceUi.Font(9.5f),SelectionBackColor=Color.FromArgb(31,32,36),SelectionForeColor=ReferenceUi.Text,Padding=new Padding(10,0,10,0)};_salesGrid.AlternatingRowsDefaultCellStyle.BackColor=ReferenceUi.Surface;_salesGrid.ColumnHeadersHeight=LogicalToDeviceUnits(38);_salesGrid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;_salesGrid.SelectionMode=DataGridViewSelectionMode.FullRowSelect;_salesGrid.Columns.Clear();
        _salesGrid.Columns.Add(new DataGridViewImageColumn {Name="Cover",HeaderText="",ImageLayout=DataGridViewImageCellLayout.Zoom,DefaultCellStyle=new DataGridViewCellStyle {NullValue=null,Padding=new Padding(12,7,2,7)},Width=LogicalToDeviceUnits(66),AutoSizeMode=DataGridViewAutoSizeColumnMode.None});
        foreach(var col in new[]{("Item","Satılan Ürün",210f),("Server","Sunucu",100f),("Date","Tarih / Saat",115f),("Time","Saat",60f),("Gross","Satış Fiyatı",125f),("Commission","Komisyon",125f),("Net","Net Tutar",125f)}){_salesGrid.Columns.Add(col.Item1,col.Item2);_salesGrid.Columns[col.Item1]!.FillWeight=col.Item3;_salesGrid.Columns[col.Item1]!.MinimumWidth=90;}
        _salesGrid.Columns["Server"]!.Visible=false;_salesGrid.Columns["Time"]!.Visible=false;_salesGrid.Columns["Item"]!.MinimumWidth=220;
        foreach(var name in new[]{"Gross","Commission","Net"}){_salesGrid.Columns[name]!.DefaultCellStyle.Alignment=DataGridViewContentAlignment.MiddleCenter;_salesGrid.Columns[name]!.HeaderCell.Style.Alignment=DataGridViewContentAlignment.MiddleCenter;}
        _salesGrid.CellPainting+=(_,e)=>{
            if(e.RowIndex<0||e.ColumnIndex<0)return;var name=_salesGrid.Columns[e.ColumnIndex].Name;if(name!="Date"&&name!="Item")return;
            e.PaintBackground(e.ClipBounds,true);var row=_salesGrid.Rows[e.RowIndex];var first=Convert.ToString(e.FormattedValue)??"";var second=Convert.ToString(row.Cells[name=="Date"?"Time":"Server"].Value)??"";
            var bounds=e.CellBounds;bounds.Inflate(-10,-6);var bold=ReferenceUi.Bold(9.5f);TextRenderer.DrawText(e.Graphics,first,name=="Item"?bold:ReferenceUi.Font(9.5f),new Rectangle(bounds.X,bounds.Y+6,bounds.Width,bounds.Height/2),ReferenceUi.Text,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine);TextRenderer.DrawText(e.Graphics,second,ReferenceUi.Font(8.5f),new Rectangle(bounds.X,bounds.Y+bounds.Height/2,bounds.Width,bounds.Height/2),ReferenceUi.Muted,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine);using var line=new Pen(ReferenceUi.Line);e.Graphics.DrawLine(line,e.CellBounds.Left,e.CellBounds.Bottom-1,e.CellBounds.Right,e.CellBounds.Bottom-1);e.Handled=true;
        };
        var table=new ReferencePanel {Dock=DockStyle.Fill,Padding=new Padding(1)};
        var cardHeader=PageLayout.CardHeader(this,"Satış detayları",search,260);
        var totals=new ReferenceSalesFooter(_salesGrid,_salesGrossLabel,_salesCommissionLabel,_salesNetLabel,_salesTotals) {Dock=DockStyle.Bottom,Height=LogicalToDeviceUnits(44)};
        table.Controls.Add(_salesGrid);table.Controls.Add(totals);table.Controls.Add(cardHeader);
        var note=PageLayout.Note(this,_salesStatusLabel);
        _salesPanel.Controls.Add(table);_salesPanel.Controls.Add(note);_salesPanel.Controls.Add(PageLayout.Spacer(this,16));_salesPanel.Controls.Add(kpi);_salesPanel.Controls.Add(PageLayout.Spacer(this,18));_salesPanel.Controls.Add(header);
    }

    private void SetSalesPeriod(bool allTime)
    {
        _salesAllTime=allTime;
        RenderSoldSalesPanel();
    }

    private void RenderSoldSalesPanel()
    {
        var selected = _salesDate.Value.Date;
        _salesPeriodSync=true;if(_salesPeriodModel.Items.Count==3){var today=selected==TurkeyToday;_salesPeriodModel.Items[2]=_salesAllTime||today?"Tarih seç":selected.ToString("dd MMM yyyy",CultureInfo.GetCultureInfo("tr-TR"));_salesPeriodModel.SelectedIndex=_salesAllTime?0:today?1:2;}_salesPeriodSync=false;
        if(_salesAllTimeButton is not null){_salesAllTimeButton.GoldOutline=_salesAllTime;_salesAllTimeButton.Selected=_salesAllTime;_salesAllTimeButton.Invalidate();}
        if(_salesPeriodLabel is not null)_salesPeriodLabel.Text=_salesAllTime?"Kayıtlı tüm satışlar · sunucu ve arama filtreleri uygulanır":selected.ToString("dd MMMM yyyy",CultureInfo.GetCultureInfo("tr-TR"))+" · seçili günün satışları ve net kazancı";
        var chosenServer=_salesServerFilter.SelectedItem?.ToString();
        var names=_salesHistory.Values.Select(x=>x.Server).Distinct().OrderBy(x=>x).ToArray();
        if(!names.SequenceEqual(_salesServerFilter.Items.Cast<string>().Skip(1))) {
            _salesFiltering=true;_salesServerFilter.Items.Clear();_salesServerFilter.Items.Add("Tüm sunucular");foreach(var name in names)_salesServerFilter.Items.Add(name);
            _salesServerFilter.SelectedItem=chosenServer;if(_salesServerFilter.SelectedIndex<0)_salesServerFilter.SelectedIndex=0;_salesFiltering=false;
        }
        var query=_salesSearch.Text.Trim();
        var sales = _salesHistory.Values.Where(s => (_salesAllTime || s.Timestamp.Date == selected) && (chosenServer is null or "Tüm sunucular" || s.Server==chosenServer) && (query.Length==0 || s.ItemTitle.Contains(query,StringComparison.CurrentCultureIgnoreCase))).OrderByDescending(s => s.Timestamp).ToList();
        var gross = sales.Sum(s => s.GrossAmount);
        var net = sales.Sum(s => s.NetAmount);
        _salesGrossLabel.Text = FormatTl(gross);
        _salesCommissionLabel.Text = FormatTl(gross - net);
        _salesNetLabel.Text = FormatTl(net);
        _salesCountLabel.Text = sales.Count.ToString();
        _salesTotals.Text = $"TOPLAM   ·   {sales.Count} satış\nSatış: {FormatTl(gross)}   ·   Komisyon: {FormatTl(gross-net)}   ·   Net: {FormatTl(net)}";
        foreach(DataGridViewRow old in _salesGrid.Rows) (old.Cells["Cover"].Value as Image)?.Dispose();
        _salesGrid.Rows.Clear();
        foreach (var sale in sales)
        {
            int row = _salesGrid.Rows.Add(null, sale.ItemTitle, sale.Server, sale.Timestamp.ToString("dd.MM.yyyy"), sale.Timestamp.ToString("HH:mm"), FormatTl(sale.GrossAmount), FormatTl(sale.CommissionAmount), FormatTl(sale.NetAmount));
            _salesGrid.Rows[row].Height = LogicalToDeviceUnits(56);
            if(row<30 && !_soldScanBusy) _=LoadSaleThumbnailAsync(_salesGrid.Rows[row],sale.ImageUrl);
            _salesGrid.Rows[row].Cells["Commission"].Style.ForeColor = ReferenceUi.Muted;
            _salesGrid.Rows[row].Cells["Gross"].Style.ForeColor = ReferenceUi.Text;
            _salesGrid.Rows[row].Cells["Net"].Style.ForeColor = ReferenceUi.Gold;
            _salesGrid.Rows[row].Cells["Net"].Style.SelectionForeColor=ReferenceUi.Gold;
            _salesGrid.Rows[row].Cells["Net"].Style.Font=ReferenceUi.Bold(9.5f);
        }
    }

    private async Task LoadSaleThumbnailAsync(DataGridViewRow row,string address)
    {
        var bitmap=await ListingThumbnails.LoadAsync(address,()=>!IsDisposed&&row.DataGridView==_salesGrid);
        if(bitmap is null)return;if(IsDisposed||row.DataGridView!=_salesGrid){bitmap.Dispose();return;}row.Cells["Cover"].Value=bitmap;
    }

    private async Task EnsureSoldScanViewAsync()
    {
        if (_soldScanView?.CoreWebView2 is not null) return;
        if (_browserEnvironment is null) throw new InvalidOperationException("Itemci oturumu henüz hazır değil.");
        _soldScanView = new WebView2 { Size = new Size(1280, 900), Visible = false };
        Controls.Add(_soldScanView);
        await _soldScanView.EnsureCoreWebView2Async(_browserEnvironment);
        _soldScanView.CoreWebView2.Settings.AreDevToolsEnabled = false;
        _soldScanView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
        _soldScanView.CoreWebView2.NewWindowRequested += (_, e) => e.Handled = true;
        ConfigureAdBlocking(_soldScanView);

    }

    private async Task NavigateSoldPageAsync(int page)
    {
        var core = _soldScanView!.CoreWebView2;
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Completed(object? sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            if (args.IsSuccess) completion.TrySetResult(true);
            else completion.TrySetException(new IOException("Satılan ilan sayfası yüklenemedi: " + args.WebErrorStatus));
        }
        core.NavigationCompleted += Completed;
        try
        {
            core.Navigate($"https://itemci.com/panel/ilanlarim?page={page}&status=4");
            await completion.Task.WaitAsync(TimeSpan.FromSeconds(30));
            if (!Uri.TryCreate(core.Source, UriKind.Absolute, out var uri) || uri.Host != "itemci.com" || !uri.AbsolutePath.Equals("/panel/ilanlarim", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Itemci hesabına uygulamanın tarayıcısından giriş yapıp satışları tekrar yenile.");
        }
        finally { core.NavigationCompleted -= Completed; }
    }

    private async Task<SoldScanResult> ReadSoldPageAsync()
    {
        var core = _soldScanView!.CoreWebView2;
        var script = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "SoldListings.js"));
        await core.ExecuteScriptAsync(script);
        var token = Guid.NewGuid().ToString("N");
        var completion = new TaskCompletionSource<SoldScanResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Received(object? sender, CoreWebView2WebMessageReceivedEventArgs args)
        {
            try
            {
                using var json = JsonDocument.Parse(args.WebMessageAsJson);
                var root = json.RootElement;
                if (!root.TryGetProperty("token", out var id) || id.GetString() != token) return;
                if (root.TryGetProperty("error", out var error)) { completion.TrySetException(new InvalidOperationException(error.GetString())); return; }
                completion.TrySetResult(root.GetProperty("result").Deserialize<SoldScanResult>() ?? throw new InvalidDataException("Satış verisi boş."));
            }
            catch (Exception ex) { completion.TrySetException(ex); }
        }
        core.WebMessageReceived += Received;
        try
        {
            var known = JsonSerializer.Serialize(_salesHistory.Values.Where(s => s.IsDelivered && !string.IsNullOrWhiteSpace(s.ListingId)).Select(s => s.ListingId).Distinct().ToArray());
            var slice=JsonSerializer.Serialize(new {skipIds=_salesPageProcessed.ToArray(),maxDetails=3,maxMillis=20000});
            await core.ExecuteScriptAsync($"window.__niyaziScanSoldListings({known},3000,{slice}).then(result => window.chrome.webview.postMessage({{token:'{token}',result}})).catch(e => window.chrome.webview.postMessage({{token:'{token}',error:String(e.message || e)}}));");
            return await completion.Task.WaitAsync(TimeSpan.FromMinutes(3));
        }
        finally { core.WebMessageReceived -= Received; }
    }

    private async Task RefreshSoldListingsAsync(bool showErrors, bool fullHistory = false)
    {
        if(SiteAutomationBlocked){if(showErrors)_salesStatusLabel.Text=SitePauseMessage;return;}
        if(IsDisposed||Disposing)return;
        if(_soldScanBusy){if(showErrors)_salesStatusLabel.Text="Satış taraması zaten devam ediyor; kayıtlar geldikçe tablo güncellenir.";return;}
        if(_liveBusy||_autoBumpBusy||_composerBusy||_composerPopup is not null||(!showErrors&&AutoBumpDue))
        {
            if(showErrors)_salesStatusLabel.Text="Başka bir işlem devam ediyor. İşlem bittikten sonra satışları yenileyebilirsin.";
            return;
        }
        if (!_webReady) { _salesStatusLabel.Text = "Itemci oturumu henüz hazır değil."; return; }
        if(!fullHistory&&DateTime.UtcNow<_salesNextRefreshUtc)
        {
            if(showErrors){RenderSoldSalesPanel();_salesStatusLabel.Text=$"Kayıtlı satışlar gösteriliyor · Yeni tarama için {Math.Max(1,(int)Math.Ceiling((_salesNextRefreshUtc-DateTime.UtcNow).TotalSeconds))} saniye bekle.";}
            return;
        }
        if(fullHistory)ResetSalesScanCursor();
        _salesNextRefreshUtc=DateTime.UtcNow.AddSeconds(45);
        _soldScanBusy = true;
        try
        {
            if (!_soldCacheLoaded)
            {
                _salesHistory.Clear();
                _historyFullyScanned = false;
                LoadSalesHistory();
                _soldCacheLoaded = true;
            }
            RenderSoldSalesPanel();
            await EnsureSoldScanViewAsync();
            // One resumable slice per turn: at most one page and three details.
            // Continue through old pages too; listing order is not sale order.
            int max = _salesResumePage;
            int pages = 0, failed = 0;
            bool reachedEnd = false, interrupted = false;
            string firstError = "";
            for (int page = _salesResumePage; page <= max; page++)
            {
                if(SiteAutomationBlocked)throw new IOException(SitePauseMessage);
                await Task.Delay(3000);
                if(_composerPopup is not null||_composerBusy){interrupted=true;break;}
                if(SiteAutomationBlocked)throw new IOException(SitePauseMessage);
                _salesStatusLabel.Text = $"Satılan ilanlar okunuyor · Sayfa {page} · Tüm sunucular";
                _salesStatusLabel.ForeColor = TextMuted;
                await NavigateSoldPageAsync(page);
                var result = await ReadSoldPageAsync();
                var fingerprint = string.Join("|", result.ListingIds);
                if (result.Empty || (_salesPreviousPage.Length>0&&fingerprint==_salesPreviousPage)) { reachedEnd = true; break; }
                pages++;
                failed += result.Errors.Count;
                if (firstError.Length == 0 && result.Errors.Count > 0) firstError = result.Errors[0];
                foreach (var data in result.Sales)
                {
                    if (!DateTime.TryParseExact($"{data.Date} {data.Time}", "dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var timestamp) ||
                        !decimal.TryParse(data.Price, NumberStyles.Number, CultureInfo.GetCultureInfo("tr-TR"), out var gross) || gross <= 0 || string.IsNullOrWhiteSpace(data.OrderId))
                    { failed++; continue; }
                    // Replace by order ID. Refreshes cannot double-count a sale.
                    _salesHistory.TryGetValue(data.OrderId,out var previous);
                    if(data.Server=="Bilinmeyen sunucu" && !string.IsNullOrWhiteSpace(previous?.Server)) data.Server=previous.Server;
                    if(string.IsNullOrWhiteSpace(data.ImageUrl))data.ImageUrl=previous?.ImageUrl??"";
                    _salesHistory[data.OrderId] = new SaleRecord { OrderId = data.OrderId, ListingId = data.ListingId, ItemTitle = data.ItemTitle, Server = data.Server, ImageUrl = data.ImageUrl, Timestamp = timestamp, GrossAmount = gross, IsDelivered = data.IsDelivered };
                }
                SaveSalesHistory();
                AdvanceSalesScanCursor(result);
                RenderSoldSalesPanel();
            }
            _salesCycleHadErrors |= failed>0;
            if (reachedEnd && !_salesCycleHadErrors) _historyFullyScanned = true;
            bool capped=_salesResumePage>150;
            if(reachedEnd||capped){ResetSalesScanCursor();_salesNextRefreshUtc=DateTime.UtcNow.AddMinutes(3);}
            else _salesNextRefreshUtc=DateTime.UtcNow.AddSeconds(45);
            SaveSalesHistory();
            RenderSoldSalesPanel();
            _salesStatusLabel.Text = $"Tüm sunucular · {pages} sayfa · {_salesHistory.Count} kayıt · Tüm Zamanlar ile bütün kayıtların toplamını görebilirsin.";
            _salesStatusLabel.ForeColor = Green;
            if(interrupted)
            {
                _salesNextRefreshUtc=DateTime.UtcNow.AddSeconds(30);
                _salesStatusLabel.Text="İlan penceresi açıldığı için tarama duraklatıldı. Okunan satışlar kaydedildi; pencereyi kapattıktan sonra yeniden tarayabilirsin.";
                _salesStatusLabel.ForeColor=Color.Orange;
            }
            else if (failed > 0)
            {
                _salesStatusLabel.Text = $"{failed} ayrıntı okunamadı; toplam eksik olabilir. {firstError}";
                _salesStatusLabel.ForeColor = Color.Orange;
            }
            else if (capped)
            {
                _salesStatusLabel.Text = "150 sayfalık sınır doldu; geçmişin tamamı henüz okunmamış olabilir.";
                _salesStatusLabel.ForeColor = Color.Orange;
            }
            else if(!reachedEnd)
                _salesStatusLabel.Text=$"{_salesHistory.Count} kayıt kaydedildi · Sıradaki bölüm: sayfa {_salesResumePage} · Sunucu takibine sıra verildi; tarama otomatik devam edecek.";
            HandleNewSalesNotifications(_salesHistory.Values.Where(s => s.Timestamp.Date == TurkeyToday).ToList());
            _salesInitialized = true;
        }
        catch (Exception ex)
        {
            _salesNextRefreshUtc=DateTime.UtcNow.AddSeconds(30);
            _salesStatusLabel.Text = "Satışlar okunamadı; kayıtlı toplam eksik olabilir. " + ex.Message;
            _salesStatusLabel.ForeColor = Color.IndianRed;
            if (_soldScanView is not null) { Controls.Remove(_soldScanView); _soldScanView.Dispose(); _soldScanView = null; }
            if (showErrors) MessageBox.Show(ex.Message, "Satış takibi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally { if(_soldScanView is not null){Controls.Remove(_soldScanView);_soldScanView.Dispose();_soldScanView=null;}_soldScanBusy = false; if(_salesPanel.Visible&&!IsDisposed)RenderSoldSalesPanel();
            if(!IsDisposed&&!Disposing)await ScanAllServerSalesAsync(); }
    }

    private sealed class SoldScanResult
    {
        public List<SoldDetail> Sales { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public List<string> ListingIds { get; set; } = new();
        public bool Empty { get; set; }
        public List<string> ProcessedIds { get; set; } = new();
        public bool PageComplete { get; set; }
    }
    private sealed class SoldDetail
    {
        public string OrderId { get; set; } = "";
        public string ListingId { get; set; } = "";
        public string ItemTitle { get; set; } = "";
        public string ImageUrl { get; set; } = "";
        public string Server { get; set; } = "Bilinmeyen sunucu";
        public string Date { get; set; } = "";
        public string Time { get; set; } = "";
        public string Price { get; set; } = "";
        public bool IsDelivered { get; set; }
    }
}
