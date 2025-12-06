using blago.Classes;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace blago.Pages
{
    public partial class UserPage : Page
    {
        private int _userId;

        // Ссылка на текущие права выбранной таблицы
        private UserManager.TablePermission _currentPermission;

        public UserPage(int userId)
        {
            InitializeComponent();
            _userId = userId;

            LoadUserTables();
        }

        private void LoadUserTables()
        {
            try
            {
                var permissions = UserManager.GetAllTablePermissions(_userId);

                if (permissions == null || permissions.Count == 0)
                {
                    MessageBox.Show("У пользователя нет прав или они не загружены.");
                    return;
                }

                TableList.Items.Clear();

                foreach (var perm in permissions)
                {
                    if (!perm.CanView)
                        continue;

                    ListBoxItem item = new ListBoxItem
                    {
                        Content = perm.TableName,
                        FontSize = 18,
                        Margin = new Thickness(5),
                        Tag = perm // ← Сохраняем права
                    };

                    TableList.Items.Add(item);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки таблиц: {ex.Message}");
            }
        }

        private void TableList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TableList.SelectedItem is ListBoxItem selected)
            {
                _currentPermission = (UserManager.TablePermission)selected.Tag;

                LoadTableData(_currentPermission.TableName);

                UpdateButtonsState();
            }
        }

        private void LoadTableData(string tableName)
        {
            try
            {
                var dt = DatabaseManager.GetTable(tableName);

                if (dt == null)
                {
                    MessageBox.Show($"Не удалось загрузить таблицу: {tableName}");
                    return;
                }

                TableView.ItemsSource = dt.DefaultView;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки таблицы {tableName}: {ex.Message}");
            }
        }

        private void UpdateButtonsState()
        {
            if (_currentPermission == null)
                return;

            AddButton.IsEnabled = _currentPermission.CanAdd;
            EditButton.IsEnabled = _currentPermission.CanEdit;
            DeleteButton.IsEnabled = _currentPermission.CanDelete;
        }

        private void AddNote(object sender, RoutedEventArgs e)
        {
            if (_currentPermission == null || !_currentPermission.CanAdd)
                return;

            if (TableList.SelectedItem == null)
            {
                MessageBox.Show("Пожалуйста, выберите таблицу для добавления записи",
                    "Таблица не выбрана",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Правильное имя таблицы
            string selectedTable = _currentPermission.TableName;

            try
            {
                var addRecordWindow = new AddRecordWindow(selectedTable);
                bool? result = addRecordWindow.ShowDialog();

                if (result == true)
                {
                    LoadTableData(selectedTable);
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


        private void EditNote(object sender, RoutedEventArgs e)
        {
            // Проверяем, выбрана ли таблица
            if (string.IsNullOrEmpty(_currentPermission.TableName))
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
                var editRecordWindow = new EditRecordWindow(_currentPermission.TableName, rowCopy);
                bool? result = editRecordWindow.ShowDialog();

                if (result == true)
                {
                    // Обновляем данные в таблице
                    LoadTableData(_currentPermission.TableName);

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

        private void DeleteNote(object sender, RoutedEventArgs e)
        {
            if (_currentPermission == null || !_currentPermission.CanDelete)
                return;

            if (string.IsNullOrEmpty(_currentPermission.TableName))
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
                var primaryKeys = GetPrimaryKeyColumns(_currentPermission.TableName);

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
                    $"Вы уверены, что хотите удалить выбранную запись из таблицы '{_currentPermission.TableName}'?",
                    "Подтверждение удаления",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                    return;

                // Выполняем удаление
                string deleteQuery = $"DELETE FROM [{_currentPermission.TableName}] WHERE {whereCondition}";

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
                            LoadTableData(_currentPermission.TableName);
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

        private void Exit(object sender, System.Windows.Input.KeyEventArgs e)
        {
            DatabaseManager.Logout();
            Auth auth = new Auth();
            NavigationService.Navigate(auth);
        }
    }
}
