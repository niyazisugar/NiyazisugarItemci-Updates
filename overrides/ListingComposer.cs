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
        _composerPanel.BackColor=Bg;
        var column=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(0)};
        column.Controls.Add(new Label{Text="Hızlı İlan Ekle",AutoSize=true,Font=new Font("Segoe UI",22,FontStyle.Bold),ForeColor=Accent,Margin=new Padding(0,0,0,8)});
        column.Controls.Add(new Label{Text="Item&Yang · Formu burada doldur; Yayınla ile Itemci'ye aktar.",AutoSize=true,ForeColor=TextMuted,Margin=new Padding(0,0,0,18)});
        void Field(string caption,Control control){column.Controls.Add(new Label{Text=caption,AutoSize=true,ForeColor=TextMuted,Margin=new Padding(0,8,0,5)});control.Margin=new Padding(0,0,0,5);column.Controls.Add(control);}
        var serverRow=new FlowLayoutPanel{Width=720,Height=45,WrapContents=false};
        serverRow.Controls.Add(_composerServer);serverRow.Controls.Add(ActionButton("＋ SUNUCU EKLE",()=>_=AddComposerServerAsync()));
        Field("SUNUCU · KATEGORİ HER ZAMAN ITEM&YANG",serverRow);
        Field("İLAN BAŞLIĞI · EN FAZLA 150 KARAKTER",_composerTitle);
        var prices=new FlowLayoutPanel{Width=720,Height=64,WrapContents=false};
        foreach(var pair in new[]{("FİYAT (TL)",(Control)_composerPrice),("TESLİM (SAAT)",(Control)_composerDelivery),("GEÇERLİLİK (GÜN)",(Control)_composerValidity)}){
            var box=new FlowLayoutPanel{Width=190,Height=60,FlowDirection=FlowDirection.TopDown,WrapContents=false};box.Controls.Add(new Label{Text=pair.Item1,AutoSize=true,ForeColor=TextMuted});box.Controls.Add(pair.Item2);prices.Controls.Add(box);
        }
        _composerDelivery.Items.AddRange(new object[]{1,2,6,12});_composerDelivery.SelectedItem=1;
        _composerValidity.Items.AddRange(new object[]{15,30});_composerValidity.SelectedItem=15;
        column.Controls.Add(prices);column.Controls.Add(_composerNet);
        Field("AÇIKLAMA · EN FAZLA 1.200 KARAKTER",_composerDescription);
        Field("GÖRSELLER · EN FAZLA 4 PNG/JPG · HER BİRİ EN FAZLA 5 MB",_composerStock);
        column.Controls.Add(_composerImages);
        var pictures=new FlowLayoutPanel{Width=720,Height=46,WrapContents=false};
        pictures.Controls.Add(ActionButton("GÖRSEL SEÇ",SelectComposerPictures));pictures.Controls.Add(ActionButton("SEÇİLENİ KALDIR",()=>{if(_composerImages.SelectedIndex>=0)_composerImages.Items.RemoveAt(_composerImages.SelectedIndex);QueueComposerSave();}));column.Controls.Add(pictures);
        column.Controls.Add(_composerOrders);
        var actions=new FlowLayoutPanel{Width=760,Height=52,WrapContents=false};
        _composerPublish=ActionButton("İLANI YAYINLA",()=>_=PublishComposerAsync());_composerPublish.Width=180;_composerPublish.ForeColor=Accent;
        actions.Controls.Add(_composerPublish);
        actions.Controls.Add(ActionButton("TASLAĞI KAYDET",()=>{SaveComposerDrafts();_composerStatus.Text="Taslak kaydedildi.";}));
        actions.Controls.Add(ActionButton("İLANLARIM",()=>{ShowPanel(_listingsPanel);_=RefreshCurrentListingsAsync(true);}));
        actions.Controls.Add(ActionButton("YENİ TASLAK",()=>{_composerLoading=true;_composerTitle.Clear();_composerPrice.Value=1;_composerImages.Items.Clear();_composerDescription.Text=DefaultComposerDescription;_composerLoading=false;_composerUncertain=false;_composerPublish.Enabled=true;QueueComposerSave();_composerStatus.Text="Yeni taslak hazır.";}));
        column.Controls.Add(actions);column.Controls.Add(_composerStatus);_composerPanel.Controls.Add(column);_content.Controls.Add(_composerPanel);
        foreach(var input in new TextBox[]{_composerTitle,_composerDescription}){input.BackColor=Card;input.ForeColor=TextMain;input.Font=new Font("Segoe UI",11);input.TextChanged+=(_,_)=>QueueComposerSave();}
        _composerPrice.ValueChanged+=(_,_)=>{UpdateComposerNet();QueueComposerSave();};
        _composerDelivery.SelectedIndexChanged+=(_,_)=>QueueComposerSave();_composerValidity.SelectedIndexChanged+=(_,_)=>QueueComposerSave();
        _composerStock.CheckedChanged+=(_,_)=>{_composerImages.Enabled=!_composerStock.Checked;QueueComposerSave();};_composerOrders.CheckedChanged+=(_,_)=>QueueComposerSave();
        _composerServer.SelectedIndexChanged+=(_,_)=>LoadComposerServerDraft();
        _composerSaveTimer.Tick+=(_,_)=>{_composerSaveTimer.Stop();SaveComposerDrafts();};
        try{if(File.Exists(ComposerDraftPath)){var drafts=JsonSerializer.Deserialize<Dictionary<string,ComposerDraft>>(File.ReadAllText(ComposerDraftPath));foreach(var d in drafts??new())_composerDrafts[d.Key]=d.Value;}}catch{}
        _composerDescription.Text=DefaultComposerDescription;_composerImages.Enabled=false;UpdateComposerNet();
        FormClosed+=(_,_)=>{SaveComposerDrafts();_composerSaveTimer.Dispose();_composerTransport?.Dispose();};
    }
    private const string DefaultComposerDescription="Ürün bilgisi ilan başlığında belirtilmiştir. Detaylar için Itemci mesaj kutusundan iletişime geçebilirsiniz.";
    private async Task ShowListingComposerAsync()
    {
        ShowPanel(_composerPanel);
        if(_marketWebReady)await RefreshMarketServersAsync();
        var id=(_composerServer.SelectedItem as MarketServerInfo)?.Id??_settings.LastQuickListingServer;
        _composerLoading=true;_composerServer.Items.Clear();foreach(var server in _marketServers)_composerServer.Items.Add(server);_composerServer.DisplayMember="Name";
        _composerServer.SelectedItem=_marketServers.FirstOrDefault(s=>s.Id==id)??_marketServers.FirstOrDefault();_composerLoading=false;LoadComposerServerDraft();
    }
    private void UpdateComposerNet(){var price=_composerPrice.Value;var commission=price>=10&&price<=50?5:decimal.Round(price*0.10m,2);_composerNet.Text=$"Komisyon: {commission:N2} TL · Net: {price-commission:N2} TL";}
    private void QueueComposerSave(){if(_composerLoading||_composerBusy)return;_composerSaveTimer.Stop();_composerSaveTimer.Start();}
    private ComposerDraft CurrentComposerDraft()=>new(){title=_composerTitle.Text.Trim(),description=_composerDescription.Text,price=_composerPrice.Value,delivery=Convert.ToInt32(_composerDelivery.SelectedItem??1),validity=Convert.ToInt32(_composerValidity.SelectedItem??15),stock=_composerStock.Checked,orders=_composerOrders.Checked,paths=_composerImages.Items.Cast<string>().ToList(),blocked=_composerUncertain};
    private void SaveComposerDrafts(){try{if(_composerDraftServer is not null)_composerDrafts[_composerDraftServer]=CurrentComposerDraft();Directory.CreateDirectory(Path.GetDirectoryName(ComposerDraftPath)!);File.WriteAllText(ComposerDraftPath+".tmp",JsonSerializer.Serialize(_composerDrafts));File.Move(ComposerDraftPath+".tmp",ComposerDraftPath,true);}catch{_composerStatus.Text="Taslak kaydı başarısız oldu.";}}
    private void LoadComposerServerDraft(){if(_composerLoading||_composerServer.SelectedItem is not MarketServerInfo server)return;SaveComposerDrafts();_composerDraftServer=server.Id;_composerLoading=true;var d=_composerDrafts.GetValueOrDefault(server.Id)??new ComposerDraft();_composerTitle.Text=d.title;_composerDescription.Text=d.description;_composerPrice.Value=Math.Clamp(d.price,1,_composerPrice.Maximum);_composerDelivery.SelectedItem=d.delivery;_composerValidity.SelectedItem=d.validity;_composerStock.Checked=d.stock;_composerOrders.Checked=d.orders;_composerImages.Items.Clear();foreach(var p in d.paths)_composerImages.Items.Add(p);_composerLoading=false;_composerUncertain=d.blocked;_composerPublish!.Enabled=!d.blocked;_composerStatus.Text=server.Name+" · Item&Yang · Taslak otomatik kaydedilir.";UpdateComposerNet();}
    private void SelectComposerPictures(){using var dialog=new OpenFileDialog{Filter="PNG/JPG|*.png;*.jpg;*.jpeg",Multiselect=true};if(dialog.ShowDialog(this)!=DialogResult.OK)return;foreach(var path in dialog.FileNames){if(_composerImages.Items.Count>=4)break;if(new FileInfo(path).Length>5*1024*1024){_composerStatus.Text="5 MB üzerindeki görsel eklenmedi.";continue;}if(!_composerImages.Items.Contains(path))_composerImages.Items.Add(path);}_composerStock.Checked=false;QueueComposerSave();}

    private async Task AddComposerServerAsync()
    {
        using var dialog=new Form{Text="Sunucu Ekle",Size=new Size(560,270),StartPosition=FormStartPosition.CenterParent,BackColor=Bg,ForeColor=TextMain,Padding=new Padding(20),MinimizeBox=false,MaximizeBox=false};
        var name=new TextBox{Dock=DockStyle.Top,PlaceholderText="Sunucu adı · Örn: Sezar2"};var url=new TextBox{Dock=DockStyle.Top,PlaceholderText="Itemci Item&Yang kategori bağlantısı"};
        var save=ActionButton("KAYDET",()=>dialog.DialogResult=DialogResult.OK);save.Dock=DockStyle.Bottom;dialog.Controls.Add(url);dialog.Controls.Add(name);dialog.Controls.Add(save);PremiumTheme.Apply(dialog);
        if(dialog.ShowDialog(this)!=DialogResult.OK)return;
        if(string.IsNullOrWhiteSpace(name.Text)||!Uri.TryCreate(url.Text.Trim(),UriKind.Absolute,out var uri)||uri.Scheme!="https"||uri.Host!="itemci.com"||!uri.AbsolutePath.StartsWith("/categories/")){_composerStatus.Text="Sunucu adı ve geçerli Itemci kategori bağlantısı gerekli.";return;}
        if(!_marketAnalyzerInjected){_composerStatus.Text="Analiz sistemi henüz hazır değil.";return;}
        await _marketWebView.CoreWebView2.ExecuteScriptAsync($"window.__NiyazisugarSaveQuickServer({JsonSerializer.Serialize(name.Text.Trim())},{JsonSerializer.Serialize(uri.ToString())})");
        await RefreshMarketServersAsync();await ShowListingComposerAsync();_composerServer.SelectedItem=_marketServers.FirstOrDefault(s=>s.Name==name.Text.Trim());
    }
    private async Task PublishComposerAsync()
    {
        if(_composerBusy||_composerUncertain||_composerServer.SelectedItem is not MarketServerInfo server)return;
        var draft=CurrentComposerDraft();if(string.IsNullOrWhiteSpace(draft.title)||string.IsNullOrWhiteSpace(draft.description)||(!draft.stock&&draft.paths.Count==0)){_composerStatus.Text="Başlık, açıklama ve en az bir görsel gerekli.";return;}
        _composerBusy=true;_composerPublish!.Enabled=false;_composerPanel.Enabled=false;SaveComposerDrafts();
        try
        {
            _composerStatus.Text="İlan Itemci'ye aktarılıyor…";
            var images=new List<object>();if(!draft.stock)foreach(var path in draft.paths){if(!File.Exists(path)||new FileInfo(path).Length>5*1024*1024)throw new IOException("Görsel dosyası bulunamadı veya 5 MB sınırını aşıyor.");images.Add(new{name=Path.GetFileName(path),type=Path.GetExtension(path).ToLowerInvariant()==".png"?"image/png":"image/jpeg",base64=Convert.ToBase64String(await File.ReadAllBytesAsync(path))});}
            if(_browserEnvironment is null)throw new IOException("Itemci oturumu hazır değil.");
            if(_composerTransport is not null){Controls.Remove(_composerTransport);_composerTransport.Dispose();}
            _composerTransport=new WebView2{Size=new Size(1280,900),Visible=false};Controls.Add(_composerTransport);await _composerTransport.EnsureCoreWebView2Async(_browserEnvironment);
            var core=_composerTransport.CoreWebView2;core.NewWindowRequested+=(_,e)=>e.Handled=true;
            var loaded=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);core.NavigationCompleted+=(_,e)=>{if(e.IsSuccess)loaded.TrySetResult(true);else loaded.TrySetException(new IOException("Itemci formu yüklenemedi."));};core.Navigate("https://itemci.com/panel/ilan-ekle");await loaded.Task.WaitAsync(TimeSpan.FromSeconds(30));
            var resultTask=new TaskCompletionSource<ComposerResult>(TaskCreationOptions.RunContinuationsAsynchronously);var token=Guid.NewGuid().ToString("N");
            core.WebMessageReceived+=(_,e)=>{try{using var json=JsonDocument.Parse(e.WebMessageAsJson);var root=json.RootElement;if(!root.TryGetProperty("composerToken",out var t)||t.GetString()!=token)return;if(root.TryGetProperty("error",out var error))resultTask.TrySetException(new IOException(error.GetString()));else resultTask.TrySetResult(root.GetProperty("result").Deserialize<ComposerResult>()??new());}catch(Exception ex){resultTask.TrySetException(ex);}};
            await core.ExecuteScriptAsync(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"QuickListing.js")));await core.ExecuteScriptAsync(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"ListingComposer.js")));
            var payload=JsonSerializer.Serialize(new{server=server.Name,draft.title,draft.description,draft.price,draft.delivery,draft.validity,draft.stock,draft.orders,images});
            await core.ExecuteScriptAsync($"(async()=>{{try{{const p={payload};await window.__NiyaziPrepareOwnListing(p);const result=await window.__NiyaziSubmitOwnListing(p);chrome.webview.postMessage({{composerToken:'{token}',result}});}}catch(e){{chrome.webview.postMessage({{composerToken:'{token}',error:String(e.message||e)}});}}}})();");
            var result=await resultTask.Task.WaitAsync(TimeSpan.FromSeconds(130));_composerUncertain=result.uncertain;
            if(result.ok){_composerStatus.Text=result.message+(result.id.Length>0?" · İlan: "+result.id:"")+" · İlanlarım bölümünden görebilirsin.";_composerStatus.ForeColor=Green;_composerUncertain=true;}
            else{_composerStatus.Text=result.message;_composerStatus.ForeColor=Accent;}
        }
        catch(TimeoutException){_composerUncertain=true;_composerStatus.Text="Sonuç doğrulanamadı. Tekrar göndermeden önce İlanlarım bölümünü kontrol et.";}
        catch(Exception ex){_composerStatus.Text=ex.Message;_composerStatus.ForeColor=Accent;}
        finally{_composerBusy=false;_composerPanel.Enabled=true;_composerPublish.Enabled=!_composerUncertain;SaveComposerDrafts();}
    }
    private sealed class ComposerDraft{public string title{get;set;}="";public string description{get;set;}=DefaultComposerDescription;public decimal price{get;set;}=1;public int delivery{get;set;}=1;public int validity{get;set;}=15;public bool stock{get;set;}=true;public bool orders{get;set;}=true;public List<string> paths{get;set;}=new();public bool blocked{get;set;}}
    private sealed class ComposerResult{public bool ok{get;set;}public bool uncertain{get;set;}public string message{get;set;}="";public string id{get;set;}="";public string url{get;set;}="";}
}
