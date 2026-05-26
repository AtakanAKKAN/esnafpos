using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EsnafPos.Data;
using EsnafPos.Helpers;
using EsnafPos.Models;
using EsnafPos.Network;
using EsnafPos.Services;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;

namespace EsnafPos.ViewModels
{
    public partial class LoginViewModel : BaseViewModel
    {
        private readonly AppDbContext _db;
        private readonly SessionService _session;

        public ObservableCollection<User> Users { get; } = new();

        [ObservableProperty] private User? _selectedUser;
        [ObservableProperty] private string _pinDisplay = "";
        [ObservableProperty] private string _errorMessage = "";

        private string _pinRaw = "";

        public event Action? LoginSuccessful;

        public LoginViewModel(AppDbContext db, SessionService session)
        {
            _db = db;
            _session = session;
        }

        public async Task LoadUsers()
        {
            Users.Clear();

            if (App.Client != null)
            {
                // İstemci modunda: kullanıcıları sunucudan çek
                var dtos = await App.Client.GetUsersAsync();
                foreach (var dto in dtos)
                {
                    Users.Add(new User
                    {
                        Id       = dto.Id,
                        Username = dto.Username,
                        Role     = Enum.TryParse<UserRole>(dto.Role, out var r) ? r : UserRole.Cashier,
                        IsActive = true
                    });
                }
                return;
            }

            var users = await _db.Users
                .Where(u => u.IsActive)
                .OrderBy(u => u.Role)
                .ThenBy(u => u.Username)
                .ToListAsync();

            foreach (var u in users) Users.Add(u);
        }

        public void SelectUser(User user)
        {
            SelectedUser = user;
            _pinRaw = "";
            PinDisplay = "";
            ErrorMessage = "";
        }

        public void PressDigit(string digit)
        {
            if (_pinRaw.Length >= 8) return;
            _pinRaw += digit;
            PinDisplay = new string('●', _pinRaw.Length);
        }

        public void PressBackspace()
        {
            if (_pinRaw.Length == 0) return;
            _pinRaw = _pinRaw[..^1];
            PinDisplay = new string('●', _pinRaw.Length);
        }

        public void PressClear()
        {
            _pinRaw = "";
            PinDisplay = "";
            ErrorMessage = "";
        }

        public void CancelUser()
        {
            SelectedUser = null;
            _pinRaw = "";
            PinDisplay = "";
            ErrorMessage = "";
        }

        [RelayCommand]
        public async Task Login()
        {
            if (SelectedUser == null || string.IsNullOrEmpty(_pinRaw))
            {
                ErrorMessage = "PIN giriniz.";
                return;
            }

            IsBusy = true;
            ErrorMessage = "";

            try
            {
                var pinHash = PinHelper.HashPin(_pinRaw);

                if (App.Client != null)
                {
                    // İstemci modunda: sunucuda doğrula
                    var result = await App.Client.LoginAsync(SelectedUser.Username, pinHash);
                    if (result == null)
                    {
                        ErrorMessage = "PIN hatali!";
                        PressClear();
                        return;
                    }
                    // Kullanıcıyı session'a al
                    var apiUser = new User
                    {
                        Id       = SelectedUser.Id,
                        Username = result.Username,
                        Role     = Enum.TryParse<UserRole>(result.Role, out var r) ? r : UserRole.Cashier,
                        IsActive = true
                    };
                    _session.Login(apiUser);
                    LoginSuccessful?.Invoke();
                    return;
                }

                var user = await Task.Run(() =>
                    _db.Users.FirstOrDefault(u =>
                        u.Id == SelectedUser.Id &&
                        u.PinHash == pinHash &&
                        u.IsActive));

                if (user == null)
                {
                    ErrorMessage = "PIN hatali!";
                    PressClear();
                    return;
                }

                _session.Login(user);
                LoginSuccessful?.Invoke();
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
