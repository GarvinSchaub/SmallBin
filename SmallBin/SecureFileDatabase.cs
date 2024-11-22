using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SmallBin.Logging;

namespace SmallBin
{
    /// <summary>
    ///     The SecureFileDatabase class provides secure storage,
    ///     retrieval, and management of files with support for metadata,
    ///     encryption, and optional compression.
    /// </summary>
    /// <example>
    /// To create a new database:
    /// <code>
    /// var db = SecureFileDatabase.Create("path/to/db", "password")
    ///     .WithoutCompression()  // Optional: disable compression
    ///     .WithAutoSave()        // Optional: enable auto-save
    ///     .WithConsoleLogging()  // Optional: enable console logging
    ///     .Build();
    /// </code>
    /// </example>
    public class SecureFileDatabase : IDisposable
    {
        private readonly DatabaseContent _database;
        private readonly string _dbPath;
        private readonly byte[] _key;
        private readonly bool _useCompression;
        private readonly bool _useAutoSave;
        private readonly ILogger? _logger;
        private bool _isDirty;

        /// <summary>
        ///     Creates a new DatabaseBuilder instance for configuring and creating a SecureFileDatabase.
        /// </summary>
        /// <param name="dbPath">The file path where the database will be stored.</param>
        /// <param name="password">The password used for encrypting the database.</param>
        /// <returns>A new DatabaseBuilder instance for fluent configuration.</returns>
        public static DatabaseBuilder Create(string dbPath, string password)
        {
            return new DatabaseBuilder(dbPath, password);
        }

        /// <summary>
        ///     Internal constructor used by DatabaseBuilder to create a new instance of SecureFileDatabase.
        ///     To create a new database, use the static Create method instead.
        /// </summary>
        internal SecureFileDatabase(
            string dbPath, 
            string password, 
            bool useCompression = true, 
            bool useAutoSave = false,
            ILogger? logger = null)
        {
            if (string.IsNullOrEmpty(dbPath) || string.IsNullOrWhiteSpace(dbPath))
                throw new ArgumentNullException(nameof(dbPath));

            if (string.IsNullOrEmpty(password) || string.IsNullOrWhiteSpace(password))
                throw new ArgumentNullException(nameof(password));

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

        /// <summary>
        ///     Releases all resources used by the SecureFileDatabase.
        ///     This method should be called when the database is no longer needed,
        ///     to ensure that any unsaved data is persisted and all resources are freed.
        /// </summary>
        public void Dispose()
        {
            if (_isDirty)
            {
                _logger?.Debug("Saving changes before disposal");
                Save();
            }

            _logger?.Debug("Disposing database");
            _logger?.Dispose();
        }

        /// <summary>
        ///     Saves a file to the secure database with optional metadata tags and content type.
        ///     The file is compressed if the compression flag is enabled and always encrypted.
        /// </summary>
        /// <param name="filePath">The path to the file that needs to be saved.</param>
        /// <param name="tags">Optional list of tags to associate with the file</param>
        /// <param name="contentType">Optional MIME type specifying the type of content stored in the file.</param>
        /// <exception cref="FileNotFoundException">Thrown when the specified file does not exist.</exception>
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        public void SaveFile(string filePath, List<string> tags = null,
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
            string contentType = "application/octet-stream")
        {
            if (!File.Exists(filePath))
            {
                _logger?.Error($"File not found: {filePath}");
                throw new FileNotFoundException("Source file not found.", filePath);
            }

            _logger?.Info($"Saving file: {filePath}");
            var fileInfo = new FileInfo(filePath);
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
            using (var ms = new MemoryStream())
            {
                using var encryptor = aes.CreateEncryptor();
                using var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write);
                cs.Write(fileContent, 0, fileContent.Length);
                cs.FlushFinalBlock();
                encryptedContent = ms.ToArray();
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
        ///     Decrypts and optionally decompresses the file content before returning it.
        /// </summary>
        /// <param name="fileId">The unique identifier of the file to retrieve.</param>
        /// <returns>A byte array containing the decrypted and decompressed file content.</returns>
        /// <exception cref="KeyNotFoundException">Thrown when the specified fileId does not exist in the database.</exception>
        public byte[] GetFile(string fileId)
        {
            _logger?.Debug($"Retrieving file: {fileId}");

            if (!_database.Files.TryGetValue(fileId, out var entry))
            {
                _logger?.Error($"File not found in database: {fileId}");
                throw new KeyNotFoundException("File not found in database.");
            }

            _logger?.Debug("Decrypting file content");
            using var aes = Aes.Create();
            aes.Key = _key;
            aes.IV = entry.IV;

            byte[] decryptedContent;
            using (var ms = new MemoryStream())
            {
                using var decryptor = aes.CreateDecryptor();
                using var cs = new CryptoStream(new MemoryStream(entry.EncryptedContent), decryptor,
                    CryptoStreamMode.Read);
                cs.CopyTo(ms);
                decryptedContent = ms.ToArray();
            }

            if (entry.IsCompressed)
            {
                _logger?.Debug("Decompressing file content");
                decryptedContent = Decompress(decryptedContent);
            }

            _logger?.Info($"File retrieved successfully: {entry.FileName}");
            return decryptedContent;
        }

        private static byte[] Compress(byte[] data)
        {
            using var compressedStream = new MemoryStream();
            using (var gzipStream = new GZipStream(compressedStream, CompressionLevel.Optimal))
            {
                gzipStream.Write(data, 0, data.Length);
            }

            return compressedStream.ToArray();
        }

        private static byte[] Decompress(byte[] compressedData)
        {
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
            try
            {
                var fileContent = File.ReadAllBytes(_dbPath);
                if (fileContent.Length < 16)
                {
                    _logger?.Error("Database file is corrupt or invalid (file too small)");
                    throw new InvalidOperationException("Database file is corrupt or invalid");
                }

                var iv = new byte[16];
                Array.Copy(fileContent, 0, iv, 0, 16);

                var encryptedContent = new byte[fileContent.Length - 16];
                Array.Copy(fileContent, 16, encryptedContent, 0, encryptedContent.Length);

                using var aes = Aes.Create();
                aes.Key = _key;
                aes.IV = iv;

                string json;
                using (new MemoryStream())
                {
                    using var decryptor = aes.CreateDecryptor();
                    using var cs = new CryptoStream(new MemoryStream(encryptedContent), decryptor, CryptoStreamMode.Read);
                    using var reader = new StreamReader(cs);
                    json = reader.ReadToEnd();
                }

                var loadedDb = JsonSerializer.Deserialize<DatabaseContent>(json);
                if (loadedDb == null)
                {
                    _logger?.Warning("Loaded database content is null");
                    return;
                }

                _database.Files.Clear();
                foreach (var entry in loadedDb.Files)
                    _database.Files[entry.Key] = entry.Value;

                _database.Version = loadedDb.Version;
                _logger?.Info($"Database loaded successfully. Files: {_database.Files.Count}");
            }
            catch (Exception ex)
            {
                _logger?.Error("Failed to load database", ex);
                throw;
            }
        }

        /// <summary>
        ///     Deletes a file from the database by its unique identifier.
        /// </summary>
        /// <param name="fileId">The unique identifier of the file to be deleted.</param>
        /// <exception cref="KeyNotFoundException">Thrown when the specified fileId does not exist in the database.</exception>
        public void DeleteFile(string fileId)
        {
            _logger?.Debug($"Deleting file: {fileId}");

            if (!_database.Files.ContainsKey(fileId))
            {
                _logger?.Error($"File not found in database: {fileId}");
                throw new KeyNotFoundException("File not found in database.");
            }

            _database.Files.Remove(fileId);
            _isDirty = true;
            _logger?.Info($"File deleted successfully: {fileId}");
        }

        /// <summary>
        ///     Persists the current state of the database to disk if there are changes.
        /// </summary>
        /// <remarks>
        ///     This method serializes the in-memory database content to a temporary file,
        ///     encrypts it, and then replaces the original database file with the new one.
        ///     A backup of the previous version is maintained until the next save operation.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown if the database content is not written correctly.</exception>
        /// <exception cref="IOException">Thrown if there are errors during file operations.</exception>
        public void Save()
        {
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
                // Create new content in temporary file
                var jsonOptions = new JsonSerializerOptions {WriteIndented = true};
                var json = JsonSerializer.Serialize(_database, jsonOptions);
                var jsonBytes = Encoding.UTF8.GetBytes(json);

                _logger?.Debug("Writing encrypted content to temporary file");
                using (var aes = Aes.Create())
                {
                    aes.Key = _key;
                    aes.GenerateIV();

                    using (var fs = File.Create(tempPath))
                    {
                        fs.Write(aes.IV, 0, aes.IV.Length);

                        using var encryptor = aes.CreateEncryptor();
                        using var cs = new CryptoStream(fs, encryptor, CryptoStreamMode.Write);
                        cs.Write(jsonBytes, 0, jsonBytes.Length);
                    }
                }

                if (new FileInfo(tempPath).Length <= 16)
                {
                    _logger?.Error("Failed to write database content (file too small)");
                    throw new InvalidOperationException("Failed to write database content");
                }

                // Backup existing file if it exists
                if (File.Exists(_dbPath))
                {
                    _logger?.Debug("Creating backup of existing database");
                    // Remove old backup if it exists
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
                    File.Copy(backupPath, _dbPath);
                }

                throw;
            }
            finally
            {
                // Clean up temporary file
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                    _logger?.Debug("Cleaned up temporary file");
                }
                
                // Clean up old backup
                if (File.Exists(oldBackupPath))
                {
                    File.Delete(oldBackupPath);
                    _logger?.Debug("Cleaned up old backup file");
                }
            }
        }

