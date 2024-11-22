using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SmallBin.Exceptions;
using SmallBin.Logging;

namespace SmallBin
{
    /// <summary>
    ///     The SecureFileDatabase class provides secure storage,
    ///     retrieval, and management of files with support for metadata,
    ///     encryption, and optional compression.
    /// </summary>
    public class SecureFileDatabase : IDisposable
    {
        private readonly DatabaseContent _database;
        private readonly string _dbPath;
        private readonly byte[] _key;
        private readonly bool _useCompression;
        private readonly bool _useAutoSave;
        private readonly ILogger? _logger;
        private bool _isDirty;
        private bool _isDisposed;

        /// <summary>
        ///     Creates a new DatabaseBuilder instance for configuring and creating a SecureFileDatabase.
        /// </summary>
        public static DatabaseBuilder Create(string dbPath, string password)
        {
            if (string.IsNullOrEmpty(dbPath))
                throw new ArgumentNullException(nameof(dbPath), "Database path cannot be null or empty");
            if (string.IsNullOrEmpty(password))
                throw new ArgumentNullException(nameof(password), "Password cannot be null or empty");

            return new DatabaseBuilder(dbPath, password);
        }

        internal SecureFileDatabase(
            string dbPath, 
            string password, 
            bool useCompression = true, 
            bool useAutoSave = false,
            ILogger? logger = null)
        {
            if (string.IsNullOrEmpty(dbPath))
                throw new ArgumentNullException(nameof(dbPath), "Database path cannot be null or empty");
            if (string.IsNullOrEmpty(password))
                throw new ArgumentNullException(nameof(password), "Password cannot be null or empty");

            _dbPath = dbPath;
            _useCompression = useCompression;
            _useAutoSave = useAutoSave;
            _logger = logger;
            _database = new DatabaseContent();

            _logger?.Debug($"Initializing database at {dbPath}");
            _logger?.Debug($"Compression: {(_useCompression ? "enabled" : "disabled")}, Auto-save: {(_useAutoSave ? "enabled" : "disabled")}");

            using var deriveBytes = new Rfc2898DeriveBytes(
                password,
                new byte[] {0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76},
                10000);
            _key = deriveBytes.GetBytes(32);

            var directory = Path.GetDirectoryName(dbPath) ?? string.Empty;
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
                _logger?.Debug($"Created directory: {directory}");
            }

            if (File.Exists(dbPath))
            {
                _logger?.Info("Loading existing database");
                LoadDatabase();
            }
            else
            {
                _logger?.Info("Creating new database");
                _isDirty = true;
                Save();
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;

            try
            {
                if (_isDirty)
                {
                    _logger?.Debug("Saving changes before disposal");
                    Save();
                }
            }
            catch (Exception ex)
            {
                _logger?.Error("Error during disposal", ex);
            }
            finally
            {
                _logger?.Debug("Disposing database");
                _logger?.Dispose();
                _isDisposed = true;
            }
        }

        /// <summary>
        ///     Saves a file to the secure database with optional metadata tags and content type.
        /// </summary>
        public void SaveFile(string filePath, List<string>? tags = null, string contentType = "application/octet-stream")
        {
            ThrowIfDisposed();

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
                fileContent = Compress(fileContent);
            }

            _logger?.Debug("Encrypting file content");
            using var aes = Aes.Create();
            aes.Key = _key;
            aes.GenerateIV();

