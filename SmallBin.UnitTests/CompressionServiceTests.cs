using System.Text;
using SmallBin.Exceptions;
using SmallBin.Services;

namespace SmallBin.UnitTests
{
    public class CompressionServiceTests
    {
        private readonly CompressionService _compressionService;

        public CompressionServiceTests()
        {
            _compressionService = new CompressionService();
        }

        [Fact]
        public void Compress_WithValidData_ShouldCompressSuccessfully()
        {
            // Arrange
            var data = Encoding.UTF8.GetBytes("Test data for compression");

            // Act
            var compressed = _compressionService.Compress(data);

            // Assert
            Assert.NotNull(compressed);
            Assert.True(compressed.Length > 0);
        }

        [Theory]
        [InlineData(null)]
        [InlineData(new byte[0])]
        public void Compress_WithInvalidData_ShouldThrowArgumentException(byte[] invalidData)
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => _compressionService.Compress(invalidData));
            Assert.Contains("Cannot compress null or empty data", exception.Message);
        }

        [Fact]
        public void Decompress_WithValidCompressedData_ShouldDecompressSuccessfully()
        {
            // Arrange
            var originalData = Encoding.UTF8.GetBytes("Test data for compression and decompression");
            var compressed = _compressionService.Compress(originalData);

            // Act
            var decompressed = _compressionService.Decompress(compressed);

            // Assert
            Assert.NotNull(decompressed);
            Assert.Equal(originalData, decompressed);
        }

        [Theory]
        [InlineData(null)]
        [InlineData(new byte[0])]
        public void Decompress_WithInvalidData_ShouldThrowArgumentException(byte[] invalidData)
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => _compressionService.Decompress(invalidData));
            Assert.Contains("Cannot decompress null or empty data", exception.Message);
        }

        [Fact]
        public void Decompress_WithCorruptData_ShouldThrowDatabaseCorruptException()
        {
            // Arrange
            var corruptData = new byte[] { 1, 2, 3, 4, 5 }; // Invalid compressed data

            // Act & Assert
            Assert.Throws<DatabaseCorruptException>(() => _compressionService.Decompress(corruptData));
        }

        [Fact]
        public void CompressDecompress_WithLargeData_ShouldMaintainDataIntegrity()
        {
            // Arrange
            var largeData = new byte[100000];
            new Random(42).NextBytes(largeData); // Fill with random data

            // Act
            var compressed = _compressionService.Compress(largeData);
            var decompressed = _compressionService.Decompress(compressed);

            // Assert
            Assert.Equal(largeData, decompressed);
        }

        [Fact]
        public void CompressDecompress_WithSpecialCharacters_ShouldMaintainDataIntegrity()
        {
            // Arrange
            var specialData = Encoding.UTF8.GetBytes("Special characters: !@#$%^&*()_+-=[]{}|;:,.<>?/~`");

            // Act
            var compressed = _compressionService.Compress(specialData);
            var decompressed = _compressionService.Decompress(compressed);

            // Assert
            Assert.Equal(specialData, decompressed);
        }
    }
}
