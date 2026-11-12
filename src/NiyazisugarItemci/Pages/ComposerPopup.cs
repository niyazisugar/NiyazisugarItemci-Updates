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

        FormClosed+=(_,_)=>{_composerCooldownTimer.Dispose();_composerPrewarmTimer.Dispose();DisposeComposerHost();};
        // Pencere ilk tıklamada sıfırdan kurulmasın: uygulama açıldıktan kısa süre sonra arka planda hazırlanır.
        _composerPrewarmTimer.Tick+=async(_,_)=>
        {
            if(_browserEnvironment is null||!_webReady){if(++_composerPrewarmAttempts>=12)_composerPrewarmTimer.Stop();return;}
            _composerPrewarmTimer.Stop();
            await PrewarmComposerHostAsync();
        };
        _composerPrewarmTimer.Start();
    }

    // Tek, yeniden kullanılan pencere. Kapatınca gizlenir; WebView2 ve sayfa hazır kalır.
    private sealed class ComposerHostForm : Form
    {
        internal bool Quiet;
        protected override bool ShowWithoutActivation=>Quiet;
    }
    private ComposerHostForm? _composerHost;
    private WebView2? _composerHostView;
    private ComposerSurface? _composerSurface;
    private Panel? _composerLoadingPanel;
    private Label? _composerLoadingText;
    private Button? _composerRetry,_composerCopyError;
    private CancellationTokenSource? _composerLifetime;
    private bool _composerSurfaceLoaded,_composerSurfaceStarting;
    private Task? _composerPrewarmTask;
    private int _composerPrewarmAttempts;
    private readonly System.Windows.Forms.Timer _composerPrewarmTimer=new(){Interval=5000};

    private void EnsureComposerHost()
    {
        if(_composerHost is not null&&!_composerHost.IsDisposed)return;
        var area=Screen.FromControl(this).WorkingArea;
        var host=new ComposerHostForm
        {
            Text="Hızlı İlan Ekle",Size=new Size(Math.Min(1120,area.Width-32),Math.Min(850,area.Height-32)),
            MinimumSize=new Size(Math.Min(440,area.Width),Math.Min(400,area.Height)),
            StartPosition=FormStartPosition.Manual,BackColor=ReferenceUi.Surface,
            ShowInTaskbar=false,MinimizeBox=false,MaximizeBox=true,Padding=Padding.Empty
        };
        var view=new WebView2{Dock=DockStyle.Fill,DefaultBackgroundColor=ReferenceUi.Surface,AllowExternalDrop=false};
        host.Controls.Add(view);
        host.FormClosing+=(_,e)=>
        {
            if(e.CloseReason==CloseReason.UserClosing&&(_composerBusy||_composerAddingServer)){e.Cancel=true;_composerStatus.Text="İşlem devam ediyor. Sonuç geldikten sonra pencereyi kapatabilirsin.";_=PushComposerStateAsync();}
        };
        var loading=new Panel{Dock=DockStyle.Fill,BackColor=ReferenceUi.Surface,Padding=new Padding(36)};
        var loadingText=new Label{Dock=DockStyle.Top,Height=160,ForeColor=Color.FromArgb(217,231,238),Font=new Font("Segoe UI",12),Text="Hızlı İlan Ekle hazırlanıyor…"};
        var retry=new Button{Text="Yeniden Dene",Width=150,Height=42,Top=210,Left=36,Visible=false,BackColor=Color.FromArgb(46,219,211),ForeColor=Color.FromArgb(9,39,42),FlatStyle=FlatStyle.Flat};
        var copyError=new Button{Text="Hata Bilgisini Kopyala",Width=200,Height=42,Top=264,Left=36,Visible=false,BackColor=Card,ForeColor=TextMain,FlatStyle=FlatStyle.Flat};
        copyError.Click+=(_,_)=>Clipboard.SetText(loadingText.Text);
        loading.Controls.Add(loadingText);loading.Controls.Add(retry);loading.Controls.Add(copyError);
        host.Controls.Add(loading);loading.BringToFront();
        _composerLifetime=new CancellationTokenSource();
        _composerHost=host;_composerHostView=view;_composerLoadingPanel=loading;_composerLoadingText=loadingText;_composerRetry=retry;_composerCopyError=copyError;
        _composerSurface=new ComposerSurface(view,async text=>
        {
            using var json=JsonDocument.Parse(text);
            await HandleComposerMessageAsync(json.RootElement.Clone());
        },ComposerSurfaceFailed);
        retry.Click+=async(_,_)=>await LoadComposerSurfaceAsync();
        // İlk gösterimde ya da önceki yükleme başarısız olduysa açılınca yükle.
        host.VisibleChanged+=async(_,_)=>{if(host.Visible&&!host.Quiet&&!_composerSurfaceLoaded)await LoadComposerSurfaceAsync();};
        PremiumTheme.Apply(host);
    }

    private void ComposerSurfaceFailed(string message)
    {
        if(_composerHost is null||_composerHost.IsDisposed||_composerLifetime?.IsCancellationRequested==true)return;
        _composerSurfaceLoaded=false;_composerViewReady=false;
        _composerLoadingText!.Text="Hızlı İlan Ekle açılamadı.\r\n\r\n"+message;
        _composerRetry!.Visible=true;_composerCopyError!.Visible=true;_composerLoadingPanel!.Visible=true;_composerLoadingPanel.BringToFront();
        try{File.AppendAllText(Path.Combine(Path.GetDirectoryName(ComposerPopupStatePath)!,"composer-view.log"),DateTime.UtcNow.ToString("O")+" "+message+Environment.NewLine);}catch{}
    }

    private async Task LoadComposerSurfaceAsync()
    {
        if(_composerSurfaceStarting||_composerSurface is null||_composerLifetime is null||_composerLifetime.IsCancellationRequested)return;
        _composerSurfaceStarting=true;_composerRetry!.Visible=false;_composerCopyError!.Visible=false;
        _composerLoadingText!.Text="Hızlı İlan Ekle hazırlanıyor…";_composerLoadingPanel!.Visible=true;_composerLoadingPanel.BringToFront();
        try
        {
            await _composerSurface.LoadAsync(_browserEnvironment,AppContext.BaseDirectory,ComposerImagesFolder,_composerLifetime.Token);
            _composerSurfaceLoaded=true;
            _composerHostView!.Visible=true;_composerLoadingPanel.Visible=false;_composerHostView.BringToFront();
            if(_composerPopup is not null){_composerViewReady=true;await PushComposerStateAsync(true);}
            else _composerViewReady=false;
        }
        catch(OperationCanceledException)when(_composerLifetime.IsCancellationRequested){}
        catch(TimeoutException){ComposerSurfaceFailed("Arayüz zamanında yüklenemedi. Yeniden Dene ile tekrar açabilirsin.");}
        catch(Exception ex){ComposerSurfaceFailed(ex.Message);}
        finally{_composerSurfaceStarting=false;}
    }

    // Ekran dışında, odağı çalmadan bir kez göster, sayfayı yükle ve gizle.
    private Task PrewarmComposerHostAsync()
    {
        if(_composerPrewarmTask is not null)return _composerPrewarmTask;
        return _composerPrewarmTask=PrewarmCoreAsync();
        async Task PrewarmCoreAsync()
        {
            try
            {
                if(IsDisposed||Disposing||_composerPopup is not null||_composerPopupOpening)return;
                Directory.CreateDirectory(ComposerImagesFolder);
                EnsureComposerHost();
                var host=_composerHost!;
                if(_composerSurfaceLoaded||host.Visible)return;
                host.Quiet=true;host.Location=new Point(-32000,-32000);
                host.Show();
                await LoadComposerSurfaceAsync();
            }
            catch(Exception ex){try{File.AppendAllText(Path.Combine(Path.GetDirectoryName(ComposerPopupStatePath)!,"composer-view.log"),DateTime.UtcNow.ToString("O")+" ön hazırlık: "+ex.Message+Environment.NewLine);}catch{}}
            finally
            {
                if(_composerHost is not null&&!_composerHost.IsDisposed&&_composerHost.Quiet){_composerHost.Hide();_composerHost.Quiet=false;}
                if(_composerPopup is null)_composerViewReady=false;
            }
        }
    }

    private void DisposeComposerHost()
    {
        try{_composerLifetime?.Cancel();}catch{}
        _composerSurface?.Dispose();_composerSurface=null;
        if(_composerHost is not null&&!_composerHost.IsDisposed)_composerHost.Dispose();
        _composerHost=null;_composerHostView=null;_composerSurfaceLoaded=false;
    }

    private async Task ShowListingComposerPopupAsync()
    {
        if(_composerPopup is not null){_composerPopup.Activate();return;}
        if(_composerPopupOpening)return;
        _composerPopupOpening=true;
        try
        {
            // Arka plandaki ön hazırlık sürüyorsa onu bekle; ikinci bir pencere kurma.
            if(_composerPrewarmTask is not null&&!_composerPrewarmTask.IsCompleted){try{await _composerPrewarmTask;}catch{}}
            // Use the already synchronized server list. A background market scan
            // must never delay opening the local composer.
            RefreshComposerServerChoices();
            _composerCooldownTimer.Start();
            Directory.CreateDirectory(ComposerImagesFolder);
            EnsureComposerHost();
            var popup=_composerHost!;
            var area=Screen.FromControl(this).WorkingArea;
            popup.Quiet=false;
            popup.MinimumSize=new Size(Math.Min(440,area.Width),Math.Min(400,area.Height));
            if(popup.Width>area.Width-32||popup.Height>area.Height-32)popup.Size=new Size(Math.Min(popup.Width,area.Width-32),Math.Min(popup.Height,area.Height-32));
            var owner=Bounds;
            popup.Location=new Point(
                Math.Clamp(owner.Left+(owner.Width-popup.Width)/2,area.Left,Math.Max(area.Left,area.Right-popup.Width)),
                Math.Clamp(owner.Top+(owner.Height-popup.Height)/2,area.Top,Math.Max(area.Top,area.Bottom-popup.Height)));
            _composerPopup=popup;_composerView=_composerHostView;
            if(_composerSurfaceLoaded)
            {
                // Hazır sayfa: yalnızca güncel taslağı ve sunucu listesini gönder.
                _composerViewReady=true;_composerLoadingPanel!.Visible=false;_composerHostView!.BringToFront();
                await PushComposerStateAsync(true);
            }
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
            case "findLink":
                if(_composerLastPublished is null||_composerResolvingLink)break;
                _composerResolvingLink=true;await PushComposerStateAsync();
                var found=await ResolveComposerPublishedLinkAsync(null,_composerLastPublished);
                _composerResolvingLink=false;
                if(IsComposerListingLink(found)){_composerLastLink=found;SaveComposerPopupState();ComposerToast("İlan linki bulundu.");}
                else ComposerToast("Link henüz görünmüyor; biraz sonra tekrar dene ya da İlanlarım bölümüne bak.");
                await PushComposerStateAsync();break;
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
    private ComposerDraft? _composerLastPublished;
    // Yayından sonra mağaza sayfasında yeni ilanı bulur. Önceki liste alınamadıysa da
    // aynı başlık ve fiyattaki en yeni ilanı kullanır (mağaza sayfası en yeniden sıralı).
    private async Task<string> ResolveComposerPublishedLinkAsync(ComposerStoreSnapshot? before,ComposerDraft draft)
    {
        var storeUrl=before?.url;
        if(string.IsNullOrWhiteSpace(storeUrl)){try{storeUrl=await DiscoverLoggedInStoreUrlAsync();}catch{}}
        if(string.IsNullOrWhiteSpace(storeUrl))return "";
        for(var attempt=0;attempt<6;attempt++)
        {
            try
            {
                var html=await FetchItemciHtmlAsync(storeUrl).WaitAsync(TimeSpan.FromSeconds(8));
                var same=ParseCurrentListings(html,storeUrl).Where(l=>IsComposerListingLink(l.Url)&&
                    NormalizeHtmlText(l.Title).Equals(NormalizeHtmlText(draft.title),StringComparison.OrdinalIgnoreCase)&&ParseTurkishMoney(Regex.Replace(l.PriceText,@"[^0-9.,]",""))==draft.price).ToList();
                var fresh=before is null?same:same.Where(l=>!before.urls.Contains(l.Url)).ToList();
                if(fresh.Count>=1)return fresh[0].Url;
            }
            catch{ }
            if(attempt<5)await Task.Delay(2000);
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
