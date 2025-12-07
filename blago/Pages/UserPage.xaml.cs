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
        private readonly int _userId;
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
                List<UserManager.TablePermission> permissions =
                    UserManager.GetAllTablePermissions(_userId);

                if (permissions == null || permissions.Count == 0)
                {
                    MessageBox.Show("У данного пользователя нет назначенных прав.");
                    return;
                }

                TableList.Items.Clear();

                foreach (UserManager.TablePermission perm in permissions)
                {
                    if (!perm.CanView)
                        continue;

                    ListBoxItem item = new ListBoxItem();
                    item.Content = perm.TableName;
                    item.FontSize = 18;
                    item.Margin = new Thickness(5);
                    item.Tag = perm;

                    TableList.Items.Add(item);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки таблиц: " + ex.Message);
            }
        }

        private void TableList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ListBoxItem item = TableList.SelectedItem as ListBoxItem;
            if (item == null) return;

            _currentPermission = (UserManager.TablePermission)item.Tag;

            LoadTableData(_currentPermission.TableName);
            UpdateButtonsState();
        }

        private void LoadTableData(string tableName)
        {
            try
            {
                DataTable dt = DatabaseManager.GetTable(tableName);
                if (dt == null)
                {
                    MessageBox.Show("Не удалось загрузить данные таблицы: " + tableName);
                    return;
                }

                TableView.ItemsSource = dt.DefaultView;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки таблицы: " + ex.Message);
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

            string table = _currentPermission.TableName;

            AddRecordWindow window = new AddRecordWindow(table);
            bool? result = window.ShowDialog();

            if (result == true)
                LoadTableData(table);
        }

        private void EditNote(object sender, RoutedEventArgs e)
        {
            if (_currentPermission == null || !_currentPermission.CanEdit)
                return;

            if (TableView.SelectedItem == null)
            {
                MessageBox.Show("Выберите запись для редактирования.");
                return;
            }

            DataRowView rowView = TableView.SelectedItem as DataRowView;
            if (rowView == null)
            {
                MessageBox.Show("Не удалось получить выбранную строку.");
                return;
            }

            DataRow copy = rowView.Row.Table.Clone().NewRow();
            copy.ItemArray = rowView.Row.ItemArray;

            EditRecordWindow wnd = new EditRecordWindow(_currentPermission.TableName, copy);
            bool? result = wnd.ShowDialog();

            if (result == true)
                LoadTableData(_currentPermission.TableName);
        }

        private List<string> GetPrimaryKeys(string tableName)
        {
            List<string> list = new List<string>();

            string sql =
                "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE " +
                "WHERE OBJECTPROPERTY(OBJECT_ID(CONSTRAINT_SCHEMA + '.' + CONSTRAINT_NAME), 'IsPrimaryKey') = 1 " +
                "AND TABLE_NAME = @t ORDER BY ORDINAL_POSITION";

            try
            {
                using (SqlConnection conn = DatabaseManager.CreateNewConnection())
                {
                    conn.Open();
                    SqlCommand cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@t", tableName);

                    SqlDataReader r = cmd.ExecuteReader();
                    while (r.Read())
                        list.Add(r["COLUMN_NAME"].ToString());
                    r.Close();
                }
            }
            catch
            {
            }

            return list;
        }

        private string FormatValue(object val)
        {
            if (val == null || val == DBNull.Value)
                return "NULL";

            if (val is string || val is DateTime)
                return "N'" + val.ToString().Replace("'", "''") + "'";

            if (val is bool)
                return ((bool)val) ? "1" : "0";

            return val.ToString();
        }

        private void DeleteNote(object sender, RoutedEventArgs e)
        {
            if (_currentPermission == null || !_currentPermission.CanDelete)
                return;

            if (TableView.SelectedItem == null)
            {
                MessageBox.Show("Выберите запись для удаления.");
                return;
            }

            DataRowView rowView = TableView.SelectedItem as DataRowView;
            if (rowView == null) return;

            DataRow row = rowView.Row;

            List<string> pk = GetPrimaryKeys(_currentPermission.TableName);
            StringBuilder where = new StringBuilder();

            if (pk.Count == 0)
            {
                foreach (DataColumn col in row.Table.Columns)
                {
                    if (where.Length > 0) where.Append(" AND ");
                    where.Append("[" + col.ColumnName + "] = " + FormatValue(row[col]));
                }
            }
            else
            {
                foreach (string key in pk)
                {
                    if (where.Length > 0) where.Append(" AND ");
                    where.Append("[" + key + "] = " + FormatValue(row[key]));
                }
            }

            if (MessageBox.Show("Удалить запись?", "Подтверждение",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            string sql = "DELETE FROM [" + _currentPermission.TableName + "] WHERE " + where.ToString();

            try
            {
                using (SqlConnection conn = DatabaseManager.CreateNewConnection())
                {
                    conn.Open();
                    SqlCommand cmd = new SqlCommand(sql, conn);

                    int ok = cmd.ExecuteNonQuery();
                    if (ok > 0)
                        LoadTableData(_currentPermission.TableName);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка удаления: " + ex.Message);
            }
        }

        private void Exit(object sender, System.Windows.Input.KeyEventArgs e)
        {
            DatabaseManager.Logout();
            Auth a = new Auth();
            NavigationService.Navigate(a);
        }
    }
}
