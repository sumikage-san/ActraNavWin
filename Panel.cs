using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace ActraNavWin
{
    /// <summary>
    /// 1画面の責任単位。UI コンテナ・WebView・状態を一体で管理する。
    /// MainWindow は Panel を通じてのみ画面を操作し、WebView に直接触らない。
    /// 将来の 5 画面化・Window 分離時もこの単位で扱う。
    /// </summary>
    public class Panel : IDisposable
    {
        public PanelRole Role { get; }
        public Border Container { get; }
        public WebView2? WebView { get; private set; }
        public string CurrentUrl { get; private set; } = "";

        /// <summary>
        /// WebView2 の CoreWebView2 が使用可能な状態かを示す。
        /// InitializeAsync 完了前や、Dispose 後は false になる。
        /// Navigate 等の操作はこのフラグが true の場合のみ実行される。
        /// </summary>
        public bool IsInitialized => WebView?.CoreWebView2 != null;

        public Panel(PanelRole role, Border container)
        {
            Role = role;
            Container = container;
        }

        /// <summary>
        /// WebView2 を生成し、共有 Environment で初期化する。
        /// Container の Child に WebView を差し込むことで、
        /// XAML 側のレイアウト（背景色・境界線）と Panel ロジックを分離する。
        /// </summary>
        public async Task InitializeAsync(CoreWebView2Environment env)
        {
            WebView = new WebView2();
            Container.Child = WebView;
            await WebView.EnsureCoreWebView2Async(env);
        }

        /// <summary>
        /// 指定 URL へナビゲートする。
        /// 初期化未完了・URL 空・不正 URL の場合はクラッシュせずスキップする。
        /// </summary>
        public void Navigate(string url)
        {
            if (!IsInitialized)
            {
                Debug.WriteLine($"Panel[{Role}]: 未初期化のため Navigate をスキップします。");
                return;
            }

            if (string.IsNullOrWhiteSpace(url))
            {
                Debug.WriteLine($"Panel[{Role}]: URL が空のためスキップします。");
                return;
            }

            try
            {
                WebView!.CoreWebView2.Navigate(url);
                CurrentUrl = url;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Panel[{Role}]: Navigate 失敗 — {ex.Message}");
            }
        }

        public void SetVisible(bool visible)
        {
            Container.Visibility = visible
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        public void Dispose()
        {
            try
            {
                WebView?.Dispose();
                WebView = null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Panel[{Role}]: Dispose 中にエラー — {ex.Message}");
            }
        }
    }
}
