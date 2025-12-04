using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace blago.Pages
{
    public partial class ConfirmDeleteWindow : Window
    {
        private string _tableName;

        public ConfirmDeleteWindow()
        {
            InitializeComponent();
        }

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

            if (input == _tableName)
            {
                btnDelete.IsEnabled = true;
                txtValidationMessage.Visibility = Visibility.Collapsed;
                btnDelete.Background = Brushes.Red;
            }
            else if (string.IsNullOrEmpty(input))
            {
                btnDelete.IsEnabled = false;
                txtValidationMessage.Visibility = Visibility.Collapsed;
                btnDelete.Background = Brushes.LightCoral;
            }
            else
            {
                btnDelete.IsEnabled = false;
                txtValidationMessage.Text = "Название не совпадает";
                txtValidationMessage.Visibility = Visibility.Visible;
                btnDelete.Background = Brushes.LightCoral;
            }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (txtConfirmTableName.Text.Trim() == _tableName)
            {
                DialogResult = true;
                Close();
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
