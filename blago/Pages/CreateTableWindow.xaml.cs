using blago.Classes;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace blago.Pages
{
    public partial class CreateTableWindow : Window
    {
        public class ColumnDefinition
        {
            public string ColumnName { get; set; }
            public string DataType { get; set; }
            public bool AllowNull { get; set; } = true;
            public bool IsIdentity { get; set; } = false;
            public bool IsPrimaryKey { get; set; } = false;
        }

        public CreateTableWindow()
        {
            InitializeComponent();
            LoadDefaultColumns();
        }

        private void LoadDefaultColumns()
        {
            dgColumns.Items.Add(new ColumnDefinition
            {
                ColumnName = "ID",
                DataType = "int",
                AllowNull = false,
                IsIdentity = true,
                IsPrimaryKey = true
            });

            dgColumns.Items.Add(new ColumnDefinition
            {
                ColumnName = "Name",
                DataType = "nvarchar(100)",
                AllowNull = false
            });

            dgColumns.Items.Add(new ColumnDefinition
            {
                ColumnName = "CreatedDate",
                DataType = "datetime",
                AllowNull = false
            });
        }

        private void AddColumn_Click(object sender, RoutedEventArgs e)
        {
            string name = txtColumnName.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                ShowError("Введите имя столбца");
                return;
            }

            var item = cmbDataType.SelectedItem as ComboBoxItem;
            if (item == null)
            {
                ShowError("Выберите тип данных");
                return;
            }


            dgColumns.Items.Add(new ColumnDefinition
            {
                ColumnName = name,
                DataType = item.Content.ToString(),
                AllowNull = true
            });

            txtColumnName.Text = $"Column{dgColumns.Items.Count + 1}";
        }

        private void DeleteColumn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ColumnDefinition column)
                dgColumns.Items.Remove(column);
        }

        private void CreateTable_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateTable())
                return;

            try
            {
                string sql = BuildCreateTableQuery(txtTableName.Text.Trim());

                using (SqlConnection conn = DatabaseManager.CreateNewConnection())
                {
                    conn.Open();
                    new SqlCommand(sql, conn).ExecuteNonQuery();
                }

                MessageBox.Show($"Таблица '{txtTableName.Text}' успешно создана!",
                    "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (SqlException ex)
            {
                ShowError($"Ошибка SQL: {ex.Message}");
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка: {ex.Message}");
            }
        }

        private bool ValidateTable()
        {
            if (string.IsNullOrWhiteSpace(txtTableName.Text))
            {
                ShowError("Введите название таблицы");
                return false;
            }

            if (dgColumns.Items.Count == 0)
            {
                ShowError("Добавьте хотя бы один столбец");
                return false;
            }

            return true;
        }

        private string BuildCreateTableQuery(string tableName)
        {
            var sql = new StringBuilder();
            sql.AppendLine($"CREATE TABLE [{tableName}] (");

            var columnDefs = new List<string>();
            var primaryKeys = new List<string>();

            foreach (ColumnDefinition c in dgColumns.Items)
            {
                var def = new StringBuilder($"[{c.ColumnName}] {c.DataType}");

                if (c.IsIdentity) def.Append(" IDENTITY(1,1)");
                if (!c.AllowNull) def.Append(" NOT NULL");

                columnDefs.Add(def.ToString());

                if (c.IsPrimaryKey)
                    primaryKeys.Add($"[{c.ColumnName}]");
            }

            sql.AppendLine(string.Join(",\n", columnDefs));

            if (primaryKeys.Count > 0)
            {
                sql.AppendLine($", CONSTRAINT [PK_{tableName}] PRIMARY KEY CLUSTERED ({string.Join(", ", primaryKeys)})");
            }

            sql.AppendLine(");");

            return sql.ToString();
        }

        private void ShowError(string msg)
        {
            MessageBox.Show(msg, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
