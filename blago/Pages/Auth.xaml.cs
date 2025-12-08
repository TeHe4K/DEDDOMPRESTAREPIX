using blago.Classes;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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
            string username = txtUser.Text.Trim();
            string password = txtPass.Password;

            if (DBChoice.Text == "Дом престарелых")
                DatabaseManager.database = "elderly_care_home";
            else
                DatabaseManager.database = "childrens_orphanage";

            if (string.IsNullOrEmpty(username))
            {
                MessageBox.Show("Введите логин", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtUser.Focus();
                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Введите пароль", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtPass.Focus();
                return;
            }

            try
            {
                if (DatabaseManager.Login(username, password))
                {
                    bool isAdmin = DatabaseManager.IsAdmin();

                    if (!isAdmin)
                    {
                        isAdmin = DatabaseManager.CheckUserIsAdmin(username);

                        if (isAdmin)
                            DatabaseManager.IsAdmin();
                    }

                    int userId = UserManager.GetUserIdByUsername(DatabaseManager.GetCurrentUsername());

                    if (isAdmin)
                    {
                        AdminPage adminPage = new AdminPage();
                        NavigationService.Navigate(adminPage);
                    }
                    else
                    {
                        UserPage userPage = new UserPage(userId);
                        NavigationService.Navigate(userPage);
                    }
                }
                else
                {
                    MessageBox.Show("Неверный логин или пароль",
                        "Ошибка авторизации",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при входе: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Enter(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                Login_Click(sender, e);
        }
    }
}
