using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace NiyazisugarItemci;
public sealed partial class MainForm
{
    private readonly Dictionary<string, ServerWatch> _serverWatch = new();
    private string _serverWatchRenderedKey="";
    private int _serverWatchNextIndex;
    private DateTime _serverWatchRetryUtc;
    private int _serverWatchVisibleCount=50;
    private string _serverWatchRenderedServer="";
    private readonly Font _watchTitleFont=new("Segoe UI",8.5F,FontStyle.Bold);
    private readonly Font _watchInfoFont=new("Segoe UI",8,FontStyle.Bold);
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
        _serverWatch.TryGetValue(server.Id, out var state);
        var rows = state?.sales.Where(s => s.detectedAt.Date == TurkeyToday).OrderByDescending(s => s.detectedAt).ToList() ?? new();
        if(_serverWatchRenderedServer!=server.Id){_serverWatchVisibleCount=50;_serverWatchRenderedServer=server.Id;}
        _liveCount.Text=rows.Count.ToString(); _liveLast.Text=(rows.FirstOrDefault()?.detectedAt.ToString("HH:mm:ss")??"—");
        _liveStatus.Text = $"Bugün tespit: {rows.Count} · {state?.status ?? "Takip hazırlanıyor"}";
        var key=server.Id+":"+_serverWatchVisibleCount+":"+string.Join("|",rows.Select(s=>s.listing.url+s.detectedAt.Ticks));
        if(key==_serverWatchRenderedKey)return;
        _serverWatchRenderedKey=key;
        _liveRows.SuspendLayout();
        foreach(Control old in _liveRows.Controls.Cast<Control>().ToArray())old.Dispose();
        if (rows.Count == 0)
        {
            _liveRows.Controls.Add(new ReferenceEmptySale {Width=_liveRows.ClientSize.Width-4,Height=Math.Max(240,_liveRows.ClientSize.Height-22)});
        }
        foreach (var sale in rows.Take(_serverWatchVisibleCount))
        {
            var row = sale.listing;
            var card = new ReferencePanel { Width = _liveRows.ClientSize.Width - 4, Height = 65, BackColor = ReferenceUi.Surface, Margin = new Padding(0,0,0,5), Cursor = Cursors.Hand };
            var name = new Label { Dock = DockStyle.Top, Height = 22, Text = row.title, AutoEllipsis = true, ForeColor = TextMain, Font = _watchTitleFont, Padding = new Padding(4,2,0,0) };
            var seller = new Label { Dock = DockStyle.Top, Height = 20, Text = "Satıcı: " + row.seller, AutoEllipsis = true, ForeColor = PremiumTheme.Gold, Font = Font, Padding = new Padding(4,0,0,0) };
            var info = new Label { Dock = DockStyle.Fill, Text = $"{row.price:N2} TL · Tespit {sale.detectedAt:HH:mm:ss}", ForeColor = PremiumTheme.Turquoise, Font = _watchInfoFont, Padding = new Padding(4,0,0,0) };
            card.Controls.Add(info);card.Controls.Add(seller);card.Controls.Add(name);info.BringToFront();
            foreach (Control target in new Control[] {card,name,seller,info})
            {
                _sidebarToolTip.SetToolTip(target, $"{row.title}\nSatıcı: {row.seller}\nSon görülen ilan fiyatı: {row.price:N2} TL\nSatıldı işareti tespit edildi: {sale.detectedAt:dd.MM.yyyy HH:mm:ss}\nKesin işlem fiyatı ve satış saati site tarafından doğrulanmadı.");
                target.Click += (_, _) => _ = OpenOrActivateBrowserTabAsync(row.url,row.title);
            }
            _liveRows.Controls.Add(card);
        }
        if(rows.Count>_serverWatchVisibleCount)
        {
            var more=ActionButton("DAHA FAZLA GÖSTER",()=>{_serverWatchVisibleCount+=50;RenderServerSalesWatch();});
            more.Width=_liveMarket.Width-26;_liveRows.Controls.Add(more);
        }
        _liveRows.ResumeLayout();PositionLiveMarket();
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
            var soldUrls=state.sales.Select(s=>s.listing.url).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var known=state.known.Values.Where(row=>!soldUrls.Contains(row.url)).ToList();
            await core.ExecuteScriptAsync($"window.__NiyazisugarServerSalesScan({JsonSerializer.Serialize(server.Id)},{JsonSerializer.Serialize(known)},{state.cursor},{state.pageCursor}).then(result=>chrome.webview.postMessage({{watchToken:'{token}',result}})).catch(e=>chrome.webview.postMessage({{watchToken:'{token}',error:String(e.message||e)}}));");
            return await task.Task.WaitAsync(TimeSpan.FromSeconds(45));
        }
        finally { core.WebMessageReceived -= Message; }
    }
    private async Task ScanAllServerSalesAsync()
    {
        if (SiteAutomationBlocked || _soldScanBusy || DateTime.UtcNow<_serverWatchRetryUtc || _liveBusy || _autoBumpBusy || AutoBumpDue || _composerPopup is not null || _composerBusy || _marketPanel.Visible || !_marketWebReady || _marketWebView.CoreWebView2 is null || IsDisposed) return;
        _liveBusy = true;
        _marketWebView.CoreWebView2.Resume();
        try
        {
            for (int attempt=0; !_marketAnalyzerInjected && attempt<100; attempt++) await Task.Delay(100);
            if (!_marketAnalyzerInjected) return;
            await _marketWebView.CoreWebView2.ExecuteScriptAsync(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"ServerSales.js")));
            var servers=_marketServers.ToArray();
            var start=servers.Length==0?0:_serverWatchNextIndex%servers.Length;
            for(var offset=0;offset<Math.Min(1,servers.Length);offset++)
            {
                if(SiteAutomationBlocked||_soldScanBusy||_composerPopup is not null||_composerBusy||_marketPanel.Visible||_autoBumpBusy||AutoBumpDue)break;
                var index=(start+offset)%servers.Length;var server=servers[index];
                _serverWatchNextIndex=(index+1)%servers.Length;
                if (!_serverWatch.TryGetValue(server.Id,out var state)) _serverWatch[server.Id] = state = new();
                try
                {
                    state.status = "Taranıyor…";
                    if((_liveServer.SelectedItem as MarketServerInfo)?.Id==server.Id)RenderServerSalesWatch();
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
                    state.cursor = scan.nextCursor;state.pageCursor=scan.nextPage<1?1:scan.nextPage;
                    state.sales.RemoveAll(s=>s.detectedAt.Date < TurkeyToday.AddDays(-30));
                    state.status = $"{state.known.Count} izlenen ilan · {scan.@checked} durum kontrolü · {DateTime.Now:HH:mm}"+(scan.failed>0?$" · {scan.failed} sayfa okunamadı":"");
                    if(scan.rateLimited){PauseSiteAutomation(300);_serverWatchRetryUtc=DateTime.UtcNow.AddMinutes(5);state.status="Site işlem sınırı · 5 dakika bekleniyor";}
                }
                catch(Exception ex) { state.status = "Kontrol başarısız: "+ex.Message; }
                if((_liveServer.SelectedItem as MarketServerInfo)?.Id==server.Id)RenderServerSalesWatch();
                if(DateTime.UtcNow<_serverWatchRetryUtc)break;
            }
            SaveServerSalesWatch();
        }
        catch { _liveStatus.Text = "Takip kaydı güncellenemedi · ↻ ile tekrar dene"; }
        finally { _liveBusy=false;await SuspendIdleMarketAsync(); }
    }
    private sealed class ServerWatch
    {
        public Dictionary<string,LiveListing> known {get;set;} = new();
        public List<WatchSale> sales {get;set;} = new();
        public int pageCursor {get;set;} = 1;
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
        public int nextPage {get;set;} = 1;
        public int @checked {get;set;}
        public int failed {get;set;}
        public bool rateLimited {get;set;}
    }
}
