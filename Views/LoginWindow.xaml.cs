using EsnafPos.Models;
using EsnafPos.Services;
using EsnafPos.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;

namespace EsnafPos.Views
{
    public partial class LoginWindow : Window
    {
        private readonly LoginViewModel _vm;

        public LoginWindow(LoginViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            DataContext = vm;

            _vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(_vm.PinDisplay))
                    TxtPinDisplay.Text = _vm.PinDisplay;

                if (e.PropertyName == nameof(_vm.ErrorMessage))
                {
                    TxtPinError.Text = _vm.ErrorMessage;
                    TxtPinError.Visibility = string.IsNullOrEmpty(_vm.ErrorMessage)
                        ? Visibility.Collapsed : Visibility.Visible;
                }
            };

            _vm.LoginSuccessful += () => DialogResult = true;

            Loaded += async (s, e) =>
            {
                var settings = App.Services.GetRequiredService<SettingsService>();
                var appName  = settings.Business.AppName;
                Title        = $"{appName} - Giriş";
                TxtAppName.Text = appName;

                await _vm.LoadUsers();
                IcUsers.ItemsSource = _vm.Users;
            };
        }

        private void BtnUser_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is User user)
            {
                _vm.SelectUser(user);
                TxtSelectedUser.Text = user.Username;
                TxtUserInitial.Text = user.Username[0].ToString().ToUpper();
                TxtSelectedRole.Text = user.Role == Models.UserRole.Admin ? "Yonetici" : "Kasiyer";
                TxtPinDisplay.Text = "";
                TxtPinError.Visibility = Visibility.Collapsed;

                PanelUserSelect.Visibility = Visibility.Collapsed;
                PanelPin.Visibility = Visibility.Visible;
            }
        }

        private void BtnDigit_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
                _vm.PressDigit(btn.Content.ToString()!);
        }

        private void BtnBackspace_Click(object sender, RoutedEventArgs e)
            => _vm.PressBackspace();

        private async void BtnLogin_Click(object sender, RoutedEventArgs e)
            => await _vm.LoginCommand.ExecuteAsync(null);

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            _vm.CancelUser();
            PanelPin.Visibility = Visibility.Collapsed;
            PanelUserSelect.Visibility = Visibility.Visible;
        }
    }
}
