using EsnafPos.Models;
using System.Windows;
using System.Windows.Controls;

namespace EsnafPos.Views
{
    public partial class EditCategoryDialog : Window
    {
        public string CategoryName { get; private set; } = "";
        public string Channel      { get; private set; } = "Masa";
        public bool   Saved        { get; private set; } = false;

        public EditCategoryDialog(Category category)
        {
            InitializeComponent();
            RunCategoryName.Text = category.Name;
            TxtName.Text         = category.Name;

            // Dogru channel secenegini sec
            var channel = string.IsNullOrWhiteSpace(category.Channel) ? "Masa" : category.Channel;
            foreach (ComboBoxItem item in CboChannel.Items)
            {
                if (item.Tag?.ToString() == channel)
                {
                    CboChannel.SelectedItem = item;
                    break;
                }
            }
            if (CboChannel.SelectedItem == null)
                CboChannel.SelectedIndex = 0;

            Loaded += (s, e) => TxtName.Focus();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            CategoryName = TxtName.Text.Trim();
            Channel      = (CboChannel.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Masa";
            Saved        = true;
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
            => DialogResult = false;
    }
}
