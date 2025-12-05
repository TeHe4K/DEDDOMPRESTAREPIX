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
    public partial class EditRecordWindow : Window
    {
        private DataRow _originalRow;
        private string _tableName;
        private Dictionary<string, object> _originalValues;
        private Dictionary<string, TextBox> _editableFields = new Dictionary<string, TextBox>();

        public EditRecordWindow(string tableName, DataRow originalRow)
        {
            InitializeComponent();

            // Проверка на null
            if (originalRow == null)
            {
                MessageBox.Show("Ошибка: переданная запись пуста", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                this.DialogResult = false;
                this.Close();
                return;
            }

            if (string.IsNullOrEmpty(tableName))
            {
                MessageBox.Show("Ошибка: не указано имя таблицы", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                this.DialogResult = false;
                this.Close();
                return;
            }

            _tableName = tableName;
            _originalRow = originalRow;
            _originalValues = new Dictionary<string, object>();

            // Сохраняем оригинальные значения
            foreach (DataColumn column in _originalRow.Table.Columns)
            {
                _originalValues[column.ColumnName] = _originalRow[column.ColumnName];
            }

            InitializeUI();
        }

        private void InitializeUI()
        {
            try
            {
                // Устанавливаем название таблицы
                TableNameText.Text = $"Таблица: {_tableName}";

                // Динамически создаем поля для редактирования
                CreateEditableFields();

                // Показываем количество редактируемых столбцов
                RecordInfoText.Text = $"Редактируемых столбцов: {_editableFields.Count}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при инициализации интерфейса: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CreateEditableFields()
        {
            FieldsPanel.Children.Clear();
            _editableFields.Clear();

            foreach (DataColumn column in _originalRow.Table.Columns)
            {
                object value = _originalRow[column.ColumnName];
                string columnName = column.ColumnName;

                // Создаем контейнер для поля
                StackPanel fieldPanel = new StackPanel
                {
                    Margin = new Thickness(0, 10, 0, 10),
                    Tag = columnName
                };

                // Название столбца
                TextBlock label = new TextBlock
                {
                    Text = $"{columnName} [{column.DataType.Name}]",
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 5)
                };

                // Поле для ввода нового значения
                TextBox textBox = new TextBox
                {
                    Text = value?.ToString() ?? "",
                    Tag = columnName,
                    Height = 25,
                    Margin = new Thickness(0, 0, 0, 2),
                    Padding = new Thickness(3)
                };

                // Сохраняем ссылку на TextBox
                _editableFields[columnName] = textBox;

                // Текущее значение (только для информации)
                TextBlock currentValue = new TextBlock
                {
                    Text = $"Текущее: {(value == DBNull.Value ? "NULL" : value?.ToString() ?? "пусто")}",
                    FontStyle = FontStyles.Italic,
                    Foreground = Brushes.Gray,
                    FontSize = 11,
                    Margin = new Thickness(0, 2, 0, 0)
                };

                fieldPanel.Children.Add(label);
                fieldPanel.Children.Add(textBox);
                fieldPanel.Children.Add(currentValue);

                FieldsPanel.Children.Add(fieldPanel);
            }
        }

        private string GetPrimaryKeyColumn()
        {
            try
            {
                string query = @"
                    SELECT COLUMN_NAME
                    FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE
                    WHERE OBJECTPROPERTY(OBJECT_ID(CONSTRAINT_SCHEMA + '.' + CONSTRAINT_NAME), 'IsPrimaryKey') = 1
                    AND TABLE_NAME = @tableName";

                using (SqlConnection conn = DatabaseManager.CreateNewConnection())
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@tableName", _tableName);
                        object result = cmd.ExecuteScalar();

                        if (result != null && !string.IsNullOrEmpty(result.ToString()))
                        {
                            return result.ToString();
                        }

                        // Если не нашли первичный ключ, ищем столбец с именем "id" или "ID"
                        string fallbackQuery = @"
                            SELECT COLUMN_NAME 
                            FROM INFORMATION_SCHEMA.COLUMNS 
                            WHERE TABLE_NAME = @tableName 
                            AND (COLUMN_NAME = 'id' OR COLUMN_NAME = 'ID' OR COLUMN_NAME LIKE '%id%')";

                        using (SqlCommand fallbackCmd = new SqlCommand(fallbackQuery, conn))
                        {
                            fallbackCmd.Parameters.AddWithValue("@tableName", _tableName);
                            object fallbackResult = fallbackCmd.ExecuteScalar();

                            return fallbackResult?.ToString() ?? _originalRow.Table.Columns[0].ColumnName;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось определить первичный ключ: {ex.Message}\nБудет использован первый столбец.",
                    "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);

                // Возвращаем первый столбец как резервный вариант
                return _originalRow.Table.Columns[0].ColumnName;
            }
        }

        private List<string> GetPrimaryKeyColumns()
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
                        cmd.Parameters.AddWithValue("@tableName", _tableName);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                primaryKeys.Add(reader["COLUMN_NAME"].ToString());
                            }
                        }
                    }
                }

                // Если не нашли первичные ключи, используем все столбцы
                if (primaryKeys.Count == 0)
                {
                    foreach (DataColumn column in _originalRow.Table.Columns)
                    {
                        primaryKeys.Add(column.ColumnName);
                    }
                }
            }
            catch (Exception)
            {
                // В случае ошибки используем все столбцы
                foreach (DataColumn column in _originalRow.Table.Columns)
                {
                    primaryKeys.Add(column.ColumnName);
                }
            }

            return primaryKeys;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Проверка
                if (_originalRow == null || _originalValues == null)
                {
                    MessageBox.Show("Ошибка: исходная запись не найдена", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Получаем первичные ключи
                List<string> primaryKeys = GetPrimaryKeyColumns();

                // Собираем новые значения
                Dictionary<string, object> newValues = new Dictionary<string, object>();
                bool hasChanges = false;

                foreach (var kvp in _editableFields)
                {
                    string columnName = kvp.Key;
                    TextBox textBox = kvp.Value;
                    string newText = textBox.Text.Trim();
                    object originalValue = _originalValues[columnName];

                    // Проверяем, изменилось ли значение
                    string originalText = (originalValue == DBNull.Value || originalValue == null) ?
                        "" : originalValue.ToString();

                    if (newText != originalText)
                    {
                        // Преобразуем значение к правильному типу
                        object newValue = ConvertValue(newText, _originalRow.Table.Columns[columnName].DataType);
                        newValues[columnName] = newValue;
                        hasChanges = true;
                    }
                }

                if (!hasChanges)
                {
                    MessageBox.Show("Нет изменений для сохранения", "Информация",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Формируем SQL запрос UPDATE
                StringBuilder setClause = new StringBuilder();
                List<SqlParameter> parameters = new List<SqlParameter>();

                foreach (var kvp in newValues)
                {
                    if (setClause.Length > 0)
                        setClause.Append(", ");
                    setClause.Append($"[{kvp.Key}] = @new_{kvp.Key}");

                    SqlParameter param = new SqlParameter($"@new_{kvp.Key}", kvp.Value ?? DBNull.Value);

                    // Устанавливаем правильный тип данных для параметра
                    if (kvp.Value != null && kvp.Value != DBNull.Value)
                    {
                        Type valueType = kvp.Value.GetType();
                        if (valueType == typeof(int))
                            param.SqlDbType = SqlDbType.Int;
                        else if (valueType == typeof(string))
                            param.SqlDbType = SqlDbType.NVarChar;
                        else if (valueType == typeof(DateTime))
                            param.SqlDbType = SqlDbType.DateTime;
                        else if (valueType == typeof(bool))
                            param.SqlDbType = SqlDbType.Bit;
                        else if (valueType == typeof(decimal))
                            param.SqlDbType = SqlDbType.Decimal;
                        else if (valueType == typeof(float) || valueType == typeof(double))
                            param.SqlDbType = SqlDbType.Float;
                    }

                    parameters.Add(param);
                }

                // Формируем WHERE условие по первичным ключам
                StringBuilder whereClause = new StringBuilder();

                foreach (string pkColumn in primaryKeys)
                {
                    if (whereClause.Length > 0)
                        whereClause.Append(" AND ");

                    whereClause.Append($"[{pkColumn}] = @orig_{pkColumn}");

                    object originalPkValue = _originalValues[pkColumn];
                    SqlParameter pkParam = new SqlParameter($"@orig_{pkColumn}", originalPkValue ?? DBNull.Value);

                    // Устанавливаем тип данных для параметра первичного ключа
                    if (originalPkValue != null && originalPkValue != DBNull.Value)
                    {
                        Type pkType = originalPkValue.GetType();
                        if (pkType == typeof(int))
                            pkParam.SqlDbType = SqlDbType.Int;
                        else if (pkType == typeof(string))
                            pkParam.SqlDbType = SqlDbType.NVarChar;
                        // Добавьте другие типы по необходимости
                    }

                    parameters.Add(pkParam);
                }

                string updateQuery = $@"
                    UPDATE [{_tableName}] 
                    SET {setClause} 
                    WHERE {whereClause}";

                // Выполняем запрос
                using (SqlConnection conn = DatabaseManager.CreateNewConnection())
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(updateQuery, conn))
                    {
                        // Добавляем все параметры
                        foreach (SqlParameter param in parameters)
                        {
                            cmd.Parameters.Add(param);
                        }

                        int rowsAffected = cmd.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            MessageBox.Show("Запись успешно обновлена!", "Успех",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                            this.DialogResult = true;
                            this.Close();
                        }
                        else
                        {
                            MessageBox.Show("Не удалось обновить запись. Возможно, запись была изменена или удалена.",
                                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
            catch (SqlException sqlEx)
            {
                MessageBox.Show($"Ошибка SQL при обновлении записи: {sqlEx.Message}",
                    "Ошибка SQL", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обновлении записи: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private object ConvertValue(string text, Type targetType)
        {
            if (string.IsNullOrEmpty(text))
            {
                return DBNull.Value;
            }

            try
            {
                if (targetType == typeof(string))
                    return text;

                if (targetType == typeof(int))
                    return int.Parse(text);

                if (targetType == typeof(long))
                    return long.Parse(text);

                if (targetType == typeof(decimal))
                    return decimal.Parse(text);

                if (targetType == typeof(double))
                    return double.Parse(text);

                if (targetType == typeof(float))
                    return float.Parse(text);

                if (targetType == typeof(bool))
                {
                    if (text.ToLower() == "true" || text == "1")
                        return true;
                    if (text.ToLower() == "false" || text == "0")
                        return false;
                    return bool.Parse(text);
                }

                if (targetType == typeof(DateTime))
                    return DateTime.Parse(text);

                // Для других типов возвращаем как строку
                return text;
            }
            catch
            {
                // В случае ошибки преобразования возвращаем как строку
                return text;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            // Восстанавливаем оригинальные значения
            foreach (var kvp in _editableFields)
            {
                string columnName = kvp.Key;
                TextBox textBox = kvp.Value;
                object originalValue = _originalValues[columnName];

                textBox.Text = (originalValue == DBNull.Value || originalValue == null) ?
                    "" : originalValue.ToString();
            }

            MessageBox.Show("Значения сброшены к оригинальным", "Сброс",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}