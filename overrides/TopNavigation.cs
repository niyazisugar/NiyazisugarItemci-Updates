namespace NiyazisugarItemci;

// Sol kenar menüsü. Açıkken ikon + isim, daraltılınca yalnızca ikonlar görünür
// ve içerik alanı genişler. Tercih AppSettings.SidebarCollapsed içinde saklanır.
// (Alan adları eski üst menüden kalmıştır; diğer dosyalar ve testler bunlara bağlı.)
public sealed partial class MainForm
{
    private Panel? _topNavigation;
    private Panel? _activeTopPanel;
    private readonly Dictionary<Button, Panel> _topSections = new();
    private readonly ContextMenuStrip _topMarketMenu = new();
    private readonly ContextMenuStrip _topCreateMenu = new();
    private Button? _topMessages;
    private ReferenceButton? _navToggle;
    private FlowLayoutPanel? _navMenu;
    private readonly List<ReferenceButton> _navButtons = new();
    private bool _navCollapsed;

    private const int NavExpandedWidth = 232;
    private const int NavCollapsedWidth = 72;

    private void InstallTopNavigation()
    {
        Controls.Remove(_sidebar);
        _sidebar.Visible = false;
        _navCollapsed = _settings.SidebarCollapsed;

        _topNavigation = new Panel
        {
            Dock = DockStyle.Left,
            Width = LogicalToDeviceUnits(_navCollapsed ? NavCollapsedWidth : NavExpandedWidth),
            BackColor = ReferenceUi.Sidebar,
            Padding = new Padding(LogicalToDeviceUnits(12), LogicalToDeviceUnits(14), LogicalToDeviceUnits(12), LogicalToDeviceUnits(12))
        };
        _topNavigation.Paint += (_, e) =>
        {
            using var pen = new Pen(ReferenceUi.Line);
            e.Graphics.DrawLine(pen, _topNavigation.Width - 1, 0, _topNavigation.Width - 1, _topNavigation.Height);
        };

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = Color.Transparent, Margin = new Padding(0), Padding = new Padding(0) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, LogicalToDeviceUnits(68)));   // marka
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));                         // bölümler
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, LogicalToDeviceUnits(52)));   // hızlı ilan
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, LogicalToDeviceUnits(100)));  // ayarlar + daralt

        var branding = new ReferenceBrand { Dock = DockStyle.Fill, Cursor = Cursors.Hand, BackColor = ReferenceUi.Sidebar, Subtitle = "Satıcı alanı", Margin = new Padding(0) };
        branding.Click += (_, _) => ShowPanel(_browserPanel);
        layout.Controls.Add(branding, 0, 0);

        var menu = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = false, WrapContents = false, FlowDirection = FlowDirection.TopDown, Padding = new Padding(0, LogicalToDeviceUnits(6), 0, 0), Margin = new Padding(0), BackColor = Color.Transparent };
        _navMenu = menu;

        Button Section(string text, Panel panel, Action click)
        {
            var button = TopButton(text, 0, click);
            _topSections.Add(button, panel);
            menu.Controls.Add(button);
            return button;
        }
        Section("Itemci", _browserPanel, () => { ShowPanel(_browserPanel); var siteTab = _browserTabs.FirstOrDefault(t => BrowserTabRules.Family(TabLocation(t)) != "messages"); if (siteTab is not null) SelectBrowserTab(siteTab); });
        _topMessages = TopButton("Mesajlar", 0, () =>
        {
            ShowPanel(_browserPanel);
            _ = OpenOrActivateBrowserTabAsync(_messageUrl ?? "https://itemci.com/panel/mesajlar", "Mesajlar");
        });
        _topSections.Add(_topMessages, _browserPanel);
        menu.Controls.Add(_topMessages);
        Section("İlanlarım", _listingsPanel, () => { ShowPanel(_listingsPanel); _ = RefreshCurrentListingsAsync(true); });
        Section("Satışlar", _salesPanel, () => { ShowPanel(_salesPanel); _ = RefreshSoldListingsAsync(true); });
        Section("Piyasa Analizi", _marketPanel, () => { ShowPanel(_marketPanel); _ = OpenEmbeddedMarketAnalyzerAsync(); });
        Section("Satış Takibi", _serverSalesPanel, () => { ShowPanel(_serverSalesPanel); RenderServerSalesWatch(); _ = RefreshLiveMarketAsync(); });
        layout.Controls.Add(menu, 0, 1);

        var create = (ReferenceButton)TopButton("Hızlı İlan Ekle", 0, () => _ = ShowListingComposerAsync());
        create.Primary = true;
        create.Icon = "plus";
        create.Font = ReferenceUi.Bold(9.5f);
        create.Dock = DockStyle.Top;
        create.Margin = new Padding(0, LogicalToDeviceUnits(6), 0, 0);
        layout.Controls.Add(create, 0, 2);

        var utility = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Margin = new Padding(0), Padding = new Padding(0, LogicalToDeviceUnits(10), 0, 0) };
        utility.Paint += (_, e) =>
        {
            using var pen = new Pen(ReferenceUi.Line);
            e.Graphics.DrawLine(pen, 0, LogicalToDeviceUnits(2), utility.Width, LogicalToDeviceUnits(2));
        };
        var settings = (ReferenceButton)TopButton("Ayarlar", 0, () => ShowPanel(_settingsPanel));
        settings.Icon = "gear";
        settings.Dock = DockStyle.Top;
        _topSections.Add(settings, _settingsPanel);
        _navToggle = (ReferenceButton)TopButton("Menüyü daralt", 0, () => SetNavigationCollapsed(!_navCollapsed));
        _navToggle.Dock = DockStyle.Top;
        // Dock=Top: sonra eklenen üstte durur; ayarlar üstte, daralt düğmesi altta kalsın.
        utility.Controls.Add(_navToggle);
        utility.Controls.Add(new Panel { Dock = DockStyle.Top, Height = LogicalToDeviceUnits(4), BackColor = Color.Transparent });
        utility.Controls.Add(settings);
        layout.Controls.Add(utility, 0, 3);

        menu.Resize += (_, _) => SizeNavigationButtons();

        _topNavigation.Controls.Add(layout);
        Controls.Add(_topNavigation);
        _topNavigation.BringToFront();

        _unreadLabel.TextChanged += (_, _) =>
        {
            if (_topMessages is ReferenceButton messages)
            {
                var value = _unreadLabel.Text;
                messages.Badge = value is "0" or "—" or "-" or "" ? "" : value;
                _sidebarToolTip.SetToolTip(messages, messages.Badge.Length == 0 ? "Mesajlar" : "Mesajlar · " + value);
                messages.Invalidate();
            }
        };
        foreach (var popup in new[] { _topMarketMenu, _topCreateMenu })
        {
            popup.BackColor = ReferenceUi.Surface;
            popup.ForeColor = ReferenceUi.Text;
            popup.ShowImageMargin = false;
            popup.Font = ReferenceUi.Font(9.5f);
            popup.Renderer = new ToolStripProfessionalRenderer(new TopMenuColors());
        }
        FormClosed += (_, _) => { _topMarketMenu.Dispose(); _topCreateMenu.Dispose(); };
        ApplyNavigationState();
        RebuildSidebarNavigation();
        SelectTopSection(_browserPanel);
        ShowPanel(_browserPanel);
    }

    private Button TopButton(string text, int width, Action click)
    {
        var icon = text switch { "Itemci" => "store", "Mesajlar" => "message", "İlanlarım" => "list", "Satışlar" => "cart", "Piyasa Analizi" => "chart", "Satış Takibi" => "pulse", _ => "" };
        var button = new ReferenceButton
        {
            Text = text,
            Icon = icon,
            SideNav = true,
            Navigation = true,
            Width = width > 0 ? width : LogicalToDeviceUnits(NavExpandedWidth - 24),
            Height = LogicalToDeviceUnits(40),
            ForeColor = ReferenceUi.Muted,
            BackColor = ReferenceUi.Sidebar,
            Margin = new Padding(0, 0, 0, LogicalToDeviceUnits(4)),
            Font = ReferenceUi.Font(9.5f),
            TabStop = true
        };
        button.Click += (_, _) => click();
        _navButtons.Add(button);
        return button;
    }

    private void SetNavigationCollapsed(bool collapsed)
    {
        if (_navCollapsed == collapsed) return;
        _navCollapsed = collapsed;
        _settings.SidebarCollapsed = collapsed;
        _settings.Save();
        ApplyNavigationState();
    }

    private void ApplyNavigationState()
    {
        if (_topNavigation is null) return;
        SuspendLayout();
        _topNavigation.Width = LogicalToDeviceUnits(_navCollapsed ? NavCollapsedWidth : NavExpandedWidth);
        foreach (var button in _navButtons)
        {
            button.IconOnly = _navCollapsed;
            _sidebarToolTip.SetToolTip(button, _navCollapsed ? button.Text : "");
            button.Invalidate();
        }
        if (_navToggle is not null)
        {
            _navToggle.Icon = _navCollapsed ? "expand" : "collapse";
            _navToggle.Text = _navCollapsed ? "Menüyü genişlet" : "Menüyü daralt";
            _sidebarToolTip.SetToolTip(_navToggle, _navToggle.Text);
        }
        // Düğme genişliğini menüden önce ayarla: aksi halde dar menüde yatay kaydırma çubuğu belirir.
        SizeNavigationButtons();
        ResumeLayout(true);
        SizeNavigationButtons();
        _topNavigation.Invalidate(true);
    }

    private void SizeNavigationButtons()
    {
        if (_navMenu is null || _topNavigation is null) return;
        // Genişlik menünün o anki boyutundan değil, hedef kenar çubuğu genişliğinden hesaplanır.
        int target = LogicalToDeviceUnits(_navCollapsed ? NavCollapsedWidth : NavExpandedWidth) - _topNavigation.Padding.Horizontal;
        int width = Math.Max(LogicalToDeviceUnits(40), Math.Min(target, _navMenu.ClientSize.Width > 0 ? _navMenu.ClientSize.Width : target));
        foreach (Control control in _navMenu.Controls) control.Width = width;
    }

    private void SelectTopSection(Panel panel)
    {
        _activeTopPanel = panel;
        var currentTab = _browserTabs.FirstOrDefault(t => ReferenceEquals(t.WebView, _webView));
        var messageActive = ReferenceEquals(panel, _browserPanel) && BrowserTabRules.Family(currentTab is null ? (_webView.Source?.ToString() ?? "") : TabLocation(currentTab)) == "messages";
        foreach (var entry in _topSections)
        {
            bool selected = ReferenceEquals(entry.Value, panel) && (!ReferenceEquals(panel, _browserPanel) || ReferenceEquals(entry.Key, _topMessages) == messageActive);
            entry.Key.BackColor = ReferenceUi.Sidebar;
            ((ReferenceButton)entry.Key).Selected = selected;
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
            group.DropDown.BackColor = ReferenceUi.Surface;
            group.DropDown.ForeColor = ReferenceUi.Text;
            _topMarketMenu.Items.Add(group);
            _topCreateMenu.Items.Add(server.Name + " · İlan oluştur", null, (_, _) => _ = OpenServerListingCreateAsync(server));
        }
        RefreshLiveMarketServers();
        if (_activeTopPanel is not null) SelectTopSection(_activeTopPanel);
    }

    private sealed class TopMenuColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => ReferenceUi.Surface;
        public override Color MenuItemSelected => Color.FromArgb(58, 50, 36);
        public override Color MenuItemBorder => ReferenceUi.Gold;
        public override Color MenuBorder => ReferenceUi.Line;
        public override Color SeparatorDark => ReferenceUi.Line;
        public override Color SeparatorLight => ReferenceUi.Line;
    }
}
