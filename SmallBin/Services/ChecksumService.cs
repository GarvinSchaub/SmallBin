using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using SmallBin.Models;

namespace SmallBin.Services
{
    /// <summary>
    /// Service responsible for calculating and verifying file checksums to ensure file integrity.
    /// </summary>
    public class ChecksumService
    {
        private const string DefaultAlgorithm = "SHA256";

        /// <summary>
        /// Calculates the checksum for the given byte array using the specified algorithm.
        /// </summary>
        /// <param name="content">The content to calculate the checksum for.</param>
        /// <param name="algorithm">The hashing algorithm to use (defaults to SHA256).</param>
        /// <returns>A string representation of the calculated checksum.</returns>
        public string CalculateChecksum(byte[] content, string algorithm = DefaultAlgorithm)
        {
            if (content == null)
                throw new ArgumentNullException(nameof(content));

            using var hashAlgorithm = CreateHashAlgorithm(algorithm);
            byte[] hash = hashAlgorithm.ComputeHash(content);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>
        /// Calculates the checksum for the given stream using the specified algorithm.
        /// </summary>
        /// <param name="stream">The stream to calculate the checksum for.</param>
        /// <param name="algorithm">The hashing algorithm to use (defaults to SHA256).</param>
        /// <returns>A string representation of the calculated checksum.</returns>
        public string CalculateChecksum(Stream stream, string algorithm = DefaultAlgorithm)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            using var hashAlgorithm = CreateHashAlgorithm(algorithm);
            byte[] hash = hashAlgorithm.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>
        /// Verifies the integrity of a file entry by comparing its stored checksum with a newly calculated one.
        /// </summary>
        /// <param name="fileEntry">The file entry to verify.</param>
        /// <param name="content">The content to verify against the stored checksum.</param>
        /// <returns>True if the checksums match, indicating the file is intact; otherwise, false.</returns>
        public bool VerifyIntegrity(FileEntry fileEntry, byte[] content)
        {
            if (fileEntry == null)
                throw new ArgumentNullException(nameof(fileEntry));
            if (content == null)
                throw new ArgumentNullException(nameof(content));
            if (string.IsNullOrEmpty(fileEntry.Checksum))
                throw new InvalidOperationException("File entry does not have a stored checksum.");

            string calculatedChecksum = CalculateChecksum(content, fileEntry.ChecksumAlgorithm);
            return string.Equals(calculatedChecksum, fileEntry.Checksum, StringComparison.OrdinalIgnoreCase);
        }

        private static HashAlgorithm CreateHashAlgorithm(string algorithm)
        {
            return algorithm?.ToUpperInvariant() switch
            {
                "SHA256" => SHA256.Create(),
                "SHA512" => SHA512.Create(),
                "MD5" => MD5.Create(),
                "SHA1" => SHA1.Create(),
                _ => throw new ArgumentException($"Unsupported hashing algorithm: {algorithm}")
            };
        }
    }
}
