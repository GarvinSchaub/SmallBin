using System;
using System.IO;
using System.Text;
using SmallBin.Models;
using SmallBin.Services;
using Xunit;

namespace SmallBin.UnitTests
{
    public class ChecksumServiceTests
    {
        private readonly ChecksumService _checksumService;

        public ChecksumServiceTests()
        {
            _checksumService = new ChecksumService();
        }

        [Fact]
        public void CalculateChecksum_WithByteArray_ReturnsCorrectHash()
        {
            // Arrange
            byte[] content = Encoding.UTF8.GetBytes("Hello, World!");
            // Expected SHA256 hash for "Hello, World!"
            string expectedHash = "dffd6021bb2bd5b0af676290809ec3a53191dd81c7f70a4b28688a362182986f";

            // Act
            string checksum = _checksumService.CalculateChecksum(content);

            // Assert
            Assert.Equal(expectedHash, checksum);
        }

        [Fact]
        public void CalculateChecksum_WithStream_ReturnsCorrectHash()
        {
            // Arrange
            byte[] content = Encoding.UTF8.GetBytes("Hello, World!");
            using var stream = new MemoryStream(content);
            // Expected SHA256 hash for "Hello, World!"
            string expectedHash = "dffd6021bb2bd5b0af676290809ec3a53191dd81c7f70a4b28688a362182986f";

            // Act
            string checksum = _checksumService.CalculateChecksum(stream);

            // Assert
            Assert.Equal(expectedHash, checksum);
        }

        [Fact]
        public void CalculateChecksum_WithDifferentAlgorithms_ReturnsDifferentHashes()
        {
            // Arrange
            byte[] content = Encoding.UTF8.GetBytes("Hello, World!");

            // Act
            string sha256Hash = _checksumService.CalculateChecksum(content, "SHA256");
            string sha512Hash = _checksumService.CalculateChecksum(content, "SHA512");
            string md5Hash = _checksumService.CalculateChecksum(content, "MD5");

            // Assert
            Assert.NotEqual(sha256Hash, sha512Hash);
            Assert.NotEqual(sha256Hash, md5Hash);
            Assert.NotEqual(sha512Hash, md5Hash);
        }

        [Fact]
        public void VerifyIntegrity_WithMatchingContent_ReturnsTrue()
        {
            // Arrange
            byte[] content = Encoding.UTF8.GetBytes("Hello, World!");
            var fileEntry = new FileEntry
            {
                ChecksumAlgorithm = "SHA256",
                Checksum = _checksumService.CalculateChecksum(content)
            };

            // Act
            bool result = _checksumService.VerifyIntegrity(fileEntry, content);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void VerifyIntegrity_WithModifiedContent_ReturnsFalse()
        {
            // Arrange
            byte[] originalContent = Encoding.UTF8.GetBytes("Hello, World!");
            byte[] modifiedContent = Encoding.UTF8.GetBytes("Hello, World");
            var fileEntry = new FileEntry
            {
                ChecksumAlgorithm = "SHA256",
                Checksum = _checksumService.CalculateChecksum(originalContent)
            };

            // Act
            bool result = _checksumService.VerifyIntegrity(fileEntry, modifiedContent);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void CalculateChecksum_WithNullContent_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => _checksumService.CalculateChecksum((byte[])null!));
        }

        [Fact]
        public void CalculateChecksum_WithNullStream_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => _checksumService.CalculateChecksum((Stream)null!));
        }

        [Fact]
        public void CalculateChecksum_WithInvalidAlgorithm_ThrowsArgumentException()
        {
            // Arrange
            byte[] content = Encoding.UTF8.GetBytes("Hello, World!");

            // Act & Assert
            Assert.Throws<ArgumentException>(() => _checksumService.CalculateChecksum(content, "INVALID_ALGORITHM"));
        }

        [Fact]
        public void VerifyIntegrity_WithNullFileEntry_ThrowsArgumentNullException()
        {
            // Arrange
            byte[] content = Encoding.UTF8.GetBytes("Hello, World!");

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => _checksumService.VerifyIntegrity(null!, content));
        }

        [Fact]
        public void VerifyIntegrity_WithNullContent_ThrowsArgumentNullException()
        {
            // Arrange
            var fileEntry = new FileEntry
            {
                ChecksumAlgorithm = "SHA256",
                Checksum = "some-hash"
            };

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => _checksumService.VerifyIntegrity(fileEntry, null!));
        }

        [Fact]
        public void VerifyIntegrity_WithNoStoredChecksum_ThrowsInvalidOperationException()
        {
            // Arrange
            byte[] content = Encoding.UTF8.GetBytes("Hello, World!");
            var fileEntry = new FileEntry
            {
                ChecksumAlgorithm = "SHA256",
                Checksum = null!
            };

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => _checksumService.VerifyIntegrity(fileEntry, content));
        }
    }
}
