using System;
using System.IO;
using System.Security.Cryptography;
using SmallBin.Exceptions;

namespace SmallBin.Services
{
    internal class EncryptionService
    {
        private readonly byte[] _key;

        public EncryptionService(byte[] key)
        {
            _key = key ?? throw new ArgumentNullException(nameof(key));
        }

        public (byte[] encryptedData, byte[] iv) Encrypt(byte[] data)
        {
            if (data == null || data.Length == 0)
                throw new ArgumentException("Data cannot be null or empty", nameof(data));

            using var aes = Aes.Create();
            aes.Key = _key;
            aes.GenerateIV();

            try
            {
                using var ms = new MemoryStream();
                using var encryptor = aes.CreateEncryptor();
                using var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write);
                cs.Write(data, 0, data.Length);
                cs.FlushFinalBlock();
                return (ms.ToArray(), aes.IV);
            }
            catch (CryptographicException ex)
            {
                throw new DatabaseEncryptionException("Failed to encrypt data", ex);
            }
        }

        public byte[] Decrypt(byte[] encryptedData, byte[] iv)
        {
            if (encryptedData == null || encryptedData.Length == 0)
                throw new ArgumentException("Encrypted data cannot be null or empty", nameof(encryptedData));
            if (iv == null || iv.Length == 0)
                throw new ArgumentException("IV cannot be null or empty", nameof(iv));

            using var aes = Aes.Create();
            aes.Key = _key;
            aes.IV = iv;

            try
            {
                using var ms = new MemoryStream();
                using var decryptor = aes.CreateDecryptor();
                using var cs = new CryptoStream(new MemoryStream(encryptedData), decryptor, CryptoStreamMode.Read);
                cs.CopyTo(ms);
                return ms.ToArray();
            }
            catch (CryptographicException ex)
            {
                throw new DatabaseEncryptionException("Failed to decrypt data", ex);
            }
        }
    }
}
