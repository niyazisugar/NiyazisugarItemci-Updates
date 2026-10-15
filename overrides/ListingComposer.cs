using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
namespace NiyazisugarItemci;
public sealed partial class MainForm
{
    private readonly Panel _composerPanel=new(){Dock=DockStyle.Fill,AutoScroll=true,Padding=new Padding(26),Visible=false};
    private readonly ComboBox _composerServer=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=280};
    private readonly TextBox _composerTitle=new(){MaxLength=150,Width=660,PlaceholderText="Örn: 250 KT Yang"};
    private readonly TextBox _composerDescription=new(){Multiline=true,MaxLength=1200,Width=660,Height=135,ScrollBars=ScrollBars.Vertical};
    private readonly NumericUpDown _composerPrice=new(){Minimum=1,Maximum=999999999,Width=170,ThousandsSeparator=true};
    private readonly ComboBox _composerDelivery=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=140};
    private readonly ComboBox _composerValidity=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=140};
    private readonly CheckBox _composerOrders=new(){Text="Bu ilana sipariş verilebilsin",Checked=true,AutoSize=true};
    private readonly CheckBox _composerStock=new(){Text="Stok Yang görselini kullan",Checked=true,AutoSize=true};
    private readonly ListBox _composerImages=new(){Width=660,Height=70};
    private readonly Label _composerNet=new(){AutoSize=true,ForeColor=Color.FromArgb(67,222,169)};
    private readonly Label _composerStatus=new(){Width=760,Height=60,ForeColor=Color.FromArgb(150,157,170)};
    private Button? _composerPublish;
    private WebView2? _composerTransport;
    private bool _composerBusy;
    private bool _composerLoading;
    private bool _composerUncertain;
    private string? _composerDraftServer;
    private readonly Dictionary<string,ComposerDraft> _composerDrafts=new();
    private readonly System.Windows.Forms.Timer _composerSaveTimer=new(){Interval=800};
    private string ComposerDraftPath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"NiyazisugarItemci","listing-drafts.json");

    private void InstallListingComposer()
    {
        // Native draft model retained; the popup uses a local WebView2 view.
        _composerDelivery.Items.AddRange(new object[]{1,2,6,12});_composerDelivery.SelectedItem=1;
        _composerValidity.Items.AddRange(new object[]{15,30});_composerValidity.SelectedItem=15;
        _composerPublish=new Button();
        _composerServer.SelectedIndexChanged+=(_,_)=>LoadComposerServerDraft();
        _composerSaveTimer.Tick+=async(_,_)=>
        {
            _composerSaveTimer.Stop();
            if(_composerBusy)return;
            if(SaveComposerDrafts()&&_composerStatusKind=="idle")_composerStatus.Text="Taslak kaydedildi.";
            await PushComposerStateAsync();
        };
        try
        {
            if(File.Exists(ComposerDraftPath))
            {
                var drafts=JsonSerializer.Deserialize<Dictionary<string,ComposerDraft>>(File.ReadAllText(ComposerDraftPath));
                foreach(var draft in drafts??new())_composerDrafts[draft.Key]=draft.Value;
            }
        }
        catch{_composerStatus.Text="Eski taslak okunamadı. Yeni bir taslak oluşturabilirsin.";}
        _composerDescription.Text=DefaultComposerDescription;
        InstallComposerPopupExtras();UpdateComposerNet();
        FormClosed+=(_,_)=>{SaveComposerDrafts();_composerSaveTimer.Dispose();_composerTransport?.Dispose();_composerPublish.Dispose();};
    }
    private const string DefaultComposerDescription="Ürün hakkında detaylı bilgi almak için mesaj gönderebilirsiniz. Farklı ihtiyaçlarınızı da mesaj yoluyla iletebilirsiniz. Hayırlı olsun.";
    private Task ShowListingComposerAsync()=>ShowListingComposerPopupAsync();
    private void UpdateComposerNet(){var price=_composerPrice.Value;var commission=price>=10&&price<=50?5:decimal.Round(price*0.10m,2);_composerNet.Text=$"Komisyon: {commission:N2} TL · Net: {price-commission:N2} TL";}
    private void QueueComposerSave(){if(_composerLoading||_composerBusy)return;_composerSaveTimer.Stop();_composerSaveTimer.Start();}
    private ComposerDraft CurrentComposerDraft()=>new(){title=_composerTitle.Text.Trim(),description=_composerDescription.Text,price=_composerPrice.Value,delivery=Convert.ToInt32(_composerDelivery.SelectedItem??1),validity=Convert.ToInt32(_composerValidity.SelectedItem??15),stock=_composerStock.Checked,orders=_composerOrders.Checked,paths=_composerImages.Items.Cast<string>().ToList(),blocked=_composerUncertain};
    private bool SaveComposerDrafts()
    {
        try
        {
            if(_composerDraftServer is not null)_composerDrafts[_composerDraftServer]=CurrentComposerDraft();
            Directory.CreateDirectory(Path.GetDirectoryName(ComposerDraftPath)!);
            File.WriteAllText(ComposerDraftPath+".tmp",JsonSerializer.Serialize(_composerDrafts));File.Move(ComposerDraftPath+".tmp",ComposerDraftPath,true);
            return true;
        }
        catch{_composerStatusKind="error";_composerStatus.Text="Taslak kaydedilemedi. Disk alanını ve uygulama klasörünün izinlerini kontrol et.";return false;}
    }
    private void LoadComposerServerDraft()
    {
        if(_composerLoading||_composerServer.SelectedItem is not MarketServerInfo server)return;
        SaveComposerDrafts();_composerDraftServer=server.Id;_composerLoading=true;
        var draft=_composerDrafts.GetValueOrDefault(server.Id)??new ComposerDraft();
        if(draft.description=="Ürün bilgisi ilan başlığında belirtilmiştir. Detaylar için Itemci mesaj kutusundan iletişime geçebilirsiniz."||
           draft.description=="Ürün hakkında daha detaylı bilgi almak için mesaj gönderebilirsiniz. Farklı bir ihtiyacınız varsa yine mesaj yoluyla iletebilirsiniz. Hayırlı olsun.")draft.description=DefaultComposerDescription;
        _composerTitle.Text=draft.title;_composerDescription.Text=draft.description;_composerPrice.Value=Math.Clamp(draft.price,1,_composerPrice.Maximum);
        _composerDelivery.SelectedItem=new[]{1,2,6,12}.Contains(draft.delivery)?draft.delivery:1;
        _composerValidity.SelectedItem=new[]{15,30}.Contains(draft.validity)?draft.validity:15;
        _composerStock.Checked=draft.stock;_composerOrders.Checked=draft.orders;_composerImages.Items.Clear();
        var missing=false;
        foreach(var path in draft.paths.Take(4))
        {
            try{var saved=ResolveComposerImageId(Path.GetFileName(path))??SaveComposerLibraryImage(path);if(!_composerImages.Items.Contains(saved))_composerImages.Items.Add(saved);}
            catch{missing=true;}
        }
        _composerLoading=false;_composerUncertain=draft.blocked;
        _composerStatusKind=_composerUncertain?"error":"idle";
        _composerStatus.Text=_composerUncertain?"Önceki gönderim sonucu belirsiz. Tekrar yayınlamadan önce İlanlarım bölümünü kontrol et.":
            missing?"Bazı taslak görselleri bulunamadı. Hazır Görseller bölümünden yeniden seçebilirsin.":"Taslak hazır · Değişiklikler otomatik kaydedilir.";
        UpdateComposerNet();
    }
    private void SelectComposerPictures(bool addToListing)
    {
        using var dialog=new OpenFileDialog{Filter="PNG/JPG|*.png;*.jpg;*.jpeg",Multiselect=true};
        if(dialog.ShowDialog(_composerPopup??this)!=DialogResult.OK)return;
        var savedCount=0;
        foreach(var path in dialog.FileNames)
        {
            try
            {
                var saved=SaveComposerLibraryImage(path);savedCount++;
                if(addToListing&&!_composerImages.Items.Contains(saved)&&_composerImages.Items.Count<4)_composerImages.Items.Add(saved);
            }
            catch(Exception ex){ComposerToast(Path.GetFileName(path)+": "+ex.Message);}
        }
        if(addToListing&&savedCount>0)_composerStock.Checked=false;
        QueueComposerSave();
        _composerStatus.Text=savedCount+" görsel Hazır Görseller’e kaydedildi.";
        if(addToListing&&dialog.FileNames.Length>4)ComposerToast("İlana en fazla 4 görsel eklenir. Diğer görseller galeride saklandı.");
    }
    private async Task PublishComposerAsync()
    {
        if(SiteAutomationBlocked||_composerBusy||_composerUncertain||DateTime.UtcNow<_composerCooldownUntilUtc||_composerServer.SelectedItem is not MarketServerInfo server){await PushComposerStateAsync();return;}
        var draft=CurrentComposerDraft();if(string.IsNullOrWhiteSpace(draft.title)||string.IsNullOrWhiteSpace(draft.description)||(!draft.stock&&draft.paths.Count==0)){_composerStatusKind="error";_composerStatus.Text="Başlık, açıklama ve en az bir görsel gerekli.";await PushComposerStateAsync();return;}
        _composerSaveTimer.Stop();_composerBusy=true;_composerPublish!.Enabled=false;_composerPanel.Enabled=false;SaveComposerDrafts();
        var submissionStarted=false;
        try
        {
            _composerStatusKind="idle";_composerStatus.Text="İlan Itemci’ye aktarılıyor…";await PushComposerStateAsync();
            var before=await ReadComposerStoreSnapshotAsync();
            var images=new List<object>();if(!draft.stock)foreach(var path in draft.paths){if(!File.Exists(path)||new FileInfo(path).Length>5*1024*1024)throw new IOException("Görsel dosyası bulunamadı veya 5 MB sınırını aşıyor.");images.Add(new{name=Path.GetFileName(path),type=Path.GetExtension(path).ToLowerInvariant()==".png"?"image/png":"image/jpeg",base64=Convert.ToBase64String(await File.ReadAllBytesAsync(path))});}
            if(_browserEnvironment is null)throw new IOException("Itemci oturumu hazır değil.");
            if(_composerTransport is not null){Controls.Remove(_composerTransport);_composerTransport.Dispose();}
            _composerTransport=new WebView2{Size=new Size(1280,900),Location=new Point(-6000,-6000),Visible=true,TabStop=false};Controls.Add(_composerTransport);await _composerTransport.EnsureCoreWebView2Async(_browserEnvironment);
            var core=_composerTransport.CoreWebView2;core.NewWindowRequested+=(_,e)=>e.Handled=true;
            var loaded=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);core.NavigationCompleted+=(_,e)=>{if(e.IsSuccess)loaded.TrySetResult(true);else loaded.TrySetException(new IOException("Itemci formu yüklenemedi."));};core.Navigate("https://itemci.com/panel/ilan-ekle");await loaded.Task.WaitAsync(TimeSpan.FromSeconds(30));
            var resultTask=new TaskCompletionSource<ComposerResult>(TaskCreationOptions.RunContinuationsAsynchronously);var token=Guid.NewGuid().ToString("N");
            core.WebMessageReceived+=(_,e)=>{try{using var json=JsonDocument.Parse(e.WebMessageAsJson);var root=json.RootElement;if(!root.TryGetProperty("composerToken",out var t)||t.GetString()!=token)return;if(root.TryGetProperty("stage",out var stage)&&stage.GetString()=="sending"){submissionStarted=true;_composerUncertain=true;SaveComposerDrafts();return;}if(root.TryGetProperty("error",out var error))resultTask.TrySetException(new IOException(error.GetString()));else resultTask.TrySetResult(root.GetProperty("result").Deserialize<ComposerResult>()??new());}catch(Exception ex){resultTask.TrySetException(ex);}};
            await core.ExecuteScriptAsync(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"QuickListing.js")));await core.ExecuteScriptAsync(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"ListingComposer.js")));
            var payload=JsonSerializer.Serialize(new{server=server.Name,categoryUrl=server.Url,draft.title,draft.description,draft.price,draft.delivery,draft.validity,draft.stock,draft.orders,images});
            await core.ExecuteScriptAsync($"(async()=>{{try{{const p={payload};await window.__NiyaziPrepareOwnListing(p);chrome.webview.postMessage({{composerToken:'{token}',stage:'sending'}});const result=await window.__NiyaziSubmitOwnListing(p);chrome.webview.postMessage({{composerToken:'{token}',result}});}}catch(e){{chrome.webview.postMessage({{composerToken:'{token}',error:String(e.message||e)}});}}}})();");
            var result=await resultTask.Task.WaitAsync(TimeSpan.FromSeconds(130));_composerUncertain=result.uncertain;
            if(result.ok)
            {
                _composerStatusKind="success";_composerStatus.Text="İlan yayınlandı. Yeni ilan için geri sayım başladı.";
                _composerUncertain=false;_composerCooldownUntilUtc=DateTime.UtcNow.AddSeconds(60);NudgeAutoBumpDiscovery();
                _composerLastLink=IsComposerListingLink(result.url)?result.url:"";SaveComposerPopupState();SaveComposerDrafts();
                if(_composerLastLink.Length==0)
                {
                    _composerResolvingLink=true;await PushComposerStateAsync();
                    _composerLastLink=await ResolveComposerPublishedLinkAsync(before,draft);
                    _composerResolvingLink=false;SaveComposerPopupState();
                }
            }
            else
            {
                _composerStatusKind="error";_composerStatus.Text=result.message;
                ApplyComposerServerWait(result);
            }
        }
        catch(TimeoutException)
        {
            _composerUncertain=submissionStarted;_composerStatusKind="error";
            _composerStatus.Text=submissionStarted?"Gönderim sonucu doğrulanamadı. Tekrar göndermeden önce İlanlarım bölümünü kontrol et.":"Itemci formu zamanında yüklenemedi. Bağlantını kontrol edip tekrar deneyebilirsin.";
        }
        catch(Exception ex)
        {
            _composerUncertain=submissionStarted;_composerStatusKind="error";
            _composerStatus.Text=ex.Message+(submissionStarted?" Tekrar yayınlamadan önce İlanlarım bölümünü kontrol et.":"");
        }
        finally
        {
            if(_composerTransport is not null){Controls.Remove(_composerTransport);_composerTransport.Dispose();_composerTransport=null;}
            _composerResolvingLink=false;_composerBusy=false;_composerPanel.Enabled=true;
            SaveComposerDrafts();await PushComposerStateAsync();
        }
    }
    private sealed class ComposerDraft{public string title{get;set;}="";public string description{get;set;}=DefaultComposerDescription;public decimal price{get;set;}=1;public int delivery{get;set;}=1;public int validity{get;set;}=15;public bool stock{get;set;}=true;public bool orders{get;set;}=true;public List<string> paths{get;set;}=new();public bool blocked{get;set;}}
    private sealed class ComposerResult{public bool ok{get;set;}public bool uncertain{get;set;}public int retryAfterSeconds{get;set;}public string message{get;set;}="";public string id{get;set;}="";public string url{get;set;}="";}
}
