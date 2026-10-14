namespace NiyazisugarItemci;

public sealed partial class MainForm
{
    private readonly System.Threading.SemaphoreSlim _browserOpenGate = new(1, 1);

    private void InstallBrowserWorkspace()
    {
        // Docked fill content must be first in z-order so top navigation
        // receives its own space rather than covering the browser toolbars.
        _content.BringToFront();
        var tabs = _browserTabStrip.Parent;
        var address = _browserAddressBox.Parent?.Parent;
        if (tabs is null || address is null) throw new InvalidOperationException("Tarayıcı araç çubukları bulunamadı.");
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = new Padding(0), Padding = new Padding(0), BackColor = Bg };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        foreach (Control control in new[] { tabs, address, _browserHost }) { control.Dock = DockStyle.Fill; control.Margin = new Padding(0); }
        layout.Controls.Add(tabs, 0, 0);
        layout.Controls.Add(address, 0, 1);
        layout.Controls.Add(_browserHost, 0, 2);
        _browserPanel.Controls.Add(layout);
        layout.BringToFront();
        // Reserve space for the + button instead of letting the tab strip overlap it.
        tabs.Controls.SetChildIndex(_browserTabStrip, 0);
        _browserTabStrip.AutoScroll = true;
        address.Controls.SetChildIndex(_browserAddressBox.Parent!, 0);
    }

    private Task<BrowserTabState?> OpenOrActivateBrowserTabAsync(string url, string title) => OpenSmartBrowserTabAsync(url, title, true);

    private static string TabLocation(BrowserTabState tab) =>
        tab.WebView.Source?.ToString() is { Length: > 0 } source && source != "about:blank" ? source : tab.RequestedUrl;

    private async Task<BrowserTabState?> OpenSmartBrowserTabAsync(string url, string title, bool activate, bool forceNew = false)
    {
        await _browserOpenGate.WaitAsync();
        try
        {
            if (IsDisposed || _browserEnvironment is null) return null;
            if (!forceNew)
            {
                var existing = _browserTabs.FirstOrDefault(tab => BrowserTabRules.SamePage(TabLocation(tab), url));
                var family = BrowserTabRules.Family(url);
                if (existing is null && family.Length > 0)
                    existing = _browserTabs.FirstOrDefault(tab => BrowserTabRules.Family(TabLocation(tab)) == family);
                if (existing is not null)
                {
                    // A general Messages click preserves the current conversation.
                    // A specific conversation link navigates the same message tab.
                    if (family == "messages" && !BrowserTabRules.IsMessageLanding(url) && !BrowserTabRules.SamePage(TabLocation(existing), url))
                    {
                        existing.RequestedUrl = url;
                        existing.WebView.Source = new Uri(url);
                    }
                    if (activate) { ShowPanel(_browserPanel); SelectBrowserTab(existing); }
                    return existing;
                }
            }
            var created = await CreateBrowserTabAsync(url, title, activate);
            if (created is not null) created.RequestedUrl = url;
            if (activate && created is not null) ShowPanel(_browserPanel);
            return created;
        }
        finally { _browserOpenGate.Release(); }
    }

    private async Task SuspendHiddenTabAsync(BrowserTabState tab)
    {
        try
        {
            var core = tab.WebView.CoreWebView2;
            if (core is null || tab.WebView.Visible) return;
            await core.TrySuspendAsync();
            // Selection may have changed while suspension was pending.
            if (tab.WebView.Visible) core.Resume();
        }
        catch { }
    }
}
