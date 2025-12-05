using blago.Classes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace blago.Pages
{
    public partial class CreateUserWindow : Window
    {
        private string _generatedPassword;
        private bool _isCreating = false;

        public CreateUserWindow()
        {
            InitializeComponent();
            LoadDatabaseRoles();
            GeneratePasswordCheckBox_Checked(null, null);
        }

        private void LoadDatabaseRoles()
        {
            try
            {
                var roles = UserManager.GetDatabaseRoles();
                RolesListBox.ItemsSource = roles;

                // Выбираем стандартные роли по умолчанию
                foreach (var item in RolesListBox.Items)
                {
                    string role = item.ToString();
                    if (role == "db_datareader" || role == "db_datawriter")
                    {
                        ListBoxItem listBoxItem = RolesListBox.ItemContainerGenerator.ContainerFromItem(item) as ListBoxItem;
                        if (listBoxItem != null)
                        {
                            listBoxItem.IsSelected = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке ролей: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void GeneratePasswordCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                PasswordBox.IsEnabled = false;
                ConfirmPasswordBox.IsEnabled = false;

                _generatedPassword = UserManager.GenerateSecurePassword();

                // Безопасное отображение пароля
                if (GeneratedPasswordText != null)
                {
                    GeneratedPasswordText.Text = $"Сгенерирован пароль: {_generatedPassword}";
                }
                else
                {
                    // Если элемент не найден, показываем в MessageBox
                    MessageBox.Show($"Сгенерирован пароль: {_generatedPassword}\n\nСкопируйте его!",
                        "Сгенерированный пароль",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }

                PasswordBox.Password = _generatedPassword;
                ConfirmPasswordBox.Password = _generatedPassword;

                UpdatePasswordStrength(_generatedPassword);
                CheckPasswordMatch();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при генерации пароля: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void GeneratePasswordCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            try
            {
                PasswordBox.IsEnabled = true;
                ConfirmPasswordBox.IsEnabled = true;

                if (GeneratedPasswordText != null)
                {
                    GeneratedPasswordText.Text = "";
                }

                PasswordBox.Password = "";
                ConfirmPasswordBox.Password = "";

                if (PasswordStrength != null)
                    PasswordStrength.Text = "";

                if (PasswordMatch != null)
                    PasswordMatch.Text = "";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UsernameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                string username = UsernameTextBox.Text.Trim();

                if (!string.IsNullOrEmpty(username))
                {
                    if (UserManager.SqlLoginExists(username))
                    {
                        if (UsernameHint != null)
                        {
                            UsernameHint.Text = "⚠️ Этот логин уже существует в SQL Server";
                            UsernameHint.Foreground = System.Windows.Media.Brushes.Red;
                        }
                        CreateButton.IsEnabled = false;
                    }
                    else
                    {
                        if (UsernameHint != null)
                        {
                            UsernameHint.Text = "✓ Этот логин доступен";
                            UsernameHint.Foreground = System.Windows.Media.Brushes.Green;
                        }
                        CreateButton.IsEnabled = true;
                    }
                }
                else
                {
                    if (UsernameHint != null)
                    {
                        UsernameHint.Text = "Это имя будет использоваться для входа в SQL Server";
                        UsernameHint.Foreground = System.Windows.Media.Brushes.Gray;
                    }
                    CreateButton.IsEnabled = false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при проверке логина: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            try
            {
                if (GeneratePasswordCheckBox.IsChecked == true)
                    return;

                string password = PasswordBox.Password;
                UpdatePasswordStrength(password);
                CheckPasswordMatch();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void UpdatePasswordStrength(string password)
        {
            try
            {
                if (PasswordStrength == null) return;

                if (string.IsNullOrEmpty(password))
                {
                    PasswordStrength.Text = "";
                    return;
                }

                if (UserManager.IsValidSqlPassword(password))
                {
                    PasswordStrength.Text = "✓ Пароль соответствует требованиям безопасности";
                    PasswordStrength.Foreground = System.Windows.Media.Brushes.Green;
                }
                else
                {
                    PasswordStrength.Text = "⚠️ Пароль не соответствует требованиям безопасности";
                    PasswordStrength.Foreground = System.Windows.Media.Brushes.Red;
                }
            }
            catch
            {
                // Игнорируем ошибки при обновлении визуальных элементов
            }
        }

        private void ConfirmPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            try
            {
                if (GeneratePasswordCheckBox.IsChecked == true)
                    return;

                CheckPasswordMatch();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void CheckPasswordMatch()
        {
            try
            {
                if (PasswordMatch == null) return;

                string password = PasswordBox.Password;
                string confirmPassword = ConfirmPasswordBox.Password;

                if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(confirmPassword))
                {
                    PasswordMatch.Text = "";
                    return;
                }

                if (password == confirmPassword)
                {
                    PasswordMatch.Text = "✓ Пароли совпадают";
                    PasswordMatch.Foreground = System.Windows.Media.Brushes.Green;
                }
                else
                {
                    PasswordMatch.Text = "⚠️ Пароли не совпадают";
                    PasswordMatch.Foreground = System.Windows.Media.Brushes.Red;
                }
            }
            catch
            {
                // Игнорируем ошибки при обновлении визуальных элементов
            }
        }

        private async void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            // Защита от повторного нажатия
            if (_isCreating)
                return;

            _isCreating = true;

            string username = UsernameTextBox.Text.Trim();
            string fullName = FullNameTextBox.Text.Trim();
            string password = GeneratePasswordCheckBox.IsChecked == true ? _generatedPassword : PasswordBox.Password;
            string confirmPassword = ConfirmPasswordBox.Password;

            // Валидация
            if (string.IsNullOrEmpty(username))
            {
                MessageBox.Show("Введите логин SQL Server",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                UsernameTextBox.Focus();
                _isCreating = false;
                return;
            }

            if (string.IsNullOrEmpty(fullName))
            {
                MessageBox.Show("Введите ФИО пользователя",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                FullNameTextBox.Focus();
                _isCreating = false;
                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Введите пароль",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                PasswordBox.Focus();
                _isCreating = false;
                return;
            }

            if (!UserManager.IsValidSqlPassword(password))
            {
                MessageBox.Show("Пароль не соответствует требованиям безопасности SQL Server:\n" +
                              "- Минимум 8 символов\n" +
                              "- Заглавные и строчные буквы\n" +
                              "- Цифры\n" +
                              "- Специальные символы (!@#$%^&* и т.д.)",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                PasswordBox.Focus();
                _isCreating = false;
                return;
            }

            if (password != confirmPassword)
            {
                MessageBox.Show("Пароли не совпадают",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                ConfirmPasswordBox.Focus();
                _isCreating = false;
                return;
            }

            try
            {
                // Показываем индикатор загрузки
                CreateButton.IsEnabled = false;
                CreateButton.Content = "Создание...";

                string generatedPassword;

                // 1. Создаем пользователя в SQL Server и нашей таблице
                bool userCreated = UserManager.CreateUserWithSqlLogin(username, password, fullName, out generatedPassword);

                if (userCreated)
                {
                    // 2. Ждем создания пользователя
                    await Task.Delay(1000);

                    // 3. Назначаем выбранные роли
                    var selectedRoles = RolesListBox.SelectedItems.Cast<string>().ToList();
                    bool hasAdminRole = false;
                    List<string> successfullyGrantedRoles = new List<string>();

                    foreach (string role in selectedRoles)
                    {
                        try
                        {
                            await Task.Delay(100);
                            bool roleGranted = await Task.Run(() => UserManager.GrantDatabaseRole(username, role));

                            if (roleGranted)
                            {
                                successfullyGrantedRoles.Add(role);

                                // Проверяем, является ли роль административной
                                if (IsAdminRole(role))
                                {
                                    hasAdminRole = true;
                                    // Добавляем в таблицу администраторов
                                    UserManager.AddToAdmins(username, DatabaseManager.GetCurrentUsername() ?? "system");
                                }
                            }
                            else
                            {
                                MessageBox.Show($"Не удалось назначить роль '{role}'",
                                    "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Не удалось назначить роль '{role}': {ex.Message}",
                                "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }

                    // 4. Показываем информацию о созданном пользователе
                    ShowSuccessMessage(username, generatedPassword, successfullyGrantedRoles, hasAdminRole);

                    this.DialogResult = true;
                    this.Close();
                }
                else
                {
                    MessageBox.Show("Не удалось создать пользователя. Возможно, недостаточно прав для создания SQL Login.",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при создании пользователя: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                CreateButton.IsEnabled = true;
                CreateButton.Content = "Создать";
                _isCreating = false;
            }
        }

        private void ShowSuccessMessage(string username, string password, List<string> roles, bool hasAdminRole)
        {
            StringBuilder message = new StringBuilder();
            message.AppendLine("✓ Пользователь успешно создан!");
            message.AppendLine();
            message.AppendLine($"Логин SQL Server: {username}");
            message.AppendLine($"Пароль: {password}");

            if (roles.Count > 0)
            {
                message.AppendLine($"Назначенные роли: {string.Join(", ", roles)}");

                if (hasAdminRole)
                {
                    message.AppendLine();
                    message.AppendLine("⚠️ ВНИМАНИЕ: Пользователю назначены административные роли!");
                    message.AppendLine("При входе в систему он будет перенаправлен на страницу администратора.");
                }
            }

            message.AppendLine();
            message.AppendLine("Для входа в приложение:");
            message.AppendLine($"Логин: {username}");
            message.AppendLine($"Пароль: {password}");
            message.AppendLine($"Сервер: WIN-Q9DJ17TRB0K\\DOMPRESTARELIX");

            if (GeneratePasswordCheckBox.IsChecked == true)
            {
                message.AppendLine();
                message.AppendLine("⚠️ Сохраните пароль! Он больше не будет показан.");
            }

            MessageBox.Show(message.ToString(), "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private bool IsAdminRole(string roleName)
        {
            try
            {
                string[] adminRoles = {
                    "db_owner",
                    "db_securityadmin",
                    "db_accessadmin",
                    "sysadmin",
                    "securityadmin",
                    "serveradmin",
                    "db_ddladmin"
                };

                return adminRoles.Contains(roleName, StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isCreating)
            {
                this.DialogResult = false;
                this.Close();
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Предотвращаем закрытие во время создания пользователя
            if (_isCreating)
            {
                MessageBox.Show("Пожалуйста, дождитесь завершения создания пользователя",
                    "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                e.Cancel = true;
            }
        }
    }
}