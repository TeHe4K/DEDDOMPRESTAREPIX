using blago.Classes;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
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
using System.Windows.Shapes;

namespace blago.Pages
{
    /// <summary>
    /// Логика взаимодействия для EditTableWindow.xaml
    /// </summary>
    public partial class EditTableWindow : Window
    {
        public class TableColumn
        {
            public string ColumnName { get; set; }
            public string DataType { get; set; }
            public bool AllowNull { get; set; }
            public bool IsPrimaryKey { get; set; }
            public bool IsExisting { get; set; }
        }

        private string _tableName;
        private List<TableColumn> _originalColumns = new List<TableColumn>();

        public EditTableWindow(string tableName)
        {
            InitializeComponent();
            _tableName = tableName;

            // Заполняем начальные данные
            CurrentTableNameText.Text = tableName;
            NewTableNameTextBox.Text = tableName;

            // Загружаем информацию о таблице
            LoadTableInfo();
        }

        private void LoadTableInfo()
        {
            try
            {
                // Загружаем количество строк
                LoadRowCount();

                // Загружаем столбцы таблицы
                LoadTableColumns();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки информации: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadRowCount()
        {
            try
            {
                using (SqlConnection conn = DatabaseManager.CreateNewConnection())
                {
                    conn.Open();
                    string query = $"SELECT COUNT(*) FROM [{_tableName}]";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        int count = (int)cmd.ExecuteScalar();
                        RowCountText.Text = $"Количество записей: {count}";
                    }
                }
            }
            catch
            {
                RowCountText.Text = "Не удалось получить количество записей";
            }
        }

        private void LoadTableColumns()
        {
            try
            {
                string query = @"
                    SELECT 
                        c.name AS ColumnName,
                        t.name AS DataType,
                        c.is_nullable AS AllowNull,
                        CASE 
                            WHEN EXISTS (
                                SELECT 1 
                                FROM sys.index_columns ic
                                JOIN sys.indexes i ON ic.object_id = i.object_id AND ic.index_id = i.index_id
                                WHERE ic.object_id = c.object_id 
                                AND ic.column_id = c.column_id 
                                AND i.is_primary_key = 1
                            ) THEN 1
                            ELSE 0
                        END AS IsPrimaryKey
                    FROM sys.columns c
                    JOIN sys.types t ON c.user_type_id = t.user_type_id
                    WHERE c.object_id = OBJECT_ID(@tableName)
                    ORDER BY c.column_id";

                using (SqlConnection conn = DatabaseManager.CreateNewConnection())
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@tableName", _tableName);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            ColumnsDataGrid.Items.Clear();
                            _originalColumns.Clear();

                            while (reader.Read())
                            {
                                var column = new TableColumn
                                {
                                    ColumnName = reader["ColumnName"].ToString(),
                                    DataType = reader["DataType"].ToString(),
                                    AllowNull = Convert.ToBoolean(reader["AllowNull"]),
                                    IsPrimaryKey = Convert.ToBoolean(reader["IsPrimaryKey"]),
                                    IsExisting = true
                                };

                                ColumnsDataGrid.Items.Add(column);
                                _originalColumns.Add(column);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки столбцов: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddColumnButton_Click(object sender, RoutedEventArgs e)
        {
            string columnName = NewColumnNameTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(columnName))
            {
                MessageBox.Show("Введите имя столбца", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Проверяем, существует ли уже столбец с таким именем
            foreach (TableColumn column in ColumnsDataGrid.Items)
            {
                if (column.ColumnName.Equals(columnName, StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show("Столбец с таким именем уже существует", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            // Получаем выбранный тип данных
            string dataType = "nvarchar(100)";
            if (DataTypeComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                dataType = selectedItem.Content.ToString();
            }

            // Добавляем новый столбец
            var newColumn = new TableColumn
            {
                ColumnName = columnName,
                DataType = dataType,
                AllowNull = AllowNullCheckBox.IsChecked ?? true,
                IsPrimaryKey = false,
                IsExisting = false
            };

            ColumnsDataGrid.Items.Add(newColumn);

            // Очищаем поле ввода
            NewColumnNameTextBox.Text = "NewColumn";
        }

        private void DeleteColumnButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;

            var column = button.DataContext as TableColumn;
            if (column != null)
            {
                // Для существующих столбцов показываем предупреждение
                if (column.IsExisting)
                {
                    MessageBoxResult result = MessageBox.Show(
                        $"Вы уверены, что хотите удалить существующий столбец '{column.ColumnName}'?\n" +
                        "Это действие может привести к потере данных!",
                        "Подтверждение удаления",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result != MessageBoxResult.Yes)
                        return;
                }

                ColumnsDataGrid.Items.Remove(column);
            }
        }

        private void ClearColumnsButton_Click(object sender, RoutedEventArgs e)
        {
            // Удаляем только новые столбцы (не существующие)
            var itemsToRemove = new List<TableColumn>();

            foreach (TableColumn column in ColumnsDataGrid.Items)
            {
                if (!column.IsExisting)
                {
                    itemsToRemove.Add(column);
                }
            }

            foreach (var column in itemsToRemove)
            {
                ColumnsDataGrid.Items.Remove(column);
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string newTableName = NewTableNameTextBox.Text.Trim();

                // Проверяем новое имя таблицы
                if (string.IsNullOrWhiteSpace(newTableName))
                {
                    MessageBox.Show("Введите имя таблицы", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Получаем текущие столбцы
                List<TableColumn> currentColumns = new List<TableColumn>();
                foreach (TableColumn column in ColumnsDataGrid.Items)
                {
                    currentColumns.Add(column);
                }

                // Проверяем, есть ли изменения
                if (!HasChanges(newTableName, currentColumns))
                {
                    MessageBox.Show("Нет изменений для сохранения", "Информация",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Подтверждение сохранения
                MessageBoxResult confirm = MessageBox.Show(
                    "Вы уверены, что хотите сохранить изменения?",
                    "Подтверждение",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (confirm != MessageBoxResult.Yes)
                    return;

                // Выполняем изменения
                ApplyChanges(newTableName, currentColumns);

                MessageBox.Show("Изменения успешно сохранены", "Успех",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool HasChanges(string newTableName, List<TableColumn> currentColumns)
        {
            // Проверяем изменение имени таблицы
            if (newTableName != _tableName)
                return true;

            // Проверяем изменения в столбцах
            if (currentColumns.Count != _originalColumns.Count)
                return true;

            // Проверяем изменения отдельных столбцов
            foreach (var currentColumn in currentColumns)
            {
                var originalColumn = _originalColumns.Find(oc => oc.ColumnName == currentColumn.ColumnName);

                if (originalColumn == null)
                    return true; // Новый столбец

                if (originalColumn.DataType != currentColumn.DataType ||
                    originalColumn.AllowNull != currentColumn.AllowNull ||
                    originalColumn.IsPrimaryKey != currentColumn.IsPrimaryKey)
                    return true;
            }

            return false;
        }

        private void ApplyChanges(string newTableName, List<TableColumn> currentColumns)
        {
            using (SqlConnection conn = DatabaseManager.CreateNewConnection())
            {
                conn.Open();

                using (SqlTransaction transaction = conn.BeginTransaction())
                {
                    try
                    {
                        // 1. Переименование таблицы (если нужно)
                        if (newTableName != _tableName)
                        {
                            string renameQuery = $"EXEC sp_rename '{_tableName}', '{newTableName}'";
                            using (SqlCommand cmd = new SqlCommand(renameQuery, conn, transaction))
                            {
                                cmd.ExecuteNonQuery();
                            }
                            _tableName = newTableName;
                        }

                        // 2. Определяем новые и удаленные столбцы
                        var newColumns = currentColumns.FindAll(c => !c.IsExisting);
                        var deletedColumns = _originalColumns.FindAll(oc =>
                            !currentColumns.Exists(c => c.ColumnName == oc.ColumnName));

                        // 3. Удаляем столбцы
                        foreach (var column in deletedColumns)
                        {
                            string dropQuery = $"ALTER TABLE [{_tableName}] DROP COLUMN [{column.ColumnName}]";
                            using (SqlCommand cmd = new SqlCommand(dropQuery, conn, transaction))
                            {
                                cmd.ExecuteNonQuery();
                            }
                        }

                        // 4. Добавляем новые столбцы
                        foreach (var column in newColumns)
                        {
                            string nullClause = column.AllowNull ? "NULL" : "NOT NULL";
                            string addQuery = $"ALTER TABLE [{_tableName}] ADD [{column.ColumnName}] {column.DataType} {nullClause}";

                            using (SqlCommand cmd = new SqlCommand(addQuery, conn, transaction))
                            {
                                cmd.ExecuteNonQuery();
                            }
                        }

                        // 5. Изменяем существующие столбцы (только NULL/NOT NULL)
                        foreach (var currentColumn in currentColumns)
                        {
                            if (currentColumn.IsExisting)
                            {
                                var originalColumn = _originalColumns.Find(oc => oc.ColumnName == currentColumn.ColumnName);

                                if (originalColumn != null && originalColumn.AllowNull != currentColumn.AllowNull)
                                {
                                    string alterQuery = $"ALTER TABLE [{_tableName}] ALTER COLUMN [{currentColumn.ColumnName}] {currentColumn.DataType} " +
                                                       $"{(currentColumn.AllowNull ? "NULL" : "NOT NULL")}";

                                    using (SqlCommand cmd = new SqlCommand(alterQuery, conn, transaction))
                                    {
                                        cmd.ExecuteNonQuery();
                                    }
                                }
                            }
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
