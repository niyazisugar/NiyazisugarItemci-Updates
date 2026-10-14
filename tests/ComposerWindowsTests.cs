using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using NiyazisugarItemci;

internal static class ComposerWindowsTests
{
    private static Exception? _failure;
    [STAThread]
    private static int Main()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        using var owner=new Form{Text="Mesajlar · Windows integration test",Size=new Size(500,400)};
        owner.Shown+=(_,_)=>
        {
            try
            {
                Console.WriteLine("WebView2 runtime: "+CoreWebView2Environment.GetAvailableBrowserVersionString());
                for(var cycle=0;cycle<2;cycle++)TestPopup(owner,cycle);
                Console.WriteLine("PASS: actual modal WinForms/WebView2 popup loads on Shown, receives native draft, renders mapped image, sends save message, captures rendered pixels and reopens.");
            }
            catch(Exception ex){_failure=ex;Console.Error.WriteLine(ex);}
            finally{owner.Close();}
        };
        Application.Run(owner);
        return _failure is null?0:1;
    }

    private static void TestPopup(Form owner,int cycle)
    {
        var root=Path.Combine(Path.GetTempPath(),"itemci-composer-windows-"+Guid.NewGuid().ToString("N"));
        var images=Path.Combine(root,"images");Directory.CreateDirectory(images);
        using(var bitmap=new Bitmap(32,32)){using var graphics=Graphics.FromImage(bitmap);graphics.Clear(Color.Turquoise);bitmap.Save(Path.Combine(images,"test.png"),System.Drawing.Imaging.ImageFormat.Png);}
        using var popup=new Form{Text="Hızlı İlan Ekle",ClientSize=new Size(1100,790),StartPosition=FormStartPosition.CenterParent};
        using var view=new WebView2{Dock=DockStyle.Fill,DefaultBackgroundColor=Color.FromArgb(16,25,31)};
        var overlay=new Panel{Dock=DockStyle.Fill,BackColor=Color.FromArgb(16,25,31)};
        popup.Controls.Add(view);popup.Controls.Add(overlay);overlay.BringToFront();
        var saveReceived=false;
        var state=new
        {
            servers=new[]{new{id="sezar2",name="Sezar2"}},serverId="sezar2",
            draft=new{title="Windows açılış testi",description="Gerçek WebView2 penceresinde yerel arayüz testi.",price=5000,delivery=1,validity=15,stock=false,orders=true,
                images=new[]{new{id="test.png",name="Yerel test görseli",url="https://composer-images.local/test.png"}}},
            busy=false,uncertain=false,remaining=0,kind="idle",message="Taslak hazır.",url=""
        };
        using var surface=new ComposerSurface(view,async text=>
        {
            using var json=JsonDocument.Parse(text);
            var action=json.RootElement.GetProperty("action").GetString();
            if(action=="ready")await view.CoreWebView2.ExecuteScriptAsync($"window.__composerReceive({JsonSerializer.Serialize(state)})");
            if(action=="save")saveReceived=true;
        },message=>_failure=new IOException(message));
        popup.Shown+=async(_,_)=>
        {
            try
            {
                var environment=await CoreWebView2Environment.CreateAsync(null,Path.Combine(root,"profile"));
                await surface.LoadAsync(environment,AppContext.BaseDirectory,images,CancellationToken.None);
                overlay.Visible=false;view.BringToFront();
                await AssertEventually(view,"document.querySelector('h1')?.textContent==='Hızlı İlan Ekle' && document.getElementById('title').value==='Windows açılış testi' && document.getElementById('cover').naturalWidth===32 && getComputedStyle(document.querySelector('.app')).display!=='none'");
                await view.CoreWebView2.ExecuteScriptAsync("document.getElementById('save').click()");
                for(var attempt=0;attempt<30&&!saveReceived;attempt++)await Task.Delay(100);
                if(!saveReceived)throw new IOException("Native save bridge did not receive its message.");
                var blocked=new TaskCompletionSource<bool>();
                EventHandler<CoreWebView2NavigationStartingEventArgs> check=(_,e)=>{if(e.Uri.StartsWith("https://itemci.com"))blocked.TrySetResult(e.Cancel);};
                view.CoreWebView2.NavigationStarting+=check;
                view.CoreWebView2.Navigate("https://itemci.com/");
                if(!await blocked.Task.WaitAsync(TimeSpan.FromSeconds(5)))throw new IOException("Local UI allowed an external navigation.");
                view.CoreWebView2.NavigationStarting-=check;
                var output=Path.Combine(Environment.CurrentDirectory,"test-results");Directory.CreateDirectory(output);
                using var capture=File.Create(Path.Combine(output,$"quick-listing-windows-{cycle+1}.png"));
                await view.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,capture);
                if(capture.Length<10000)throw new IOException("Rendered popup capture was empty.");
            }
            catch(Exception ex){_failure=ex;Console.Error.WriteLine(ex);}
            finally{popup.Close();}
        };
        popup.ShowDialog(owner);
        if(_failure is not null)throw _failure;
    }
    private static async Task AssertEventually(WebView2 view,string script)
    {
        for(var attempt=0;attempt<50;attempt++)
        {
            if(await view.CoreWebView2.ExecuteScriptAsync(script)=="true")return;
            await Task.Delay(100);
        }
        throw new IOException("Popup DOM or mapped image failed to render.");
    }
}
