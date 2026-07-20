using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace AriyAI.ERP.Api.Services
{
    public static class CryptographyHelper
    {
        // 32-byte default key (256-bit) and 16-byte initialization vector (128-bit)
        private static readonly byte[] DefaultKey = Encoding.UTF8.GetBytes("AriyAI_ERP_Secret_Encryption_Key");
        private static readonly byte[] DefaultIv = Encoding.UTF8.GetBytes("AriyAI_ERP_IV_16");

        private static byte[] GetEncryptionKey()
        {
            var keyStr = Environment.GetEnvironmentVariable("ENCRYPTION_KEY");
            if (string.IsNullOrEmpty(keyStr))
            {
                return DefaultKey;
            }
            var keyBytes = Encoding.UTF8.GetBytes(keyStr);
            if (keyBytes.Length == 32) return keyBytes;
            
            var paddedKey = new byte[32];
            Array.Copy(keyBytes, paddedKey, Math.Min(keyBytes.Length, 32));
            return paddedKey;
        }

        public static string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return string.Empty;

            byte[] key = GetEncryptionKey();
            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = DefaultIv;

            using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            using var ms = new MemoryStream();
            using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
            using (var sw = new StreamWriter(cs))
            {
                sw.Write(plainText);
            }

            return Convert.ToBase64String(ms.ToArray());
        }

        public static string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return string.Empty;

            try
            {
                byte[] key = GetEncryptionKey();
                using var aes = Aes.Create();
                aes.Key = key;
                aes.IV = DefaultIv;

                using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                using var ms = new MemoryStream(Convert.FromBase64String(cipherText));
                using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
                using var sr = new StreamReader(cs);
                return sr.ReadToEnd();
            }
            catch
            {
                // Return original text if decryption fails (fallback for unencrypted legacy values)
                return cipherText;
            }
        }
    }
}
