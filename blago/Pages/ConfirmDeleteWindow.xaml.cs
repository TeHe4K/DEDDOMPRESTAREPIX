using System;
using System.Collections.Generic;
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
    
    public partial class ConfirmDeleteWindow : Window
    {
        public ConfirmDeleteWindow()
        {
            InitializeComponent();
        }
        private string _tableName;

        public ConfirmDeleteWindow(string tableName)
        {
            InitializeComponent();
            _tableName = tableName;
            txtTableName.Text = tableName;

            // Устанавливаем фокус на поле ввода
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
                this.DialogResult = true;
                this.Close();
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
