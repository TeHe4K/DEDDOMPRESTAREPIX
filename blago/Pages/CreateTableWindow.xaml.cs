using blago.Classes;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
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
            public bool IsIdentity { get; set; } = false;   // AUTO_INCREMENT
            public bool IsPrimaryKey { get; set; } = false;
        }

        public CreateTableWindow()
        {
            InitializeComponent();

            // Дефолтные столбцы
            dgColumns.Items.Add(new ColumnDefinition
            {
                ColumnName = "ID",
                DataType = "INT",
                AllowNull = false,
                IsIdentity = true,
                IsPrimaryKey = true
            });

            dgColumns.Items.Add(new ColumnDefinition
            {
                ColumnName = "Name",
                DataType = "VARCHAR(100)",
                AllowNull = false
            });

            dgColumns.Items.Add(new ColumnDefinition
            {
                ColumnName = "CreatedDate",
                DataType = "DATETIME",
                AllowNull = false
            });
        }

        private void AddColumn_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtColumnName.Text))
            {
                MessageBox.Show("Введите имя столбца", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!(cmbDataType.SelectedItem is ComboBoxItem selectedItem))
                return;

            string dataType = selectedItem.Content.ToString();

            dgColumns.Items.Add(new ColumnDefinition
            {
                ColumnName = txtColumnName.Text.Trim(),
                DataType = dataType,
                AllowNull = true
            });

            txtColumnName.Text = "Column" + (dgColumns.Items.Count + 1);
        }

        private void DeleteColumn_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.DataContext is ColumnDefinition column)
            {
                dgColumns.Items.Remove(column);
            }
        }

        private void CreateTable_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtTableName.Text))
            {
                MessageBox.Show("Введите название таблицы", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (dgColumns.Items.Count == 0)
            {
                MessageBox.Show("Добавьте хотя бы один столбец", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                string tableName = txtTableName.Text.Trim();

                StringBuilder sql = new StringBuilder();
                sql.AppendLine($"CREATE TABLE `{tableName}` (");

                List<string> columns = new List<string>();
                List<string> primaryKeys = new List<string>();

                foreach (ColumnDefinition col in dgColumns.Items)
                {
                    string colDef = $"`{col.ColumnName}` {ConvertToMySqlType(col.DataType)}";

                    if (!col.AllowNull)
                        colDef += " NOT NULL";
                    else
                        colDef += " NULL";

                    if (col.IsIdentity)
                        colDef += " AUTO_INCREMENT";

                    columns.Add(colDef);

                    if (col.IsPrimaryKey)
                        primaryKeys.Add($"`{col.ColumnName}`");
                }

                sql.AppendLine(string.Join(",\n", columns));

                if (primaryKeys.Count > 0)
                {
                    sql.AppendLine($", PRIMARY KEY ({string.Join(", ", primaryKeys)})");
                }

                sql.AppendLine(") ENGINE=InnoDB DEFAULT CHARSET=utf8;");

                using (MySqlConnection conn = DatabaseManager.CreateNewConnection())
                {
                    conn.Open();
                    using (MySqlCommand cmd = new MySqlCommand(sql.ToString(), conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }

                MessageBox.Show($"Таблица '{tableName}' успешно создана!", "Успех",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (MySqlException mysqlEx)
            {
                MessageBox.Show($"Ошибка MySQL: {mysqlEx.Message}", "Ошибка создания таблицы",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка создания таблицы",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string ConvertToMySqlType(string dataType)
        {
            if (string.IsNullOrEmpty(dataType))
                return "TEXT";

            dataType = dataType.ToLower();

            if (dataType == "int")
                return "INT";

            if (dataType == "nvarchar(100)")
                return "VARCHAR(100)";

            if (dataType == "nvarchar(50)")
                return "VARCHAR(50)";

            if (dataType == "nvarchar(max)")
                return "TEXT";

            if (dataType == "datetime")
                return "DATETIME";

            if (dataType == "date")
                return "DATE";

            if (dataType == "float")
                return "FLOAT";

            if (dataType == "decimal")
                return "DECIMAL(10,2)";

            // fallback
            return dataType.ToUpper();
        }


        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
