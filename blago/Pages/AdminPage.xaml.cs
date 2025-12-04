using blago.Classes;
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
            DatabaseManager.Login(DatabaseManager.GetCurrentUsername(), DatabaseManager.GetCurrentPassword());
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
                if (!DatabaseManager.IsUserLoggedIn())
                    return;

                TableList.Items.Clear();

                // Фильтруем системные таблицы (начинающиеся с "sys" и "MS")
                string query = @"
            SELECT TABLE_NAME 
            FROM INFORMATION_SCHEMA.TABLES 
            WHERE TABLE_TYPE = 'BASE TABLE'
            AND TABLE_NAME NOT LIKE 'sys%'  -- Исключаем системные таблицы
            AND TABLE_NAME NOT LIKE 'MS%'   -- Исключаем таблицы MS
            AND TABLE_SCHEMA != 'sys'       -- Исключаем схему sys
            AND TABLE_SCHEMA != 'INFORMATION_SCHEMA' -- Исключаем информационную схему
            ORDER BY TABLE_NAME";

                // Альтернативный запрос с более полным фильтром:
                string queryAlternative = @"
            SELECT 
                t.name AS TABLE_NAME,
                s.name AS SCHEMA_NAME
            FROM sys.tables t
            JOIN sys.schemas s ON t.schema_id = s.schema_id
            WHERE t.is_ms_shipped = 0  -- Исключаем системные объекты
            AND t.name NOT LIKE 'sys%'
            AND t.name NOT LIKE 'MS%'
            AND s.name NOT IN ('sys', 'INFORMATION_SCHEMA')
            ORDER BY s.name, t.name";

                using (SqlConnection conn = DatabaseManager.CreateNewConnection())
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                TableList.Items.Add(reader["TABLE_NAME"].ToString());
                            }
                        }
                    }
                }

                if (TableList.Items.Count > 0)
                {
                    TableList.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке списка таблиц: {ex.Message}");
            }
        }
        private void LoadTableData(string tableName)
        {
            try
            {
                string query = $"SELECT TOP 1000 * FROM [{tableName}]";

                using (SqlConnection conn = DatabaseManager.CreateNewConnection())
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        DataTable dataTable = new DataTable();
                        using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                        {
                            adapter.Fill(dataTable);
                        }

                        // Устанавливаем источник данных для DataGrid
                        TableView.ItemsSource = dataTable.DefaultView;

                        // Настраиваем автоматическое изменение ширины столбцов
                        TableView.AutoGenerateColumns = true;
                        TableView.CanUserAddRows = false;
                        TableView.CanUserDeleteRows = false;
                        TableView.IsReadOnly = true;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке данных таблицы {tableName}: {ex.Message}");
                TableView.ItemsSource = null;
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

                using (SqlConnection conn = DatabaseManager.CreateNewConnection())
                {
                    conn.Open();

                    // Проверяем, существует ли таблица
                    using (SqlCommand checkCmd = new SqlCommand(checkQuery, conn))
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

                        using (SqlCommand disableCmd = new SqlCommand(disableConstraintsQuery, conn))
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

                    using (SqlCommand deleteCmd = new SqlCommand(deleteQuery, conn))
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