        /// <summary>
        ///     Updates the metadata of a file entry in the secure database.
        /// </summary>
        /// <param name="fileId">The unique identifier of the file whose metadata is to be updated.</param>
        /// <param name="updateAction">An action that encapsulates the updates to be made to the file entry.</param>
        /// <exception cref="KeyNotFoundException">Thrown when the specified fileId does not exist in the database.</exception>
        public void UpdateMetadata(string fileId, Action<FileEntry> updateAction)
        {
            _logger?.Debug($"Updating metadata for file: {fileId}");

            if (!_database.Files.TryGetValue(fileId, out var entry))
            {
                _logger?.Error($"File not found in database: {fileId}");
                throw new KeyNotFoundException("File not found in database.");
            }

            updateAction(entry);
            entry.UpdatedOn = DateTime.UtcNow;
            _isDirty = true;
            _logger?.Info($"Metadata updated successfully for file: {fileId}");
        }

        /// <summary>
        ///     Searches for files in the database based on the provided search criteria.
        /// </summary>
        /// <param name="criteria">The criteria used to filter the search results.</param>
        /// <returns>A collection of <c>FileEntry</c> objects that match the specified search criteria.</returns>
        public IEnumerable<FileEntry> Search(SearchCriteria criteria)
        {
            _logger?.Debug($"Searching files with criteria: {criteria?.FileName ?? "all"}");

            var query = _database.Files.Values.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(criteria?.FileName))
                query = query.Where(e => e.FileName.Contains(criteria?.FileName, StringComparison.OrdinalIgnoreCase));

            var results = query.ToList();
            _logger?.Info($"Search completed. Found {results.Count} matches");
            return results;
        }
    }
}
