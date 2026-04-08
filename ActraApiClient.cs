using System.Net.Http;
using System.Text.Json;

namespace ActraNavWin
{
    /// <summary>
    /// 既存 Actra PHP API への HTTP アクセスを担うクライアント。
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
    }
}
