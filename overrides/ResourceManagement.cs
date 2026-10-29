namespace NiyazisugarItemci;
public sealed partial class MainForm
{
    private bool _messagePollBusy;
    private async Task MonitorItemciAsync()
    {
        if(_messagePollBusy||IsDisposed||Disposing||!_webReady||_webView.CoreWebView2 is null)return;
        _messagePollBusy=true;
        Microsoft.Web.WebView2.WinForms.WebView2? monitor=null;
        try
        {
            var messageTab=_browserTabs.FirstOrDefault(t=>t.WebView.CoreWebView2 is not null&&BrowserTabRules.Family(TabLocation(t))=="messages");
            monitor=messageTab?.WebView??_messageMonitorWebView;
            bool shared=messageTab is not null;
            if(shared&&_messageMonitorWebView.CoreWebView2 is { } dedicated&&dedicated.Source!="about:blank")
            {
                _messageMonitorReady=false;_loadedMonitorUrl=null;
                dedicated.Navigate("about:blank");
            }
            _webView.CoreWebView2.Resume();monitor.CoreWebView2?.Resume();
            await LegacyMonitorItemciAsync(monitor,shared);
        }
        catch(Exception ex){_lastDiagnosticsJson="Mesaj kontrolü: "+ex.Message;}
        finally
        {
            _messagePollBusy=false;
            foreach(var tab in _browserTabs.ToArray())if(!tab.WebView.Visible)await SuspendHiddenTabAsync(tab);
            if(monitor is not null&&ReferenceEquals(monitor,_messageMonitorWebView))
            {
                try{await monitor.CoreWebView2.TrySuspendAsync();}catch{}
            }
        }
    }
    private void InstallResourceManagement()
    {
        _messageMonitorWebView.Visible=false;
        _marketPanel.VisibleChanged+=(_,_)=>
        {
            if(_marketPanel.Visible)_marketWebView.CoreWebView2?.Resume();
            else _=SuspendIdleMarketAsync();
        };
        _browserPanel.VisibleChanged+=(_,_)=>
        {
            foreach(var tab in _browserTabs)
            {
                if(_browserPanel.Visible&&tab.WebView.Visible)tab.WebView.CoreWebView2?.Resume();
                else _=SuspendHiddenTabAsync(tab);
            }
        };
        FormClosed+=(_,_)=>{_watchTitleFont.Dispose();_watchInfoFont.Dispose();};
    }
    private async Task SuspendIdleMarketAsync()
    {
        try
        {
            var core=_marketWebView.CoreWebView2;
            if(core is null||_marketPanel.Visible||_liveBusy||_watchUnitsBusy||_composerAddingServer)return;
            await core.TrySuspendAsync();
            if(_marketPanel.Visible||_liveBusy||_watchUnitsBusy||_composerAddingServer)core.Resume();
        }
        catch { }
    }
}
