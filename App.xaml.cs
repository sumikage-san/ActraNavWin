using System.Diagnostics;
using System.Windows;

namespace ActraNavWin
{
    public partial class App : Application
    {
        private MainWindow? _main;
        private SlideWindow? _slide;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                _main = new MainWindow();

                _slide = new SlideWindow(_main);
                _slide.Show();

                Debug.WriteLine("SlideWindow started");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"起動エラー:\n{ex}", "ActraNavWin エラー",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _main?.ForceClose();
            base.OnExit(e);
        }
    }
}
