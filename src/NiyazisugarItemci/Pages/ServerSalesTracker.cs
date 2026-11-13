using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace NiyazisugarItemci;
public sealed partial class MainForm
{
    private readonly Dictionary<string, ServerWatch> _serverWatch = new();
    private string _serverWatchRenderedKey="";
    private bool _watchUnitsBusy;
    private string _watchUnitsKey="";
    private DateTime _serverWatchNextTurnUtc;
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
            foreach (var entry in saved ?? new()) { entry.Value.dismissed ??= new(StringComparer.OrdinalIgnoreCase); PruneBulkWatchSales(entry.Value); _serverWatch[entry.Key] = entry.Value; }
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
    // Kesin: ürün sayfasında "satıldı" işareti. Güçlü: ilan satışa kapandı ve satıcı sayacı arttı,
    // son açık ölçüm en fazla 6 saat önce. Diğerleri (eski ölçüm, yalnızca kategori görüntüsü) olasıdır.
    private static bool WatchSaleStrong(WatchSale sale)=>!IsWatchPlaceholder(sale.listing.title)&&(sale.evidence=="explicit"||sale.evidence=="stock"||(sale.evidence=="counter"&&sale.baselineAgeMinutes<=360));
    private static string WatchSaleSignal(WatchSale sale)=>sale.evidence switch
    {
        _ when IsWatchPlaceholder(sale.listing.title)=>"Olası · duyuru ilanı",
        "explicit"=>"Kesin · satıldı",
        "stock"=>$"Güçlü · kısmi satış +{sale.sellerIncrease}",
        "counter" when sale.baselineAgeMinutes<=360=>$"Güçlü · sayaç +{sale.sellerIncrease}",
        "counter"=>$"Olası · eski ölçüm +{sale.sellerIncrease}",
        _=>$"Olası · sayaç +{sale.sellerIncrease}"
    };
    private DateTime WatchPeriodStart=>_watchPeriod.SelectedIndex switch {1=>TurkeyToday.AddDays(-6),2=>TurkeyToday.AddDays(-29),_=>TurkeyToday};
    private void RenderServerSalesWatch()
    {
        if (_liveMarket is null || _liveServer.SelectedItem is not MarketServerInfo server) return;
        _serverWatch.TryGetValue(server.Id, out var state);
        var from=WatchPeriodStart;bool today=_watchPeriod.SelectedIndex<=0;
        var all = state?.sales.Where(s => s.detectedAt.Date >= from).OrderByDescending(s => s.detectedAt).ToList() ?? new();
        if(_serverWatchRenderedServer!=server.Id){_serverWatchVisibleCount=50;_serverWatchRenderedServer=server.Id;}
        var culture=System.Globalization.CultureInfo.GetCultureInfo("tr-TR");
        string Tl(decimal value)=>value.ToString("N2",culture)+" TL";
        var strongTotal=all.Where(WatchSaleStrong).Sum(s=>s.listing.price);
        var possibleTotal=all.Where(s=>!WatchSaleStrong(s)).Sum(s=>s.listing.price);
        _liveCount.Text=all.Count.ToString();
        _liveVolume.Text=Tl(strongTotal);
        _livePossible.Text=Tl(possibleTotal);
        _liveLast.Text=all.FirstOrDefault() is WatchSale latest?(latest.detectedAt.Date==TurkeyToday?latest.detectedAt.ToString("HH:mm:ss"):latest.detectedAt.ToString("dd.MM HH:mm")):"—";
        var lastCheck=state is not null&&state.lastSuccessUtc!=DateTime.MinValue?state.lastSuccessUtc.ToLocalTime().ToString("HH:mm"):"henüz yok";
        var problem=state?.status is string st&&(st.StartsWith("Kontrol başarısız")||st.StartsWith("Site işlem sınırı"))?" · "+st:"";
        _liveStatus.Text=$"Son kontrol {lastCheck} · {state?.observations.Count??0} ilan ölçülü"+problem;
        // Satıcı sıralaması: kesin + olası tüm tespitlerin ilan fiyatı toplamı.
        var sellers=all.Where(s=>!string.IsNullOrWhiteSpace(s.listing.seller)).GroupBy(s=>s.listing.seller,StringComparer.OrdinalIgnoreCase)
            .Select(g=>new SellerBoardItem(g.First().listing.seller,g.Count(),g.Sum(s=>s.listing.price),Tl(g.Sum(s=>s.listing.price))))
            .OrderByDescending(x=>x.Total).ThenByDescending(x=>x.Count).ToList();
        if(_watchPinnedSeller.Length>0&&!sellers.Any(x=>string.Equals(x.Seller,_watchPinnedSeller,StringComparison.OrdinalIgnoreCase)))_watchPinnedSeller="";
        _sellerBoard?.SetItems(sellers,_watchPinnedSeller);
        if(_sellerBoardTitle is not null)
        {
            var pinned=sellers.FirstOrDefault(x=>string.Equals(x.Seller,_watchPinnedSeller,StringComparison.OrdinalIgnoreCase));
            _sellerBoardTitle.Text=pinned is null?$"{sellers.Count} satıcı · tıkla, üste al":$"{pinned.Seller} · {pinned.Count} satış · {pinned.TotalText}";
            _sellerBoardTitle.ForeColor=pinned is null?ReferenceUi.Muted:ReferenceUi.Gold;
        }
        // Seçili satıcının satışları üste alınır, kalanlar zaman sırasıyla devam eder.
        var rows=_watchPinnedSeller.Length==0?all:all.OrderByDescending(s=>string.Equals(s.listing.seller,_watchPinnedSeller,StringComparison.OrdinalIgnoreCase)).ThenByDescending(s=>s.detectedAt).ToList();
        _=RefreshWatchUnitsLocalAsync(server,all.Where(s=>s.detectedAt.Date==TurkeyToday).ToList());
        var key=server.Id+":"+_watchPeriod.SelectedIndex+":"+_watchPinnedSeller+":"+string.Join("|",rows.Select(s=>s.listing.url+s.detectedAt.Ticks+":"+s.listing.price.ToString(System.Globalization.CultureInfo.InvariantCulture)+":"+s.priceFromDetail+":"+s.listing.title+":"+s.listing.seller+":"+s.listing.quantity+":"+s.listing.unitLabel+":"+s.evidence));
        if(key==_serverWatchRenderedKey)return;
        _serverWatchRenderedKey=key;
        var items=rows.Select(sale=>
        {
            var row=sale.listing;
            var unit=WatchUnitPrice(row);
            var strong=WatchSaleStrong(sale);
            var why=sale.evidence switch
            {
                "explicit"=>"Ürün sayfasında satıldı işareti görüldü.",
                "stock"=>"İlan açık kaldı ama stoğu azaldı ve satıcının toplam satış sayacı arttı (kısmi satış). Tutar = birim fiyat × satılan adet.",
                "counter"=>$"İlan satışa kapandı ve satıcının toplam satış sayacı {sale.sellerIncrease} arttı."+(strong?"":" İlanın açık görüldüğü son ölçüm 6 saatten eski; sayaç artışı başka bir ilana ait olabilir."),
                _=>"İlan listede görüldükten sonra satışa kapandı ve satıcı sayacı arttı. İlanın tek başına açık ölçümü yok; satış kesinleşmedi."
            };
            var tip=$"{row.title}\nSatıcı: {row.seller}\nİlan fiyatı: {Tl(row.price)}{(sale.priceFromDetail?"":" (liste fiyatı)")}\nTespit: {sale.detectedAt:dd.MM.yyyy HH:mm:ss}\n{why}\nBirim fiyat: {unit}\nTıkla: ilanı aç";
            var pinned=_watchPinnedSeller.Length>0&&string.Equals(row.seller,_watchPinnedSeller,StringComparison.OrdinalIgnoreCase);
            return new WatchRowItem(row.title,row.seller,Tl(row.price),today?sale.detectedAt.ToString("HH:mm:ss"):sale.detectedAt.ToString("dd.MM HH:mm"),WatchSaleSignal(sale),strong,tip,()=>_=OpenOrActivateBrowserTabAsync(row.url,row.title),unit,pinned);
        }).ToList();
        _liveList?.SetItems(items);
        PositionLiveMarket();
    }
    private async Task RefreshWatchUnitsLocalAsync(MarketServerInfo server,List<WatchSale> sales)
    {
        var core=_marketWebView.CoreWebView2;
        if(_watchUnitsBusy||core is null||sales.Count==0||IsDisposed)return;
        var payload=JsonSerializer.Serialize(sales.Select(s=>s.listing));
        var key=server.Id+payload;
        if(_watchUnitsKey==key)return;
        _watchUnitsBusy=true;
        try
        {
            core.Resume();
            if(await core.ExecuteScriptAsync("typeof window.__NiyazisugarWatchUnitRows==='function'")!="true")
                await core.ExecuteScriptAsync(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"MarketAnalyzer.js")));
            var raw=await core.ExecuteScriptAsync($"window.__NiyazisugarWatchUnitRows({JsonSerializer.Serialize(server.Id)},{payload})");
            var units=JsonSerializer.Deserialize<List<LiveListing>>(raw)??new();
            bool changed=false;
            foreach(var unit in units)
            {
                var saved=sales.FirstOrDefault(s=>s.listing.url==unit.url&&s.listing.title==unit.title)?.listing;
                if(saved is null)continue;
                changed|=saved.quantity!=unit.quantity||saved.unitLabel!=unit.unitLabel;
                saved.quantity=unit.quantity;saved.unitLabel=unit.unitLabel;saved.categoryName=unit.categoryName;
            }
            _watchUnitsKey=server.Id+JsonSerializer.Serialize(sales.Select(s=>s.listing));
            if(changed){_serverWatchRenderedKey="";RenderServerSalesWatch();}
        }
        catch{_watchUnitsKey="";}
        finally{_watchUnitsBusy=false;if(!IsDisposed)await SuspendIdleMarketAsync();}
    }
    private static string WatchUnitPrice(LiveListing row)
    {
        if(row.quantity is not >0||row.price<=0||string.IsNullOrWhiteSpace(row.unitLabel))return "—";
        var unit=row.price/row.quantity.Value;
        return "1 "+row.unitLabel+" · "+unit.ToString(unit<0.0001m?"0.########":"0.####",System.Globalization.CultureInfo.GetCultureInfo("tr-TR"))+" TL";
    }
    // Tur başına açılan ürün sayfası. Eskiden 3'tü ve bunun 2'si artık çalışmayan mağaza sayfasına gidiyordu.
    private const int WatchDetailBudget=8;
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
            var known=state.threePageWindow?state.known.Values.Where(row=>!soldUrls.Contains(row.url)).OrderByDescending(row=>state.pending.TryGetValue(row.url,out var since)?since:DateTime.MinValue).ToList():new List<LiveListing>();
            var repairs=state.sales.Where(s=>!s.priceFromDetail).OrderBy(s=>s.priceCheckAttemptUtc).ThenByDescending(s=>s.detectedAt).Take(1).Select(s=>s.listing).ToList();
            await core.ExecuteScriptAsync($"window.__NiyazisugarServerSalesScan({JsonSerializer.Serialize(server.Id)},{JsonSerializer.Serialize(known)},{state.cursor},{state.pageCursor},{JsonSerializer.Serialize(state.observations.Values)},{JsonSerializer.Serialize(state.lastChecked)},{JsonSerializer.Serialize(repairs)},{JsonSerializer.Serialize(state.sellers.Values)},{JsonSerializer.Serialize(soldUrls)},{JsonSerializer.Serialize(state.sales.Where(s=>s.detectedAt.Date==TurkeyToday).Select(s=>s.listing))},{{detailBudget:{WatchDetailBudget},stores:false}}).then(result=>chrome.webview.postMessage({{watchToken:'{token}',result}})).catch(e=>chrome.webview.postMessage({{watchToken:'{token}',error:String(e.message||e)}}));");
            return await task.Task.WaitAsync(TimeSpan.FromSeconds(65));
        }
        finally { core.WebMessageReceived -= Message; }
    }
    private async Task ScanAllServerSalesAsync()
    {
        if(IsDisposed||Disposing)return;
        string wait=SiteAutomationBlocked?SitePauseMessage:_soldScanBusy?"Kişisel satış taramasının kısa bölümü tamamlanıyor":
            _autoBumpBusy||AutoBumpDue?"İlan yükseltme sırası bekleniyor":_composerPopup is not null||_composerBusy?"İlan penceresinin kapanması bekleniyor":
            _marketPanel.Visible?"Piyasa analizi açık":
            !_marketWebReady||_marketWebView.CoreWebView2 is null?"Itemci bağlantısı hazırlanıyor":"";
        if(_liveBusy)return;
        if(wait.Length>0){_liveStatus.Text="Takip bekliyor · "+wait;return;}
        var next=_serverWatchRetryUtc>_serverWatchNextTurnUtc?_serverWatchRetryUtc:_serverWatchNextTurnUtc;
        if(DateTime.UtcNow<next)return;
        _serverWatchNextTurnUtc=DateTime.UtcNow.AddMinutes(1);
        _liveBusy = true;
        _marketWebView.CoreWebView2.Resume();
        try
        {
            for (int attempt=0; !_marketAnalyzerInjected && attempt<100; attempt++) await Task.Delay(100);
            if (!_marketAnalyzerInjected) return;
            await _marketWebView.CoreWebView2.ExecuteScriptAsync(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"ServerSales.js")));
            var servers=_marketServers.OrderBy(s=>_serverWatch.TryGetValue(s.Id,out var watched)?watched.lastAttemptUtc:DateTime.MinValue).ToArray();
            var start=0;
            for(var offset=0;offset<Math.Min(1,servers.Length);offset++)
            {
                if(SiteAutomationBlocked||_soldScanBusy||_composerPopup is not null||_composerBusy||_marketPanel.Visible||_autoBumpBusy||AutoBumpDue)break;
                var index=(start+offset)%servers.Length;var server=servers[index];

                if (!_serverWatch.TryGetValue(server.Id,out var state)) _serverWatch[server.Id] = state = new();
                try
                {
                    state.lastAttemptUtc=DateTime.UtcNow;
                    state.status = "Taranıyor…";
                    if((_liveServer.SelectedItem as MarketServerInfo)?.Id==server.Id)RenderServerSalesWatch();
                    var scan = await ReadServerWatchAsync(server,state);
                    foreach(var unit in scan.units)
                    {
                        var saved=state.sales.FirstOrDefault(s=>s.listing.url==unit.url)?.listing;
                        if(saved is not null&&saved.title==unit.title){saved.quantity=unit.quantity;saved.unitLabel=unit.unitLabel;saved.categoryName=unit.categoryName;}
                    }
                    UpdateThreePageWatchWindow(state,scan,DateTime.UtcNow);
                    foreach(var url in scan.attemptedUrls){state.lastChecked[url]=DateTime.UtcNow;foreach(var sale in state.sales.Where(s=>s.listing.url==url))sale.priceCheckAttemptUtc=DateTime.UtcNow;}
                    foreach(var snapshot in scan.sellerResults)ApplyWatchSellerSnapshot(state,snapshot,DateTime.UtcNow);
                    foreach(var sellerName in scan.sellerAttempts){var key=sellerName.ToLowerInvariant();if(!state.sellers.TryGetValue(key,out var snapshot))state.sellers[key]=snapshot=new SellerWatchSnapshot {seller=sellerName};snapshot.lastAttemptUtc=DateTime.UtcNow;}
                    foreach(var key in state.sellers.OrderByDescending(p=>p.Value.lastAttemptUtc).Skip(300).Select(p=>p.Key).ToArray())state.sellers.Remove(key);
                    ApplyWatchObservations(state,scan.observations,DateTime.UtcNow);
                    RefreshVisibleBaselines(state,scan,DateTime.UtcNow);
                    foreach (var row in scan.sold)
                    {
                        if (!state.sales.Any(s=>s.listing.url==row.url))
                        {
                            var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time"));
                            var detail=scan.observations.FirstOrDefault(o=>o.url==row.url&&o.price>0);
                            if(detail is not null)UpdateWatchListingPrice(row,detail);
                            state.sales.Add(new WatchSale { listing=row,detectedAt=now,evidence="explicit",priceFromDetail=detail is not null,priceCheckAttemptUtc=DateTime.UtcNow });
                        }
                        state.known.Remove(row.url);state.pending.Remove(row.url);
                    }
                    if(scan.complete&&!scan.rateLimited)state.lastSuccessUtc=DateTime.UtcNow;
                    state.cursor = scan.nextCursor;state.pageCursor=scan.nextPage<1?1:scan.nextPage;
                    state.sales.RemoveAll(s=>s.detectedAt.Date < TurkeyToday.AddDays(-30));
                    state.status = $"{state.sellers.Values.Count(s=>s.sellerId.Length>0)} satıcı ölçümü · İlk 3 sayfa · {scan.pagesRead} okundu · {state.pending.Count} öncelikli · {state.observations.Count} sayaç ölçümü · {state.known.Count} izlenen ilan · {scan.@checked} durum kontrolü · {DateTime.Now:HH:mm}"+(scan.failed>0?$" · {scan.failed} sayfa okunamadı":"");
                    var noBaseline=state.observations.Values.Count(o=>!o.available&&!o.categoryOnly&&o.lastAvailableAtUtc==DateTime.MinValue);
                    if(noBaseline>0)state.status+=$" · {noBaseline} satış dışı: ilk ölçüm eksik";
                    if(!scan.complete)state.status+=" · Pencere taraması eksik";
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
    private void UpdateThreePageWatchWindow(ServerWatch state,WatchScan scan,DateTime nowUtc)
    {
        if(scan.complete)
        {
            var recorded=state.sales.Select(s=>s.listing.url).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var current=scan.rows.Where(r=>!recorded.Contains(r.url)).Select(r=>r.url).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if(!state.threePageWindow){state.known.Clear();state.observations.Clear();state.pending.Clear();state.lastChecked.Clear();}
            else foreach(var url in state.recentWindow.Where(url=>!current.Contains(url)&&!recorded.Contains(url)))if(!state.pending.ContainsKey(url))state.pending[url]=nowUtc;
            foreach(var url in current.Concat(recorded))state.pending.Remove(url);
            foreach(var url in state.pending.Where(p=>p.Value<nowUtc.AddHours(-24)).Select(p=>p.Key).ToArray())state.pending.Remove(url);
            foreach(var url in state.pending.OrderByDescending(p=>p.Value).Skip(200).Select(p=>p.Key).ToArray())state.pending.Remove(url);
            foreach(var url in state.known.Keys.Where(url=>!current.Contains(url)&&!state.pending.ContainsKey(url)).ToArray())state.known.Remove(url);
            foreach(var url in state.observations.Keys.Where(url=>!current.Contains(url)&&!state.pending.ContainsKey(url)).ToArray())state.observations.Remove(url);
            foreach(var url in state.lastChecked.Keys.Where(url=>!current.Contains(url)&&!state.pending.ContainsKey(url)).ToArray())state.lastChecked.Remove(url);
            state.recentWindow=current.ToList();state.threePageWindow=true;
        }
        var sold=state.sales.Select(s=>s.listing.url).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach(var row in scan.rows.Where(r=>!sold.Contains(r.url)))
        {
            state.known[row.url]=row;
            SeedCategoryCounterBaseline(state,row,nowUtc);
        }
        foreach(var url in sold){state.known.Remove(url);state.pending.Remove(url);}
    }
    // Bir ürün sayfası satıcının güncel toplam satış sayısını verir. Aynı satıcının şu an kategoride
    // görünen (yani açık) diğer ilanları için bu sayı taze başlangıç ölçümü olur; böylece her ilanı
    // tek tek açmadan, ilan kaybolduğunda sayaç karşılaştırması yapılabilir.
    private static void RefreshVisibleBaselines(ServerWatch state,WatchScan scan,DateTime nowUtc)
    {
        var visible=scan.rows.Where(r=>!string.IsNullOrWhiteSpace(r.seller)).ToList();
        var measuredSellers=scan.observations.Where(o=>!o.fromStore&&o.sellerId.Length>0&&o.seller.Length>0)
            .GroupBy(o=>o.seller,StringComparer.OrdinalIgnoreCase).Select(g=>g.OrderByDescending(o=>o.salesCount).First());
        foreach(var measured in measuredSellers)
        {
            var key=measured.seller.ToLowerInvariant();
            if(!state.sellers.TryGetValue(key,out var snapshot))state.sellers[key]=snapshot=new SellerWatchSnapshot {seller=measured.seller};
            if(snapshot.sellerId.Length==0||(snapshot.sellerId==measured.sellerId&&measured.salesCount>=snapshot.salesCount))
            {snapshot.sellerId=measured.sellerId;snapshot.seller=measured.seller;snapshot.salesCount=measured.salesCount;snapshot.observedAtUtc=nowUtc;snapshot.lastAttemptUtc=nowUtc;}
            foreach(var row in visible.Where(r=>string.Equals(r.seller,measured.seller,StringComparison.OrdinalIgnoreCase)&&r.url!=measured.url&&state.known.ContainsKey(r.url)))
            {
                state.observations.TryGetValue(row.url,out var previous);
                if(previous is not null&&previous.sellerId.Length>0&&previous.sellerId!=measured.sellerId)continue;
                if(previous is not null&&previous.salesCount>measured.salesCount)continue;
                if(previous is not null&&!previous.available&&!previous.categoryOnly)continue;
                var baseline=previous??new ProductObservation {url=row.url,title=row.title,price=row.price,seller=row.seller};
                baseline.sellerId=measured.sellerId;baseline.seller=measured.seller;baseline.salesCount=measured.salesCount;
                baseline.available=true;baseline.categoryOnly=false;baseline.lastAvailableAtUtc=nowUtc;baseline.lastAvailableCount=measured.salesCount;baseline.observedAtUtc=nowUtc;
                if(baseline.price<=0)baseline.price=row.price;
                state.observations[row.url]=baseline;
            }
        }
    }
    private static void SeedCategoryCounterBaseline(ServerWatch state,LiveListing row,DateTime nowUtc)
    {
        if(state.observations.ContainsKey(row.url)||string.IsNullOrWhiteSpace(row.seller))return;
        var seller=state.sellers.Values.FirstOrDefault(s=>s.sellerId.Length>0&&string.Equals(s.seller,row.seller,StringComparison.OrdinalIgnoreCase)&&s.observedAtUtc>=nowUtc.AddMinutes(-10)&&s.observedAtUtc<=nowUtc);
        if(seller is null)return;
        // This is a category sighting with an independently measured counter,
        // not a claim that the product was individually verified as available.
        state.observations[row.url]=new ProductObservation {url=row.url,sellerId=seller.sellerId,seller=seller.seller,salesCount=seller.salesCount,price=row.price,title=row.title,categoryOnly=true,categorySeenAtUtc=nowUtc,categoryCount=seller.salesCount,categoryCounterAtUtc=seller.observedAtUtc,observedAtUtc=nowUtc};
    }
    private void ApplyWatchObservations(ServerWatch state,IEnumerable<ProductObservation> observations,DateTime nowUtc)
    {
        foreach(var current in observations)
        {
            var existingSale=state.sales.FirstOrDefault(s=>s.listing.url==current.url);
            if(existingSale is not null&&current.price>0&&!current.fromStore){UpdateWatchListingPrice(existingSale.listing,current);existingSale.priceFromDetail=true;existingSale.priceCheckAttemptUtc=nowUtc;}
            if(state.known.TryGetValue(current.url,out var knownListing))UpdateWatchListingPrice(knownListing,current);
            state.observations.TryGetValue(current.url,out var previous);
            // Public HTML may be served from a short cache. Retain a recent
            // counter rise independently of when availability catches up.
            var sameSeller=previous is not null&&previous.sellerId==current.sellerId;
            if(sameSeller&&current.salesCount<previous!.salesCount)continue;
            if(sameSeller&&previous!.categoryOnly&&!current.available)
            {
                current.categoryOnly=true;current.categorySeenAtUtc=previous.categorySeenAtUtc;
                current.categoryCount=previous.categoryCount;current.categoryCounterAtUtc=previous.categoryCounterAtUtc;
            }
            // Kısmi satış: ilan açık kaldı ama stoğu düştü ve satıcı sayacı arttı.
            if(!current.fromStore&&sameSeller&&previous!.available&&current.available&&previous.stock is int beforeStock&&current.stock is int afterStock&&afterStock<beforeStock&&current.salesCount>previous.salesCount&&state.known.TryGetValue(current.url,out var stockListing))
            {
                var units=beforeStock-afterStock;var saleKey=current.url+"#stok-"+afterStock;
                if(!state.sales.Any(s=>s.listing.url==saleKey))
                    state.sales.Add(new WatchSale {listing=new LiveListing {title=stockListing.title+$" · {units} adet",price=(current.price>0?current.price:stockListing.price)*units,seller=stockListing.seller,sellerUrl=stockListing.sellerUrl,url=saleKey,categoryName=stockListing.categoryName},
                        detectedAt=TimeZoneInfo.ConvertTimeFromUtc(nowUtc,TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time")),evidence="stock",sellerIncrease=current.salesCount-previous.salesCount,priceFromDetail=true,priceCheckAttemptUtc=nowUtc});
            }
            current.observedAtUtc=nowUtc;
            if(sameSeller&&current.salesCount>previous!.salesCount){current.counterRiseUtc=nowUtc;current.counterIncrease=current.salesCount-previous.salesCount;}
            else if(sameSeller&&previous!.counterRiseUtc>=nowUtc.AddMinutes(-15)){current.counterRiseUtc=previous.counterRiseUtc;current.counterIncrease=previous.counterIncrease;}

            if(current.available){current.lastAvailableAtUtc=nowUtc;current.lastAvailableCount=current.salesCount;}
            else if(sameSeller){current.lastAvailableAtUtc=previous!.lastAvailableAtUtc;current.lastAvailableCount=previous.lastAvailableCount;}
            var expired=DateTime.TryParse(current.expiresAt,out var expires)&&expires.ToUniversalTime()<=nowUtc;
            var counterDelta=Math.Max(current.salesCount-current.lastAvailableCount,current.counterRiseUtc>=nowUtc.AddMinutes(-15)?current.counterIncrease:0);
            var directSignal=counterDelta>0&&current.lastAvailableAtUtc>=nowUtc.AddHours(-24);
            var categoryDelta=current.salesCount-current.categoryCount;
            var categorySignal=current.categoryOnly&&categoryDelta>0&&current.categorySeenAtUtc>=nowUtc.AddHours(-24)&&
                current.categoryCounterAtUtc<=current.categorySeenAtUtc&&current.categoryCounterAtUtc>=current.categorySeenAtUtc.AddMinutes(-10)&&
                DateTimeOffset.TryParse(current.createdAt,out var created)&&created.UtcDateTime<=current.categorySeenAtUtc&&
                DateTimeOffset.TryParse(current.updatedAt,out var updated)&&updated.UtcDateTime>current.categorySeenAtUtc&&updated.UtcDateTime<=nowUtc.AddMinutes(1);
            if(current.available)state.dismissed.Remove(current.url);
            if(!current.fromStore&&previous is not null&&!current.available&&!expired&&sameSeller&&(directSignal||categorySignal)&&!state.dismissed.Contains(current.url)&&!state.sales.Any(s=>s.listing.url==current.url)&&state.known.TryGetValue(current.url,out var listing))
            {
                state.sales.Add(new WatchSale {listing=listing,detectedAt=TimeZoneInfo.ConvertTimeFromUtc(nowUtc,TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time")),evidence=directSignal?"counter":"category-counter",sellerIncrease=directSignal?counterDelta:categoryDelta,baselineAgeMinutes=directSignal?(int)Math.Max(0,(nowUtc-current.lastAvailableAtUtc).TotalMinutes):0,priceFromDetail=current.price>0,priceCheckAttemptUtc=nowUtc,sellerId=current.sellerId,sellerCount=current.salesCount,listingUpdatedAt=current.updatedAt});
                state.known.Remove(current.url);state.pending.Remove(current.url);
            }
            state.observations[current.url]=current;
            // Leaving page three is not the end of a listing's lifecycle.
            // Keep its baseline and bounded pending entry if it is still active;
            // otherwise its later sale can never be correlated.

        }
        PruneBulkWatchSales(state);
    }
    // Satıcı birden çok ilanını aynı anda "satışta değil" yaparsa (aynı saniyede güncellenmiş ilanlar)
    // ve satış sayacı ilan sayısı kadar artmadıysa bu satış değil toplu düzenlemedir: kayıtlar silinir,
    // ilanlar izlemeye geri döner ve tekrar açık görülene kadar satış sayılmaz.
    private static void PruneBulkWatchSales(ServerWatch state)
    {
        static DateTime Stamp(WatchSale s)=>DateTimeOffset.TryParse(s.listingUpdatedAt,out var u)?u.UtcDateTime:s.detectedAt;
        var candidates=state.sales.Where(s=>s.evidence is "counter" or "category-counter"&&!string.IsNullOrWhiteSpace(s.listing.seller)).GroupBy(s=>s.listing.seller,StringComparer.OrdinalIgnoreCase);
        if(state.dismissed.Count>1000)state.dismissed.Clear();
        var remove=new List<WatchSale>();
        foreach(var seller in candidates)
        {
            var ordered=seller.OrderBy(Stamp).ToList();
            for(int i=0;i<ordered.Count;)
            {
                int j=i+1;while(j<ordered.Count&&(Stamp(ordered[j])-Stamp(ordered[j-1])).TotalSeconds<=5)j++;
                var cluster=ordered.GetRange(i,j-i);
                if(cluster.Count>1&&cluster.Count>cluster.Max(s=>s.sellerIncrease))remove.AddRange(cluster);
                i=j;
            }
        }
        foreach(var sale in remove)
        {
            state.sales.Remove(sale);state.dismissed.Add(sale.listing.url);
            if(!sale.listing.url.Contains('#'))state.known[sale.listing.url]=sale.listing;
        }
    }
    // "Mesaj atın", "siparişleriniz alınır" gibi vitrin/duyuru ilanları gerçek satış ilanı değildir.
    private static readonly System.Text.RegularExpressions.Regex WatchPlaceholderPattern=new(@"mesaj\s*at|siparisleriniz\s*alinir|fiyat\s*icin|sinirsiz\s*stok|her\s*turlu");
    private static bool IsWatchPlaceholder(string title)
    {
        var text=new string((title??"").Replace('İ','i').Replace('I','ı').ToLowerInvariant().Replace('ı','i').Normalize(System.Text.NormalizationForm.FormD).Where(c=>System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)!=System.Globalization.UnicodeCategory.NonSpacingMark).ToArray());
        return WatchPlaceholderPattern.IsMatch(text);
    }
    private void ApplyWatchSellerSnapshot(ServerWatch state,SellerWatchSnapshot snapshot,DateTime nowUtc)
    {
        var key=snapshot.seller.ToLowerInvariant();state.sellers.TryGetValue(key,out var previous);
        if(previous is not null&&previous.sellerId==snapshot.sellerId&&snapshot.salesCount<previous.salesCount)return;
        snapshot.observedAtUtc=nowUtc;snapshot.lastAttemptUtc=nowUtc;
        // Bootstrap every monitored product present in this seller snapshot.
        // Do not create sales just because a store product disappears.
        var baseline=new List<ProductObservation>();
        foreach(var row in snapshot.products.Where(r=>state.known.ContainsKey(r.url)))baseline.Add(new ProductObservation {url=row.url,sellerId=snapshot.sellerId,seller=snapshot.seller,salesCount=snapshot.salesCount,available=true,price=row.price,title=row.title,fromStore=true});
        if(previous is not null&&previous.sellerId==snapshot.sellerId&&previous.complete&&snapshot.complete&&previous.observedAtUtc>=nowUtc.AddHours(-2))
        {
            var present=snapshot.products.Select(r=>r.url).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach(var row in previous.products.Where(r=>state.known.ContainsKey(r.url)&&!present.Contains(r.url)))
            {
                if(!state.observations.TryGetValue(row.url,out var measured)||measured.lastAvailableAtUtc==DateTime.MinValue)
                    state.observations[row.url]=new ProductObservation {url=row.url,sellerId=previous.sellerId,seller=previous.seller,salesCount=previous.salesCount,available=true,lastAvailableCount=previous.salesCount,lastAvailableAtUtc=previous.observedAtUtc,observedAtUtc=previous.observedAtUtc,price=row.price,title=row.title,fromStore=true};
                if(snapshot.salesCount>previous.salesCount)state.pending[row.url]=nowUtc;
            }
        }
        ApplyWatchObservations(state,baseline,nowUtc);
        state.sellers[key]=snapshot;
    }
    private sealed class SellerWatchSnapshot
    {
        public string url {get;set;}="";
        public string sellerId {get;set;}="";
        public string seller {get;set;}="";
        public int salesCount {get;set;}
        public bool complete {get;set;}
        public List<LiveListing> products {get;set;}=new();
        public DateTime observedAtUtc {get;set;}
        public DateTime lastAttemptUtc {get;set;}
    }
    private static void UpdateWatchListingPrice(LiveListing listing,ProductObservation current)
    {
        if(current.price<=0)return;
        listing.price=current.price;
        if(!string.IsNullOrWhiteSpace(current.title)){if(listing.title!=current.title){listing.quantity=null;listing.unitPrice=null;listing.unitLabel="";}listing.title=current.title;}
        if(!string.IsNullOrWhiteSpace(current.seller))listing.seller=current.seller;
    }
    private sealed class ProductObservation
    {
        public string url {get;set;}="";
        public bool fromStore {get;set;}
        public string sellerId {get;set;}="";
        public decimal price {get;set;}
        public string title {get;set;}="";
        public string seller {get;set;}="";
        public bool available {get;set;}
        public int salesCount {get;set;}
        public string expiresAt {get;set;}="";
        public string createdAt {get;set;}="";
        public string updatedAt {get;set;}="";
        public bool categoryOnly {get;set;}
        public DateTime categorySeenAtUtc {get;set;}
        public DateTime categoryCounterAtUtc {get;set;}
        public int categoryCount {get;set;}
        public DateTime observedAtUtc {get;set;}
        public DateTime lastAvailableAtUtc {get;set;}
        public int lastAvailableCount {get;set;}
        public DateTime counterRiseUtc {get;set;}
        public int counterIncrease {get;set;}
        public int? stock {get;set;}
    }
    private sealed class ServerWatch
    {
        public DateTime lastAttemptUtc {get;set;}
        public DateTime lastSuccessUtc {get;set;}
        public Dictionary<string,DateTime> lastChecked {get;set;}=new();
        public Dictionary<string,SellerWatchSnapshot> sellers {get;set;}=new();
        public bool threePageWindow {get;set;}
        public List<string> recentWindow {get;set;}=new();
        public Dictionary<string,DateTime> pending {get;set;}=new();
        public Dictionary<string,ProductObservation> observations {get;set;}=new();
        public Dictionary<string,LiveListing> known {get;set;} = new();
        public List<WatchSale> sales {get;set;} = new();
        public HashSet<string> dismissed {get;set;} = new(StringComparer.OrdinalIgnoreCase);
        public int pageCursor {get;set;} = 1;
        public int cursor {get;set;}
        public string status {get;set;} = "İzleniyor";
    }
    private sealed class WatchSale
    {
        public LiveListing listing {get;set;} = new();
        public DateTime detectedAt {get;set;}
        public string evidence {get;set;}="explicit";
        public int sellerIncrease {get;set;}
        public int baselineAgeMinutes {get;set;}
        public bool priceFromDetail {get;set;}
        public DateTime priceCheckAttemptUtc {get;set;}
        public string sellerId {get;set;}="";
        public int sellerCount {get;set;}
        public string listingUpdatedAt {get;set;}="";
    }
    private sealed class WatchScan
    {
        public List<LiveListing> units {get;set;} = new();
        public List<LiveListing> rows {get;set;} = new();
        public List<LiveListing> sold {get;set;} = new();
        public List<ProductObservation> observations {get;set;}=new();
        public List<string> attemptedUrls {get;set;}=new();
        public List<SellerWatchSnapshot> sellerResults {get;set;}=new();
        public List<string> sellerAttempts {get;set;}=new();
        public bool complete {get;set;}
        public int nextCursor {get;set;}
        public int nextPage {get;set;} = 1;
        public int pagesRead {get;set;}
        public int @checked {get;set;}
        public int failed {get;set;}
        public bool rateLimited {get;set;}
    }
}
