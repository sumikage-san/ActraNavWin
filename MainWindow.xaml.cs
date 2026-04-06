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
    /// WebView2 を通じて Actra（将来的には Nexus）の Web UI を表示する。
    /// 接続先URLは config.json で外部管理し、legacy／nexus の切替に備える。
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// アプリ全体で共有する WebView2 Environment（仕様 §15: 必須）。
        /// 全 WebView が同一ブラウザプロセスとキャッシュを使うことで、
        /// メモリ削減と安定化を実現する。
        /// 将来の Panel 化や別 Window（Worklog等）でもこの Environment を渡す。
        /// </summary>
        private CoreWebView2Environment? _sharedEnv;

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
            var config = LoadConfig();

            // Environment はアプリ起動時に1回だけ生成し、フィールドに保持する。
            // 今後 Panel 追加や別 Window 生成時にも _sharedEnv を渡すだけで済む。
            // 既に生成済みの場合は再生成しない（将来の再初期化シナリオへの備え）。
            _sharedEnv ??= await CoreWebView2Environment.CreateAsync();

            // Environment 生成失敗時は WebView を初期化できないため、ここで中断する。
            // 将来 Window 追加や Panel 動的生成で呼び出し経路が増えた場合の安全弁。
            if (_sharedEnv is null)
            {
                Debug.WriteLine("WebView2 Environment の生成に失敗しました。WebView を初期化できません。");
                return;
            }

            // 各 WebView2 を順番に初期化する。
            // 共有 Environment を渡すことで同一ブラウザプロセスを使い回す。
            await webView1.EnsureCoreWebView2Async(_sharedEnv);
            await webView2.EnsureCoreWebView2Async(_sharedEnv);
            await webView3.EnsureCoreWebView2Async(_sharedEnv);

            // WebView 番号ではなく「役割名」でURLを割り当てる。
            // config.json の panels キー（left/center/right）が画面の役割定義になる。
            NavigatePanel(webView1, config, "left");
            NavigatePanel(webView2, config, "center");
            NavigatePanel(webView3, config, "right");
        }

        /// <summary>
        /// 役割名に対応する URL を config から取得し、WebView に設定する。
        /// panels キーの欠落や不正な URL でもアプリが落ちないようガードする。
        /// </summary>
        private static void NavigatePanel(
            Microsoft.Web.WebView2.Wpf.WebView2 webView, AppConfig config, string role)
        {
            if (!config.Panels.TryGetValue(role, out var panel) || string.IsNullOrWhiteSpace(panel.Url))
            {
                Debug.WriteLine($"panels[\"{role}\"] が未定義または URL が空のため、スキップします。");
                return;
            }

            try
            {
                webView.CoreWebView2.Navigate(panel.Url);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"panels[\"{role}\"] の URL 設定に失敗: {ex.Message}");
            }
        }

        /// <summary>
        /// WebView2 リソースの明示的な解放。
        /// WebView2 は内部でブラウザプロセス（msedgewebview2.exe）を保持しており、
        /// Window の Close だけでは即座に解放されない場合がある。
        /// 現在は MainWindow で一括管理しているが、Panel 化後は各 Panel の責務に移行する。
        /// </summary>
        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            // ?. を使う理由：現在は XAML 定義のため null にならないが、
            // 将来 Panel 化で動的生成に変わると未初期化のまま Close される可能性がある。
            // try-catch の理由：Closing 中の未処理例外はアプリ終了をブロックするため、
            // Dispose 失敗でも確実に終了できるよう保護する。
            try
            {
                webView1?.Dispose();
                webView2?.Dispose();
                webView3?.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WebView2 Dispose 中にエラー: {ex.Message}");
            }
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
