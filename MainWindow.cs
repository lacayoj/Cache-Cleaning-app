using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace WinCleanPro
{
    public class MainWindow : Window
    {
        private CleanerEngine _engine;
        private List<CleanCategoryItem> _categories;
        private CancellationTokenSource _cts;
        private CleanProfile _currentProfile = CleanProfile.Quick;
        private CategoryGroup _currentGroup = CategoryGroup.All;

        // UI Controls - Header
        private TextBlock _adminStatusBadge;
        private TextBlock _driveInfoBadge;

        // UI Controls - Hero / Animation
        private TextBlock _heroTitleText;
        private TextBlock _heroSubText;
        private TextBlock _heroSizeText;
        private TextBlock _currentFileText;
        private ProgressBar _progressBar;
        private TextBlock _progressPctText;
        private Border _statusIndicatorPill;
        private Grid _heroCenterGrid;
        private Canvas _orbitCanvas;
        private Ellipse _orbitRing1;
        private Ellipse _orbitRing2;
        private Ellipse _orbitDot1;
        private Ellipse _orbitDot2;

        // UI Controls - Profile Selector
        private StackPanel _profileSelectorPanel;
        private Dictionary<CleanProfile, Button> _profileButtons = new Dictionary<CleanProfile, Button>();

        // UI Controls - Group Tabs
        private StackPanel _groupTabsPanel;
        private Dictionary<CategoryGroup, Button> _groupTabButtons = new Dictionary<CategoryGroup, Button>();

        // UI Controls - Conflict Warning Banner
        private Border _conflictBanner;
        private TextBlock _conflictBannerText;
        private Button _btnResolveConflicts;
        private List<string> _currentConflictingProcesses = new List<string>();

        // UI Controls - Categories & Logs
        private StackPanel _categoriesPanel;
        private Border _logDrawer;
        private TextBox _txtLog;
        private bool _isLogVisible = false;
        private bool _hasScanResults = false;

        // UI Controls - Bottom Bar
        private Button _btnSelectAll;
        private Button _btnToggleLog;
        private CheckBox _chkProtect24h;
        private Button _btnScan;
        private Button _btnClean;
        private Button _btnCancel;

        // Animation Storyboards
        private Storyboard _rotationStoryboard;
        private Storyboard _pulseStoryboard;
        private string _activeCategoryId = null;

        // ==========================================
        // PALETA OFICIAL: CYPRUS (#004741) & SAND (#F0EDE4)
        // ==========================================
        private readonly SolidColorBrush _colorCyprus = new SolidColorBrush(Color.FromRgb(0, 71, 65));       // #004741 Pure Cyprus
        private readonly SolidColorBrush _colorSand = new SolidColorBrush(Color.FromRgb(240, 237, 228));   // #F0EDE4 Pure Sand

        private readonly SolidColorBrush _bgDark = new SolidColorBrush(Color.FromRgb(3, 24, 22));           // #031816 Deep Cyprus Forest
        private readonly SolidColorBrush _cardBg = new SolidColorBrush(Color.FromRgb(6, 44, 40));           // #062C28 Card Cyprus
        private readonly SolidColorBrush _cardHoverBg = new SolidColorBrush(Color.FromRgb(10, 60, 55));     // #0A3C37 Hover Cyprus
        private readonly SolidColorBrush _borderBrush = new SolidColorBrush(Color.FromRgb(18, 77, 71));     // #124D47 Border
        private readonly SolidColorBrush _textPrimary = new SolidColorBrush(Color.FromRgb(240, 237, 228));   // #F0EDE4 Sand
        private readonly SolidColorBrush _textSecondary = new SolidColorBrush(Color.FromRgb(172, 194, 187)); // #ACC2BB Sage Sand
        private readonly SolidColorBrush _accentCyan = new SolidColorBrush(Color.FromRgb(0, 204, 175));      // #00CCAF Vibrant Mint Accent
        private readonly SolidColorBrush _accentIndigo = new SolidColorBrush(Color.FromRgb(0, 71, 65));      // #004741 Cyprus
        private readonly SolidColorBrush _accentGreen = new SolidColorBrush(Color.FromRgb(26, 188, 156));    // #1ABC9C Emerald
        private readonly SolidColorBrush _accentAmber = new SolidColorBrush(Color.FromRgb(224, 164, 88));    // #E0A458 Sand Gold
        private readonly SolidColorBrush _accentPurple = new SolidColorBrush(Color.FromRgb(0, 102, 94));     // #00665E

        private Dictionary<string, Border> _categoryCards = new Dictionary<string, Border>();
        private Dictionary<string, TextBlock> _categorySizeLabels = new Dictionary<string, TextBlock>();
        private Dictionary<string, CheckBox> _categoryCheckBoxes = new Dictionary<string, CheckBox>();

        public MainWindow()
        {
            InitializeComponent();
            _engine = new CleanerEngine();
            _engine.ProgressChanged += Engine_ProgressChanged;
            _engine.LogMessage += Engine_LogMessage;

            LoadData();
            ApplyProfile(CleanProfile.Quick);
            UpdateDriveSpaceDisplay();
            CheckActiveProcessConflicts();
        }

        private void TrySetWindowIcon()
        {
            // El icono ya viaja embebido como recurso Win32 dentro del .exe (compilado con
            // /win32icon:AppIcon.ico), así que lo extraemos directamente del propio proceso.
            // Esto evita depender de un archivo .ico externo en tiempo de ejecución.
            try
            {
                string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
                using (System.Drawing.Icon extracted = System.Drawing.Icon.ExtractAssociatedIcon(exePath))
                {
                    if (extracted != null)
                    {
                        Icon = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                            extracted.Handle,
                            Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions());
                    }
                }
            }
            catch
            {
                // Si por algún motivo no se puede extraer (p.ej. ejecutándose sin el icono
                // embebido), la app sigue funcionando normalmente sin icono personalizado.
            }
        }

        private void InitializeComponent()
        {
            Title = "SS Clan Cleaner Pro - Limpiador Inteligente y Acelerador de Windows";
            Width = 1040;
            Height = 780;
            MinWidth = 920;
            MinHeight = 680;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = _bgDark;
            Foreground = _textPrimary;
            FontFamily = new FontFamily("Segoe UI, Arial");
            TrySetWindowIcon();

            Grid rootGrid = new Grid();
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(68) });  // 0: Header
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });     // 1: Profiles & Conflict Banner
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(175) }); // 2: Hero Animation Dial
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });     // 3: Group Tabs
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 4: Categories & Logs
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(72) });  // 5: Bottom Action Bar

            // ==========================================
            // 0. TOP HEADER (Cyprus & Sand)
            // ==========================================
            Border headerBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(235, 2, 22, 20)),
                BorderBrush = _borderBrush,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(24, 12, 24, 12)
            };
            Grid headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Brand
            StackPanel brandPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            Border logoBorder = new Border
            {
                Width = 38,
                Height = 38,
                CornerRadius = new CornerRadius(10),
                Background = new LinearGradientBrush(Color.FromRgb(0, 71, 65), Color.FromRgb(0, 46, 42), 45),
                BorderBrush = _colorSand,
                BorderThickness = new Thickness(1.5),
                Margin = new Thickness(0, 0, 14, 0)
            };
            TextBlock logoIcon = new TextBlock
            {
                Text = "SS",
                FontSize = 16,
                FontWeight = FontWeights.ExtraBold,
                Foreground = _colorSand,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            logoBorder.Child = logoIcon;
            brandPanel.Children.Add(logoBorder);

            StackPanel titleTextPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            TextBlock titleLabel = new TextBlock
            {
                Text = "SS Clan Cleaner Pro",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = _colorSand
            };
            TextBlock subtitleLabel = new TextBlock
            {
                Text = "Limpieza Inteligente, Shaders de GPU y Rendimiento",
                FontSize = 11,
                Foreground = _textSecondary
            };
            titleTextPanel.Children.Add(titleLabel);
            titleTextPanel.Children.Add(subtitleLabel);
            brandPanel.Children.Add(titleTextPanel);
            Grid.SetColumn(brandPanel, 0);
            headerGrid.Children.Add(brandPanel);

            // Right Badges (Admin + Drive Space)
            StackPanel headerRightPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            // Drive Space Pill
            Border drivePill = new Border
            {
                Background = _cardBg,
                BorderBrush = _borderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(12, 6, 12, 6),
                Margin = new Thickness(0, 0, 10, 0)
            };
            _driveInfoBadge = new TextBlock
            {
                Text = "💾 Disco: Calculando...",
                FontSize = 12,
                Foreground = _colorSand
            };
            drivePill.Child = _driveInfoBadge;
            headerRightPanel.Children.Add(drivePill);

            // Admin Pill / Button
            bool isAdmin = DiskHelper.IsRunningAsAdministrator();
            Border adminPill = new Border
            {
                Background = isAdmin ? new SolidColorBrush(Color.FromArgb(40, 26, 188, 156)) : new SolidColorBrush(Color.FromArgb(40, 224, 164, 88)),
                BorderBrush = isAdmin ? _accentGreen : _accentAmber,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(12, 6, 12, 6),
                Cursor = isAdmin ? System.Windows.Input.Cursors.Arrow : System.Windows.Input.Cursors.Hand
            };
            _adminStatusBadge = new TextBlock
            {
                Text = isAdmin ? "🛡️ Administrador" : "⚡ Ejecutar como Admin",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = isAdmin ? _accentGreen : _accentAmber
            };
            if (!isAdmin)
            {
                adminPill.ToolTip = "Haz clic aquí para reiniciar con permisos de Administrador y limpiar carpetas de sistema.";
                adminPill.MouseLeftButtonUp += (s, e) => DiskHelper.RestartAsAdministrator();
            }
            adminPill.Child = _adminStatusBadge;
            headerRightPanel.Children.Add(adminPill);

            Grid.SetColumn(headerRightPanel, 1);
            headerGrid.Children.Add(headerRightPanel);
            headerBorder.Child = headerGrid;
            Grid.SetRow(headerBorder, 0);
            rootGrid.Children.Add(headerBorder);

            // ==========================================
            // 1. PROFILES & CONFLICT WARNING BANNER
            // ==========================================
            StackPanel topControlBar = new StackPanel
            {
                Background = new SolidColorBrush(Color.FromArgb(200, 3, 24, 22)),
                Margin = new Thickness(0, 0, 0, 0)
            };

            // Conflict Warning Banner
            _conflictBanner = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(70, 224, 164, 88)),
                BorderBrush = _accentAmber,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(24, 8, 24, 8),
                Visibility = Visibility.Collapsed
            };
            Grid conflictGrid = new Grid();
            conflictGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            conflictGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _conflictBannerText = new TextBlock
            {
                Text = "⚠️ Aplicaciones abiertas detectadas que pueden bloquear la caché.",
                FontSize = 12,
                Foreground = _colorSand,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_conflictBannerText, 0);
            conflictGrid.Children.Add(_conflictBannerText);

            _btnResolveConflicts = new Button
            {
                Content = "Cerrar Aplicaciones",
                Height = 28,
                Padding = new Thickness(12, 2, 12, 2),
                Background = _accentAmber,
                Foreground = _colorCyprus,
                FontWeight = FontWeights.Bold,
                FontSize = 11,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            _btnResolveConflicts.Click += BtnResolveConflicts_Click;
            Grid.SetColumn(_btnResolveConflicts, 1);
            conflictGrid.Children.Add(_btnResolveConflicts);
            _conflictBanner.Child = conflictGrid;
            topControlBar.Children.Add(_conflictBanner);

            // Profile Pills Bar
            Border profilesBorder = new Border
            {
                Padding = new Thickness(24, 8, 24, 8),
                BorderBrush = _borderBrush,
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
            StackPanel profileInnerPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            TextBlock profileLabel = new TextBlock
            {
                Text = "PERFIL:",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = _textSecondary,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0)
            };
            profileInnerPanel.Children.Add(profileLabel);

            _profileSelectorPanel = new StackPanel { Orientation = Orientation.Horizontal };
            AddProfileButton(CleanProfile.Quick, "⚡ Limpieza Rápida");
            AddProfileButton(CleanProfile.Deep, "🛡️ Limpieza Profunda");
            AddProfileButton(CleanProfile.Gamer, "🎮 Modo Gamer / GPU");
            AddProfileButton(CleanProfile.Developer, "💻 Modo Desarrollador");
            AddProfileButton(CleanProfile.Custom, "⚙️ Personalizada");
            profileInnerPanel.Children.Add(_profileSelectorPanel);
            profilesBorder.Child = profileInnerPanel;
            topControlBar.Children.Add(profilesBorder);

            Grid.SetRow(topControlBar, 1);
            rootGrid.Children.Add(topControlBar);

            // ==========================================
            // 2. HERO / ANIMATION ZONE
            // ==========================================
            Border heroBorder = new Border
            {
                Background = new LinearGradientBrush(Color.FromRgb(0, 71, 65), Color.FromRgb(3, 24, 22), 90),
                BorderBrush = _borderBrush,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(24, 12, 24, 12)
            };

            Grid heroGrid = new Grid();
            heroGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
            heroGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Center Animated Ring
            Border animationContainer = new Border
            {
                Width = 135,
                Height = 135,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            _heroCenterGrid = new Grid();
            _orbitCanvas = new Canvas { Width = 135, Height = 135, RenderTransformOrigin = new Point(0.5, 0.5) };
            _orbitCanvas.RenderTransform = new RotateTransform(0);

            // Outer Ring: Cyprus & Mint
            _orbitRing1 = new Ellipse
            {
                Width = 125,
                Height = 125,
                StrokeThickness = 3,
                Stroke = new LinearGradientBrush(Color.FromRgb(0, 71, 65), Color.FromRgb(0, 204, 175), 45)
            };
            Canvas.SetLeft(_orbitRing1, 5);
            Canvas.SetTop(_orbitRing1, 5);
            _orbitCanvas.Children.Add(_orbitRing1);

            // Inner Ring: Sand dashed
            _orbitRing2 = new Ellipse
            {
                Width = 105,
                Height = 105,
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 4, 3 },
                Stroke = new SolidColorBrush(Color.FromArgb(140, 240, 237, 228))
            };
            Canvas.SetLeft(_orbitRing2, 15);
            Canvas.SetTop(_orbitRing2, 15);
            _orbitCanvas.Children.Add(_orbitRing2);

            // Dot 1: Sand Glow
            _orbitDot1 = new Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = _colorSand,
                Effect = new DropShadowEffect { Color = Color.FromRgb(240, 237, 228), BlurRadius = 12, ShadowDepth = 0, Opacity = 0.9 }
            };
            Canvas.SetLeft(_orbitDot1, 62);
            Canvas.SetTop(_orbitDot1, 0);
            _orbitCanvas.Children.Add(_orbitDot1);

            // Dot 2: Mint Glow
            _orbitDot2 = new Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = _accentCyan,
                Effect = new DropShadowEffect { Color = Color.FromRgb(0, 204, 175), BlurRadius = 10, ShadowDepth = 0, Opacity = 0.8 }
            };
            Canvas.SetLeft(_orbitDot2, 63);
            Canvas.SetTop(_orbitDot2, 127);
            _orbitCanvas.Children.Add(_orbitDot2);

            _heroCenterGrid.Children.Add(_orbitCanvas);

            StackPanel ringCenterPanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _progressPctText = new TextBlock
            {
                Text = "🧹",
                FontSize = 30,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = _colorSand
            };
            ringCenterPanel.Children.Add(_progressPctText);
            _heroCenterGrid.Children.Add(ringCenterPanel);

            animationContainer.Child = _heroCenterGrid;
            Grid.SetColumn(animationContainer, 0);
            heroGrid.Children.Add(animationContainer);

            // Right Hero Details
            StackPanel heroDetailsPanel = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(20, 0, 0, 0)
            };

            _statusIndicatorPill = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = new SolidColorBrush(Color.FromArgb(60, 0, 71, 65)),
                BorderBrush = _colorSand,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(10, 3, 10, 3),
                Margin = new Thickness(0, 0, 0, 6)
            };
            _heroTitleText = new TextBlock
            {
                Text = "ESTADO: LISTO PARA ANALIZAR",
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = _colorSand
            };
            _statusIndicatorPill.Child = _heroTitleText;
            heroDetailsPanel.Children.Add(_statusIndicatorPill);

            _heroSubText = new TextBlock
            {
                Text = "Optimiza tu PC eliminando cachés obsoletos, shaders y temporales",
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = _colorSand,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 4)
            };
            heroDetailsPanel.Children.Add(_heroSubText);

            _heroSizeText = new TextBlock
            {
                Text = "Elige tu perfil de limpieza o haz clic en 'Analizar Sistema' para medir el espacio disponible.",
                FontSize = 12,
                Foreground = _textSecondary,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            };
            heroDetailsPanel.Children.Add(_heroSizeText);

            _progressBar = new ProgressBar
            {
                Height = 7,
                Value = 0,
                Maximum = 100,
                Foreground = new LinearGradientBrush(Color.FromRgb(0, 204, 175), Color.FromRgb(240, 237, 228), 0),
                Background = new SolidColorBrush(Color.FromRgb(3, 24, 22)),
                BorderThickness = new Thickness(0),
                Margin = new Thickness(0, 0, 0, 5)
            };
            heroDetailsPanel.Children.Add(_progressBar);

            _currentFileText = new TextBlock
            {
                Text = "Listo",
                FontSize = 11,
                Foreground = _textSecondary,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            heroDetailsPanel.Children.Add(_currentFileText);

            Grid.SetColumn(heroDetailsPanel, 1);
            heroGrid.Children.Add(heroDetailsPanel);

            heroBorder.Child = heroGrid;
            Grid.SetRow(heroBorder, 2);
            rootGrid.Children.Add(heroBorder);

            // ==========================================
            // 3. GROUP FILTER TABS
            // ==========================================
            Border tabsBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(180, 2, 22, 20)),
                BorderBrush = _borderBrush,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(24, 8, 24, 8)
            };
            _groupTabsPanel = new StackPanel { Orientation = Orientation.Horizontal };
            AddGroupTab(CategoryGroup.All, "Todas las Categorías");
            AddGroupTab(CategoryGroup.System, "🖥️ Sistema Windows");
            AddGroupTab(CategoryGroup.Browsers, "🌐 Navegadores Web");
            AddGroupTab(CategoryGroup.GamingGpu, "🎮 Shaders & GPU");
            AddGroupTab(CategoryGroup.MediaApps, "💬 Apps Multimedia");
            AddGroupTab(CategoryGroup.Developer, "💻 Desarrollador");
            tabsBorder.Child = _groupTabsPanel;
            Grid.SetRow(tabsBorder, 3);
            rootGrid.Children.Add(tabsBorder);

            // ==========================================
            // 4. MAIN CONTENT: CATEGORY CARDS & LOG DRAWER
            // ==========================================
            Grid mainContentGrid = new Grid();
            mainContentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            ScrollViewer scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(24, 14, 24, 14)
            };

            _categoriesPanel = new StackPanel();
            scrollViewer.Content = _categoriesPanel;
            mainContentGrid.Children.Add(scrollViewer);

            // Log Drawer
            _logDrawer = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(250, 3, 24, 22)),
                BorderBrush = _borderBrush,
                BorderThickness = new Thickness(0, 1, 0, 0),
                Height = 180,
                VerticalAlignment = VerticalAlignment.Bottom,
                Visibility = Visibility.Collapsed
            };
            Grid logGrid = new Grid();
            logGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });
            logGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            Border logHeader = new Border
            {
                Background = _cardBg,
                Padding = new Thickness(12, 4, 12, 4)
            };
            TextBlock logTitle = new TextBlock
            {
                Text = "📜 Registro de Actividad y Limpieza",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = _colorSand
            };
            logHeader.Child = logTitle;
            Grid.SetRow(logHeader, 0);
            logGrid.Children.Add(logHeader);

            _txtLog = new TextBox
            {
                Background = Brushes.Transparent,
                Foreground = _textSecondary,
                BorderThickness = new Thickness(0),
                FontFamily = new FontFamily("Consolas, Courier New"),
                FontSize = 11,
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(12, 6, 12, 6)
            };
            Grid.SetRow(_txtLog, 1);
            logGrid.Children.Add(_txtLog);
            _logDrawer.Child = logGrid;
            mainContentGrid.Children.Add(_logDrawer);

            Grid.SetRow(mainContentGrid, 4);
            rootGrid.Children.Add(mainContentGrid);

            // ==========================================
            // 5. BOTTOM ACTION BAR (Cyprus & Sand contrast)
            // ==========================================
            Border footerBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(240, 2, 22, 20)),
                BorderBrush = _borderBrush,
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(24, 12, 24, 12)
            };
            Grid footerGrid = new Grid();
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Left actions: Select All, Log, and 24h Safety Filter
            StackPanel leftFooterPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            _btnSelectAll = CreateSecondaryButton("☑️ Marcar Todo", 130);
            _btnSelectAll.Click += BtnSelectAll_Click;
            leftFooterPanel.Children.Add(_btnSelectAll);

            _btnToggleLog = CreateSecondaryButton("📋 Ver Log", 100);
            _btnToggleLog.Margin = new Thickness(8, 0, 0, 0);
            _btnToggleLog.Click += BtnToggleLog_Click;
            leftFooterPanel.Children.Add(_btnToggleLog);

            // 24-Hour Safety Checkbox
            Border safePill = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(50, 0, 71, 65)),
                BorderBrush = _colorSand,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(10, 4, 10, 4),
                Margin = new Thickness(14, 0, 0, 0)
            };
            _chkProtect24h = new CheckBox
            {
                IsChecked = true,
                Content = "🛡️ Proteger archivos recientes (< 24h)",
                Foreground = _colorSand,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "Recomendado: Evita eliminar temporales en uso por instaladores o descargas activas."
            };
            safePill.Child = _chkProtect24h;
            leftFooterPanel.Children.Add(safePill);

            Grid.SetColumn(leftFooterPanel, 0);
            footerGrid.Children.Add(leftFooterPanel);

            // Right actions: Scan / Clean / Cancel
            StackPanel rightFooterPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            _btnScan = CreateSecondaryButton("🔍 Analizar Sistema", 150);
            _btnScan.Click += BtnScan_Click;
            rightFooterPanel.Children.Add(_btnScan);

            // Signature Sand Button with Cyprus Text
            _btnClean = CreatePrimaryButton("✨ Limpiar Ahora", 160);
            _btnClean.Margin = new Thickness(12, 0, 0, 0);
            _btnClean.Click += BtnClean_Click;
            rightFooterPanel.Children.Add(_btnClean);

            _btnCancel = CreateSecondaryButton("✖ Cancelar", 110);
            _btnCancel.Margin = new Thickness(12, 0, 0, 0);
            _btnCancel.Visibility = Visibility.Collapsed;
            _btnCancel.Click += BtnCancel_Click;
            rightFooterPanel.Children.Add(_btnCancel);

            Grid.SetColumn(rightFooterPanel, 2);
            footerGrid.Children.Add(rightFooterPanel);

            footerBorder.Child = footerGrid;
            Grid.SetRow(footerBorder, 5);
            rootGrid.Children.Add(footerBorder);

            Content = rootGrid;

            SetupAnimations();
        }

        private void AddProfileButton(CleanProfile profile, string title)
        {
            Button btn = new Button
            {
                Content = title,
                Height = 30,
                Padding = new Thickness(12, 2, 12, 2),
                Margin = new Thickness(0, 0, 8, 0),
                Background = _cardBg,
                Foreground = _textSecondary,
                FontSize = 11,
                FontWeight = FontWeights.Medium,
                BorderBrush = _borderBrush,
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btn.Click += (s, e) => ApplyProfile(profile);
            _profileButtons[profile] = btn;
            _profileSelectorPanel.Children.Add(btn);
        }

        private void AddGroupTab(CategoryGroup group, string title)
        {
            Button btn = new Button
            {
                Content = title,
                Height = 28,
                Padding = new Thickness(12, 2, 12, 2),
                Margin = new Thickness(0, 0, 8, 0),
                Background = (group == CategoryGroup.All) ? _colorCyprus : Brushes.Transparent,
                Foreground = (group == CategoryGroup.All) ? _colorSand : _textSecondary,
                FontSize = 11,
                FontWeight = (group == CategoryGroup.All) ? FontWeights.Bold : FontWeights.Normal,
                BorderBrush = (group == CategoryGroup.All) ? _colorSand : _borderBrush,
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btn.Click += (s, e) => FilterCategoriesByGroup(group);
            _groupTabButtons[group] = btn;
            _groupTabsPanel.Children.Add(btn);
        }

        private void ApplyProfile(CleanProfile profile)
        {
            _currentProfile = profile;
            foreach (var kv in _profileButtons)
            {
                bool active = kv.Key == profile;
                kv.Value.Background = active ? _colorCyprus : _cardBg;
                kv.Value.Foreground = active ? _colorSand : _textSecondary;
                kv.Value.BorderBrush = active ? _colorSand : _borderBrush;
                kv.Value.FontWeight = active ? FontWeights.Bold : FontWeights.Medium;
            }

            if (profile == CleanProfile.Custom) return;

            foreach (var cat in _categories)
            {
                bool select = false;
                switch (profile)
                {
                    case CleanProfile.Quick:
                        select = (cat.Type == CategoryType.UserTemp || cat.Type == CategoryType.BrowserCache || cat.Type == CategoryType.Thumbnails || cat.Type == CategoryType.DnsCache || cat.Type == CategoryType.ErrorReports);
                        break;
                    case CleanProfile.Deep:
                        select = (cat.Group == CategoryGroup.System || cat.Group == CategoryGroup.Browsers);
                        break;
                    case CleanProfile.Gamer:
                        select = (cat.Type == CategoryType.GpuShaders || cat.Type == CategoryType.UserTemp || cat.Type == CategoryType.MediaDiscord || cat.Type == CategoryType.MediaSpotify || cat.Type == CategoryType.DnsCache);
                        break;
                    case CleanProfile.Developer:
                        select = (cat.Group == CategoryGroup.Developer || cat.Type == CategoryType.UserTemp);
                        break;
                }
                cat.IsSelected = select;
                if (_categoryCheckBoxes.ContainsKey(cat.Id))
                {
                    _categoryCheckBoxes[cat.Id].IsChecked = select;
                }
            }
            CheckActiveProcessConflicts();
            RefreshHeroSelectionSummary();
        }

        private void FilterCategoriesByGroup(CategoryGroup group)
        {
            _currentGroup = group;
            foreach (var kv in _groupTabButtons)
            {
                bool active = kv.Key == group;
                kv.Value.Background = active ? _colorCyprus : Brushes.Transparent;
                kv.Value.Foreground = active ? _colorSand : _textSecondary;
                kv.Value.BorderBrush = active ? _colorSand : _borderBrush;
                kv.Value.FontWeight = active ? FontWeights.Bold : FontWeights.Normal;
            }

            foreach (var cat in _categories)
            {
                if (_categoryCards.ContainsKey(cat.Id))
                {
                    bool visible = (group == CategoryGroup.All) || (cat.Group == group);
                    _categoryCards[cat.Id].Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }

        private void CheckActiveProcessConflicts()
        {
            List<string> candidateProcs = new List<string>();
            foreach (var cat in _categories)
            {
                if (cat.IsSelected && cat.ConflictingProcesses != null)
                {
                    foreach (string p in cat.ConflictingProcesses)
                    {
                        if (!candidateProcs.Contains(p)) candidateProcs.Add(p);
                    }
                }
            }

            _currentConflictingProcesses = DiskHelper.GetActiveConflictingProcesses(candidateProcs);
            if (_currentConflictingProcesses.Count > 0)
            {
                _conflictBannerText.Text = string.Format("⚠️ Procesos activos detectados ({0}). Ciérralos para liberar el 100% de la caché sin archivos omitidos.", string.Join(", ", _currentConflictingProcesses.ToArray()));
                _conflictBanner.Visibility = Visibility.Visible;
            }
            else
            {
                _conflictBanner.Visibility = Visibility.Collapsed;
            }
        }

        private void BtnResolveConflicts_Click(object sender, RoutedEventArgs e)
        {
            if (_currentConflictingProcesses.Count > 0)
            {
                DiskHelper.CloseConflictingProcesses(_currentConflictingProcesses);
                Thread.Sleep(500);
                CheckActiveProcessConflicts();
            }
        }

        private void SetupAnimations()
        {
            _rotationStoryboard = new Storyboard();
            DoubleAnimation rotateAnim = new DoubleAnimation
            {
                From = 0,
                To = 360,
                Duration = TimeSpan.FromSeconds(3),
                RepeatBehavior = RepeatBehavior.Forever
            };
            Storyboard.SetTarget(_rotationStoryboard, _orbitCanvas);
            Storyboard.SetTargetProperty(_rotationStoryboard, new PropertyPath("(UIElement.RenderTransform).(RotateTransform.Angle)"));
            _rotationStoryboard.Children.Add(rotateAnim);

            _pulseStoryboard = new Storyboard();
            DoubleAnimation pulseAnim = new DoubleAnimation
            {
                From = 0.95,
                To = 1.05,
                Duration = TimeSpan.FromSeconds(1.2),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            ScaleTransform scaleTransform = new ScaleTransform(1, 1);
            _heroCenterGrid.RenderTransformOrigin = new Point(0.5, 0.5);
            _heroCenterGrid.RenderTransform = scaleTransform;
            Storyboard.SetTarget(_pulseStoryboard, _heroCenterGrid);
            Storyboard.SetTargetProperty(_pulseStoryboard, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleX)"));
            _pulseStoryboard.Children.Add(pulseAnim);

            DoubleAnimation pulseAnimY = pulseAnim.Clone();
            Storyboard.SetTarget(pulseAnimY, _heroCenterGrid);
            Storyboard.SetTargetProperty(pulseAnimY, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleY)"));
            _pulseStoryboard.Children.Add(pulseAnimY);
        }

        private void StartElegantCleaningAnimation()
        {
            _rotationStoryboard.Begin();
            _pulseStoryboard.Begin();

            _orbitRing1.Stroke = new LinearGradientBrush(Color.FromRgb(0, 71, 65), Color.FromRgb(0, 204, 175), 45);
            _statusIndicatorPill.BorderBrush = _colorSand;
            _statusIndicatorPill.Background = new SolidColorBrush(Color.FromArgb(60, 0, 71, 65));
            _heroTitleText.Foreground = _colorSand;
            _heroTitleText.Text = "⚡ LIMPIEZA EN CURSO...";
        }

        private void StopCleaningAnimation(bool success, string totalFreed)
        {
            _rotationStoryboard.Stop();
            _pulseStoryboard.Stop();

            if (!string.IsNullOrEmpty(_activeCategoryId) && _categoryCards.ContainsKey(_activeCategoryId))
            {
                _categoryCards[_activeCategoryId].BorderBrush = _borderBrush;
            }
            _activeCategoryId = null;

            if (success)
            {
                _orbitRing1.Stroke = _colorSand;
                _orbitRing2.Stroke = new SolidColorBrush(Color.FromArgb(140, 26, 188, 156));
                _orbitDot1.Fill = _colorSand;
                _orbitDot2.Fill = _accentGreen;

                _statusIndicatorPill.BorderBrush = _colorSand;
                _statusIndicatorPill.Background = new SolidColorBrush(Color.FromArgb(60, 26, 188, 156));
                _heroTitleText.Foreground = _colorSand;
                _heroTitleText.Text = "✓ SISTEMA OPTIMIZADO";

                _progressPctText.Text = "✓";
                _progressPctText.Foreground = _colorSand;

                _heroSubText.Text = "¡Limpieza completada con éxito!";
                _heroSizeText.Text = string.Format("Se han liberado {0} de almacenamiento en tu equipo.", totalFreed);
                _progressBar.Value = 100;
                _progressBar.Foreground = _colorSand;
                _currentFileText.Text = "Operación finalizada. Tu sistema está más ligero y rápido.";
            }
            else
            {
                _progressPctText.Text = "🧹";
                _progressPctText.Foreground = _colorSand;
            }
        }

        private void LoadData()
        {
            _categories = _engine.GetDefaultCategories();
            _categoriesPanel.Children.Clear();
            _categoryCards.Clear();
            _categorySizeLabels.Clear();
            _categoryCheckBoxes.Clear();

            foreach (var cat in _categories)
            {
                Border card = CreateCategoryCard(cat);
                _categoryCards[cat.Id] = card;
                _categoriesPanel.Children.Add(card);
            }
        }

        private Border CreateCategoryCard(CleanCategoryItem cat)
        {
            Border cardBorder = new Border
            {
                Background = _cardBg,
                BorderBrush = _borderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 0, 0, 8),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            Grid cardGrid = new Grid();
            cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
            cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Inspect Button
            cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Size Label
            cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) }); // Checkbox

            // Icon Badge in Cyprus style
            Border iconBadge = new Border
            {
                Width = 36,
                Height = 36,
                CornerRadius = new CornerRadius(8),
                Background = _colorCyprus,
                BorderBrush = _borderBrush,
                BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center
            };
            TextBlock iconText = new TextBlock
            {
                Text = cat.IconSymbol,
                FontSize = 18,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            iconBadge.Child = iconText;
            Grid.SetColumn(iconBadge, 0);
            cardGrid.Children.Add(iconBadge);

            // Title & Description
            StackPanel textPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 12, 0) };
            StackPanel titleRow = new StackPanel { Orientation = Orientation.Horizontal };
            TextBlock titleBlock = new TextBlock
            {
                Text = cat.Name,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = _colorSand
            };
            titleRow.Children.Add(titleBlock);

            if (cat.RequiresAdmin)
            {
                Border adminTag = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(40, 224, 164, 88)),
                    BorderBrush = _accentAmber,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(4, 1, 4, 1),
                    Margin = new Thickness(8, 0, 0, 0)
                };
                adminTag.Child = new TextBlock { Text = "ADMIN", FontSize = 9, FontWeight = FontWeights.Bold, Foreground = _accentAmber };
                titleRow.Children.Add(adminTag);
            }

            textPanel.Children.Add(titleRow);

            TextBlock descBlock = new TextBlock
            {
                Text = cat.Description,
                FontSize = 11,
                Foreground = _textSecondary,
                Margin = new Thickness(0, 2, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            textPanel.Children.Add(descBlock);

            Grid.SetColumn(textPanel, 1);
            cardGrid.Children.Add(textPanel);

            // Inspect / Details Button
            Button btnInspect = new Button
            {
                Content = "🔍 Explorar",
                Height = 26,
                Padding = new Thickness(8, 2, 8, 2),
                Background = _colorCyprus,
                Foreground = _colorSand,
                BorderBrush = _colorSand,
                BorderThickness = new Thickness(1),
                FontSize = 11,
                FontWeight = FontWeights.Medium,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0),
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = "Abre el explorador de archivos para ver qué contiene esta categoría."
            };
            btnInspect.Click += (s, e) =>
            {
                e.Handled = true;
                OpenFileInspector(cat);
            };
            Grid.SetColumn(btnInspect, 2);
            cardGrid.Children.Add(btnInspect);

            // Size / Status Label
            TextBlock sizeLabel = new TextBlock
            {
                Text = cat.StatusText,
                FontSize = 12,
                FontWeight = FontWeights.Medium,
                Foreground = _textSecondary,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, 14, 0)
            };
            _categorySizeLabels[cat.Id] = sizeLabel;
            Grid.SetColumn(sizeLabel, 3);
            cardGrid.Children.Add(sizeLabel);

            // Checkbox
            CheckBox chk = new CheckBox
            {
                IsChecked = cat.IsSelected,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            chk.Checked += (s, e) =>
            {
                cat.IsSelected = true;
                CheckActiveProcessConflicts();
                RefreshHeroSelectionSummary();
            };
            chk.Unchecked += (s, e) =>
            {
                cat.IsSelected = false;
                CheckActiveProcessConflicts();
                RefreshHeroSelectionSummary();
            };
            _categoryCheckBoxes[cat.Id] = chk;
            Grid.SetColumn(chk, 4);
            cardGrid.Children.Add(chk);

            cardBorder.MouseLeftButtonUp += (s, e) =>
            {
                if (e.OriginalSource != chk && !(e.OriginalSource is Button) && !(e.OriginalSource is TextBlock && ((TextBlock)e.OriginalSource).Text == "🔍 Explorar"))
                {
                    chk.IsChecked = !chk.IsChecked;
                }
            };

            cardBorder.MouseEnter += (s, e) => cardBorder.Background = _cardHoverBg;
            cardBorder.MouseLeave += (s, e) => cardBorder.Background = _cardBg;

            cardBorder.Child = cardGrid;
            return cardBorder;
        }

        private void OpenFileInspector(CleanCategoryItem cat)
        {
            FileInspectorWindow inspector = new FileInspectorWindow(cat, _engine);
            inspector.Owner = this;
            inspector.ShowDialog();
        }

        // Sand primary button with bold Cyprus text (#F0EDE4 background, #004741 text)
        private Button CreatePrimaryButton(string text, double width)
        {
            Button btn = new Button
            {
                Content = text,
                Width = width,
                Height = 42,
                Background = _colorSand,
                Foreground = _colorCyprus,
                FontWeight = FontWeights.ExtraBold,
                FontSize = 13,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btn.Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(240, 237, 228),
                BlurRadius = 14,
                ShadowDepth = 0,
                Opacity = 0.35
            };
            return btn;
        }

        private Button CreateSecondaryButton(string text, double width)
        {
            Button btn = new Button
            {
                Content = text,
                Width = width,
                Height = 42,
                Background = _cardBg,
                Foreground = _colorSand,
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                BorderBrush = _borderBrush,
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            return btn;
        }

        private void UpdateDriveSpaceDisplay()
        {
            Task.Factory.StartNew(() =>
            {
                var info = DiskHelper.GetSystemDriveInfo();
                Dispatcher.Invoke(() =>
                {
                    if (info.FixedDrivesCount > 1)
                    {
                        _driveInfoBadge.Text = string.Format("💾 Disco {0} {1} lib. | Total {2} discos: {3} lib.",
                            info.DriveLetter, info.FormattedFree, info.FixedDrivesCount, info.FormattedGlobalFree);
                    }
                    else
                    {
                        _driveInfoBadge.Text = string.Format("💾 Disco {0} {1} libres de {2}", info.DriveLetter, info.FormattedFree, info.FormattedTotal);
                    }
                });
            });
        }

        private void BtnToggleLog_Click(object sender, RoutedEventArgs e)
        {
            _isLogVisible = !_isLogVisible;
            _logDrawer.Visibility = _isLogVisible ? Visibility.Visible : Visibility.Collapsed;
            _btnToggleLog.Content = _isLogVisible ? "📋 Ocultar Log" : "📋 Ver Log";
        }

        private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            bool anyUnchecked = false;
            foreach (var chk in _categoryCheckBoxes.Values)
            {
                if (chk.IsChecked != true) { anyUnchecked = true; break; }
            }

            bool newState = anyUnchecked;
            foreach (var cat in _categories)
            {
                cat.IsSelected = newState;
                if (_categoryCheckBoxes.ContainsKey(cat.Id))
                {
                    _categoryCheckBoxes[cat.Id].IsChecked = newState;
                }
            }

            _btnSelectAll.Content = newState ? "☒ Desmarcar Todo" : "☑️ Marcar Todo";
            CheckActiveProcessConflicts();
            RefreshHeroSelectionSummary();
        }

        private async void BtnScan_Click(object sender, RoutedEventArgs e)
        {
            SetControlsBusyState(true);
            _heroTitleText.Text = "🔍 ANALIZANDO ARCHIVOS...";
            _heroTitleText.Foreground = _colorSand;
            _heroSubText.Text = "Escaneando cachés, shaders y temporales...";
            _heroSizeText.Text = "Calculando peso real de cada categoría...";
            _progressBar.IsIndeterminate = true;

            StartElegantCleaningAnimation();

            await Task.Factory.StartNew(() =>
            {
                _engine.ScanCategories(_categories, (cat) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (_categorySizeLabels.ContainsKey(cat.Id))
                        {
                            _categorySizeLabels[cat.Id].Text = cat.StatusText;
                            if (cat.TotalSizeBytes > 0)
                            {
                                _categorySizeLabels[cat.Id].Foreground = _colorSand;
                            }
                        }
                    });
                });
            });

            _progressBar.IsIndeterminate = false;
            _progressBar.Value = 0;
            StopCleaningAnimation(false, "");

            _hasScanResults = true;
            RefreshHeroSelectionSummary();
            _heroSizeText.Text = "Revisa las categorías marcadas o usa 'Explorar' para inspeccionar archivos antes de limpiar.";
            _currentFileText.Text = "Listo para iniciar la limpieza.";

            SetControlsBusyState(false);
            CheckActiveProcessConflicts();
        }

        // Recalcula y muestra cuánto se liberará según lo REALMENTE seleccionado,
        // frente al total encontrado en el análisis. Evita el mensaje engañoso de
        // mostrar el total escaneado cuando un perfil (p.ej. "Limpieza Rápida")
        // solo tiene marcada una parte de las categorías.
        private void RefreshHeroSelectionSummary()
        {
            if (!_hasScanResults) return;

            long totalSize = 0;
            int totalFiles = 0;
            long selectedSize = 0;
            int selectedFiles = 0;
            foreach (var cat in _categories)
            {
                totalSize += cat.TotalSizeBytes;
                totalFiles += cat.ItemCount;
                if (cat.IsSelected)
                {
                    selectedSize += cat.TotalSizeBytes;
                    selectedFiles += cat.ItemCount;
                }
            }

            _heroTitleText.Text = "ANÁLISIS COMPLETADO";
            if (selectedSize == totalSize && selectedFiles == totalFiles)
            {
                _heroSubText.Text = string.Format("Se encontraron {0} ({1} archivos) listos para purgar", DiskHelper.FormatBytes(totalSize), totalFiles);
            }
            else
            {
                _heroSubText.Text = string.Format("{0} ({1} archivos) seleccionados para purgar · {2} ({3} archivos) encontrados en total",
                    DiskHelper.FormatBytes(selectedSize), selectedFiles, DiskHelper.FormatBytes(totalSize), totalFiles);
            }
        }

        private async void BtnClean_Click(object sender, RoutedEventArgs e)
        {
            bool anySelected = false;
            foreach (var cat in _categories)
            {
                if (cat.IsSelected) { anySelected = true; break; }
            }

            if (!anySelected)
            {
                MessageBox.Show("Por favor, selecciona al menos una categoría para limpiar.", "SS Clan Cleaner Pro", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Confirmación de seguridad: el borrado es permanente (no pasa por la Papelera).
            // Se advierte explícitamente si hay categorías de mayor impacto seleccionadas.
            List<string> highImpact = new List<string>();
            foreach (var cat in _categories)
            {
                if (cat.IsSelected && (cat.Type == CategoryType.RecycleBin || cat.Type == CategoryType.WindowsMemoryDump))
                {
                    highImpact.Add(cat.Name);
                }
            }

            long totalToFree = 0;
            int totalCats = 0;
            foreach (var cat in _categories)
            {
                if (cat.IsSelected) { totalToFree += cat.TotalSizeBytes; totalCats++; }
            }

            string confirmMsg = string.Format(
                "Se limpiarán {0} categorías, liberando aproximadamente {1}.\n\nEsta acción es PERMANENTE y no se puede deshacer.",
                totalCats, DiskHelper.FormatBytes(totalToFree));

            if (highImpact.Count > 0)
            {
                confirmMsg += "\n\n⚠️ Incluye categorías sensibles:\n- " + string.Join("\n- ", highImpact.ToArray());
                if (highImpact.Contains("Papelera de Reciclaje"))
                {
                    confirmMsg += "\n\nLa Papelera de Reciclaje se vaciará por completo y esos archivos ya NO se podrán recuperar.";
                }
            }
            confirmMsg += "\n\n¿Deseas continuar?";

            MessageBoxResult confirmResult = MessageBox.Show(confirmMsg, "Confirmar limpieza - SS Clan Cleaner Pro",
                MessageBoxButton.YesNo, highImpact.Count > 0 ? MessageBoxImage.Warning : MessageBoxImage.Question, MessageBoxResult.No);

            if (confirmResult != MessageBoxResult.Yes)
            {
                return;
            }

            SetControlsBusyState(true);
            _btnCancel.Visibility = Visibility.Visible;
            _cts = new CancellationTokenSource();

            StartElegantCleaningAnimation();
            _progressBar.Value = 0;

            CleanerOptions options = new CleanerOptions
            {
                SkipRecentFiles24h = _chkProtect24h.IsChecked == true
            };

            SummaryResult result = null;
            await Task.Factory.StartNew(() =>
            {
                result = _engine.CleanCategories(_categories, options, _cts.Token);
            });

            _btnCancel.Visibility = Visibility.Collapsed;
            SetControlsBusyState(false);

            if (_cts != null && _cts.IsCancellationRequested)
            {
                StopCleaningAnimation(false, "");
                _heroTitleText.Text = "LIMPIEZA CANCELADA";
                _heroSubText.Text = "La operación fue detenida por el usuario.";
                _heroSizeText.Text = string.Format("Se alcanzaron a liberar {0}.", DiskHelper.FormatBytes(result != null ? result.TotalBytesFreed : 0));
            }
            else
            {
                StopCleaningAnimation(true, result != null ? result.FormattedBytesFreed : "0 B");
            }

            foreach (var cat in _categories)
            {
                if (_categorySizeLabels.ContainsKey(cat.Id))
                {
                    _categorySizeLabels[cat.Id].Text = cat.StatusText;
                    _categorySizeLabels[cat.Id].Foreground = _colorSand;
                }
            }
            UpdateDriveSpaceDisplay();
            CheckActiveProcessConflicts();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _currentFileText.Text = "Cancelando operación...";
            }
        }

        private void SetControlsBusyState(bool busy)
        {
            _btnScan.IsEnabled = !busy;
            _btnClean.IsEnabled = !busy;
            _btnSelectAll.IsEnabled = !busy;
            foreach (var chk in _categoryCheckBoxes.Values)
            {
                chk.IsEnabled = !busy;
            }
        }

        private void Engine_ProgressChanged(ProgressUpdate update)
        {
            Dispatcher.Invoke(() =>
            {
                _progressBar.Value = update.Percentage;
                _progressPctText.Text = string.Format("{0:0}%", update.Percentage);
                _heroSubText.Text = string.Format("Limpiando: {0}", update.CurrentCategoryName);
                _heroSizeText.Text = string.Format("Liberados: {0} ({1} archivos eliminados)", update.FormattedBytesCleaned, update.ItemsCleanedSoFar);
                _currentFileText.Text = update.CurrentItemPath;

                if (!string.IsNullOrEmpty(update.CategoryId))
                {
                    if (_activeCategoryId != update.CategoryId)
                    {
                        if (!string.IsNullOrEmpty(_activeCategoryId) && _categoryCards.ContainsKey(_activeCategoryId))
                        {
                            _categoryCards[_activeCategoryId].BorderBrush = _borderBrush;
                        }
                        _activeCategoryId = update.CategoryId;
                        if (_categoryCards.ContainsKey(_activeCategoryId))
                        {
                            _categoryCards[_activeCategoryId].BorderBrush = _colorSand;
                        }
                    }
                }
            });
        }

        private void Engine_LogMessage(string logMsg)
        {
            Dispatcher.Invoke(() =>
            {
                if (_txtLog.Text.Length > 25000)
                {
                    _txtLog.Text = _txtLog.Text.Substring(10000);
                }
                _txtLog.AppendText(logMsg + Environment.NewLine);
                _txtLog.ScrollToEnd();
            });
        }
    }

    // ==========================================
    // VENTANA MODAL: EXPLORADOR DE ARCHIVOS (Cyprus & Sand)
    // ==========================================
    public class FileInspectorWindow : Window
    {
        private CleanCategoryItem _category;
        private CleanerEngine _engine;
        private StackPanel _itemsListPanel;
        private TextBlock _summaryText;
        private ProgressBar _loadingBar;

        private readonly SolidColorBrush _colorCyprus = new SolidColorBrush(Color.FromRgb(0, 71, 65));
        private readonly SolidColorBrush _colorSand = new SolidColorBrush(Color.FromRgb(240, 237, 228));
        private readonly SolidColorBrush _cardBg = new SolidColorBrush(Color.FromRgb(6, 44, 40));
        private readonly SolidColorBrush _borderBrush = new SolidColorBrush(Color.FromRgb(18, 77, 71));

        public FileInspectorWindow(CleanCategoryItem cat, CleanerEngine engine)
        {
            _category = cat;
            _engine = engine;
            InitializeInspector();
            LoadFilesAsync();
        }

        private void InitializeInspector()
        {
            Title = string.Format("Explorador de Archivos - {0}", _category.Name);
            Width = 780;
            Height = 540;
            MinWidth = 650;
            MinHeight = 450;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(Color.FromRgb(3, 24, 22));
            Foreground = _colorSand;
            FontFamily = new FontFamily("Segoe UI, Arial");

            Grid root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(50) });

            // Header
            Border header = new Border
            {
                Background = _cardBg,
                BorderBrush = _borderBrush,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(20, 10, 20, 10)
            };
            StackPanel headerStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            TextBlock iconBlock = new TextBlock { Text = _category.IconSymbol, FontSize = 22, Margin = new Thickness(0, 0, 12, 0) };
            StackPanel textStack = new StackPanel();
            TextBlock titleBlock = new TextBlock { Text = _category.Name, FontSize = 15, FontWeight = FontWeights.Bold, Foreground = _colorSand };
            _summaryText = new TextBlock { Text = "Escaneando archivos más pesados...", FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(172, 194, 187)) };
            textStack.Children.Add(titleBlock);
            textStack.Children.Add(_summaryText);
            headerStack.Children.Add(iconBlock);
            headerStack.Children.Add(textStack);
            header.Child = headerStack;
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            // Loading Bar
            _loadingBar = new ProgressBar
            {
                Height = 3,
                IsIndeterminate = true,
                Foreground = _colorSand,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0)
            };
            Grid.SetRow(_loadingBar, 1);
            root.Children.Add(_loadingBar);

            // Scrollable List
            ScrollViewer scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(20, 12, 20, 12)
            };
            _itemsListPanel = new StackPanel();
            scroll.Content = _itemsListPanel;
            Grid.SetRow(scroll, 2);
            root.Children.Add(scroll);

            // Footer
            Border footer = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(2, 22, 20)),
                BorderBrush = _borderBrush,
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(20, 8, 20, 8)
            };
            Grid footerGrid = new Grid();
            Button btnClose = new Button
            {
                Content = "Cerrar",
                Width = 100,
                Height = 32,
                Background = _colorSand,
                Foreground = _colorCyprus,
                FontWeight = FontWeights.Bold,
                BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Right,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnClose.Click += (s, e) => Close();
            footerGrid.Children.Add(btnClose);
            footer.Child = footerGrid;
            Grid.SetRow(footer, 3);
            root.Children.Add(footer);

            Content = root;
        }

        private async void LoadFilesAsync()
        {
            List<FileDetailItem> files = null;
            await Task.Factory.StartNew(() =>
            {
                files = _engine.GetTopFilesForCategory(_category, 100);
            });

            _loadingBar.Visibility = Visibility.Collapsed;

            if (files == null || files.Count == 0)
            {
                _summaryText.Text = "No se encontraron archivos en esta categoría (está limpia o vacía).";
                TextBlock emptyMsg = new TextBlock
                {
                    Text = "✓ Esta carpeta no contiene archivos temporales actualmente.",
                    FontSize = 13,
                    Foreground = _colorSand,
                    Margin = new Thickness(0, 20, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                _itemsListPanel.Children.Add(emptyMsg);
                return;
            }

            long totalBytes = 0;
            foreach (var f in files) totalBytes += f.SizeBytes;
            _summaryText.Text = string.Format("Mostrando los {0} archivos más pesados ({1} en total):", files.Count, DiskHelper.FormatBytes(totalBytes));

            foreach (var file in files)
            {
                Border card = new Border
                {
                    Background = _cardBg,
                    BorderBrush = _borderBrush,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(12, 8, 12, 8),
                    Margin = new Thickness(0, 0, 0, 6)
                };

                Grid g = new Grid();
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                StackPanel infoStack = new StackPanel();
                TextBlock nameBlock = new TextBlock
                {
                    Text = file.Name,
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 12,
                    Foreground = _colorSand,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                TextBlock pathBlock = new TextBlock
                {
                    Text = file.FullPath,
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(172, 194, 187)),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    ToolTip = file.FullPath
                };
                infoStack.Children.Add(nameBlock);
                infoStack.Children.Add(pathBlock);
                Grid.SetColumn(infoStack, 0);
                g.Children.Add(infoStack);

                // Size Badge in Sand with Cyprus text
                Border sizeBadge = new Border
                {
                    Background = _colorSand,
                    BorderBrush = _colorSand,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(8, 2, 8, 2),
                    Margin = new Thickness(10, 0, 10, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                TextBlock sizeText = new TextBlock
                {
                    Text = file.FormattedSize,
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = _colorCyprus
                };
                sizeBadge.Child = sizeText;
                Grid.SetColumn(sizeBadge, 1);
                g.Children.Add(sizeBadge);

                // Open in Explorer button
                Button btnOpen = new Button
                {
                    Content = "📂 Abrir Carpeta",
                    Padding = new Thickness(8, 3, 8, 3),
                    Background = _colorCyprus,
                    BorderBrush = _borderBrush,
                    Foreground = _colorSand,
                    FontSize = 10,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    VerticalAlignment = VerticalAlignment.Center
                };
                string targetFile = file.FullPath;
                btnOpen.Click += (s, e) =>
                {
                    try
                    {
                        if (File.Exists(targetFile))
                        {
                            Process.Start("explorer.exe", string.Format("/select,\"{0}\"", targetFile));
                        }
                        else
                        {
                            string dir = System.IO.Path.GetDirectoryName(targetFile);
                            if (Directory.Exists(dir)) Process.Start("explorer.exe", dir);
                        }
                    }
                    catch { }
                };
                Grid.SetColumn(btnOpen, 2);
                g.Children.Add(btnOpen);

                card.Child = g;
                _itemsListPanel.Children.Add(card);
            }
        }
    }
}
