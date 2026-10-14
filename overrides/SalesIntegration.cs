using System.Globalization;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace NiyazisugarItemci;

public sealed partial class MainForm
{
    private readonly DateTimePicker _salesDate = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "dd.MM.yyyy", Width = 145 };
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
        _salesPanel.Dock = DockStyle.Fill;
        _salesPanel.BackColor = Bg;
        _salesPanel.Padding = new Padding(24);
        var heading = new Label { Text = "Satışlar / Günlük Net Kazanç", Dock = DockStyle.Top, Height = 55, Font = new Font("Segoe UI", 20, FontStyle.Bold), ForeColor = TextMain };
        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 55, WrapContents = false };
        actions.Controls.Add(new Label { Text = "GÜN", AutoSize = true, Margin = new Padding(0, 12, 10, 0), ForeColor = TextMuted });
        _salesDate.Value = TurkeyToday;
        _salesDate.Margin = new Padding(0, 8, 12, 0);
        _salesDate.ValueChanged += (_, _) => RenderSoldSalesPanel();
        actions.Controls.Add(_salesDate);
        var today = ActionButton("BUGÜN", () => _salesDate.Value = TurkeyToday);
        today.Width = 85;
        actions.Controls.Add(today);
        var refresh = ActionButton("YENİLE", () => _ = RefreshSoldListingsAsync(true));
        refresh.Width = 110;
        actions.Controls.Add(refresh);
        var history = ActionButton("TÜM GEÇMİŞİ TARA", () => _ = RefreshSoldListingsAsync(true, true));
        history.Width = 165;
        actions.Controls.Add(history);
        var cards = new TableLayoutPanel { Dock = DockStyle.Top, Height = 130, ColumnCount = 4, RowCount = 1 };
        for (int i = 0; i < 4; i++) cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        var metrics = new[] {
            SalesMetricCard("GÜNLÜK SATIŞ TOPLAMI", _salesGrossLabel, "0,00 TL", Accent),
            SalesMetricCard("KOMİSYON (%10)", _salesCommissionLabel, "0,00 TL", Color.FromArgb(255, 110, 110)),
            SalesMetricCard("GÜNLÜK NET (%90)", _salesNetLabel, "0,00 TL", Green),
            SalesMetricCard("SATIŞ ADEDİ", _salesCountLabel, "0", TextMain)
        };
        for (int i = 0; i < 4; i++) { metrics[i].Dock = DockStyle.Fill; metrics[i].Margin = new Padding(0, 5, i < 3 ? 10 : 0, 12); cards.Controls.Add(metrics[i], i, 0); }
        var status = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = Card, Padding = new Padding(14) };
        status.Paint += (_, e) => DrawBorder(e.Graphics, status.ClientRectangle);
        _salesStatusLabel.Dock = DockStyle.Fill;
        _salesStatusLabel.Text = "Tüm sunucular · Satılan ilanların sipariş tarihi esas alınır · Net = satış − %10 komisyon";
        _salesStatusLabel.ForeColor = TextMuted;
        status.Controls.Add(_salesStatusLabel);
        _salesGrid.Dock = DockStyle.Fill;
        _salesGrid.ReadOnly = true;
        _salesGrid.AllowUserToAddRows = false;
        _salesGrid.AllowUserToDeleteRows = false;
        _salesGrid.RowHeadersVisible = false;
        _salesGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _salesGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _salesGrid.DefaultCellStyle.ForeColor = TextMain;
        _salesGrid.DefaultCellStyle.Font = new Font("Segoe UI", 9);
        _salesGrid.ColumnHeadersHeight = 42;
        _salesGrid.Columns.Clear();
        _salesGrid.Columns.Add("Item", "SATILAN ÜRÜN");
        _salesGrid.Columns.Add("Date", "TARİH");
        _salesGrid.Columns.Add("Time", "SAAT");
        _salesGrid.Columns.Add("Gross", "SATIŞ FİYATI");
        _salesGrid.Columns.Add("Net", "NET FİYAT (%10 DÜŞÜLMÜŞ)");
        _salesGrid.Columns["Item"]!.FillWeight = 200;
        _salesGrid.Columns["Date"]!.FillWeight = 80;
        _salesGrid.Columns["Time"]!.FillWeight = 55;
        _salesGrid.Columns["Gross"]!.FillWeight = 100;
        _salesGrid.Columns["Net"]!.FillWeight = 130;
        var table = new Panel { Dock = DockStyle.Fill, BackColor = Card, Padding = new Padding(8), };
        table.Controls.Add(_salesGrid);
        _salesPanel.Controls.Add(table);
        _salesPanel.Controls.Add(status);
        _salesPanel.Controls.Add(cards);
        _salesPanel.Controls.Add(actions);
        _salesPanel.Controls.Add(heading);
    }

    private void RenderSoldSalesPanel()
    {
        var selected = _salesDate.Value.Date;
        var sales = _salesHistory.Values.Where(s => s.Timestamp.Date == selected).OrderByDescending(s => s.Timestamp).ToList();
        var gross = sales.Sum(s => s.GrossAmount);
        var net = sales.Sum(s => s.NetAmount);
        _salesGrossLabel.Text = FormatTl(gross);
        _salesCommissionLabel.Text = FormatTl(gross - net);
        _salesNetLabel.Text = FormatTl(net);
        _salesCountLabel.Text = sales.Count.ToString();
        _salesGrid.Rows.Clear();
        foreach (var sale in sales)
        {
            int row = _salesGrid.Rows.Add(sale.ItemTitle, sale.Timestamp.ToString("dd.MM.yyyy"), sale.Timestamp.ToString("HH:mm"), FormatTl(sale.GrossAmount), FormatTl(sale.NetAmount));
            _salesGrid.Rows[row].Height = 38;
            _salesGrid.Rows[row].Cells["Gross"].Style.ForeColor = Accent;
            _salesGrid.Rows[row].Cells["Net"].Style.ForeColor = Green;
        }
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
        FormClosed += (_, _) => _soldScanView?.Dispose();
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
        if (_soldScanBusy || IsDisposed) return;
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
                    _salesHistory[data.OrderId] = new SaleRecord { OrderId = data.OrderId, ListingId = data.ListingId, ItemTitle = data.ItemTitle, Timestamp = timestamp, GrossAmount = gross, IsDelivered = data.IsDelivered };
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
        finally { _soldScanBusy = false; }
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
        public string Date { get; set; } = "";
        public string Time { get; set; } = "";
        public string Price { get; set; } = "";
        public bool IsDelivered { get; set; }
    }
}
