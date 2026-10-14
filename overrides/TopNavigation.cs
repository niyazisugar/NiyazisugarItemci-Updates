namespace NiyazisugarItemci;

public sealed partial class MainForm
{
    private Panel? _topNavigation;
    private Panel? _activeTopPanel;
    private readonly Dictionary<Button, Panel> _topSections = new();
    private readonly ContextMenuStrip _topMarketMenu = new();
    private readonly ContextMenuStrip _topCreateMenu = new();
    private Button? _topMessages;

    private void InstallTopNavigation()
    {
        Controls.Remove(_sidebar);
        _sidebar.Visible = false;
        _topNavigation = new Panel { Dock = DockStyle.Top, Height = 76, BackColor = ReferenceUi.Background, Padding = new Padding(24, 8, 24, 6) };
        _topNavigation.Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(48, 52, 62));
            e.Graphics.DrawLine(pen, 0, _topNavigation.Height - 1, _topNavigation.Width, _topNavigation.Height - 1);
        };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, BackColor = Color.Transparent, Margin = new Padding(0) };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 270));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,56));
        var branding=new ReferenceBrand {Dock=DockStyle.Fill,Cursor=Cursors.Hand};branding.Click+=(_,_)=>ShowPanel(_browserPanel);layout.Controls.Add(branding,0,0);
        var menu = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, WrapContents = false, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 6, 0, 0), Margin = new Padding(0), BackColor = Color.Transparent };
        Button Section(string text, int width, Panel panel, Action click)
        {
            var button = TopButton(text, width, click);
            _topSections.Add(button, panel);
            menu.Controls.Add(button);
            return button;
        }
        Section("Itemci", 102, _browserPanel, () => { ShowPanel(_browserPanel); var siteTab=_browserTabs.FirstOrDefault(t=>BrowserTabRules.Family(TabLocation(t))!="messages");if(siteTab is not null)SelectBrowserTab(siteTab); });
        _topMessages = TopButton("Mesajlar", 120, () =>
        {
            ShowPanel(_browserPanel);
            _ = OpenOrActivateBrowserTabAsync(_messageUrl ?? "https://itemci.com/panel/mesajlar", "Mesajlar");
        });
        _topSections.Add(_topMessages,_browserPanel);
        menu.Controls.Add(_topMessages);
        Section("İlanlarım", 120, _listingsPanel, () => { ShowPanel(_listingsPanel); _ = RefreshCurrentListingsAsync(true); });
        Section("Satışlar", 108, _salesPanel, () => { ShowPanel(_salesPanel); _ = RefreshSoldListingsAsync(true); });
        Section("Piyasa Analizi", 156, _marketPanel, () =>
        {
            ShowPanel(_marketPanel);
            _ = OpenEmbeddedMarketAnalyzerAsync();
        });
        var create = TopButton("Hızlı İlan Ekle", 130, () => _ = ShowListingComposerAsync());
        create.ForeColor = Color.FromArgb(10,38,38);
        create.BackColor = PremiumTheme.Turquoise;
        ((ReferenceButton)create).Navigation=false;((ReferenceButton)create).Primary=true;create.Font=ReferenceUi.Bold(11);((ReferenceButton)create).Icon="plus";create.Height=44;create.Width=178;create.Margin=new Padding(0,7,8,0);
        layout.Controls.Add(create,2,0);
        layout.Controls.Add(menu, 1, 0);
        var utility = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 6, 0, 0), Margin = new Padding(0), BackColor = Color.Transparent };
        var settings = TopButton("⚙", 42, () => ShowPanel(_settingsPanel));
        settings.Text="";((ReferenceButton)settings).Navigation=false;((ReferenceButton)settings).Icon="gear";settings.Height=44;settings.Width=44;
        _topSections.Add(settings, _settingsPanel);
        _sidebarToolTip.SetToolTip(settings, "Ayarlar ve güncellemeler");
        utility.Controls.Add(settings);
        layout.Controls.Add(utility, 3, 0);
        void SizeMenu() {
            var compact=ClientSize.Width<1250;
            layout.ColumnStyles[0].Width=LogicalToDeviceUnits(compact?220:270);
            int[] widths=compact?new[]{80,96,98,88,148}:new[]{102,120,120,108,156};
            for(int i=0;i<5;i++)menu.Controls[i].Width=LogicalToDeviceUnits(widths[i]);
        }
        SizeChanged+=(_,_)=>SizeMenu();SizeMenu();
        _topNavigation.Controls.Add(layout);
        Controls.Add(_topNavigation);
        _topNavigation.BringToFront();
        _unreadLabel.TextChanged += (_, _) =>
        {
            if (_topMessages is not null)
            {
                var value = _unreadLabel.Text;
                _topMessages.Text = value is "0" or "—" or "-" or "" ? "Mesajlar" : "Mesajlar · " + value;
                _sidebarToolTip.SetToolTip(_topMessages, "Mesajlar · " + value);
            }
        };
        foreach (var popup in new[] { _topMarketMenu, _topCreateMenu })
        {
            popup.BackColor = Color.FromArgb(26, 29, 36);
            popup.ForeColor = Color.FromArgb(241, 242, 244);
            popup.ShowImageMargin = false;
            popup.Font = new Font("Segoe UI", 10);
            popup.Renderer = new ToolStripProfessionalRenderer(new TopMenuColors());
        }
        FormClosed += (_, _) => { _topMarketMenu.Dispose(); _topCreateMenu.Dispose(); };
        RebuildSidebarNavigation();
        SelectTopSection(_browserPanel);
        ShowPanel(_browserPanel);
    }

    private Button TopButton(string text, int width, Action click)
    {
        var icon=text switch {"Itemci"=>"store","Mesajlar"=>"message","İlanlarım"=>"list","Satışlar"=>"cart","Piyasa Analizi"=>"chart",_=>""};
        var button=new ReferenceButton {Text=text,Icon=icon,Navigation=true,Width=width,Height=52,ForeColor=ReferenceUi.Muted,Margin=new Padding(0,0,2,0),Font=ReferenceUi.Font(10)};
        button.Click += (_, _) => click();
        return button;
    }

    private void SelectTopSection(Panel panel)
    {
        _activeTopPanel = panel;
        var currentTab=_browserTabs.FirstOrDefault(t=>ReferenceEquals(t.WebView,_webView));
        var messageActive=ReferenceEquals(panel,_browserPanel) && BrowserTabRules.Family(currentTab is null ? (_webView.Source?.ToString()??"") : TabLocation(currentTab))=="messages";
        foreach (var entry in _topSections)
        {
            bool selected = ReferenceEquals(entry.Value, panel) && (!ReferenceEquals(panel,_browserPanel) || ReferenceEquals(entry.Key,_topMessages)==messageActive);
            entry.Key.BackColor = ReferenceUi.Background;
            ((ReferenceButton)entry.Key).Selected=selected;
            entry.Key.ForeColor = selected ? PremiumTheme.Gold : ReferenceUi.Muted;
            entry.Key.Invalidate();
        }
    }

    private void RebuildSidebarNavigation()
    {
        if (_topNavigation is null) { LegacyRebuildSidebarNavigation(); return; }
        foreach (var popup in new[] { _topMarketMenu, _topCreateMenu })
        {
            while (popup.Items.Count > 0) { var old = popup.Items[0]; popup.Items.RemoveAt(0); old.Dispose(); }
        }
        _topMarketMenu.Items.Add("Analiz ve sunucu yönetimi", null, (_, _) => { ShowPanel(_marketPanel); _ = OpenEmbeddedMarketAnalyzerAsync(); });
        _topMarketMenu.Items.Add("＋ Sunucu ekle", null, (_, _) => { ShowPanel(_marketPanel); _ = OpenMarketAddServerAsync(); });
        _topMarketMenu.Items.Add(new ToolStripSeparator());
        foreach (var server in _marketServers.OrderByDescending(s => s.IsDefault).ThenBy(s => s.Name))
        {
            var group = new ToolStripMenuItem(server.Name);
            group.DropDownItems.Add("Analizi çalıştır", null, (_, _) => { ShowPanel(_marketPanel); _ = RunMarketServerAsync(server.Id, server.Name); });
            group.DropDownItems.Add("Sunucuyu düzenle", null, (_, _) => { ShowPanel(_marketPanel); _ = EditMarketServerAsync(server.Id); });
            if (!server.IsDefault) group.DropDownItems.Add("Sunucuyu sil", null, (_, _) => _ = DeleteMarketServerAsync(server.Id, server.Name));
            group.DropDown.BackColor = Color.FromArgb(26, 29, 36);
            group.DropDown.ForeColor = TextMain;
            _topMarketMenu.Items.Add(group);
            _topCreateMenu.Items.Add(server.Name + " · İlan oluştur", null, (_, _) => _ = OpenServerListingCreateAsync(server));
        }
        RefreshLiveMarketServers();
        if (_activeTopPanel is not null) SelectTopSection(_activeTopPanel);
    }

    private sealed class TopMenuColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Color.FromArgb(26, 29, 36);
        public override Color MenuItemSelected => Color.FromArgb(59, 49, 32);
        public override Color MenuItemBorder => Color.FromArgb(216, 180, 106);
        public override Color MenuBorder => Color.FromArgb(48, 52, 62);
        public override Color SeparatorDark => Color.FromArgb(48, 52, 62);
        public override Color SeparatorLight => Color.FromArgb(48, 52, 62);
    }
}
