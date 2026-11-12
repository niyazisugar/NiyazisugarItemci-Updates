using System.Media;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using HtmlAgilityPack;
using System.Net;
using System.Text.RegularExpressions;

namespace NiyazisugarItemci;

public sealed partial class MainForm : Form
{
    private static readonly Color Bg = Color.FromArgb(17, 19, 24);
    private static readonly Color Side = Color.FromArgb(17, 20, 26);
    private static readonly Color Card = Color.FromArgb(26, 29, 36);
    private static readonly Color Border = Color.FromArgb(44, 50, 60);
    private static readonly Color Accent = Color.FromArgb(225, 193, 136);
    private static readonly Color Green = Color.FromArgb(52, 225, 132);
    private static readonly Color TextMain = Color.FromArgb(242, 244, 247);
    private static readonly Color TextMuted = Color.FromArgb(145, 154, 168);

    private readonly AppSettings _settings;
    private readonly NotifyIcon _trayIcon;
    private readonly System.Windows.Forms.Timer _monitorTimer;
    private readonly System.Windows.Forms.Timer _salesTimer;

    private readonly Panel _content = new();
    private readonly Panel _homePanel = new();
    private readonly Panel _browserPanel = new();
    private readonly Panel _marketPanel = new();
    private readonly Panel _salesPanel = new();
    private readonly Panel _settingsPanel = new();
    private readonly Panel _listingsPanel = new();

    private readonly Panel _sidebar = new();
    private readonly TableLayoutPanel _sidebarLayout = new();
    private readonly Panel _sidebarHeader = new();
    private readonly Label _sidebarBrand = new();
    private readonly Label _sidebarVersion = new();
    private readonly Button _sidebarCollapseButton = new();
    private readonly ToolTip _sidebarToolTip = new();
    private bool _sidebarCollapsed;

    private readonly FlowLayoutPanel _sidebarMainNav = new();
    private readonly FlowLayoutPanel _sidebarBottomNav = new();
    private readonly Label _marketTitleLabel = new();
    private readonly List<MarketServerInfo> _marketServers = new()
    {
        new MarketServerInfo
        {
            Id = "msp2-global-yeni",
            Name = "MSP2 Global - Yeni",
            CategoryCount = 3,
            IsDefault = true
        }
    };

    private readonly Label _connectionLabel = new();
    private readonly Label _unreadLabel = new();
    private readonly Label _detectorLabel = new();
    private readonly Label _lastCheckLabel = new();

    private readonly Label _salesGrossLabel = new();
    private readonly Label _salesCommissionLabel = new();
    private readonly Label _salesNetLabel = new();
    private readonly Label _salesCountLabel = new();
    private readonly Label _salesStatusLabel = new();
    private readonly DataGridView _salesGrid = new();

    private readonly Label _listingsStatusLabel = new();
    private readonly FlowLayoutPanel _listingsList = new();

    private WebView2 _webView = new();
    private CoreWebView2Environment? _browserEnvironment;
    private readonly Panel _browserHost = new();
    private readonly FlowLayoutPanel _browserTabStrip = new();
    private readonly TextBox _browserAddressBox = new();
    private readonly List<BrowserTabState> _browserTabs = new();
    private BrowserTabState? _activeBrowserTab;
    private int _browserTabSequence;
    private readonly WebView2 _messageMonitorWebView = new();
    private readonly WebView2 _marketWebView = new();

    private CheckBox? _notificationsCheck;
    private CheckBox? _soundCheck;
    private CheckBox? _startupCheck;
    private NumericUpDown? _reminderInput;
    private CheckBox? _adBlockCheck;
    private Button? _browserAdBlockButton;

    private CheckBox? _autoUpdateCheck;
    private Label? _updateCurrentLabel;
    private Label? _updateLatestLabel;
    private Label? _updateStatusLabel;
    private Label? _updateNotesLabel;
    private Button? _updateCheckButton;
    private Button? _updateInstallButton;
    private UpdateManifest? _availableUpdate;
    private bool _updateCheckRunning;

    private bool _allowExit;
    private bool _webReady;
    private bool _messageMonitorReady;
    private bool _marketWebReady;
    private bool _marketAnalyzerInjected;
    private string? _loadedMonitorUrl;

    private string? _pendingListingServerId;
    private string? _pendingListingServerName;
    private string? _pendingListingCategoryUrl;
    private int _pendingListingAttempts;
    private WebView2? _pendingListingWebView;
    private DateTime _lastMonitorReloadAt = DateTime.MinValue;
    private int _lastUnreadCount = -1;
    private readonly Dictionary<string, int> _lastConversationCounts = new(StringComparer.OrdinalIgnoreCase);
    private DateTime? _firstUnreadAt;
    private DateTime? _lastReminderAt;
    private string? _messageUrl;
    private string _lastDetectorNote = "Itemci bekleniyor";
    private bool _salesInitialized;
    private bool _historyFullyScanned;
    private readonly Dictionary<string, SaleRecord> _salesHistory =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _knownSaleOrderIds = new(StringComparer.OrdinalIgnoreCase);
    private string _lastDiagnosticsJson = "Tanılama verisi henüz oluşmadı.";

    public MainForm()
    {
        _settings = AppSettings.Load();
        _sidebarCollapsed = _settings.SidebarCollapsed;
        LoadSalesHistory();

        Text = "NiyaziSUGAR · Itemci";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1050, 680);
        Size = new Size(1320, 820);
        BackColor = Bg;
        ForeColor = TextMain;
        Font = new Font("Segoe UI", 9F);
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

        AutoScaleMode = AutoScaleMode.Dpi;
        BuildUi();
        InstallTopNavigation();
        InstallBrowserWorkspace();
        InstallLiveMarket();
        InstallListingComposer();
        InstallResourceManagement();
        InstallAutoBump();
        PremiumTheme.Apply(this);

        _trayIcon = BuildTrayIcon();

        _monitorTimer = new System.Windows.Forms.Timer
        {
            Interval = 10000
        };
        _monitorTimer.Tick += async (_, _) => await MonitorItemciAsync();

        _salesTimer = new System.Windows.Forms.Timer
        {
            Interval = 60000
        };
        _salesTimer.Tick += async (_, _) => await RefreshSoldListingsAsync(showErrors: false);

        Shown += async (_, _) =>
        {
            await InitializeWebViewAsync();
            if (IsDisposed || Disposing || _allowExit) return;
            _monitorTimer.Start();
            _salesTimer.Start();

            // İlk satış taramasını oturum hazırlandıktan kısa süre sonra yap.
            await Task.Delay(1800);
            if (IsDisposed || Disposing || _allowExit) return;
            _ = RefreshSoldListingsAsync(showErrors: false, fullHistory: false);

            if (_settings.AutoUpdateCheck)
                _ = CheckForUpdatesAsync(silent: true);

            if (Environment.GetCommandLineArgs().Any(x => x.Equals("--tray", StringComparison.OrdinalIgnoreCase)))
            {
                HideToTray(showInfo: false);
            }
        };

