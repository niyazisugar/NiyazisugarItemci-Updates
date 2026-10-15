using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
namespace NiyazisugarItemci;
public sealed partial class MainForm
{
    private readonly System.Windows.Forms.Timer _autoBumpTimer=new(){Interval=15000};
    private readonly Dictionary<string,AutoBumpRecord> _autoBumpRecords=new();
    private DateTime _autoBumpNextScanUtc;
    private DateTime _autoBumpDiscoverUtc;
    private DateTime _autoBumpGlobalNextUtc;
    private string AutoBumpSpacingPath=>AutoBumpStatePath+".spacing.json";
    private bool _autoBumpBusy;
    private bool _autoBumpInstalled;
    private string? _autoBumpConfirmId;
    private bool AutoBumpDue=>_autoBumpInstalled&&_settings.AutoFreeBumpEnabled&&_webReady&&DateTime.UtcNow>=_autoBumpNextScanUtc&&DateTime.UtcNow>=_autoBumpGlobalNextUtc;
    private WebView2? _autoBumpView;
    private readonly Label _autoBumpStatus=new(){Dock=DockStyle.Top,Height=50,ForeColor=Color.FromArgb(150,157,170)};
    private string AutoBumpStatePath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"NiyazisugarItemci","auto-bump-state.json");

    private void InstallAutoBump()
    {
        try
        {
            if(File.Exists(AutoBumpStatePath))foreach(var record in JsonSerializer.Deserialize<Dictionary<string,AutoBumpRecord>>(File.ReadAllText(AutoBumpStatePath))??new())_autoBumpRecords[record.Key]=record.Value;
        }
        catch{_autoBumpStatus.Text="Yukarı taşıma kaydı okunamadı; site düğmeleri esas alınacak.";}
        try{if(File.Exists(AutoBumpSpacingPath))_autoBumpGlobalNextUtc=JsonSerializer.Deserialize<DateTime>(File.ReadAllText(AutoBumpSpacingPath));}catch{_autoBumpGlobalNextUtc=DateTime.UtcNow.AddSeconds(90);}
        _autoBumpGlobalNextUtc=AutoBumpSchedule.GlobalNextAllowed(_autoBumpRecords.Values,_autoBumpGlobalNextUtc);
        var card=new Panel{Dock=DockStyle.Top,Height=205,BackColor=Card,Padding=new Padding(18)};
        var title=new Label{Dock=DockStyle.Top,Height=30,Text="Otomatik Ücretsiz Yukarı Taşıma",ForeColor=TextMain,Font=new Font("Segoe UI",12,FontStyle.Bold)};
        var enabled=new CheckBox{Dock=DockStyle.Top,Height=30,Text="Süresi gelen aktif ilanları otomatik yukarı taşı",Checked=_settings.AutoFreeBumpEnabled,ForeColor=TextMain};
        var actions=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=42,WrapContents=false};
        actions.Controls.Add(ActionButton("ŞİMDİ KONTROL ET",()=>{_autoBumpNextScanUtc=DateTime.MinValue;_autoBumpDiscoverUtc=DateTime.MinValue;_=RunAutoBumpAsync();}));
        actions.Controls.Add(ActionButton("SONUCU KOPYALA",()=>Clipboard.SetText(_autoBumpStatus.Text)));
        if(_autoBumpStatus.Text.Length==0)_autoBumpStatus.Text="Uygulama açıkken çalışır · Site düğmesi aktifse ücretsiz taşıma · Her ilan saatte bir · İşlemler arasında en az 90 saniye";
        enabled.CheckedChanged+=(_,_)=>{_settings.AutoFreeBumpEnabled=enabled.Checked;_settings.Save();_autoBumpStatus.Text=enabled.Checked?"Otomatik kontrol açık; ilk uygun kontrolde başlayacak.":"Otomatik yukarı taşıma kapalı.";_autoBumpNextScanUtc=DateTime.MinValue;};
        card.Controls.Add(_autoBumpStatus);card.Controls.Add(enabled);card.Controls.Add(title);card.Controls.Add(actions);_autoBumpStatus.BringToFront();
        _settingsPanel.Controls.Add(card);card.BringToFront();
        _autoBumpInstalled=true;
        _autoBumpTimer.Tick+=(_,_)=>_=RunAutoBumpAsync();_autoBumpTimer.Start();
        FormClosed+=(_,_)=>{_autoBumpTimer.Dispose();_autoBumpView?.Dispose();};
    }
    private void SaveAutoBumpState()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(AutoBumpStatePath)!);
        File.WriteAllText(AutoBumpSpacingPath+".tmp",JsonSerializer.Serialize(_autoBumpGlobalNextUtc));File.Move(AutoBumpSpacingPath+".tmp",AutoBumpSpacingPath,true);
        File.WriteAllText(AutoBumpStatePath+".tmp",JsonSerializer.Serialize(_autoBumpRecords));File.Move(AutoBumpStatePath+".tmp",AutoBumpStatePath,true);
    }
    private void LogAutoBump(string message)
    {
        try
        {
            var path=Path.Combine(Path.GetDirectoryName(AutoBumpStatePath)!,"auto-bump.log");
            if(File.Exists(path)&&new FileInfo(path).Length>1024*1024)File.Move(path,path+".previous",true);
            File.AppendAllText(path,DateTime.UtcNow.ToString("O")+" "+message+Environment.NewLine);
        }
        catch{ }
    }
    private void NudgeAutoBumpDiscovery(){_autoBumpDiscoverUtc=DateTime.MinValue;_autoBumpNextScanUtc=DateTime.UtcNow.AddMinutes(1);}
    private async Task NavigateAutoBumpPageAsync(int page)
    {
        var core=_autoBumpView!.CoreWebView2;
        var completion=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Completed(object? sender,CoreWebView2NavigationCompletedEventArgs e){if(e.IsSuccess)completion.TrySetResult(true);else completion.TrySetException(new IOException("İlanlarım yüklenemedi: "+e.WebErrorStatus));}
        core.NavigationCompleted+=Completed;
        try
        {
            core.Navigate("https://itemci.com/panel/ilanlarim?page="+page);
            await completion.Task.WaitAsync(TimeSpan.FromSeconds(25));
            if(!Uri.TryCreate(core.Source,UriKind.Absolute,out var uri)||uri.Host!="itemci.com"||uri.AbsolutePath.TrimEnd('/')!="/panel/ilanlarim")throw new IOException("Itemci oturumun açık değil. Itemci bölümünden giriş yapmalısın.");
            await core.ExecuteScriptAsync(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"AutoBump.js")));
        }
        finally{core.NavigationCompleted-=Completed;}
    }
    private async Task<T> ReadAutoBumpResultAsync<T>(string expression) where T:class
    {
        var core=_autoBumpView!.CoreWebView2;var token=Guid.NewGuid().ToString("N");
        var completion=new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Received(object? sender,CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                using var json=JsonDocument.Parse(e.WebMessageAsJson);var root=json.RootElement;
                if(!root.TryGetProperty("bumpToken",out var t)||t.GetString()!=token)return;
                if(root.TryGetProperty("error",out var error))completion.TrySetException(new IOException(error.GetString()));
                else completion.TrySetResult(root.GetProperty("result").Deserialize<T>()??throw new IOException("Yukarı taşıma yanıtı boş."));
            }
            catch(Exception ex){completion.TrySetException(ex);}
        }
        core.WebMessageReceived+=Received;
        try
        {
            await core.ExecuteScriptAsync($"Promise.resolve({expression}).then(result=>chrome.webview.postMessage({{bumpToken:'{token}',result}})).catch(e=>chrome.webview.postMessage({{bumpToken:'{token}',error:String(e.message||e)}}));");
            return await completion.Task.WaitAsync(TimeSpan.FromSeconds(20));
        }
        finally{core.WebMessageReceived-=Received;}
    }
    private async Task RunAutoBumpAsync()
    {
        if(!_settings.AutoFreeBumpEnabled||_autoBumpBusy||IsDisposed||!_webReady||_browserEnvironment is null||DateTime.UtcNow<_autoBumpNextScanUtc||DateTime.UtcNow<_autoBumpGlobalNextUtc)return;
        if(_composerPopup is not null||_composerBusy||_soldScanBusy||_liveBusy||_marketPanel.Visible)return;
        _autoBumpBusy=true;
        var attempted=false;var throttled=false;var successes=0;var checkedCount=0;var uncertain=0;var interrupted=false;var unsupported=0;var seen=new HashSet<string>();
        try
        {
            _autoBumpStatus.Text="Aktif ilanların ücretsiz yukarı taşıma süreleri kontrol ediliyor…";
            _autoBumpView=new WebView2{Size=new Size(1280,900),Location=new Point(-7000,-7000),Visible=true,TabStop=false};
            Controls.Add(_autoBumpView);await _autoBumpView.EnsureCoreWebView2Async(_browserEnvironment);
            _autoBumpView.CoreWebView2.Settings.AreDevToolsEnabled=false;_autoBumpView.CoreWebView2.Settings.AreDefaultContextMenusEnabled=false;
            _autoBumpView.CoreWebView2.NewWindowRequested+=(_,e)=>e.Handled=true;
            _autoBumpView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled=false;
            _autoBumpView.CoreWebView2.ScriptDialogOpening+=(_,e)=>
            {
                var message=e.Message.ToLowerInvariant().Replace('ı','i').Replace('ü','u').Replace('ş','s');
                var fee=System.Text.RegularExpressions.Regex.IsMatch(message,@"ucret(?!siz)|vip|vitrin|bakiye|\d[\d.,]*\s*(?:tl|₺)");
                if(_autoBumpConfirmId is not null&&!fee&&e.Kind==CoreWebView2ScriptDialogKind.Confirm&&message.Contains("ilan")&&message.Contains("yukari"))e.Accept();
            };
            ConfigureAdBlocking(_autoBumpView);
            for(var page=1;page<=40;page++)
            {
                if(!_settings.AutoFreeBumpEnabled||_composerPopup is not null||_composerBusy){interrupted=true;break;}
                await NavigateAutoBumpPageAsync(page);
                var read=await ReadAutoBumpResultAsync<AutoBumpPage>("window.__niyaziReadBumpPage()");
                if(read.unsupported)unsupported++;
                foreach(var listing in read.listings)
                {
                    if(!_settings.AutoFreeBumpEnabled||_composerPopup is not null||_composerBusy){interrupted=true;break;}
                    if(!seen.Add(listing.id))continue;checkedCount++;
                    if(!_autoBumpRecords.TryGetValue(listing.id,out var record))_autoBumpRecords[listing.id]=record=new();
                    var now=DateTime.UtcNow;
                    if(!AutoBumpSchedule.CanAttempt(record,now))continue;
                    if(!listing.enabled){record.nextAllowedUtc=now.AddSeconds(Math.Max(60,listing.waitSeconds));record.status="Site düğmesi henüz aktif değil";continue;}
                    // Persist the attempt before clicking. A crash or restart cannot
                    // immediately send the same free bump twice.
                    _autoBumpGlobalNextUtc=now.AddSeconds(90);AutoBumpSchedule.Reserve(record,now);SaveAutoBumpState();attempted=true;
                    AutoBumpResult result;
                    try{_autoBumpConfirmId=listing.id;result=await ReadAutoBumpResultAsync<AutoBumpResult>($"window.__niyaziBumpOne({JsonSerializer.Serialize(listing.id)})");}
                    catch(Exception ex){result=new AutoBumpResult{uncertain=true,message="İşlem sonucu doğrulanamadı: "+ex.Message};}
                    finally{_autoBumpConfirmId=null;}
                    throttled=result.rateLimited;
                    _autoBumpGlobalNextUtc=AutoBumpSchedule.AfterAttempt(DateTime.UtcNow,throttled,result.retryAfterSeconds);
                    AutoBumpSchedule.Complete(record,DateTime.UtcNow,result.ok,result.uncertain,result.notSent,result.retryAfterSeconds);SaveAutoBumpState();
                    if(result.ok)successes++;if(result.uncertain)uncertain++;
                    LogAutoBump("İlan "+listing.id+" · "+record.status+" · "+result.message);
                    interrupted=true;break; // One attempt per pass; release the browser while the global cooldown runs.
                }
                if(attempted||interrupted||!read.hasNext)break;
                if(page==40)throw new IOException("İlk 40 sayfa kontrol edildi; kalan sayfalar için manuel kontrol gerekli.");
            }
            var nowEnd=DateTime.UtcNow;
            // Remove obsolete tracking records, keeping pending and recent attempts.
            foreach(var id in _autoBumpRecords.Where(k=>!interrupted).Where(k=>!seen.Contains(k.Key)&&k.Value.lastAttemptUtc<nowEnd.AddDays(-14)&&k.Value.nextAllowedUtc<nowEnd).Select(k=>k.Key).ToArray())_autoBumpRecords.Remove(id);
            SaveAutoBumpState();
            _autoBumpDiscoverUtc=nowEnd.AddMinutes(10);
            _autoBumpNextScanUtc=interrupted?nowEnd.AddMinutes(1):AutoBumpSchedule.NextScan(_autoBumpRecords.Where(k=>seen.Contains(k.Key)).Select(k=>k.Value),nowEnd,_autoBumpDiscoverUtc);
            if(_autoBumpNextScanUtc<_autoBumpGlobalNextUtc)_autoBumpNextScanUtc=_autoBumpGlobalNextUtc;
            _autoBumpStatus.Text=$"{checkedCount} ilan kontrol edildi · {successes} ücretsiz yukarı taşındı · {DateTime.Now:HH:mm}"+
                (uncertain>0?$" · {uncertain} sonuç belirsiz; aynı saat içinde tekrar denenmez":"")+(unsupported>0?" · Bazı ücretsiz düğmeler okunamadı":"")+(attempted?$" · Sonraki işlem en erken {_autoBumpGlobalNextUtc.ToLocalTime():HH:mm:ss}":"")+(throttled?" · Site işlem sınırı: otomasyon beklemeye alındı":"");
        }
        catch(Exception ex){_autoBumpStatus.Text="Yukarı taşıma kontrolü tamamlanamadı: "+ex.Message;LogAutoBump(ex.Message);_autoBumpNextScanUtc=DateTime.UtcNow.AddMinutes(5);}
        finally
        {
            if(_autoBumpView is not null){Controls.Remove(_autoBumpView);_autoBumpView.Dispose();_autoBumpView=null;}
            _autoBumpBusy=false;
        }
    }
    private sealed class AutoBumpPage{public List<AutoBumpListing> listings{get;set;}=new();public bool hasNext{get;set;}public bool unsupported{get;set;}}
    private sealed class AutoBumpListing{public string id{get;set;}="";public bool enabled{get;set;}public int waitSeconds{get;set;}}
    private sealed class AutoBumpResult{public bool ok{get;set;}public bool uncertain{get;set;}public bool notSent{get;set;}public bool rateLimited{get;set;}public int retryAfterSeconds{get;set;}public string message{get;set;}="";}
}
