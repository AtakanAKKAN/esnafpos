using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using System.Security.Cryptography;
using System.Text;

namespace EsnafPos.Helpers
{
    public static class PinHelper
    {
        public static string HashPin(string pin)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(pin));
            return Convert.ToHexString(bytes).ToLower();
        }
    }
}
