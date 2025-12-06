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
using System.Windows.Shapes;

namespace blago.Pages
{
    public partial class UserManagementWindow : Window
    {
        private Classes.UserManager.User _selectedUser;
        private List<Classes.UserManager.TablePermission> _currentPermissions;

        public UserManagementWindow()
        {
            InitializeComponent();
            LoadUsers();
        }

        private void LoadUsers()
        {
            try
            {
                var users = Classes. UserManager.GetAllUsers();
                UsersGrid.ItemsSource = users;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке пользователей: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UsersGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedUser = UsersGrid.SelectedItem as Classes.UserManager.User;
            if (_selectedUser != null)
            {
                SelectedUserText.Text = $"Права доступа для: {_selectedUser.FullName} ({_selectedUser.Username})";
                LoadUserPermissions();
                SavePermissionsButton.IsEnabled = true;
            }
            else
            {
                SelectedUserText.Text = "Выберите пользователя";
                PermissionsGrid.ItemsSource = null;
                SavePermissionsButton.IsEnabled = false;
            }
        }

        private void LoadUserPermissions()
        {
            try
            {
                _currentPermissions = Classes.UserManager.GetAllTablePermissions(_selectedUser.UserId);
                PermissionsGrid.ItemsSource = _currentPermissions;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке прав доступа: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddUserButton_Click(object sender, RoutedEventArgs e)
        {
            var createUserWindow = new CreateUserWindow();
            bool? result = createUserWindow.ShowDialog();

            if (result == true)
            {
                LoadUsers(); // Обновляем список пользователей
            }
        }

        private void DeleteUserButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedUser == null)
            {
                MessageBox.Show("Выберите пользователя для удаления",
                    "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_selectedUser.Username == "admin")
            {
                MessageBox.Show("Нельзя удалить администратора",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            MessageBoxResult result = MessageBox.Show(
                $"Вы уверены, что хотите удалить пользователя '{_selectedUser.Username}'?\nВсе его права доступа также будут удалены.",
                "Подтверждение удаления",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                bool success = Classes.UserManager.DeleteUser(_selectedUser.UserId);
                if (success)
                {
                    MessageBox.Show("Пользователь успешно удален",
                        "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadUsers();
                }
                else
                {
                    MessageBox.Show("Не удалось удалить пользователя",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SavePermissionsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedUser == null || _currentPermissions == null)
                return;

            try
            {
                // Сохраняем изменения
                bool allSaved = true;
                foreach (var permission in _currentPermissions)
                {
                    bool success = UserManager.SaveUserTablePermission(_selectedUser.UserId, permission);

                    UserManager.ApplyTablePermission(_selectedUser.Username, permission);

                    if (!success)
                    {
                        allSaved = false;
                    }
                }

                if (allSaved)
                {
                    MessageBox.Show("Права доступа успешно сохранены",
                        "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Некоторые права не удалось сохранить",
                        "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении прав: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }
    }
}
