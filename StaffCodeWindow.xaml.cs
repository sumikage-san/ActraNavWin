using System.Text.RegularExpressions;
using System.Windows;

namespace ActraNavWin
{
    public partial class StaffCodeWindow : Window
    {
        private readonly ActraApiClient _api;

        public StaffCodeWindow(AppConfig config)
        {
            InitializeComponent();
            _api = new ActraApiClient(config.BaseUrl);
            Loaded += (_, _) => txtStaffCode.Focus();
        }

        private async void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            var input = txtStaffCode.Text.Trim();

            // バリデーション: 3〜10文字 英数字
            if (!Regex.IsMatch(input, @"^[a-zA-Z0-9]{3,10}$"))
            {
                MessageBox.Show("Staff Code は3〜10文字の英数字で入力してください。",
                    "入力エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // API 呼び出し中はボタンを無効化
            btnConfirm.IsEnabled = false;
            try
            {
                var info = await _api.GetStaffInfoAsync(input);
                AppSession.CurrentUser = info;
                DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "エラー",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnConfirm.IsEnabled = true;
            }
        }
    }
}
