using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using ProxyShellReady.Models;
using ProxyShellReady.Services;

namespace ProxyShellReady
{
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<AppEntry> _selectedApps = new ObservableCollection<AppEntry>();
        private readonly ObservableCollection<RuleFileItem> _ruleFiles = new ObservableCollection<RuleFileItem>();
        private readonly DispatcherTimer _connectionTimer = new DispatcherTimer();
        private readonly SingBoxRunner _runner = new SingBoxRunner();

        private AppState _state = new AppState();
        private ContentTab _currentTab = ContentTab.Rules;
        private DateTime _connectedAt;
        private bool _isConnected;
        private bool _isBusy;
        private bool _settingsVisible;

        public MainWindow()
        {
            InitializeComponent();

            StateChanged += MainWindow_StateChanged;
            Closing += MainWindow_Closing;
            _connectionTimer.Interval = TimeSpan.FromSeconds(1);
            _connectionTimer.Tick += ConnectionTimer_Tick;

            AppsList.ItemsSource = _selectedApps;
            RulesListBox.ItemsSource = _ruleFiles;
            RuleFilesSettingsListBox.ItemsSource = _ruleFiles;

            _state = StateStore.Load();
            LoadStateIntoCollections();
            LoadStateIntoSettingsUi();
            ApplyTheme(_state.Theme);
            InitializeUiState();
            UpdateBottomContent();
            UpdateAppsEmptyState();
            UpdateRuleFilesEmptyState();
            UpdateThemeSelectionUi();
            AppendLog("Приложение запущено.");
        }

        private void LoadStateIntoCollections()
        {
            _selectedApps.Clear();
            foreach (AppEntry app in _state.SelectedApps)
            {
                if (string.IsNullOrWhiteSpace(app.FullPath))
                    continue;
                _selectedApps.Add(new AppEntry
                {
                    Name = string.IsNullOrWhiteSpace(app.Name) ? Path.GetFileName(app.FullPath) : app.Name,
                    FullPath = app.FullPath,
                    IsEnabled = app.IsEnabled
                });
            }

            _ruleFiles.Clear();
            foreach (RuleFileItem file in _state.RuleFiles)
            {
                RuleFileItem item = new RuleFileItem
                {
                    Name = file.Name,
                    FullPath = file.FullPath,
                    IsEnabled = file.IsEnabled,
                    RoutingMode = file.RoutingMode
                };
                ReloadRuleFile(item, false);
                _ruleFiles.Add(item);
            }
        }

        private void LoadStateIntoSettingsUi()
        {
            SingBoxPathTextBox.Text = _state.SingBoxPath;
            SocksHostTextBox.Text = _state.SocksHost;
            SocksPortTextBox.Text = _state.SocksPort.ToString();
            SocksUserTextBox.Text = _state.SocksUsername;
            SocksPasswordTextBox.Text = _state.SocksPassword;

            foreach (ComboBoxItem item in LocalModeComboBox.Items)
            {
                if ((string)item.Tag == _state.LocalMode.ToString())
                {
                    LocalModeComboBox.SelectedItem = item;
                    break;
                }
            }
            if (LocalModeComboBox.SelectedIndex < 0)
                LocalModeComboBox.SelectedIndex = 0;
        }

