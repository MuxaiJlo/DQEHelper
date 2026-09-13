using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace DQEHelper.Services
{
    public static class SecureStorage
    {
        // Путь: C:\Users\<User>\AppData\Local\DQEHelper\coralogix.dat
        private static readonly string AppDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DQEHelper");
        private static readonly string CookieFilePath = Path.Combine(AppDataFolder, "coralogix.dat");

        public static void SaveCoralogixCookie(string cookieValue)
        {
            if (!Directory.Exists(AppDataFolder))
            {
                Directory.CreateDirectory(AppDataFolder);
            }

            // Шифруем данные с привязкой к текущему пользователю Windows (CurrentUser)
            byte[] plainBytes = Encoding.UTF8.GetBytes(cookieValue);
            byte[] encryptedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
            
            File.WriteAllBytes(CookieFilePath, encryptedBytes);
        }

        public static string GetCoralogixCookie()
        {
            if (!File.Exists(CookieFilePath))
                return string.Empty;

            try
            {
                byte[] encryptedBytes = File.ReadAllBytes(CookieFilePath);
                // Расшифровываем
                byte[] plainBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch (CryptographicException)
            {
                // Если файл поврежден или открывается на другом ПК
                return string.Empty;
            }
        }

        public static bool HasValidCookie()
        {
            return !string.IsNullOrWhiteSpace(GetCoralogixCookie());
        }
    }
}