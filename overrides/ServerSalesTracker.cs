using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace NiyazisugarItemci;
public sealed partial class MainForm
{
    private readonly Dictionary<string, ServerWatch> _serverWatch = new();
    private string WatchPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NiyazisugarItemci", "server-sales-watch.json");
    private void LoadServerSalesWatch()
    {
        try
        {
            if (!File.Exists(WatchPath)) return;
            var saved = JsonSerializer.Deserialize<Dictionary<string, ServerWatch>>(File.ReadAllText(WatchPath));
            foreach (var entry in saved ?? new()) _serverWatch[entry.Key] = entry.Value;
        }
        catch { _liveStatus.Text = "Kayıtlar okunamadı; takip yeniden başlayacak."; }
    }
    private void SaveServerSalesWatch()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(WatchPath)!);
        var temporary = WatchPath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(_serverWatch));
        File.Move(temporary, WatchPath, true);
    }
    private void RenderServerSalesWatch()
    {
        if (_liveMarket is null || _liveServer.SelectedItem is not MarketServerInfo server) return;
        foreach (Control old in _liveRows.Controls.Cast<Control>().ToArray()) old.Dispose();
        _serverWatch.TryGetValue(server.Id, out var state);
        var rows = state?.sales.Where(s => s.detectedAt.Date == TurkeyToday).OrderByDescending(s => s.detectedAt).ToList() ?? new();
        if (rows.Count == 0)
        {
            _liveRows.Controls.Add(new Label { Width = _liveMarket.Width - 28, Height = 112, ForeColor = TextMuted, Font = new Font("Segoe UI",8.5F), Text = "Henüz doğrulanmış satış yok.\n\nBu panel takip sırasında satış işareti bulunan ilanları kaydeder. Önceki satışların tamamına erişim yok." });
        }
        foreach (var sale in rows)
        {
            var row = sale.listing;
            var card = new Panel { Width = _liveMarket.Width - 26, Height = 65, BackColor = Color.FromArgb(21,25,33), Margin = new Padding(0,0,0,5), Cursor = Cursors.Hand };
            var name = new Label { Dock = DockStyle.Top, Height = 22, Text = row.title, AutoEllipsis = true, ForeColor = Color.FromArgb(151,190,255), Font = new Font("Segoe UI",8.5F,FontStyle.Bold), Padding = new Padding(4,2,0,0) };
            var seller = new Label { Dock = DockStyle.Top, Height = 20, Text = "Satıcı: " + row.seller, AutoEllipsis = true, ForeColor = Color.FromArgb(190,151,255), Font = new Font("Segoe UI",8), Padding = new Padding(4,0,0,0) };
            var info = new Label { Dock = DockStyle.Fill, Text = $"{row.price:N2} TL · Tespit {sale.detectedAt:HH:mm:ss}", ForeColor = Color.FromArgb(67,222,169), Font = new Font("Segoe UI",8,FontStyle.Bold), Padding = new Padding(4,0,0,0) };
            card.Controls.Add(info);card.Controls.Add(seller);card.Controls.Add(name);info.BringToFront();
            foreach (Control target in new Control[] {card,name,seller,info})
            {
                _sidebarToolTip.SetToolTip(target, $"{row.title}\nSatıcı: {row.seller}\nSon görülen ilan fiyatı: {row.price:N2} TL\nSatıldı işareti tespit edildi: {sale.detectedAt:dd.MM.yyyy HH:mm:ss}\nKesin işlem fiyatı ve satış saati site tarafından doğrulanmadı.");
                target.Click += (_, _) => _ = OpenOrActivateBrowserTabAsync(row.url,row.title);
            }
            _liveRows.Controls.Add(card);
        }
        _liveStatus.Text = $"Bugün tespit: {rows.Count} · {state?.status ?? "Takip hazırlanıyor"}";
        PositionLiveMarket();
    }
    private async Task<WatchScan> ReadServerWatchAsync(MarketServerInfo server, ServerWatch state)
    {
        var core = _marketWebView.CoreWebView2;
        var token = Guid.NewGuid().ToString("N");
        var task = new TaskCompletionSource<WatchScan>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Message(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                using var json = JsonDocument.Parse(e.WebMessageAsJson);
                var root = json.RootElement;
                if (!root.TryGetProperty("watchToken",out var t) || t.GetString() != token) return;
                if (root.TryGetProperty("error",out var error)) task.TrySetException(new IOException(error.GetString()));
                else task.TrySetResult(root.GetProperty("result").Deserialize<WatchScan>() ?? new());
            }
            catch (Exception ex) { task.TrySetException(ex); }
        }
        core.WebMessageReceived += Message;
        try
        {
            var known = state.known.Values.Where(row => !state.sales.Any(s => s.listing.url == row.url)).ToList();
            await core.ExecuteScriptAsync($"window.__NiyazisugarServerSalesScan({JsonSerializer.Serialize(server.Id)},{JsonSerializer.Serialize(known)},{state.cursor}).then(result=>chrome.webview.postMessage({{watchToken:'{token}',result}})).catch(e=>chrome.webview.postMessage({{watchToken:'{token}',error:String(e.message||e)}}));");
            return await task.Task.WaitAsync(TimeSpan.FromMinutes(3));
        }
        finally { core.WebMessageReceived -= Message; }
    }
    private async Task ScanAllServerSalesAsync()
    {
        if (_liveBusy || !_marketWebReady || _marketWebView.CoreWebView2 is null || IsDisposed) return;
        _liveBusy = true;
        try
        {
            for (int attempt=0; !_marketAnalyzerInjected && attempt<100; attempt++) await Task.Delay(100);
            if (!_marketAnalyzerInjected) return;
            await _marketWebView.CoreWebView2.ExecuteScriptAsync(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"ServerSales.js")));
            foreach (var server in _marketServers.ToArray())
            {
                if (!_serverWatch.TryGetValue(server.Id,out var state)) _serverWatch[server.Id] = state = new();
                try
                {
                    state.status = "Taranıyor…";
                    RenderServerSalesWatch();
                    var scan = await ReadServerWatchAsync(server,state);
                    foreach (var row in scan.rows) state.known[row.url] = row;
                    foreach (var row in scan.sold)
                    {
                        if (!state.sales.Any(s=>s.listing.url==row.url))
                        {
                            var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time"));
                            state.sales.Add(new WatchSale { listing=row,detectedAt=now });
                        }
                        state.known.Remove(row.url);
                    }
                    state.cursor = scan.nextCursor;
                    state.sales.RemoveAll(s=>s.detectedAt.Date < TurkeyToday.AddDays(-30));
                    state.status = scan.complete ? $"{state.known.Count} ilan · {DateTime.Now:HH:mm}" : "Tarama eksik; kaybolan ilanlar değerlendirilmedi";
                }
                catch { state.status = "Kontrol başarısız; sonraki turda tekrar denenecek"; }
                RenderServerSalesWatch();
            }
            SaveServerSalesWatch();
        }
        catch { _liveStatus.Text = "Takip kaydı güncellenemedi · ↻ ile tekrar dene"; }
        finally { _liveBusy=false; }
    }
    private sealed class ServerWatch
    {
        public Dictionary<string,LiveListing> known {get;set;} = new();
        public List<WatchSale> sales {get;set;} = new();
        public int cursor {get;set;}
        public string status {get;set;} = "İzleniyor";
    }
    private sealed class WatchSale
    {
        public LiveListing listing {get;set;} = new();
        public DateTime detectedAt {get;set;}
    }
    private sealed class WatchScan
    {
        public List<LiveListing> rows {get;set;} = new();
        public List<LiveListing> sold {get;set;} = new();
        public bool complete {get;set;}
        public int nextCursor {get;set;}
    }
}
