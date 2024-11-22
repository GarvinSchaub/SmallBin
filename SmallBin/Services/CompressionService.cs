using System;
using System.IO;
using System.IO.Compression;
using SmallBin.Exceptions;

namespace SmallBin.Services
{
    internal class CompressionService
    {
        public byte[] Compress(byte[] data)
        {
            if (data == null || data.Length == 0)
                throw new ArgumentException("Cannot compress null or empty data", nameof(data));

            try
            {
                using var compressedStream = new MemoryStream();
                using (var gzipStream = new GZipStream(compressedStream, CompressionLevel.Optimal))
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
