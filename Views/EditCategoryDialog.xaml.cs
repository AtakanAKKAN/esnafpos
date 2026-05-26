using EsnafPos.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace EsnafPos.Views
{
    public partial class EditCategoryDialog : Window
    {
        public string CategoryName { get; private set; } = "";
        public string Channel      { get; private set; } = "Masa";
        public bool   Saved        { get; private set; } = false;

        public EditCategoryDialog(Category category, List<AppChannel>? channels = null)
        {
            InitializeComponent();
            RunCategoryName.Text = category.Name;
            TxtName.Text         = category.Name;

            // Kanalları doldur — dinamik liste varsa onu, yoksa fallback
            CboChannel.Items.Clear();
            var channelList = channels?.Where(c => c.IsActive).OrderBy(c => c.DisplayOrder).ToList();
            if (channelList != null && channelList.Count > 0)
            {
                foreach (var ch in channelList)
                    CboChannel.Items.Add(new ComboBoxItem { Content = ch.Name, Tag = ch.Name });
            }
            else
            {
                // Fallback: statik liste
                foreach (var name in new[] { "Masa", "Kurye", "Bekci", "Trendyol", "Diger" })
                    CboChannel.Items.Add(new ComboBoxItem { Content = name, Tag = name });
            }

            // Mevcut kanalı seç
            var currentChannel = string.IsNullOrWhiteSpace(category.Channel) ? "Masa" : category.Channel;
            foreach (ComboBoxItem item in CboChannel.Items)
            {
                if (item.Tag?.ToString() == currentChannel)
                {
                    CboChannel.SelectedItem = item;
                    break;
                }
            }
            if (CboChannel.SelectedItem == null && CboChannel.Items.Count > 0)
                CboChannel.SelectedIndex = 0;

            Loaded += (s, e) => TxtName.Focus();
            TxtName.KeyDown += (s, e) => { if (e.Key == Key.Enter) BtnSave_Click(s, e); };
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
