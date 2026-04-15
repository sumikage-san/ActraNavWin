using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Net.Http;
using Microsoft.Web.WebView2.Core;

namespace ActraNavWin
{
    public partial class MainWindow : Window
    {
        private static readonly string ConfigPath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

        private static readonly JsonSerializerOptions JsonReadOpts = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private static readonly JsonSerializerOptions JsonWriteOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        private const string ConnectionErrorMessage =
            "サーバーに接続できません。\n\n" +
            "・ネットワーク接続を確認してください\n" +
            "・設定内容（IP）を確認してください";

        private AppConfig _config = new();
        private ActraApiClient? _api;
        private CancellationTokenSource? _monitorCts;
        private Microsoft.Web.WebView2.Wpf.WebView2? _webView;
        private bool _webViewReady;
        private bool _autoLoginDone;

        private enum ConnState { Normal, Warning, Error }

        private const double TabWidth = 30;
        private double _lastLeft = -1;
        private double _lastTop = -1;
        private bool _isClamping;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
            LocationChanged += (s, e) => ClampToScreen();
        }

        // ========================================================
        // 起動
        // ========================================================

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _config = LoadConfig();

                if (_config.IsInitialized && _config.Qr != null)
                {
                    ApplyConfigAndShowLogin();
                }
                else
                {
                    ShowQrSetupView();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初期化エラー:\n{ex.Message}", "ActraNavWin",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ========================================================
        // QrSetupView
        // ========================================================

        // MainView から遷移してきた場合 true。戻るボタンの表示制御に使用。
        private bool _cameFromMainView;

        private void ShowQrSetupView(bool fromMainView = false)
        {
            _cameFromMainView = fromMainView;

            QrSetupView.Visibility = Visibility.Visible;
            LoginView.Visibility = Visibility.Collapsed;
            MainView.Visibility = Visibility.Collapsed;

            btnQrBack.Visibility = fromMainView ? Visibility.Visible : Visibility.Collapsed;

            HideQrError();
            btnQrApply.IsEnabled = true;
            txtQrJson.Focus();
        }

        private void BtnQrBack_Click(object sender, RoutedEventArgs e)
        {
            QrSetupView.Visibility = Visibility.Collapsed;
            MainView.Visibility = Visibility.Visible;
            if (_webView != null) _webView.Visibility = Visibility.Visible;
        }

        private async void BtnQrApply_Click(object sender, RoutedEventArgs e)
        {
            HideQrError();

            var input = txtQrJson.Text.Trim();
            if (string.IsNullOrEmpty(input))
            {
                ShowQrError("JSONを入力してください。");
                return;
            }

            QrConfig qr;
            try
            {
                qr = ParseQr(input);
            }
            catch (Exception ex)
            {
                ShowQrError(ex.Message);
                return;
            }

            // 接続テスト
            var testApi = new ActraApiClient(
                $"{qr.Protocol}://{qr.Ip}/company-wide/html/Actra");

            btnQrApply.IsEnabled = false;
            try
            {
                await testApi.TestConnectionAsync();
            }
            catch (TaskCanceledException)
            {
                ShowQrError(ConnectionErrorMessage);
                btnQrApply.IsEnabled = true;
                return;
            }
            catch (HttpRequestException)
            {
                ShowQrError(ConnectionErrorMessage);
                btnQrApply.IsEnabled = true;
                return;
            }
            catch (Exception ex)
            {
                ShowQrError($"接続エラー: {ex.Message}");
                btnQrApply.IsEnabled = true;
                return;
            }

            // 保存
            _config.Qr = qr;
            _config.IsInitialized = true;
            _config.ApplyQrConfig();
            SaveConfig(_config);

            btnQrApply.IsEnabled = true;
            ApplyConfigAndShowLogin();
        }

        private void ShowQrError(string msg)
        {
            txtQrError.Text = msg;
            txtQrError.Visibility = Visibility.Visible;
        }

        private void HideQrError()
        {
            txtQrError.Text = "";
            txtQrError.Visibility = Visibility.Collapsed;
        }

        // ========================================================
        // 設定適用 → LoginView 表示
        // ========================================================

        private void ApplyConfigAndShowLogin()
        {
            _config.ApplyQrConfig();
            _api = new ActraApiClient(_config.ApiBaseUrl);
            PopulateUrlComboBox();
            UpdateInfoLabels();

            QrSetupView.Visibility = Visibility.Collapsed;
            MainView.Visibility = Visibility.Collapsed;
            LoginView.Visibility = Visibility.Visible;

            txtStaffCode.Text = "";
            HideLoginError();
            txtStaffCode.Focus();
        }

        private void UpdateInfoLabels()
        {
            if (_config.Qr != null)
            {
                var info = $"{_config.Qr.Ip}（{_config.Qr.Location}）";
                txtQrInfo.Text = info;
                txtLoginConnInfo.Text = $"接続先: {info}";
            }
            else
            {
                txtQrInfo.Text = "";
                txtLoginConnInfo.Text = "";
            }
        }

        // ========================================================
        // 設定リセット（⚙ ボタン）
        // ========================================================

        private void BtnResetConfig_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "設定画面に移動しますか？",
                "確認",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            if (_webView != null) _webView.Visibility = Visibility.Collapsed;
            ShowQrSetupView(fromMainView: true);
        }

