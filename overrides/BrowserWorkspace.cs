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
        var addressHost=_browserAddressBox.Parent;
        var address = addressHost?.Parent;
        if (tabs is null || address is null) throw new InvalidOperationException("Tarayıcı araç çubukları bulunamadı.");
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = new Padding(0), Padding = new Padding(0), BackColor = Bg };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, LogicalToDeviceUnits(44)));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, LogicalToDeviceUnits(46)));
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
        // Sekme satırı: zemine karışan sekmeler, seçili sekme yuvarlak bir hap; sağda sade "+".
        tabs.BackColor=ReferenceUi.Background;tabs.Padding=new Padding(LogicalToDeviceUnits(12),LogicalToDeviceUnits(6),LogicalToDeviceUnits(12),LogicalToDeviceUnits(2));
        _browserTabStrip.BackColor=ReferenceUi.Background;_browserTabStrip.Padding=new Padding(0);
        foreach(var plus in tabs.Controls.OfType<ReferenceButton>())
        {
            plus.Text="";plus.Icon="plus";plus.IconOnly=true;plus.Navigation=true;plus.IconSize=16;plus.Width=LogicalToDeviceUnits(34);plus.ForeColor=ReferenceUi.Muted;plus.BackColor=ReferenceUi.Background;plus.Font=ReferenceUi.Font(9);_sidebarToolTip.SetToolTip(plus,"Yeni sekme");
        }
        // Araç satırı: ikon düğmeleri, yuvarlak adres alanı; Enter ile gidildiği için ayrı "Git" düğmesi yok.
        address.Controls.SetChildIndex(addressHost!,0);
        address.Padding=new Padding(LogicalToDeviceUnits(10),LogicalToDeviceUnits(5),LogicalToDeviceUnits(12),LogicalToDeviceUnits(7));address.BackColor=ReferenceUi.Background;
        address.Paint+=(_,e)=>{using var pen=new Pen(ReferenceUi.Line);e.Graphics.DrawLine(pen,0,address.Height-1,address.Width,address.Height-1);};
        foreach(var go in address.Controls.OfType<ReferenceButton>().Where(b=>b.Text=="Git").ToArray())go.Visible=false;
        foreach(var tools in address.Controls.OfType<FlowLayoutPanel>())
        {
            int count=tools.Controls.Count;tools.Width=count*LogicalToDeviceUnits(34)+LogicalToDeviceUnits(10);tools.Padding=new Padding(0);
        }
        addressHost!.Controls.Remove(_browserAddressBox);addressHost.Padding=new Padding(0);addressHost.BackColor=ReferenceUi.Background;
        _browserAddressBox.Font=ReferenceUi.Font(9.5f);
        var roundedAddress=new ReferenceSearch(_browserAddressBox) {Dock=DockStyle.Fill,Icon="lock"};addressHost.Controls.Add(roundedAddress);
        // The report already owns its reference header and actions. Keep its
        // separate server chooser's JavaScript and styling intact.
        foreach(Control chrome in _marketPanel.Controls)if(!ReferenceEquals(chrome,_marketWebView))chrome.Visible=false;

    }

    private Button BrowserToolButton(string text,string tooltip)
    {
        var icon=text switch {"←"=>"back","→"=>"next","↻"=>"refresh","⌂"=>"store","🛡"=>"shield",_=>""};
        var button=new ReferenceButton {Text=text=="Git"?"Git":"",Icon=icon,IconOnly=text!="Git",Navigation=text!="Git",IconSize=18,Width=LogicalToDeviceUnits(32),Height=LogicalToDeviceUnits(34),ForeColor=ReferenceUi.Muted,BackColor=ReferenceUi.Background,Margin=new Padding(0,0,LogicalToDeviceUnits(2),0)};_sidebarToolTip.SetToolTip(button,tooltip);return button;
    }
    private Panel CreateBrowserTabControl(BrowserTabState tab)
    {
        var panel=new ReferenceTab(()=>ReferenceEquals(_activeBrowserTab,tab)) {Width=LogicalToDeviceUnits(200),Height=LogicalToDeviceUnits(34),Margin=new Padding(0,0,LogicalToDeviceUnits(4),0),Padding=new Padding(LogicalToDeviceUnits(6),LogicalToDeviceUnits(3),LogicalToDeviceUnits(4),LogicalToDeviceUnits(3))};
        var title=new ReferenceButton {Text=ShortTabTitle(tab.Title),Icon=TabIcon(tab),IconSize=15,Navigation=true,Dock=DockStyle.Fill,Font=ReferenceUi.Font(9),ForeColor=ReferenceUi.Text};title.Click+=(_,_)=>SelectBrowserTab(tab);
        var close=new ReferenceButton {Text="",Icon="close",IconOnly=true,Navigation=true,IconSize=12,Dock=DockStyle.Right,Width=LogicalToDeviceUnits(26),ForeColor=ReferenceUi.Muted};_sidebarToolTip.SetToolTip(close,"Sekmeyi kapat");close.Click+=(_,_)=>CloseBrowserTab(tab);panel.Controls.Add(title);panel.Controls.Add(close);tab.TitleButton=title;return panel;
    }

    private static string TabIcon(BrowserTabState tab)
    {
        var where=(tab.RequestedUrl+" "+tab.Title).ToLowerInvariant();
        return where.Contains("mesaj")?"message":where.Contains("bildirim")?"bell":where.Contains("google")?"search":"store";
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
