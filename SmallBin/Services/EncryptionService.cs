using System;
using System.IO;
using System.Security.Cryptography;
using SmallBin.Exceptions;

namespace SmallBin.Services
{
    /// <summary>
    ///     Provides encryption and decryption services using AES-256 encryption
    /// </summary>
    /// <remarks>
    ///     This service handles the cryptographic operations for the secure file database.
    ///     It uses AES encryption with a unique IV for each encryption operation to ensure
    ///     maximum security.
    /// </remarks>
    internal class EncryptionService
    {
        private readonly byte[] _key;

        /// <summary>
        ///     Initializes a new instance of the EncryptionService class
        /// </summary>
        /// <param name="key">The encryption key to use for all cryptographic operations</param>
        /// <exception cref="ArgumentNullException">Thrown when the key is null</exception>
        public EncryptionService(byte[] key)
        {
            _key = key ?? throw new ArgumentNullException(nameof(key));
        }

        /// <summary>
        ///     Encrypts the provided data using AES encryption
        /// </summary>
        /// <param name="data">The data to encrypt</param>
        /// <returns>A tuple containing the encrypted data and the initialization vector (IV)</returns>
        /// <exception cref="ArgumentException">Thrown when data is null or empty</exception>
        /// <exception cref="DatabaseEncryptionException">Thrown when encryption fails</exception>
        /// <remarks>
        ///     A new IV is generated for each encryption operation to ensure
        ///     that identical data produces different encrypted results
        /// </remarks>
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

        /// <summary>
        ///     Decrypts the provided encrypted data using the specified IV
        /// </summary>
        /// <param name="encryptedData">The encrypted data to decrypt</param>
        /// <param name="iv">The initialization vector used during encryption</param>
        /// <returns>The decrypted data</returns>
        /// <exception cref="ArgumentException">Thrown when encryptedData or IV is null or empty</exception>
        /// <exception cref="DatabaseEncryptionException">Thrown when decryption fails</exception>
        /// <remarks>
        ///     The same IV used during encryption must be provided for successful decryption
        /// </remarks>
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
