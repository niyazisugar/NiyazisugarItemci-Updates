using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
namespace NiyazisugarItemci;
public sealed partial class MainForm
{
    private Form? _composerPopup;
    private WebView2? _composerView;
    private bool _composerPopupOpening;
    private bool _composerViewReady;
    private DateTime _composerCooldownUntilUtc;
    private string _composerLastLink="";
    private string _composerStatusKind="idle";
    private bool _composerResolvingLink;
    private bool _composerAddingServer;
    private bool _composerTickHadCountdown;
    private readonly System.Windows.Forms.Timer _composerCooldownTimer=new(){Interval=1000};
    private string ComposerImagesFolder=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"NiyazisugarItemci","ListingImages");
    private string ComposerPopupStatePath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"NiyazisugarItemci","composer-state.json");

    private void InstallComposerPopupExtras()
    {
        try
        {
            if(File.Exists(ComposerPopupStatePath))
            {
                var state=JsonSerializer.Deserialize<ComposerPopupState>(File.ReadAllText(ComposerPopupStatePath));
                if(state is not null){_composerCooldownUntilUtc=state.untilUtc;if(IsComposerListingLink(state.link))_composerLastLink=state.link;}
            }
        }
        catch { }
        _composerCooldownTimer.Tick+=(_,_)=>UpdateComposerPublishAvailability();

        FormClosed+=(_,_)=>_composerCooldownTimer.Dispose();
    }

    private async Task ShowListingComposerPopupAsync()
    {
        if(_composerPopup is not null){_composerPopup.Activate();return;}
        if(_composerPopupOpening)return;
        _composerPopupOpening=true;
        try
        {
            // Use the already synchronized server list. A background market scan
            // must never delay opening the local composer.
            RefreshComposerServerChoices();
            _composerCooldownTimer.Start();
            Directory.CreateDirectory(ComposerImagesFolder);
            var area=Screen.FromControl(this).WorkingArea;
            using var popup=new Form
            {
                Text="Hızlı İlan Ekle",Size=new Size(Math.Min(1120,area.Width-32),Math.Min(850,area.Height-32)),
                MinimumSize=new Size(Math.Min(440,area.Width),Math.Min(400,area.Height)),
                StartPosition=FormStartPosition.CenterParent,BackColor=Color.FromArgb(16,25,31),
                ShowInTaskbar=false,MinimizeBox=false,MaximizeBox=true,Padding=Padding.Empty
            };
            using var view=new WebView2{Dock=DockStyle.Fill,DefaultBackgroundColor=Color.FromArgb(16,25,31),AllowExternalDrop=false};
            _composerPopup=popup;_composerView=view;_composerViewReady=false;
            popup.Controls.Add(view);
            popup.FormClosing+=(_,e)=>
            {
                if(_composerBusy||_composerAddingServer){e.Cancel=true;_composerStatus.Text="İşlem devam ediyor. Sonuç geldikten sonra pencereyi kapatabilirsin.";_=PushComposerStateAsync();}
            };
            var loading=new Panel{Dock=DockStyle.Fill,BackColor=Color.FromArgb(16,25,31),Padding=new Padding(36)};
            var loadingText=new Label{Dock=DockStyle.Top,Height=160,ForeColor=Color.FromArgb(217,231,238),Font=new Font("Segoe UI",12),Text="Hızlı İlan Ekle hazırlanıyor…"};
            var retry=new Button{Text="Yeniden Dene",Width=150,Height=42,Top=210,Left=36,Visible=false,BackColor=Color.FromArgb(46,219,211),ForeColor=Color.FromArgb(9,39,42),FlatStyle=FlatStyle.Flat};
            var copyError=new Button{Text="Hata Bilgisini Kopyala",Width=200,Height=42,Top=264,Left=36,Visible=false,BackColor=Card,ForeColor=TextMain,FlatStyle=FlatStyle.Flat};
            copyError.Click+=(_,_)=>Clipboard.SetText(loadingText.Text);
            loading.Controls.Add(loadingText);loading.Controls.Add(retry);loading.Controls.Add(copyError);
            popup.Controls.Add(loading);loading.BringToFront();
            using var lifetime=new CancellationTokenSource();
            popup.FormClosed+=(_,_)=>lifetime.Cancel();
            void Failed(string message)
            {
                if(popup.IsDisposed||lifetime.IsCancellationRequested)return;
                _composerViewReady=false;loadingText.Text="Hızlı İlan Ekle açılamadı.\r\n\r\n"+message;
                retry.Visible=true;copyError.Visible=true;loading.Visible=true;loading.BringToFront();
                try{File.AppendAllText(Path.Combine(Path.GetDirectoryName(ComposerPopupStatePath)!,"composer-view.log"),DateTime.UtcNow.ToString("O")+" "+message+Environment.NewLine);}catch{}
            }
            using var surface=new ComposerSurface(view,async text=>
            {
                using var json=JsonDocument.Parse(text);
                await HandleComposerMessageAsync(json.RootElement.Clone());
            },Failed);
            var starting=false;
            async Task LoadSurfaceAsync()
            {
                if(starting||lifetime.IsCancellationRequested)return;
                starting=true;_composerViewReady=false;retry.Visible=false;copyError.Visible=false;
                loadingText.Text="Hızlı İlan Ekle hazırlanıyor…";loading.Visible=true;loading.BringToFront();
                try
                {
                    await surface.LoadAsync(_browserEnvironment,AppContext.BaseDirectory,ComposerImagesFolder,lifetime.Token);
                    if(!lifetime.IsCancellationRequested){view.Visible=true;loading.Visible=false;view.BringToFront();await PushComposerStateAsync(true);}
                }
                catch(OperationCanceledException)when(lifetime.IsCancellationRequested){}
                catch(TimeoutException){Failed("Arayüz zamanında yüklenemedi. Yeniden Dene ile tekrar açabilirsin.");}
                catch(Exception ex){Failed(ex.Message);}
                finally{starting=false;}
            }
            retry.Click+=async(_,_)=>await LoadSurfaceAsync();
            popup.Shown+=async(_,_)=>await LoadSurfaceAsync();
            PremiumTheme.Apply(popup);
            await ComposerModal.ShowAsync(this,popup);
        }
        catch(Exception ex){_composerStatus.Text="Hızlı ilan penceresi açılamadı: "+ex.Message;MessageBox.Show(this,_composerStatus.Text,"Hızlı İlan Ekle",MessageBoxButtons.OK,MessageBoxIcon.Information);}
        finally
        {
            SaveComposerDrafts();_composerCooldownTimer.Stop();_composerTickHadCountdown=false;_composerViewReady=false;_composerView=null;_composerPopup=null;_composerPopupOpening=false;
        }
    }

    private void RefreshComposerServerChoices()
    {
        var id=(_composerServer.SelectedItem as MarketServerInfo)?.Id??_settings.LastQuickListingServer;
        _composerLoading=true;_composerServer.Items.Clear();
        foreach(var server in _marketServers)_composerServer.Items.Add(server);
        _composerServer.DisplayMember="Name";
        _composerServer.SelectedItem=_marketServers.FirstOrDefault(s=>s.Id==id)??_marketServers.FirstOrDefault();
        _composerLoading=false;LoadComposerServerDraft();
    }

    private async Task HandleComposerMessageAsync(JsonElement message)
    {
        var action=message.GetProperty("action").GetString();
        if(action=="ready"){_composerViewReady=true;await PushComposerStateAsync(true);return;}
        if(_composerBusy||_composerAddingServer){await PushComposerStateAsync();return;}
        if(message.TryGetProperty("draft",out var draft))ApplyComposerWebDraft(draft);
        switch(action)
        {
            case "draft":QueueComposerSave();break;
            case "server":
                SaveComposerDrafts();
                var id=message.GetProperty("serverId").GetString();
                _composerServer.SelectedItem=_marketServers.FirstOrDefault(s=>s.Id==id);
                if(_composerServer.SelectedItem is MarketServerInfo selected){_settings.LastQuickListingServer=selected.Id;SaveSettings();}
                await PushComposerStateAsync(true);break;
            case "save":
                _composerStatusKind="idle";
                if(SaveComposerDrafts())_composerStatus.Text="Taslak kaydedildi.";
                await PushComposerStateAsync();break;
            case "unlockDraft":
                _composerUncertain=false;_composerStatusKind="idle";
                _composerStatus.Text="Taslak kilidi kaldırıldı. Yayınlamadan önce önceki ilanın İlanlarım bölümünde olmadığını kontrol et.";
                SaveComposerDrafts();await PushComposerStateAsync();break;
            case "newDraft":
                _composerLoading=true;_composerTitle.Clear();_composerPrice.Value=1;_composerImages.Items.Clear();
                _composerDescription.Text=DefaultComposerDescription;_composerStock.Checked=true;_composerOrders.Checked=true;
                _composerDelivery.SelectedItem=1;_composerValidity.SelectedItem=15;_composerLoading=false;_composerUncertain=false;
                _composerStatusKind="idle";_composerStatus.Text="Yeni taslak hazır. Önceki yayın sonucu belirsizse İlanlarım bölümünü kontrol et.";
                SaveComposerDrafts();await PushComposerStateAsync(true);break;
            case "publish":await PublishComposerAsync();break;
            case "chooseImages":SelectComposerPictures(true);await PushComposerStateAsync(true,true);break;
            case "importImages":SelectComposerPictures(false);await PushComposerStateAsync(true,true);break;
            case "gallery":await PushComposerStateAsync(false,true);break;
            case "addServer":await AddComposerServerFromWebAsync(message.GetProperty("name").GetString()??"",message.GetProperty("url").GetString()??"");break;
            case "copyLink":if(IsComposerListingLink(_composerLastLink)){Clipboard.SetText(_composerLastLink);ComposerToast("İlan linki kopyalandı.");}break;
            case "openLink":
                if(IsComposerListingLink(_composerLastLink))
                {
                    var link=_composerLastLink;_composerPopup?.Close();ShowPanel(_browserPanel);
                    await OpenOrActivateBrowserTabAsync(link,"İlan");
                }
                break;
            case "listings":_composerPopup?.Close();ShowPanel(_listingsPanel);await RefreshCurrentListingsAsync(true);break;
            case "copyStatus":Clipboard.SetText(_composerStatus.Text);ComposerToast("Durum bilgisi kopyalandı.");break;
            case "close":_composerPopup?.Close();break;
        }
    }

    private void ApplyComposerWebDraft(JsonElement element)
    {
        var draft=element.Deserialize<ComposerWebDraft>()??throw new IOException("Taslak okunamadı.");
        if(draft.title.Length>150||draft.description.Length>1200||draft.price<1||draft.price>999999999||decimal.Truncate(draft.price)!=draft.price||
           !new[]{1,2,6,12}.Contains(draft.delivery)||!new[]{15,30}.Contains(draft.validity)||draft.images.Count>4)
            throw new IOException("Başlık, fiyat veya görsel sınırlarını kontrol et.");
        var paths=draft.images.Select(ResolveComposerImageId).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if(paths.Any(p=>p is null))throw new IOException("Seçilen görsel bulunamadı. Hazır Görseller bölümünden yeniden seçebilirsin.");
        _composerLoading=true;
        try
        {
            _composerTitle.Text=draft.title;_composerDescription.Text=draft.description;_composerPrice.Value=draft.price;
            _composerDelivery.SelectedItem=draft.delivery;_composerValidity.SelectedItem=draft.validity;
            _composerStock.Checked=draft.stock;_composerOrders.Checked=draft.orders;_composerImages.Items.Clear();
            foreach(var path in paths)_composerImages.Items.Add(path!);
        }
        finally{_composerLoading=false;}
        UpdateComposerNet();
    }

    private async Task PushComposerStateAsync(bool includeDraft=false,bool includeGallery=false,object? serverResult=null)
    {
        var view=_composerView;
        if(!_composerViewReady||view?.CoreWebView2 is null||view.IsDisposed)return;
        var state=new Dictionary<string,object?>
        {
            ["busy"]=_composerBusy,["addingServer"]=_composerAddingServer,["uncertain"]=_composerUncertain,
            ["remaining"]=Math.Max(0,(int)Math.Ceiling((_composerCooldownUntilUtc-DateTime.UtcNow).TotalSeconds)),
            ["url"]=IsComposerListingLink(_composerLastLink)?_composerLastLink:"",
            ["kind"]=_composerStatusKind,["message"]=_composerStatus.Text,["resolvingLink"]=_composerResolvingLink
        };
        if(includeDraft)
        {
            var draft=CurrentComposerDraft();
            state["servers"]=_marketServers.Select(s=>new{id=s.Id,name=s.Name}).ToArray();
            state["serverId"]=(_composerServer.SelectedItem as MarketServerInfo)?.Id;
            state["draft"]=new{draft.title,draft.description,draft.price,draft.delivery,draft.validity,draft.stock,draft.orders,
                images=draft.paths.Where(File.Exists).Select(ComposerImageDescriptor).ToArray()};
        }
        if(includeGallery){Directory.CreateDirectory(ComposerImagesFolder);state["gallery"]=Directory.GetFiles(ComposerImagesFolder).Where(IsComposerImageFile).OrderByDescending(File.GetLastWriteTimeUtc).Select(ComposerImageDescriptor).ToArray();}
        if(serverResult is not null)state["serverResult"]=serverResult;
        try{await view.CoreWebView2.ExecuteScriptAsync($"window.__composerReceive?.({JsonSerializer.Serialize(state)})");}
        catch{ /* The owner may have closed the popup while the script was queued. */ }
    }

    private void ComposerToast(string message){try{_composerView?.CoreWebView2?.PostWebMessageAsJson(JsonSerializer.Serialize(new{toast=message}));}catch{}}
    private static bool IsComposerImageFile(string path)=>new[]{".png",".jpg",".jpeg"}.Contains(Path.GetExtension(path).ToLowerInvariant());
    private object ComposerImageDescriptor(string path)
    {
        var id=Path.GetFileName(path);
        var name=Regex.Replace(Path.GetFileNameWithoutExtension(path),"-[A-F0-9]{16}$","");
        return new{id,name,url="https://composer-images.local/"+Uri.EscapeDataString(id)};
    }
    private string? ResolveComposerImageId(string id)
    {
        if(string.IsNullOrWhiteSpace(id)||Path.GetFileName(id)!=id||id.Contains('/')||id.Contains('\\'))return null;
        var path=Path.Combine(ComposerImagesFolder,id);
        return File.Exists(path)&&IsComposerImageFile(path)&&new FileInfo(path).Length<=5*1024*1024?path:null;
    }
    private string SaveComposerLibraryImage(string path)
    {
        if(!File.Exists(path))throw new IOException("Görsel dosyası bulunamadı.");
        var file=new FileInfo(path);
        if(file.Length>5*1024*1024||!IsComposerImageFile(path))throw new IOException("PNG/JPG ve en fazla 5 MB görsel seçmelisin.");
        // Validate decoded format too, so a renamed arbitrary file cannot enter the gallery.
        using(var stream=File.OpenRead(path))using(var image=Image.FromStream(stream))
        {
            if(image.RawFormat.Guid!=System.Drawing.Imaging.ImageFormat.Png.Guid&&image.RawFormat.Guid!=System.Drawing.Imaging.ImageFormat.Jpeg.Guid)
                throw new IOException("Dosya geçerli bir PNG veya JPG görseli değil.");
        }
        Directory.CreateDirectory(ComposerImagesFolder);
        if(string.Equals(Path.GetDirectoryName(Path.GetFullPath(path)),Path.GetFullPath(ComposerImagesFolder),StringComparison.OrdinalIgnoreCase))return Path.GetFullPath(path);
        using var input=File.OpenRead(path);var hash=Convert.ToHexString(SHA256.HashData(input))[..16];
        var name=Regex.Replace(Path.GetFileNameWithoutExtension(path),@"[^\p{L}\p{N} _-]","");if(name.Length>60)name=name[..60];if(name.Length==0)name="Görsel";
        var destination=Path.Combine(ComposerImagesFolder,name+"-"+hash+file.Extension.ToLowerInvariant());
        if(!File.Exists(destination))File.Copy(path,destination);
        return destination;
    }
    private static bool IsComposerListingLink(string value)=>Uri.TryCreate(value,UriKind.Absolute,out var uri)&&uri.Scheme=="https"&&uri.Host=="itemci.com"&&uri.UserInfo.Length==0&&uri.IsDefaultPort&&uri.AbsolutePath.StartsWith("/product/");
    private void SaveComposerPopupState()
    {
        try{Directory.CreateDirectory(Path.GetDirectoryName(ComposerPopupStatePath)!);File.WriteAllText(ComposerPopupStatePath+".tmp",JsonSerializer.Serialize(new ComposerPopupState{untilUtc=_composerCooldownUntilUtc,link=_composerLastLink}));File.Move(ComposerPopupStatePath+".tmp",ComposerPopupStatePath,true);}
        catch{_composerStatus.Text="Yayın sonucu alındı; bekleme süresi bilgisayara kaydedilemedi.";}
    }
    private void UpdateComposerPublishAvailability()
    {
        if(_composerPublish is not null)_composerPublish.Enabled=!_composerBusy&&!_composerUncertain&&DateTime.UtcNow>=_composerCooldownUntilUtc;
        var countdown=DateTime.UtcNow<_composerCooldownUntilUtc;
        if(countdown||_composerTickHadCountdown)_=PushComposerStateAsync();
        _composerTickHadCountdown=countdown;
    }
    private void ApplyComposerServerWait(ComposerResult result)
    {
        int seconds=result.retryAfterSeconds;
        var match=Regex.Match(result.message,@"(?<n>\d+)\s*(?<unit>saniye|dakika)",RegexOptions.IgnoreCase);
        if(seconds<=0&&match.Success&&int.TryParse(match.Groups["n"].Value,out var n))seconds=(int)Math.Min(86400,(long)n*(match.Groups["unit"].Value.StartsWith("dakika",StringComparison.OrdinalIgnoreCase)?60:1));
        if(seconds>0){_composerCooldownUntilUtc=DateTime.UtcNow.AddSeconds(Math.Min(seconds,86400));SaveComposerPopupState();}
    }

    private async Task AddComposerServerFromWebAsync(string name,string url)
    {
        _composerAddingServer=true;
        _marketWebView.CoreWebView2?.Resume();
        try
        {
            name=name.Trim();
            if(name.Length==0||name.Length>80||!Uri.TryCreate(url.Trim(),UriKind.Absolute,out var uri)||uri.Scheme!="https"||uri.Host!="itemci.com"||!uri.AbsolutePath.StartsWith("/categories/"))throw new IOException("Sunucu adı ve geçerli Itemci kategori bağlantısı gerekli.");
            if(!_marketAnalyzerInjected)throw new IOException("Analiz sistemi henüz hazır değil. Itemci oturumunun yüklenmesini bekleyip tekrar dene.");
            SaveComposerDrafts();
            await _marketWebView.CoreWebView2.ExecuteScriptAsync($"window.__NiyazisugarSaveQuickServer({JsonSerializer.Serialize(name)},{JsonSerializer.Serialize(uri.ToString())})");
            await RefreshMarketServersAsync();RefreshComposerServerChoices();_composerServer.SelectedItem=_marketServers.FirstOrDefault(s=>s.Name==name);
            if(_composerServer.SelectedItem is not MarketServerInfo selected)throw new IOException("Sunucu kaydı doğrulanamadı. Kategori bağlantısını kontrol et.");
            _settings.LastQuickListingServer=selected.Id;SaveSettings();
            await PushComposerStateAsync(true,false,new{ok=true});
        }
        catch(Exception ex){await PushComposerStateAsync(false,false,new{ok=false,message=ex.Message});}
        finally{_composerAddingServer=false;await SuspendIdleMarketAsync();}
    }

    // Use the owner's observed store, never a fabricated listing URL or somebody else's profile.
    private async Task<ComposerStoreSnapshot?> ReadComposerStoreSnapshotAsync()
    {
        try
        {
            var url=await DiscoverLoggedInStoreUrlAsync();if(string.IsNullOrWhiteSpace(url))return null;
            var html=await FetchItemciHtmlAsync(url).WaitAsync(TimeSpan.FromSeconds(8));if(string.IsNullOrWhiteSpace(html))return null;
            var listings=ParseCurrentListings(html,url);if(listings.Count==0)return null;
            return new ComposerStoreSnapshot{url=url,urls=listings.Select(l=>l.Url).ToHashSet(StringComparer.OrdinalIgnoreCase)};
        }
        catch{return null;}
    }
    private async Task<string> ResolveComposerPublishedLinkAsync(ComposerStoreSnapshot? before,ComposerDraft draft)
    {
        if(before is null)return "";
        for(var attempt=0;attempt<3;attempt++)
        {
            try
            {
                var html=await FetchItemciHtmlAsync(before.url).WaitAsync(TimeSpan.FromSeconds(8));
                var matches=ParseCurrentListings(html,before.url).Where(l=>!before.urls.Contains(l.Url)&&IsComposerListingLink(l.Url)&&
                    NormalizeHtmlText(l.Title).Equals(NormalizeHtmlText(draft.title),StringComparison.OrdinalIgnoreCase)&&ParseTurkishMoney(Regex.Replace(l.PriceText,@"[^0-9.,]",""))==draft.price).ToList();
                if(matches.Count==1)return matches[0].Url;
            }
            catch{ }
            if(attempt<2)await Task.Delay(1200);
        }
        return "";
    }
    private sealed class ComposerStoreSnapshot{public string url="";public HashSet<string> urls=new(StringComparer.OrdinalIgnoreCase);}
    private sealed class ComposerPopupState{public DateTime untilUtc{get;set;}public string link{get;set;}="";}
    private sealed class ComposerWebDraft
    {
        public string title{get;set;}="";public string description{get;set;}="";public decimal price{get;set;}=1;
        public int delivery{get;set;}=1;public int validity{get;set;}=15;public bool stock{get;set;}=true;public bool orders{get;set;}=true;
        public List<string> images{get;set;}=new();
    }
}
