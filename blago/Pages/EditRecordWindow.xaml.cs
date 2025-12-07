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
    public partial class EditRecordWindow : Window
    {
        private readonly DataRow _originalRow;
        private readonly string _tableName;

        private readonly Dictionary<string, object> _originalValues = new Dictionary<string, object>();
        private readonly Dictionary<string, TextBox> _fields = new Dictionary<string, TextBox>();

        public EditRecordWindow(string tableName, DataRow originalRow)
        {
            InitializeComponent();

            if (originalRow == null || string.IsNullOrEmpty(tableName))
            {
                MessageBox.Show("Ошибка инициализации окна", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                DialogResult = false;
                Close();
                return;
            }

            _tableName = tableName;
            _originalRow = originalRow;

            foreach (DataColumn col in originalRow.Table.Columns)
                _originalValues[col.ColumnName] = originalRow[col.ColumnName];

            InitializeUI();
        }

        private void InitializeUI()
        {
            TableNameText.Text = "Таблица: " + _tableName;
            CreateFields();
            RecordInfoText.Text = "Редактируемых столбцов: " + _fields.Count;
        }

        private void CreateFields()
        {
            FieldsPanel.Children.Clear();
            _fields.Clear();

            foreach (DataColumn column in _originalRow.Table.Columns)
            {
                string name = column.ColumnName;
                object val = _originalValues[name];

                StackPanel panel = new StackPanel
                {
                    Margin = new Thickness(0, 10, 0, 10)
                };

                panel.Children.Add(new TextBlock
                {
                    Text = name + " [" + column.DataType.Name + "]",
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 5)
                });

                TextBox box = new TextBox
                {
                    Text = (val == DBNull.Value || val == null) ? "" : val.ToString(),
                    Height = 25,
                    Padding = new Thickness(3)
                };

                _fields[name] = box;
                panel.Children.Add(box);

                panel.Children.Add(new TextBlock
                {
                    Text = "Текущее: " + ((val == DBNull.Value) ? "NULL" : (val ?? "пусто")),
                    FontStyle = FontStyles.Italic,
                    Foreground = System.Windows.Media.Brushes.Gray,
                    Margin = new Thickness(0, 2, 0, 0),
                    FontSize = 11
                });

                FieldsPanel.Children.Add(panel);
            }
        }

        private List<string> GetPrimaryKeys()
        {
            List<string> keys = new List<string>();

            try
            {
                string query =
                    "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE " +
                    "WHERE OBJECTPROPERTY(OBJECT_ID(CONSTRAINT_SCHEMA + '.' + CONSTRAINT_NAME), 'IsPrimaryKey') = 1 " +
                    "AND TABLE_NAME = @t ORDER BY ORDINAL_POSITION";

                using (SqlConnection conn = DatabaseManager.CreateNewConnection())
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@t", _tableName);

                        using (SqlDataReader r = cmd.ExecuteReader())
                        {
                            while (r.Read())
                                keys.Add(r["COLUMN_NAME"].ToString());
                        }
                    }
                }
            }
            catch { }

            if (keys.Count == 0)
                foreach (DataColumn c in _originalRow.Table.Columns)
                    keys.Add(c.ColumnName);

            return keys;
        }

        private object ConvertValue(string text, Type type)
        {
            if (string.IsNullOrEmpty(text))
                return DBNull.Value;

            try
            {
                if (type == typeof(string)) return text;
                if (type == typeof(int)) return int.Parse(text);
                if (type == typeof(long)) return long.Parse(text);
                if (type == typeof(decimal)) return decimal.Parse(text);
                if (type == typeof(double)) return double.Parse(text);
                if (type == typeof(float)) return float.Parse(text);

                if (type == typeof(bool))
                {
                    string lower = text.ToLower();
                    if (lower == "true" || lower == "1") return true;
                    if (lower == "false" || lower == "0") return false;
                    return bool.Parse(text);
                }

                if (type == typeof(DateTime)) return DateTime.Parse(text);

                return text;
            }
            catch
            {
                return text;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            List<string> pks = GetPrimaryKeys();
            Dictionary<string, object> newValues = new Dictionary<string, object>();

            bool changed = false;

            foreach (DataColumn column in _originalRow.Table.Columns)
            {
                string name = column.ColumnName;
                string newText = _fields[name].Text.Trim();

                object original = _originalValues[name];
                string originalText = (original == DBNull.Value || original == null) ? "" : original.ToString();

                if (newText != originalText)
                {
                    newValues[name] = ConvertValue(newText, column.DataType);
                    changed = true;
                }
            }

            if (!changed)
            {
                MessageBox.Show("Нет изменений", "Информация",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            StringBuilder set = new StringBuilder();
            List<SqlParameter> parameters = new List<SqlParameter>();

            foreach (KeyValuePair<string, object> kv in newValues)
            {
                if (set.Length > 0) set.Append(", ");
                set.Append("[" + kv.Key + "] = @n_" + kv.Key);

                SqlParameter p = new SqlParameter("@n_" + kv.Key, kv.Value ?? DBNull.Value);
                parameters.Add(p);
            }

            StringBuilder where = new StringBuilder();

            foreach (string pk in pks)
            {
                if (where.Length > 0) where.Append(" AND ");

                where.Append("[" + pk + "] = @o_" + pk);

                object originalPk = _originalValues[pk];
                SqlParameter pp = new SqlParameter("@o_" + pk, originalPk ?? DBNull.Value);
                parameters.Add(pp);
            }

            string query = "UPDATE [" + _tableName + "] SET " + set + " WHERE " + where;

            try
            {
                using (SqlConnection conn = DatabaseManager.CreateNewConnection())
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        foreach (SqlParameter p in parameters)
                            cmd.Parameters.Add(p);

                        int affected = cmd.ExecuteNonQuery();

                        if (affected > 0)
                        {
                            MessageBox.Show("Запись успешно обновлена", "Успех",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                            DialogResult = true;
                            Close();
                        }
                        else
                        {
                            MessageBox.Show("Не удалось обновить запись", "Ошибка",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                MessageBox.Show("Ошибка SQL: " + ex.Message, "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка: " + ex.Message, "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (KeyValuePair<string, TextBox> kv in _fields)
            {
                object original = _originalValues[kv.Key];
                kv.Value.Text = (original == DBNull.Value || original == null) ? "" : original.ToString();
            }

            MessageBox.Show("Значения восстановлены", "Сброс",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