        // ========================================================
        // WebView2 初期化
        // ========================================================

        private async Task InitializeWebViewAsync()
        {
            _webView = new Microsoft.Web.WebView2.Wpf.WebView2();
            _webView.DefaultBackgroundColor = System.Drawing.Color.White;
            webViewHost.Child = _webView;

            var options = new CoreWebView2EnvironmentOptions(
                "--disable-features=msWebOOUI,msPdfOOUI,msEdgeDevToolsUI");

            var env = await CoreWebView2Environment.CreateAsync(null, null, options);
            await _webView.EnsureCoreWebView2Async(env);

            _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            _webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
            _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;

            _webView.CoreWebView2.NavigationCompleted += WebView_NavigationCompleted;
            _webViewReady = true;
        }

        // ========================================================
        // LoginView
        // ========================================================

        private void TxtStaffCode_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) BtnLogin_Click(sender, e);
        }

        private async void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            HideLoginError();

            var input = txtStaffCode.Text.Trim();
            if (!Regex.IsMatch(input, @"^[a-zA-Z0-9]{3,10}$"))
            {
                ShowLoginError("社員番号は3〜10文字の英数字で入力してください。");
                return;
            }

            if (_api == null)
            {
                ShowLoginError("API クライアント未初期化です。");
                return;
            }

            btnLogin.IsEnabled = false;
            try
            {
                var info = await _api.GetStaffInfoAsync(input);
                AppSession.CurrentUser = info;
                SwitchToMainView();
            }
            catch (TaskCanceledException)
            {
                ShowLoginError(ConnectionErrorMessage);
            }
            catch (HttpRequestException)
            {
                ShowLoginError(ConnectionErrorMessage);
            }
            catch (Exception ex)
            {
                ShowLoginError(ex.Message);
            }
            finally
            {
                btnLogin.IsEnabled = true;
            }
        }

        private void ShowLoginError(string msg)
        {
            txtLoginError.Text = msg;
            txtLoginError.Visibility = Visibility.Visible;
        }

        private void HideLoginError()
        {
            txtLoginError.Text = "";
            txtLoginError.Visibility = Visibility.Collapsed;
        }

        // ========================================================
        // ビュー切替
        // ========================================================

        private async void SwitchToMainView()
        {
            var user = AppSession.CurrentUser;
            if (user == null) return;

            txtUser.Text = $"{user.StaffName}（{user.StaffCode}）";

            LoginView.Visibility = Visibility.Collapsed;
            MainView.Visibility = Visibility.Visible;

            if (!_webViewReady)
            {
                await InitializeWebViewAsync();
            }

            if (_webView != null) _webView.Visibility = Visibility.Visible;

            _autoLoginDone = false;
            StartConnectionMonitor();

            Dispatcher.InvokeAsync(() =>
            {
                ForceNavigateCurrentSelection();
            }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        private void ForceNavigateCurrentSelection()
        {
            if (!_webViewReady) return;

            var selected = cmbUrl.SelectedItem as UrlEntry;
            var url = !string.IsNullOrWhiteSpace(selected?.Url)
                ? selected!.Url
                : _config.DefaultUrl;

            if (string.IsNullOrWhiteSpace(url)) return;

            _autoLoginDone = false;
            try { _webView!.CoreWebView2.Navigate(url); }
            catch (Exception ex) { Debug.WriteLine($"Navigate failed: {ex.Message}"); }
        }

        private void SwitchToLoginView()
        {
            StopConnectionMonitor();

            if (_webView != null) _webView.Visibility = Visibility.Collapsed;
            if (_webViewReady)
            {
                try { _webView!.CoreWebView2.Navigate("about:blank"); }
                catch (Exception ex) { Debug.WriteLine($"Logout navigate failed: {ex.Message}"); }
            }

            AppSession.CurrentUser = null;

            MainView.Visibility = Visibility.Collapsed;
            LoginView.Visibility = Visibility.Visible;

            txtStaffCode.Text = "";
            HideLoginError();
            ResetConnectionIndicator();
            txtStaffCode.Focus();
        }

        // ========================================================
        // ログアウト
        // ========================================================

        private void BtnLogout_Click(object sender, RoutedEventArgs e) => SwitchToLoginView();

        // ========================================================
        // URL プルダウン
        // ========================================================

        private void PopulateUrlComboBox()
        {
            cmbUrl.Items.Clear();
            foreach (var entry in _config.UrlList)
            {
                cmbUrl.Items.Add(entry);
            }

            var defaultEntry = _config.UrlList.FirstOrDefault(u => u.IsDefault)
                               ?? _config.UrlList.FirstOrDefault();
            if (defaultEntry != null)
            {
                cmbUrl.SelectedItem = defaultEntry;
            }
        }

        private void CmbUrl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_webViewReady) return;

            if (cmbUrl.SelectedItem is UrlEntry entry && !string.IsNullOrWhiteSpace(entry.Url))
            {
                _autoLoginDone = false;
                try { _webView!.CoreWebView2.Navigate(entry.Url); }
                catch (Exception ex) { Debug.WriteLine($"URL switch failed: {ex.Message}"); }
            }
        }

        // ========================================================
        // 自動ログイン（NavigationCompleted + JS 注入）
        // ========================================================

        private async void WebView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (_autoLoginDone || !e.IsSuccess) return;

            var staffCode = AppSession.CurrentUser?.StaffCode;
            if (string.IsNullOrEmpty(staffCode)) return;

            var escaped = staffCode.Replace("'", "\\'");
            var script = $@"
                (function() {{
                    var form = document.getElementById('loginForm');
                    if (!form) return 'no_form';
                    var input = document.getElementById('userId');
                    if (!input) return 'no_input';
                    window.actraStaffCode = '{escaped}';
                    if (typeof window.autoLogin === 'function') {{
                        window.autoLogin('{escaped}');
                    }} else {{
                        input.value = '{escaped}';
                        form.submit();
                    }}
                    return 'ok';
                }})();";

            try
            {
                var result = await _webView!.CoreWebView2.ExecuteScriptAsync(script);
                if (result?.Contains("ok") == true)
                    _autoLoginDone = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AutoLogin failed: {ex.Message}");
            }
        }

        // ========================================================
        // 接続状態監視
        // ========================================================

        private void StartConnectionMonitor()
        {
            StopConnectionMonitor();
            _monitorCts = new CancellationTokenSource();
            var token = _monitorCts.Token;
            _ = Task.Run(() => MonitorLoopAsync(token));
        }

        private void StopConnectionMonitor()
        {
            try
            {
                _monitorCts?.Cancel();
                _monitorCts?.Dispose();
            }
            catch (ObjectDisposedException) { }
            _monitorCts = null;
        }

        private async Task MonitorLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                var staffCode = AppSession.CurrentUser?.StaffCode;
                if (string.IsNullOrEmpty(staffCode) || _api == null) break;

                using var pingCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                pingCts.CancelAfter(TimeSpan.FromSeconds(5));

                var (ok, elapsedMs) = await _api.PingStaffInfoAsync(staffCode, pingCts.Token);

                ConnState state;
                if (!ok) state = ConnState.Error;
                else if (elapsedMs > 2000) state = ConnState.Warning;
                else state = ConnState.Normal;

                try
                {
                    await Dispatcher.InvokeAsync(() => UpdateConnectionIndicator(state));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Monitor UI update failed: {ex.Message}");
                }

                try { await Task.Delay(TimeSpan.FromSeconds(5), ct); }
                catch (OperationCanceledException) { break; }
            }
        }

        private void UpdateConnectionIndicator(ConnState state)
        {
            var dotColor = state switch
            {
                ConnState.Normal  => Color.FromRgb(0x4C, 0xAF, 0x50), // 緑
                ConnState.Warning => Color.FromRgb(0xFF, 0xC1, 0x07), // 黄
                _                 => Color.FromRgb(0xE5, 0x39, 0x35), // 赤
            };
            stateDot.Fill = new SolidColorBrush(dotColor);
        }

        private void ResetConnectionIndicator()
        {
            // 起動時・ログアウト時は黄（接続中）
            stateDot.Fill = new SolidColorBrush(Color.FromRgb(0xFF, 0xC1, 0x07));
        }

        // ========================================================
        // QR パース（JSON / KeyValue デュアルフォーマット）
        // ========================================================

        private static QrConfig ParseQr(string input)
        {
            input = input.Trim();

            // ① JSON として試す
            try
            {
                var qr = JsonSerializer.Deserialize<QrConfig>(input, JsonReadOpts);
                if (qr != null && !string.IsNullOrWhiteSpace(qr.Ip))
                {
                    qr.Version ??= 1;
                    return qr;
                }
            }
            catch (JsonException) { }

            // ② ハンディリーダ対策の前処理 → KeyValue パース
            var normalized = input
                .Replace("*", "")
                .Replace("+", "=")
                .Replace(",", ";")
                .Replace("`", "")
                .ToLower();

            return ParseKeyValue(normalized);
        }

        private static QrConfig ParseKeyValue(string input)
        {
            var qr = new QrConfig();

            foreach (var p in input.Split(';'))
            {
                var kv = p.Split('=');
                if (kv.Length != 2) continue;

                var key = kv[0].Trim();
                var value = kv[1].Trim();

                switch (key)
                {
                    case "ip":       qr.Ip = value; break;
                    case "location": qr.Location = value; break;
                    case "protocol": qr.Protocol = value.ToLower(); break;
                    case "version":
                        if (int.TryParse(value, out var v)) qr.Version = v;
                        break;
                }
            }

            if (string.IsNullOrWhiteSpace(qr.Ip) ||
                string.IsNullOrWhiteSpace(qr.Location) ||
                string.IsNullOrWhiteSpace(qr.Protocol))
            {
                throw new FormatException(
                    "QRコードの内容が正しくありません。\n" +
                    "JSON形式 または IP=...;LOCATION=...;PROTOCOL=... 形式で入力してください。");
            }

            qr.Version ??= 1;
            return qr;
        }

        // ========================================================
        // ヘッダードラッグ
        // ========================================================

        private void Header_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        // ========================================================
        // 終了（Hide で隠す。Close はアプリ終了時のみ）
        // ========================================================

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            e.Cancel = true;
            SavePosition();
            Hide();
        }

        /// <summary>
        /// 現在位置を保存する。Hide 前に呼ぶ。
        /// </summary>
        internal void SavePosition()
        {
            _lastLeft = Left;
            _lastTop = Top;
        }

        /// <summary>
        /// 表示する。前回位置があれば復元、なければ右端に寄せる。
        /// </summary>
        internal void ShowAtLastPosition(double tabTop)
        {
            if (_lastLeft < 0)
            {
                Left = SystemParameters.WorkArea.Width - Width - TabWidth;
                Top = tabTop;
            }
            else
            {
                Left = _lastLeft;
                Top = _lastTop;
            }

            Dispatcher.InvokeAsync(() => ClampToScreen());
            Show();
            Activate();
        }

        private void ClampToScreen()
        {
            if (_isClamping) return;
            try
            {
                _isClamping = true;
                double maxLeft = SystemParameters.WorkArea.Width - Width - TabWidth;
                if (Left > maxLeft && Left != maxLeft)
                    Left = maxLeft;
            }
            finally
            {
                _isClamping = false;
            }
        }

        /// <summary>
        /// アプリ終了時に呼ばれる。リソースを解放して実際に閉じる。
        /// </summary>
        internal void ForceClose()
        {
            StopConnectionMonitor();
            try { _webView?.Dispose(); }
            catch (Exception ex) { Debug.WriteLine($"WebView dispose: {ex.Message}"); }

            Closing -= MainWindow_Closing;
            Close();
        }

        // ========================================================
        // config.json 読み書き
        // ========================================================

        internal static AppConfig LoadConfig()
        {
            if (!File.Exists(ConfigPath))
                return new AppConfig();

            try
            {
                var json = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<AppConfig>(json, JsonReadOpts) ?? new AppConfig();
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"config.json parse error: {ex.Message}");
                return new AppConfig();
            }
        }

        internal static void SaveConfig(AppConfig config)
        {
            try
            {
                var json = JsonSerializer.Serialize(config, JsonWriteOpts);
                File.WriteAllText(ConfigPath, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"config.json save error: {ex.Message}");
            }
        }
    }
}
