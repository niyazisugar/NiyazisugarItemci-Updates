using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace NiyazisugarItemci;
public sealed partial class MainForm
{
    private bool _quickPickerOpen;
    private readonly Dictionary<string, BrowserTabState> _quickDrafts = new();
    private readonly Label _quickFormStatus = new() { Dock=DockStyle.Bottom,Height=28,BackColor=Color.FromArgb(26,29,36),ForeColor=Color.FromArgb(150,157,170),Padding=new Padding(12,5,0,0),Visible=false };
    private bool _quickStatusInstalled;

    private async Task ShowQuickListingPickerAsync()
    {
        if(_quickPickerOpen) return;
        _quickPickerOpen=true;
        try
        {
            if(!_webReady) { MessageBox.Show("Itemci oturumu hazırlanıyor. Biraz sonra tekrar dene.","Hızlı İlan Ekle");return; }
            if(_marketWebReady) await RefreshMarketServersAsync();
            MarketServerInfo? selected=null;
            using var picker=new Form { Text="Hızlı İlan Ekle",Size=new Size(620,510),MinimumSize=new Size(560,440),StartPosition=FormStartPosition.CenterParent,BackColor=Bg,ForeColor=TextMain,Font=new Font("Segoe UI",10),MinimizeBox=false,MaximizeBox=false,ShowInTaskbar=false,Padding=new Padding(22) };
            var heading=new Label {Text="Sunucunu seç",Dock=DockStyle.Top,Height=45,Font=new Font("Segoe UI",22,FontStyle.Bold),ForeColor=Accent};
            var description=new Label {Text="Analiz sistemindeki sunucular · Seçince ilan ekleme formu açılır",Dock=DockStyle.Top,Height=38,ForeColor=TextMuted,Font=new Font("Segoe UI",9)};
            var search=new TextBox {Dock=DockStyle.Top,PlaceholderText="Sunucu ara…",Font=new Font("Segoe UI",12),BackColor=Card,ForeColor=TextMain,BorderStyle=BorderStyle.FixedSingle};
            var list=new FlowLayoutPanel {Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(0,12,0,0)};
            void Draw()
            {
                foreach(Control old in list.Controls.Cast<Control>().ToArray()) old.Dispose();
                var query=search.Text.Trim();
                foreach(var server in _marketServers.OrderByDescending(s=>s.Id==_settings.LastQuickListingServer).ThenByDescending(s=>s.IsDefault).ThenBy(s=>s.Name))
                {
                    if(query.Length>0&&!server.Name.Contains(query,StringComparison.CurrentCultureIgnoreCase))continue;
                    var button=new Button {Text=server.Name+(server.Id==_settings.LastQuickListingServer?"  · Son kullanılan":""),Width=Math.Max(300,list.ClientSize.Width-25),Height=58,TextAlign=ContentAlignment.MiddleLeft,Padding=new Padding(16,0,0,0),Font=new Font("Segoe UI",11,FontStyle.Bold),FlatStyle=FlatStyle.Flat,BackColor=Card,ForeColor=TextMain,Margin=new Padding(0,0,0,9),Cursor=Cursors.Hand,AutoEllipsis=true};
                    button.Click+=(_,_)=>{selected=server;picker.DialogResult=DialogResult.OK;picker.Close();};
                    list.Controls.Add(button);
                }
                if(list.Controls.Count==0) list.Controls.Add(new Label {Text="Bu aramada sunucu bulunamadı.",AutoSize=true,ForeColor=TextMuted});
            }
            search.TextChanged+=(_,_)=>Draw();
            list.SizeChanged+=(_,_)=>{foreach(Control row in list.Controls)row.Width=Math.Max(300,list.ClientSize.Width-25);};
            picker.Controls.Add(list);picker.Controls.Add(search);picker.Controls.Add(description);picker.Controls.Add(heading);list.BringToFront();
            PremiumTheme.Apply(picker);Draw();
            if(picker.ShowDialog(this)!=DialogResult.OK||selected is null)return;
            _settings.LastQuickListingServer=selected.Id;_settings.Save();
            await OpenQuickListingAsync(selected);
        }
        finally {_quickPickerOpen=false;}
    }

