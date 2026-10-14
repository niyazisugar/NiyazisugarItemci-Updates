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
        // Verify real data binding, filters and total math using isolated test records.
        _salesHistory.Clear();_salesDate.Value=TurkeyToday;
        _salesHistory["test-order"]=new SaleRecord {OrderId="test-order",ItemTitle="Yalıtılmış test kaydı",Server="Test sunucusu",Timestamp=TurkeyToday.AddHours(12),GrossAmount=100.05m};
        RenderSoldSalesPanel();if(_salesGrid.Rows.Count!=1||_salesHistory["test-order"].NetAmount!=90.04m)throw new Exception("Commission or data binding changed.");
        _salesSearch.Text="eşleşmeyen";if(_salesGrid.Rows.Count!=0)throw new Exception("Sales search filter failed.");_salesSearch.Clear();
        _salesHistory["test-order-2"]=new SaleRecord {OrderId="test-order-2",ItemTitle="Test satış kaydı 02",Server="Test Sunucusu B",Timestamp=TurkeyToday.AddHours(11),GrossAmount=175.55m};
        _salesHistory["test-order-3"]=new SaleRecord {OrderId="test-order-3",ItemTitle="Test satış kaydı 03",Server="Test Sunucusu A",Timestamp=TurkeyToday.AddHours(10),GrossAmount=320m};
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
