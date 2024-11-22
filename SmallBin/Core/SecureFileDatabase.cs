using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using SmallBin.Exceptions;
using SmallBin.Logging;
using SmallBin.Models;
using SmallBin.Services;

namespace SmallBin.Core
{
    /// <summary>
    ///     The SecureFileDatabase class provides secure storage,
    ///     retrieval, and management of files with support for metadata,
    ///     encryption, and optional compression.
    /// </summary>
    /// <remarks>
    ///     This class implements the IDisposable pattern and ensures proper cleanup
    ///     of resources. It uses a builder pattern for configuration and supports
    ///     features such as:
    ///     - AES-256 encryption for file content
    ///     - Optional GZip compression
    ///     - File metadata and tagging
    ///     - Automatic saving of changes
    ///     - Comprehensive logging
    /// </remarks>
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
        ///     Creates a new DatabaseBuilder instance for configuring and creating a SecureFileDatabase
        /// </summary>
        /// <param name="dbPath">The path where the database file will be stored</param>
        /// <param name="password">The password used for encrypting the database content</param>
        /// <returns>A DatabaseBuilder instance for fluent configuration</returns>
        /// <exception cref="ArgumentNullException">Thrown when dbPath or password is null or empty</exception>
        /// <remarks>
        ///     This is the recommended way to create a new SecureFileDatabase instance.
        ///     Use the returned builder to configure options such as compression, auto-save,
        ///     and logging before calling Build() to create the database.
        /// </remarks>
        public static DatabaseBuilder Create(string dbPath, string password)
        {
            if (string.IsNullOrEmpty(dbPath))
                throw new ArgumentNullException(nameof(dbPath), "Database path cannot be null or empty");
            if (string.IsNullOrEmpty(password))
                throw new ArgumentNullException(nameof(password), "Password cannot be null or empty");

            return new DatabaseBuilder(dbPath, password);
        }

        /// <summary>
        ///     Initializes a new instance of the SecureFileDatabase class
        /// </summary>
        /// <param name="dbPath">The path where the database file will be stored</param>
        /// <param name="password">The password used for encrypting the database content</param>
        /// <param name="useCompression">Whether to use compression for stored files</param>
        /// <param name="useAutoSave">Whether to automatically save changes after operations</param>
        /// <param name="logger">Optional logger for tracking database operations</param>
        /// <exception cref="ArgumentNullException">Thrown when dbPath or password is null or empty</exception>
        /// <remarks>
        ///     This constructor is internal. Use the Create() method and DatabaseBuilder
        ///     for creating new instances with proper configuration.
        /// </remarks>
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

        /// <summary>
        ///     Disposes of resources and ensures any pending changes are saved
        /// </summary>
        /// <remarks>
        ///     If auto-save is disabled and there are unsaved changes,
        ///     this method will attempt to save them before disposing.
        ///     Any errors during save are logged but not propagated.
        /// </remarks>
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
        ///     Saves a file to the secure database with optional metadata tags and content type
        /// </summary>
        /// <param name="filePath">The path to the file to save</param>
        /// <param name="tags">Optional list of tags to associate with the file</param>
        /// <param name="contentType">The MIME type of the file content (defaults to application/octet-stream)</param>
        /// <exception cref="ArgumentNullException">Thrown when filePath is null or empty</exception>
        /// <exception cref="FileNotFoundException">Thrown when the source file does not exist</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the database has been disposed</exception>
        /// <remarks>
        ///     The file is optionally compressed and always encrypted before storage.
        ///     If auto-save is enabled, changes are immediately persisted to disk.
        /// </remarks>
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
        ///     Retrieves the file associated with the specified fileId from the database
        /// </summary>
        /// <param name="fileId">The unique identifier of the file to retrieve</param>
        /// <returns>The decrypted (and decompressed if applicable) file content</returns>
        /// <exception cref="ArgumentNullException">Thrown when fileId is null or empty</exception>
        /// <exception cref="KeyNotFoundException">Thrown when the specified file is not found</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the database has been disposed</exception>
        /// <remarks>
        ///     The file content is automatically decrypted and decompressed (if it was compressed)
        ///     before being returned to the caller.
        /// </remarks>
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
        ///     Deletes a file from the database by its unique identifier
        /// </summary>
        /// <param name="fileId">The unique identifier of the file to delete</param>
        /// <exception cref="ArgumentNullException">Thrown when fileId is null or empty</exception>
        /// <exception cref="KeyNotFoundException">Thrown when the specified file is not found</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the database has been disposed</exception>
        /// <remarks>
        ///     If auto-save is enabled, changes are immediately persisted to disk.
        ///     This operation cannot be undone.
        /// </remarks>
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
        ///     Updates the metadata of a file entry in the secure database
        /// </summary>
        /// <param name="fileId">The unique identifier of the file to update</param>
        /// <param name="updateAction">An action that performs the metadata updates</param>
        /// <exception cref="ArgumentNullException">Thrown when fileId or updateAction is null</exception>
        /// <exception cref="KeyNotFoundException">Thrown when the specified file is not found</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the database has been disposed</exception>
        /// <remarks>
        ///     The update action is executed within a controlled context that ensures
        ///     proper tracking of changes. If auto-save is enabled, changes are
        ///     immediately persisted to disk.
        /// </remarks>
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
        ///     Searches for files in the database based on the provided search criteria
        /// </summary>
        /// <param name="criteria">The search criteria to apply</param>
        /// <returns>A collection of file entries matching the search criteria</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the database has been disposed</exception>
        /// <remarks>
        ///     If no criteria is specified, all files are returned.
        ///     The search is case-insensitive and supports partial matches for filenames.
        /// </remarks>
        public IEnumerable<FileEntry> Search(SearchCriteria? criteria)
        {
            ThrowIfDisposed();
            return _searchService.Search(_database.Files.Values, criteria);
        }

        /// <summary>
        ///     Persists the current state of the database to disk if there are changes
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown when the database has been disposed</exception>
        /// <exception cref="DatabaseOperationException">Thrown when the save operation fails</exception>
        /// <remarks>
        ///     This method only writes to disk if there are unsaved changes.
        ///     It implements a safe-save mechanism using temporary files and backups
        ///     to prevent data loss during save operations.
        /// </remarks>
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