        Shown += (_, _) => { var area=Screen.FromControl(this).WorkingArea; MinimumSize=new Size(Math.Min(MinimumSize.Width,area.Width),Math.Min(MinimumSize.Height,area.Height)); if (Width>area.Width || Height>area.Height) Size=new Size(Math.Min(Width,area.Width),Math.Min(Height,area.Height)); }; FormClosing += OnFormClosing;
    }

    private void BuildUi()
    {
        // V2.6: kontrol merkezi artık SAĞ tarafta.
        // Site içeriğinin doğal sol gezinme düzeni korunuyor; bizim araçlarımız sağda kalıyor.
        _sidebar.Dock = DockStyle.Right;
        _sidebar.BackColor = Color.FromArgb(13, 16, 21);
        _sidebar.Padding = new Padding(_sidebarCollapsed ? 6 : 11);
        _sidebar.Width = _sidebarCollapsed ? 64 : 238;
        _sidebar.Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(38, 44, 54), 1);
            e.Graphics.DrawLine(pen, 0, 0, 0, _sidebar.ClientSize.Height);
        };

        _sidebarLayout.Dock = DockStyle.Fill;
        _sidebarLayout.ColumnCount = 1;
        _sidebarLayout.RowCount = 4;
        _sidebarLayout.BackColor = Color.Transparent;
        _sidebarLayout.Margin = new Padding(0);
        _sidebarLayout.Padding = new Padding(0);
        _sidebarLayout.RowStyles.Clear();
        _sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        _sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 98));
        _sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));

        _sidebarHeader.Dock = DockStyle.Fill;
        _sidebarHeader.BackColor = Color.Transparent;

        _sidebarBrand.Dock = DockStyle.Fill;
        _sidebarBrand.Text = "NiyaziSUGAR\nITEMCI HUB";
        _sidebarBrand.ForeColor = Accent;
        _sidebarBrand.Font = new Font("Segoe UI", 13.5F, FontStyle.Bold);
        _sidebarBrand.TextAlign = ContentAlignment.MiddleRight;
        _sidebarBrand.Padding = new Padding(34, 0, 4, 0);

        _sidebarCollapseButton.Width = 29;
        _sidebarCollapseButton.Height = 29;
        _sidebarCollapseButton.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        _sidebarCollapseButton.Location = new Point(0, 8);
        _sidebarCollapseButton.Text = _sidebarCollapsed ? "«" : "»";
        _sidebarCollapseButton.FlatStyle = FlatStyle.Flat;
        _sidebarCollapseButton.BackColor = Color.FromArgb(22, 27, 34);
        _sidebarCollapseButton.ForeColor = Accent;
        _sidebarCollapseButton.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        _sidebarCollapseButton.Cursor = Cursors.Hand;
        _sidebarCollapseButton.TabStop = false;
        _sidebarCollapseButton.FlatAppearance.BorderColor = Color.FromArgb(67, 58, 30);
        _sidebarCollapseButton.Click += (_, _) => ToggleSidebar();

        _sidebarHeader.Controls.Add(_sidebarBrand);
        _sidebarHeader.Controls.Add(_sidebarCollapseButton);
        _sidebarCollapseButton.BringToFront();

        _sidebarMainNav.Dock = DockStyle.Fill;
        _sidebarMainNav.FlowDirection = FlowDirection.TopDown;
        _sidebarMainNav.WrapContents = false;
        _sidebarMainNav.AutoScroll = true;
        _sidebarMainNav.BackColor = Color.Transparent;
        _sidebarMainNav.Padding = new Padding(0, 4, 0, 4);
        _sidebarMainNav.Margin = new Padding(0);

        _sidebarBottomNav.Dock = DockStyle.Fill;
        _sidebarBottomNav.FlowDirection = FlowDirection.TopDown;
        _sidebarBottomNav.WrapContents = false;
        _sidebarBottomNav.BackColor = Color.Transparent;
        _sidebarBottomNav.Padding = new Padding(0, 3, 0, 0);
        _sidebarBottomNav.Margin = new Padding(0);

        _sidebarVersion.Dock = DockStyle.Fill;
        _sidebarVersion.ForeColor = Color.FromArgb(91, 101, 115);
        _sidebarVersion.Font = new Font("Segoe UI", 7.5F);

        _sidebarLayout.Controls.Add(_sidebarHeader, 0, 0);
        _sidebarLayout.Controls.Add(_sidebarMainNav, 0, 1);
        _sidebarLayout.Controls.Add(_sidebarBottomNav, 0, 2);
        _sidebarLayout.Controls.Add(_sidebarVersion, 0, 3);
        _sidebar.Controls.Add(_sidebarLayout);

        _content.Dock = DockStyle.Fill;
        _content.BackColor = Bg;

        Controls.Add(_content);
        Controls.Add(_sidebar);
        _sidebar.BringToFront();

        BuildHomePanel();
        BuildBrowserPanel();
        BuildListingsPanel();
        BuildSoldSalesPanel();
        BuildMarketPanel();
        BuildSettingsPanel();

        _content.Controls.Add(_homePanel);
        _content.Controls.Add(_browserPanel);
        _content.Controls.Add(_listingsPanel);
        _content.Controls.Add(_salesPanel);
        _content.Controls.Add(_marketPanel);
        _content.Controls.Add(_settingsPanel);

        ApplySidebarVisualState();
        RebuildSidebarNavigation();
        ShowPanel(_homePanel);
    }

    private void ToggleSidebar()
    {
        _sidebarCollapsed = !_sidebarCollapsed;
        _settings.SidebarCollapsed = _sidebarCollapsed;
        _settings.Save();

        ApplySidebarVisualState();
        RebuildSidebarNavigation();
    }

    private void ApplySidebarVisualState()
    {
        _sidebar.Width = _sidebarCollapsed ? 64 : 238;
        _sidebar.Padding = new Padding(_sidebarCollapsed ? 6 : 11);

        if (_sidebarLayout.RowStyles.Count > 0)
        {
            _sidebarLayout.RowStyles[0].SizeType = SizeType.Absolute;
            _sidebarLayout.RowStyles[0].Height = _sidebarCollapsed ? 46 : 78;
        }

        if (_sidebarCollapsed)
        {
            _sidebarBrand.Visible = false;

            _sidebarCollapseButton.Dock = DockStyle.Fill;
            _sidebarCollapseButton.Anchor = AnchorStyles.None;
            _sidebarCollapseButton.Margin = new Padding(5);
            _sidebarCollapseButton.Text = "«";
            _sidebarCollapseButton.BackColor = Color.FromArgb(21, 25, 31);
            _sidebarCollapseButton.ForeColor = Accent;

            _sidebarVersion.Text = $"Desktop V{UpdateService.CurrentVersion}";
            _sidebarVersion.TextAlign = ContentAlignment.MiddleCenter;
            _sidebarVersion.Padding = new Padding(0);
        }
        else
        {
            _sidebarBrand.Visible = true;
            _sidebarBrand.Text = "NiyaziSUGAR\nITEMCI HUB";
            _sidebarBrand.Font = new Font("Segoe UI", 13.5F, FontStyle.Bold);
            _sidebarBrand.TextAlign = ContentAlignment.MiddleRight;
            _sidebarBrand.Padding = new Padding(34, 0, 4, 0);

            _sidebarCollapseButton.Dock = DockStyle.None;
            _sidebarCollapseButton.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            _sidebarCollapseButton.Width = 29;
            _sidebarCollapseButton.Height = 29;
            _sidebarCollapseButton.Left = 0;
            _sidebarCollapseButton.Top = 8;
            _sidebarCollapseButton.Text = "»";

            _sidebarVersion.Text = $"Desktop V{UpdateService.CurrentVersion}";
            _sidebarVersion.TextAlign = ContentAlignment.MiddleRight;
            _sidebarVersion.Padding = new Padding(0, 0, 4, 0);
        }

        _sidebarCollapseButton.BringToFront();
        _sidebarHeader.PerformLayout();
        _sidebarLayout.PerformLayout();
        _sidebar.PerformLayout();
    }

    private Label SidebarSection(string text)
    {
        return new Label
        {
            Width = 214,
            Height = 24,
            Text = text.ToUpperInvariant(),
            ForeColor = Color.FromArgb(95, 107, 123),
            Font = new Font("Segoe UI", 7.2F, FontStyle.Bold),
            TextAlign = ContentAlignment.BottomLeft,
            Padding = new Padding(8, 0, 0, 4),
            Margin = new Padding(0, 5, 0, 2)
        };
    }

    private void LegacyRebuildSidebarNavigation()
    {
        _sidebarMainNav.SuspendLayout();
        _sidebarBottomNav.SuspendLayout();
        _sidebarMainNav.Controls.Clear();
        _sidebarBottomNav.Controls.Clear();

        if (!_sidebarCollapsed)
            _sidebarMainNav.Controls.Add(SidebarSection("Hızlı Erişim"));

        _sidebarMainNav.Controls.Add(NavButton(
            "⌂", "Ana Panel", () => ShowPanel(_homePanel)));

        _sidebarMainNav.Controls.Add(NavButton(
            "✉", "Itemci / Mesajlar", () =>
            {
                ShowPanel(_browserPanel);
                _ = OpenOrActivateBrowserTabAsync(
                    _messageUrl ?? "https://itemci.com/panel/mesajlar",
                    "Mesajlar"
                );
            }
        ));

        _sidebarMainNav.Controls.Add(NavButton(
            "▤", "Güncel İlanlarım", () =>
            {
                ShowPanel(_listingsPanel);
                _ = RefreshCurrentListingsAsync(showErrors: true);
            }
        ));

        if (!_sidebarCollapsed)
            _sidebarMainNav.Controls.Add(SidebarSection("İlan Yönetimi"));

        foreach (var server in _marketServers
                     .OrderByDescending(x => x.IsDefault)
                     .ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            var listingLabel = server.IsDefault
                ? "MSP2 İlan Oluştur"
                : $"{server.Name} İlan Oluştur";

            var createListing = NavButton(
                "＋", listingLabel, () =>
                {
                    _ = OpenServerListingCreateAsync(server);
                }
            );
            createListing.ForeColor = Color.FromArgb(139, 198, 255);
            _sidebarMainNav.Controls.Add(createListing);
        }

        if (!_sidebarCollapsed)
            _sidebarMainNav.Controls.Add(SidebarSection("Piyasa Analizi"));

        var addServer = NavButton(
            "＋", "Analiz İçin Server Ekle", () =>
            {
                ShowPanel(_marketPanel);
                _ = OpenMarketAddServerAsync();
            }
        );
        addServer.ForeColor = Color.FromArgb(111, 235, 166);
        _sidebarMainNav.Controls.Add(addServer);

        foreach (var server in _marketServers
                     .OrderByDescending(x => x.IsDefault)
                     .ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            _sidebarMainNav.Controls.Add(CreateServerNavRow(server));
        }

        _sidebarBottomNav.Controls.Add(NavButton(
            "₺", "Satışlar / Günlük Kâr", () =>
            {
                ShowPanel(_salesPanel);
                _ = RefreshSoldListingsAsync(
                    showErrors: true,
                    fullHistory: !_historyFullyScanned
                );
            }
        ));

        _sidebarBottomNav.Controls.Add(NavButton(
            "⚙", "Ayarlar", () => ShowPanel(_settingsPanel)));

        _sidebarMainNav.ResumeLayout();
        _sidebarBottomNav.ResumeLayout();
    }

    private Control CreateServerNavRow(MarketServerInfo server)
    {
        if (_sidebarCollapsed)
        {
            var compact = NavButton(
                "▥",
                server.IsDefault
                    ? "MSP2 Piyasa Analiz"
                    : $"{server.Name} Piyasa Analiz",
                () =>
                {
                    ShowPanel(_marketPanel);
                    _ = RunMarketServerAsync(server.Id, server.Name);
                }
            );

            _sidebarToolTip.SetToolTip(
                compact,
                server.IsDefault
                    ? "MSP2 Piyasa Analiz"
                    : $"{server.Name} Piyasa Analiz"
            );

            return compact;
        }

        const int rowWidth = 214;
        var row = new Panel
        {
            Width = rowWidth,
            Height = 40,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 5)
        };

        var rightWidth = server.IsDefault ? 34 : 64;

        var mainButton = new Button
        {
            Text = server.IsDefault
                ? "▥  MSP2 Piyasa Analiz"
                : $"▥  {server.Name} Piyasa Analiz",
            Width = rowWidth - rightWidth,
            Height = 38,
            Location = new Point(0, 1),
            TextAlign = ContentAlignment.MiddleLeft,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(18, 22, 28),
            ForeColor = Color.FromArgb(211, 216, 224),
            Font = new Font("Segoe UI", 8.7F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            AutoEllipsis = true
        };
        mainButton.FlatAppearance.BorderColor = Color.FromArgb(32, 39, 48);
        mainButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(29, 35, 43);
        mainButton.Click += async (_, _) =>
        {
            ShowPanel(_marketPanel);
            await RunMarketServerAsync(server.Id, server.Name);
        };

        var editButton = new Button
        {
            Text = "⚙",
            Width = 30,
            Height = 30,
            Location = new Point(rowWidth - rightWidth + 2, 5),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(23, 29, 37),
            ForeColor = Color.FromArgb(139, 198, 255),
            Font = new Font("Segoe UI Symbol", 8.5F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            TabStop = false
        };
        editButton.FlatAppearance.BorderColor = Color.FromArgb(45, 54, 67);
        editButton.Click += async (_, _) =>
        {
            ShowPanel(_marketPanel);
            await EditMarketServerAsync(server.Id);
        };

        row.Controls.Add(mainButton);
        row.Controls.Add(editButton);

        if (!server.IsDefault)
        {
            var deleteButton = new Button
            {
                Text = "×",
                Width = 28,
                Height = 30,
                Location = new Point(rowWidth - 28, 5),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(39, 24, 29),
                ForeColor = Color.FromArgb(255, 126, 126),
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                TabStop = false
            };
            deleteButton.FlatAppearance.BorderColor = Color.FromArgb(78, 42, 48);
            deleteButton.Click += async (_, _) =>
            {
                await DeleteMarketServerAsync(server.Id, server.Name);
            };
            row.Controls.Add(deleteButton);
        }

        return row;
    }

    private Button NavButton(string icon, string label, Action click)
    {
        var b = new Button
        {
            Width = _sidebarCollapsed ? 44 : 214,
            Height = 40,
            Text = _sidebarCollapsed ? icon : $"{icon}   {label}",
            TextAlign = _sidebarCollapsed
                ? ContentAlignment.MiddleCenter
                : ContentAlignment.MiddleLeft,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(16, 20, 26),
            ForeColor = Color.FromArgb(205, 211, 220),
            Font = new Font(
                "Segoe UI",
                _sidebarCollapsed ? 11.5F : 8.9F,
                FontStyle.Bold
            ),
            Margin = new Padding(0, 0, 0, 5),
            Cursor = Cursors.Hand,
            Padding = _sidebarCollapsed
                ? new Padding(0)
                : new Padding(7, 0, 4, 0)
        };

        b.FlatAppearance.BorderColor = Color.FromArgb(29, 35, 43);
        b.FlatAppearance.MouseOverBackColor = Color.FromArgb(27, 33, 41);
        b.Click += (_, _) => click();
        _sidebarToolTip.SetToolTip(b, label);
        return b;
    }

    private void BuildHomePanel()
    {
        _homePanel.Dock = DockStyle.Fill;
        _homePanel.BackColor = Bg;
        _homePanel.Padding = new Padding(26);

        var title = new Label
        {
            Dock = DockStyle.Top,
            Height = 58,
            Text = "Ana Panel",
            ForeColor = TextMain,
            Font = new Font("Segoe UI", 22F, FontStyle.Bold)
        };

        var cards = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 150,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 10, 0, 0)
        };

        cards.Controls.Add(StatusCard("ITEMCI BAĞLANTISI", _connectionLabel, "Hazırlanıyor"));
        cards.Controls.Add(StatusCard("OKUNMAMIŞ MESAJ", _unreadLabel, "0"));
        cards.Controls.Add(StatusCard("SON KONTROL", _lastCheckLabel, "—"));

        var monitorCard = new Panel
        {
            Dock = DockStyle.Top,
            Height = 180,
            BackColor = Card,
            Padding = new Padding(18),
            Margin = new Padding(0, 14, 0, 0)
        };
        monitorCard.Paint += (_, e) => DrawBorder(e.Graphics, monitorCard.ClientRectangle);

        var monitorTitle = new Label
        {
            Dock = DockStyle.Top,
            Height = 34,
            Text = "Mesaj Takibi",
            ForeColor = TextMain,
            Font = new Font("Segoe UI", 13F, FontStyle.Bold)
        };

        _detectorLabel.Dock = DockStyle.Top;
        _detectorLabel.Height = 40;
        _detectorLabel.ForeColor = TextMuted;
        _detectorLabel.Text = "Itemci sayfası bekleniyor...";
        _detectorLabel.Font = new Font("Segoe UI", 9F);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 54,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };

        buttons.Controls.Add(ActionButton("ITEMCI'Yİ AÇ", () =>
        {
            ShowPanel(_browserPanel);
            if (_webReady && _webView.Source is null)
                _webView.Source = new Uri("https://itemci.com/");
        }));

        buttons.Controls.Add(ActionButton("TEST POPUP", () =>
        {
            _detectorLabel.Text = "Test popup gönderildi. Sağ altta görünmesi gerekiyor.";
            _detectorLabel.ForeColor = Accent;

            ShowDesktopNotification(
                "Test bildirimi",
                "NiyaziSUGAR · Itemci bildirim sistemi çalışıyor ✅",
                ToolTipIcon.Info
            );
        }));

        buttons.Controls.Add(ActionButton("MESAJLARA GİT", () =>
        {
            ShowPanel(_browserPanel);

            if (_messageUrl is not null && _webReady)
                _webView.Source = new Uri(_messageUrl);
        }));

        buttons.Controls.Add(ActionButton("TANILAMA KOPYALA", () =>
        {
            try
            {
                Clipboard.SetText(_lastDiagnosticsJson);
                _detectorLabel.Text = "Mesaj tanılama verisi panoya kopyalandı.";
                _detectorLabel.ForeColor = Green;

                ShowDesktopNotification(
                    "Tanılama kopyalandı",
                    "ChatGPT'ye yapıştırabilirsin.",
                    ToolTipIcon.Info
                );
            }
            catch
            {
                _detectorLabel.Text = "Tanılama verisi kopyalanamadı.";
                _detectorLabel.ForeColor = Color.IndianRed;
            }
        }));

        monitorCard.Controls.Add(buttons);
        monitorCard.Controls.Add(_detectorLabel);
        monitorCard.Controls.Add(monitorTitle);

        var infoCard = new Panel
        {
            Dock = DockStyle.Top,
            Height = 118,
            BackColor = Card,
            Padding = new Padding(18),
            Margin = new Padding(0, 14, 0, 0)
        };
        infoCard.Paint += (_, e) => DrawBorder(e.Graphics, infoCard.ClientRectangle);

        var info = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = TextMuted,
            Font = new Font("Segoe UI", 9F),
            Text =
                "V1 amacı: Itemci'yi uygulamanın içinde açık tutmak, sistem tepsisinde çalışmak ve yeni mesaj geldiğinde Windows bildirimi vermek.\n" +
                "Mesaj takibi kişi adı + son mesaj önizlemesi için arka planda Mesajlar sayfasını izler. Oturum düşerse otomatik yeniden bağlanır."
        };
        infoCard.Controls.Add(info);

        _homePanel.Controls.Add(infoCard);
        _homePanel.Controls.Add(monitorCard);
        _homePanel.Controls.Add(cards);
        _homePanel.Controls.Add(title);
    }

    private Panel StatusCard(string caption, Label valueLabel, string initialValue)
    {
        var p = new Panel
        {
            Width = 270,
            Height = 112,
            BackColor = Card,
            Margin = new Padding(0, 0, 12, 0),
            Padding = new Padding(16)
        };
        p.Paint += (_, e) => DrawBorder(e.Graphics, p.ClientRectangle);

        var captionLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 24,
            Text = caption,
            ForeColor = TextMuted,
            Font = new Font("Segoe UI", 8F, FontStyle.Bold)
        };

        valueLabel.Dock = DockStyle.Fill;
        valueLabel.Text = initialValue;
        valueLabel.ForeColor = TextMain;
        valueLabel.Font = new Font("Segoe UI", 20F, FontStyle.Bold);
        valueLabel.TextAlign = ContentAlignment.MiddleLeft;

        p.Controls.Add(valueLabel);
        p.Controls.Add(captionLabel);
        return p;
    }

    private Button ActionButton(string text, Action click)
    {
        var b = new Button
        {
            Width = 150,
            Height = 38,
            Text = text,
            BackColor = Color.FromArgb(31, 36, 44),
            ForeColor = TextMain,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            Margin = new Padding(0, 6, 8, 0),
            Cursor = Cursors.Hand
        };
        b.FlatAppearance.BorderColor = Border;
        b.Click += (_, _) => click();
        return b;
    }

    private void BuildBrowserPanel()
    {
        _browserPanel.Dock = DockStyle.Fill;
        _browserPanel.BackColor = Bg;

        var tabBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 38,
            BackColor = Color.FromArgb(16, 19, 24),
            Padding = new Padding(8, 4, 8, 3)
        };

        _browserTabStrip.Dock = DockStyle.Fill;
        _browserTabStrip.FlowDirection = FlowDirection.LeftToRight;
        _browserTabStrip.WrapContents = false;
        _browserTabStrip.AutoScroll = true;
        _browserTabStrip.BackColor = Color.Transparent;
        _browserTabStrip.Padding = new Padding(0);

        var newTab = new ReferenceButton
        {
            Dock = DockStyle.Right,
            Width = 36,
            Text = "+",
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(25, 30, 37),
            ForeColor = TextMain,
            Font = new Font("Segoe UI", 13F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            TabStop = false
        };
        newTab.FlatAppearance.BorderColor = Border;
        newTab.Click += async (_, _) =>
        {
            await OpenSmartBrowserTabAsync(
                "https://www.google.com/",
                "Yeni Sekme",
                activate: true, forceNew: true
            );
        };

        tabBar.Controls.Add(_browserTabStrip);
        tabBar.Controls.Add(newTab);

        var addressBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 48,
            BackColor = Side,
            Padding = new Padding(8, 7, 8, 7)
        };

        var back = BrowserToolButton("←", "Geri");
        back.Click += (_, _) =>
        {
            if (_webReady && _webView.CanGoBack)
                _webView.GoBack();
        };

        var forward = BrowserToolButton("→", "İleri");
        forward.Click += (_, _) =>
        {
            if (_webReady && _webView.CanGoForward)
                _webView.GoForward();
        };

        var refresh = BrowserToolButton("↻", "Yenile");
        refresh.Click += (_, _) =>
        {
            if (_webReady)
                _webView.Reload();
        };

        var home = BrowserToolButton("⌂", "Itemci Ana Sayfa");
        home.Click += (_, _) =>
        {
            NavigateActiveBrowser(
                "https://itemci.com/"
            );
        };

        _browserAdBlockButton = BrowserToolButton("🛡", "Reklam engelleyici");
        _browserAdBlockButton.Font = new Font("Segoe UI Emoji", 9.5F, FontStyle.Regular);
        _browserAdBlockButton.Click += (_, _) => ToggleAdBlockFromBrowser();

        var leftTools = new FlowLayoutPanel
        {
            Dock = DockStyle.Left,
            Width = 194,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent,
            Padding = new Padding(0)
        };

        leftTools.Controls.Add(back);
        leftTools.Controls.Add(forward);
        leftTools.Controls.Add(refresh);
        leftTools.Controls.Add(home);
        leftTools.Controls.Add(_browserAdBlockButton);
        RefreshAdBlockButton();

        var go = BrowserToolButton("Git", "Adrese Git");
        go.Dock = DockStyle.Right;
        go.Width = 48;
        go.Click += (_, _) => NavigateFromAddressBar();

        _browserAddressBox.Dock = DockStyle.Fill;
        _browserAddressBox.BackColor = Color.FromArgb(27, 31, 38);
        _browserAddressBox.ForeColor = TextMain;
        _browserAddressBox.BorderStyle = BorderStyle.FixedSingle;
        _browserAddressBox.Font = new Font("Segoe UI", 9.5F);
        _browserAddressBox.PlaceholderText = "Google'da ara veya adres yaz";
        _browserAddressBox.Margin = new Padding(0);
        _browserAddressBox.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter)
                return;

            e.SuppressKeyPress = true;
            e.Handled = true;
            NavigateFromAddressBar();
        };

        var addressHost = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Padding = new Padding(4, 2, 4, 2)
        };
        addressHost.Controls.Add(_browserAddressBox);

        addressBar.Controls.Add(addressHost);
        addressBar.Controls.Add(go);
        addressBar.Controls.Add(leftTools);

        _browserHost.Dock = DockStyle.Fill;
        _browserHost.BackColor = Bg;

        // Mesaj listesi takipçisi görünmeden arka planda çalışır.
        _messageMonitorWebView.Size = new Size(1180, 760);
        _messageMonitorWebView.Location = new Point(-5000, -5000);
        _messageMonitorWebView.BackColor = Bg;
        _messageMonitorWebView.TabStop = false;

        _browserPanel.Controls.Add(_messageMonitorWebView);
        _browserPanel.Controls.Add(_browserHost);
        _browserPanel.Controls.Add(addressBar);
        _browserPanel.Controls.Add(tabBar);
    }

    private Button LegacyBrowserToolButton(string text, string tooltip)
    {
        var button = new Button
        {
            Width = 34,
            Height = 32,
            Text = text,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(28, 33, 41),
            ForeColor = TextMain,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            Margin = new Padding(0, 0, 4, 0),
            Cursor = Cursors.Hand,
            TabStop = false
        };

        button.FlatAppearance.BorderColor = Border;
        _sidebarToolTip.SetToolTip(button, tooltip);

        return button;
    }

    private void NavigateFromAddressBar()
    {
        var text = _browserAddressBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(text))
            return;

        // Chrome gibi tek kutu: URL ise siteye git, değilse Google'da ara.
        var isLikelyUrl =
            text.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("file://", StringComparison.OrdinalIgnoreCase) ||
            (!text.Contains(' ') && text.Contains('.')) ||
            text.StartsWith("localhost", StringComparison.OrdinalIgnoreCase);

        if (!isLikelyUrl)
        {
            var query = Uri.EscapeDataString(text);
            NavigateActiveBrowser($"https://www.google.com/search?q={query}");
            return;
        }

        var candidate = text;

        if (!candidate.Contains("://"))
            candidate = "https://" + candidate;

        if (Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
            NavigateActiveBrowser(uri.ToString());
    }

    private void NavigateActiveBrowser(string url)
    {
        if (!_webReady || _webView.CoreWebView2 is null)
            return;

        try
        {
            _webView.Source = new Uri(url);
        }
        catch { }
    }

    private async Task<BrowserTabState?> LegacyOpenOrActivateBrowserTabAsync(
        string url,
        string title
    )
    {
        var normalized = NormalizeBrowserUrl(url);

        var existing = _browserTabs.FirstOrDefault(x =>
            string.Equals(
                NormalizeBrowserUrl(
                    x.WebView.Source?.ToString() ?? ""
                ),
                normalized,
                StringComparison.OrdinalIgnoreCase
            )
        );

        if (existing is not null)
        {
            SelectBrowserTab(existing);
            return existing;
        }

        return await OpenSmartBrowserTabAsync(
            url,
            title,
            activate: true
        );
    }

    private async Task<BrowserTabState?> CreateBrowserTabAsync(
        string url,
        string title,
        bool activate
    )
    {
        if (_browserEnvironment is null)
            return null;

        var view = new WebView2
        {
            Dock = DockStyle.Fill,
            BackColor = Bg,
            Visible = false
        };

        _browserHost.Controls.Add(view);

        try
        {
            await view.EnsureCoreWebView2Async(
                _browserEnvironment
            );

            ConfigureBrowserWebView(view);

            var tab = new BrowserTabState
            {
                Id = ++_browserTabSequence,
                WebView = view,
                Title = string.IsNullOrWhiteSpace(title)
                    ? "Itemci"
                    : title
            };

            tab.TabPanel = CreateBrowserTabControl(tab);

            _browserTabs.Add(tab);
            _browserTabStrip.Controls.Add(tab.TabPanel);

            view.Source = new Uri(url);

            if (activate)
                SelectBrowserTab(tab);

            return tab;
        }
        catch
        {
            _browserHost.Controls.Remove(view);
            view.Dispose();
            return null;
        }
    }

    private Panel LegacyCreateBrowserTabControl(
        BrowserTabState tab
    )
    {
        var panel = new Panel
        {
            Width = 205,
            Height = 30,
            BackColor = Color.FromArgb(26, 30, 37),
            Margin = new Padding(0, 0, 5, 0)
        };

        var title = new Button
        {
            Dock = DockStyle.Fill,
            Text = ShortTabTitle(tab.Title),
            TextAlign = ContentAlignment.MiddleLeft,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Transparent,
            ForeColor = TextMain,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            Padding = new Padding(8, 0, 4, 0),
            Cursor = Cursors.Hand,
            AutoEllipsis = true,
            TabStop = false
        };
        title.FlatAppearance.BorderSize = 0;
        title.Click += (_, _) =>
        {
            SelectBrowserTab(tab);
        };

        var close = new Button
        {
            Dock = DockStyle.Right,
            Width = 28,
            Text = "×",
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Transparent,
            ForeColor = TextMuted,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            TabStop = false
        };
        close.FlatAppearance.BorderSize = 0;
        close.Click += (_, _) =>
        {
            CloseBrowserTab(tab);
        };

        panel.Controls.Add(title);
        panel.Controls.Add(close);

        tab.TitleButton = title;

        return panel;
    }

    private void SelectBrowserTab(
        BrowserTabState tab
    )
    {
        if (!_browserTabs.Contains(tab))
            return;

        foreach (var item in _browserTabs)
        {
            item.WebView.Visible = false;
            if (!ReferenceEquals(item, tab)) _ = SuspendHiddenTabAsync(item);

            if (item.TabPanel is not null)
                item.TabPanel.BackColor =
                    Color.FromArgb(26, 30, 37);
        }

        _activeBrowserTab = tab;
        _webView = tab.WebView; SelectTopSection(_activeTopPanel ?? _browserPanel);

        tab.WebView.Visible = true;
        try { tab.WebView.CoreWebView2?.Resume(); } catch { }
        tab.WebView.BringToFront();

        if (tab.TabPanel is not null)
            tab.TabPanel.BackColor =
                Color.FromArgb(39, 45, 55);

        _browserAddressBox.Text =
            tab.WebView.Source?.ToString() ?? "";

        _ = InstallQuickReplyDockAsync(
            tab.WebView
        );
    }

    private void CloseBrowserTab(
        BrowserTabState tab
    )
    {
        if (!_browserTabs.Contains(tab))
            return;

        if (_browserTabs.Count == 1)
        {
            tab.WebView.Source =
                new Uri("https://itemci.com/");
            return;
        }

        var index = _browserTabs.IndexOf(tab);
        var wasActive =
            ReferenceEquals(_activeBrowserTab, tab);

        _browserTabs.Remove(tab);

        if (tab.TabPanel is not null)
            _browserTabStrip.Controls.Remove(
                tab.TabPanel
            );

        _browserHost.Controls.Remove(
            tab.WebView
        );

        tab.TabPanel?.Dispose();
        tab.WebView.Dispose();

        if (wasActive)
        {
            var nextIndex = Math.Clamp(
                index - 1,
                0,
                _browserTabs.Count - 1
            );

            SelectBrowserTab(
                _browserTabs[nextIndex]
            );
        }
    }

    private static string NormalizeBrowserUrl(
        string value
    )
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        try
        {
            var uri = new Uri(value);

            return uri.GetLeftPart(
                    UriPartial.Path
                )
                .TrimEnd('/');
        }
        catch
        {
            return value.TrimEnd('/');
        }
    }

    private static string ShortTabTitle(
        string value
    )
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Itemci";

        var clean = value
            .Replace(" | itemci.com", "")
            .Replace(" - itemci.com", "")
            .Trim();

        if (clean.Length <= 28)
            return clean;

        return clean[..25] + "...";
    }

    private void ConfigureBrowserWebView(
        WebView2 view
    )
    {
        if (view.CoreWebView2 is null)
            return;

        view.CoreWebView2.Settings
            .AreDefaultContextMenusEnabled = true;

        view.CoreWebView2.Settings
            .AreDevToolsEnabled = false;

        view.CoreWebView2.Settings
            .IsStatusBarEnabled = false;

        ConfigureAdBlocking(view);

        view.CoreWebView2.NewWindowRequested +=
            (_, e) =>
            {
                e.Handled = true;
                if (_settings.AdBlockEnabled && !e.IsUserInitiated) return;

                if (string.IsNullOrWhiteSpace(e.Uri))
                    return;

                if (_settings.AdBlockEnabled && IsBlockedAdUrl(e.Uri))
                    return;

                _ = OpenSmartBrowserTabAsync(
                    e.Uri,
                    "Yeni Sekme",
                    activate: true, forceNew: true
                );
            };

        view.CoreWebView2.DocumentTitleChanged +=
            (_, _) =>
            {
                var tab = _browserTabs.FirstOrDefault(
                    x => ReferenceEquals(
                        x.WebView,
                        view
                    )
                );

                if (tab is null)
                    return;

                var title =
                    view.CoreWebView2.DocumentTitle;

                if (!string.IsNullOrWhiteSpace(title))
                {
                    tab.Title = title;

                    if (tab.TitleButton is not null)
                    {
                        tab.TitleButton.Text =
                            ShortTabTitle(title);

                        _sidebarToolTip.SetToolTip(
                            tab.TitleButton,
                            title
                        );
                    }
                }
            };

        view.CoreWebView2.NavigationStarting +=
            (_, e) =>
            {
                if (ReferenceEquals(
                    _webView,
                    view
                ))
                {
                    _browserAddressBox.Text =
                        e.Uri ?? "";
                }
            };

        view.CoreWebView2.NavigationCompleted +=
            async (_, e) =>
            {
                if (!e.IsSuccess)
                    return;

                if (ReferenceEquals(
                    _webView,
                    view
                ))
                {
                    _browserAddressBox.Text =
                        view.Source?.ToString() ?? "";
                }

                await RefreshOwnStoreUrlAsync(view);
                await InstallQuickReplyDockAsync(view);
                await InstallAdBlockDomAsync(view);
                PositionLiveMarket(); SelectTopSection(_activeTopPanel ?? _browserPanel);

                if (ReferenceEquals(
                    _pendingListingWebView,
                    view
                ) &&
                !string.IsNullOrWhiteSpace(
                    _pendingListingServerName
                ))
                {
                    await TryAutoSelectPendingListingAsync(
                        view
                    );
                }

                if (!ReferenceEquals(
                    _webView,
                    view
                ))
                {
                    return;
                }

                _connectionLabel.Text = "BAĞLI";
                _connectionLabel.ForeColor = Green;

                try
                {
                    var current =
                        view.Source?.ToString() ?? "";

                    if (
                        current.Contains(
                            "itemci.com",
                            StringComparison.OrdinalIgnoreCase
                        ) &&
                        _messageMonitorWebView
                            .CoreWebView2 is not null &&
                        DateTime.Now -
                            _lastMonitorReloadAt >
                            TimeSpan.FromSeconds(2)
                    )
                    {
                        _lastMonitorReloadAt =
                            DateTime.Now;

                        var target =
                            _messageUrl ??
                            "https://itemci.com/panel/mesajlar";

                        _loadedMonitorUrl = target;
                        _messageMonitorReady = false;

                        _messageMonitorWebView.Source =
                            new Uri(target);
                    }
                }
                catch { }

                _ = MonitorItemciAsync();
            };
    }

    private void ConfigureAdBlocking(WebView2 view)
    {
        InstallSiteTrafficGuard(view);
        if (_settings.AdBlockEnabled)
        {
            var adCookie = view.CoreWebView2.CookieManager.CreateCookie("site_login_advert", "1", "itemci.com", "/");
            adCookie.Expires = DateTime.Now.AddYears(1);
            adCookie.IsSecure = true;
            view.CoreWebView2.CookieManager.AddOrUpdateCookie(adCookie);
        }

        if (view.CoreWebView2 is null)
            return;

        try
        {
            view.CoreWebView2.AddWebResourceRequestedFilter(
                "*",
                CoreWebView2WebResourceContext.All
            );

            view.CoreWebView2.WebResourceRequested += (_, e) =>
            {
                if (!_settings.AdBlockEnabled)
                    return;

                // Giriş / kayıt / şifre / OAuth ekranlarında reklam engelleyiciyi
                // tamamen pas geç. Itemci bazı ortak kaynakları bu sayfalarda da
                // kullandığı için agresif engelleme login ekranını boş bırakabiliyordu.
                if (IsAdBlockBypassPage(view.Source?.ToString()))
                    return;

                var uri = e.Request?.Uri;

                if (!IsBlockedAdUrl(uri))
                    return;

                try
                {
                    e.Response = _browserEnvironment?.CreateWebResourceResponse(
                        Stream.Null,
                        403,
                        "Blocked",
                        "Content-Type: text/plain\r\nCache-Control: no-store"
                    );
                }
                catch { }
            };
        }
        catch { }
    }

    private static bool IsAdBlockBypassPage(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        try
        {
            var uri = new Uri(value);
            var host = uri.Host.ToLowerInvariant();
            var path = uri.AbsolutePath.ToLowerInvariant();

            if (host.Equals("accounts.google.com", StringComparison.OrdinalIgnoreCase))
                return true;

            if (!host.EndsWith("itemci.com", StringComparison.OrdinalIgnoreCase))
                return false;

            return
                path.StartsWith("/user/login", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/user/register", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("forgot", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("password", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("oauth", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("auth", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsBlockedAdUrl(string? value)
    {
        return AdBlockRules.ShouldBlock(value);
    }

    private async Task InstallAdBlockDomAsync(WebView2 view)
    {
        if (!_settings.AdBlockEnabled || view.CoreWebView2 is null)
            return;

        if (IsAdBlockBypassPage(view.Source?.ToString()))
            return;

        try
        {
            var file = Path.Combine(
                AppContext.BaseDirectory,
                "AdBlock.js"
            );

            if (!File.Exists(file))
                return;

            var script = await File.ReadAllTextAsync(file);
            await view.CoreWebView2.ExecuteScriptAsync(script);
        }
        catch { }
    }

    private void ToggleAdBlockFromBrowser()
    {
        _settings.AdBlockEnabled = !_settings.AdBlockEnabled;
        _settings.Save();

        if (_adBlockCheck is not null)
            _adBlockCheck.Checked = _settings.AdBlockEnabled;

        RefreshAdBlockButton();
        ReloadBrowserViewsForAdBlock();
    }

    private void RefreshAdBlockButton()
    {
        if (_browserAdBlockButton is null)
            return;

        _browserAdBlockButton.Text = "🛡";
        _browserAdBlockButton.ForeColor = _settings.AdBlockEnabled
            ? Green
            : Color.FromArgb(120, 130, 144);
        _browserAdBlockButton.BackColor = _settings.AdBlockEnabled
            ? Color.FromArgb(20, 39, 31)
            : Color.FromArgb(28, 33, 41);

        _sidebarToolTip.SetToolTip(
            _browserAdBlockButton,
            _settings.AdBlockEnabled
                ? "Reklam engelleyici açık — kapatmak için tıkla"
                : "Reklam engelleyici kapalı — açmak için tıkla"
        );
    }

    private void ReloadBrowserViewsForAdBlock()
    {
        foreach (var tab in _browserTabs.ToList())
        {
            try { tab.WebView.Reload(); }
            catch { }
        }

        try
        {
            if (_marketWebReady)
                _marketWebView.Reload();
        }
        catch { }
    }

    private async Task InstallQuickReplyDockAsync(
        WebView2 view
    )
    {
        if (
            view.CoreWebView2 is null
        )
            return;

        try
        {
            var file = Path.Combine(
                AppContext.BaseDirectory,
                "QuickReplyDock.js"
            );

            if (!File.Exists(file))
                return;

            var script =
                await File.ReadAllTextAsync(file);

            await view.CoreWebView2
                .ExecuteScriptAsync(script);
        }
        catch
        {
            // Sayfa değişimi sırasında kısa süreli hata olabilir.
        }
    }

    private async Task RefreshOwnStoreUrlAsync(
        WebView2 view
    )
    {
        if (
            view.CoreWebView2 is null ||
            !string.IsNullOrWhiteSpace(
                _settings.OwnStoreUrl
            )
        )
        {
            return;
        }

        try
        {
            var script = """
            (() => {
                const visible = el => {
                    if (!el) return false;
                    const r = el.getBoundingClientRect();
                    const s = getComputedStyle(el);

                    return (
                        r.width > 0 &&
                        r.height > 0 &&
                        s.display !== 'none' &&
                        s.visibility !== 'hidden'
                    );
                };

                const storeLinks = [
                    ...document.querySelectorAll(
                        'a[href*="/user/store/"]'
                    )
                ]
                .filter(visible)
                .map(a => {
                    const r = a.getBoundingClientRect();
                    const text = String(
                        a.textContent || ''
                    ).trim();

                    let score = 0;

                    // Kendi hesap kartı / sol kullanıcı paneli ekranın solunda olur.
                    if (
                        r.left <
                        window.innerWidth * .42
                    ) score += 50;

                    if (r.top < 720)
                        score += 20;

                    if (text.includes('@'))
                        score += 15;

                    // Mesaj konuşma alanındaki karşı taraf linklerini cezalandır.
                    if (
                        r.left >
                        window.innerWidth * .52
                    ) score -= 80;

                    return {
                        href:a.href,
                        score
                    };
                })
                .sort((a,b) => b.score-a.score);

                if (
                    storeLinks[0] &&
                    storeLinks[0].score > 0
                )
                {
                    return storeLinks[0].href;
                }

                // Yedek: sadece ekranın sol kısmındaki @kullanıcı adını kullan.
                const nodes = [
                    ...document.querySelectorAll(
                        'div,span,a,strong,b'
                    )
                ]
                .filter(visible)
                .map(el => {
                    const r = el.getBoundingClientRect();
                    const text = String(
                        el.textContent || ''
                    ).trim();

                    let score = 0;

                    if (
                        r.left <
                        window.innerWidth * .42
                    ) score += 40;

                    if (r.top < 720)
                        score += 15;

                    if (
                        /^@[A-Za-z0-9_.-]{2,50}$/
                            .test(text)
                    ) score += 80;

                    if (
                        r.left >
                        window.innerWidth * .52
                    ) score -= 80;

                    return {
                        text,
                        score
                    };
                })
                .filter(x => x.score > 60)
                .sort((a,b) => b.score-a.score);

                const own = nodes[0]?.text;

                if (
                    own &&
                    own.startsWith('@')
                )
                {
                    const username = own.slice(1);

                    return (
                        'https://itemci.com/user/store/' +
                        encodeURIComponent(username)
                    );
                }

                return null;
            })();
            """;

            var raw = await view.CoreWebView2
                .ExecuteScriptAsync(script);

            if (raw == "null")
                return;

            var url =
                JsonSerializer.Deserialize<string>(raw);

            if (
                string.IsNullOrWhiteSpace(url) ||
                !url.Contains(
                    "/user/store/",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                return;
            }

            _settings.OwnStoreUrl = url; if (_marketWebView.CoreWebView2 is not null) await _marketWebView.CoreWebView2.ExecuteScriptAsync("window.__niyaziVerifiedOwnStoreUrl=" + JsonSerializer.Serialize(url));
            _settings.Save();
        }
        catch { }
    }

    private void LegacyBuildListingsPanel()
    {
        _listingsPanel.Dock = DockStyle.Fill;
        _listingsPanel.BackColor = Bg;
        _listingsPanel.Padding = new Padding(26);

        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 64,
            BackColor = Bg
        };

        var title = new Label
        {
            Dock = DockStyle.Left,
            Width = 360,
            Text = "Güncel İlanlarım",
            ForeColor = TextMain,
            Font = new Font("Segoe UI", 22F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };

        var headerActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 330,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Bg,
            Padding = new Padding(0, 6, 0, 0)
        };

        var refresh = ActionButton("İLANLARI YENİLE", () =>
        {
            _ = RefreshCurrentListingsAsync(showErrors: true);
        });
        refresh.Width = 145;

        var add = ActionButton("＋ MSP2 İLAN OLUŞTUR", () =>
        {
            var msp = _marketServers.FirstOrDefault(x => x.IsDefault)
                ?? _marketServers.FirstOrDefault();

            if (msp is not null)
                _ = OpenServerListingCreateAsync(msp);
        });
        add.Width = 175;
        add.ForeColor = Color.FromArgb(139, 198, 255);

        headerActions.Controls.Add(refresh);
        headerActions.Controls.Add(add);

        header.Controls.Add(headerActions);
        header.Controls.Add(title);

        var statusCard = new Panel
        {
            Dock = DockStyle.Top,
            Height = 64,
            BackColor = Card,
            Padding = new Padding(16)
        };
        statusCard.Paint += (_, e) => DrawBorder(e.Graphics, statusCard.ClientRectangle);

        _listingsStatusLabel.Dock = DockStyle.Fill;
        _listingsStatusLabel.Text = "Aktif ilanlar yüklenmeyi bekliyor...";
        _listingsStatusLabel.ForeColor = TextMuted;
        _listingsStatusLabel.Font = new Font("Segoe UI", 9F);
        _listingsStatusLabel.TextAlign = ContentAlignment.MiddleLeft;

        statusCard.Controls.Add(_listingsStatusLabel);

        _listingsList.Dock = DockStyle.Fill;
        _listingsList.FlowDirection = FlowDirection.TopDown;
        _listingsList.WrapContents = false;
        _listingsList.AutoScroll = true;
        _listingsList.BackColor = Bg;
        _listingsList.Padding = new Padding(0, 14, 0, 0);

        _listingsList.Resize += (_, _) => ResizeListingCards();

        _listingsPanel.Controls.Add(_listingsList);
        _listingsPanel.Controls.Add(statusCard);
        _listingsPanel.Controls.Add(header);
    }

    private void ResizeListingCards()
    {
        var width = Math.Max(
            520,
            _listingsList.ClientSize.Width - 24
        );

        foreach (Control control in _listingsList.Controls)
        {
            if (control is Panel card)
                card.Width = width;
        }
    }

    private async Task RefreshCurrentListingsAsync(bool showErrors)
    {
        if (!_webReady || _webView.CoreWebView2 is null)
        {
            _listingsStatusLabel.Text = "Itemci oturumu henüz hazır değil.";
            return;
        }

        try
        {
            _listingsStatusLabel.Text = "Aktif ilanların yükleniyor...";
            _listingsStatusLabel.ForeColor = TextMuted;

            // En sağlam kaynak: satıcının herkese açık mağaza/profil sayfası.
            // Bu sayfada aktif ilanların product linkleri doğrudan bulunuyor.
            var storeUrl = await DiscoverLoggedInStoreUrlAsync();

            var candidates = new List<string>();

            if (!string.IsNullOrWhiteSpace(storeUrl))
                candidates.Add(storeUrl);

            // Yedek: paneldeki ilan işlemleri / aktif ilanlar sayfalarını dinamik keşfet.
            var managementUrl = await DiscoverItemciLinkAsync(
                "aktif ilanlar",
                "ilanlarım",
                "ilanlarim",
                "ilan işlemleri",
                "ilan islemleri"
            );

            if (!string.IsNullOrWhiteSpace(managementUrl))
                candidates.Add(managementUrl);

            candidates.Add("https://itemci.com/panel/ilan-islemleri");
            candidates.Add("https://itemci.com/panel/ilanlarim");
            candidates.Add("https://itemci.com/panel/ilanlar");

            List<ListingInfo> listings = new();

            foreach (var url in candidates
                         .Where(x => !string.IsNullOrWhiteSpace(x))
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    var html = await FetchItemciHtmlAsync(url);
                    var parsed = ParseCurrentListings(html, url);

                    if (parsed.Count > 0)
                    {
                        listings = parsed;
                        break;
                    }

                    // İlan İşlemleri bir üst menüyse, içindeki "Aktif İlanlarım" linkini bulup dene.
                    var nested = DiscoverListingManagementLinks(html, url);

                    foreach (var nestedUrl in nested)
                    {
                        try
                        {
                            var nestedHtml = await FetchItemciHtmlAsync(nestedUrl);
                            parsed = ParseCurrentListings(nestedHtml, nestedUrl);

                            if (parsed.Count > 0)
                            {
                                listings = parsed;
                                break;
                            }
                        }
                        catch { }
                    }

                    if (listings.Count > 0)
                        break;
                }
                catch
                {
                    // Diğer olası kaynağa geç.
                }
            }

            RenderCurrentListings(listings);

            if (listings.Count > 0)
            {
                _listingsStatusLabel.Text =
                    $"{listings.Count} aktif ilan bulundu. ⧉ = linki kopyala · ↗ = ilana git";

                _listingsStatusLabel.ForeColor = Green;
            }
            else
            {
                _listingsStatusLabel.Text =
                    "Aktif ilan listesi bulunamadı. Itemci mağaza/profil linkini algılayamadıysa hesabın profil sayfasını bir kez açıp tekrar yenile.";

                _listingsStatusLabel.ForeColor = Color.FromArgb(255, 190, 90);
            }
        }
        catch (Exception ex)
        {
            _listingsStatusLabel.Text =
                "İlanlar okunamadı: " + ex.Message;
            _listingsStatusLabel.ForeColor = Color.IndianRed;

            if (showErrors)
            {
                MessageBox.Show(
                    "Güncel ilanlar okunamadı.\n\n" + ex.Message,
                    "NiyaziSUGAR · Itemci",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
            }
        }
    }

    private async Task<string?> DiscoverLoggedInStoreUrlAsync()
    {
        if (!string.IsNullOrWhiteSpace(
            _settings.OwnStoreUrl
        ))
        {
            return _settings.OwnStoreUrl;
        }

        if (!_webReady)
            return null;

        // Açık bütün dahili sekmeleri sırayla dene.
        // Ancak RefreshOwnStoreUrlAsync yalnızca ekranın SOL hesap kartını kabul eder;
        // mesajlaştığın kişinin profilini hiçbir zaman "benim mağazam" diye kaydetmez.
        foreach (var tab in _browserTabs)
        {
            await RefreshOwnStoreUrlAsync(
                tab.WebView
            );

            if (!string.IsNullOrWhiteSpace(
                _settings.OwnStoreUrl
            ))
            {
                return _settings.OwnStoreUrl;
            }
        }

        return null;
    }


    private List<string> DiscoverListingManagementLinks(
        string html,
        string baseUrl
    )
    {
        var doc = new HtmlAgilityPack.HtmlDocument();
        doc.LoadHtml(html);

        var result = new List<(string Url, int Score)>();

        foreach (var a in doc.DocumentNode.Descendants("a"))
        {
            var href = a.Attributes["href"]?.Value;

            if (string.IsNullOrWhiteSpace(href))
                continue;

            var text = NormalizeHtmlText(a.InnerText).ToLower(System.Globalization.CultureInfo.GetCultureInfo("tr-TR"));
            var h = WebUtility.HtmlDecode(href).ToLower(System.Globalization.CultureInfo.GetCultureInfo("tr-TR"));

            var score = 0;

            if (text.Contains("aktif ilan")) score += 50;
            if (text.Contains("ilanlarım") || text.Contains("ilanlarim")) score += 40;
            if (text.Contains("ilan işlemleri") || text.Contains("ilan islemleri")) score += 25;
            if (h.Contains("aktif")) score += 12;
            if (h.Contains("ilan")) score += 10;

            if (score <= 0)
                continue;

            try
            {
                var absolute = new Uri(
                    new Uri(baseUrl),
                    href
                ).ToString();

                result.Add((absolute, score));
            }
            catch { }
        }

        return result
            .OrderByDescending(x => x.Score)
            .Select(x => x.Url)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();
    }


    private async Task<string?> DiscoverItemciLinkAsync(params string[] terms)
    {
        if (!_webReady || _webView.CoreWebView2 is null)
            return null;

        try
        {
            var termsJson = JsonSerializer.Serialize(terms);

            var script = $$"""
            (() => {
                const TERMS = {{termsJson}};

                const norm = (v) => String(v || '')
                    .toLocaleLowerCase('tr-TR')
                    .normalize('NFD')
                    .replace(/[\u0300-\u036f]/g, '')
                    .replace(/ı/g, 'i')
                    .replace(/\s+/g, ' ')
                    .trim();

                const wanted = TERMS.map(norm);

                const links = [...document.querySelectorAll('a[href]')]
                    .map(a => {
                        const text = norm(a.textContent);
                        const href = String(a.getAttribute('href') || '');
                        const h = norm(href);

                        let score = 0;

                        for (const term of wanted) {
                            if (text === term) score += 40;
                            if (text.includes(term)) score += 25;
                            if (h.includes(term.replace(/\s+/g, '-'))) score += 18;
                            if (h.includes(term.replace(/\s+/g, ''))) score += 10;
                        }

                        if (h.includes('/panel/')) score += 5;

                        return { href, score };
                    })
                    .filter(x => x.score > 0)
                    .sort((a, b) => b.score - a.score);

                if (!links.length) return null;

                try {
                    return new URL(links[0].href, location.href).href;
                } catch {
                    return null;
                }
            })();
            """;

            var raw = await _webView.CoreWebView2.ExecuteScriptAsync(script);

            if (raw == "null")
                return null;

            return JsonSerializer.Deserialize<string>(raw);
        }
        catch
        {
            return null;
        }
    }

    private List<ListingInfo> ParseCurrentListings(
        string html,
        string baseUrl
    )
    {
        var doc = new HtmlAgilityPack.HtmlDocument();
        doc.LoadHtml(html);

        var result = new List<ListingInfo>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var link in doc.DocumentNode
                     .Descendants("a")
                     .Where(x => x.Attributes["href"] is not null))
        {
            var href = WebUtility.HtmlDecode(
                link.Attributes["href"]!.Value
            );

            if (string.IsNullOrWhiteSpace(href))
                continue;

            string absolute;

            try
            {
                absolute = new Uri(
                    new Uri(baseUrl),
                    href
                ).ToString();
            }
            catch
            {
                continue;
            }

            if (!absolute.Contains(
                    "/product/",
                    StringComparison.OrdinalIgnoreCase
                ))
            {
                continue;
            }

            if (!seen.Add(absolute))
                continue;

            var container = link;

            for (var i = 0; i < 5 && container.ParentNode is not null; i++)
            {
                var parentText = NormalizeHtmlText(
                    container.ParentNode.InnerText
                );

                if (parentText.Length > 1200)
                    break;

                container = container.ParentNode;
            }

            var context = NormalizeHtmlText(container.InnerText);

            if (context.Contains("pasif", StringComparison.OrdinalIgnoreCase) ||
                context.Contains("süresi doldu", StringComparison.OrdinalIgnoreCase) ||
                context.Contains("suresi doldu", StringComparison.OrdinalIgnoreCase) ||
                context.Contains("reddedildi", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var titleNode =
                link.Descendants()
                    .FirstOrDefault(x =>
                        x.Name.Equals("h3", StringComparison.OrdinalIgnoreCase) ||
                        x.Name.Equals("h4", StringComparison.OrdinalIgnoreCase)
                    );

            var titleText = NormalizeHtmlText(
                titleNode?.InnerText ?? link.InnerText
            );

            if (string.IsNullOrWhiteSpace(titleText))
            {
                titleText = absolute
                    .Split('/')
                    .LastOrDefault()?
                    .Replace('-', ' ')
                    ?? "Itemci İlanı";
            }

            var priceMatch = Regex.Match(
                context,
                @"(?<p>\d{1,3}(?:\.\d{3})*(?:,\d{2})?|\d+(?:,\d{2})?)\s*TL",
                RegexOptions.IgnoreCase
            );

            var price = priceMatch.Success
                ? priceMatch.Groups["p"].Value + " TL"
                : "";

            result.Add(new ListingInfo
            {
                Title = titleText,
                Url = absolute,
                PriceText = price,
                Status = "AKTİF", ImageUrl = ReadListingImage(link), Server = ReadListingServer(container)
            });
        }

        return result
            .Take(200)
            .ToList();
    }

    private void LegacyRenderCurrentListings(List<ListingInfo> listings)
    {
        _listingsList.SuspendLayout();
        _listingsList.Controls.Clear();

        if (listings.Count == 0)
        {
            var empty = new Label
            {
                Width = Math.Max(520, _listingsList.ClientSize.Width - 30),
                Height = 80,
                Text = "Gösterilecek aktif ilan bulunamadı.",
                ForeColor = TextMuted,
                Font = new Font("Segoe UI", 10F),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Card
            };

            _listingsList.Controls.Add(empty);
            _listingsList.ResumeLayout();
            return;
        }

        foreach (var listing in listings)
        {
            var card = new Panel
            {
                Width = Math.Max(520, _listingsList.ClientSize.Width - 24),
                Height = 76,
                BackColor = Card,
                Margin = new Padding(0, 0, 0, 9)
            };
            card.Paint += (_, e) => DrawBorder(e.Graphics, card.ClientRectangle);

            var status = new Label
            {
                Text = listing.Status,
                AutoSize = false,
                Width = 68,
                Height = 24,
                Location = new Point(14, 12),
                ForeColor = Green,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };

            var title = new Label
            {
                Text = listing.Title,
                AutoEllipsis = true,
                Height = 28,
                Location = new Point(90, 8),
                ForeColor = TextMain,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right
            };

            var price = new Label
            {
                Text = listing.PriceText,
                AutoSize = false,
                Width = 120,
                Height = 24,
                Location = new Point(90, 39),
                ForeColor = Accent,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };

            var copy = new Button
            {
                Text = "⧉",
                Width = 38,
                Height = 34,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(29, 35, 43),
                ForeColor = Color.FromArgb(139, 198, 255),
                Font = new Font("Segoe UI Symbol", 11F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            copy.FlatAppearance.BorderColor = Border;
            _sidebarToolTip.SetToolTip(copy, "İlan linkini kopyala");

            var open = new Button
            {
                Text = "↗",
                Width = 38,
                Height = 34,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(29, 35, 43),
                ForeColor = Green,
                Font = new Font("Segoe UI Symbol", 11F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            open.FlatAppearance.BorderColor = Border;
            _sidebarToolTip.SetToolTip(open, "İlana git");

            void Reposition()
            {
                title.Width = Math.Max(160, card.ClientSize.Width - 300);
                copy.Left = card.ClientSize.Width - 94;
                copy.Top = 20;
                open.Left = card.ClientSize.Width - 50;
                open.Top = 20;
            }

            card.Resize += (_, _) => Reposition();
            Reposition();

            copy.Click += (_, _) =>
            {
                try
                {
                    Clipboard.SetText(listing.Url);
                    copy.Text = "✓";

                    var timer = new System.Windows.Forms.Timer
                    {
                        Interval = 900
                    };

                    timer.Tick += (_, _) =>
                    {
                        timer.Stop();
                        timer.Dispose();

                        if (!copy.IsDisposed)
                            copy.Text = "⧉";
                    };

                    timer.Start();
                }
                catch { }
            };

            open.Click += async (_, _) =>
            {
                ShowPanel(_browserPanel);

                await OpenSmartBrowserTabAsync(
                    listing.Url,
                    listing.Title,
                    activate: true
                );
            };

            card.Controls.Add(status);
            card.Controls.Add(title);
            card.Controls.Add(price);
            card.Controls.Add(copy);
            card.Controls.Add(open);

            _listingsList.Controls.Add(card);
        }

        _listingsList.ResumeLayout();
    }

    private async Task OpenServerListingCreateAsync(
        MarketServerInfo server
    )
    {
        if (!_webReady)
        {
            MessageBox.Show(
                "Itemci henüz hazır değil.",
                "İlan Oluştur",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
            return;
        }

        _pendingListingServerId =
            server.Id;

        _pendingListingServerName =
            server.Name;

        _pendingListingCategoryUrl =
            server.Url;

        _pendingListingAttempts = 0;
        _pendingListingWebView = null;

        var createUrl =
            await DiscoverItemciLinkAsync(
                "ilan ver",
                "ilan oluştur",
                "ilan olustur"
            );

        if (string.IsNullOrWhiteSpace(
            createUrl
        ))
        {
            // Ana sayfayı yeni dahili sekmede aç; orada gerçek İLAN VER linki var.
            var homeTab =
                await OpenSmartBrowserTabAsync(
                    "https://itemci.com/",
                    "İlan Oluştur",
                    activate: true
                );

            ShowPanel(_browserPanel);

            MessageBox.Show(
                $"{(server.IsDefault ? "MSP2" : server.Name)} ilan oluşturma için Itemci ana sayfası açıldı.\n\n" +
                "Sayfa yüklendikten sonra sol menüdeki ilan oluştur düğmesine tekrar bas.",
                "İlan Oluştur",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );

            return;
        }

        ShowPanel(_browserPanel);

        var tab =
            await OpenSmartBrowserTabAsync(
                createUrl,
                server.IsDefault
                    ? "MSP2 İlan Oluştur"
                    : $"{server.Name} İlan Oluştur",
                activate: true
            );

        if (tab is null)
            return;

        _pendingListingWebView =
            tab.WebView;
    }

    private async Task TryAutoSelectPendingListingAsync(
        WebView2 view
    )
    {
        if (
            string.IsNullOrWhiteSpace(
                _pendingListingServerName
            ) ||
            view.CoreWebView2 is null
        )
        {
            return;
        }

        if (_pendingListingAttempts >= 4)
        {
            _pendingListingServerId = null;
            _pendingListingServerName = null;
            _pendingListingCategoryUrl = null;
            _pendingListingWebView = null;
            _pendingListingAttempts = 0;
            return;
        }

        _pendingListingAttempts++;

        try
        {
            var serverNameJson =
                JsonSerializer.Serialize(
                    _pendingListingServerName
                );

            var categoryUrlJson =
                JsonSerializer.Serialize(
                    _pendingListingCategoryUrl ?? ""
                );

            var script = $$"""
            (async () => {
                const SERVER_NAME = {{serverNameJson}};
                const CATEGORY_URL = {{categoryUrlJson}};

                const sleep = ms =>
                    new Promise(r =>
                        setTimeout(r, ms)
                    );

                const norm = v =>
                    String(v || '')
                        .toLocaleLowerCase('tr-TR')
                        .normalize('NFD')
                        .replace(/[\u0300-\u036f]/g,'')
                        .replace(/ı/g,'i')
                        .replace(/\s+/g,' ')
                        .trim();

                const visible = el => {
                    if (!el) return false;

                    const r =
                        el.getBoundingClientRect();

                    const s =
                        getComputedStyle(el);

                    return (
                        r.width > 0 &&
                        r.height > 0 &&
                        s.display !== 'none' &&
                        s.visibility !== 'hidden'
                    );
                };

                const allCandidates = () => [
                    ...document.querySelectorAll(
                        'button,a,label,[role="button"],[tabindex],div,span,p'
                    )
                ].filter(visible);

                function actualClickable(el) {
                    if (!el) return null;

                    if (
                        el.matches(
                            'button,a,label,[role="button"],[tabindex]'
                        )
                    ) return el;

                    return el.closest(
                        'button,a,label,[role="button"],[tabindex]'
                    ) || el;
                }

                function bestExact(text) {
                    const wanted =
                        norm(text);

                    const rows =
                        allCandidates()
                        .map(el => {
                            const txt =
                                norm(el.textContent);

                            if (!txt)
                                return null;

                            const r =
                                el.getBoundingClientRect();

                            let score = 0;

                            if (txt === wanted)
                                score += 300;
                            else if (
                                txt.includes(wanted)
                            )
                                score += 100;
                            else
                                return null;

                            const area =
                                Math.max(
                                    1,
                                    r.width * r.height
                                );

                            // Küçük gerçek seçim kartı / butonu breadcrumb devlerinden öne çıkar.
                            score +=
                                Math.max(
                                    0,
                                    80 -
                                    Math.log10(area) * 12
                                );

                            if (
                                el.matches(
                                    'button,[role="button"],label'
                                )
                            ) score += 40;

                            const cls =
                                norm(
                                    el.className || ''
                                );

                            if (
                                cls.includes(
                                    'breadcrumb'
                                )
                            ) score -= 120;

                            if (
                                r.height >= 30 &&
                                r.height <= 150
                            ) score += 20;

                            return {
                                el,
                                score,
                                area
                            };
                        })
                        .filter(Boolean)
                        .sort(
                            (a,b) =>
                                b.score-a.score ||
                                a.area-b.area
                        );

                    return rows[0]?.el || null;
                }

                function bodyHas(text) {
                    return norm(
                        document.body?.innerText ||
                        document.body?.textContent ||
                        ''
                    ).includes(
                        norm(text)
                    );
                }

                async function clickExact(
                    text,
                    mustAppearText=null
                ) {
                    for (
                        let attempt=0;
                        attempt<24;
                        attempt++
                    ) {
                        const found =
                            bestExact(text);

                        if (found) {
                            const clickTarget =
                                actualClickable(found);

                            clickTarget
                                .scrollIntoView({
                                    block:'center',
                                    behavior:'instant'
                                });

                            clickTarget.click();

                            await sleep(400);

                            if (
                                !mustAppearText ||
                                bodyHas(
                                    mustAppearText
                                )
                            ) {
                                return true;
                            }
                        }

                        await sleep(180);
                    }

                    return false;
                }

                async function findTitleInput() {
                    for (
                        let attempt=0;
                        attempt<30;
                        attempt++
                    ) {
                        const inputs = [
                            ...document.querySelectorAll(
                                'input,textarea'
                            )
                        ].filter(visible);

                        const scored =
                            inputs
                            .map(el => {
                                const ph =
                                    norm(
                                        el.getAttribute(
                                            'placeholder'
                                        )
                                    );

                                const aria =
                                    norm(
                                        el.getAttribute(
                                            'aria-label'
                                        )
                                    );

                                const name =
                                    norm(
                                        el.getAttribute(
                                            'name'
                                        )
                                    );

                                let score = 0;

                                if (
                                    ph.includes(
                                        'ilan basligi'
                                    ) ||
                                    ph.includes(
                                        'ilan başlığı'
                                    )
                                ) score += 100;

                                if (
                                    ph.includes(
                                        'baslik'
                                    ) ||
                                    ph.includes(
                                        'başlık'
                                    )
                                ) score += 70;

                                if (
                                    aria.includes(
                                        'ilan basligi'
                                    ) ||
                                    aria.includes(
                                        'ilan başlığı'
                                    )
                                ) score += 90;

                                if (
                                    name.includes(
                                        'title'
                                    ) ||
                                    name.includes(
                                        'baslik'
                                    )
                                ) score += 50;

                                return {
                                    el,
                                    score
                                };
                            })
                            .filter(
                                x => x.score > 0
                            )
                            .sort(
                                (a,b) =>
                                    b.score-a.score
                            );

                        let input =
                            scored[0]?.el || null;

                        if (!input) {
                            const labels = [
                                ...document.querySelectorAll(
                                    'label,div,span,p'
                                )
                            ].filter(
                                el =>
                                    visible(el) &&
                                    (
                                        norm(el.textContent) ===
                                            'ilan basligi' ||
                                        norm(el.textContent) ===
                                            'ilan başlığı'
                                    )
                            );

                            for (
                                const label of labels
                            ) {
                                const forId =
                                    label.getAttribute(
                                        'for'
                                    );

                                if (forId) {
                                    input =
                                        document.getElementById(
                                            forId
                                        );

                                    if (input)
                                        break;
                                }

                                input =
                                    label.parentElement
                                        ?.querySelector(
                                            'input,textarea'
                                        ) ||
                                    label.nextElementSibling
                                        ?.matches?.(
                                            'input,textarea'
                                        )
                                        ? label.nextElementSibling
                                        : null;

                                if (input)
                                    break;
                            }
                        }

                        if (
                            input &&
                            visible(input)
                        ) {
                            input.scrollIntoView({
                                block:'center',
                                behavior:'instant'
                            });

                            await sleep(250);
                            input.focus();
                            input.click();

                            return true;
                        }

                        await sleep(220);
                    }

                    return false;
                }

                // Oyun
                if (
                    !bodyHas(
                        'Metin2 PvP Serverler'
                    )
                ) {
                    await clickExact(
                        'Metin2 PvP Serverler',
                        'Metin2 PvP Serverler'
                    );
                }

                // Server
                await clickExact(
                    SERVER_NAME,
                    SERVER_NAME
                );

                // Alt kategori:
                // Kullanıcı özellikle MSP2 Global - Yeni Item&Yang seçili olsun istiyor.
                const subCategory =
                    `${SERVER_NAME} Item&Yang`;

                const subOk =
                    await clickExact(
                        subCategory,
                        subCategory
                    ) ||
                    await clickExact(
                        'Item&Yang',
                        'Item&Yang'
                    ) ||
                    await clickExact(
                        'Item & Yang',
                        'Item & Yang'
                    );

                await sleep(650);

                const titleFocused =
                    await findTitleInput();

                return JSON.stringify({
                    ok:
                        subOk &&
                        titleFocused,
                    subCategory,
                    titleFocused,
                    page:location.href
                });
            })();
            """;

            var raw =
                await view.CoreWebView2
                    .ExecuteScriptAsync(script);

            var json =
                JsonSerializer.Deserialize<string>(
                    raw
                );

            if (
                string.IsNullOrWhiteSpace(json)
            )
                return;

            var result =
                JsonSerializer.Deserialize<ListingCreateResult>(
                    json,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive =
                            true
                    }
                );

            if (result?.Ok == true)
            {
                _pendingListingServerId = null;
                _pendingListingServerName = null;
                _pendingListingCategoryUrl = null;
                _pendingListingWebView = null;
                _pendingListingAttempts = 0;
            }
        }
        catch
        {
            // Sayfa SPA ise bir sonraki navigation tamamlanmasında tekrar denenir.
        }
    }


    private void BuildSalesPanel()
    {
        _salesPanel.Dock = DockStyle.Fill;
        _salesPanel.BackColor = Bg;
        _salesPanel.Padding = new Padding(26);

        var titleRow = new Panel
        {
            Dock = DockStyle.Top,
            Height = 64,
            BackColor = Bg
        };

        var title = new Label
        {
            Dock = DockStyle.Left,
            Width = 390,
            Text = "Satışlar / Günlük Kâr",
            ForeColor = TextMain,
            Font = new Font("Segoe UI", 22F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };

        var refreshButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 350,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Bg,
            Padding = new Padding(0, 6, 0, 0)
        };

        var refresh = ActionButton("YENİ SATIŞLARI YENİLE", () =>
        {
            _ = RefreshSoldListingsAsync(showErrors: true, fullHistory: false);
        });
        refresh.Width = 165;

        var allHistory = ActionButton("TÜM GEÇMİŞİ TARA", () =>
        {
            _ = RefreshSoldListingsAsync(showErrors: true, fullHistory: true);
        });
        allHistory.Width = 160;

        refreshButtons.Controls.Add(refresh);
        refreshButtons.Controls.Add(allHistory);

        titleRow.Controls.Add(refreshButtons);
        titleRow.Controls.Add(title);

        var cards = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 145,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 10, 0, 0)
        };

        cards.Controls.Add(SalesMetricCard("BUGÜNKÜ SATIŞ", _salesGrossLabel, "0,00 TL", Accent));
        cards.Controls.Add(SalesMetricCard("KOMİSYON (%10)", _salesCommissionLabel, "0,00 TL", Color.FromArgb(255, 110, 110)));
        cards.Controls.Add(SalesMetricCard("NET KAZANÇ", _salesNetLabel, "0,00 TL", Green));
        cards.Controls.Add(SalesMetricCard("SATIŞ ADEDİ", _salesCountLabel, "0", TextMain));

        var statusCard = new Panel
        {
            Dock = DockStyle.Top,
            Height = 72,
            BackColor = Card,
            Padding = new Padding(16),
            Margin = new Padding(0, 8, 0, 10)
        };
        statusCard.Paint += (_, e) => DrawBorder(e.Graphics, statusCard.ClientRectangle);

        _salesStatusLabel.Dock = DockStyle.Fill;
        _salesStatusLabel.Text = "Itemci bildirimleri taranmayı bekliyor...";
        _salesStatusLabel.ForeColor = TextMuted;
        _salesStatusLabel.Font = new Font("Segoe UI", 9F);
        _salesStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
        statusCard.Controls.Add(_salesStatusLabel);

        var tableCard = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Card,
            Padding = new Padding(1)
        };
        tableCard.Paint += (_, e) => DrawBorder(e.Graphics, tableCard.ClientRectangle);

        _salesGrid.Dock = DockStyle.Fill;
        _salesGrid.BackgroundColor = Color.FromArgb(15, 18, 23);
        _salesGrid.BorderStyle = BorderStyle.None;
        _salesGrid.EnableHeadersVisualStyles = false;
        _salesGrid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(28, 33, 41);
        _salesGrid.ColumnHeadersDefaultCellStyle.ForeColor = Accent;
        _salesGrid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        _salesGrid.ColumnHeadersHeight = 36;
        _salesGrid.DefaultCellStyle.BackColor = Color.FromArgb(20, 24, 30);
        _salesGrid.DefaultCellStyle.ForeColor = TextMain;
        _salesGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(39, 45, 55);
        _salesGrid.DefaultCellStyle.SelectionForeColor = TextMain;
        _salesGrid.DefaultCellStyle.Font = new Font("Segoe UI", 9F);
        _salesGrid.RowHeadersVisible = false;
        _salesGrid.AllowUserToAddRows = false;
        _salesGrid.AllowUserToDeleteRows = false;
        _salesGrid.AllowUserToResizeRows = false;
        _salesGrid.ReadOnly = true;
        _salesGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _salesGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _salesGrid.MultiSelect = false;

        _salesGrid.Columns.Clear();
        _salesGrid.Columns.Add("Date", "TARİH");
        _salesGrid.Columns.Add("Time", "SAAT");
        _salesGrid.Columns.Add("Item", "SATIŞ / ÜRÜN");
        _salesGrid.Columns.Add("Status", "DURUM");
        _salesGrid.Columns.Add("Gross", "SATIŞ");
        _salesGrid.Columns.Add("Commission", "KOMİSYON");
        _salesGrid.Columns.Add("Net", "NET");
        _salesGrid.Columns.Add("Order", "SİPARİŞ NO");

        _salesGrid.Columns["Date"]!.FillWeight = 68;
        _salesGrid.Columns["Time"]!.FillWeight = 48;
        _salesGrid.Columns["Item"]!.FillWeight = 210;
        _salesGrid.Columns["Status"]!.FillWeight = 82;
        _salesGrid.Columns["Gross"]!.FillWeight = 76;
        _salesGrid.Columns["Commission"]!.FillWeight = 76;
        _salesGrid.Columns["Net"]!.FillWeight = 76;
        _salesGrid.Columns["Order"]!.FillWeight = 95;

        tableCard.Controls.Add(_salesGrid);

        _salesPanel.Controls.Add(tableCard);
        _salesPanel.Controls.Add(statusCard);
        _salesPanel.Controls.Add(cards);
        _salesPanel.Controls.Add(titleRow);
    }

    private Panel SalesMetricCard(string caption, Label valueLabel, string initialValue, Color valueColor)
    {
        var p = new Panel
        {
            Width = 235,
            Height = 108,
            BackColor = Card,
            Margin = new Padding(0, 0, 10, 0),
            Padding = new Padding(15)
        };
        p.Paint += (_, e) => DrawBorder(e.Graphics, p.ClientRectangle);

        var captionLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 25,
            Text = caption,
            ForeColor = TextMuted,
            Font = new Font("Segoe UI", 8F, FontStyle.Bold)
        };

        valueLabel.Dock = DockStyle.Fill;
        valueLabel.Text = initialValue;
        valueLabel.ForeColor = valueColor;
        valueLabel.Font = new Font("Segoe UI", 18F, FontStyle.Bold);
        valueLabel.TextAlign = ContentAlignment.MiddleLeft;

        p.Controls.Add(valueLabel);
        p.Controls.Add(captionLabel);
        return p;
    }

    private void BuildMarketPanel()
    {
        _marketPanel.Dock = DockStyle.Fill;
        _marketPanel.BackColor = Bg;

        var toolbar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 64,
            BackColor = Side,
            Padding = new Padding(12, 8, 12, 8)
        };

        _marketTitleLabel.Dock = DockStyle.Left;
        _marketTitleLabel.Width = 390;
        _marketTitleLabel.Text = "Piyasa Analizi";
        _marketTitleLabel.ForeColor = TextMain;
        _marketTitleLabel.Font = new Font("Segoe UI", 18F, FontStyle.Bold);
        _marketTitleLabel.TextAlign = ContentAlignment.MiddleLeft;

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 540,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 4, 0, 0)
        };

        var openAnalyzer = ActionButton("SERVER YÖNETİMİ", async () =>
        {
            await OpenEmbeddedMarketAnalyzerAsync();
        });
        openAnalyzer.Width = 140;

        var itemciHome = ActionButton("ITEMCI ANA SAYFA", () =>
        {
            if (_marketWebReady)
                _marketWebView.Source = new Uri("https://itemci.com/");
        });
        itemciHome.Width = 140;

        var reload = ActionButton("YENİLE", () =>
        {
            if (_marketWebReady)
            {
                _marketAnalyzerInjected = false;
                _marketWebView.Reload();
            }
        });
        reload.Width = 90;

        actions.Controls.Add(openAnalyzer);
        actions.Controls.Add(itemciHome);
        actions.Controls.Add(reload);

        toolbar.Controls.Add(actions);
        toolbar.Controls.Add(_marketTitleLabel);

        var infoBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 42,
            BackColor = Color.FromArgb(18, 21, 27),
            Padding = new Padding(14, 8, 14, 8)
        };

        var info = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = TextMuted,
            Font = new Font("Segoe UI", 8.5F),
            Text =
                "Mevcut server/kategori analiz sistemin artık uygulamanın içinde çalışıyor. " +
                "Server seçebilir, kendi kategorilerini oluşturabilir ve ilanları tarayabilirsin."
        };
        infoBar.Controls.Add(info);

        _marketWebView.Dock = DockStyle.Fill;
        _marketWebView.BackColor = Bg;

        _marketPanel.Controls.Add(_marketWebView);
        _marketPanel.Controls.Add(infoBar);
        _marketPanel.Controls.Add(toolbar);
    }

    private async Task InjectMarketAnalyzerAsync()
    {
        if (!_marketWebReady ||
            _marketWebView.CoreWebView2 is null ||
            _marketAnalyzerInjected)
        {
            return;
        }

        try
        {
            var analyzerPath = Path.Combine(
                AppContext.BaseDirectory,
                "MarketAnalyzer.js"
            );

            if (!File.Exists(analyzerPath))
                throw new FileNotFoundException(
                    "MarketAnalyzer.js bulunamadı.",
                    analyzerPath
                );

            var script = await File.ReadAllTextAsync(analyzerPath);
            await _marketWebView.CoreWebView2.ExecuteScriptAsync(script); if (!string.IsNullOrWhiteSpace(_settings.OwnStoreUrl)) await _marketWebView.CoreWebView2.ExecuteScriptAsync("window.__niyaziVerifiedOwnStoreUrl=" + JsonSerializer.Serialize(_settings.OwnStoreUrl));

            _marketAnalyzerInjected = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Piyasa analiz motoru yüklenemedi.\n\n" + ex.Message,
                "NiyaziSUGAR · Itemci",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );
        }
    }

    private async Task OpenEmbeddedMarketAnalyzerAsync()
    {
        _marketWebView.CoreWebView2?.Resume();
        if (!_marketWebReady)
        {
            MessageBox.Show(
                "Piyasa analiz tarayıcısı henüz hazırlanıyor.",
                "NiyaziSUGAR · Itemci",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
            return;
        }

        await InjectMarketAnalyzerAsync();

        try
        {
            if (_marketWebView.CoreWebView2 is null)
                return;

            var result = await _marketWebView.CoreWebView2.ExecuteScriptAsync(
                "typeof window.__NiyazisugarOpenMarket === 'function'"
            );

            if (!string.Equals(result, "true", StringComparison.OrdinalIgnoreCase))
            {
                _marketAnalyzerInjected = false;
                await InjectMarketAnalyzerAsync();
            }

            await _marketWebView.CoreWebView2.ExecuteScriptAsync(
                "window.__NiyazisugarOpenMarket && window.__NiyazisugarOpenMarket();"
            );
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Analiz menüsü açılamadı.\n\n" + ex.Message,
                "NiyaziSUGAR · Itemci",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );
        }
    }

    private async Task RefreshMarketServersAsync()
    {
        _marketWebView.CoreWebView2?.Resume();
        if (!_marketWebReady || _marketWebView.CoreWebView2 is null)
            return;

        await InjectMarketAnalyzerAsync();

        try
        {
            var raw = await _marketWebView.CoreWebView2.ExecuteScriptAsync(
                "window.__NiyazisugarGetServersJson ? window.__NiyazisugarGetServersJson() : '[]'"
            );

            var json = JsonSerializer.Deserialize<string>(raw);

            if (string.IsNullOrWhiteSpace(json))
                return;

            var servers = JsonSerializer.Deserialize<List<MarketServerInfo>>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }
            );

            if (servers is null || servers.Count == 0)
                return;

            ApplyMarketServers(servers);
        }
        catch
        {
            // Default MSP2 butonu kalmaya devam eder.
        }
    }

    private void ApplyMarketServers(List<MarketServerInfo> servers)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => ApplyMarketServers(servers));
            return;
        }

        _marketServers.Clear();

        foreach (var server in servers)
        {
            if (string.IsNullOrWhiteSpace(server.Id) ||
                string.IsNullOrWhiteSpace(server.Name))
            {
                continue;
            }

            server.IsDefault =
                server.IsDefault ||
                server.Id.Equals(
                    "msp2-global-yeni",
                    StringComparison.OrdinalIgnoreCase
                );

            _marketServers.Add(server);
        }

        if (!_marketServers.Any(x => x.IsDefault))
        {
            _marketServers.Insert(
                0,
                new MarketServerInfo
                {
                    Id = "msp2-global-yeni",
                    Name = "MSP2 Global - Yeni",
                    CategoryCount = 3,
                    IsDefault = true
                }
            );
        }

        RebuildSidebarNavigation();
    }

    private async Task OpenMarketAddServerAsync()
    {
        _marketWebView.CoreWebView2?.Resume();
        if (!_marketWebReady)
        {
            MessageBox.Show(
                "Piyasa analiz sistemi henüz hazırlanıyor.",
                "NiyaziSUGAR · Itemci",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
            return;
        }

        await InjectMarketAnalyzerAsync();

        try
        {
            await _marketWebView.CoreWebView2.ExecuteScriptAsync(
                "window.__NiyazisugarAddServer && window.__NiyazisugarAddServer();"
            );
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Server ekleme ekranı açılamadı.\n\n" + ex.Message,
                "NiyaziSUGAR · Itemci",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );
        }
    }

    private async Task RunMarketServerAsync(string serverId, string serverName)
    {
        _marketWebView.CoreWebView2?.Resume();
        if (!_marketWebReady || _marketWebView.CoreWebView2 is null)
        {
            MessageBox.Show(
                "Piyasa analiz sistemi henüz hazırlanıyor.",
                "NiyaziSUGAR · Itemci",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
            return;
        }

        await InjectMarketAnalyzerAsync();

        try
        {
            _marketTitleLabel.Text =
                serverId.Equals(
                    "msp2-global-yeni",
                    StringComparison.OrdinalIgnoreCase
                )
                    ? "MSP2 Piyasa Analiz"
                    : $"{serverName} Piyasa Analiz";

            var idJson = JsonSerializer.Serialize(serverId);

            await _marketWebView.CoreWebView2.ExecuteScriptAsync(
                $"window.__NiyazisugarRunServerById && window.__NiyazisugarRunServerById({idJson});"
            );
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Server analizi başlatılamadı.\n\n" + ex.Message,
                "NiyaziSUGAR · Itemci",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );
        }
    }

    private async Task EditMarketServerAsync(string serverId)
    {
        _marketWebView.CoreWebView2?.Resume();
        if (!_marketWebReady || _marketWebView.CoreWebView2 is null)
            return;

        await InjectMarketAnalyzerAsync();

        try
        {
            var idJson = JsonSerializer.Serialize(serverId);

            await _marketWebView.CoreWebView2.ExecuteScriptAsync(
                $"window.__NiyazisugarEditServerById && window.__NiyazisugarEditServerById({idJson});"
            );
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Server kategorileri açılamadı.\n\n" + ex.Message,
                "NiyaziSUGAR · Itemci",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );
        }
    }

    private async Task DeleteMarketServerAsync(string serverId, string serverName)
    {
        _marketWebView.CoreWebView2?.Resume();
        if (!_marketWebReady || _marketWebView.CoreWebView2 is null)
            return;

        var answer = MessageBox.Show(
            $"\"{serverName}\" serverini analiz listesinden silmek istiyor musun?",
            "Server Sil",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question
        );

        if (answer != DialogResult.Yes)
            return;

        await InjectMarketAnalyzerAsync();

        try
        {
            var idJson = JsonSerializer.Serialize(serverId);

            await _marketWebView.CoreWebView2.ExecuteScriptAsync(
                $"window.__NiyazisugarDeleteServerById && window.__NiyazisugarDeleteServerById({idJson});"
            );

            await Task.Delay(150);
            await RefreshMarketServersAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Server silinemedi.\n\n" + ex.Message,
                "NiyaziSUGAR · Itemci",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );
        }
    }

    private void HandleMarketWebMessage(
        CoreWebView2WebMessageReceivedEventArgs e
    )
    {
        try
        {
            var message = e.TryGetWebMessageAsString();

            if (string.IsNullOrWhiteSpace(message))
                return;

            var bridge = JsonSerializer.Deserialize<MarketBridgeMessage>(
                message,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }
            );

            if (bridge is null)
                return;

            if (string.Equals(
                bridge.Type,
                "serversChanged",
                StringComparison.OrdinalIgnoreCase
            ) &&
            bridge.Servers is { Count: > 0 })
            {
                ApplyMarketServers(bridge.Servers);
            }
        }
        catch
        {
            // Analiz sayfasından gelen önemsiz mesajları yok say.
        }
    }


    private void BuildSettingsPanel()
    {
        _settingsPanel.Dock = DockStyle.Fill;
        _settingsPanel.BackColor = Bg;
        _settingsPanel.Padding = new Padding(26);
        _settingsPanel.AutoScroll = true;

        var title = new Label
        {
            Dock = DockStyle.Top,
            Height = 58,
            Text = "Ayarlar",
            ForeColor = TextMain,
            Font = new Font("Segoe UI", 22F, FontStyle.Bold)
        };

        var card = new Panel
        {
            Dock = DockStyle.Top,
            Height = 300,
            BackColor = Card,
            Padding = new Padding(18),
            Margin = new Padding(0, 0, 0, 14)
        };
        card.Paint += (_, e) => DrawBorder(e.Graphics, card.ClientRectangle);

        _notificationsCheck = SettingCheck("Windows bildirimleri açık", _settings.NotificationsEnabled);
        _soundCheck = SettingCheck("Bildirim sesi açık", _settings.SoundEnabled);
        _startupCheck = SettingCheck("Windows açılınca uygulamayı başlat", _settings.StartWithWindows);
        _adBlockCheck = SettingCheck("Reklam engelleyici açık (Itemci + tarayıcı)", _settings.AdBlockEnabled);

        _notificationsCheck.Location = new Point(18, 24);
        _soundCheck.Location = new Point(18, 64);
        _startupCheck.Location = new Point(18, 104);
        _adBlockCheck.Location = new Point(18, 144);

        var reminderLabel = new Label
        {
            Text = "Cevap bekleyen mesaj tekrar uyarısı (dakika)",
            ForeColor = TextMuted,
            AutoSize = true,
            Location = new Point(18, 190)
        };

        _reminderInput = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 60,
            Value = Math.Clamp(_settings.ReminderMinutes, 1, 60),
            Location = new Point(330, 186),
            Width = 70,
            BackColor = Color.FromArgb(18, 21, 26),
            ForeColor = TextMain,
            BorderStyle = BorderStyle.FixedSingle
        };

        var save = ActionButton("AYARLARI KAYDET", SaveSettings);
        save.Location = new Point(18, 232);

        var adInfo = new Label
        {
            Text = "Reklam koruması: reklam ağlarını engeller ve Itemci'deki reklam katmanlarını gizler.",
            ForeColor = Color.FromArgb(104, 115, 130),
            AutoSize = true,
            Location = new Point(420, 147),
            Font = new Font("Segoe UI", 8F)
        };

        card.Controls.Add(_notificationsCheck);
        card.Controls.Add(_soundCheck);
        card.Controls.Add(_startupCheck);
        card.Controls.Add(_adBlockCheck);
        card.Controls.Add(reminderLabel);
        card.Controls.Add(_reminderInput);
        card.Controls.Add(adInfo);
        card.Controls.Add(save);

        var updateCard = new Panel
        {
            Dock = DockStyle.Top,
            Height = 270,
            BackColor = Card,
            Padding = new Padding(18),
            Margin = new Padding(0, 14, 0, 0)
        };
        updateCard.Paint += (_, e) => DrawBorder(e.Graphics, updateCard.ClientRectangle);

        var updateTitle = new Label
        {
            Text = "Uygulama Güncellemeleri",
            ForeColor = TextMain,
            Font = new Font("Segoe UI", 13F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(18, 17)
        };

        var updateSub = new Label
        {
            Text = "Yeni sürümler artık uygulamanın içinden tek tıkla indirilebilir.",
            ForeColor = TextMuted,
            Font = new Font("Segoe UI", 8.5F),
            AutoSize = true,
            Location = new Point(18, 48)
        };

        _autoUpdateCheck = SettingCheck(
            "Program açılırken güncellemeyi otomatik kontrol et",
            _settings.AutoUpdateCheck
        );
        _autoUpdateCheck.Location = new Point(18, 79);

        _updateCurrentLabel = new Label
        {
            Text = $"Mevcut sürüm: V{UpdateService.CurrentVersion}",
            ForeColor = TextMuted,
            AutoSize = true,
            Location = new Point(18, 117)
        };

        _updateLatestLabel = new Label
        {
            Text = "Son sürüm: Henüz kontrol edilmedi",
            ForeColor = TextMuted,
            AutoSize = true,
            Location = new Point(220, 117)
        };

        _updateStatusLabel = new Label
        {
            Text = "Hazır",
            ForeColor = Color.FromArgb(104, 115, 130),
            AutoSize = true,
            Location = new Point(18, 148)
        };

        _updateNotesLabel = new Label
        {
            Text = "",
            ForeColor = TextMuted,
            AutoEllipsis = true,
            Location = new Point(18, 174),
            Size = new Size(610, 38)
        };

        _updateCheckButton = new Button
        {
            Text = "GÜNCELLEMEYİ DENETLE",
            Width = 190,
            Height = 38,
            Location = new Point(18, 216),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(31, 36, 44),
            ForeColor = TextMain,
            Cursor = Cursors.Hand
        };
        _updateCheckButton.FlatAppearance.BorderColor = Border;
        _updateCheckButton.Click += async (_, _) =>
            await CheckForUpdatesAsync(silent: false);

        _updateInstallButton = new Button
        {
            Text = "İNDİR VE GÜNCELLE",
            Width = 180,
            Height = 38,
            Location = new Point(220, 216),
            FlatStyle = FlatStyle.Flat,
            BackColor = Accent,
            ForeColor = Color.FromArgb(22, 18, 9),
            Cursor = Cursors.Hand,
            Visible = false,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold)
        };
        _updateInstallButton.FlatAppearance.BorderColor = Color.FromArgb(210, 139, 0);
        _updateInstallButton.Click += async (_, _) =>
            await InstallAvailableUpdateAsync();

        updateCard.Controls.Add(updateTitle);
        updateCard.Controls.Add(updateSub);
        updateCard.Controls.Add(_autoUpdateCheck);
        updateCard.Controls.Add(_updateCurrentLabel);
        updateCard.Controls.Add(_updateLatestLabel);
        updateCard.Controls.Add(_updateStatusLabel);
        updateCard.Controls.Add(_updateNotesLabel);
        updateCard.Controls.Add(_updateCheckButton);
        updateCard.Controls.Add(_updateInstallButton);

        _settingsPanel.Controls.Add(updateCard);
        _settingsPanel.Controls.Add(card);
        _settingsPanel.Controls.Add(title);
    }

    private async Task CheckForUpdatesAsync(bool silent)
    {
        if (_updateCheckRunning)
            return;

        _updateCheckRunning = true;

        if (_updateCheckButton is not null)
            _updateCheckButton.Enabled = false;

        if (_updateStatusLabel is not null && !silent)
        {
            _updateStatusLabel.Text = "Güncelleme kontrol ediliyor...";
            _updateStatusLabel.ForeColor = Accent;
        }

        try
        {
            var manifest = await UpdateService.GetLatestAsync();

            if (_updateLatestLabel is not null)
                _updateLatestLabel.Text = $"Son sürüm: V{manifest?.Version ?? "-"}";

            if (manifest is not null &&
                UpdateService.IsNewerVersion(manifest.Version))
            {
                _availableUpdate = manifest;

                if (_updateStatusLabel is not null)
                {
                    _updateStatusLabel.Text = $"Yeni sürüm hazır: V{manifest.Version}";
                    _updateStatusLabel.ForeColor = Green;
                }

                if (_updateNotesLabel is not null)
                {
                    _updateNotesLabel.Text = string.IsNullOrWhiteSpace(manifest.Notes)
                        ? "Yeni sürüm indirilmeye hazır."
                        : manifest.Notes.Replace("\r", " ").Replace("\n", " • ");
                }

                if (_updateInstallButton is not null)
                    _updateInstallButton.Visible = true;

                if (silent)
                {
                    ShowDesktopNotification(
                        $"Yeni sürüm: V{manifest.Version}",
                        "Ayarlar > Güncellemeler bölümünden tek tıkla kurabilirsin.",
                        ToolTipIcon.Info,
                        onClick: () => { RestoreFromTray(); ShowPanel(_settingsPanel); }
                    );
                }
            }
            else
            {
                _availableUpdate = null;

                if (_updateStatusLabel is not null)
                {
                    _updateStatusLabel.Text = "Uygulama güncel.";
                    _updateStatusLabel.ForeColor = Green;
                }

                if (_updateNotesLabel is not null)
                    _updateNotesLabel.Text = "En son NiyaziSUGAR · Itemci sürümünü kullanıyorsun.";

                if (_updateInstallButton is not null)
                    _updateInstallButton.Visible = false;
            }
        }
        catch (Exception ex)
        {
            if (!silent)
            {
                if (_updateStatusLabel is not null)
                {
                    _updateStatusLabel.Text =
                        "Güncelleme sunucusuna ulaşılamadı.";
                    _updateStatusLabel.ForeColor = Color.IndianRed;
                }

                if (_updateNotesLabel is not null)
                    _updateNotesLabel.Text = ex.Message;
            }
        }
        finally
        {
            _updateCheckRunning = false;

            if (_updateCheckButton is not null)
                _updateCheckButton.Enabled = true;
        }
    }

    private async Task InstallAvailableUpdateAsync()
    {
        if (_availableUpdate is null)
        {
            await CheckForUpdatesAsync(silent: false);
            if (_availableUpdate is null)
                return;
        }

        if (_updateInstallButton is null)
            return;

        _updateInstallButton.Enabled = false;

        try
        {
            var progress = new Progress<int>(percent =>
            {
                _updateInstallButton.Text = $"İNDİRİLİYOR %{percent}";

                if (_updateStatusLabel is not null)
                {
                    _updateStatusLabel.Text =
                        $"Güncelleme indiriliyor... %{percent}";
                    _updateStatusLabel.ForeColor = Accent;
                }
            });

            var packagePath = await UpdateService.DownloadPackageAsync(
                _availableUpdate,
                progress
            );

            if (_updateStatusLabel is not null)
            {
                _updateStatusLabel.Text =
                    "İndirme tamamlandı. Uygulama yeniden başlatılıyor...";
                _updateStatusLabel.ForeColor = Green;
            }

            UpdateService.StartUpdaterAndExit(
                packagePath,
                Environment.ProcessId
            );

            _allowExit = true;
            _trayIcon.Visible = false;

            await Task.Delay(350);
            Application.Exit();
        }
        catch (Exception ex)
        {
            _updateInstallButton.Enabled = true;
            _updateInstallButton.Text = "İNDİR VE GÜNCELLE";

            if (_updateStatusLabel is not null)
            {
                _updateStatusLabel.Text = "Güncelleme kurulamadı.";
                _updateStatusLabel.ForeColor = Color.IndianRed;
            }

            MessageBox.Show(
                "Güncelleme sırasında hata oluştu.\n\n" + ex.Message,
                "NiyaziSUGAR · Itemci",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }
    }

    private CheckBox SettingCheck(string text, bool value)
    {
        return new CheckBox
        {
            Text = text,
            Checked = value,
            AutoSize = true,
            ForeColor = TextMain,
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 9.5F)
        };
    }

    private void SaveSettings()
    {
        if (_notificationsCheck is null ||
            _soundCheck is null ||
            _startupCheck is null ||
            _adBlockCheck is null ||
            _autoUpdateCheck is null ||
            _reminderInput is null)
            return;

        var oldAdBlock = _settings.AdBlockEnabled;

        _settings.NotificationsEnabled = _notificationsCheck.Checked;
        _settings.SoundEnabled = _soundCheck.Checked;
        _settings.StartWithWindows = _startupCheck.Checked;
        _settings.AdBlockEnabled = _adBlockCheck.Checked;
        _settings.AutoUpdateCheck = _autoUpdateCheck.Checked;
        _settings.ReminderMinutes = (int)_reminderInput.Value;
        _settings.Save();

        RefreshAdBlockButton();

        if (oldAdBlock != _settings.AdBlockEnabled)
            ReloadBrowserViewsForAdBlock();

        AppSettings.SetStartup(_settings.StartWithWindows);

        ShowDesktopNotification(
            "Ayarlar kaydedildi",
            "NiyaziSUGAR · Itemci ayarları güncellendi.",
            ToolTipIcon.Info
        );
    }

    private NotifyIcon BuildTrayIcon()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("NiyaziSUGAR · Itemci'yi Aç", null, (_, _) => RestoreFromTray());
        menu.Items.Add("Itemci / Mesajlar", null, (_, _) =>
        {
            RestoreFromTray();
            ShowPanel(_browserPanel);

            if (_webReady)
            {
                _ = OpenOrActivateBrowserTabAsync(
                    _messageUrl ??
                    "https://itemci.com/panel/mesajlar",
                    "Mesajlar"
                );
            }
        });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Çıkış", null, (_, _) =>
        {
            _allowExit = true;
            Close();
        });

        var tray = new NotifyIcon
        {
            Visible = true,
            Text = "NiyaziSUGAR · Itemci",
            Icon = Icon ?? SystemIcons.Application,
            ContextMenuStrip = menu
        };

        tray.DoubleClick += (_, _) => RestoreFromTray();
        tray.BalloonTipClicked += (_, _) =>
        {
            RestoreFromTray();
            ShowPanel(_browserPanel);

            if (_webReady && _messageUrl is not null)
                _webView.Source = new Uri(_messageUrl);
        };

        return tray;
    }

    private void LegacyOnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_allowExit) return;

        e.Cancel = true;
        HideToTray(showInfo: true);
    }

    private void LegacyHideToTray(bool showInfo)
    {
        Hide();
        ShowInTaskbar = false;

        if (showInfo)
        {
            _trayIcon.ShowBalloonTip(
                2500,
                "NiyaziSUGAR · Itemci",
                "Uygulama arka planda mesajları takip etmeye devam ediyor.",
                ToolTipIcon.Info
            );
        }
    }

    private void LegacyRestoreFromTray()
    {
        Show();
        ShowInTaskbar = true;
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private void ShowPanel(Panel panel)
    {
        foreach (Control c in _content.Controls)
            c.Visible = ReferenceEquals(c, panel);

        panel.BringToFront();
        SelectTopSection(panel);
    }

    private async Task InitializeWebViewAsync()
    {
        try
        {
            var userDataFolder = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData
                ),
                "NiyazisugarItemci",
                "WebView2"
            );

            Directory.CreateDirectory(
                userDataFolder
            );

            var env =
                await CoreWebView2Environment
                    .CreateAsync(
                        browserExecutableFolder:null,
                        userDataFolder:userDataFolder
                    );

            _browserEnvironment = env;

            await _webView
                .EnsureCoreWebView2Async(env);

            await _messageMonitorWebView
                .EnsureCoreWebView2Async(env);

            await _marketWebView
                .EnsureCoreWebView2Async(env);

            ConfigureBrowserWebView(
                _webView
            );

            _webView.Dock = DockStyle.Fill;
            _webView.BackColor = Bg;
            _webView.Visible = true;

            _browserHost.Controls.Add(
                _webView
            );

            var initialTab =
                new BrowserTabState
                {
                    Id =
                        ++_browserTabSequence,
                    WebView =
                        _webView,
                    Title =
                        "Itemci"
                };

            initialTab.TabPanel =
                CreateBrowserTabControl(
                    initialTab
                );

            _browserTabs.Add(
                initialTab
            );

            _browserTabStrip.Controls.Add(
                initialTab.TabPanel
            );

            SelectBrowserTab(
                initialTab
            );

            _messageMonitorWebView
                .CoreWebView2.Settings
                .AreDefaultContextMenusEnabled =
                false;

            _messageMonitorWebView
                .CoreWebView2.Settings
                .AreDevToolsEnabled =
                false;

            _messageMonitorWebView
                .CoreWebView2.Settings
                .IsStatusBarEnabled =
                false;

            _marketWebView
                .CoreWebView2.Settings
                .AreDefaultContextMenusEnabled =
                true;

            _marketWebView
                .CoreWebView2.Settings
                .AreDevToolsEnabled =
                false;

            _marketWebView
                .CoreWebView2.Settings
                .IsStatusBarEnabled =
                false;

            ConfigureAdBlocking(_marketWebView);

            _marketWebView.CoreWebView2
                .WebMessageReceived +=
                (_, e) =>
                {
                    HandleMarketWebMessage(e);
                };

            _marketWebView.CoreWebView2
                .NavigationCompleted +=
                async (_, e) =>
                {
                    if (!e.IsSuccess)
                        return;

                    _marketWebReady = true;
                    _marketAnalyzerInjected =
                        false;

                    await InjectMarketAnalyzerAsync();
                    await RefreshMarketServersAsync();
                    await InstallAdBlockDomAsync(_marketWebView);
                };

            _messageMonitorWebView
                .CoreWebView2
                .NavigationCompleted +=
                (_, e) =>
                {
                    if (!e.IsSuccess)
                        return;

                    _messageMonitorReady = true;

                    _loadedMonitorUrl =
                        _messageMonitorWebView
                            .Source
                            ?.ToString();

                    _ = MonitorItemciAsync();
                };

            _webView.Source =
                new Uri(
                    "https://itemci.com/"
                );

            _marketWebView.Source =
                new Uri(
                    "https://itemci.com/"
                );

            _webReady = true;

            _connectionLabel.Text =
                "AÇILIYOR";

            _connectionLabel.ForeColor =
                Accent;
        }
        catch (Exception ex)
        {
            _connectionLabel.Text =
                "HATA";

            _connectionLabel.ForeColor =
                Color.IndianRed;

            MessageBox.Show(
                "Itemci tarayıcısı başlatılamadı.\n\n" +
                "Microsoft Edge WebView2 Runtime gerekli olabilir.\n\n" +
                ex.Message,
                "NiyaziSUGAR · Itemci",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }
    }


    private async Task LegacyMonitorItemciAsync(WebView2 monitorView, bool sharedMonitor)
    {
        bool monitorReady=sharedMonitor||_messageMonitorReady;
        if (!_webReady || _webView.CoreWebView2 is null)
            return;

        try
        {
            // Üst menüden toplam okunmamış sayısını ve Mesajlar linkini bul.
            // Link bulunamazsa Itemci'nin panel mesajlar adresini yedek olarak kullan.
            var headerScript = """
            (() => {
                const norm = (v) => String(v || '')
                    .toLocaleLowerCase('tr-TR')
                    .normalize('NFD')
                    .replace(/[\u0300-\u036f]/g, '')
                    .replace(/ı/g, 'i')
                    .replace(/\s+/g, ' ')
                    .trim();

                const clean = (v) => String(v || '').replace(/\s+/g, ' ').trim();

                let count = 0;
                let messageUrl = null;

                const links = [...document.querySelectorAll('a[href]')];

                const scored = links
                    .map(a => {
                        const href = String(a.getAttribute('href') || '');
                        const text = norm(a.textContent);
                        const aria = norm(a.getAttribute('aria-label'));
                        const title = norm(a.getAttribute('title'));
                        const h = norm(href);

                        let score = 0;
                        if (h.includes('/panel/mesaj')) score += 30;
                        if (h.includes('mesaj')) score += 15;
                        if (h.includes('message')) score += 12;
                        if (h.includes('chat')) score += 10;
                        if (text === 'mesajlar' || text.includes('mesajlar')) score += 12;
                        if (aria.includes('mesaj')) score += 8;
                        if (title.includes('mesaj')) score += 8;

                        return { a, href, score };
                    })
                    .filter(x => x.score > 0)
                    .sort((a, b) => b.score - a.score);

                if (scored.length) {
                    try {
                        messageUrl = new URL(scored[0].href, location.href).href;
                    } catch {}

                    for (const item of scored.slice(0, 15)) {
                        const nodes = [item.a, ...item.a.querySelectorAll('span,div,strong,b')];

                        for (const node of nodes) {
                            const raw = clean(node.textContent);

                            if (/^\d{1,3}$/.test(raw)) {
                                const n = Number(raw);
                                if (Number.isFinite(n) && n > count && n <= 999)
                                    count = n;
                            }
                        }
                    }
                }

                const titleMatch = document.title.match(/^\((\d+)\)/);
                if (titleMatch)
                    count = Math.max(count, Number(titleMatch[1]) || 0);

                if (!messageUrl)
                    messageUrl = 'https://itemci.com/panel/mesajlar';

                return JSON.stringify({
                    count,
                    note: `Mesajlar izleniyor · ${count} toplam okunmamış`,
                    messageUrl,
                    conversations: [],
                    diagnostics: '',
                    pageUrl: location.href,
                    pageTitle: document.title
                });
            })();
            """;

            var headerRaw = await _webView.CoreWebView2.ExecuteScriptAsync(headerScript);
            var headerJson = JsonSerializer.Deserialize<string>(headerRaw);

            if (string.IsNullOrWhiteSpace(headerJson))
                return;

            var headerState = JsonSerializer.Deserialize<DomMonitorState>(headerJson);
            if (headerState is null)
                return;

            var detectedMessageUrl = NormalizeItemciUrl(headerState.MessageUrl)
                ?? "https://itemci.com/panel/mesajlar";

            _messageUrl = detectedMessageUrl;

            if (!sharedMonitor && monitorView.CoreWebView2 is not null &&
                !UrlsEquivalent(_loadedMonitorUrl, detectedMessageUrl))
            {
                monitorReady = false;
                _loadedMonitorUrl = detectedMessageUrl;
                monitorView.Source = new Uri(detectedMessageUrl);
            }

            DomMonitorState finalState = headerState;

            if (monitorReady &&
                monitorView.CoreWebView2 is not null)
            {
                var globalUnread = headerState.Count;

                // Mesajlar sayfasında "konuşma satırı" tahmini yapıyoruz.
                // Sayısal rozet şart değil: kullanıcı adı + son mesaj + zaman yapısından da satırı buluyor.
                var inboxScript = $$"""
                (() => {
                    const GLOBAL_UNREAD = {{globalUnread}};

                    const norm = (v) => String(v || '')
                        .toLocaleLowerCase('tr-TR')
                        .normalize('NFD')
                        .replace(/[\u0300-\u036f]/g, '')
                        .replace(/ı/g, 'i')
                        .replace(/\s+/g, ' ')
                        .trim();

                    const clean = (v) => String(v || '')
                        .replace(/\u00a0/g, ' ')
                        .replace(/[ \t]+/g, ' ')
                        .trim();

                    const visible = (el) => {
                        if (!el) return false;
                        const r = el.getBoundingClientRect();
                        const s = getComputedStyle(el);

                        return (
                            r.width > 0 &&
                            r.height > 0 &&
                            s.display !== 'none' &&
                            s.visibility !== 'hidden'
                        );
                    };

                    const absoluteUrl = (href) => {
                        try { return new URL(href, location.href).href; }
                        catch { return null; }
                    };

                    const isTimeLike = (text) => {
                        const t = norm(text);
                        return (
                            /^\d+\s*(dk|saat|gun|hafta|ay)\s*once$/.test(t) ||
                            /^(az once|simdi|dun)$/.test(t) ||
                            /^\d{1,2}:\d{2}$/.test(t) ||
                            /^\d{1,2}[./]\d{1,2}/.test(t) ||
                            /^\d{2}[./]\d{2}[./]\d{2,4}/.test(t) ||
                            /^\d{1,2}:\d{2}\s+\d{1,2}[./]\d{1,2}/.test(t)
                        );
                    };

                    const bannedExact = new Set([
                        'mesaj', 'mesajlar', 'bildirim', 'bildirimler',
                        'okunmamis', 'okunmamış', 'okundu', 'gonder', 'gönder',
                        'mesaj gonder', 'mesaj gönder', 'cevapla', 'ara',
                        'tum', 'tümü', 'yeni mesaj', 'itemci',
                        'hos geldin', 'hoş geldin', 'enuyguncom',
                        'google ile giris yap', 'google ile giriş yap',
                        'giris yap', 'giriş yap', 'kayit ol', 'kayıt ol',
                        'hesabina giris yap', 'hesabına giriş yap',
                        'e-posta ile giris yap', 'e-posta ile giriş yap',
                        'anasayfa', 'kullanici paneli', 'kullanıcı paneli',
                        'cekilisler', 'çekilişler', 'sepetim', 'siparislerim',
                        'siparişlerim', 'ilan islemleri', 'ilan işlemleri',
                        'reklam yonetimi', 'reklam yönetimi',
                        'hesap ve bakiye', 'ayarlar'
                    ].map(norm));

                    const pageText = norm(document.body?.innerText || document.body?.textContent || '');

                    const looksLoggedOut =
                        pageText.includes('google ile giris yap') ||
                        pageText.includes('google ile giriş yap') ||
                        (
                            (pageText.includes('giris yap') || pageText.includes('giriş yap')) &&
                            !pageText.includes('mesajlar')
                        );

                    if (looksLoggedOut) {
                        return JSON.stringify({
                            count: GLOBAL_UNREAD,
                            note: 'Mesaj takipçisi giriş ekranında kaldı · yeniden bağlanıyor',
                            messageUrl: location.href,
                            conversations: [],
                            diagnostics: JSON.stringify({
                                page: location.href,
                                title: document.title,
                                loggedOutDetected: true,
                                bodySample: clean(document.body?.innerText || '').slice(0, 1200)
                            }, null, 2),
                            pageUrl: location.href,
                            pageTitle: document.title
                        });
                    }

                    const isBadName = (text) => {
                        const t = norm(text);

                        if (!t) return true;
                        if (/^\d+$/.test(t)) return true;
                        if (t.length < 2 || t.length > 45) return true;
                        if (isTimeLike(t)) return true;
                        if (bannedExact.has(t)) return true;
                        if (t.startsWith('hos geldin') || t.startsWith('hoş geldin')) return true;
                        if (t.includes('google ile giris yap') || t.includes('google ile giriş yap')) return true;
                        if (t === 'giris yap' || t === 'giriş yap') return true;
                        if (t === 'kayit ol' || t === 'kayıt ol') return true;
                        if (t.includes('toplam satis')) return true;
                        if (t.includes('degerlendirme')) return true;

                        return false;
                    };

                    const linesOf = (el) => {
                        const raw = String(el.innerText || el.textContent || '');

                        return raw
                            .split(/\n+/)
                            .map(clean)
                            .filter(Boolean)
                            .filter((x, i, arr) => i === 0 || x !== arr[i - 1]);
                    };

                    const getSmallNumbers = (el) => {
                        const nums = [];

                        for (const n of el.querySelectorAll('span,div,b,strong')) {
                            const raw = clean(n.textContent);

                            if (/^\d{1,2}$/.test(raw)) {
                                const v = Number(raw);
                                if (v > 0 && v <= 99)
                                    nums.push(v);
                            }
                        }

                        return nums;
                    };

                    const getName = (el, lines) => {
                        // 1) Avatar alt bilgisi
                        for (const img of el.querySelectorAll('img[alt]')) {
                            const alt = clean(img.getAttribute('alt'));
                            const n = norm(alt);

                            if (!alt || isBadName(alt)) continue;
                            if (n.includes('avatar') || n.includes('profil') || n.includes('logo')) continue;

                            return alt.replace(/^@/, '');
                        }

                        // 2) Kullanıcı/name benzeri class'lar
                        const selectors = [
                            '[class*="username"]',
                            '[class*="user-name"]',
                            '[class*="user_name"]',
                            '[class*="nickname"]',
                            '[class*="sender"]',
                            '[class*="participant"]',
                            '[class*="customer"]',
                            '[class*="name"]',
                            'strong',
                            'b',
                            'h3',
                            'h4',
                            'h5'
                        ];

                        for (const sel of selectors) {
                            for (const node of el.querySelectorAll(sel)) {
                                const txt = clean(node.textContent);
                                if (!txt || isBadName(txt)) continue;
                                if (txt.length <= 45)
                                    return txt.replace(/^@/, '');
                            }
                        }

                        // 3) Satırların ilk mantıklı kısa metni
                        for (const line of lines) {
                            if (isBadName(line)) continue;
                            if (line.length <= 40)
                                return line.replace(/^@/, '');
                        }

                        return null;
                    };

                    const getPreview = (el, name, lines) => {
                        const nName = norm(name || '');
                        const candidates = [];

                        const nodes = [...el.querySelectorAll('p,span,div')]
                            .filter(visible);

                        for (const node of nodes) {
                            if (node.children.length > 3) continue;

                            const txt = clean(node.textContent);
                            const t = norm(txt);

                            if (!txt || txt.length < 2 || txt.length > 180) continue;
                            if (t === nName) continue;
                            if (isTimeLike(t)) continue;
                            if (/^\d+$/.test(t)) continue;
                            if (bannedExact.has(t)) continue;
                            if (t.startsWith('hos geldin') || t.startsWith('hoş geldin')) continue;
                            if (t.includes('google ile giris yap') || t.includes('google ile giriş yap')) continue;
                            if (t === 'giris yap' || t === 'giriş yap') continue;
                            if (t === 'kayit ol' || t === 'kayıt ol') continue;

                            let score = 0;
                            const cls = norm(node.className || '');

                            if (cls.includes('message')) score += 12;
                            if (cls.includes('preview')) score += 12;
                            if (cls.includes('last')) score += 7;
                            if (cls.includes('truncate')) score += 7;
                            if (cls.includes('line-clamp')) score += 7;
                            if (node.tagName === 'P') score += 4;
                            if (txt.length >= 5 && txt.length <= 110) score += 5;
                            if (txt.length > 130) score -= 4;

                            candidates.push({ txt, score });
                        }

                        for (const line of lines) {
                            const t = norm(line);

                            if (t === nName) continue;
                            if (isTimeLike(t)) continue;
                            if (/^\d+$/.test(t)) continue;
                            if (bannedExact.has(t)) continue;
                            if (line.length < 2 || line.length > 180) continue;

                            candidates.push({
                                txt: line,
                                score: line.length >= 5 && line.length <= 110 ? 3 : 0
                            });
                        }

                        candidates.sort((a, b) => b.score - a.score);

                        return candidates[0]?.txt || '';
                    };

                    const candidateElements = new Set();

                    // Önce link/role/button gibi konuşma satırı olabilecek elemanları topla.
                    for (const el of document.querySelectorAll('a[href], [role="button"], button')) {
                        if (!visible(el)) continue;

                        const r = el.getBoundingClientRect();
                        const txt = clean(el.innerText || el.textContent || '');

                        if (r.width < 180 || r.height < 34 || r.height > 240) continue;
                        if (txt.length < 3 || txt.length > 900) continue;

                        candidateElements.add(el);
                    }

                    // Küçük unread rozetlerinden yukarı çıkarak satır adayları ekle.
                    for (const badge of document.querySelectorAll('span,div,b,strong')) {
                        const raw = clean(badge.textContent);
                        if (!/^\d{1,2}$/.test(raw)) continue;

                        let cur = badge.parentElement;

                        for (let i = 0; i < 6 && cur && cur !== document.body; i++, cur = cur.parentElement) {
                            const r = cur.getBoundingClientRect();
                            const txt = clean(cur.innerText || cur.textContent || '');

                            if (
                                r.width >= 180 &&
                                r.height >= 34 &&
                                r.height <= 240 &&
                                txt.length >= 3 &&
                                txt.length <= 900
                            ) {
                                candidateElements.add(cur);
                                break;
                            }
                        }
                    }

                    const candidates = [];

                    for (const el of candidateElements) {
                        const r = el.getBoundingClientRect();
                        const lines = linesOf(el);
                        const text = clean(el.innerText || el.textContent || '');
                        const nt = norm(text);

                        if (nt.includes('hos geldin') || nt.includes('hoş geldin')) continue;
                        if (nt.includes('kullanici paneli') || nt.includes('kullanıcı paneli')) continue;
                        if (nt.includes('ilan islemleri') || nt.includes('ilan işlemleri')) continue;

                        const hrefNode =
                            el.matches?.('a[href]') ? el :
                            el.querySelector?.('a[href]');

                        const href = hrefNode?.getAttribute?.('href') || '';
                        const url = absoluteUrl(href);
                        const h = norm(href);
                        const cls = norm(el.className || '');
                        const numbers = getSmallNumbers(el);

                        const name = getName(el, lines);
                        const preview = getPreview(el, name, lines);

                        let score = 0;

                        if (h.includes('mesaj')) score += 12;
                        if (h.includes('message')) score += 10;
                        if (h.includes('chat')) score += 10;
                        if (h.includes('conversation')) score += 10;
                        if (h.includes('thread')) score += 10;

                        if (el.querySelector('img')) score += 4;
                        if (lines.length >= 2 && lines.length <= 8) score += 5;
                        if (lines.some(isTimeLike)) score += 5;
                        if (numbers.length) score += 5;
                        if (cls.includes('unread')) score += 8;
                        if (cls.includes('font-bold') || cls.includes('font-semibold')) score += 2;
                        if (name && !isBadName(name)) score += 10;
                        if (preview) score += 6;

                        // Header/nav alanlarını puan kır.
                        if (r.top < 170) score -= 12;
                        if (r.height > 180) score -= 4;
                        if (r.width > window.innerWidth * 0.92) score -= 5;

                        if (!name || isBadName(name)) continue;
                        if (score < 8) continue;

                        let unread = numbers.length ? Math.max(...numbers) : 0;

                        const unreadish =
                            cls.includes('unread') ||
                            cls.includes('font-bold') ||
                            cls.includes('font-semibold') ||
                            nt.includes('okunmamis') ||
                            nt.includes('okunmamış');

                        candidates.push({
                            name,
                            preview,
                            unread,
                            unreadish,
                            score,
                            url,
                            top: Math.round(r.top),
                            height: Math.round(r.height),
                            lines: lines.slice(0, 8),
                            cls: String(el.className || '').slice(0, 180),
                            href: href.slice(0, 220)
                        });
                    }

                    // Aynı konuşma DOM'da birkaç kez yakalanabilir.
                    candidates.sort((a, b) => b.score - a.score);

                    const deduped = [];
                    const seen = new Set();

                    for (const c of candidates) {
                        const key = c.url
                            ? `url:${c.url}`
                            : `name:${norm(c.name)}`;

                        if (seen.has(key)) continue;
                        seen.add(key);
                        deduped.push(c);
                    }

                    // Unread olduğu belli olan satırları seç.
                    let unreadRows = deduped.filter(c => c.unread > 0 || c.unreadish);

                    // Itemci globalde 5 okunmamış diyor ama satırda sayı yoksa:
                    // tek bir okunmamış satır varsa 5'in tamamını ona ver.
                    if (GLOBAL_UNREAD > 0) {
                        if (unreadRows.length === 1 && unreadRows[0].unread <= 1) {
                            unreadRows[0].unread = GLOBAL_UNREAD;
                        }
                        else if (unreadRows.length === 0 && deduped.length > 0) {
                            // En yüksek puanlı ilk satırı yedek olarak kullan.
                            // Bu sadece kişi adı/önizleme kaybolmasın diye.
                            const first = deduped[0];
                            first.unread = GLOBAL_UNREAD;
                            unreadRows = [first];
                        }
                        else {
                            const known = unreadRows.reduce((s, x) => s + Math.max(0, x.unread), 0);

                            if (known < GLOBAL_UNREAD && unreadRows.length === 1)
                                unreadRows[0].unread = GLOBAL_UNREAD;
                        }
                    }

                    const conversations = unreadRows
                        .filter(x => x.unread > 0)
                        .map(x => ({
                            name: x.name,
                            unread: x.unread,
                            url: x.url,
                            preview: x.preview
                        }));

                    const counted = conversations.reduce((sum, x) => sum + x.unread, 0);
                    const total = GLOBAL_UNREAD > 0 ? GLOBAL_UNREAD : counted;

                    const diagnostics = JSON.stringify({
                        page: location.href,
                        title: document.title,
                        globalUnread: GLOBAL_UNREAD,
                        selected: conversations,
                        topCandidates: deduped.slice(0, 12)
                    }, null, 2);

                    return JSON.stringify({
                        count: total,
                        note: conversations.length
                            ? `${conversations.length} kişi / ${total} okunmamış · kişi adı bulundu`
                            : `Kişi adı bulunamadı · ${total} toplam okunmamış`,
                        messageUrl: location.href,
                        conversations,
                        diagnostics,
                        pageUrl: location.href,
                        pageTitle: document.title
                    });
                })();
                """;

                var inboxRaw = await monitorView.CoreWebView2.ExecuteScriptAsync(inboxScript);
                var inboxJson = JsonSerializer.Deserialize<string>(inboxRaw);

                if (!string.IsNullOrWhiteSpace(inboxJson))
                {
                    var inboxState = JsonSerializer.Deserialize<DomMonitorState>(inboxJson);

                    if (inboxState is not null)
                    {
                        if (!string.IsNullOrWhiteSpace(inboxState.Diagnostics))
                            _lastDiagnosticsJson = inboxState.Diagnostics!;

                        if (!sharedMonitor && (inboxState.Note ?? "").Contains("giriş ekranında", StringComparison.OrdinalIgnoreCase) &&
                            monitorView.CoreWebView2 is not null &&
                            DateTime.Now - _lastMonitorReloadAt > TimeSpan.FromSeconds(4))
                        {
                            _lastMonitorReloadAt = DateTime.Now;
                            monitorReady = false;
                            _loadedMonitorUrl = detectedMessageUrl;
                            monitorView.Source = new Uri(detectedMessageUrl);
                        }

                        // Kişi bazlı veri bulunduysa onu kullan.
                        // Bulunmadıysa global toplam sayaç çalışmaya devam etsin.
                        if (inboxState.Conversations is { Count: > 0 })
                            finalState = inboxState;
                        else
                            headerState.Note = inboxState.Note;
                    }
                }
            }

            _lastDetectorNote = finalState.Note ?? "Kontrol edildi";

            var finalMessageUrl = NormalizeItemciUrl(finalState.MessageUrl);
            if (!string.IsNullOrWhiteSpace(finalMessageUrl))
                _messageUrl = finalMessageUrl;

            UpdateDashboard(finalState);
            HandleUnreadState(finalState);
        }
        catch (Exception ex)
        {
            _lastDiagnosticsJson =
                "MONITOR HATASI:\r\n" + ex.Message + "\r\n\r\n" + ex.StackTrace;
        }
    }

    private static bool UrlsEquivalent(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
            return false;

        try
        {
            var ua = new Uri(a);
            var ub = new Uri(b);

            return string.Equals(
                ua.GetLeftPart(UriPartial.Path).TrimEnd('/'),
                ub.GetLeftPart(UriPartial.Path).TrimEnd('/'),
                StringComparison.OrdinalIgnoreCase
            );
        }
        catch
        {
            return string.Equals(
                a.TrimEnd('/'),
                b.TrimEnd('/'),
                StringComparison.OrdinalIgnoreCase
            );
        }
    }

    private void UpdateDashboard(DomMonitorState state)
    {
        _connectionLabel.Text = "BAĞLI";
        _connectionLabel.ForeColor = Green;

        if (state.Conversations is { Count: > 0 })
        {
            var people = state.Conversations.Count(x => x.Unread > 0);
            _unreadLabel.Text = $"{people} kişi / {state.Count} mesaj";
            _unreadLabel.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
        }
        else
        {
            _unreadLabel.Text = state.Count.ToString();
            _unreadLabel.Font = new Font("Segoe UI", 20F, FontStyle.Bold);
        }

        _unreadLabel.ForeColor = state.Count > 0 ? Accent : TextMain;

        _detectorLabel.Text = _lastDetectorNote;
        _lastCheckLabel.Text = DateTime.Now.ToString("HH:mm:ss");
    }

    private void HandleUnreadState(DomMonitorState state)
    {
        var currentCount = state.Count;
        var conversations = state.Conversations ?? new List<ConversationState>();

        // İlk okumada kişi bazlı veriyi belleğe al.
        if (_lastUnreadCount < 0)
        {
            _lastUnreadCount = currentCount;
            _lastConversationCounts.Clear();

            foreach (var c in conversations)
            {
                var key = ConversationKey(c);
                _lastConversationCounts[key] = c.Unread;
            }

            if (currentCount > 0)
            {
                _firstUnreadAt = DateTime.Now;

                if (conversations.Count > 0)
                {
                    foreach (var c in conversations.Where(x => x.Unread > 0))
                    {
                        ShowConversationNotification(c, isInitial: true);
                    }
                }
                else
                {
                    ShowDesktopNotification(
                        "Okunmamış Itemci mesajı",
                        $"{currentCount} okunmamış mesaj görünüyor.",
                        ToolTipIcon.Info
                    );
                }
            }

            return;
        }

        var anyConversationNotification = false;

        if (conversations.Count > 0)
        {
            foreach (var c in conversations)
            {
                if (c.Unread <= 0) continue;

                var key = ConversationKey(c);
                _lastConversationCounts.TryGetValue(key, out var previous);

                if (c.Unread > previous)
                {
                    var delta = c.Unread - previous;
                    anyConversationNotification = true;

                    var text = delta == 1
                        ? $"{c.Name} yeni bir mesaj yolladı."
                        : $"{c.Name} {delta} yeni mesaj yolladı.";

                    var preview = ShortPreview(c.Preview);
                    if (!string.IsNullOrWhiteSpace(preview))
                        text += $"\n“{preview}”";

                    ShowDesktopNotification(
                        "Yeni Itemci mesajı",
                        text,
                        ToolTipIcon.Info,
                        c.Url
                    );
                }
            }

            // Artık görünmeyen konuşmaları sıfır kabul etmek için sözlüğü yenile.
            _lastConversationCounts.Clear();

            foreach (var c in conversations)
            {
                var key = ConversationKey(c);
                _lastConversationCounts[key] = c.Unread;
            }
        }

        // Kişi bazlı veri yoksa eski toplam sayaç davranışına geri dön.
        if (!anyConversationNotification &&
            conversations.Count == 0 &&
            currentCount > _lastUnreadCount)
        {
            var delta = currentCount - _lastUnreadCount;

            ShowDesktopNotification(
                "Yeni Itemci mesajı",
                delta == 1
                    ? "Yeni bir müşteri mesajı geldi."
                    : $"{delta} yeni müşteri mesajı geldi.",
                ToolTipIcon.Info
            );
        }

        if (currentCount <= 0)
        {
            _firstUnreadAt = null;
            _lastReminderAt = null;
            _lastConversationCounts.Clear();
        }
        else
        {
            _firstUnreadAt ??= DateTime.Now;

            var reminderAfter = TimeSpan.FromMinutes(Math.Max(1, _settings.ReminderMinutes));
            var now = DateTime.Now;

            if (now - _firstUnreadAt.Value >= reminderAfter &&
                (_lastReminderAt is null || now - _lastReminderAt.Value >= reminderAfter))
            {
                _lastReminderAt = now;

                if (conversations.Count > 0)
                {
                    var top = conversations
                        .Where(x => x.Unread > 0)
                        .OrderByDescending(x => x.Unread)
                        .Take(3)
                        .ToList();

                    var parts = top
                        .Select(x => $"{x.Name}: {x.Unread}")
                        .ToList();

                    var suffix = conversations.Count > 3
                        ? $" +{conversations.Count - 3} kişi"
                        : "";

                    ShowDesktopNotification(
                        "Cevap bekleyen müşteriler",
                        string.Join(" · ", parts) + suffix,
                        ToolTipIcon.Warning
                    );
                }
                else
                {
                    ShowDesktopNotification(
                        "Cevap bekleyen müşteri",
                        $"{currentCount} okunmamış Itemci mesajı hâlâ cevap bekliyor.",
                        ToolTipIcon.Warning
                    );
                }
            }
        }

        _lastUnreadCount = currentCount;
    }

    private string ConversationKey(ConversationState c)
    {
        if (!string.IsNullOrWhiteSpace(c.Url))
            return "url:" + c.Url;

        return "name:" + (c.Name ?? "Bilinmeyen kullanıcı").Trim().ToLowerInvariant();
    }

    private void ShowConversationNotification(ConversationState c, bool isInitial)
    {
        var text = c.Unread == 1
            ? $"{c.Name} 1 okunmamış mesaj."
            : $"{c.Name} {c.Unread} okunmamış mesaj.";

        var preview = ShortPreview(c.Preview);
        if (!string.IsNullOrWhiteSpace(preview))
            text += $"\n“{preview}”";

        ShowDesktopNotification(
            isInitial ? "Okunmamış Itemci mesajı" : "Yeni Itemci mesajı",
            text,
            ToolTipIcon.Info,
            c.Url
        );
    }

    private static string ShortPreview(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var clean = string.Join(
            " ",
            value.Split(
                new[] { '\r', '\n', '\t' },
                StringSplitOptions.RemoveEmptyEntries
            )
        );

        clean = string.Join(" ", clean.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Trim();

        if (clean.Length <= 105)
            return clean;

        return clean[..102] + "...";
    }

    private (string title, string message, bool warning, Action click)? _pendingToast;
    private System.Windows.Forms.Timer? _pendingToastTimer;

    private void ShowDesktopNotification(string title, string message, ToolTipIcon icon, string? targetUrl = null, Action? onClick = null)
    {
        if (!_settings.NotificationsEnabled)
            return;

        if (_settings.SoundEnabled)
        {
            try
            {
                if (icon == ToolTipIcon.Warning)
                    SystemSounds.Exclamation.Play();
                else
                    SystemSounds.Asterisk.Play();
            }
            catch { }
        }

        Action click = onClick ?? (() =>
        {
            RestoreFromTray();
            ShowPanel(_browserPanel);

            var target = NormalizeItemciUrl(targetUrl) ?? _messageUrl;

            if (_webReady && target is not null)
                _webView.Source = new Uri(target);
        });

        try
        {
            BeginInvoke(() =>
            {
                // Tam ekran oyun/sunum sırasında pencere açılmaz; bitince son bildirim gösterilir.
                if (!ToastForm.UserAcceptsPopups())
                {
                    _pendingToast = (title, message, icon == ToolTipIcon.Warning, click);
                    if (_pendingToastTimer is null)
                    {
                        _pendingToastTimer = new System.Windows.Forms.Timer { Interval = 3000 };
                        _pendingToastTimer.Tick += (_, _) =>
                        {
                            if (_pendingToast is null || !ToastForm.UserAcceptsPopups()) return;
                            var pending = _pendingToast.Value;
                            _pendingToast = null;
                            _pendingToastTimer.Stop();
                            ToastForm.ShowToast(pending.title, pending.message, pending.warning, pending.click);
                        };
                        FormClosed += (_, _) => _pendingToastTimer?.Dispose();
                    }
                    _pendingToastTimer.Start();
                    return;
                }

                ToastForm.ShowToast(title, message, icon == ToolTipIcon.Warning, click);
            });
        }
        catch
        {
            // Yedek olarak eski Windows tray balonunu dene.
            try
            {
                _trayIcon.BalloonTipTitle = title;
                _trayIcon.BalloonTipText = message;
                _trayIcon.BalloonTipIcon = icon;
                _trayIcon.ShowBalloonTip(4500);
            }
            catch { }
        }
    }

    private string? NormalizeItemciUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (Uri.TryCreate(value, UriKind.Absolute, out var absolute))
            return absolute.ToString();

        if (Uri.TryCreate(new Uri("https://itemci.com/"), value, out var relative))
            return relative.ToString();

        return null;
    }

    private async Task LegacyRefreshSalesAsync(
        bool showErrors,
        bool fullHistory = false
    )
    {
        if (!_webReady || _webView.CoreWebView2 is null)
        {
            _salesStatusLabel.Text = "Itemci oturumu henüz hazır değil.";
            return;
        }

        try
        {
            _salesStatusLabel.ForeColor = TextMuted;

            if (_salesHistory.Count > 0)
                RenderSoldSalesPanel();

            var maxPages = fullHistory ? 150 : 8;
            var deliveredIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var pageFingerprints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var scannedPages = 0;
            var emptyPages = 0;

            for (var page = 1; page <= maxPages; page++)
            {
                _salesStatusLabel.Text = fullHistory
                    ? $"Tüm satış geçmişi taranıyor... Sayfa {page}"
                    : $"Yeni satışlar kontrol ediliyor... Sayfa {page}";

                Application.DoEvents();

                var url = page == 1
                    ? "https://itemci.com/panel/bildirimler"
                    : $"https://itemci.com/panel/bildirimler?page={page}";

                var html = await FetchItemciHtmlAsync(url);

                if (string.IsNullOrWhiteSpace(html))
                    break;

                var parsed = ParseNotificationPage(html);

                if (string.IsNullOrWhiteSpace(parsed.Fingerprint))
                {
                    emptyPages++;
                    if (emptyPages >= 1)
                        break;
                    continue;
                }

                if (!pageFingerprints.Add(parsed.Fingerprint))
                    break;

                scannedPages++;

                if (parsed.NotificationTimestampCount == 0)
                {
                    emptyPages++;
                    if (emptyPages >= 1)
                        break;
                }
                else
                {
                    emptyPages = 0;
                }

                foreach (var delivered in parsed.DeliveredOrderIds)
                    deliveredIds.Add(delivered);

                foreach (var sale in parsed.Sales)
                    MergeSaleIntoHistory(sale);

                // Yakalanan teslim bildirimlerini var olan satışlara işle.
                foreach (var id in deliveredIds)
                {
                    if (_salesHistory.TryGetValue(id, out var existing))
                    {
                        existing.IsDelivered = true;
                    }
                }

                // Son sayfalarda veri yoksa pagination bitmiştir.
                if (!fullHistory && page >= maxPages)
                    break;
            }

            // Tam geçmiş tarandıysa geçmiş satışların teslim durumunu da,
            // tüm tarama boyunca bulunan onay bildirimlerine göre güncelle.
            foreach (var id in deliveredIds)
            {
                if (_salesHistory.TryGetValue(id, out var existing))
                    existing.IsDelivered = true;
            }

            if (fullHistory && scannedPages > 0)
                _historyFullyScanned = true;

            SaveSalesHistory();
            RenderSoldSalesPanel();

            var totalHistory = _salesHistory.Count;
            var todayCount = _salesHistory.Values.Count(x => x.Timestamp.Date == DateTime.Today);

            _salesStatusLabel.Text =
                fullHistory
                    ? $"Geçmiş taraması tamamlandı · {scannedPages} sayfa · Toplam {totalHistory} satış · Bugün {todayCount} satış."
                    : $"Güncel kontrol tamamlandı · {scannedPages} sayfa · Kayıtlı toplam {totalHistory} satış.";

            _salesStatusLabel.ForeColor = Green;

            HandleNewSalesNotifications(
                _salesHistory.Values
                    .Where(x => x.Timestamp.Date == DateTime.Today)
                    .OrderByDescending(x => x.Timestamp)
                    .ToList()
            );

            _salesInitialized = true;
        }
        catch (Exception ex)
        {
            _salesStatusLabel.Text =
                "Satışlar okunamadı: " + ex.Message;
            _salesStatusLabel.ForeColor = Color.IndianRed;

            if (showErrors)
            {
                MessageBox.Show(
                    "Itemci satış bildirimleri okunamadı.\n\n" +
                    ex.Message +
                    "\n\nItemci / Mesajlar bölümünde hesabına giriş yaptığından emin ol.",
                    "NiyaziSUGAR · Itemci",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
            }
        }
    }

    private void RenderSalesPanel()
    {
        var allSales = _salesHistory.Values
            .OrderByDescending(x => x.Timestamp)
            .ThenByDescending(x => x.OrderId)
            .ToList();

        var todaySales = allSales
            .Where(x => x.Timestamp.Date == DateTime.Today)
            .ToList();

        var gross = todaySales.Sum(x => x.GrossAmount);
        var commission = gross * 0.10m;
        var net = gross - commission;

        _salesGrossLabel.Text = FormatTl(gross);
        _salesCommissionLabel.Text = FormatTl(commission);
        _salesNetLabel.Text = FormatTl(net);
        _salesCountLabel.Text = todaySales.Count.ToString();

        _salesGrid.Rows.Clear();

        foreach (var sale in allSales)
        {
            var statusText = sale.IsDelivered
                ? "TESLİM EDİLDİ"
                : "BEKLİYOR";

            var rowIndex = _salesGrid.Rows.Add(
                sale.Timestamp.ToString("dd.MM.yyyy"),
                sale.Timestamp.ToString("HH:mm"),
                sale.ItemTitle,
                statusText,
                FormatTl(sale.GrossAmount),
                FormatTl(sale.CommissionAmount),
                FormatTl(sale.NetAmount),
                sale.OrderId
            );

            var row = _salesGrid.Rows[rowIndex];
            row.Height = 34;

            row.Cells["Gross"].Style.ForeColor = Accent;
            row.Cells["Commission"].Style.ForeColor = Color.FromArgb(255, 125, 125);
            row.Cells["Net"].Style.ForeColor = Green;

            if (sale.IsDelivered)
            {
                row.Cells["Status"].Style.ForeColor = Green;
                row.Cells["Status"].Style.Font = new Font(
                    "Segoe UI",
                    8.5F,
                    FontStyle.Bold
                );
            }
            else
            {
                row.Cells["Status"].Style.ForeColor = Accent;
                row.Cells["Status"].Style.Font = new Font(
                    "Segoe UI",
                    8.5F,
                    FontStyle.Bold
                );
            }
        }
    }

    private void MergeSaleIntoHistory(SaleRecord sale)
    {
        var key = SaleKey(sale);

        if (_salesHistory.TryGetValue(key, out var existing))
        {
            // İlk satış bilgisini koru ama yeni taramada teslim edilmiş görünüyorsa güncelle.
            existing.IsDelivered |= sale.IsDelivered;

            if (string.IsNullOrWhiteSpace(existing.ItemTitle) &&
                !string.IsNullOrWhiteSpace(sale.ItemTitle))
            {
                existing.ItemTitle = sale.ItemTitle;
            }

            if (existing.GrossAmount <= 0 && sale.GrossAmount > 0)
                existing.GrossAmount = sale.GrossAmount;

            if (string.IsNullOrWhiteSpace(existing.OrderId))
                existing.OrderId = sale.OrderId;

            return;
        }

        _salesHistory[key] = sale;
    }

    private static string SaleKey(SaleRecord sale)
    {
        if (!string.IsNullOrWhiteSpace(sale.OrderId))
            return sale.OrderId.Trim();

        return $"{sale.Timestamp:yyyyMMddHHmm}|{sale.GrossAmount}|{sale.ItemTitle}";
    }

    private void HandleNewSalesNotifications(List<SaleRecord> todaySales)
    {
        var currentIds = todaySales
            .Select(x => x.OrderId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!_salesInitialized)
        {
            foreach (var id in currentIds)
                _knownSaleOrderIds.Add(id);

            return;
        }

        var newSales = todaySales
            .Where(x =>
                !string.IsNullOrWhiteSpace(x.OrderId) &&
                !_knownSaleOrderIds.Contains(x.OrderId))
            .OrderBy(x => x.Timestamp)
            .ToList();

        foreach (var sale in newSales)
        {
            _knownSaleOrderIds.Add(sale.OrderId);

            ShowDesktopNotification(
                "Yeni Itemci satışı",
                $"{sale.ItemTitle}\n" +
                $"Satış: {FormatTl(sale.GrossAmount)} · " +
                $"Komisyon: {FormatTl(sale.CommissionAmount)} · " +
                $"Net: {FormatTl(sale.NetAmount)}\n" +
                $"Durum: {(sale.IsDelivered ? "Teslim Edildi" : "Bekliyor")}",
                ToolTipIcon.Info,
                "https://itemci.com/panel/bildirimler"
            );
        }
    }

    private async Task<string> FetchItemciHtmlAsync(string url)
    {
        if (_webView.CoreWebView2 is null)
            throw new InvalidOperationException("Itemci tarayıcısı hazır değil.");

        // Önce doğrudan WebView oturumunun içinde fetch yap.
        // Böylece Itemci giriş oturumu %100 aynı tarayıcı profilinden kullanılır.
        try
        {
            var jsUrl = JsonSerializer.Serialize(url);

            var raw = await _webView.CoreWebView2.ExecuteScriptAsync(
                $"fetch({jsUrl}, {{ credentials: 'include', cache: 'no-store' }})" +
                ".then(async r => JSON.stringify({ status:r.status, url:r.url, text:await r.text() }))"
            );

            var outer = JsonSerializer.Deserialize<string>(raw);

            if (!string.IsNullOrWhiteSpace(outer))
            {
                var result = JsonSerializer.Deserialize<BrowserFetchResult>(outer);

                if (result is not null &&
                    result.Status >= 200 &&
                    result.Status < 400 &&
                    !string.IsNullOrWhiteSpace(result.Text))
                {
                    if (!result.Url.Contains("/user/login", StringComparison.OrdinalIgnoreCase) &&
                        !result.Text.Contains("Google ile Giriş Yap", StringComparison.OrdinalIgnoreCase))
                    {
                        return result.Text;
                    }
                }
            }
        }
        catch
        {
            // Aşağıdaki cookie tabanlı yedek yolu dene.
        }

        var cookies = await _webView.CoreWebView2.CookieManager.GetCookiesAsync("https://itemci.com/");

        using var handler = new HttpClientHandler
        {
            AutomaticDecompression =
                DecompressionMethods.GZip |
                DecompressionMethods.Deflate |
                DecompressionMethods.Brotli,
            UseCookies = false,
            AllowAutoRedirect = true
        };

        using var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(20)
        };

        var cookieHeader = string.Join(
            "; ",
            cookies.Select(c => $"{c.Name}={c.Value}")
        );

        if (!string.IsNullOrWhiteSpace(cookieHeader))
            client.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", cookieHeader);

        client.DefaultRequestHeaders.TryAddWithoutValidation(
            "User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/154 Safari/537.36"
        );

        client.DefaultRequestHeaders.TryAddWithoutValidation(
            "Accept",
            "text/html,application/xhtml+xml"
        );

        var response = await client.GetAsync(url);
        var html = await response.Content.ReadAsStringAsync();

        if (response.RequestMessage?.RequestUri?.AbsolutePath.Contains(
                "/user/login",
                StringComparison.OrdinalIgnoreCase
            ) == true ||
            html.Contains("Google ile Giriş Yap", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Itemci oturumu satış taramasına aktarılamadı. Itemci / Mesajlar bölümünde hesabına giriş yapıp tekrar dene."
            );
        }

        return html;
    }

    private NotificationPageResult ParseNotificationPage(string html)
    {
        var result = new NotificationPageResult();

        var doc = new HtmlAgilityPack.HtmlDocument();
        doc.LoadHtml(html);

        var allText = NormalizeHtmlText(doc.DocumentNode.InnerText);

        var timestamps = Regex.Matches(
            allText,
            @"\d{2}\.\d{2}\.\d{4}\s+\d{2}:\d{2}"
        );

        result.NotificationTimestampCount = timestamps.Count;

        var fingerprintSource =
            string.Join(
                "|",
                timestamps
                    .Cast<Match>()
                    .Take(10)
                    .Select(x => x.Value)
            ) +
            "|" +
            allText[..Math.Min(allText.Length, 1200)];

        if (!string.IsNullOrWhiteSpace(fingerprintSource))
        {
            result.Fingerprint = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(fingerprintSource)
                )
            );
        }

        // Satış bildirimleri
        var nodes = doc.DocumentNode
            .Descendants()
            .Where(n =>
                n.NodeType == HtmlNodeType.Element &&
                HtmlEntity.DeEntitize(n.InnerText)
                    .Contains("Ürününüz satıldı", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var seenSales = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in nodes)
        {
            var container = node;

            while (container.ParentNode is not null)
            {
                var parentText = NormalizeHtmlText(container.ParentNode.InnerText);

                if (parentText.Length > 900)
                    break;

                if (parentText.Contains("Ürününüz satıldı", StringComparison.OrdinalIgnoreCase))
                    container = container.ParentNode;
                else
                    break;
            }

            var text = NormalizeHtmlText(container.InnerText);
            var sale = ParseSaleText(text);

            if (sale is null)
                continue;

            var key = SaleKey(sale);

            if (seenSales.Add(key))
                result.Sales.Add(sale);
        }

        // DOM seçimi hiçbir şey bulamazsa tüm sayfa metnini regex ile yedek tara.
        if (result.Sales.Count == 0)
        {
            var rx = new Regex(
                @"Ürününüz\s+satıldı\s*\((?<inside>[^)]{1,350})\)\s*(?<date>\d{2}\.\d{2}\.\d{4}\s+\d{2}:\d{2})",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
            );

            foreach (Match m in rx.Matches(allText))
            {
                var sale = ParseSaleText(
                    $"Ürününüz satıldı ({m.Groups["inside"].Value}) {m.Groups["date"].Value}"
                );

                if (sale is null)
                    continue;

                var key = SaleKey(sale);

                if (seenSales.Add(key))
                    result.Sales.Add(sale);
            }
        }

        // Teslim / ödeme onayı bildirimleri.
        // Kullanıcının ekranında görülen örnek:
        // "Alıcı siparişi onayladı, ödeme hesabınıza aktarıldı (94150057625)"
        var deliveryRegexes = new[]
        {
            new Regex(
                @"Alıcı\s+siparişi\s+onayladı[^()]{0,180}\((?<order>\d{7,})\)",
                RegexOptions.IgnoreCase
            ),
            new Regex(
                @"ödeme\s+hesabınıza\s+aktarıldı[^()]{0,180}\((?<order>\d{7,})\)",
                RegexOptions.IgnoreCase
            ),
            new Regex(
                @"(?:sipariş|ürün)[^()]{0,120}teslim\s+edildi[^()]{0,120}\((?<order>\d{7,})\)",
                RegexOptions.IgnoreCase
            ),
            new Regex(
                @"sipariş[^()]{0,120}tamamlandı[^()]{0,120}\((?<order>\d{7,})\)",
                RegexOptions.IgnoreCase
            )
        };

        foreach (var rx in deliveryRegexes)
        {
            foreach (Match m in rx.Matches(allText))
            {
                var id = m.Groups["order"].Value.Trim();

                if (!string.IsNullOrWhiteSpace(id))
                    result.DeliveredOrderIds.Add(id);
            }
        }

        // Bu sayfada hem satış hem teslim bildirimi varsa satışların durumunu anında işle.
        foreach (var sale in result.Sales)
        {
            if (!string.IsNullOrWhiteSpace(sale.OrderId) &&
                result.DeliveredOrderIds.Contains(sale.OrderId))
            {
                sale.IsDelivered = true;
            }
        }

        return result;
    }

    private SaleRecord? ParseSaleText(string text)
    {
        text = NormalizeHtmlText(text);

        var soldIndex = text.IndexOf("Ürününüz satıldı", StringComparison.OrdinalIgnoreCase);
        if (soldIndex < 0)
            return null;

        var dateMatch = Regex.Match(
            text,
            @"(?<date>\d{2}\.\d{2}\.\d{4})\s+(?<time>\d{2}:\d{2})",
            RegexOptions.IgnoreCase
        );

        if (!dateMatch.Success)
            return null;

        if (!DateTime.TryParseExact(
            $"{dateMatch.Groups["date"].Value} {dateMatch.Groups["time"].Value}",
            "dd.MM.yyyy HH:mm",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None,
            out var timestamp))
        {
            return null;
        }

        var insideMatch = Regex.Match(
            text,
            @"Ürününüz\s+satıldı\s*\((?<inside>[^)]{1,350})\)",
            RegexOptions.IgnoreCase
        );

        if (!insideMatch.Success)
            return null;

        var inside = NormalizeHtmlText(insideMatch.Groups["inside"].Value);

        var orderMatch = Regex.Match(
            inside,
            @"-\s*(?<order>\d{7,})\s*$",
            RegexOptions.IgnoreCase
        );

        var orderId = orderMatch.Success
            ? orderMatch.Groups["order"].Value
            : "";

        var withoutOrder = orderMatch.Success
            ? inside[..orderMatch.Index].Trim()
            : inside;

        // Fiyatı sondaki TL değerinden al: 100TL, 1.250 TL, 1.250,50 TL...
        var priceMatches = Regex.Matches(
            withoutOrder,
            @"(?<price>\d{1,3}(?:\.\d{3})*(?:,\d{1,2})?|\d+(?:,\d{1,2})?)\s*TL\b",
            RegexOptions.IgnoreCase
        );

        if (priceMatches.Count == 0)
            return null;

        var priceMatch = priceMatches[^1];
        var gross = ParseTurkishMoney(priceMatch.Groups["price"].Value);

        if (gross <= 0)
            return null;

        var itemTitle = withoutOrder.Remove(
            priceMatch.Index,
            priceMatch.Length
        ).Trim(' ', '-', '–', '—', '|');

        if (string.IsNullOrWhiteSpace(itemTitle))
            itemTitle = "Itemci satışı";

        return new SaleRecord
        {
            Timestamp = timestamp,
            ItemTitle = itemTitle,
            GrossAmount = gross,
            OrderId = orderId
        };
    }

    private static decimal ParseTurkishMoney(string value)
    {
        var s = value.Trim().Replace(" ", "");

        if (s.Contains('.') && s.Contains(','))
            s = s.Replace(".", "").Replace(',', '.');
        else if (s.Contains(','))
            s = s.Replace(',', '.');
        else if (s.Count(c => c == '.') > 1)
            s = s.Replace(".", "");
        else if (s.Contains('.') &&
                 s.Length - s.LastIndexOf('.') - 1 == 3)
            s = s.Replace(".", "");

        return decimal.TryParse(
            s,
            System.Globalization.NumberStyles.Number |
            System.Globalization.NumberStyles.AllowDecimalPoint,
            System.Globalization.CultureInfo.InvariantCulture,
            out var amount)
                ? amount
                : 0m;
    }

    private static string NormalizeHtmlText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return WebUtility.HtmlDecode(value)
            .Replace('\u00A0', ' ')
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Replace("\t", " ")
            .Replace("  ", " ")
            .Trim();
    }

    private string SalesCacheFilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NiyazisugarItemci",
            "sold-sales-history-v2.json"
        );

    private void LoadSalesHistory()
    {
        try
        {
            if (!File.Exists(SalesCacheFilePath))
                return;

            var json = File.ReadAllText(SalesCacheFilePath);
            var cache = JsonSerializer.Deserialize<SalesCacheEnvelope>(json);

            if (cache is null)
                return;

            _historyFullyScanned = cache.FullHistoryCompleted;

            foreach (var sale in cache.Sales ?? new List<SaleRecord>())
            {
                _salesHistory[SaleKey(sale)] = sale;
            }
        }
        catch
        {
            _historyFullyScanned = false;
            _salesHistory.Clear();
        }
    }

    private void SaveSalesHistory()
    {
        try
        {
            var folder = Path.GetDirectoryName(SalesCacheFilePath);

            if (!string.IsNullOrWhiteSpace(folder))
                Directory.CreateDirectory(folder);

            var cache = new SalesCacheEnvelope
            {
                FullHistoryCompleted = _historyFullyScanned,
                Sales = _salesHistory.Values
                    .OrderByDescending(x => x.Timestamp)
                    .ToList()
            };

            File.WriteAllText(
                SalesCacheFilePath,
                JsonSerializer.Serialize(
                    cache,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    }
                )
            );
        }
        catch
        {
            // Önbellek yazılamasa bile satış paneli çalışmaya devam etsin.
        }
    }

    private static string FormatTl(decimal amount)
    {
        return amount.ToString(
            "N2",
            System.Globalization.CultureInfo.GetCultureInfo("tr-TR")
        ) + " TL";
    }

    private static void DrawBorder(Graphics g, Rectangle rect)
    {
        PremiumTheme.DrawBorder(g, rect);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _monitorTimer?.Dispose();
            _salesTimer?.Dispose();
            _trayIcon?.Dispose();
            foreach (var tab in _browserTabs.ToList())
            {
                try
                {
                    tab.WebView?.Dispose();
                }
                catch { }
            }

            _messageMonitorWebView?.Dispose();
            _marketWebView?.Dispose();
        }

        base.Dispose(disposing);
    }

    private sealed class BrowserTabState
    {
        public string RequestedUrl { get; set; } = string.Empty;
        public int Id { get; set; }
        public WebView2 WebView { get; set; } = null!;
        public Panel? TabPanel { get; set; }
        public Button? TitleButton { get; set; }
        public string Title { get; set; } = "Itemci";
    }

    private sealed class ListingCreateResult
    {
        [JsonPropertyName("ok")]
        public bool Ok { get; set; }

        [JsonPropertyName("page")]
        public string Page { get; set; } = "";

        [JsonPropertyName("server")]
        public string Server { get; set; } = "";

        [JsonPropertyName("subCategory")]
        public string SubCategory { get; set; } = "";

        [JsonPropertyName("titleFocused")]
        public bool TitleFocused { get; set; }
    }

    private sealed class ListingInfo
    {
        public string Title { get; set; } = "";
        public string Url { get; set; } = "";
        public string PriceText { get; set; } = "";
        public string Status { get; set; } = "AKTİF"; public string ImageUrl { get; set; } = ""; public string Server { get; set; } = "Bilinmeyen sunucu";
    }

    private sealed class MarketServerInfo
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("categoryCount")]
        public int CategoryCount { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; } = "";

        [JsonPropertyName("isDefault")]
        public bool IsDefault { get; set; }
    }

    private sealed class MarketBridgeMessage
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "";

        [JsonPropertyName("servers")]
        public List<MarketServerInfo>? Servers { get; set; }
    }

    private sealed class SaleRecord
    {
        public DateTime Timestamp { get; set; }
        public string ItemTitle { get; set; } = ""; public string Server { get; set; } = "Bilinmeyen sunucu"; public string ImageUrl { get; set; } = "";
        public decimal GrossAmount { get; set; }
        public string OrderId { get; set; } = ""; public string ListingId { get; set; } = "";
        public bool IsDelivered { get; set; }

        public decimal CommissionAmount => decimal.Round(GrossAmount * 0.10m, 2, MidpointRounding.AwayFromZero);
        public decimal NetAmount => GrossAmount - CommissionAmount;
    }

    private sealed class NotificationPageResult
    {
        public List<SaleRecord> Sales { get; set; } = new();
        public HashSet<string> DeliveredOrderIds { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);

        public int NotificationTimestampCount { get; set; }
        public string Fingerprint { get; set; } = "";
    }

    private sealed class SalesCacheEnvelope
    {
        public bool FullHistoryCompleted { get; set; }
        public List<SaleRecord> Sales { get; set; } = new();
    }

    private sealed class BrowserFetchResult
    {
        [JsonPropertyName("status")]
        public int Status { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; } = "";

        [JsonPropertyName("text")]
        public string Text { get; set; } = "";
    }

    private sealed class DomMonitorState
    {
        [JsonPropertyName("count")]
        public int Count { get; set; }

        [JsonPropertyName("note")]
        public string? Note { get; set; }

        [JsonPropertyName("messageUrl")]
        public string? MessageUrl { get; set; }

        [JsonPropertyName("pageUrl")]
        public string? PageUrl { get; set; }

        [JsonPropertyName("pageTitle")]
        public string? PageTitle { get; set; }

        [JsonPropertyName("conversations")]
        public List<ConversationState>? Conversations { get; set; }

        [JsonPropertyName("diagnostics")]
        public string? Diagnostics { get; set; }
    }

    private sealed class ConversationState
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "Bilinmeyen kullanıcı";

        [JsonPropertyName("unread")]
        public int Unread { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("preview")]
        public string? Preview { get; set; }
    }
}

