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
    private WebView2? _soldScanView;
    private bool _soldScanBusy;
    private bool _soldCacheLoaded;
    private static DateTime TurkeyToday
    {
        get
        {
            try { return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time")).Date; }
            catch { return DateTime.Today; }
        }
    }

    private void BuildSoldSalesPanel()
    {
        _salesPanel.Dock=DockStyle.Fill;_salesPanel.BackColor=ReferenceUi.Background;_salesPanel.Padding=new Padding(30,22,30,20);
        var heading=new Panel {Dock=DockStyle.Top,Height=94,BackColor=ReferenceUi.Background};var title=ReferenceUi.Label("Satışlar ve Kazanç",27,true);title.Dock=DockStyle.Top;title.Height=48;var sub=ReferenceUi.Label("Günlük satışlarını ve komisyon sonrası kazancını takip et.",12);sub.Dock=DockStyle.Top;sub.Height=34;sub.ForeColor=ReferenceUi.Muted;heading.Controls.Add(sub);heading.Controls.Add(title);
        var actions=new TableLayoutPanel {Dock=DockStyle.Top,Height=62,ColumnCount=2,RowCount=1};actions.RowStyles.Add(new RowStyle(SizeType.Percent,100));actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,334));
        var filters=new FlowLayoutPanel {Dock=DockStyle.Fill,WrapContents=true,BackColor=ReferenceUi.Background};var commands=new FlowLayoutPanel {Dock=DockStyle.Fill,WrapContents=false,BackColor=ReferenceUi.Background};
        _salesDate.Value=TurkeyToday;_salesDate.ValueChanged+=(_,_)=>RenderSoldSalesPanel();
        var dateChoice=ReferenceUi.Button(_salesDate.Value.ToString("dd MMMM yyyy",CultureInfo.GetCultureInfo("tr-TR")),"calendar",()=>{},225);
        _salesDate.ValueChanged+=(_,_)=>dateChoice.Text=_salesDate.Value.ToString("dd MMMM yyyy",CultureInfo.GetCultureInfo("tr-TR"));
        dateChoice.Click+=(_,_)=>{using var picker=new Form {Text="Satış günü",FormBorderStyle=FormBorderStyle.FixedToolWindow,StartPosition=FormStartPosition.Manual,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,BackColor=Card,Padding=new Padding(8),ShowInTaskbar=false};var calendar=new MonthCalendar {MaxSelectionCount=1,SelectionStart=_salesDate.Value,SelectionEnd=_salesDate.Value};calendar.DateSelected+=(_,e)=>{_salesDate.Value=e.Start;picker.Close();};picker.Controls.Add(calendar);var point=dateChoice.PointToScreen(new Point(0,dateChoice.Height));var area=Screen.FromControl(dateChoice).WorkingArea;picker.Location=new Point(Math.Min(point.X,area.Right-270),Math.Min(point.Y,area.Bottom-240));picker.ShowDialog(this);};
        filters.Controls.Add(dateChoice);filters.Controls.Add(ReferenceUi.Button("Bugün","",()=>_salesDate.Value=TurkeyToday,88));
        _salesServerFilter.Items.Add("Tüm sunucular");_salesServerFilter.SelectedIndex=0;_salesServerFilter.SelectedIndexChanged+=(_,_)=>{if(!_salesFiltering)RenderSoldSalesPanel();};_salesSearch.TextChanged+=(_,_)=>RenderSoldSalesPanel();filters.Controls.Add(ReferenceUi.Select(_salesServerFilter,240));
        commands.Controls.Add(ReferenceUi.Button("Yenile","refresh",()=>_=RefreshSoldListingsAsync(true),125));var history=ReferenceUi.Button("Tüm Geçmişi Tara","clock",()=>_=RefreshSoldListingsAsync(true,true),190);history.GoldOutline=true;history.Margin=new Padding(0);commands.Controls.Add(history);actions.Controls.Add(filters,0,0);actions.Controls.Add(commands,1,0);
        actions.SizeChanged+=(_,_)=>actions.Height=actions.Width<1040?112:62;
        var cards=new TableLayoutPanel {Dock=DockStyle.Top,Height=120,ColumnCount=4,RowCount=1,Padding=new Padding(0,0,0,14)};cards.RowStyles.Add(new RowStyle(SizeType.Percent,100));for(int i=0;i<4;i++)cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));
        var metrics=new[]{ReferenceUi.Metric("Satış Toplamı",_salesGrossLabel,"coins",ReferenceUi.Gold,true),ReferenceUi.Metric("Komisyon (%10)",_salesCommissionLabel,"percent",ReferenceUi.Coral,true),ReferenceUi.Metric("Komisyon Sonrası Net",_salesNetLabel,"wallet",ReferenceUi.Aqua,true),ReferenceUi.Metric("Satış Adedi",_salesCountLabel,"cart",ReferenceUi.Gold,true)};
        foreach(var label in new[]{_salesGrossLabel,_salesCommissionLabel,_salesNetLabel})label.Text="0,00 TL";_salesCountLabel.Text="0";for(int i=0;i<4;i++){cards.Controls.Add(metrics[i],i,0);if(i==3)metrics[i].Margin=new Padding(0);}
        var statusArea=new Panel {Dock=DockStyle.Top,Height=60,BackColor=ReferenceUi.Background,Padding=new Padding(0,0,0,14)};var status=new ReferencePanel {Dock=DockStyle.Fill,Padding=new Padding(18,0,18,0)};_salesStatusLabel.Dock=DockStyle.Fill;_salesStatusLabel.Font=ReferenceUi.Font(10);_salesStatusLabel.ForeColor=ReferenceUi.Muted;_salesStatusLabel.BackColor=ReferenceUi.Surface;_salesStatusLabel.TextAlign=ContentAlignment.MiddleLeft;_salesStatusLabel.Text="Satış geçmişi yüklenmeyi bekliyor · Tüm sunucular";status.Controls.Add(_salesStatusLabel);statusArea.Controls.Add(status);
        _salesGrid.Dock=DockStyle.Fill;_salesGrid.Tag="reference";_salesGrid.ReadOnly=true;_salesGrid.AllowUserToAddRows=false;_salesGrid.AllowUserToDeleteRows=false;_salesGrid.AllowUserToResizeRows=false;_salesGrid.RowHeadersVisible=false;_salesGrid.BorderStyle=BorderStyle.None;_salesGrid.BackgroundColor=ReferenceUi.Surface;_salesGrid.GridColor=ReferenceUi.Line;_salesGrid.CellBorderStyle=DataGridViewCellBorderStyle.SingleHorizontal;_salesGrid.ColumnHeadersBorderStyle=DataGridViewHeaderBorderStyle.None;_salesGrid.EnableHeadersVisualStyles=false;_salesGrid.ColumnHeadersDefaultCellStyle=new DataGridViewCellStyle {BackColor=ReferenceUi.Field,ForeColor=ReferenceUi.Muted,Font=ReferenceUi.Font(10),Padding=new Padding(8),Alignment=DataGridViewContentAlignment.MiddleLeft};_salesGrid.DefaultCellStyle=new DataGridViewCellStyle {BackColor=ReferenceUi.Surface,ForeColor=ReferenceUi.Text,Font=ReferenceUi.Font(11),SelectionBackColor=Color.FromArgb(29,38,44),SelectionForeColor=ReferenceUi.Text,Padding=new Padding(10,0,10,0)};_salesGrid.AlternatingRowsDefaultCellStyle.BackColor=ReferenceUi.Surface;_salesGrid.ColumnHeadersHeight=42;_salesGrid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;_salesGrid.SelectionMode=DataGridViewSelectionMode.FullRowSelect;_salesGrid.Columns.Clear();
        _salesGrid.Columns.Add(new DataGridViewImageColumn {Name="Cover",HeaderText="",ImageLayout=DataGridViewImageCellLayout.Zoom,DefaultCellStyle=new DataGridViewCellStyle {NullValue=null,Padding=new Padding(14,8,4,8)},Width=90,AutoSizeMode=DataGridViewAutoSizeColumnMode.None});
        foreach(var col in new[]{("Item","Satılan Ürün",210f),("Server","Sunucu",100f),("Date","Tarih / Saat",115f),("Time","Saat",60f),("Gross","Satış Fiyatı",125f),("Commission","Komisyon",125f),("Net","Net Tutar",125f)}){_salesGrid.Columns.Add(col.Item1,col.Item2);_salesGrid.Columns[col.Item1]!.FillWeight=col.Item3;_salesGrid.Columns[col.Item1]!.MinimumWidth=90;}
        _salesGrid.Columns["Server"]!.Visible=false;_salesGrid.Columns["Time"]!.Visible=false;_salesGrid.Columns["Item"]!.MinimumWidth=220;
        foreach(var name in new[]{"Gross","Commission","Net"}){_salesGrid.Columns[name]!.DefaultCellStyle.Alignment=DataGridViewContentAlignment.MiddleCenter;_salesGrid.Columns[name]!.HeaderCell.Style.Alignment=DataGridViewContentAlignment.MiddleCenter;}
        _salesGrid.CellPainting+=(_,e)=>{
            if(e.RowIndex<0||e.ColumnIndex<0)return;var name=_salesGrid.Columns[e.ColumnIndex].Name;if(name!="Date"&&name!="Item")return;
            e.PaintBackground(e.ClipBounds,true);var row=_salesGrid.Rows[e.RowIndex];var first=Convert.ToString(e.FormattedValue)??"";var second=Convert.ToString(row.Cells[name=="Date"?"Time":"Server"].Value)??"";
            var bounds=e.CellBounds;bounds.Inflate(-10,-8);var bold=ReferenceUi.Bold(11);TextRenderer.DrawText(e.Graphics,first,name=="Item"?bold:ReferenceUi.Font(10),new Rectangle(bounds.X,bounds.Y+6,bounds.Width,bounds.Height/2),ReferenceUi.Text,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine);TextRenderer.DrawText(e.Graphics,second,ReferenceUi.Font(9),new Rectangle(bounds.X,bounds.Y+bounds.Height/2,bounds.Width,bounds.Height/2),ReferenceUi.Muted,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine);using var line=new Pen(ReferenceUi.Line);e.Graphics.DrawLine(line,e.CellBounds.Left,e.CellBounds.Bottom-1,e.CellBounds.Right,e.CellBounds.Bottom-1);e.Handled=true;
        };
        var table=new ReferencePanel {Dock=DockStyle.Fill,Padding=new Padding(1)};
        var tableTitle=new TableLayoutPanel {Dock=DockStyle.Top,Height=64,ColumnCount=2,RowCount=1,Padding=new Padding(20,9,14,9),BackColor=ReferenceUi.Surface};tableTitle.RowStyles.Add(new RowStyle(SizeType.Percent,100));tableTitle.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));tableTitle.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,290));var caption=ReferenceUi.Label("Satış Detayları",17,true);caption.Dock=DockStyle.Fill;caption.TextAlign=ContentAlignment.MiddleLeft;tableTitle.Controls.Add(caption,0,0);var search=ReferenceUi.Search(_salesSearch,270);search.Dock=DockStyle.Fill;search.Margin=new Padding(0);tableTitle.Controls.Add(search,1,0);
        var totals=new ReferenceSalesFooter(_salesGrid,_salesGrossLabel,_salesCommissionLabel,_salesNetLabel,_salesTotals) {Dock=DockStyle.Bottom,Height=54};var note=ReferenceUi.Label("ⓘ  Net tutar, satış fiyatından %10 komisyon düşülerek hesaplanır.",9);note.Dock=DockStyle.Bottom;note.Height=34;note.Padding=new Padding(18,0,0,0);note.TextAlign=ContentAlignment.MiddleLeft;note.ForeColor=ReferenceUi.Muted;
        table.Controls.Add(_salesGrid);table.Controls.Add(totals);table.Controls.Add(note);table.Controls.Add(tableTitle);_salesPanel.Controls.Add(table);_salesPanel.Controls.Add(statusArea);_salesPanel.Controls.Add(cards);_salesPanel.Controls.Add(actions);_salesPanel.Controls.Add(heading);
    }

    private void RenderSoldSalesPanel()
    {
        var selected = _salesDate.Value.Date;
        var chosenServer=_salesServerFilter.SelectedItem?.ToString();
        var names=_salesHistory.Values.Select(x=>x.Server).Distinct().OrderBy(x=>x).ToArray();
        if(!names.SequenceEqual(_salesServerFilter.Items.Cast<string>().Skip(1))) {
            _salesFiltering=true;_salesServerFilter.Items.Clear();_salesServerFilter.Items.Add("Tüm sunucular");foreach(var name in names)_salesServerFilter.Items.Add(name);
            _salesServerFilter.SelectedItem=chosenServer;if(_salesServerFilter.SelectedIndex<0)_salesServerFilter.SelectedIndex=0;_salesFiltering=false;
        }
        var query=_salesSearch.Text.Trim();
        var sales = _salesHistory.Values.Where(s => s.Timestamp.Date == selected && (chosenServer is null or "Tüm sunucular" || s.Server==chosenServer) && (query.Length==0 || s.ItemTitle.Contains(query,StringComparison.CurrentCultureIgnoreCase))).OrderByDescending(s => s.Timestamp).ToList();
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
            _salesGrid.Rows[row].Height = 74;
            if(row<30 && !_soldScanBusy) _=LoadSaleThumbnailAsync(_salesGrid.Rows[row],sale.ImageUrl);
            _salesGrid.Rows[row].Cells["Commission"].Style.ForeColor = ReferenceUi.Text;
            _salesGrid.Rows[row].Cells["Gross"].Style.ForeColor = ReferenceUi.Text;
            _salesGrid.Rows[row].Cells["Net"].Style.ForeColor = ReferenceUi.Aqua;
            _salesGrid.Rows[row].Cells["Net"].Style.SelectionForeColor=ReferenceUi.Aqua;
            _salesGrid.Rows[row].Cells["Net"].Style.Font=ReferenceUi.Bold(12);
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
            await core.ExecuteScriptAsync($"window.__niyaziScanSoldListings({known}).then(result => window.chrome.webview.postMessage({{token:'{token}',result}})).catch(e => window.chrome.webview.postMessage({{token:'{token}',error:String(e.message || e)}}));");
            return await completion.Task.WaitAsync(TimeSpan.FromMinutes(3));
        }
        finally { core.WebMessageReceived -= Received; }
    }

    private async Task RefreshSoldListingsAsync(bool showErrors, bool fullHistory = false)
    {
        if (_soldScanBusy || (!showErrors&&(_autoBumpBusy||AutoBumpDue)) || IsDisposed) return;
        if (!_webReady) { _salesStatusLabel.Text = "Itemci oturumu henüz hazır değil."; return; }
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
            var full = fullHistory || !_historyFullyScanned;
            // Listings may be ordered by creation date rather than sale date.
            // Check every page, skipping details of already completed orders.
            int max = 150;
            int pages = 0, failed = 0;
            bool reachedEnd = false;
            string firstError = "";
            var fingerprints = new HashSet<string>();
            for (int page = 1; page <= max; page++)
            {
                _salesStatusLabel.Text = $"Satılan ilanlar okunuyor · Sayfa {page} · Tüm sunucular";
                _salesStatusLabel.ForeColor = TextMuted;
                await NavigateSoldPageAsync(page);
                var result = await ReadSoldPageAsync();
                var fingerprint = string.Join("|", result.ListingIds);
                if (result.Empty || !fingerprints.Add(fingerprint)) { reachedEnd = true; break; }
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
                RenderSoldSalesPanel();
            }
            if (full && reachedEnd && failed == 0) _historyFullyScanned = true;
            SaveSalesHistory();
            RenderSoldSalesPanel();
            _salesStatusLabel.Text = $"Tüm sunucular · {pages} sayfa · {_salesHistory.Count} kayıt · Gün seçerek toplamları görebilirsin.";
            _salesStatusLabel.ForeColor = Green;
            if (failed > 0)
            {
                _salesStatusLabel.Text = $"{failed} ayrıntı okunamadı; toplam eksik olabilir. {firstError}";
                _salesStatusLabel.ForeColor = Color.Orange;
            }
            else if (full && !reachedEnd)
            {
                _salesStatusLabel.Text = "150 sayfalık sınır doldu; geçmişin tamamı henüz okunmamış olabilir.";
                _salesStatusLabel.ForeColor = Color.Orange;
            }
            HandleNewSalesNotifications(_salesHistory.Values.Where(s => s.Timestamp.Date == TurkeyToday).ToList());
            _salesInitialized = true;
        }
        catch (Exception ex)
        {
            _salesStatusLabel.Text = "Satışlar okunamadı; kayıtlı toplam eksik olabilir. " + ex.Message;
            _salesStatusLabel.ForeColor = Color.IndianRed;
            if (_soldScanView is not null) { Controls.Remove(_soldScanView); _soldScanView.Dispose(); _soldScanView = null; }
            if (showErrors) MessageBox.Show(ex.Message, "Satış takibi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally { if(_soldScanView is not null){Controls.Remove(_soldScanView);_soldScanView.Dispose();_soldScanView=null;}_soldScanBusy = false; if(_salesPanel.Visible&&!IsDisposed)RenderSoldSalesPanel(); }
    }

    private sealed class SoldScanResult
    {
        public List<SoldDetail> Sales { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public List<string> ListingIds { get; set; } = new();
        public bool Empty { get; set; }
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
