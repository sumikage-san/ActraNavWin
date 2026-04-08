using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace ActraNavWin
{
    /// <summary>
    /// ActraNavWin のメインウィンドウ。
    /// Panel / PanelManager を通じて画面を管理し、WebView に直接触らない。
    /// 接続先URLは config.json で外部管理し、legacy／nexus の切替に備える。
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// アプリ全体で共有する WebView2 Environment（仕様 §15: 必須）。
        /// 全 Panel が同一ブラウザプロセスとキャッシュを使うことで、
        /// メモリ削減と安定化を実現する。
        /// 将来の別 Window（Worklog等）でもこの Environment を渡す。
        /// </summary>
        private CoreWebView2Environment? _sharedEnv;

        private readonly PanelManager _panelManager = new();

        public MainWindow()
        {
            InitializeComponent();
            // Loaded イベントで初期化することで、ウィンドウ描画完了後に
            // WebView2 の非同期初期化を安全に実行できる
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            txtStaffName.Text = AppSession.CurrentUser?.StaffName ?? "";

            var config = LoadConfig();

            // Panel 生成・登録：XAML の Border コンテナと役割を紐づける。
            // WebView は Panel.InitializeAsync で動的に生成・差し込みされる。
            _panelManager.Register(new Panel(PanelRole.Left, borderLeft));
            _panelManager.Register(new Panel(PanelRole.Center, borderCenter));
            _panelManager.Register(new Panel(PanelRole.Right, borderRight));

            // Environment はアプリ起動時に1回だけ生成し、フィールドに保持する。
            // 既に生成済みの場合は再生成しない（将来の再初期化シナリオへの備え）。
            _sharedEnv ??= await CoreWebView2Environment.CreateAsync();

            // Environment 生成失敗時は Panel を初期化できないため、ここで中断する。
            if (_sharedEnv is null)
            {
                Debug.WriteLine("WebView2 Environment の生成に失敗しました。Panel を初期化できません。");
                return;
            }

            // 全 Panel の WebView2 を共有 Environment で初期化し、
            // config.json の panels 設定に基づいて URL を割り当てる。
            await _panelManager.InitializeAllAsync(_sharedEnv);
            _panelManager.NavigateAll(config);
        }

        /// <summary>
        /// 全 Panel のリソースを解放する。
        /// WebView2 は内部でブラウザプロセス（msedgewebview2.exe）を保持しており、
        /// Window の Close だけでは即座に解放されない場合がある。
        /// PanelManager.DisposeAll() により全 Panel の Dispose を一括実行する。
        /// </summary>
        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            _panelManager.DisposeAll();
        }

        /// <summary>
        /// 実行ディレクトリの config.json からアプリ設定を読み込む。
        /// ファイル未配置やパースエラー時はデフォルト値で動作を継続する。
        /// これにより、config.json が無くてもアプリが起動できることを保証する。
        /// </summary>
        internal static AppConfig LoadConfig()
        {
            // 実行ディレクトリ基準で探索する。開発時（bin/Debug）と
            // 配布時（インストール先）のどちらでも同じ挙動になる
            var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

            if (!File.Exists(configPath))
            {
                Debug.WriteLine($"config.json not found at {configPath}, using defaults.");
                return new AppConfig();
            }

            try
            {
                var json = File.ReadAllText(configPath);
                // JSON のキー名が camelCase（baseUrl）でも C# の PascalCase（BaseUrl）でも
                // 対応できるよう、大文字小文字を区別しない設定にしている
                return JsonSerializer.Deserialize<AppConfig>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new AppConfig();
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"Failed to parse config.json: {ex.Message}, using defaults.");
                return new AppConfig();
            }
        }
    }
}
