using System.Security.Cryptography;
using System.Text;
using SmallBin.Exceptions;
using SmallBin.Services;

namespace SmallBin.UnitTests
{
    public class EncryptionServiceTests
    {
        private readonly EncryptionService _encryptionService;
        private readonly byte[] _key;

        public EncryptionServiceTests()
        {
            // Generate a valid 256-bit key for testing
            _key = new byte[32]; // 256 bits = 32 bytes
            RandomNumberGenerator.Fill(_key); // Modern way to generate random bytes
            _encryptionService = new EncryptionService(_key);
        }

        [Fact]
        public void Constructor_WithNullKey_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new EncryptionService(null));
        }

        [Fact]
        public void Encrypt_WithValidData_ReturnsEncryptedDataAndIV()
        {
            // Arrange
            var data = Encoding.UTF8.GetBytes("Test data for encryption");

            // Act
            var (encryptedData, iv) = _encryptionService.Encrypt(data);

            // Assert
            Assert.NotNull(encryptedData);
            Assert.NotNull(iv);
            Assert.True(encryptedData.Length > 0);
            Assert.Equal(16, iv.Length); // AES IV is always 16 bytes
            Assert.NotEqual(data, encryptedData); // Encrypted data should be different from original
        }

        [Fact]
        public void Encrypt_WithNullData_ThrowsArgumentException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => _encryptionService.Encrypt(null));
        }

        [Fact]
        public void Encrypt_WithEmptyData_ThrowsArgumentException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => _encryptionService.Encrypt(Array.Empty<byte>()));
        }

        [Fact]
        public void Decrypt_WithValidData_ReturnsOriginalData()
        {
            // Arrange
            var originalData = Encoding.UTF8.GetBytes("Test data for encryption and decryption");
            var (encryptedData, iv) = _encryptionService.Encrypt(originalData);

            // Act
            var decryptedData = _encryptionService.Decrypt(encryptedData, iv);

            // Assert
            Assert.Equal(originalData, decryptedData);
        }

        [Fact]
        public void Decrypt_WithNullData_ThrowsArgumentException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => _encryptionService.Decrypt(null, new byte[16]));
        }

        [Fact]
        public void Decrypt_WithEmptyData_ThrowsArgumentException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => _encryptionService.Decrypt(Array.Empty<byte>(), new byte[16]));
        }

        [Fact]
        public void Decrypt_WithNullIV_ThrowsArgumentException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => _encryptionService.Decrypt(new byte[10], null));
        }

        [Fact]
        public void Decrypt_WithEmptyIV_ThrowsArgumentException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => _encryptionService.Decrypt(new byte[10], Array.Empty<byte>()));
        }

        [Fact]
        public void Decrypt_WithInvalidIV_ThrowsDatabaseEncryptionException()
        {
            // Arrange
            var data = Encoding.UTF8.GetBytes("Test data");
            var (encryptedData, _) = _encryptionService.Encrypt(data);
            var invalidIv = new byte[16]; // All zeros IV

            // Act & Assert
            Assert.Throws<DatabaseEncryptionException>(() => 
                _encryptionService.Decrypt(encryptedData, invalidIv));
        }

        [Fact]
        public void EncryptDecrypt_WithLargeData_MaintainsDataIntegrity()
        {
            // Arrange
            var largeData = new byte[100000];
            Random.Shared.NextBytes(largeData); // Using Random.Shared for better performance

            // Act
            var (encryptedData, iv) = _encryptionService.Encrypt(largeData);
            var decryptedData = _encryptionService.Decrypt(encryptedData, iv);

            // Assert
            Assert.Equal(largeData, decryptedData);
        }

        [Fact]
        public void EncryptDecrypt_WithSpecialCharacters_MaintainsDataIntegrity()
        {
            // Arrange
            var specialData = Encoding.UTF8.GetBytes("Special characters: !@#$%^&*()_+-=[]{}|;:,.<>?/~`");

            // Act
            var (encryptedData, iv) = _encryptionService.Encrypt(specialData);
            var decryptedData = _encryptionService.Decrypt(encryptedData, iv);

            // Assert
            Assert.Equal(specialData, decryptedData);
        }

        [Fact]
        public void EncryptDecrypt_WithDifferentInstances_WorksCorrectly()
        {
            // Arrange
            var data = Encoding.UTF8.GetBytes("Test data");
            var encryptionService1 = new EncryptionService(_key);
            var encryptionService2 = new EncryptionService(_key);

            // Act
            var (encryptedData, iv) = encryptionService1.Encrypt(data);
            var decryptedData = encryptionService2.Decrypt(encryptedData, iv);

            // Assert
            Assert.Equal(data, decryptedData);
        }
    }
}
