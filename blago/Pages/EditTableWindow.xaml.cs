using blago.Classes;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
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
        private List<TableColumn> _originalColumns = new List<TableColumn>();

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
                    string query = $"SELECT COUNT(*) FROM `{_tableName}`";
                    using (var cmd = new MySqlCommand(query, conn))
                    {
                        int count = Convert.ToInt32(cmd.ExecuteScalar());
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
                using (var conn = DatabaseManager.CreateNewConnection())
                {
                    conn.Open();

                    string query = $"SHOW COLUMNS FROM `{_tableName}`";

                    using (var cmd = new MySqlCommand(query, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        ColumnsDataGrid.Items.Clear();
                        _originalColumns.Clear();

                        while (reader.Read())
                        {
                            var col = new TableColumn
                            {
                                ColumnName = reader["Field"].ToString(),
                                DataType = reader["Type"].ToString(),
                                AllowNull = reader["Null"].ToString() == "YES",
                                IsPrimaryKey = reader["Key"].ToString() == "PRI",
                                IsExisting = true
                            };

                            ColumnsDataGrid.Items.Add(col);
                            _originalColumns.Add(col);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки столбцов: {ex.Message}");
            }
        }

        private void AddColumnButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NewColumnNameTextBox.Text))
            {
                MessageBox.Show("Введите имя столбца");
                return;
            }

            string dataType = "VARCHAR(100)";
            if (DataTypeComboBox.SelectedItem is ComboBoxItem item)
                dataType = item.Content.ToString();

            ColumnsDataGrid.Items.Add(new TableColumn
            {
                ColumnName = NewColumnNameTextBox.Text.Trim(),
                DataType = dataType,
                AllowNull = AllowNullCheckBox.IsChecked ?? true,
                IsPrimaryKey = false,
                IsExisting = false
            });
        }

        private void DeleteColumnButton_Click(object sender, RoutedEventArgs e)
        {
            var col = (sender as Button)?.DataContext as TableColumn;
            if (col != null)
            {
                ColumnsDataGrid.Items.Remove(col);
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            string newTableName = NewTableNameTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(newTableName))
            {
                MessageBox.Show("Введите имя таблицы");
                return;
            }

            List<TableColumn> currentColumns = new List<TableColumn>();
            foreach (TableColumn col in ColumnsDataGrid.Items)
                currentColumns.Add(col);

            ApplyChanges(newTableName, currentColumns);

            MessageBox.Show("Изменения сохранены!");
            DialogResult = true;
            Close();
        }

        private void ApplyChanges(string newTableName, List<TableColumn> currentColumns)
        {
            using (var conn = DatabaseManager.CreateNewConnection())
            {
                conn.Open();
                using (var tr = conn.BeginTransaction())
                {
                    try
                    {
                        // 1: Переименование таблицы
                        if (newTableName != _tableName)
                        {
                            string rename = $"RENAME TABLE `{_tableName}` TO `{newTableName}`;";
                            using (var cmd = new MySqlCommand(rename, conn, tr))
                                cmd.ExecuteNonQuery();
                            _tableName = newTableName;
                        }

                        // 2: Удаление колонок
                        foreach (var oldCol in _originalColumns)
                        {
                            if (!currentColumns.Exists(c => c.ColumnName == oldCol.ColumnName))
                            {
                                string drop = $"ALTER TABLE `{_tableName}` DROP COLUMN `{oldCol.ColumnName}`;";
                                using (var cmd = new MySqlCommand(drop, conn, tr))
                                    cmd.ExecuteNonQuery();
                            }
                        }

                        // 3: Добавление новых колонок
                        foreach (var col in currentColumns)
                        {
                            if (!col.IsExisting)
                            {
                                string nullClause = col.AllowNull ? "NULL" : "NOT NULL";
                                string add = $"ALTER TABLE `{_tableName}` ADD `{col.ColumnName}` {col.DataType} {nullClause};";
                                using (var cmd = new MySqlCommand(add, conn, tr))
                                    cmd.ExecuteNonQuery();
                            }
                        }

                        // 4: Изменение existing колонок (NULL / NOT NULL)
                        foreach (var col in currentColumns)
                        {
                            var original = _originalColumns.Find(o => o.ColumnName == col.ColumnName);
                            if (original != null && original.AllowNull != col.AllowNull)
                            {
                                string nullClause = col.AllowNull ? "NULL" : "NOT NULL";
                                string modify = $"ALTER TABLE `{_tableName}` MODIFY `{col.ColumnName}` {col.DataType} {nullClause};";
                                using (var cmd = new MySqlCommand(modify, conn, tr))
                                    cmd.ExecuteNonQuery();
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
        private void ClearColumnsButton_Click(object sender, RoutedEventArgs e)
        {
            var itemsToRemove = new List<TableColumn>();

            foreach (TableColumn column in ColumnsDataGrid.Items)
            {
                if (!column.IsExisting)
                    itemsToRemove.Add(column);
            }

            foreach (var col in itemsToRemove)
                ColumnsDataGrid.Items.Remove(col);
        }

    }
}