            byte[] encryptedContent;
            try
            {
                using var ms = new MemoryStream();
                using var encryptor = aes.CreateEncryptor();
                using var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write);
                cs.Write(fileContent, 0, fileContent.Length);
                cs.FlushFinalBlock();
                encryptedContent = ms.ToArray();
            }
            catch (CryptographicException ex)
            {
                throw new DatabaseEncryptionException("Failed to encrypt file content", ex);
            }

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
                IV = aes.IV
            };

            _database.Files[entry.Id] = entry;
            _isDirty = true;

            _logger?.Info($"File saved successfully. ID: {entry.Id}");

            if (_useAutoSave)
            {
                _logger?.Debug("Auto-save enabled, saving changes");
                Save();
            }
        }

        /// <summary>
        ///     Retrieves the file associated with the specified fileId from the database.
        /// </summary>
        public byte[] GetFile(string fileId)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(fileId))
                throw new ArgumentNullException(nameof(fileId), "File ID cannot be null or empty");

            _logger?.Debug($"Retrieving file: {fileId}");

            if (!_database.Files.TryGetValue(fileId, out var entry))
            {
                _logger?.Error($"File not found in database: {fileId}");
                throw new KeyNotFoundException($"File with ID '{fileId}' not found in database.");
            }

            _logger?.Debug("Decrypting file content");
            using var aes = Aes.Create();
            aes.Key = _key;
            aes.IV = entry.IV;

            byte[] decryptedContent;
            try
            {
                using var ms = new MemoryStream();
                using var decryptor = aes.CreateDecryptor();
                using var cs = new CryptoStream(new MemoryStream(entry.EncryptedContent), decryptor, CryptoStreamMode.Read);
                cs.CopyTo(ms);
                decryptedContent = ms.ToArray();
            }
            catch (CryptographicException ex)
            {
                throw new DatabaseEncryptionException($"Failed to decrypt file: {entry.FileName}", ex);
            }

            if (entry.IsCompressed)
            {
                _logger?.Debug("Decompressing file content");
                try
                {
                    decryptedContent = Decompress(decryptedContent);
                }
                catch (InvalidDataException ex)
                {
                    throw new DatabaseCorruptException($"Failed to decompress file: {entry.FileName}", ex);
                }
            }

            _logger?.Info($"File retrieved successfully: {entry.FileName}");
            return decryptedContent;
        }

        private static byte[] Compress(byte[] data)
        {
            if (data == null || data.Length == 0)
                throw new ArgumentException("Cannot compress null or empty data", nameof(data));

            using var compressedStream = new MemoryStream();
            using (var gzipStream = new GZipStream(compressedStream, CompressionLevel.Optimal))
            {
                gzipStream.Write(data, 0, data.Length);
            }
            return compressedStream.ToArray();
        }

        private static byte[] Decompress(byte[] compressedData)
        {
            if (compressedData == null || compressedData.Length == 0)
                throw new ArgumentException("Cannot decompress null or empty data", nameof(compressedData));

            using var compressedStream = new MemoryStream(compressedData);
            using var decompressedStream = new MemoryStream();
            using (var gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress))
            {
                gzipStream.CopyTo(decompressedStream);
            }
            return decompressedStream.ToArray();
        }

        private void LoadDatabase()
        {
            var fileContent = File.ReadAllBytes(_dbPath);
            if (fileContent.Length < 16)
            {
                _logger?.Error("Database file is corrupt or invalid (file too small)");
                throw new DatabaseCorruptException("Database file is corrupt or invalid (file too small)");
            }

            var iv = new byte[16];
            Array.Copy(fileContent, 0, iv, 0, 16);

            var encryptedContent = new byte[fileContent.Length - 16];
            Array.Copy(fileContent, 16, encryptedContent, 0, encryptedContent.Length);

            string json;
            try
            {
                using var aes = Aes.Create();
                aes.Key = _key;
                aes.IV = iv;

                using var ms = new MemoryStream();
                using var decryptor = aes.CreateDecryptor();
                using var cs = new CryptoStream(new MemoryStream(encryptedContent), decryptor, CryptoStreamMode.Read);
                using var reader = new StreamReader(cs);
                json = reader.ReadToEnd();
            }
            catch (CryptographicException ex)
            {
                throw new DatabaseEncryptionException("Failed to decrypt database content", ex);
            }

            var loadedDb = JsonSerializer.Deserialize<DatabaseContent>(json);
            if (loadedDb == null)
            {
                _logger?.Error("Database deserialization failed");
                throw new DatabaseCorruptException("Failed to deserialize database content");
            }

            _database.Files.Clear();
            foreach (var entry in loadedDb.Files)
                _database.Files[entry.Key] = entry.Value;

            _database.Version = loadedDb.Version;
            _logger?.Info($"Database loaded successfully. Files: {_database.Files.Count}");
        }

        /// <summary>
        ///     Deletes a file from the database by its unique identifier.
        /// </summary>
        public void DeleteFile(string fileId)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(fileId))
                throw new ArgumentNullException(nameof(fileId), "File ID cannot be null or empty");

            _logger?.Debug($"Deleting file: {fileId}");

            if (!_database.Files.ContainsKey(fileId))
            {
                _logger?.Error($"File not found in database: {fileId}");
                throw new KeyNotFoundException($"File with ID '{fileId}' not found in database.");
            }

            _database.Files.Remove(fileId);
            _isDirty = true;
            _logger?.Info($"File deleted successfully: {fileId}");

            if (_useAutoSave)
            {
                Save();
            }
        }

        /// <summary>
        ///     Persists the current state of the database to disk if there are changes.
        /// </summary>
        public void Save()
        {
            ThrowIfDisposed();

            if (!_isDirty)
            {
                _logger?.Debug("No changes to save");
                return;
            }

            _logger?.Info("Saving database changes");
            var tempPath = $"{_dbPath}.tmp";
            var backupPath = $"{_dbPath}.bak";
            var oldBackupPath = $"{_dbPath}.bak.old";

            try
            {
                var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(_database, jsonOptions);
                var jsonBytes = Encoding.UTF8.GetBytes(json);

                _logger?.Debug("Writing encrypted content to temporary file");
                using var aes = Aes.Create();
                aes.Key = _key;
                aes.GenerateIV();

                using (var fs = File.Create(tempPath))
                {
                    fs.Write(aes.IV, 0, aes.IV.Length);

                    using var encryptor = aes.CreateEncryptor();
                    using var cs = new CryptoStream(fs, encryptor, CryptoStreamMode.Write);
                    cs.Write(jsonBytes, 0, jsonBytes.Length);
                }

                if (new FileInfo(tempPath).Length <= 16)
                {
                    _logger?.Error("Failed to write database content (file too small)");
                    throw new InvalidDatabaseStateException("Failed to write database content (file too small)");
                }

                // Backup existing file if it exists
                if (File.Exists(_dbPath))
                {
                    _logger?.Debug("Creating backup of existing database");
                    if (File.Exists(backupPath))
                    {
                        if (File.Exists(oldBackupPath))
                            File.Delete(oldBackupPath);
                        File.Move(backupPath, oldBackupPath);
                    }
                    File.Copy(_dbPath, backupPath, true);
                }

                // Replace current file with new content
                _logger?.Debug("Replacing current database file with new content");
                if (File.Exists(_dbPath))
                    File.Delete(_dbPath);
                File.Copy(tempPath, _dbPath);

                _isDirty = false;
                _logger?.Info("Database saved successfully");
            }
            catch (Exception ex)
            {
                _logger?.Error("Failed to save database", ex);
                // On error, try to restore from backup
                if (File.Exists(backupPath) && !File.Exists(_dbPath))
                {
                    _logger?.Warning("Attempting to restore from backup");
                    try
                    {
                        File.Copy(backupPath, _dbPath);
                    }
                    catch (Exception restoreEx)
                    {
                        throw new DatabaseOperationException("Failed to save database and restore from backup", 
                            new AggregateException(ex, restoreEx));
                    }
                }
                throw;
            }
            finally
            {
                // Clean up temporary file
                if (File.Exists(tempPath))
                {
                    try
                    {
                        File.Delete(tempPath);
                        _logger?.Debug("Cleaned up temporary file");
                    }
                    catch (Exception)
                    {
                        _logger?.Warning("Failed to clean up temporary file");
                    }
                }
                
                // Clean up old backup
                if (File.Exists(oldBackupPath))
                {
                    try
                    {
                        File.Delete(oldBackupPath);
                        _logger?.Debug("Cleaned up old backup file");
                    }
                    catch (Exception)
                    {
                        _logger?.Warning("Failed to clean up old backup file");
                    }
                }
            }
        }

        /// <summary>
        ///     Updates the metadata of a file entry in the secure database.
        /// </summary>
        public void UpdateMetadata(string fileId, Action<FileEntry> updateAction)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(fileId))
                throw new ArgumentNullException(nameof(fileId), "File ID cannot be null or empty");
            if (updateAction == null)
                throw new ArgumentNullException(nameof(updateAction), "Update action cannot be null");

            _logger?.Debug($"Updating metadata for file: {fileId}");

            if (!_database.Files.TryGetValue(fileId, out var entry))
            {
                _logger?.Error($"File not found in database: {fileId}");
                throw new KeyNotFoundException($"File with ID '{fileId}' not found in database.");
            }

            try
            {
                updateAction(entry);
                entry.UpdatedOn = DateTime.UtcNow;
                _isDirty = true;
                _logger?.Info($"Metadata updated successfully for file: {fileId}");

                if (_useAutoSave)
                {
                    Save();
                }
            }
            catch (Exception ex)
            {
                _logger?.Error($"Failed to update metadata for file: {fileId}", ex);
                throw new DatabaseOperationException($"Failed to update metadata for file: {fileId}", ex);
            }
        }

        /// <summary>
        ///     Searches for files in the database based on the provided search criteria.
        /// </summary>
        public IEnumerable<FileEntry> Search(SearchCriteria? criteria)
        {
            ThrowIfDisposed();

            _logger?.Debug($"Searching files with criteria: {criteria?.FileName ?? "all"}");

            try
            {
                var query = _database.Files.Values.AsEnumerable();

                if (!string.IsNullOrWhiteSpace(criteria?.FileName))
                    query = query.Where(e => e.FileName.Contains(criteria.FileName, StringComparison.OrdinalIgnoreCase));

                var results = query.ToList();
                _logger?.Info($"Search completed. Found {results.Count} matches");
                return results;
            }
            catch (Exception ex)
            {
                _logger?.Error("Search operation failed", ex);
                throw new DatabaseOperationException("Search operation failed", ex);
            }
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(SecureFileDatabase), 
                    "Cannot perform operations on a disposed database");
            }
        }
    }
}
