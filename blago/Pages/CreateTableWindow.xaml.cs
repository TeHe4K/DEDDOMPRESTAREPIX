using blago.Classes;
using System;
using System.Collections.Generic;
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

            // Добавляем несколько столбцов по умолчанию
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
            if (string.IsNullOrWhiteSpace(txtColumnName.Text))
            {
                MessageBox.Show("Введите имя столбца", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedItem = cmbDataType.SelectedItem as ComboBoxItem;
            if (selectedItem == null) return;

            string dataType = selectedItem.Content.ToString();

            dgColumns.Items.Add(new ColumnDefinition
            {
                ColumnName = txtColumnName.Text.Trim(),
                DataType = dataType,
                AllowNull = true
            });

            // Очищаем поле для ввода
            txtColumnName.Text = "Column" + (dgColumns.Items.Count + 1);
        }

        private void DeleteColumn_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;

            var column = button.DataContext as ColumnDefinition;
            if (column != null)
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

                // Строим SQL запрос для создания таблицы
                StringBuilder sqlBuilder = new StringBuilder();
                sqlBuilder.AppendLine($"CREATE TABLE [{tableName}] (");

                List<string> columnDefinitions = new List<string>();
                List<string> primaryKeyColumns = new List<string>();

                foreach (ColumnDefinition column in dgColumns.Items)
                {
                    string columnDef = $"[{column.ColumnName}] {column.DataType}";

                    if (!column.AllowNull)
                        columnDef += " NOT NULL";

                    if (column.IsIdentity)
                        columnDef += " IDENTITY(1,1)";

                    columnDefinitions.Add(columnDef);

                    if (column.IsPrimaryKey)
                        primaryKeyColumns.Add($"[{column.ColumnName}]");
                }

                sqlBuilder.AppendLine(string.Join(",\n", columnDefinitions));

                // Добавляем первичный ключ, если есть
                if (primaryKeyColumns.Count > 0)
                {
                    sqlBuilder.AppendLine($",CONSTRAINT [PK_{tableName}] PRIMARY KEY CLUSTERED (");
                    sqlBuilder.AppendLine(string.Join(", ", primaryKeyColumns));
                    sqlBuilder.AppendLine(")");
                }

                sqlBuilder.AppendLine(");");

                string sqlQuery = sqlBuilder.ToString();

                // Выполняем SQL запрос
                using (SqlConnection conn = DatabaseManager.CreateNewConnection())
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(sqlQuery, conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }

                MessageBox.Show($"Таблица '{tableName}' успешно создана!", "Успех",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                this.DialogResult = true;
                this.Close();
            }
            catch (SqlException sqlEx)
            {
                MessageBox.Show($"Ошибка SQL: {sqlEx.Message}", "Ошибка создания таблицы",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка создания таблицы",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
