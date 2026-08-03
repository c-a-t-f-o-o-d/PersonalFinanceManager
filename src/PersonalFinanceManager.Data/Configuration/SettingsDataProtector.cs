using System.Security.Cryptography;
using System.Text;

namespace PersonalFinanceManager.Data.Configuration
{
    internal static class SettingsDataProtector
    {
        public static string Protect(string value)
        {
            ArgumentNullException.ThrowIfNull(value);
            byte[] plainBytes = Encoding.UTF8.GetBytes(value);

            try
            {
                byte[] protectedBytes = ProtectedData.Protect(plainBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
                return Convert.ToBase64String(protectedBytes);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plainBytes);
            }
        }

        public static string Unprotect(string protectedValue)
        {
            if (string.IsNullOrWhiteSpace(protectedValue))
            {
                return string.Empty;
            }

            byte[] protectedBytes = Convert.FromBase64String(protectedValue);
            byte[] plainBytes = ProtectedData.Unprotect(protectedBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);

            try
            {
                return Encoding.UTF8.GetString(plainBytes);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plainBytes);
            }
        }
    }
}