    private async Task OpenQuickListingAsync(MarketServerInfo server)
    {
        ShowPanel(_browserPanel);
        if(!_quickStatusInstalled)
        {
            _browserPanel.Controls.Add(_quickFormStatus);
            _browserPanel.Controls.SetChildIndex(_quickFormStatus,1);
            _quickStatusInstalled=true;
        }
        _quickFormStatus.Visible=true;
        if(_quickDrafts.TryGetValue(server.Id,out var existing)&&_browserTabs.Contains(existing)&&BrowserTabRules.Family(TabLocation(existing))=="create")
        {
            SelectBrowserTab(existing);
            _quickFormStatus.Text=server.Name+" · Mevcut ilan taslağın açıldı.";
            return;
        }
        // Each explicitly chosen server may retain its own draft; another server
        // cannot silently overwrite a form already associated with this picker.
        bool separate=_quickDrafts.Values.Any(draftTab=>_browserTabs.Contains(draftTab)&&BrowserTabRules.Family(TabLocation(draftTab))=="create");
        var tab=await OpenSmartBrowserTabAsync("https://itemci.com/panel/ilan-ekle","İlan · "+server.Name,true,forceNew:separate);
        if(tab is null) {_quickFormStatus.Text="İlan formu açılamadı. Tekrar dene.";return;}
        _quickDrafts[server.Id]=tab;
        _quickFormStatus.Text=server.Name+" · İlan formu hazırlanıyor…";
        try
        {
            var core=tab.WebView.CoreWebView2;
            var token=Guid.NewGuid().ToString("N");
            var completion=new TaskCompletionSource<QuickListingResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            void Message(object? sender,CoreWebView2WebMessageReceivedEventArgs e)
            {
                try
                {
                    using var json=JsonDocument.Parse(e.WebMessageAsJson);var root=json.RootElement;
                    if(!root.TryGetProperty("quickToken",out var t)||t.GetString()!=token)return;
                    if(root.TryGetProperty("error",out var error))completion.TrySetException(new IOException(error.GetString()));
                    else completion.TrySetResult(root.GetProperty("result").Deserialize<QuickListingResult>()??new());
                }
                catch(Exception ex){completion.TrySetException(ex);}
            }
            async Task Run()
            {
                var script=await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"QuickListing.js"));
                await core.ExecuteScriptAsync(script);
                await core.ExecuteScriptAsync($"window.__NiyaziQuickListing({JsonSerializer.Serialize(server.Name)}).then(result=>chrome.webview.postMessage({{quickToken:'{token}',result}})).catch(e=>chrome.webview.postMessage({{quickToken:'{token}',error:String(e.message||e)}}));");
            }
            var loaded=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            void Navigated(object? sender,CoreWebView2NavigationCompletedEventArgs e)=>loaded.TrySetResult(e.IsSuccess);
            core.WebMessageReceived+=Message;core.NavigationCompleted+=Navigated;
            try
            {
                // Wait for the new route to finish loading; reused ready forms
                // can run immediately. Authentication redirects are reported by JS.
                var ready=await core.ExecuteScriptAsync("document.readyState === 'complete' && location.pathname.startsWith('/panel/ilan-ekle')");
                if(ready!="true") await loaded.Task.WaitAsync(TimeSpan.FromSeconds(25));
                await Run();
                var result=await completion.Task.WaitAsync(TimeSpan.FromSeconds(35));
                _quickFormStatus.Text=result.message;
                _quickFormStatus.ForeColor=result.ok?Green:Accent;
            }
            finally{core.WebMessageReceived-=Message;core.NavigationCompleted-=Navigated;}
        }
        catch{_quickFormStatus.Text=server.Name+" · Form açıldı; sunucu ve kategori seçimini kontrol et.";_quickFormStatus.ForeColor=Accent;}
    }
    private sealed class QuickListingResult
    {
        public bool ok {get;set;}
        public string message {get;set;}="";
        public bool draft {get;set;}
    }
}
