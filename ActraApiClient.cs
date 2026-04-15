using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;

namespace ActraNavWin
{
    /// <summary>
    /// 既存 Actra PHP API への HTTP アクセスを担うクライアント。
    /// Phase6 では接続状態監視用の軽量 Ping メソッドを追加している。
    /// </summary>
    public class ActraApiClient
    {
        private static readonly HttpClient _http = new()
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        private readonly string _baseUrl;

        public ActraApiClient(string apiBaseUrl)
        {
            _baseUrl = apiBaseUrl.TrimEnd('/');
        }

        /// <summary>
        /// staffcode から StaffInfo を取得する。
        /// GET api/get_staff_info.php?staffcode=xxx
        /// </summary>
        public async Task<StaffInfo> GetStaffInfoAsync(string staffCode)
        {
            var url = $"{_baseUrl}/api/get_staff_info.php?staffcode={Uri.EscapeDataString(staffCode)}";
            var json = await _http.GetStringAsync(url);

            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var status = root.GetProperty("status").GetString();
            if (status != "ok")
                throw new Exception("該当するスタッフが見つかりません。");

            return new StaffInfo
            {
                StaffCode = root.GetProperty("staffcode").GetString() ?? staffCode,
                StaffName = root.GetProperty("name").GetString() ?? ""
            };
        }

        /// <summary>
        /// サーバ疎通確認。QR 設定直後に接続先が生きているかをチェックする。
        /// タイムアウト時は TaskCanceledException として呼び出し元へ伝播させる。
        /// </summary>
        public async Task TestConnectionAsync()
        {
            var url = $"{_baseUrl}/api/get_staff_info.php?staffcode=ping";
            using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        }

        /// <summary>
        /// 接続状態監視用の軽量 Ping。
        /// 成否と経過ミリ秒を返す。監視ループ用のため例外は握りつぶす。
        /// </summary>
        public async Task<(bool ok, long elapsedMs)> PingStaffInfoAsync(string staffCode, CancellationToken ct)
        {
            var url = $"{_baseUrl}/api/get_staff_info.php?staffcode={Uri.EscapeDataString(staffCode)}";
            var sw = Stopwatch.StartNew();
            try
            {
                using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
                sw.Stop();
                return (resp.IsSuccessStatusCode, sw.ElapsedMilliseconds);
            }
            catch
            {
                sw.Stop();
                return (false, sw.ElapsedMilliseconds);
            }
        }
    }
}
