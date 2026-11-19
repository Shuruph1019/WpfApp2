using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using WpfApp2;

namespace WpfApp2
{
    public partial class LoginWindow : Window
    {
        public LoginWindow() { InitializeComponent(); }

        private void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            User user = null;
            foreach (var u in Db.Store.Users)
                if (u.Login == TbLogin.Text.Trim() && u.PassHash == Db.Hash(PbPass.Password))
                { user = u; break; }

            if (user == null)
            {
                MessageBox.Show("Неверный логин или пароль.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            App.CurrentUser = user;
            new MainWindow().Show();
            Close();
        }
    }
}
