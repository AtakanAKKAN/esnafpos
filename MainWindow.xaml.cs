using EsnafPos.Services;
using EsnafPos.ViewModels;
using EsnafPos.Views;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace EsnafPos
{
    public partial class MainWindow : Window
    {
        private readonly SessionService _session;
        private readonly SettingsService _settings;

        public MainWindow(SessionService session, SettingsService settings)
        {
            InitializeComponent();
            _session  = session;
            _settings = settings;
            ApplyAppName();
        }

        private void ApplyAppName()
        {
            var name = _settings.Business.AppName;
            if (string.IsNullOrWhiteSpace(name)) name = "Esnaf POS";
            Title           = name;
            TxtAppName.Text = name;
        }

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);
            ShowLogin();
        }

        private void ShowLogin()
        {
            MainFrame.Visibility = Visibility.Collapsed;
            var loginVm = App.Services.GetRequiredService<LoginViewModel>();
            var loginWindow = new LoginWindow(loginVm);
            loginWindow.Owner = this;
            loginWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            loginWindow.ShowDialog();

            if (_session.IsLoggedIn)
            {
                MainFrame.Visibility = Visibility.Visible;
                TxtCurrentUser.Text = _session.CurrentUsername;
                TxtCurrentRole.Text = _session.IsAdmin ? "Yonetici" : "Kasiyer";

                BtnReports.Visibility = Visibility.Visible;
                BtnAdmin.Visibility = _session.IsAdmin
                    ? Visibility.Visible : Visibility.Collapsed;

                NavigateToTables();
            }
            else
            {
                Application.Current.Shutdown();
            }
        }

        private void NavigateToTables()
        {
            var vm = App.Services.GetRequiredService<TablesViewModel>();
            MainFrame.Navigate(new TablesPage(vm));
        }

        private void BtnTables_Click(object sender, RoutedEventArgs e)
            => NavigateToTables();

        private void BtnReports_Click(object sender, RoutedEventArgs e)
        {
            var page = App.Services.GetRequiredService<ReportsPage>();
            MainFrame.Navigate(page);
        }

        private void BtnAdmin_Click(object sender, RoutedEventArgs e)
        {
            // Admin sayfasından dönünce uygulama adı değişmiş olabilir
            ApplyAppName();
            var page = App.Services.GetRequiredService<AdminPage>();
            MainFrame.Navigate(page);
        }

        private void Window_ManipulationBoundaryFeedback(object sender, System.Windows.Input.ManipulationBoundaryFeedbackEventArgs e)
        {
            e.Handled = true;
        }

        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Çıkış yapmak istediğinizden emin misiniz?",
                "Çıkış",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                BtnReports.Visibility = Visibility.Collapsed;
                BtnAdmin.Visibility   = Visibility.Collapsed;
                TxtCurrentUser.Text   = "";
                TxtCurrentRole.Text   = "";

                _session.Logout();
                ShowLogin();
            }
        }
    }
}
