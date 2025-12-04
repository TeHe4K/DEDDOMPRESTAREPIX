using blago.Classes;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace blago.Pages
{
    
    public partial class Auth : Page
    {
        public Auth()
        {
            InitializeComponent();
        }


        private void Login_Click(object sender, RoutedEventArgs e)
        {
            string username = txtUser.Text;
            string password = txtPass.Password;

            if (DatabaseManager.Login(username, password))
            {
                if (DatabaseManager.IsAdmin())
                {
                    AdminPage adminPage = new AdminPage();
                    this.NavigationService.Navigate(adminPage);
                }
                else
                {
                    UserPage userPage = new UserPage();
                    this.NavigationService.Navigate(userPage);
                }
            }
            else
            {
                MessageBox.Show("Ошибка авторизации");
            }
        }

        private void Enter(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Login_Click(sender, e);
            }
        }
    }
}
