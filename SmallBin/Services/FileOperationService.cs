using System;
using System.Collections.Generic;
using System.IO;
using SmallBin.Exceptions;
using SmallBin.Logging;

namespace SmallBin.Services
{
    internal class FileOperationService
    {
        private readonly EncryptionService _encryptionService;
        private readonly CompressionService _compressionService;
        private readonly bool _useCompression;
        private readonly ILogger? _logger;

        public FileOperationService(
            EncryptionService encryptionService,
            CompressionService compressionService,
            bool useCompression = true,
            ILogger? logger = null)
        {
            _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
            _compressionService = compressionService ?? throw new ArgumentNullException(nameof(compressionService));
            _useCompression = useCompression;
            _logger = logger;
        }

        public FileEntry SaveFile(string filePath, List<string>? tags = null, string contentType = "application/octet-stream")
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentNullException(nameof(filePath), "File path cannot be null or empty");
            if (string.IsNullOrEmpty(contentType))
                throw new ArgumentNullException(nameof(contentType), "Content type cannot be null or empty");

            if (!File.Exists(filePath))
            {
                _logger?.Error($"File not found: {filePath}");
                throw new FileNotFoundException("Source file not found.", filePath);
            }

            _logger?.Info($"Saving file: {filePath}");
            var fileInfo = new FileInfo(filePath);
            
            if (fileInfo.Length == 0)
                throw new FileValidationException("Cannot save empty file");

            var fileContent = File.ReadAllBytes(filePath);

            if (_useCompression)
            {
                _logger?.Debug("Compressing file content");
                fileContent = _compressionService.Compress(fileContent);
            }

            _logger?.Debug("Encrypting file content");
            var (encryptedContent, iv) = _encryptionService.Encrypt(fileContent);

            var entry = new FileEntry
            {
                FileName = Path.GetFileName(filePath),
                Tags = tags ?? new List<string>(),
                CreatedOn = DateTime.UtcNow,
                UpdatedOn = DateTime.UtcNow,
                FileSize = fileInfo.Length,
                ContentType = contentType,
                IsCompressed = _useCompression,
                EncryptedContent = encryptedContent,
                IV = iv
            };

            _logger?.Info($"File saved successfully. ID: {entry.Id}");
            return entry;
        }

        public byte[] GetFile(FileEntry entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            _logger?.Debug($"Retrieving file: {entry.FileName}");

            byte[] decryptedContent;
            try
            {
                _logger?.Debug("Decrypting file content");
                decryptedContent = _encryptionService.Decrypt(entry.EncryptedContent, entry.IV);
            }
            catch (DatabaseEncryptionException ex)
            {
                throw new DatabaseEncryptionException($"Failed to decrypt file: {entry.FileName}", ex);
            }

            if (entry.IsCompressed)
            {
                _logger?.Debug("Decompressing file content");
                try
                {
                    decryptedContent = _compressionService.Decompress(decryptedContent);
                }
                catch (DatabaseCorruptException ex)
                {
                    throw new DatabaseCorruptException($"Failed to decompress file: {entry.FileName}", ex);
                }
            }

            _logger?.Info($"File retrieved successfully: {entry.FileName}");
            return decryptedContent;
        }

        public void UpdateMetadata(FileEntry entry, Action<FileEntry> updateAction)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));
            if (updateAction == null)
                throw new ArgumentNullException(nameof(updateAction));

            _logger?.Debug($"Updating metadata for file: {entry.FileName}");

            try
            {
                updateAction(entry);
                entry.UpdatedOn = DateTime.UtcNow;
                _logger?.Info($"Metadata updated successfully for file: {entry.FileName}");
            }
            catch (Exception ex)
            {
                _logger?.Error($"Failed to update metadata for file: {entry.FileName}", ex);
                throw new DatabaseOperationException($"Failed to update metadata for file: {entry.FileName}", ex);
            }
        }
    }
}
