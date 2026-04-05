using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace ActraNavWin
{
    /// <summary>
    /// ActraNavWin のメインウィンドウ。
    /// WebView2 を通じて Actra（将来的には Nexus）の Web UI を表示する。
    /// 接続先URLは config.json で外部管理し、legacy／nexus の切替に備える。
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            // Loaded イベントで初期化することで、ウィンドウ描画完了後に
            // WebView2 の非同期初期化を安全に実行できる
            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var config = LoadConfig();

            // 各 WebView2 を順番に初期化する。
            // EnsureCoreWebView2Async はランタイム環境のセットアップを伴うため、
            // 並列実行ではなく順番に await して安定性を確保する。
            await webView1.EnsureCoreWebView2Async(null);
            await webView2.EnsureCoreWebView2Async(null);
            await webView3.EnsureCoreWebView2Async(null);

            // 現時点では3画面とも同じ baseUrl を表示する。
            // 将来的にはパネルごとに異なるURLや役割を割り当てる想定。
            webView1.CoreWebView2.Navigate(config.BaseUrl);
            webView2.CoreWebView2.Navigate(config.BaseUrl);
            webView3.CoreWebView2.Navigate(config.BaseUrl);
        }

        /// <summary>
        /// 実行ディレクトリの config.json からアプリ設定を読み込む。
        /// ファイル未配置やパースエラー時はデフォルト値で動作を継続する。
        /// これにより、config.json が無くてもアプリが起動できることを保証する。
        /// </summary>
        private static AppConfig LoadConfig()
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
