using System;
using System.IO;
using System.IO.Compression;
using SmallBin.Exceptions;

namespace SmallBin.Services
{
    /// <summary>
    ///     Provides compression and decompression services using GZip compression
    /// </summary>
    /// <remarks>
    ///     This service handles data compression to reduce storage space requirements.
    ///     It supports different compression levels to balance between compression ratio
    ///     and performance based on specific needs.
    /// </remarks>
    internal class CompressionService
    {
        /// <summary>
        ///     Compresses the provided data using GZip compression with specified compression level
        /// </summary>
        /// <param name="data">The data to compress</param>
        /// <param name="compressionLevel">The compression level to use. Defaults to Optimal.</param>
        /// <returns>The compressed data as a byte array</returns>
        /// <exception cref="ArgumentException">Thrown when data is null or empty</exception>
        /// <exception cref="DatabaseOperationException">Thrown when compression fails</exception>
        /// <remarks>
        ///     Supports different compression levels:
        ///     - NoCompression: Fastest but no compression
        ///     - Fastest: Quick compression with moderate ratio
        ///     - Optimal: Best balance between speed and compression (default)
        ///     - SmallestSize: Maximum compression but slower
        /// </remarks>
        public byte[] Compress(byte[] data, CompressionLevel compressionLevel = CompressionLevel.Optimal)
        {
            if (data == null || data.Length == 0)
                throw new ArgumentException("Cannot compress null or empty data", nameof(data));

            try
            {
                using var compressedStream = new MemoryStream();
                using (var gzipStream = new GZipStream(compressedStream, compressionLevel))
                {
                    gzipStream.Write(data, 0, data.Length);
                }
                return compressedStream.ToArray();
            }
            catch (Exception ex)
            {
                throw new DatabaseOperationException("Failed to compress data", ex);
            }
        }

        /// <summary>
        ///     Decompresses the provided compressed data using GZip decompression
        /// </summary>
        /// <param name="compressedData">The compressed data to decompress</param>
        /// <returns>The decompressed data as a byte array</returns>
        /// <exception cref="ArgumentException">Thrown when compressedData is null or empty</exception>
        /// <exception cref="DatabaseCorruptException">Thrown when the compressed data is invalid or corrupted</exception>
        /// <exception cref="DatabaseOperationException">Thrown when decompression fails for other reasons</exception>
        /// <remarks>
        ///     Handles corrupted data by throwing a specific DatabaseCorruptException
        ///     to distinguish between data corruption and other operational failures
        /// </remarks>
        public byte[] Decompress(byte[] compressedData)
        {
            if (compressedData == null || compressedData.Length == 0)
                throw new ArgumentException("Cannot decompress null or empty data", nameof(compressedData));

            try
            {
                using var compressedStream = new MemoryStream(compressedData);
                using var decompressedStream = new MemoryStream();
                using (var gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress))
                {
                    gzipStream.CopyTo(decompressedStream);
                }
                return decompressedStream.ToArray();
            }
            catch (InvalidDataException ex)
            {
                throw new DatabaseCorruptException("Failed to decompress data", ex);
            }
            catch (Exception ex)
            {
                throw new DatabaseOperationException("Failed to decompress data", ex);
            }
        }
    }
}
