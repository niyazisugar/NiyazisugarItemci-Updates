namespace NiyazisugarItemci;
public sealed partial class MainForm
{
    private void InstallResourceManagement()
    {
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
            if(core is null||_marketPanel.Visible||_liveBusy||_composerAddingServer)return;
            await core.TrySuspendAsync();
            if(_marketPanel.Visible||_liveBusy||_composerAddingServer)core.Resume();
        }
        catch { }
    }
}
