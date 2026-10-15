using System.Text.Json;
using System.Runtime.CompilerServices;
using Microsoft.Web.WebView2.WinForms;

namespace NiyazisugarItemci;
public sealed partial class MainForm
{
    private DateTime _sitePauseUntilUtc;
    private bool _sitePauseLoaded;
    private readonly ConditionalWeakTable<WebView2,object> _siteGuardViews=new();
    private string SitePausePath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"NiyazisugarItemci","site-pause.json");
    private bool SiteAutomationBlocked
    {
        get
        {
            if(!_sitePauseLoaded){_sitePauseLoaded=true;try{if(File.Exists(SitePausePath))_sitePauseUntilUtc=JsonSerializer.Deserialize<DateTime>(File.ReadAllText(SitePausePath));}catch{}}
            if(DateTime.UtcNow<_sitePauseUntilUtc&&_composerCooldownUntilUtc<_sitePauseUntilUtc)_composerCooldownUntilUtc=_sitePauseUntilUtc;
            return DateTime.UtcNow<_sitePauseUntilUtc;
        }
    }
    private string SitePauseMessage=>$"Itemci işlem sınırı · {Math.Max(0,(int)Math.Ceiling((_sitePauseUntilUtc-DateTime.UtcNow).TotalSeconds))} saniye otomasyon beklemesi";
    private void PauseSiteAutomation(int seconds)
    {
        _=SiteAutomationBlocked;
        var until=DateTime.UtcNow.AddSeconds(Math.Clamp(seconds+10,90,86400));
        if(until<=_sitePauseUntilUtc)return;
        _sitePauseUntilUtc=until;
        if(_composerCooldownUntilUtc<until)_composerCooldownUntilUtc=until;
        _autoBumpStatus.Text=_liveStatus.Text=_salesStatusLabel.Text=SitePauseMessage;
        var stamp=new DateTimeOffset(until).ToUnixTimeMilliseconds();
        foreach(var view in new[]{_webView,_marketWebView,_soldScanView,_autoBumpView}.Where(v=>v?.CoreWebView2 is not null).Distinct())
            _=SetSitePauseInViewAsync(view!,stamp);
        try{Directory.CreateDirectory(Path.GetDirectoryName(SitePausePath)!);File.WriteAllText(SitePausePath+".tmp",JsonSerializer.Serialize(until));File.Move(SitePausePath+".tmp",SitePausePath,true);}catch{}
    }
    private static async Task SetSitePauseInViewAsync(WebView2 view,long stamp){try{await view.CoreWebView2.ExecuteScriptAsync($"window.__niyaziSitePauseUntil={stamp};");}catch{}}
    private void InstallSiteTrafficGuard(WebView2 view)
    {
        if(_siteGuardViews.TryGetValue(view,out _))return;
        _siteGuardViews.Add(view,new object());
        var core=view.CoreWebView2;if(core is null)return;
        core.WebResourceResponseReceived+=(_,e)=>{
            if(Uri.TryCreate(e.Request.Uri,UriKind.Absolute,out var url)&&url.Host=="itemci.com"&&e.Response.StatusCode==429){
                int seconds=300;
                try{var value=e.Response.Headers.GetHeader("Retry-After");if(int.TryParse(value,out var n))seconds=Math.Clamp(n,1,86400);else if(DateTimeOffset.TryParse(value,out var date))seconds=Math.Clamp((int)(date.UtcDateTime-DateTime.UtcNow).TotalSeconds,1,86400);}catch{}
                PauseSiteAutomation(seconds);
            }
        };
        core.WebMessageReceived+=(_,e)=>{
            try{using var json=JsonDocument.Parse(e.WebMessageAsJson);var root=json.RootElement;
                if(Uri.TryCreate(e.Source,UriKind.Absolute,out var url)&&url.Host=="itemci.com"&&root.TryGetProperty("niyaziSiteLimit",out var flag)&&flag.ValueKind==JsonValueKind.True){
                    var seconds=root.TryGetProperty("seconds",out var n)&&n.TryGetInt32(out var value)?value:300;
                    PauseSiteAutomation(Math.Clamp(seconds,1,86400));
                }
            }catch{}
        };
        _=RegisterSiteObserverAsync(view);
    }
    private async Task RegisterSiteObserverAsync(WebView2 view)
    {
        try{await view.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(SiteLimitObserver);if(!view.IsDisposed)await view.CoreWebView2.ExecuteScriptAsync(SiteLimitObserver);}catch{}
    }
    private const string SiteLimitObserver="""
    (()=>{
      if(location.hostname!=='itemci.com'||window.__niyaziSiteLimitObserver)return;
      window.__niyaziSiteLimitObserver=true;
      const seen=new WeakMap();
      const check=()=>{
        for(const node of document.querySelectorAll('[role="alert"],[role="status"],[data-sonner-toast],.Toastify__toast,[class*="toast"]')){
          if(!node.getClientRects().length)continue;
          const text=(node.innerText||'').toLocaleLowerCase('tr-TR').normalize('NFD').replace(/[\u0300-\u036f]/g,'').replace(/ı/g,'i');
          if(!/cok fazla (?:deneme|islem)|too many (?:requests|attempts)/.test(text)||seen.get(node)===text)continue;
          seen.set(node,text);
          const seconds=Number(text.match(/(\d+)\s*saniye/)?.[1]||300);
          window.__niyaziSitePauseUntil=Date.now()+Math.max(90000,(seconds+10)*1000);
          window.chrome?.webview?.postMessage({niyaziSiteLimit:true,seconds});
        }
      };
      const start=()=>{check();new MutationObserver(check).observe(document.body,{childList:true,subtree:true,characterData:true});};
      if(document.body)start();else document.addEventListener('DOMContentLoaded',start,{once:true});
    })();
    """;
}
