using System.Windows;
using System.Windows.Input;

namespace ActraNavWin
{
    public partial class SlideWindow : Window
    {
        private readonly MainWindow _main;

        public SlideWindow(MainWindow main)
        {
            InitializeComponent();
            _main = main;

            Left = SystemParameters.WorkArea.Width - Width;
            Top = (SystemParameters.WorkArea.Height - Height) / 2;
        }

        private void Tab_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_main.IsVisible)
            {
                _main.SavePosition();
                _main.Hide();
            }
            else
            {
                _main.ShowAtLastPosition(Top);

                // 遅延でタブを最前面に戻す
                Dispatcher.InvokeAsync(() =>
                {
                    Topmost = false;
                    Topmost = true;
                    Activate();
                });
            }
        }
    }
}
