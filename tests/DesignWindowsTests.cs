using Microsoft.Web.WebView2.Core;
using System.Drawing.Imaging;
using NiyazisugarItemci;
internal static class DesignWindowsTests
{
    internal static Exception? Failure;
    [STAThread] private static int Main()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        using var form=new MainForm();
        using var deadline=new System.Windows.Forms.Timer { Interval=240000 };
        deadline.Tick+=(_,_)=>{Failure=new TimeoutException("Windows interface checks timed out.");Environment.Exit(1);};deadline.Start();
        form.Shown+=async(_,_)=>{try{await form.TestDesignAsync();}catch(Exception ex){Failure=ex;Console.Error.WriteLine(ex);}finally{Environment.Exit(Failure is null?0:1);}};
        Application.Run(form);return Failure is null?0:1;
    }
}
namespace NiyazisugarItemci
{
public sealed partial class MainForm
{
    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet=System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern bool EnumDisplaySettings(string? device,int mode,ref DisplayMode value);
    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet=System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int ChangeDisplaySettings(ref DisplayMode value,int flags);
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential,CharSet=System.Runtime.InteropServices.CharSet.Unicode)]
    private struct DisplayMode {
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr,SizeConst=32)] public string DeviceName;
        public ushort SpecVersion,DriverVersion,Size,DriverExtra;public uint Fields;
        public int PositionX,PositionY;public uint Orientation,FixedOutput;
        public short Color,Duplex,YResolution,TTOption,Collate;
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr,SizeConst=32)] public string FormName;
        public ushort LogPixels;public uint BitsPerPel,PelsWidth,PelsHeight,DisplayFlags,Frequency,ICMMethod,ICMIntent,MediaType,DitherType,Reserved1,Reserved2,PanningWidth,PanningHeight;
    }
    private static void TrySetTestDesktop() {
        var mode=new DisplayMode { Size=(ushort)System.Runtime.InteropServices.Marshal.SizeOf<DisplayMode>() };
        if(EnumDisplaySettings(null,-1,ref mode)) {mode.PelsWidth=1920;mode.PelsHeight=1080;mode.Fields=0x80000|0x100000;Console.WriteLine("Test desktop resolution result: "+ChangeDisplaySettings(ref mode,0));}
    }
    internal async Task TestDesignAsync()
    {
        TrySetTestDesktop();
        var brand=_topNavigation!.Controls[0].Controls.OfType<ReferenceBrand>().First();brand.Subtitle="Yalıtılmış test ortamı";
        _monitorTimer.Stop();_salesTimer.Stop();_liveTimer.Stop();_autoBumpTimer.Stop();
        var output=Path.Combine(Environment.CurrentDirectory,"test-results");Directory.CreateDirectory(output);
        _browserEnvironment=await CoreWebView2Environment.CreateAsync(null,Path.Combine(Path.GetTempPath(),"niyazi-design-test-"+Guid.NewGuid().ToString("N")));
        _webView.Dock=DockStyle.Fill;_browserHost.Controls.Add(_webView);_webView.Visible=true;
        await _webView.EnsureCoreWebView2Async(_browserEnvironment);
        var firstTab=new BrowserTabState { Id=++_browserTabSequence, Title="Itemci",WebView=_webView };
        firstTab.TabPanel=CreateBrowserTabControl(firstTab);_browserTabs.Add(firstTab);_browserTabStrip.Controls.Add(firstTab.TabPanel);SelectBrowserTab(firstTab);
        _webView.CoreWebView2.NavigateToString("<html lang='tr'><body style='background:#181818;color:#ddd;font:16px Segoe UI;padding:32px'><h2>Itemci</h2><p>Yalıtılmış arayüz testi · Hesap oturumu ve ilan verisi kullanılmıyor.</p></body></html>");
        MinimumSize=new Size(900,600);
        Location=new Point(10,10);
        ClientSize=new Size(Math.Min(1440,Screen.PrimaryScreen!.Bounds.Width-40),Math.Min(900,Screen.PrimaryScreen.Bounds.Height-80));
        await Task.Delay(500);ShowPanel(_browserPanel);await Task.Delay(300);await CaptureAsync("main");
        // Exercise actual WinForms dropdown close/selection and repeated reopen.
        using(var model=new ComboBox())using(var select=new ReferenceSelect(model) {Size=new Size(220,44),Location=new Point(20,110)}) {
            model.Items.AddRange(new object[]{"MSP2","Sezar2"});model.SelectedIndex=0;Controls.Add(select);select.BringToFront();
            var open=typeof(ReferenceSelect).GetMethod("OpenChoices",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!;
            var menu=(ContextMenuStrip)typeof(ReferenceSelect).GetField("_choices",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(select)!;
            for(int attempt=0;attempt<3;attempt++) {
                open.Invoke(select,null);await Task.Delay(100);
                ((ToolStripMenuItem)menu.Items[1]).Select();SendKeys.SendWait("{ENTER}");await Task.Delay(100);
                if(model.SelectedIndex!=1||menu.IsDisposed||menu.Visible)throw new Exception("Server dropdown selection disposed its active menu or failed to close.");
                open.Invoke(select,null);await Task.Delay(100);SendKeys.SendWait("{ESC}");await Task.Delay(100);
                if(menu.IsDisposed||menu.Visible)throw new Exception("Dropdown cancellation or repeated reopen failed.");
                model.Items.Clear();model.Items.AddRange(new object[]{"MSP2","Sezar2"});model.SelectedIndex=0;
            }
            select.Dispose();if(!menu.IsDisposed)throw new Exception("Owned dropdown leaked after control disposal.");
        }
        // Exercise actual toast detection and shared cross-automation cooldown.
        var rateFixture=Path.Combine(Path.GetTempPath(),"niyazi-rate-fixture-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(rateFixture);
        File.WriteAllText(Path.Combine(rateFixture,"rate-test.html"),"<html><meta charset=utf-8><body>Yalıtılmış Itemci uyarı testi</body></html>");
        _webView.CoreWebView2.SetVirtualHostNameToFolderMapping("itemci.com",rateFixture,CoreWebView2HostResourceAccessKind.DenyCors);
        var rateLoaded=new TaskCompletionSource<bool>();
        void RateLoaded(object? sender,CoreWebView2NavigationCompletedEventArgs e)=>rateLoaded.TrySetResult(e.IsSuccess);
        _webView.CoreWebView2.NavigationCompleted+=RateLoaded;
        _webView.CoreWebView2.Navigate("https://itemci.com/rate-test.html");await rateLoaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
        _webView.CoreWebView2.NavigationCompleted-=RateLoaded;
        InstallSiteTrafficGuard(_webView);
        await _webView.CoreWebView2.ExecuteScriptAsync(SiteLimitObserver);
        await _webView.CoreWebView2.ExecuteScriptAsync("document.body.insertAdjacentHTML('beforeend','<div role=alert>Çok fazla deneme yaptınız. Güvenliğiniz için 32 saniye beklemeniz gerekiyor.</div>');");
        for(int n=0;n<30&&!SiteAutomationBlocked;n++)await Task.Delay(50);
        if(!SiteAutomationBlocked||_sitePauseUntilUtc<DateTime.UtcNow.AddSeconds(80)||_composerCooldownUntilUtc<_sitePauseUntilUtc)throw new Exception("Site rate-limit toast did not pause all automation and publication.");
        var savedGate=_sitePauseUntilUtc;_sitePauseUntilUtc=DateTime.MinValue;_sitePauseLoaded=false;
        if(!SiteAutomationBlocked||_sitePauseUntilUtc!=savedGate)throw new Exception("Shared site cooldown did not survive state reload.");
        // A new publishing worker must inherit the shared deadline after navigation.
        _composerTransport=new Microsoft.Web.WebView2.WinForms.WebView2 {Size=new Size(200,100),Location=new Point(-4000,-4000)};
        Controls.Add(_composerTransport);await _composerTransport.EnsureCoreWebView2Async(_browserEnvironment);
        _composerTransport.CoreWebView2.SetVirtualHostNameToFolderMapping("itemci.com",rateFixture,CoreWebView2HostResourceAccessKind.DenyCors);
        InstallSiteTrafficGuard(_composerTransport);
        var workerLoaded=new TaskCompletionSource<bool>();
        _composerTransport.CoreWebView2.NavigationCompleted+=(_,e)=>workerLoaded.TrySetResult(e.IsSuccess);
        _composerTransport.CoreWebView2.Navigate("https://itemci.com/rate-test.html");await workerLoaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var expectedStamp=new DateTimeOffset(savedGate).ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        string workerStamp="";for(int n=0;n<30;n++){workerStamp=await _composerTransport.CoreWebView2.ExecuteScriptAsync("String(window.__niyaziSitePauseUntil||0)");if(workerStamp=="\""+expectedStamp+"\"")break;await Task.Delay(50);}
        if(workerStamp!="\""+expectedStamp+"\"")throw new Exception("A newly navigated publishing worker lost the persisted site cooldown.");
        Controls.Remove(_composerTransport);_composerTransport.Dispose();_composerTransport=null;
        _sitePauseUntilUtc=DateTime.MinValue;_sitePauseLoaded=true;File.Delete(SitePausePath);_webView.CoreWebView2.ClearVirtualHostNameToFolderMapping("itemci.com");
        // Busy/cooldown clicks must explain their state without starting another worker.
        _composerCooldownUntilUtc=DateTime.MinValue;var wasReady=_webReady;_webReady=true;
        _liveBusy=true;await RefreshSoldListingsAsync(true);
        if(!_salesStatusLabel.Text.Contains("Başka bir işlem")||_soldScanView is not null)throw new Exception("Busy sales refresh silently returned or opened another browser.");
        _composerStatus.Text="";await PublishComposerAsync();
        if(!_composerStatus.Text.Contains("ilan henüz gönderilmedi")||_composerTransport is not null)throw new Exception("Publish overlapped a background scan or failed to explain the block.");
        _liveBusy=false;_soldScanBusy=true;await RefreshSoldListingsAsync(true);
        if(!_salesStatusLabel.Text.Contains("zaten devam ediyor"))throw new Exception("Duplicate refresh was not explained.");
        _soldScanBusy=false;_salesNextRefreshUtc=DateTime.UtcNow.AddSeconds(90);await RefreshSoldListingsAsync(true);
        if(!_salesStatusLabel.Text.Contains("saniye bekle")||_soldScanView is not null)throw new Exception("Sales cooldown was silent or started another scan.");
        _salesNextRefreshUtc=DateTime.MinValue;_webReady=wasReady;
        Console.WriteLine("PASS: shared cooldown survives worker navigation; busy publication and refresh do not overlap or silently fail.");
        // Regression for --tray startup: the real shown form must retain its
        // HWND and initialized WebView controllers over hide/restore cycles.
        await _messageMonitorWebView.EnsureCoreWebView2Async(_browserEnvironment);
        await _marketWebView.EnsureCoreWebView2Async(_browserEnvironment);
        var mainHandle=Handle;var browserHandle=_webView.Handle;var marketHandle=_marketWebView.Handle;
        int recreations=0;EventHandler recreated=(_,_)=>recreations++;HandleCreated+=recreated;
        for(int attempt=0;attempt<4;attempt++){
            HideToTray(showInfo:false);await Task.Delay(80);
            if(Visible||Handle!=mainHandle||_webView.IsDisposed||_marketWebView.IsDisposed)throw new Exception("Tray hiding recreated the form or disposed its browser.");
            RestoreFromTray();await Task.Delay(80);
            if(!Visible||Handle!=mainHandle||_webView.Handle!=browserHandle||_marketWebView.Handle!=marketHandle)throw new Exception("Tray restore recreated initialized browser handles.");
            await _webView.CoreWebView2.ExecuteScriptAsync("1+1");
            await _marketWebView.CoreWebView2.ExecuteScriptAsync("1+1");
            await _messageMonitorWebView.CoreWebView2.ExecuteScriptAsync("1+1");
        }
        HandleCreated-=recreated;if(recreations!=0)throw new Exception("Tray transitions recreated the main window.");
        var closing=new FormClosingEventArgs(CloseReason.UserClosing,false);OnFormClosing(this,closing);
        if(!closing.Cancel||Visible)throw new Exception("User close did not preserve close-to-tray behavior.");RestoreFromTray();
        var shutdown=new FormClosingEventArgs(CloseReason.WindowsShutDown,false);OnFormClosing(this,shutdown);
        if(shutdown.Cancel||!_allowExit)throw new Exception("Windows shutdown was incorrectly intercepted as close-to-tray.");_allowExit=false;
        Console.WriteLine("PASS: initialized browser handles survive tray startup, repeated hide/restore, user close and Windows shutdown routing.");
        var original=_liveMarket!.Width;_liveMarket.Width=48;await Task.Delay(100);if(_browserHost.Width<600)throw new Exception("Collapsed rail did not release browser space.");_liveMarket.Width=original;
        RenderCurrentListings(new List<ListingInfo> {
            new() {Title="Test ilanı 01 · başlık ve görsel yerleşimi",Server="Test Sunucusu A",PriceText="100,05 TL",Status="AKTİF",Url="https://itemci.com/product/test-ilan-01"},
            new() {Title="Test ilanı 02 · uzun başlığın okunurluk kontrolü",Server="Test Sunucusu B",PriceText="175,55 TL",Status="AKTİF",Url="https://itemci.com/product/test-ilan-02"},
            new() {Title="Test ilanı 03 · yatay işlem butonları",Server="Test Sunucusu A",PriceText="320,00 TL",Status="AKTİF",Url="https://itemci.com/product/test-ilan-03"}
        });
        var photos=Descendants(_listingsList).OfType<PictureBox>().ToArray();for(int i=0;i<photos.Length;i++)photos[i].Image=TestThumbnail(i+1);
        _listingSearch.Text="02";if(_listingsList.Controls.OfType<ReferenceRow>().Count()!=1)throw new Exception("Listing title filter failed.");_listingSearch.Clear();
        photos=Descendants(_listingsList).OfType<PictureBox>().ToArray();for(int i=0;i<photos.Length;i++)photos[i].Image=TestThumbnail(i+1);
        ShowPanel(_listingsPanel);await Task.Delay(100);await CaptureAsync("listings");
        // Seller-counter correlation: both signals required; no first-observation backfill.
        var watch=new ServerWatch();var stamp=DateTime.UtcNow;
        ProductObservation Observation(bool available,int count)=>new(){url="https://itemci.com/product/test/test",sellerId="seller",available=available,salesCount=count,expiresAt=stamp.AddDays(1).ToString("O")};
        watch.known["https://itemci.com/product/test/test"]=new LiveListing {url="https://itemci.com/product/test/test",title="Test takip ilanı",seller="Test satıcı",price=100};
        ApplyWatchObservations(watch,new[]{Observation(true,10)},stamp);
        ApplyWatchObservations(watch,new[]{Observation(true,14)},stamp.AddMinutes(1));if(watch.sales.Count!=0)throw new Exception("Seller count alone was classified as a sale.");
        watch=System.Text.Json.JsonSerializer.Deserialize<ServerWatch>(System.Text.Json.JsonSerializer.Serialize(watch))!;
        ApplyWatchObservations(watch,new[]{Observation(false,14)},stamp.AddMinutes(2));if(watch.sales.Count!=1||watch.sales[0].sellerIncrease!=4)throw new Exception("Counter-before-availability ordering lost the sale signal.");
        ApplyWatchObservations(watch,new[]{Observation(false,16)},stamp.AddMinutes(4));if(watch.sales.Count!=1)throw new Exception("Counter signal duplicated a listing.");
        var delayed=new ServerWatch();delayed.known["https://itemci.com/product/test/test"]=new LiveListing {url="https://itemci.com/product/test/test"};
        ApplyWatchObservations(delayed,new[]{Observation(true,10)},stamp);
        ApplyWatchObservations(delayed,new[]{Observation(false,10)},stamp.AddMinutes(1));if(delayed.sales.Count!=0)throw new Exception("Unavailable without counter rise was classified as a sale.");
        ApplyWatchObservations(delayed,new[]{Observation(false,11)},stamp.AddMinutes(2));if(delayed.sales.Count!=1||delayed.sales[0].sellerIncrease!=1)throw new Exception("Availability-before-counter ordering lost the sale signal.");
        var stale=new ServerWatch();stale.known["https://itemci.com/product/test/test"]=new LiveListing {url="https://itemci.com/product/test/test"};
        ApplyWatchObservations(stale,new[]{Observation(true,10)},stamp);ApplyWatchObservations(stale,new[]{Observation(true,14)},stamp.AddMinutes(1));ApplyWatchObservations(stale,new[]{Observation(false,14)},stamp.AddMinutes(30));if(stale.sales.Count!=0)throw new Exception("An old counter rise was reused for an unrelated unavailable listing.");
        var cached=new ServerWatch();cached.known["https://itemci.com/product/test/test"]=new LiveListing {url="https://itemci.com/product/test/test"};
        ApplyWatchObservations(cached,new[]{Observation(true,14)},stamp);ApplyWatchObservations(cached,new[]{Observation(true,10)},stamp.AddMinutes(1));ApplyWatchObservations(cached,new[]{Observation(false,14)},stamp.AddMinutes(2));if(cached.sales.Count!=0)throw new Exception("An older cached counter created a false rebound signal.");
        // The final product-detail price must replace the old category price.
        var priced=new ServerWatch();var priceUrl="https://itemci.com/product/test/test";
        priced.known[priceUrl]=new LiveListing {url=priceUrl,title="Old title",seller="Test seller",price=175m};
        var firstPrice=Observation(true,10);firstPrice.price=175m;ApplyWatchObservations(priced,new[]{firstPrice},stamp);
        var finalPrice=Observation(false,11);finalPrice.price=160m;finalPrice.title="Updated title";finalPrice.seller="Updated seller";ApplyWatchObservations(priced,new[]{finalPrice},stamp.AddMinutes(1));
        if(priced.sales.Count!=1||priced.sales[0].listing.price!=160m||!priced.sales[0].priceFromDetail||priced.sales[0].listing.title!="Updated title")throw new Exception("Sale retained the obsolete category price instead of final product detail.");
        priced.sales[0].listing.price=175m;priced.sales[0].priceFromDetail=false;
        var repairPrice=Observation(false,11);repairPrice.price=160m;ApplyWatchObservations(priced,new[]{repairPrice},stamp.AddMinutes(2));
        if(priced.sales.Count!=1||priced.sales[0].listing.price!=160m||!priced.sales[0].priceFromDetail)throw new Exception("Existing sale price repair failed or created a duplicate.");
        var missingPrice=Observation(false,11);missingPrice.price=0;ApplyWatchObservations(priced,new[]{missingPrice},stamp.AddMinutes(3));if(priced.sales[0].listing.price!=160m)throw new Exception("Unavailable price overwrote a valid recorded value.");
        var fresh=new ServerWatch();fresh.known["https://itemci.com/product/test/test"]=new LiveListing {url="https://itemci.com/product/test/test"};
        ApplyWatchObservations(fresh,new[]{Observation(false,20)},stamp);if(fresh.sales.Count!=0)throw new Exception("Already unavailable listing fabricated historical sales.");
        var expired=new ServerWatch();expired.known["https://itemci.com/product/test/test"]=new LiveListing {url="https://itemci.com/product/test/test"};
        ApplyWatchObservations(expired,new[]{Observation(true,10)},stamp);var end=Observation(false,11);end.expiresAt=stamp.AddSeconds(-1).ToString("O");ApplyWatchObservations(expired,new[]{end},stamp.AddMinutes(1));if(expired.sales.Count!=0)throw new Exception("Expired listing was classified as correlated sale.");
        // A single seller snapshot bootstraps several monitored product baselines.
        var sellerWatch=new ServerWatch();var storeRow=new LiveListing {url=priceUrl,title="Monitored store item",seller="TestSeller",price=175m};
        var secondRow=new LiveListing {url="https://itemci.com/product/test/second",title="Second monitored item",seller="TestSeller",price=200m};
        sellerWatch.known[storeRow.url]=storeRow;sellerWatch.known[secondRow.url]=secondRow;
        ApplyWatchSellerSnapshot(sellerWatch,new SellerWatchSnapshot {sellerId="seller",seller="TestSeller",salesCount=10,complete=true,products=new(){storeRow,secondRow}},stamp);
        if(sellerWatch.observations.Count!=2||sellerWatch.sales.Count!=0)throw new Exception("Seller batch did not seed both monitored baselines safely.");
        ApplyWatchSellerSnapshot(sellerWatch,new SellerWatchSnapshot {sellerId="seller",seller="TestSeller",salesCount=11,complete=true,products=new(){secondRow}},stamp.AddMinutes(1));
        if(sellerWatch.sales.Count!=0||!sellerWatch.pending.ContainsKey(priceUrl))throw new Exception("Store absence created a sale without product verification.");
        var verified=Observation(false,11);verified.price=160m;
        ApplyWatchObservations(sellerWatch,new[]{verified},stamp.AddMinutes(1));
        if(sellerWatch.sales.Count!=1||sellerWatch.sales[0].listing.price!=160m)throw new Exception("Seller snapshot to unavailable-product verification failed end to end.");
        var partialStore=new ServerWatch();partialStore.known[priceUrl]=storeRow;
        ApplyWatchSellerSnapshot(partialStore,new SellerWatchSnapshot {sellerId="seller",seller="TestSeller",salesCount=10,complete=true,products=new(){storeRow}},stamp);
        ApplyWatchSellerSnapshot(partialStore,new SellerWatchSnapshot {sellerId="seller",seller="TestSeller",salesCount=11,complete=false},stamp.AddMinutes(1));
        if(partialStore.sales.Count!=0||partialStore.pending.Count!=0)throw new Exception("Partial store pagination fabricated missing products.");
        // Three-page migration and disappearance queue must not convert old history into candidates.
        var scoped=new ServerWatch();scoped.known["old-archive"]=new LiveListing {url="old-archive"};
        var windowA=new WatchScan {complete=true,rows=new(){new(){url="a"},new(){url="b"}}};
        UpdateThreePageWatchWindow(scoped,windowA,stamp);if(scoped.known.Count!=2||scoped.pending.Count!=0)throw new Exception("Three-page migration retained old archive or fabricated pending sales.");
        UpdateThreePageWatchWindow(scoped,new WatchScan {complete=false,rows=new(){new(){url="c"}}},stamp.AddMinutes(1));if(scoped.pending.Count!=0||scoped.recentWindow.Count!=2)throw new Exception("Failed page changed the trusted window.");
        UpdateThreePageWatchWindow(scoped,new WatchScan {complete=true,rows=new(){new(){url="b"},new(){url="c"}}},stamp.AddMinutes(2));if(!scoped.pending.ContainsKey("a")||scoped.pending.Count!=1||scoped.sales.Count!=0)throw new Exception("Disappeared listing was not prioritized or was falsely marked sold.");
        UpdateThreePageWatchWindow(scoped,new WatchScan {complete=true,rows=new(){new(){url="b"},new(){url="c"}}},stamp.AddMinutes(33));if(!scoped.pending.ContainsKey("a"))throw new Exception("Backlog expired before a bounded checker could reach it.");
        UpdateThreePageWatchWindow(scoped,new WatchScan {complete=true,rows=new(){new(){url="b"},new(){url="c"}}},stamp.AddHours(3));if(scoped.pending.Count!=0||scoped.known.ContainsKey("a"))throw new Exception("Stale pending queue was not bounded.");
        // Verify real data binding, filters and total math using isolated test records.
        _salesHistory.Clear();_salesDate.Value=TurkeyToday;
        _salesHistory["test-order"]=new SaleRecord {OrderId="test-order",ItemTitle="Yalıtılmış test kaydı",Server="Test sunucusu",Timestamp=TurkeyToday.AddHours(12),GrossAmount=100.05m};
        RenderSoldSalesPanel();if(_salesGrid.Rows.Count!=1||_salesHistory["test-order"].NetAmount!=90.04m)throw new Exception("Commission or data binding changed.");
        _salesSearch.Text="eşleşmeyen";if(_salesGrid.Rows.Count!=0)throw new Exception("Sales search filter failed.");_salesSearch.Clear();
        _salesHistory["test-order-2"]=new SaleRecord {OrderId="test-order-2",ItemTitle="Test satış kaydı 02",Server="Test Sunucusu B",Timestamp=TurkeyToday.AddDays(-1).AddHours(11),GrossAmount=175.55m};
        _salesHistory["test-order-3"]=new SaleRecord {OrderId="test-order-3",ItemTitle="Test satış kaydı 03",Server="Test Sunucusu A",Timestamp=TurkeyToday.AddDays(-30).AddHours(10),GrossAmount=320m};
        SetSalesPeriod(true);
        if(_salesGrid.Rows.Count!=3||_salesGrossLabel.Text!=FormatTl(595.60m)||_salesNetLabel.Text!=FormatTl(536.03m)||_salesCommissionLabel.Text!=FormatTl(59.57m))throw new Exception("All-time totals exclude older sales or round incorrectly.");
        SetSalesPeriod(false);if(_salesGrid.Rows.Count!=1)throw new Exception("Today filter failed when date was unchanged.");
        _salesDate.Value=TurkeyToday.AddDays(-1);if(_salesGrid.Rows.Count!=1||_salesGrossLabel.Text!=FormatTl(175.55m))throw new Exception("Selected day totals failed.");
        SetSalesPeriod(true);_salesServerFilter.SelectedItem="Test Sunucusu A";if(_salesGrid.Rows.Count!=1||_salesGrossLabel.Text!=FormatTl(320m))throw new Exception("All-time server totals failed.");
        _salesServerFilter.SelectedIndex=0;_salesSearch.Text="02";if(_salesGrid.Rows.Count!=1||_salesGrossLabel.Text!=FormatTl(175.55m))throw new Exception("All-time search totals failed.");_salesSearch.Clear();
        RenderSoldSalesPanel();for(int i=0;i<_salesGrid.Rows.Count;i++)_salesGrid.Rows[i].Cells["Cover"].Value=TestThumbnail(i+1);
        ShowPanel(_salesPanel);await Task.Delay(100);await CaptureAsync("sales");
        foreach(var size in new[]{new Size(1050,680),new Size(1280,720),new Size(1440,900)}) {
            ClientSize=size;await Task.Delay(100);
            foreach(var panel in new[]{_listingsPanel,_salesPanel,_browserPanel}) {
                ShowPanel(panel);PerformLayout();
                if(panel.PointToScreen(Point.Empty).Y<_topNavigation!.PointToScreen(new Point(0,_topNavigation.Height)).Y)throw new Exception("Content overlaps top menu.");
                if(ReferenceEquals(panel,_browserPanel) && _browserHost.PointToScreen(new Point(_browserHost.Width,0)).X>_liveMarket!.PointToScreen(Point.Empty).X)throw new Exception("Sales rail overlays Itemci instead of reserving width.");
                foreach(var field in Descendants(panel).Where(c=>c.Visible&&(c is ReferenceSearch||c is ReferenceSelect))) {
                    if(field.Parent is null)continue;
                    if(field.Bottom>field.Parent.ClientSize.Height+1)throw new Exception("A reference filter was clipped in a small window.");
                }
                if(panel.Width<=0||panel.Height<=0)throw new Exception("Panel lost usable space.");
            }
        }
        ClientSize=new Size(Math.Min(1440,Screen.PrimaryScreen!.Bounds.Width-40),Math.Min(900,Screen.PrimaryScreen.Bounds.Height-80));ShowPanel(_browserPanel);
        // Real WebView2 message page, empty isolated test. Native Itemci sections are left alone.
        var loaded=new TaskCompletionSource<bool>();_webView.CoreWebView2.NavigationCompleted+=(_,_)=>loaded.TrySetResult(true);
        _webView.CoreWebView2.NavigateToString("<html><body style='margin:0;background:#17191e;color:#ddd;font:14px Segoe UI'><div style='display:grid;grid-template-columns:260px 1fr;height:760px'><aside style='border-right:1px solid #333;padding:25px'>Mesajlar<br><p>Henüz konuşma yok</p></aside><main style='display:flex;flex-direction:column;padding:30px'><h2>Mesajlar</h2><div style='flex:1'>Yalıtılmış arayüz testi</div><div><textarea placeholder='Mesajınızı yazın' style='box-sizing:border-box;width:100%;height:85px;background:#24272e;color:white;border:1px solid #444'></textarea><button id='send'>Gönder</button></div></main></div><script>window.sent=0;document.getElementById('send').onclick=()=>sent++;</script></body></html>");await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var reply=await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"QuickReplyDock.js"));
        // Only adapt route detection in the test's about:blank fixture.
        reply=reply.Replace("function isMessagesPage(){return /\\/(panel\\/mesaj|messages?|chat)/i.test(location.pathname);}","function isMessagesPage(){return true;}");await _webView.CoreWebView2.ExecuteScriptAsync(reply);await Task.Delay(300);
        var check=await _webView.CoreWebView2.ExecuteScriptAsync("document.querySelector('#niyazi-quick-replies button').click();document.querySelector('textarea').value.length>0&&window.sent===0");if(check!="true")throw new Exception("Reply chip sent or failed to fill the input.");firstTab.RequestedUrl="https://itemci.com/panel/mesajlar";SelectTopSection(_browserPanel);if(_topMessages!.ForeColor!=PremiumTheme.Gold)throw new Exception("Messages menu did not become active.");await CaptureAsync("messages");
        await _marketWebView.EnsureCoreWebView2Async(_browserEnvironment);ShowPanel(_marketPanel);
        _marketWebView.CoreWebView2.NavigateToString("<html><body></body></html>");await Task.Delay(300);
        var analyzer=await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"MarketAnalyzer.js"));
        analyzer=analyzer.Replace("    window.__NiyazisugarRunMarket = runAnalysis;","    window.__DesignEmptyReport=()=>showReport([],0,0,{id:'msp2-global-yeni',name:'MSP2 Global - Yeni',url:'https://itemci.com/categories/msp2-global-yeni',categories:defaultCategoriesClone()});\n    window.__NiyazisugarRunMarket = runAnalysis;");
        await _marketWebView.CoreWebView2.ExecuteScriptAsync(analyzer);await _marketWebView.CoreWebView2.ExecuteScriptAsync("window.__DesignEmptyReport()");await Task.Delay(300);await CaptureAsync("market");
        Console.WriteLine("PASS: actual MainForm screens, filters, commission math, rail layout, viewport sizes and no-send quick replies.");
        static IEnumerable<Control> Descendants(Control root) {foreach(Control child in root.Controls){yield return child;foreach(var nested in Descendants(child))yield return nested;}}
        static Bitmap TestThumbnail(int n) {var image=new Bitmap(100,76);using var g=Graphics.FromImage(image);g.Clear(Color.FromArgb(22,33,42));ReferenceUi.Icon(g,"list",new RectangleF(34,8,30,30),ReferenceUi.Gold);TextRenderer.DrawText(g,"TEST "+n,ReferenceUi.Font(9),new Rectangle(0,42,100,24),ReferenceUi.Text,TextFormatFlags.HorizontalCenter);return image;}
        async Task CaptureAsync(string name) { Refresh();await Task.Delay(180); using var bitmap=new Bitmap(ClientSize.Width,ClientSize.Height);using var graphics=Graphics.FromImage(bitmap);graphics.CopyFromScreen(PointToScreen(Point.Empty),Point.Empty,new Size(ClientSize.Width,ClientSize.Height));bitmap.Save(Path.Combine(output,"design-"+name+".png"),ImageFormat.Png); }
    }
}
}
