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
    internal async Task TestDesignAsync()
    {
        _monitorTimer.Stop();_salesTimer.Stop();_liveTimer.Stop();_autoBumpTimer.Stop();
        var output=Path.Combine(Environment.CurrentDirectory,"test-results");Directory.CreateDirectory(output);
        _browserEnvironment=await CoreWebView2Environment.CreateAsync(null,Path.Combine(Path.GetTempPath(),"niyazi-design-test-"+Guid.NewGuid().ToString("N")));
        await _webView.EnsureCoreWebView2Async(_browserEnvironment);
        _webView.CoreWebView2.NavigateToString("<html lang='tr'><body style='background:#181818;color:#ddd;font:16px Segoe UI;padding:32px'><h2>Itemci</h2><p>Yalıtılmış arayüz testi · Hesap oturumu ve ilan verisi kullanılmıyor.</p></body></html>");
        ClientSize=new Size(1440,900);
        await Task.Delay(500);ShowPanel(_browserPanel);await Task.Delay(300);Capture("main");
        var original=_liveMarket!.Width;_liveMarket.Width=48;await Task.Delay(100);if(_browserHost.Width<600)throw new Exception("Collapsed rail did not release browser space.");_liveMarket.Width=original;
        RenderCurrentListings(new List<ListingInfo>());ShowPanel(_listingsPanel);await Task.Delay(100);Capture("listings");
        // Verify real data binding, filters and total math using isolated test records.
        _salesHistory.Clear();_salesDate.Value=TurkeyToday;
        _salesHistory["test-order"]=new SaleRecord {OrderId="test-order",ItemTitle="Yalıtılmış test kaydı",Server="Test sunucusu",Timestamp=TurkeyToday.AddHours(12),GrossAmount=100.05m};
        RenderSoldSalesPanel();if(_salesGrid.Rows.Count!=1||_salesHistory["test-order"].NetAmount!=90.04m)throw new Exception("Commission or data binding changed.");
        _salesSearch.Text="eşleşmeyen";if(_salesGrid.Rows.Count!=0)throw new Exception("Sales search filter failed.");_salesSearch.Clear();_salesHistory.Clear();RenderSoldSalesPanel();
        ShowPanel(_salesPanel);await Task.Delay(100);Capture("sales");
        foreach(var size in new[]{new Size(1050,680),new Size(1280,720),new Size(1440,900)}) {
            ClientSize=size;await Task.Delay(100);
            foreach(var panel in new[]{_listingsPanel,_salesPanel,_browserPanel}) {
                ShowPanel(panel);PerformLayout();
                if(panel.Bounds.Top<_topNavigation!.Bottom)throw new Exception("Content overlaps top menu.");
                if(panel.Width<=0||panel.Height<=0)throw new Exception("Panel lost usable space.");
            }
        }
        ClientSize=new Size(1440,900);ShowPanel(_browserPanel);
        // Real WebView2 message page, empty isolated test. Native Itemci sections are left alone.
        var loaded=new TaskCompletionSource<bool>();_webView.CoreWebView2.NavigationCompleted+=(_,_)=>loaded.TrySetResult(true);
        _webView.CoreWebView2.NavigateToString("<html><body style='margin:0;background:#17191e;color:#ddd;font:14px Segoe UI'><div style='display:grid;grid-template-columns:260px 1fr;height:760px'><aside style='border-right:1px solid #333;padding:25px'>Mesajlar<br><p>Henüz konuşma yok</p></aside><main style='display:flex;flex-direction:column;padding:30px'><h2>Mesajlar</h2><div style='flex:1'>Yalıtılmış arayüz testi</div><div><textarea placeholder='Mesajınızı yazın' style='box-sizing:border-box;width:100%;height:85px;background:#24272e;color:white;border:1px solid #444'></textarea><button id='send'>Gönder</button></div></main></div><script>window.sent=0;document.getElementById('send').onclick=()=>sent++;</script></body></html>");await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var reply=await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"QuickReplyDock.js"));
        // Only adapt route detection in the test's about:blank fixture.
        reply=reply.Replace("function isMessagesPage(){return /\\/(panel\\/mesaj|messages?|chat)/i.test(location.pathname);}","function isMessagesPage(){return true;}");await _webView.CoreWebView2.ExecuteScriptAsync(reply);await Task.Delay(300);
        var check=await _webView.CoreWebView2.ExecuteScriptAsync("document.querySelector('#niyazi-quick-replies button').click();document.querySelector('textarea').value.length>0&&window.sent===0");if(check!="true")throw new Exception("Reply chip sent or failed to fill the input.");Capture("messages");
        await _marketWebView.EnsureCoreWebView2Async(_browserEnvironment);ShowPanel(_marketPanel);
        _marketWebView.CoreWebView2.NavigateToString("<html><body></body></html>");await Task.Delay(300);
        var analyzer=await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"MarketAnalyzer.js"));
        analyzer=analyzer.Replace("    window.__NiyazisugarRunMarket = runAnalysis;","    window.__DesignEmptyReport=()=>showReport([],0,0,{id:'msp2-global-yeni',name:'MSP2 Global - Yeni',url:'https://itemci.com/categories/msp2-global-yeni',categories:[]});\n    window.__NiyazisugarRunMarket = runAnalysis;");
        await _marketWebView.CoreWebView2.ExecuteScriptAsync(analyzer);await _marketWebView.CoreWebView2.ExecuteScriptAsync("window.__DesignEmptyReport()");await Task.Delay(300);Capture("market");
        Console.WriteLine("PASS: actual MainForm screens, filters, commission math, rail layout, viewport sizes and no-send quick replies.");
        void Capture(string name) { using var bitmap=new Bitmap(Width,Height);using var graphics=Graphics.FromImage(bitmap);graphics.CopyFromScreen(PointToScreen(Point.Empty),Point.Empty,new Size(ClientSize.Width,ClientSize.Height));bitmap.Save(Path.Combine(output,"design-"+name+".png"),ImageFormat.Png); }
    }
}
}
