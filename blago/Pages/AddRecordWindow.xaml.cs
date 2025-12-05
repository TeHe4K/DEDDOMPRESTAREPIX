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
    /// <summary>
    /// Логика взаимодействия для AddRecordWindow.xaml
    /// </summary>
    public partial class AddRecordWindow : Window
    {
        public class ColumnInfo
        {
            public string Name { get; set; }
            public string DataType { get; set; }
            public int MaxLength { get; set; }
            public bool IsNullable { get; set; }
            public bool IsIdentity { get; set; }
            public bool IsPrimaryKey { get; set; }
        }

        private string _tableName;
        private List<ColumnInfo> _columns = new List<ColumnInfo>();
        private Dictionary<string, UIElement> _inputControls = new Dictionary<string, UIElement>(); // Изменено на UIElement

        public AddRecordWindow(string tableName)
        {
            InitializeComponent();
            _tableName = tableName;
            
            TableNameText.Text = tableName;
            LoadTableColumns();
        }

        private void LoadTableColumns()
        {
            try
            {
                string query = @"
                    SELECT 
                        c.name AS ColumnName,
                        t.name AS DataType,
                        c.max_length AS MaxLength,
                        c.is_nullable AS IsNullable,
                        c.is_identity AS IsIdentity,
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
                            _columns.Clear();
                            FieldsPanel.Children.Clear();
                            _inputControls.Clear();
                            
                            int nonIdentityCount = 0;
                            
                            while (reader.Read())
                            {
                                var column = new ColumnInfo
                                {
                                    Name = reader["ColumnName"].ToString(),
                                    DataType = reader["DataType"].ToString(),
                                    MaxLength = Convert.ToInt32(reader["MaxLength"]),
                                    IsNullable = Convert.ToBoolean(reader["IsNullable"]),
                                    IsIdentity = Convert.ToBoolean(reader["IsIdentity"]),
                                    IsPrimaryKey = Convert.ToBoolean(reader["IsPrimaryKey"])
                                };
                                
                                _columns.Add(column);
                                
                                if (!column.IsIdentity)
                                {
                                    nonIdentityCount++;
                                    AddFieldToPanel(column);
                                }
                            }
                            
                            ColumnsCountText.Text = $"Столбцов для заполнения: {nonIdentityCount}";
                            
                            if (nonIdentityCount == 0)
                            {
                                FieldsPanel.Children.Add(new TextBlock
                                {
                                    Text = "В этой таблице нет столбцов для заполнения (все столбцы IDENTITY).",
                                    Foreground = Brushes.Gray,
                                    FontStyle = FontStyles.Italic,
                                    Margin = new Thickness(0, 10, 0, 0)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки столбцов таблицы: {ex.Message}", 
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                this.Close();
            }
        }

        private void AddFieldToPanel(ColumnInfo column)
        {
            Border fieldBorder = new Border
            {
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(0, 5, 0, 10),
                Margin = new Thickness(0, 0, 0, 5)
            };
            
            StackPanel fieldPanel = new StackPanel();
            
            // Заголовок поля
            StackPanel headerPanel = new StackPanel { Orientation = Orientation.Horizontal };
            
            TextBlock nameText = new TextBlock
            {
                Text = column.Name,
                FontWeight = FontWeights.Bold,
                FontSize = 13
            };
            
            if (column.IsPrimaryKey)
            {
                nameText.Text += " (PK)";
                nameText.Foreground = Brushes.Blue;
            }
            
            headerPanel.Children.Add(nameText);
            
            TextBlock typeText = new TextBlock
            {
                Text = $" [{column.DataType}]",
                FontSize = 11,
                Foreground = Brushes.Gray,
                Margin = new Thickness(5, 0, 0, 0)
            };
            
            headerPanel.Children.Add(typeText);
            
            if (!column.IsNullable)
            {
                TextBlock requiredText = new TextBlock
                {
                    Text = " *",
                    FontSize = 11,
                    Foreground = Brushes.Red,
                    FontWeight = FontWeights.Bold
                };
                
                headerPanel.Children.Add(requiredText);
            }
            
            fieldPanel.Children.Add(headerPanel);
            
            // Поле ввода
            UIElement inputControl = CreateInputControl(column);
            _inputControls[column.Name] = inputControl;
            
            fieldPanel.Children.Add(inputControl);
            
            fieldBorder.Child = fieldPanel;
            FieldsPanel.Children.Add(fieldBorder);
        }

        private UIElement CreateInputControl(ColumnInfo column)
        {
            string dataType = column.DataType.ToLower();
            
            if (dataType.Contains("int") || dataType.Contains("decimal") || dataType.Contains("numeric"))
            {
                TextBox textBox = new TextBox
                {
                    FontSize = 13,
                    Padding = new Thickness(5),
                    Margin = new Thickness(0, 5, 0, 0)
                };
                
                textBox.PreviewTextInput += (s, e) =>
                {
                    if (!char.IsDigit(e.Text, 0) && e.Text != "." && e.Text != "-")
                        e.Handled = true;
                };
                
                return textBox;
            }
            else if (dataType.Contains("date") || dataType.Contains("time"))
            {
                if (dataType.Contains("time") && !dataType.Contains("date"))
                {
                    TextBox timeTextBox = new TextBox
                    {
                        Text = DateTime.Now.ToString("HH:mm:ss"),
                        FontSize = 13,
                        Width = 150,
                        Margin = new Thickness(0, 5, 0, 0),
                        ToolTip = "Время в формате HH:mm:ss"
                    };
                    
                    return timeTextBox;
                }
                else
                {
                    DatePicker datePicker = new DatePicker
                    {
                        FontSize = 13,
                        Width = 200,
                        Margin = new Thickness(0, 5, 0, 0),
                        SelectedDate = DateTime.Now
                    };
                    
                    return datePicker;
                }
            }
            else if (dataType.Contains("bit"))
            {
                CheckBox checkBox = new CheckBox
                {
                    Content = "Да",
                    FontSize = 13,
                    Margin = new Thickness(0, 5, 0, 0),
                    IsChecked = false
                };
                
                return checkBox;
            }
            else if (dataType.Contains("uniqueidentifier"))
            {
                StackPanel guidPanel = new StackPanel { Orientation = Orientation.Horizontal };
                
                TextBox guidTextBox = new TextBox
                {
                    Text = Guid.NewGuid().ToString(),
                    FontSize = 13,
                    Padding = new Thickness(5),
                    Margin = new Thickness(0, 5, 10, 0),
                    Width = 300,
                    IsReadOnly = true
                };
                
                Button generateButton = new Button
                {
                    Content = "Сгенерировать",
                    FontSize = 11,
                    Margin = new Thickness(0, 5, 0, 0),
                    Padding = new Thickness(5, 2, 5, 2),
                    Background = Brushes.LightBlue
                };
                
                generateButton.Click += (s, e) =>
                {
                    guidTextBox.Text = Guid.NewGuid().ToString();
                };
                
                guidPanel.Children.Add(guidTextBox);
                guidPanel.Children.Add(generateButton);
                
                return guidPanel;
            }
            else
            {
                TextBox textBox = new TextBox
                {
                    FontSize = 13,
                    Padding = new Thickness(5),
                    Margin = new Thickness(0, 5, 0, 0)
                };
                
                if (column.MaxLength > 0 && column.MaxLength < 8000)
                {
                    textBox.MaxLength = column.MaxLength;
                    textBox.ToolTip = $"Максимальная длина: {column.MaxLength} символов";
                }
                
                if (dataType.Contains("max") || column.MaxLength == -1)
                {
                    textBox.AcceptsReturn = true;
                    textBox.TextWrapping = TextWrapping.Wrap;
                    textBox.Height = 100;
                    textBox.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                }
                
                return textBox;
            }
        }

        private object GetControlValue(UIElement control)
        {
            if (control == null)
                return null;

            // Проверяем тип элемента
            if (control is TextBox textBox)
            {
                return textBox.Text;
            }
            else if (control is CheckBox checkBox)
            {
                return checkBox.IsChecked ?? false;
            }
            else if (control is DatePicker datePicker)
            {
                return datePicker.SelectedDate;
            }
            else if (control is StackPanel stackPanel) // Безопасное преобразование
            {
                // Обрабатываем StackPanel для GUID
                foreach (var child in stackPanel.Children)
                {
                    if (child is TextBox childTextBox)
                    {
                        return childTextBox.Text;
                    }
                }
            }
            
            return null;
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!ValidateRequiredFields())
                    return;

                string insertQuery = BuildInsertQuery();
                
                using (SqlConnection conn = DatabaseManager.CreateNewConnection())
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(insertQuery, conn))
                    {
                        AddParametersToCommand(cmd);
                        
                        int rowsAffected = cmd.ExecuteNonQuery();
                        
                        if (rowsAffected > 0)
                        {
                            MessageBox.Show($"Запись успешно добавлена в таблицу '{_tableName}'", 
                                "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                            this.DialogResult = true;
                            this.Close();
                        }
                        else
                        {
                            MessageBox.Show("Не удалось добавить запись", 
                                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
            catch (SqlException sqlEx)
            {
                MessageBox.Show($"Ошибка SQL при добавлении записи: {sqlEx.Message}", 
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", 
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool ValidateRequiredFields()
        {
            foreach (var column in _columns)
            {
                if (!column.IsNullable && !column.IsIdentity && _inputControls.ContainsKey(column.Name))
                {
                    object value = GetControlValue(_inputControls[column.Name]);
                    
                    if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
                    {
                        MessageBox.Show($"Поле '{column.Name}' обязательно для заполнения", 
                            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return false;
                    }
                }
            }
            
            return true;
        }

        private string BuildInsertQuery()
        {
            StringBuilder columnsBuilder = new StringBuilder();
            StringBuilder valuesBuilder = new StringBuilder();
            
            bool firstColumn = true;
            
            foreach (var column in _columns)
            {
                if (!column.IsIdentity && _inputControls.ContainsKey(column.Name))
                {
                    object value = GetControlValue(_inputControls[column.Name]);
                    
                    if (column.IsNullable && (value == null || string.IsNullOrWhiteSpace(value.ToString())))
                        continue;
                    
                    if (!firstColumn)
                    {
                        columnsBuilder.Append(", ");
                        valuesBuilder.Append(", ");
                    }
                    
                    columnsBuilder.Append($"[{column.Name}]");
                    valuesBuilder.Append($"@{column.Name}");
                    
                    firstColumn = false;
                }
            }
            
            return $"INSERT INTO [{_tableName}] ({columnsBuilder}) VALUES ({valuesBuilder})";
        }

        private void AddParametersToCommand(SqlCommand cmd)
        {
            foreach (var column in _columns)
            {
                if (!column.IsIdentity && _inputControls.ContainsKey(column.Name))
                {
                    object value = GetControlValue(_inputControls[column.Name]);
                    
                    if (column.IsNullable && (value == null || string.IsNullOrWhiteSpace(value.ToString())))
                    {
                        cmd.Parameters.AddWithValue($"@{column.Name}", DBNull.Value);
                    }
                    else
                    {
                        object convertedValue = ConvertValueToType(value, column.DataType);
                        cmd.Parameters.AddWithValue($"@{column.Name}", convertedValue);
                    }
                }
            }
        }

        private object ConvertValueToType(object value, string dataType)
        {
            if (value == null)
                return DBNull.Value;
            
            string stringValue = value.ToString();
            
            if (string.IsNullOrWhiteSpace(stringValue))
                return DBNull.Value;
            
            dataType = dataType.ToLower();
            
            try
            {
                if (dataType.Contains("int"))
                {
                    return Convert.ToInt32(value);
                }
                else if (dataType.Contains("decimal") || dataType.Contains("numeric"))
                {
                    return Convert.ToDecimal(value);
                }
                else if (dataType.Contains("bit"))
                {
                    return Convert.ToBoolean(value);
                }
                else if (dataType.Contains("date") || dataType.Contains("time"))
                {
                    if (value is DateTime dateTime)
                        return dateTime;
                    
                    return Convert.ToDateTime(value);
                }
                else if (dataType.Contains("uniqueidentifier"))
                {
                    return new Guid(stringValue);
                }
                else
                {
                    return stringValue;
                }
            }
            catch
            {
                return stringValue;
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            // Очищаем все поля ввода
            foreach (var control in _inputControls.Values)
            {
                ClearControlValue(control);
            }
        }

        private void ClearControlValue(UIElement control)
        {
            if (control is TextBox textBox)
            {
                textBox.Text = "";
            }
            else if (control is CheckBox checkBox)
            {
                checkBox.IsChecked = false;
            }
            else if (control is DatePicker datePicker)
            {
                datePicker.SelectedDate = DateTime.Now;
            }
            else if (control is StackPanel stackPanel)
            {
                foreach (var child in stackPanel.Children)
                {
                    if (child is TextBox childTextBox)
                    {
                        childTextBox.Text = Guid.NewGuid().ToString();
                    }
                    else if (child is DatePicker childDatePicker)
                    {
                        childDatePicker.SelectedDate = DateTime.Now;
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
