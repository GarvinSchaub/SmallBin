using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using SmallBin.Exceptions;
using SmallBin.Logging;
using SmallBin.Services;

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
        private readonly DatabasePersistenceService _persistenceService;
        private readonly FileOperationService _fileOperationService;
        private readonly SearchService _searchService;
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

            _useAutoSave = useAutoSave;
            _logger = logger;

            _logger?.Debug($"Initializing database at {dbPath}");
            _logger?.Debug($"Compression: {(useCompression ? "enabled" : "disabled")}, Auto-save: {(_useAutoSave ? "enabled" : "disabled")}");

            // Initialize encryption key
            using var deriveBytes = new Rfc2898DeriveBytes(
                password,
                new byte[] {0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76},
                10000);
            var key = deriveBytes.GetBytes(32);

            // Initialize services
            var encryptionService = new EncryptionService(key);
            var compressionService = new CompressionService();
            _persistenceService = new DatabasePersistenceService(dbPath, encryptionService, logger);
            _fileOperationService = new FileOperationService(encryptionService, compressionService, useCompression, logger);
            _searchService = new SearchService(logger);

            // Initialize database
            if (System.IO.File.Exists(dbPath))
            {
                _logger?.Info("Loading existing database");
                _database = _persistenceService.Load();
            }
            else
            {
                _logger?.Info("Creating new database");
                _database = new DatabaseContent();
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

            var entry = _fileOperationService.SaveFile(filePath, tags, contentType);
            _database.Files[entry.Id] = entry;
            _isDirty = true;

            if (_useAutoSave)
            {
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

            if (!_database.Files.TryGetValue(fileId, out var entry))
            {
                _logger?.Error($"File not found in database: {fileId}");
                throw new KeyNotFoundException($"File with ID '{fileId}' not found in database.");
            }

            return _fileOperationService.GetFile(entry);
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
        ///     Updates the metadata of a file entry in the secure database.
        /// </summary>
        public void UpdateMetadata(string fileId, Action<FileEntry> updateAction)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(fileId))
                throw new ArgumentNullException(nameof(fileId), "File ID cannot be null or empty");
            if (updateAction == null)
                throw new ArgumentNullException(nameof(updateAction), "Update action cannot be null");

            if (!_database.Files.TryGetValue(fileId, out var entry))
            {
                _logger?.Error($"File not found in database: {fileId}");
                throw new KeyNotFoundException($"File with ID '{fileId}' not found in database.");
            }

            _fileOperationService.UpdateMetadata(entry, updateAction);
            _isDirty = true;

            if (_useAutoSave)
            {
                Save();
            }
        }

        /// <summary>
        ///     Searches for files in the database based on the provided search criteria.
        /// </summary>
        public IEnumerable<FileEntry> Search(SearchCriteria? criteria)
        {
            ThrowIfDisposed();
            return _searchService.Search(_database.Files.Values, criteria);
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

            _persistenceService.Save(_database);
            _isDirty = false;
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