        private void InitializeUiState()
        {
            _isConnected = false;
            _isBusy = false;
            _settingsVisible = false;
            MainPage.Visibility = Visibility.Visible;
            SettingsPage.Visibility = Visibility.Collapsed;
            TimerTextBlock.Text = "00:00:00";
            StatusTextBlock.Text = "СТАТУС: НЕ ПОДКЛЮЧЕНО";
            StatusDot.Fill = CreateBrushFromHex(GetBrushColorHex("OffBrush"));
            PowerButton.ToolTip = "Подключить";
            _currentTab = _state.ConnectionMode == ConnectionMode.AppSelection ? ContentTab.Apps : ContentTab.Rules;
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            CaptureSettingsFromUi();
            SaveState();
            _connectionTimer.Stop();
            if (_runner.IsRunning)
                _runner.Stop(AppendLog);
        }

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                RootBorder.Margin = new Thickness(0);
                RootBorder.CornerRadius = new CornerRadius(0);
            }
            else
            {
                RootBorder.Margin = new Thickness(10);
                RootBorder.CornerRadius = new CornerRadius(28);
            }
        }

        private void TopBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
                return;
            }

            DragMove();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            _settingsVisible = !_settingsVisible;
            MainPage.Visibility = _settingsVisible ? Visibility.Collapsed : Visibility.Visible;
            SettingsPage.Visibility = _settingsVisible ? Visibility.Visible : Visibility.Collapsed;
            if (!_settingsVisible)
                CaptureSettingsFromUi();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void PowerButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isBusy)
                return;

            _isBusy = true;
            PowerButton.IsEnabled = false;

            try
            {
                if (!_isConnected)
                    await ConnectAsync();
                else
                    await DisconnectAsync();
            }
            catch (Exception ex)
            {
                _connectionTimer.Stop();
                _isConnected = false;
                TimerTextBlock.Text = "00:00:00";
                StatusTextBlock.Text = "СТАТУС: ОШИБКА ПОДКЛЮЧЕНИЯ";
                StatusDot.Fill = CreateBrushFromHex(GetBrushColorHex("DangerBrush"));
                PowerButton.ToolTip = "Подключить";
                AppendLog("Ошибка подключения: " + ex.Message);
                MessageBox.Show("Ошибка при переключении подключения:\n" + ex.Message,
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                _isBusy = false;
                PowerButton.IsEnabled = true;
            }
        }

        private async Task ConnectAsync()
        {
            CaptureSettingsFromUi();
            SaveState();

            if (string.IsNullOrWhiteSpace(_state.SingBoxPath) || !File.Exists(_state.SingBoxPath))
                throw new InvalidOperationException("Укажи корректный путь к sing-box.exe в настройках.");

            if (string.IsNullOrWhiteSpace(_state.SocksHost) || _state.SocksPort <= 0)
                throw new InvalidOperationException("Укажи корректный SOCKS5 сервер и порт.");

            if (_state.LocalMode == LocalProxyMode.Tun && !IsAdministrator())
                throw new InvalidOperationException("Для TUN режима приложение нужно запускать от имени администратора.");

            StatusTextBlock.Text = "СТАТУС: ПОДКЛЮЧЕНИЕ...";
            StatusDot.Fill = CreateBrushFromHex(GetBrushColorHex("WarningBrush"));
            PowerButton.ToolTip = "Подключение...";
            AppendLog("Формирование конфигурации sing-box...");

            string configJson = SingBoxConfigBuilder.Build(_state, _ruleFiles.ToList(), _selectedApps.ToList());
            string configPath = await _runner.StartAsync(_state.SingBoxPath, configJson, AppendLog);

            _connectedAt = DateTime.Now;
            _isConnected = true;
            _connectionTimer.Start();
            UpdateTimerText();

            AppendLog("Использован конфиг: " + configPath);
            AppendLog("Подключение запущено в режиме: " + (_state.LocalMode == LocalProxyMode.Tun ? "TUN" : "System Proxy"));

            bool verified = await VerifyConnectionAsync();
            if (verified)
            {
                StatusTextBlock.Text = "СТАТУС: ПОДКЛЮЧЕНО";
                StatusDot.Fill = CreateBrushFromHex(GetBrushColorHex("SuccessBrush"));
                AppendLog("Проверка подключения пройдена.");
            }
            else
            {
                StatusTextBlock.Text = "СТАТУС: ЗАПУЩЕНО, НО НЕ ПРОВЕРЕНО";
                StatusDot.Fill = CreateBrushFromHex(GetBrushColorHex("WarningBrush"));
                AppendLog("Подключение запущено, но проверка не подтвердила работу локальной точки.");
            }

            PowerButton.ToolTip = "Отключить";
        }

        private async Task<bool> VerifyConnectionAsync()
        {
            await Task.Delay(1200);
            if (!_runner.IsRunning)
                return false;

            if (_state.LocalMode == LocalProxyMode.MixedSystemProxy)
            {
                try
                {
                    using (TcpClient client = new TcpClient())
                    {
                        Task connectTask = client.ConnectAsync("127.0.0.1", 2080);
                        Task timeoutTask = Task.Delay(1500);
                        Task completed = await Task.WhenAny(connectTask, timeoutTask);
                        if (completed == connectTask && client.Connected)
                        {
                            client.Close();
                            AppendLog("Локальный mixed proxy отвечает на 127.0.0.1:2080.");
                            return true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    AppendLog("Проверка mixed proxy не пройдена: " + ex.Message);
                }
                return false;
            }

            AppendLog("Процесс sing-box активен, TUN режим запущен.");
            return true;
        }

        private Task DisconnectAsync()
        {
            StatusTextBlock.Text = "СТАТУС: ОТКЛЮЧЕНИЕ...";
            StatusDot.Fill = CreateBrushFromHex(GetBrushColorHex("WarningBrush"));
            PowerButton.ToolTip = "Отключение...";

            return Task.Run(delegate
            {
                _runner.Stop(AppendLog);
                Dispatcher.Invoke(delegate
                {
                    _connectionTimer.Stop();
                    _isConnected = false;
                    TimerTextBlock.Text = "00:00:00";
                    StatusTextBlock.Text = "СТАТУС: НЕ ПОДКЛЮЧЕНО";
                    StatusDot.Fill = CreateBrushFromHex(GetBrushColorHex("OffBrush"));
                    PowerButton.ToolTip = "Подключить";
                });
            });
        }

        private void ConnectionTimer_Tick(object sender, EventArgs e)
        {
            if (_isConnected)
                UpdateTimerText();
        }

        private void UpdateTimerText()
        {
            TimeSpan elapsed = DateTime.Now - _connectedAt;
            TimerTextBlock.Text = elapsed.ToString(@"hh\:mm\:ss");
        }

        private void WholeComputerButton_Click(object sender, RoutedEventArgs e)
        {
            _state.ConnectionMode = ConnectionMode.WholeComputer;
            _currentTab = ContentTab.Rules;
            UpdateBottomContent();
            SaveState();
        }

        private void AppSelectionButton_Click(object sender, RoutedEventArgs e)
        {
            _state.ConnectionMode = ConnectionMode.AppSelection;
            _currentTab = ContentTab.Apps;
            UpdateBottomContent();
            SaveState();
        }

        private void RulesTabButton_Click(object sender, RoutedEventArgs e)
        {
            _currentTab = ContentTab.Rules;
            UpdateBottomContent();
        }

        private void AppsTabButton_Click(object sender, RoutedEventArgs e)
        {
            if (_state.ConnectionMode == ConnectionMode.AppSelection)
            {
                _currentTab = ContentTab.Apps;
                UpdateBottomContent();
            }
        }

        private void BottomActionButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentTab == ContentTab.Rules)
            {
                AddRuleFile();
                return;
            }

            AddApplications();
        }

        private void AddApplications()
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Title = "Выбор приложений";
            dialog.Filter = "Приложения (*.exe)|*.exe";
            dialog.Multiselect = true;

            if (dialog.ShowDialog() != true)
                return;

            foreach (string filePath in dialog.FileNames)
            {
                if (!File.Exists(filePath))
                    continue;

                bool alreadyExists = _selectedApps.Any(delegate(AppEntry app)
                {
                    return string.Equals(app.FullPath, filePath, StringComparison.OrdinalIgnoreCase);
                });

                if (alreadyExists)
                    continue;

                _selectedApps.Add(new AppEntry
                {
                    Name = Path.GetFileName(filePath),
                    FullPath = filePath,
                    IsEnabled = true
                });

                AppendLog("Добавлено приложение: " + Path.GetFileName(filePath));
            }

            UpdateAppsEmptyState();
            SaveState();
        }

        private void RemoveAppButton_Click(object sender, RoutedEventArgs e)
        {
            FrameworkElement element = sender as FrameworkElement;
            if (element == null)
                return;

            AppEntry app = element.Tag as AppEntry;
            if (app == null)
                return;

            _selectedApps.Remove(app);
            AppendLog("Удалено приложение: " + app.Name);
            UpdateAppsEmptyState();
            SaveState();
        }

        private void AddRuleFileButton_Click(object sender, RoutedEventArgs e)
        {
            AddRuleFile();
        }

        private void AddRuleFile()
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Title = "Выбор файла правил";
            dialog.Filter = "Текстовые и JSON файлы|*.txt;*.json|Все файлы (*.*)|*.*";
            dialog.Multiselect = true;

            if (dialog.ShowDialog() != true)
                return;

            foreach (string filePath in dialog.FileNames)
            {
                if (!File.Exists(filePath))
                    continue;

                bool alreadyExists = _ruleFiles.Any(delegate(RuleFileItem file)
                {
                    return string.Equals(file.FullPath, filePath, StringComparison.OrdinalIgnoreCase);
                });

                if (alreadyExists)
                    continue;

                RuleFileItem item = new RuleFileItem
                {
                    Name = Path.GetFileName(filePath),
                    FullPath = filePath,
                    IsEnabled = true,
                    RoutingMode = RuleRoutingDetector.Detect(filePath)
                };
                ReloadRuleFile(item, true);
                _ruleFiles.Add(item);
                AppendLog("Добавлен русет: " + item.Name + " [" + item.RoutingMode + "]");
            }

            UpdateRuleFilesEmptyState();
            SaveState();
        }

        private void ReloadRuleFile(RuleFileItem item, bool log)
        {
            item.Entries = RuleParser.ParseFile(item.FullPath);
            item.EntryCount = item.Entries.Count;
            item.Name = string.IsNullOrWhiteSpace(item.Name) ? Path.GetFileName(item.FullPath) : item.Name;
            if (log)
                AppendLog("Загружен файл правил: " + item.Name + " (" + item.EntryCount + " записей, режим " + item.RoutingMode + ")");
        }

        private void RemoveRuleFileButton_Click(object sender, RoutedEventArgs e)
        {
            FrameworkElement element = sender as FrameworkElement;
            if (element == null)
                return;

            RuleFileItem item = element.Tag as RuleFileItem;
            if (item == null)
                return;

            _ruleFiles.Remove(item);
            AppendLog("Удалён русет: " + item.Name);
            UpdateRuleFilesEmptyState();
            SaveState();
        }

        private void UpdateAppsEmptyState()
        {
            EmptyAppsText.Visibility = _selectedApps.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateRuleFilesEmptyState()
        {
            Visibility visible = _ruleFiles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            EmptyRulesText.Visibility = visible;
            SettingsEmptyRulesText.Visibility = visible;
        }

        private void UpdateBottomContent()
        {
            bool appMode = _state.ConnectionMode == ConnectionMode.AppSelection;
            bool rulesTab = _currentTab == ContentTab.Rules;
            bool appsTab = _currentTab == ContentTab.Apps;

            AppsTabButton.Visibility = appMode ? Visibility.Visible : Visibility.Collapsed;
            RulesUnderline.Visibility = rulesTab ? Visibility.Visible : Visibility.Collapsed;
            AppsUnderline.Visibility = appMode && appsTab ? Visibility.Visible : Visibility.Collapsed;
            RulesPanel.Visibility = rulesTab ? Visibility.Visible : Visibility.Collapsed;
            AppsPanel.Visibility = appMode && appsTab ? Visibility.Visible : Visibility.Collapsed;
            RulesTabText.Foreground = rulesTab ? CreateBrushFromHex(GetBrushColorHex("PrimaryTextBrush")) : CreateBrushFromHex(GetBrushColorHex("SecondaryTextBrush"));
            AppsTabText.Foreground = appMode && appsTab ? CreateBrushFromHex(GetBrushColorHex("PrimaryTextBrush")) : CreateBrushFromHex(GetBrushColorHex("SecondaryTextBrush"));
            BottomActionText.Text = appsTab ? "Добавить приложение" : "Добавить файл правил";
        }

        private void BrowseSingBoxButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Title = "Выбор sing-box.exe";
            dialog.Filter = "Исполняемый файл sing-box|sing-box.exe|Исполняемые файлы|*.exe|Все файлы (*.*)|*.*";
            dialog.Multiselect = false;
            if (dialog.ShowDialog() == true)
            {
                SingBoxPathTextBox.Text = dialog.FileName;
                CaptureSettingsFromUi();
                SaveState();
                AppendLog("Путь к sing-box обновлён.");
            }
        }

        private void LocalModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CaptureSettingsFromUi();
            SaveState();
        }

        private void ClearLogButton_Click(object sender, RoutedEventArgs e)
        {
            LogsTextBox.Clear();
        }

        private void DarkThemeButton_Click(object sender, RoutedEventArgs e)
        {
            _state.Theme = AppThemeMode.Dark;
            ApplyTheme(_state.Theme);
            UpdateThemeSelectionUi();
            SaveState();
            AppendLog("Выбрана тёмная тема.");
        }

        private void LightThemeButton_Click(object sender, RoutedEventArgs e)
        {
            _state.Theme = AppThemeMode.Light;
            ApplyTheme(_state.Theme);
            UpdateThemeSelectionUi();
            SaveState();
            AppendLog("Выбрана светлая тема.");
        }

        private void UpdateThemeSelectionUi()
        {
            bool isDark = _state.Theme == AppThemeMode.Dark;
            DarkThemeButton.BorderBrush = isDark ? CreateBrushFromHex("#5A88FF") : CreateBrushFromHex("#2D3647");
            DarkThemeButton.BorderThickness = isDark ? new Thickness(2) : new Thickness(1);
            LightThemeButton.BorderBrush = !isDark ? CreateBrushFromHex("#5A88FF") : CreateBrushFromHex("#2D3647");
            LightThemeButton.BorderThickness = !isDark ? new Thickness(2) : new Thickness(1);
            DarkThemeMarker.Visibility = isDark ? Visibility.Visible : Visibility.Collapsed;
            LightThemeMarker.Visibility = !isDark ? Visibility.Visible : Visibility.Collapsed;
        }

        private void CaptureSettingsFromUi()
        {
            _state.SingBoxPath = SingBoxPathTextBox.Text != null ? SingBoxPathTextBox.Text.Trim() : string.Empty;
            _state.SocksHost = SocksHostTextBox.Text != null ? SocksHostTextBox.Text.Trim() : string.Empty;

            int port;
            if (int.TryParse(SocksPortTextBox.Text, out port) && port > 0)
                _state.SocksPort = port;

            _state.SocksUsername = SocksUserTextBox.Text != null ? SocksUserTextBox.Text.Trim() : string.Empty;
            _state.SocksPassword = SocksPasswordTextBox.Text != null ? SocksPasswordTextBox.Text : string.Empty;

            ComboBoxItem item = LocalModeComboBox.SelectedItem as ComboBoxItem;
            if (item != null)
            {
                string tag = item.Tag as string;
                if (tag == LocalProxyMode.Tun.ToString())
                    _state.LocalMode = LocalProxyMode.Tun;
                else
                    _state.LocalMode = LocalProxyMode.MixedSystemProxy;
            }
        }

        private void SaveState()
        {
            _state.SelectedApps = _selectedApps.Select(delegate(AppEntry app)
            {
                return new AppEntry
                {
                    Name = app.Name,
                    FullPath = app.FullPath,
                    IsEnabled = app.IsEnabled
                };
            }).ToList();

            _state.RuleFiles = _ruleFiles.Select(delegate(RuleFileItem file)
            {
                return new RuleFileItem
                {
                    Name = file.Name,
                    FullPath = file.FullPath,
                    IsEnabled = file.IsEnabled,
                    EntryCount = file.EntryCount,
                    RoutingMode = file.RoutingMode
                };
            }).ToList();

            StateStore.Save(_state);
        }

        private void AppendLog(string message)
        {
            string line = "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message;
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(delegate { AppendLog(message); });
                return;
            }
            if (LogsTextBox.Text.Length > 0)
                LogsTextBox.AppendText(Environment.NewLine);
            LogsTextBox.AppendText(line);
            LogsTextBox.ScrollToEnd();
        }

        private bool IsAdministrator()
        {
            try
            {
                WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        private void ApplyTheme(AppThemeMode theme)
        {
            if (theme == AppThemeMode.Light)
            {
                SetBrush("PrimaryTextBrush", "#10131A");
                SetBrush("SecondaryTextBrush", "#6F7787");
                SetBrush("AccentSoftBrush", "#566074");
                SetBrush("ShellBorderBrush", "#D6DCE7");
                SetBrush("OffBrush", "#A1A7B3");
                SetGradient("ShellBackgroundBrush", "#F4F6FA", "#EEF3FA", "#F8FAFD");
                SetGradient("CardBrush", "#FFFFFF", "#F6F8FC");
                SetGradient("InnerCardBrush", "#FFFFFF", "#F8FAFD");
            }
            else
            {
                SetBrush("PrimaryTextBrush", "#FFFFFF");
                SetBrush("SecondaryTextBrush", "#8E96A7");
                SetBrush("AccentSoftBrush", "#C9D1DD");
                SetBrush("ShellBorderBrush", "#2B3140");
                SetBrush("OffBrush", "#6E7583");
                SetGradient("ShellBackgroundBrush", "#1E222D", "#232938", "#202636");
                SetGradient("CardBrush", "#17202F", "#121A28");
                SetGradient("InnerCardBrush", "#131C2A", "#0F1724");
            }
        }

        private void SetBrush(string key, string hex)
        {
            Resources[key] = CreateBrushFromHex(hex);
        }

        private void SetGradient(string key, string c1, string c2)
        {
            LinearGradientBrush brush = new LinearGradientBrush();
            brush.StartPoint = new Point(0, 0);
            brush.EndPoint = new Point(1, 1);
            brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(c1), 0));
            brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(c2), 1));
            Resources[key] = brush;
        }

        private void SetGradient(string key, string c1, string cMid, string c2)
        {
            LinearGradientBrush brush = new LinearGradientBrush();
            brush.StartPoint = new Point(0, 0);
            brush.EndPoint = new Point(1, 0);
            brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(c1), 0));
            brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(cMid), 0.45));
            brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(c2), 1));
            Resources[key] = brush;
        }

        private SolidColorBrush CreateBrushFromHex(string hex)
        {
            return (SolidColorBrush)(new BrushConverter().ConvertFrom(hex));
        }

        private string GetBrushColorHex(string resourceKey)
        {
            object value = Resources[resourceKey];
            SolidColorBrush brush = value as SolidColorBrush;
            if (brush == null)
                return "#FFFFFF";
            return brush.Color.ToString();
        }
    }
}
