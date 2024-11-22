using System;
using System.IO;
using System.Text;
using System.Text.Json;
using SmallBin.Exceptions;
using SmallBin.Logging;
using SmallBin.Models;

namespace SmallBin.Services
{
    /// <summary>
    ///     Provides persistence operations for the secure file database
    /// </summary>
    /// <remarks>
    ///     This service handles loading and saving the database file, including encryption
    ///     of the database content. It implements a safe-save mechanism using temporary
    ///     files and backups to prevent data loss during save operations.
    /// </remarks>
    internal class DatabasePersistenceService
    {
        private readonly string _dbPath;
        private readonly EncryptionService _encryptionService;
        private readonly ILogger? _logger;

        /// <summary>
        ///     Initializes a new instance of the DatabasePersistenceService class
        /// </summary>
        /// <param name="dbPath">The path where the database file will be stored</param>
        /// <param name="encryptionService">The service used for encrypting and decrypting database content</param>
        /// <param name="logger">Optional logger for tracking persistence operations</param>
        /// <exception cref="ArgumentNullException">Thrown when dbPath or encryptionService is null</exception>
        public DatabasePersistenceService(string dbPath, EncryptionService encryptionService, ILogger? logger = null)
        {
            _dbPath = dbPath ?? throw new ArgumentNullException(nameof(dbPath));
            _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
            _logger = logger;
        }

        /// <summary>
        ///     Loads and decrypts the database content from disk
        /// </summary>
        /// <returns>The decrypted database content</returns>
        /// <exception cref="DatabaseCorruptException">Thrown when the database file is corrupt or invalid</exception>
        /// <exception cref="DatabaseEncryptionException">Thrown when decryption fails</exception>
        /// <remarks>
        ///     The database file format consists of a 16-byte IV followed by the encrypted content.
        ///     The content is encrypted using AES encryption and stored in JSON format.
        /// </remarks>
        public DatabaseContent Load()
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

            try
            {
                var decryptedBytes = _encryptionService.Decrypt(encryptedContent, iv);
                var json = Encoding.UTF8.GetString(decryptedBytes);
                
                var loadedDb = JsonSerializer.Deserialize<DatabaseContent>(json);
                if (loadedDb == null)
                {
                    _logger?.Error("Database deserialization failed");
                    throw new DatabaseCorruptException("Failed to deserialize database content");
                }

                _logger?.Info($"Database loaded successfully. Files: {loadedDb.Files.Count}");
                return loadedDb;
            }
            catch (DatabaseEncryptionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new DatabaseCorruptException("Failed to load database content", ex);
            }
        }

        /// <summary>
        ///     Encrypts and saves the database content to disk
        /// </summary>
        /// <param name="database">The database content to save</param>
        /// <exception cref="InvalidDatabaseStateException">Thrown when the save operation produces an invalid file</exception>
        /// <exception cref="DatabaseOperationException">Thrown when the save operation fails</exception>
        /// <remarks>
        ///     Implements a safe-save mechanism using temporary files and backups:
        ///     1. Writes new content to a temporary file
        ///     2. Creates a backup of the existing database file
        ///     3. Replaces the current file with the new content
        ///     4. Cleans up temporary files
        ///     
        ///     If an error occurs during save, attempts to restore from backup.
        ///     All operations are logged for debugging and auditing purposes.
        /// </remarks>
        public void Save(DatabaseContent database)
        {
            var tempPath = $"{_dbPath}.tmp";
            var backupPath = $"{_dbPath}.bak";
            var oldBackupPath = $"{_dbPath}.bak.old";

            try
            {
                var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(database, jsonOptions);
                var jsonBytes = Encoding.UTF8.GetBytes(json);

                _logger?.Debug("Writing encrypted content to temporary file");
                var (encryptedData, iv) = _encryptionService.Encrypt(jsonBytes);

                using (var fs = File.Create(tempPath))
                {
                    fs.Write(iv, 0, iv.Length);
                    fs.Write(encryptedData, 0, encryptedData.Length);
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
                File.Move(tempPath, _dbPath);

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
    }
}
