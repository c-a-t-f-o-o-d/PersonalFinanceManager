using System.Security.Cryptography;
using System.Text;
using PersonalFinanceManager.Core.Common;

namespace PersonalFinanceManager.Data.Security
{
    public sealed class AccountDataProtector
    {
        public byte[]? Protect(string? plaintext)
        {
            if (string.IsNullOrWhiteSpace(plaintext))
            {
                return null;
            }

            byte[] bytes = Encoding.UTF8.GetBytes(plaintext.Trim());

            try
            {
                return ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(bytes);
            }
        }

        public string? Unprotect(byte[]? protectedBytes)
        {
            if (protectedBytes is null || protectedBytes.Length == 0)
            {
                return null;
            }

            byte[] bytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);

            try
            {
                return Encoding.UTF8.GetString(bytes);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(bytes);
            }
        }

        public string LastFour(string? value)
        {
            return TextNormalizer.LastFour(value);
        }

        public string Mask(string? value)
        {
            return TextNormalizer.MaskAccountNumber(value);
        }
    }
}
