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
        private string _currentTableName;
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

                string query = @"
            SELECT TABLE_NAME 
            FROM INFORMATION_SCHEMA.TABLES 
            WHERE TABLE_TYPE = 'BASE TABLE'
            AND TABLE_NAME NOT LIKE 'sys%'
            AND TABLE_NAME NOT LIKE 'MS%'
            AND TABLE_SCHEMA != 'sys'
            AND TABLE_SCHEMA != 'INFORMATION_SCHEMA'
            ORDER BY TABLE_NAME";

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
                _currentTableName = tableName; // Сохраняем имя текущей таблицы

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
                        TableView.SelectionMode = DataGridSelectionMode.Single; // Важно!
                        TableView.SelectionUnit = DataGridSelectionUnit.FullRow; // Важно!
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
            AddRecordToTable(sender, e);
        }
        private void AddRecordToTable(object sender, RoutedEventArgs e)
        {
            // Проверяем, выбрана ли таблица
            if (TableList.SelectedItem == null)
            {
                MessageBox.Show("Пожалуйста, выберите таблицу для добавления записи",
                    "Таблица не выбрана",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string selectedTable = TableList.SelectedItem.ToString();

            try
            {
                // Открываем окно добавления записи
                var addRecordWindow = new AddRecordWindow(selectedTable);
                bool? result = addRecordWindow.ShowDialog();

                if (result == true)
                {
                    // Обновляем данные в таблице
                    LoadTableData(selectedTable);

                    // Показываем сообщение об успехе
                    MessageBox.Show("Запись успешно добавлена",
                        "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при добавлении записи: {ex.Message}",
                    "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteNote(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentTableName))
            {
                MessageBox.Show("Пожалуйста, выберите таблицу",
                    "Таблица не выбрана",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Проверяем, выбрана ли запись в DataGrid
            if (TableView.SelectedItem == null)
            {
                MessageBox.Show("Пожалуйста, выберите запись для удаления",
                    "Запись не выбрана",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Получаем выбранную строку
                DataRowView selectedRow = (DataRowView)TableView.SelectedItem;
                DataRow row = selectedRow.Row;

                // Получаем первичный ключ таблицы
                var primaryKeys = GetPrimaryKeyColumns(_currentTableName);

                // Формируем WHERE условие для удаления
                string whereCondition = "";

                if (primaryKeys.Count > 0)
                {
                    // Используем первичный ключ
                    StringBuilder whereBuilder = new StringBuilder();

                    foreach (string columnName in primaryKeys)
                    {
                        if (whereBuilder.Length > 0)
                            whereBuilder.Append(" AND ");

                        object value = row[columnName];
                        string formattedValue = FormatValueForSql(value);

                        whereBuilder.Append($"[{columnName}] = {formattedValue}");
                    }

                    whereCondition = whereBuilder.ToString();
                }
                else
                {
                    // Если нет первичного ключа, используем все столбцы
                    MessageBox.Show("В таблице не найден первичный ключ. Удаление может быть неточным.",
                        "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);

                    StringBuilder whereBuilder = new StringBuilder();

                    foreach (DataColumn column in row.Table.Columns)
                    {
                        if (whereBuilder.Length > 0)
                            whereBuilder.Append(" AND ");

                        object value = row[column.ColumnName];

                        if (value == DBNull.Value || value == null)
                        {
                            whereBuilder.Append($"[{column.ColumnName}] IS NULL");
                        }
                        else
                        {
                            string formattedValue = FormatValueForSql(value);
                            whereBuilder.Append($"[{column.ColumnName}] = {formattedValue}");
                        }
                    }

                    whereCondition = whereBuilder.ToString();
                }

                // Показываем подтверждение
                MessageBoxResult result = MessageBox.Show(
                    $"Вы уверены, что хотите удалить выбранную запись из таблицы '{_currentTableName}'?",
                    "Подтверждение удаления",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                    return;

                // Выполняем удаление
                string deleteQuery = $"DELETE FROM [{_currentTableName}] WHERE {whereCondition}";

                using (SqlConnection conn = DatabaseManager.CreateNewConnection())
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(deleteQuery, conn))
                    {
                        int rowsAffected = cmd.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            MessageBox.Show("Запись успешно удалена",
                                "Успех",
                                MessageBoxButton.OK, MessageBoxImage.Information);

                            // Обновляем данные в таблице
                            LoadTableData(_currentTableName);
                        }
                        else
                        {
                            MessageBox.Show("Не удалось удалить запись",
                                "Ошибка",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
            catch (SqlException sqlEx)
            {
                if (sqlEx.Number == 547) // Ошибка внешнего ключа
                {
                    MessageBox.Show("Невозможно удалить запись. Существуют связанные записи в других таблицах.",
                        "Ошибка удаления", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    MessageBox.Show($"Ошибка SQL при удалении: {sqlEx.Message}",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении записи: {ex.Message}",
                    "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void EditNote(object sender, RoutedEventArgs e)
        {
            // Проверяем, выбрана ли таблица
            if (string.IsNullOrEmpty(_currentTableName))
            {
                MessageBox.Show("Пожалуйста, выберите таблицу",
                    "Таблица не выбрана",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Проверяем, выбрана ли запись в DataGrid
            if (TableView.SelectedItem == null)
            {
                MessageBox.Show("Пожалуйста, выберите запись для редактирования",
                    "Запись не выбрана",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Пробуем получить выбранную строку
                DataRow row = null;
                DataRowView rowView = null;

                // Способ 1: Пробуем как DataRowView
                rowView = TableView.SelectedItem as DataRowView;
                if (rowView != null)
                {
                    row = rowView.Row;
                }
                else
                {
                    // Способ 2: Пробуем получить через ItemsSource
                    if (TableView.ItemsSource is DataView dataView)
                    {
                        int selectedIndex = TableView.SelectedIndex;
                        if (selectedIndex >= 0 && selectedIndex < dataView.Count)
                        {
                            rowView = dataView[selectedIndex];
                            row = rowView.Row;
                        }
                    }
                }

                if (row == null)
                {
                    MessageBox.Show("Не удалось получить выбранную запись",
                        "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                DataTable tempTable = row.Table.Clone();
                DataRow rowCopy = tempTable.NewRow();
                rowCopy.ItemArray = row.ItemArray;

                // Открываем окно редактирования записи
                var editRecordWindow = new EditRecordWindow(_currentTableName, rowCopy);
                bool? result = editRecordWindow.ShowDialog();

                if (result == true)
                {
                    // Обновляем данные в таблице
                    LoadTableData(_currentTableName);

                    MessageBox.Show("Запись успешно обновлена",
                        "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при редактировании записи: {ex.Message}",
                    "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CreateUser(object sender, RoutedEventArgs e)
        {
            try
            {
                var userManagementWindow = new UserManagementWindow();
                userManagementWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии управления пользователями: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
        private List<string> GetPrimaryKeyColumns(string tableName)
        {
            List<string> primaryKeys = new List<string>();

            try
            {
                string query = @"
            SELECT COLUMN_NAME
            FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE
            WHERE OBJECTPROPERTY(OBJECT_ID(CONSTRAINT_SCHEMA + '.' + CONSTRAINT_NAME), 'IsPrimaryKey') = 1
            AND TABLE_NAME = @tableName
            ORDER BY ORDINAL_POSITION";

                using (SqlConnection conn = DatabaseManager.CreateNewConnection())
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@tableName", tableName);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                primaryKeys.Add(reader["COLUMN_NAME"].ToString());
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // В случае ошибки возвращаем пустой список
            }

            return primaryKeys;
        }
        private string FormatValueForSql(object value)
        {
            if (value == DBNull.Value || value == null)
                return "NULL";

            Type valueType = value.GetType();

            if (valueType == typeof(string) || valueType == typeof(DateTime) || valueType == typeof(Guid))
            {
                // Экранируем одинарные кавычки
                string stringValue = value.ToString().Replace("'", "''");
                return $"N'{stringValue}'";
            }
            else if (valueType == typeof(bool))
            {
                return Convert.ToBoolean(value) ? "1" : "0";
            }
            else if (valueType == typeof(int) || valueType == typeof(long) ||
                     valueType == typeof(decimal) || valueType == typeof(double) ||
                     valueType == typeof(float))
            {
                return value.ToString();
            }
            else
            {
                // Для других типов
                string stringValue = value.ToString().Replace("'", "''");
                return $"N'{stringValue}'";
            }
        }

        private void Exit(object sender, KeyEventArgs e)
        {
            DatabaseManager.Logout();
            Auth auth = new Auth();
            this.NavigationService.Navigate(auth);
        }
    }
}
