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
        private readonly List<TableColumn> _originalColumns = new List<TableColumn>();

        public EditTableWindow(string tableName)
        {
            InitializeComponent();
            _tableName = tableName;

            CurrentTableNameText.Text = tableName;
            NewTableNameTextBox.Text = tableName;

            LoadTableInfo();
        }

        private void LoadTableInfo()
        {
            LoadRowCount();
            LoadTableColumns();
        }

        private void LoadRowCount()
        {
            try
            {
                using (var conn = DatabaseManager.CreateNewConnection())
                {
                    conn.Open();
                    using (var cmd = new SqlCommand($"SELECT COUNT(*) FROM [{_tableName}]", conn))
                    {
                        RowCountText.Text = $"Количество записей: {(int)cmd.ExecuteScalar()}";
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
                        ) THEN 1 ELSE 0 END AS IsPrimaryKey
                FROM sys.columns c
                JOIN sys.types t ON c.user_type_id = t.user_type_id
                WHERE c.object_id = OBJECT_ID(@tableName)
                ORDER BY c.column_id";

            try
            {
                using (var conn = DatabaseManager.CreateNewConnection())
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@tableName", _tableName);

                        using (var reader = cmd.ExecuteReader())
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
            string name = NewColumnNameTextBox.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Введите имя столбца", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            foreach (TableColumn c in ColumnsDataGrid.Items)
            {
                if (c.ColumnName.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show("Столбец уже существует", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            string type = "nvarchar(100)";
            if (DataTypeComboBox.SelectedItem is ComboBoxItem item)
                type = item.Content.ToString();

            ColumnsDataGrid.Items.Add(new TableColumn
            {
                ColumnName = name,
                DataType = type,
                AllowNull = AllowNullCheckBox.IsChecked ?? true,
                IsExisting = false
            });

            NewColumnNameTextBox.Text = "NewColumn";
        }

        private void DeleteColumnButton_Click(object sender, RoutedEventArgs e)
        {
            var column = (sender as Button)?.DataContext as TableColumn;
            if (column == null) return;

            if (column.IsExisting)
            {
                var r = MessageBox.Show(
                    $"Удалить столбец '{column.ColumnName}'? Возможна потеря данных.",
                    "Подтверждение",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (r != MessageBoxResult.Yes) return;
            }

            ColumnsDataGrid.Items.Remove(column);
        }

        private void ClearColumnsButton_Click(object sender, RoutedEventArgs e)
        {
            var list = new List<TableColumn>();
            foreach (TableColumn c in ColumnsDataGrid.Items)
                if (!c.IsExisting) list.Add(c);

            foreach (var c in list)
                ColumnsDataGrid.Items.Remove(c);
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string newName = NewTableNameTextBox.Text.Trim();
                if (string.IsNullOrEmpty(newName))
                {
                    MessageBox.Show("Введите имя таблицы", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var current = new List<TableColumn>();
                foreach (TableColumn c in ColumnsDataGrid.Items)
                    current.Add(c);

                if (!HasChanges(newName, current))
                {
                    MessageBox.Show("Нет изменений", "Информация",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var confirm = MessageBox.Show(
                    "Сохранить изменения?",
                    "Подтверждение",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (confirm != MessageBoxResult.Yes) return;

                ApplyChanges(newName, current);

                MessageBox.Show("Изменения сохранены", "Успех",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool HasChanges(string newName, List<TableColumn> current)
        {
            if (newName != _tableName) return true;
            if (current.Count != _originalColumns.Count) return true;

            foreach (var c in current)
            {
                var o = _originalColumns.Find(x => x.ColumnName == c.ColumnName);
                if (o == null) return true;
                if (o.DataType != c.DataType || o.AllowNull != c.AllowNull || o.IsPrimaryKey != c.IsPrimaryKey)
                    return true;
            }

            return false;
        }

        private void ApplyChanges(string newName, List<TableColumn> current)
        {
            using (var conn = DatabaseManager.CreateNewConnection())
            {
                conn.Open();
                using (var tr = conn.BeginTransaction())
                {
                    try
                    {
                        if (newName != _tableName)
                        {
                            using (var cmd = new SqlCommand($"EXEC sp_rename '{_tableName}', '{newName}'", conn, tr))
                                cmd.ExecuteNonQuery();

                            _tableName = newName;
                        }

                        var newColumns = current.FindAll(c => !c.IsExisting);
                        var deleted = _originalColumns.FindAll(o => !current.Exists(c => c.ColumnName == o.ColumnName));

                        foreach (var col in deleted)
                        {
                            using (var cmd = new SqlCommand(
                                $"ALTER TABLE [{_tableName}] DROP COLUMN [{col.ColumnName}]", conn, tr))
                                cmd.ExecuteNonQuery();
                        }

                        foreach (var col in newColumns)
                        {
                            string nullPart = col.AllowNull ? "NULL" : "NOT NULL";
                            using (var cmd = new SqlCommand(
                                $"ALTER TABLE [{_tableName}] ADD [{col.ColumnName}] {col.DataType} {nullPart}", conn, tr))
                                cmd.ExecuteNonQuery();
                        }

                        foreach (var col in current)
                        {
                            if (col.IsExisting)
                            {
                                var orig = _originalColumns.Find(o => o.ColumnName == col.ColumnName);
                                if (orig != null && orig.AllowNull != col.AllowNull)
                                {
                                    string nullPart = col.AllowNull ? "NULL" : "NOT NULL";
                                    using (var cmd = new SqlCommand(
                                        $"ALTER TABLE [{_tableName}] ALTER COLUMN [{col.ColumnName}] {col.DataType} {nullPart}",
                                        conn, tr))
                                        cmd.ExecuteNonQuery();
                                }
                            }
                        }

                        tr.Commit();
                    }
                    catch
                    {
                        tr.Rollback();
                        throw;
                    }
                }
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
