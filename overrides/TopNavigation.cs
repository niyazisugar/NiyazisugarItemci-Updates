using System.Drawing.Drawing2D;

namespace NiyazisugarItemci;

// Üst menü çubuğu: yuvarlak köşeli tek bir çubuk içinde solda marka, ortada hap şeklinde
// bölümler (Itemci · Mesajlar · İlanlarım · Satışlar · Piyasa · Takip), sağda altın
// "+ Hızlı İlan" ve yuvarlak ayarlar düğmesi.
public sealed partial class MainForm
{
    private Panel? _topNavigation;
    private Panel? _activeTopPanel;
    private readonly Dictionary<Button, Panel> _topSections = new();
    private readonly ContextMenuStrip _topMarketMenu = new();
    private readonly ContextMenuStrip _topCreateMenu = new();
    private Button? _topMessages;
    private readonly List<ReferenceButton> _navButtons = new();

    private static readonly Color TopBarColor = Color.FromArgb(24, 25, 28);

    private void InstallTopNavigation()
    {
        Controls.Remove(_sidebar);
        _sidebar.Visible = false;

        _topNavigation = new Panel
        {
            Dock = DockStyle.Top,
            Height = LogicalToDeviceUnits(74),
            BackColor = ReferenceUi.Background,
            Padding = new Padding(LogicalToDeviceUnits(16), LogicalToDeviceUnits(14), LogicalToDeviceUnits(16), LogicalToDeviceUnits(6))
        };

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, BackColor = TopBarColor, Margin = new Padding(0), Padding = new Padding(LogicalToDeviceUnits(12), LogicalToDeviceUnits(7), LogicalToDeviceUnits(8), LogicalToDeviceUnits(7)) };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, LogicalToDeviceUnits(158)));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        // Çubuğun yuvarlak köşeleri: içerik bölgesi kırpılır, kenarlık dış panele çizilir.
        void RoundBar()
        {
            if (layout.Width <= 0 || layout.Height <= 0) return;
            using var path = ReferenceUi.Shape(new RectangleF(0, 0, layout.Width, layout.Height), layout.Height / 2.6f);
            var old = layout.Region; layout.Region = new Region(path); old?.Dispose();
            _topNavigation.Invalidate();
        }
        layout.SizeChanged += (_, _) => RoundBar();
        _topNavigation.Paint += (_, e) =>
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(layout.Left - .5f, layout.Top - .5f, layout.Width, layout.Height);
            using var path = ReferenceUi.Shape(r, layout.Height / 2.6f);
            using var pen = new Pen(Color.FromArgb(37, 38, 42));
            g.DrawPath(pen, path);
        };

        var branding = new ReferenceBrand { Dock = DockStyle.Fill, Cursor = Cursors.Hand, BackColor = TopBarColor, Compact = true, Margin = new Padding(0) };
        branding.Click += (_, _) => ShowPanel(_browserPanel);
        layout.Controls.Add(branding, 0, 0);

        var menu = new ReferencePillGroup { Anchor = AnchorStyles.Left, Padding = new Padding(LogicalToDeviceUnits(4)), Margin = new Padding(0, LogicalToDeviceUnits(1), 0, 0) };

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
        var market = Section("Piyasa", _marketPanel, () => { ShowPanel(_marketPanel); _ = OpenEmbeddedMarketAnalyzerAsync(); });
        var tracking = Section("Takip", _serverSalesPanel, () => { ShowPanel(_serverSalesPanel); RenderServerSalesWatch(); _ = RefreshLiveMarketAsync(); });
        _sidebarToolTip.SetToolTip(market, "Piyasa Analizi");
        _sidebarToolTip.SetToolTip(tracking, "Satış Takibi");
        layout.Controls.Add(menu, 1, 0);

        var actions = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, FlowDirection = FlowDirection.LeftToRight, BackColor = TopBarColor, Margin = new Padding(0, LogicalToDeviceUnits(3), 0, 0), Anchor = AnchorStyles.Right };
        var create = (ReferenceButton)TopButton("+ Hızlı İlan", 0, () => _ = ShowListingComposerAsync());
        create.Primary = true;
        create.Font = ReferenceUi.Semibold(9);
        create.BackColor = TopBarColor;
        create.Width = create.PillWidth() + LogicalToDeviceUnits(6);
        create.Margin = new Padding(0, 0, LogicalToDeviceUnits(8), 0);
        var settings = (ReferenceButton)TopButton("", 0, () => ShowPanel(_settingsPanel));
        settings.Icon = "gear"; settings.IconOnly = true; settings.IconSize = 15; settings.BackColor = TopBarColor;
        settings.Width = settings.Height; settings.Margin = new Padding(0);
        _sidebarToolTip.SetToolTip(settings, "Ayarlar ve güncellemeler");
        _topSections.Add(settings, _settingsPanel);
        actions.Controls.Add(create);
        actions.Controls.Add(settings);
        layout.Controls.Add(actions, 3, 0);

        _topNavigation.Controls.Add(layout);
        Controls.Add(_topNavigation);
        _topNavigation.BringToFront();

        _unreadLabel.TextChanged += (_, _) =>
        {
            if (_topMessages is ReferenceButton messages)
            {
                var value = _unreadLabel.Text;
                messages.Badge = value is "0" or "—" or "-" or "" ? "" : value;
                messages.Width = messages.PillWidth();
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
        RebuildSidebarNavigation();
        SelectTopSection(_browserPanel);
        ShowPanel(_browserPanel);
    }

    private Button TopButton(string text, int width, Action click)
    {
        var button = new ReferenceButton
        {
            Text = text,
            Pill = true,
            Navigation = true,
            Height = LogicalToDeviceUnits(32),
            ForeColor = ReferenceUi.Muted,
            BackColor = ReferenceUi.Background,
            Margin = new Padding(0, 0, LogicalToDeviceUnits(2), 0),
            Font = ReferenceUi.Font(9),
            TabStop = true
        };
        button.Width = width > 0 ? width : button.PillWidth();
        button.Click += (_, _) => click();
        _navButtons.Add(button);
        return button;
    }

    private void SelectTopSection(Panel panel)
    {
        _activeTopPanel = panel;
        var currentTab = _browserTabs.FirstOrDefault(t => ReferenceEquals(t.WebView, _webView));
        var messageActive = ReferenceEquals(panel, _browserPanel) && BrowserTabRules.Family(currentTab is null ? (_webView.Source?.ToString() ?? "") : TabLocation(currentTab)) == "messages";
        foreach (var entry in _topSections)
        {
            bool selected = ReferenceEquals(entry.Value, panel) && (!ReferenceEquals(panel, _browserPanel) || ReferenceEquals(entry.Key, _topMessages) == messageActive);
            var button = (ReferenceButton)entry.Key;
            button.Selected = selected;
            // Yazı tipi değişince hap genişliği de değişir; seçili öğe biraz daha belirgin.
            var font = selected ? ReferenceUi.Semibold(9) : ReferenceUi.Font(9);
            if (!button.IconOnly && !ReferenceEquals(button.Font, font)) { button.Font = font; button.Width = button.PillWidth(); }
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
        public override Color MenuItemSelected => Color.FromArgb(40, 41, 47);
        public override Color MenuItemBorder => ReferenceUi.Gold;
        public override Color MenuBorder => ReferenceUi.Line;
        public override Color SeparatorDark => ReferenceUi.Line;
        public override Color SeparatorLight => ReferenceUi.Line;
    }
}
