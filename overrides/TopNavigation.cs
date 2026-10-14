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
        _topNavigation = new Panel { Dock = DockStyle.Top, Height = 78, BackColor = Color.FromArgb(21, 24, 30), Padding = new Padding(16, 10, 16, 10) };
        _topNavigation.Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(48, 52, 62));
            e.Graphics.DrawLine(pen, 0, _topNavigation.Height - 1, _topNavigation.Width, _topNavigation.Height - 1);
        };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, BackColor = Color.Transparent, Margin = new Padding(0) };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 52));
        var brand = new Label { Text = "NiyaziSUGAR", Dock = DockStyle.Fill, ForeColor = Accent, Font = new Font("Segoe UI", 12, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft, Cursor = Cursors.Hand, Margin = new Padding(0) };
        brand.Click += (_, _) => ShowPanel(_browserPanel);
        var branding = new Panel { Dock = DockStyle.Fill, Padding = new Padding(44,0,0,0) };
        branding.Controls.Add(brand);
        branding.Controls.Add(new BrandMark { Location = new Point(0,7), Size = new Size(36,36) });
        layout.Controls.Add(branding, 0, 0);
        var menu = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, WrapContents = false, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 6, 0, 0), Margin = new Padding(0), BackColor = Color.Transparent };
        Button Section(string text, int width, Panel panel, Action click)
        {
            var button = TopButton(text, width, click);
            _topSections.Add(button, panel);
            menu.Controls.Add(button);
            return button;
        }
        Section("Itemci", 72, _browserPanel, () => ShowPanel(_browserPanel));
        _topMessages = TopButton("Mesajlar", 105, () =>
        {
            ShowPanel(_browserPanel);
            _ = OpenOrActivateBrowserTabAsync(_messageUrl ?? "https://itemci.com/panel/mesajlar", "Mesajlar");
        });
        menu.Controls.Add(_topMessages);
        Section("İlanlarım", 85, _listingsPanel, () => { ShowPanel(_listingsPanel); _ = RefreshCurrentListingsAsync(true); });
        Section("Satışlar", 78, _salesPanel, () => { ShowPanel(_salesPanel); _ = RefreshSoldListingsAsync(true); });
        Section("Piyasa Analiz", 116, _marketPanel, () =>
        {
            ShowPanel(_marketPanel);
            _ = OpenEmbeddedMarketAnalyzerAsync();
        });
        var create = TopButton("Hızlı İlan Ekle", 130, () => _ = ShowListingComposerAsync());
        create.ForeColor = Color.FromArgb(10,38,38);
        create.BackColor = PremiumTheme.Turquoise;
        menu.Controls.Add(create);
        layout.Controls.Add(menu, 1, 0);
        var utility = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 6, 0, 0), Margin = new Padding(0), BackColor = Color.Transparent };
        var settings = TopButton("⚙", 42, () => ShowPanel(_settingsPanel));
        settings.Text="\uE713";settings.Font=new Font("Segoe MDL2 Assets",14);
        _topSections.Add(settings, _settingsPanel);
        _sidebarToolTip.SetToolTip(settings, "Ayarlar ve güncellemeler");
        utility.Controls.Add(settings);
        layout.Controls.Add(utility, 2, 0);
        _topNavigation.Controls.Add(layout);
        Controls.Add(_topNavigation);
        _topNavigation.BringToFront();
        _unreadLabel.TextChanged += (_, _) =>
        {
            if (_topMessages is not null)
            {
                var value = _unreadLabel.Text;
                _topMessages.Text = value is "0" or "—" or "-" or "" ? "✉ Mesajlar" : "✉ " + value;
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
        var button = new Button { Text = text, Width = width, Height = 38, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(26, 29, 36), ForeColor = TextMain, Font = new Font("Segoe UI", 9, FontStyle.Bold), Cursor = Cursors.Hand, Margin = new Padding(0, 0, 4, 0), AutoEllipsis = true };
        button.FlatAppearance.BorderSize = 0;
        button.Paint += (_, e) => { if (_topSections.TryGetValue(button, out var target) && ReferenceEquals(target, _activeTopPanel)) { using var line = new Pen(PremiumTheme.Gold, 2); e.Graphics.DrawLine(line, 8, button.Height-2, button.Width-8, button.Height-2); } };
        button.Click += (_, _) => click();
        return button;
    }

    private void SelectTopSection(Panel panel)
    {
        _activeTopPanel = panel;
        foreach (var entry in _topSections)
        {
            bool selected = ReferenceEquals(entry.Value, panel);
            entry.Key.BackColor = Color.FromArgb(26, 29, 36);
            entry.Key.ForeColor = selected ? PremiumTheme.Gold : TextMain;
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
