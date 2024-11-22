using System;
using System.Collections.Generic;
using System.IO;
using SmallBin.Exceptions;
using SmallBin.Logging;
using SmallBin.Models;

namespace SmallBin.Services
{
    /// <summary>
    ///     Provides core file operations for the secure file database
    /// </summary>
    /// <remarks>
    ///     This service handles saving, retrieving, and updating files in the database.
    ///     It coordinates encryption and compression operations, manages file metadata,
    ///     and provides logging of all file operations for security auditing.
    /// </remarks>
    internal class FileOperationService
    {
        private readonly EncryptionService _encryptionService;
        private readonly CompressionService _compressionService;
        private readonly bool _useCompression;
        private readonly ILogger? _logger;

        /// <summary>
        ///     Initializes a new instance of the FileOperationService class
        /// </summary>
        /// <param name="encryptionService">The service used for encrypting and decrypting file content</param>
        /// <param name="compressionService">The service used for compressing and decompressing file content</param>
        /// <param name="useCompression">Whether to use compression for stored files</param>
        /// <param name="logger">Optional logger for tracking file operations</param>
        /// <exception cref="ArgumentNullException">Thrown when encryptionService or compressionService is null</exception>
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

        /// <summary>
        ///     Saves a file to the database with optional tags and content type
        /// </summary>
        /// <param name="filePath">The path to the file to save</param>
        /// <param name="tags">Optional list of tags to associate with the file</param>
        /// <param name="contentType">The MIME type of the file content (defaults to application/octet-stream)</param>
        /// <returns>A FileEntry object containing the saved file's metadata and encrypted content</returns>
        /// <exception cref="ArgumentNullException">Thrown when filePath or contentType is null or empty</exception>
        /// <exception cref="FileNotFoundException">Thrown when the source file does not exist</exception>
        /// <exception cref="FileValidationException">Thrown when attempting to save an empty file</exception>
        /// <remarks>
        ///     The file content is optionally compressed and always encrypted before storage.
        ///     Each file gets a unique ID and maintains creation/update timestamps.
        /// </remarks>
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

        /// <summary>
        ///     Retrieves a file's content from the database
        /// </summary>
        /// <param name="entry">The FileEntry object containing the file's metadata and encrypted content</param>
        /// <returns>The decrypted and decompressed file content as a byte array</returns>
        /// <exception cref="ArgumentNullException">Thrown when entry is null</exception>
        /// <exception cref="DatabaseEncryptionException">Thrown when decryption fails</exception>
        /// <exception cref="DatabaseCorruptException">Thrown when decompression fails due to corrupted data</exception>
        /// <remarks>
        ///     The file content is first decrypted and then decompressed (if compression was used).
        ///     All operations are logged for security auditing purposes.
        /// </remarks>
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

        /// <summary>
        ///     Updates the metadata of a file entry
        /// </summary>
        /// <param name="entry">The FileEntry object to update</param>
        /// <param name="updateAction">An action that performs the metadata updates</param>
        /// <exception cref="ArgumentNullException">Thrown when entry or updateAction is null</exception>
        /// <exception cref="DatabaseOperationException">Thrown when the update operation fails</exception>
        /// <remarks>
        ///     This method automatically updates the UpdatedOn timestamp.
        ///     The update operation is atomic and logged for security auditing.
        /// </remarks>
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
