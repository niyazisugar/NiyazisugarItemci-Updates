using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
namespace NiyazisugarItemci;

// Shared by the app and the Windows integration test: create and navigate only
// after the containing popup is shown, with a stable local HTTPS origin.
internal sealed class ComposerSurface : IDisposable
{
    internal const string PageUrl="https://composer-ui.local/ComposerView.html";
    private readonly WebView2 _view;
    private readonly Func<string,Task> _message;
    private readonly Action<string> _failure;
    private bool _configured;
    private TaskCompletionSource<bool>? _ready;
    private TaskCompletionSource<bool>? _navigation;

    internal ComposerSurface(WebView2 view,Func<string,Task> message,Action<string> failure)
    { _view=view;_message=message;_failure=failure; }

    internal async Task LoadAsync(CoreWebView2Environment? environment,string assets,string images,CancellationToken cancellationToken)
    {
        if(!_view.IsHandleCreated||!_view.Visible)throw new IOException("İlan penceresi görüntülenmeden içerik başlatılamaz.");
        if(!File.Exists(Path.Combine(assets,"ComposerView.html")))throw new IOException("Arayüz dosyası eksik. Güncelleme ZIP dosyasının tamamını çıkartıp uygulamayı yeniden aç.");
        Directory.CreateDirectory(images);
        await _view.EnsureCoreWebView2Async(environment).WaitAsync(TimeSpan.FromSeconds(25),cancellationToken);
        var core=_view.CoreWebView2;
        if(!_configured)
        {
            core.Settings.IsScriptEnabled=true;
            core.Settings.IsWebMessageEnabled=true;
            core.Settings.AreDefaultContextMenusEnabled=false;
            core.Settings.AreDevToolsEnabled=false;
            core.Settings.AreDefaultScriptDialogsEnabled=false;
            core.Settings.IsStatusBarEnabled=false;
            core.Settings.IsZoomControlEnabled=false;
            core.SetVirtualHostNameToFolderMapping("composer-ui.local",assets,CoreWebView2HostResourceAccessKind.DenyCors);
            core.SetVirtualHostNameToFolderMapping("composer-images.local",images,CoreWebView2HostResourceAccessKind.DenyCors);
            core.NavigationStarting+=NavigationStarting;
            core.NavigationCompleted+=NavigationCompleted;
            core.WebMessageReceived+=WebMessageReceived;
            core.NewWindowRequested+=NewWindowRequested;
            core.ProcessFailed+=ProcessFailed;
            _configured=true;
        }
        _ready=new(TaskCreationOptions.RunContinuationsAsynchronously);
        _navigation=new(TaskCreationOptions.RunContinuationsAsynchronously);
        core.Navigate(PageUrl);
        await Task.WhenAll(_ready.Task,_navigation.Task).WaitAsync(TimeSpan.FromSeconds(25),cancellationToken);
        var rendered=await core.ExecuteScriptAsync("!!document.querySelector('.app') && document.querySelector('h1')?.textContent === 'Hızlı İlan Ekle' && typeof window.__composerReceive === 'function'").WaitAsync(TimeSpan.FromSeconds(8),cancellationToken);
        if(rendered!="true")throw new IOException("İlan arayüzü yüklenemedi. Yeniden Dene ile tekrar açabilirsin.");
    }

    private static bool IsPage(string source)=>Uri.TryCreate(source,UriKind.Absolute,out var uri)&&
        uri.Scheme=="https"&&uri.Host=="composer-ui.local"&&uri.IsDefaultPort&&uri.UserInfo.Length==0&&
        uri.AbsolutePath=="/ComposerView.html"&&uri.Query.Length==0;
    private void NavigationStarting(object? sender,CoreWebView2NavigationStartingEventArgs e)
    { if(!IsPage(e.Uri))e.Cancel=true; }
    private void NavigationCompleted(object? sender,CoreWebView2NavigationCompletedEventArgs e)
    {
        if(e.IsSuccess)_navigation?.TrySetResult(true);
        else _navigation?.TrySetException(new IOException("İlan arayüzü açılamadı: "+e.WebErrorStatus));
    }
    private async void WebMessageReceived(object? sender,CoreWebView2WebMessageReceivedEventArgs e)
    {
        if(!IsPage(e.Source))return;
        try
        {
            var text=e.WebMessageAsJson;
            using var document=JsonDocument.Parse(text);
            await _message(text);
            if(document.RootElement.TryGetProperty("action",out var action)&&action.GetString()=="ready")_ready?.TrySetResult(true);
        }
        catch(Exception ex){_ready?.TrySetException(ex);_failure("İlan arayüzüyle bağlantı kurulamadı: "+ex.Message);}
    }
    private void NewWindowRequested(object? sender,CoreWebView2NewWindowRequestedEventArgs e)=>e.Handled=true;
    private void ProcessFailed(object? sender,CoreWebView2ProcessFailedEventArgs e)
    {
        var error=new IOException("İlan penceresinin görüntüleme işlemi durdu. Yeniden Dene ile tekrar açabilirsin.");
        _ready?.TrySetException(error);_navigation?.TrySetException(error);_failure(error.Message);
    }
    public void Dispose()
    {
        if(!_configured||_view.IsDisposed||_view.CoreWebView2 is null)return;
        var core=_view.CoreWebView2;
        core.NavigationStarting-=NavigationStarting;core.NavigationCompleted-=NavigationCompleted;
        core.WebMessageReceived-=WebMessageReceived;core.NewWindowRequested-=NewWindowRequested;core.ProcessFailed-=ProcessFailed;
    }
}
