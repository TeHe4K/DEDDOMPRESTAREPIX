using blago.Classes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace blago.Pages
{
    public partial class CreateUserWindow : Window
    {
        private string _generatedPassword = "";
        private bool _isCreating = false;

        public CreateUserWindow()
        {
            InitializeComponent();
            LoadRoles();
            EnableGeneratedPasswordMode();
        }

        private void LoadRoles()
        {
            try
            {
                var roles = UserManager.GetDatabaseRoles();
                RolesListBox.ItemsSource = roles;

                SelectDefaultRoles(roles);
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка загрузки ролей: {ex.Message}");
            }
        }

        private void SelectDefaultRoles(IEnumerable<string> roles)
        {
            string[] defaultRoles = { "db_datareader", "db_datawriter" };

            foreach (string role in defaultRoles)
            {
                if (roles.Contains(role))
                {
                    RolesListBox.SelectedItems.Add(role);
                }
            }
        }

        private void EnableGeneratedPasswordMode()
        {
            PasswordBox.IsEnabled = false;
            ConfirmPasswordBox.IsEnabled = false;

            _generatedPassword = UserManager.GenerateSecurePassword();

            GeneratedPasswordText.Text = $"Сгенерирован пароль: {_generatedPassword}";
            PasswordBox.Password = _generatedPassword;
            ConfirmPasswordBox.Password = _generatedPassword;

            UpdatePasswordStrength(_generatedPassword);
            UpdatePasswordMatch();
        }

        private void DisableGeneratedPasswordMode()
        {
            PasswordBox.IsEnabled = true;
            ConfirmPasswordBox.IsEnabled = true;

            GeneratedPasswordText.Text = "";
            PasswordBox.Password = "";
            ConfirmPasswordBox.Password = "";

            PasswordStrength.Text = "";
            PasswordMatch.Text = "";
        }

        private void GeneratePasswordCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            EnableGeneratedPasswordMode();
        }

        private void GeneratePasswordCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            DisableGeneratedPasswordMode();
        }

        private void UsernameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ValidateUsername();
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (!GeneratePasswordCheckBox.IsChecked == true)
            {
                UpdatePasswordStrength(PasswordBox.Password);
                UpdatePasswordMatch();
            }
        }

        private void ConfirmPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (!GeneratePasswordCheckBox.IsChecked == true)
            {
                UpdatePasswordMatch();
            }
        }

        private void ValidateUsername()
        {
            string username = UsernameTextBox.Text.Trim();

            if (string.IsNullOrEmpty(username))
            {
                SetUsernameHint("Это имя будет использоваться для входа в SQL Server", Brushes.Gray);
                CreateButton.IsEnabled = false;
                return;
            }

            if (UserManager.SqlLoginExists(username))
            {
                SetUsernameHint("⚠️ Этот логин уже существует", Brushes.Red);
                CreateButton.IsEnabled = false;
            }
            else
            {
                SetUsernameHint("✓ Логин доступен", Brushes.Green);
                CreateButton.IsEnabled = true;
            }
        }

        private void SetUsernameHint(string text, Brush color)
        {
            UsernameHint.Text = text;
            UsernameHint.Foreground = color;
        }

        private void UpdatePasswordStrength(string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                PasswordStrength.Text = "";
                return;
            }

            bool ok = UserManager.IsValidSqlPassword(password);

            PasswordStrength.Text = ok
                ? "✓ Пароль соответствует требованиям"
                : "⚠️ Пароль не соответствует требованиям";

            PasswordStrength.Foreground = ok ? Brushes.Green : Brushes.Red;
        }

        private void UpdatePasswordMatch()
        {
            if (string.IsNullOrEmpty(PasswordBox.Password) ||
                string.IsNullOrEmpty(ConfirmPasswordBox.Password))
            {
                PasswordMatch.Text = "";
                return;
            }

            bool match = PasswordBox.Password == ConfirmPasswordBox.Password;

            PasswordMatch.Text = match
                ? "✓ Пароли совпадают"
                : "⚠️ Пароли не совпадают";

            PasswordMatch.Foreground = match ? Brushes.Green : Brushes.Red;
        }

        private async void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isCreating)
                return;

            if (!ValidateBeforeCreate())
                return;

            _isCreating = true;
            CreateButton.IsEnabled = false;
            CreateButton.Content = "Создание...";

            string username = UsernameTextBox.Text.Trim();
            string fullName = FullNameTextBox.Text.Trim();
            string password = GeneratePasswordCheckBox.IsChecked == true
                ? _generatedPassword
                : PasswordBox.Password;

            try
            {
                string sqlPassword;

                bool created = UserManager.CreateUserWithSqlLogin(username, password, fullName, out sqlPassword);

                if (!created)
                {
                    ShowError("Не удалось создать пользователя");
                    return;
                }

                await Task.Delay(1000);

                await GrantSelectedRoles(username);

                ShowSuccess(username, sqlPassword);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка при создании пользователя: {ex.Message}");
            }
            finally
            {
                _isCreating = false;
                CreateButton.IsEnabled = true;
                CreateButton.Content = "Создать";
            }
        }

        private bool ValidateBeforeCreate()
        {
            if (string.IsNullOrWhiteSpace(UsernameTextBox.Text))
            {
                ShowError("Введите логин SQL Server");
                UsernameTextBox.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(FullNameTextBox.Text))
            {
                ShowError("Введите ФИО пользователя");
                FullNameTextBox.Focus();
                return false;
            }

            string pwd = GeneratePasswordCheckBox.IsChecked == true
                ? _generatedPassword
                : PasswordBox.Password;

            if (!UserManager.IsValidSqlPassword(pwd))
            {
                ShowError("Пароль не соответствует требованиям SQL Server");
                return false;
            }

            if (!GeneratePasswordCheckBox.IsChecked == true &&
                PasswordBox.Password != ConfirmPasswordBox.Password)
            {
                ShowError("Пароли не совпадают");
                return false;
            }

            return true;
        }

        private async Task GrantSelectedRoles(string username)
        {
            var roles = RolesListBox.SelectedItems.Cast<string>().ToList();

            foreach (string role in roles)
            {
                try
                {
                    await Task.Delay(100);
                    await Task.Run(() => UserManager.GrantDatabaseRole(username, role));
                }
                catch
                {
                }
            }
        }

        private void ShowSuccess(string username, string password)
        {
            StringBuilder msg = new StringBuilder();

            msg.AppendLine("✓ Пользователь создан!");
            msg.AppendLine();
            msg.AppendLine($"Логин: {username}");
            msg.AppendLine($"Пароль: {password}");
            msg.AppendLine();
            msg.AppendLine("Роли:");
            msg.AppendLine(string.Join("\n", RolesListBox.SelectedItems.Cast<string>()));

            if (GeneratePasswordCheckBox.IsChecked == true)
            {
                msg.AppendLine();
                msg.AppendLine("⚠️ Сохраните пароль, он больше не будет показан.");
            }

            MessageBox.Show(msg.ToString(), "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ShowError(string msg)
        {
            MessageBox.Show(msg, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isCreating)
            {
                DialogResult = false;
                Close();
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_isCreating)
            {
                MessageBox.Show("Дождитесь завершения создания пользователя", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);

                e.Cancel = true;
            }
        }
    }
}
