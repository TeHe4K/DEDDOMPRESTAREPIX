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

            if (string.IsNullOrEmpty(username))
            {
                MessageBox.Show("Введите логин", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                txtUser.Focus();
                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Введите пароль", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                txtPass.Focus();
                return;
            }

            try
            {
                // 1. Стандартная авторизация
                if (DatabaseManager.Login(username, password))
                {
                    // 2. Получаем статус из DatabaseManager (уже определен в Login)
                    bool isAdmin = DatabaseManager.IsAdmin();

                    // 3. Для надежности проверяем через таблицу UserAdmins
                    if (!isAdmin)
                    {
                        isAdmin = DatabaseManager.IsUserAdminInDatabase(username);

                        // Обновляем статус если нашли в таблице
                        if (isAdmin)
                        {
                            DatabaseManager.IsAdmin();
                        }
                    }

                    // 4. Перенаправление
                    if (isAdmin)
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
                    MessageBox.Show("Неверный логин или пароль",
                        "Ошибка авторизации",
                        MessageBoxButton.OK, MessageBoxImage.Error);
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
            {
                Login_Click(sender, e);
            }
        }

        private void ComboBoxAuth(object sender, ContextMenuEventArgs e)
        {
            if(Orphanage.IsSelected == true)
            {
                DatabaseManager.database = "childrens_orphanage";
            }
            else
            {
                if (Nursing_home.IsSelected == true)
                {
                    DatabaseManager.database = "elderly_care_home";
                }
            }
        }
    }
}