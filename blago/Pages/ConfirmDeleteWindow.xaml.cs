using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace blago.Pages
{
    public partial class ConfirmDeleteWindow : Window
    {
        private readonly string _tableName;

        public ConfirmDeleteWindow(string tableName)
        {
            InitializeComponent();
            _tableName = tableName;
            txtTableName.Text = tableName;
            Loaded += (s, e) => txtConfirmTableName.Focus();
        }

        private void TxtConfirmTableName_TextChanged(object sender, TextChangedEventArgs e)
        {
            string input = txtConfirmTableName.Text.Trim();
            bool match = input == _tableName;

            btnDelete.IsEnabled = match;
            txtValidationMessage.Visibility = match || input == "" ? Visibility.Collapsed : Visibility.Visible;
            txtValidationMessage.Text = match ? "" : "Название не совпадает";
            btnDelete.Background = match ? Brushes.Red : Brushes.LightCoral;
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = txtConfirmTableName.Text.Trim() == _tableName;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
