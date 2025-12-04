using blago.Classes;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
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

namespace blago.Pages
{
  
    public partial class AdminPage : Page
    {
        public AdminPage()
        {
            InitializeComponent();
            LoadTableList();
        }

        private void TableList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

            if (TableList.SelectedItem == null)
                return;

            string selectedTable = TableList.SelectedItem.ToString();
            LoadTableData(selectedTable);
        }
        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadTableList();
        }
        private void LoadTableList()
        {
            try
            {
                using (var conn = DatabaseManager.CreateNewConnection())
                {
                    conn.Open();

                    string sql = "SHOW TABLES";

                    using (var cmd = new MySqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        TableList.Items.Clear();

                        while (reader.Read())
                        {
                            TableList.Items.Add(reader.GetString(0));
                        }
                    }
                }

                if (TableList.Items.Count > 0)
                    TableList.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка: " + ex.Message);
            }
        }
        private void LoadTableData(string tableName)
        {
            try
            {
                using (var conn = DatabaseManager.CreateNewConnection())
                {
                    conn.Open();

                    string sql = $"SELECT * FROM `{tableName}` LIMIT 1000";

                    using (var cmd = new MySqlCommand(sql, conn))
                    using (var adapter = new MySqlDataAdapter(cmd))
                    {
                        DataTable dt = new DataTable();
                        adapter.Fill(dt);
                        TableView.ItemsSource = dt.DefaultView;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки таблицы: " + ex.Message);
            }
        }

        private void AddNote(object sender, RoutedEventArgs e)
        {

        }

        private void DeleteNote(object sender, RoutedEventArgs e)
        {

        }

        private void EditNote(object sender, RoutedEventArgs e)
        {

        }

        private void CreateUser(object sender, RoutedEventArgs e)
        {

        }

        private void CreateTable(object sender, RoutedEventArgs e)
        {
            try
            {
                var createTableWindow = new CreateTableWindow();
                bool? result = createTableWindow.ShowDialog();

                if (result == true)
                {
                    // Обновляем список таблиц после создания
                    LoadTableList();

                    // Показываем сообщение об успехе
                    MessageBox.Show("Таблица успешно создана!", "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при создании таблицы: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteTable(object sender, RoutedEventArgs e)
        {
            if (TableList.SelectedItem == null)
            {
                MessageBox.Show("Пожалуйста, выберите таблицу для удаления",
                    "Таблица не выбрана",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string selectedTable = TableList.SelectedItem.ToString();

            // Показываем окно подтверждения с подробной информацией
            MessageBoxResult result = MessageBox.Show(
                $"Вы уверены, что хотите удалить таблицу \"{selectedTable}\"?\n\n" +
                "⚠️  ВНИМАНИЕ:\n" +
                "• Это действие нельзя отменить\n" +
                "• Все данные в таблице будут безвозвратно удалены\n" +
                "• Зависимые объекты могут быть затронуты\n\n" +
                "Для подтверждения введите название таблицы:",
                "Подтверждение удаления таблицы",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // Запрашиваем подтверждение через дополнительное окно с вводом имени
                    var confirmWindow = new ConfirmDeleteWindow(selectedTable);
                    bool? deleteResult = confirmWindow.ShowDialog();

                    if (deleteResult == true)
                    {
                        // Выполняем удаление таблицы
                        DeleteTableFromDatabase(selectedTable);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при удалении таблицы: {ex.Message}",
                        "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        private void DeleteTableFromDatabase(string tableName)
        {
            try
            {
                // Проверяем существование таблицы
                string checkQuery = @"
                    SELECT COUNT(*) 
                    FROM INFORMATION_SCHEMA.TABLES 
                    WHERE TABLE_NAME = @tableName";

                using (MySqlConnection conn = DatabaseManager.CreateNewConnection())
                {
                    conn.Open();

                    // Проверяем, существует ли таблица
                    using (MySqlCommand checkCmd = new MySqlCommand(checkQuery, conn))
                    {
                        checkCmd.Parameters.AddWithValue("@tableName", tableName);
                        int tableCount = (int)checkCmd.ExecuteScalar();

                        if (tableCount == 0)
                        {
                            MessageBox.Show($"Таблица \"{tableName}\" не найдена",
                                "Ошибка",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                    }

                    // Отключаем внешние ключи, если они есть
                    try
                    {
                        string disableConstraintsQuery = @"
                            DECLARE @sql NVARCHAR(MAX) = '';
                            SELECT @sql = @sql + 
                                'ALTER TABLE ' + QUOTENAME(TABLE_SCHEMA) + '.' + 
                                QUOTENAME(TABLE_NAME) + ' NOCHECK CONSTRAINT ALL;'
                            FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS
                            WHERE CONSTRAINT_TYPE = 'FOREIGN KEY'
                            AND TABLE_NAME = @tableName;
                            
                            EXEC sp_executesql @sql;";

                        using (MySqlCommand disableCmd = new MySqlCommand(disableConstraintsQuery, conn))
                        {
                            disableCmd.Parameters.AddWithValue("@tableName", tableName);
                            disableCmd.ExecuteNonQuery();
                        }
                    }
                    catch
                    {
                        // Игнорируем ошибки при отключении ограничений
                    }

                    // Удаляем таблицу
                    string deleteQuery = $"DROP TABLE [{tableName}];";

                    using (MySqlCommand deleteCmd = new MySqlCommand(deleteQuery, conn))
                    {
                        int rowsAffected = deleteCmd.ExecuteNonQuery();

                        MessageBox.Show($"Таблица \"{tableName}\" успешно удалена",
                            "Успех",
                            MessageBoxButton.OK, MessageBoxImage.Information);

                        // Обновляем список таблиц
                        LoadTableList();

                        // Очищаем DataGrid
                        TableView.ItemsSource = null;
                    }
                }
            }
            catch (SqlException sqlEx)
            {
                // Обрабатываем ошибки SQL
                if (sqlEx.Number == 3701) // Cannot drop the table because it is being referenced by a foreign key constraint
                {
                    MessageBox.Show($"Невозможно удалить таблицу \"{tableName}\".\n" +
                                  "Существуют связанные таблицы через внешние ключи.\n\n" +
                                  "Сначала удалите зависимости или используйте каскадное удаление.",
                                  "Ошибка удаления",
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    MessageBox.Show($"Ошибка SQL при удалении таблицы: {sqlEx.Message}",
                                  "Ошибка",
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении таблицы: {ex.Message}",
                              "Ошибка",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EditTable(object sender, RoutedEventArgs e)
        {
            // Проверяем, выбрана ли таблица
            if (TableList.SelectedItem == null)
            {
                MessageBox.Show("Пожалуйста, выберите таблицу для редактирования",
                    "Таблица не выбрана",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string selectedTable = TableList.SelectedItem.ToString();

            try
            {
                // Открываем окно редактирования таблицы
                var editTableWindow = new EditTableWindow(selectedTable);
                bool? result = editTableWindow.ShowDialog();

                if (result == true)
                {
                    // Обновляем список таблиц
                    LoadTableList();

                    // Показываем сообщение об успехе
                    MessageBox.Show("Таблица успешно обновлена",
                        "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при редактировании таблицы: {ex.Message}",
                    "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
