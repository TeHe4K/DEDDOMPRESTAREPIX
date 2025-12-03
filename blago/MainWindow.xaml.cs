using System;
using System.Collections.Generic;
using System.Data.SqlClient;
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

namespace blago
{
    public partial class MainWindow : Window
    {
        // НАСТРОЙКИ ДЛЯ ВАШЕЙ СЕТИ:
        private string sqlServerIp = "WIN-Q9DJ17TRB0K\\DOMPRESTARELIX";  // IP ПК с SQL Server 2022
        private string database = "childrens_orphanage";         // Имя базы данных

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Login_Click(object sender, RoutedEventArgs e)
        {
            string username = txtUser.Text;
            string password = txtPass.Password;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                txtStatus.Text = "Заполните все поля";
                return;
            }

            txtStatus.Text = "Проверяем...";
            // 11
            // ПОДКЛЮЧЕНИЕ К SQL SERVER 2022
            string connectionString =
                $"Server={sqlServerIp};" +           // IP сервера
                $"Database={database};" +            // Имя БД
                $"User Id={username};" +             // Логин SQL
                $"Password={password};" +            // Пароль SQL
                $"TrustServerCertificate=True;"+     // Для локальной сети
                $"Integrated Security=false;" +
                $"MultipleActiveResultSets=true;" +
                $"Network Library=DBMSSOCN;";  // Это для TCP/IP
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open(); // Если открылось - авторизация успешна!

                    MessageBox.Show($"Успешный вход!\nСервер: {sqlServerIp}\nБД: {database}",
                                    "Готово", MessageBoxButton.OK, MessageBoxImage.Information);


                    txtStatus.Text = "Авторизация успешна!";
                    conn.Close();
                }
            }
            catch (SqlException)
            {
                txtStatus.Text = "Неверный логин/пароль SQL или нет доступа";
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Ошибка: {ex.Message}";
            }
        }
    }
}
