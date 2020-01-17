using System.Windows;
using System.Windows.Forms;

namespace VisorPedidos
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            SetScreen();
            InitializeComponent();
        }

        private void SetScreen()
        {
            Screen targetScreen = (Screen.AllScreens.Length > 1) ? Screen.AllScreens[1] : Screen.AllScreens[0];
            this.Top = targetScreen.WorkingArea.Y;
            this.Left = targetScreen.WorkingArea.X;
        }
    }
}
