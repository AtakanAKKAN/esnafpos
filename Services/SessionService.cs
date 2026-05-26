using EsnafPos.Models;

namespace EsnafPos.Services
{
    public class SessionService
    {
        public User? CurrentUser { get; private set; }

        public bool IsLoggedIn => CurrentUser != null;
        public bool IsAdmin => CurrentUser?.Role == UserRole.Admin;
        public string CurrentUsername => CurrentUser?.Username ?? "";

        public void Login(User user)
        {
            CurrentUser = user;
        }

        public void Logout()
        {
            CurrentUser = null;
        }
    }
}