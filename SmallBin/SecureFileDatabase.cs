using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

// ReSharper disable HeapView.ObjectAllocation.Evident
// ReSharper disable HeapView.ObjectAllocation

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
    ///     .Build();
    /// </code>
    /// </example>
    public class SecureFileDatabase : IDisposable
    {
        /// <summary>
        ///     Represents the in-memory storage of the file database.
        ///     This field holds all the database entries in a structured format,
        ///     allowing for easy retrieval and manipulation of the files stored within the database.
        /// </summary>
        private readonly DatabaseContent _database;

        /// <summary>
        ///     The file path where the database is stored.
        /// </summary>
        private readonly string _dbPath;

        /// <summary>
        ///     Holds the cryptographic key derived from the user's password.
        ///     This key is used to encrypt and decrypt the file contents and database entries.
        /// </summary>
        private readonly byte[] _key;

        /// <summary>
        ///     Indicates whether compression should be used for file storage.
        ///     When set to true, files will be compressed before being stored to reduce space usage.
        ///     Defaults to true.
        /// </summary>
        private readonly bool _useCompression;

        /// <summary>
        ///     Indicates whether the internal state of the SecureFileDatabase
        ///     has been modified since the last save operation. When set to true,
        ///     it signifies that there are unsaved changes that need to be persisted
        ///     to disk to maintain data integrity.
        /// </summary>
        private bool _isDirty;

        private bool _useAutoSave;

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
        internal SecureFileDatabase(string dbPath, string password, bool useCompression = true, bool useAutoSave = false)
        {
            if (string.IsNullOrEmpty(dbPath) || string.IsNullOrWhiteSpace(dbPath))
                throw new ArgumentNullException(nameof(dbPath));

            if (string.IsNullOrEmpty(password) || string.IsNullOrWhiteSpace(password))
                throw new ArgumentNullException(nameof(password));

            _dbPath = dbPath;
            _useCompression = useCompression;
            _useAutoSave = useAutoSave;
            _database = new DatabaseContent();

            using var deriveBytes = new Rfc2898DeriveBytes(
                password,
                new byte[] {0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76},
                10000);
            _key = deriveBytes.GetBytes(32);

            var directory = Path.GetDirectoryName(dbPath) ?? string.Empty;
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            if (File.Exists(dbPath))
            {
                LoadDatabase();
            }
            else
            {
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
            if (_isDirty) Save();
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
                throw new FileNotFoundException("Source file not found.", filePath);

            var fileInfo = new FileInfo(filePath);
            var fileContent = File.ReadAllBytes(filePath);

            if (_useCompression) fileContent = Compress(fileContent);

            // Encrypt the file content
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
                // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
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

            // Add the auto save functionality
            if (_useAutoSave)
                Save();
            else
                _isDirty = true;
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
            if (!_database.Files.TryGetValue(fileId, out var entry))
                throw new KeyNotFoundException("File not found in database.");

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

            if (entry.IsCompressed) decryptedContent = Decompress(decryptedContent);

            return decryptedContent;
        }

        /// <summary>
        ///     Compresses the given byte array using GZip compression.
        /// </summary>
        /// <param name="data">The byte array to be compressed.</param>
        /// <returns>The compressed byte array.</returns>
        private static byte[] Compress(byte[] data)
        {
            using var compressedStream = new MemoryStream();
            using (var gzipStream = new GZipStream(compressedStream, CompressionLevel.Optimal))
            {
                gzipStream.Write(data, 0, data.Length);
            }

            return compressedStream.ToArray();
        }

        /// <summary>
        ///     Decompresses the given byte array which is compressed using GZip compression algorithm.
        /// </summary>
        /// <param name="compressedData">The byte array containing the compressed data.</param>
        /// <returns>A byte array containing the decompressed data.</returns>
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

        /// <summary>
        ///     Loads the database from the file specified in the constructor,
        ///     decrypting the content using the provided password and initializing
        ///     the in-memory representation of the database. This method is
        ///     automatically called during the initialization if the database file
        ///     exists. It ensures that the database content is securely loaded and
        ///     ready for operations.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        ///     Thrown when the database file is corrupt or invalid.
        /// </exception>
        private void LoadDatabase()
        {
            var fileContent = File.ReadAllBytes(_dbPath);
            if (fileContent.Length < 16)
                throw new InvalidOperationException("Database file is corrupt or invalid");

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
            if (loadedDb == null) return;

            _database.Files.Clear();
            foreach (var entry in loadedDb.Files) _database.Files[entry.Key] = entry.Value;

            _database.Version = loadedDb.Version;
        }

        /// <summary>
        ///     Deletes a file from the database by its unique identifier.
        /// </summary>
        /// <param name="fileId">The unique identifier of the file to be deleted.</param>
        /// <exception cref="KeyNotFoundException">Thrown when the specified fileId does not exist in the database.</exception>
        public void DeleteFile(string fileId)
        {
            if (!_database.Files.ContainsKey(fileId))
                throw new KeyNotFoundException("File not found in database.");

            _database.Files.Remove(fileId);
            _isDirty = true;
        }

        /// <summary>
        ///     Persists the current state of the database to disk if there are changes.
        /// </summary>
        /// <remarks>
        ///     This method serializes the in-memory database content to a temporary file,
        ///     encrypts it, and then replaces the original database file with the new one.
        ///     In case of failure, the method ensures that any temporary files are cleaned up,
        ///     and the database remains in a consistent state.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown if the database content is not written correctly.</exception>
        /// <exception cref="IOException">Thrown if there are errors during file operations.</exception>
        public void Save()
        {
            if (!_isDirty) return;

            var tempPath = $"{_dbPath}.tmp";
            var backupPath = $"{_dbPath}.bak";

            //TODO: Looks like the backup stuff doesn't work properly

            try
            {
                if (File.Exists(_dbPath)) File.Copy(_dbPath, backupPath, true);

                var jsonOptions = new JsonSerializerOptions {WriteIndented = true};
                var json = JsonSerializer.Serialize(_database, jsonOptions);
                var jsonBytes = Encoding.UTF8.GetBytes(json);

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
                    throw new InvalidOperationException("Failed to write database content");

                if (File.Exists(_dbPath)) File.Delete(_dbPath);

                File.Move(tempPath, _dbPath);

                if (File.Exists(backupPath)) File.Delete(backupPath);

                _isDirty = false;
            }
            catch
            {
                if (File.Exists(backupPath) && !File.Exists(_dbPath)) File.Move(backupPath, _dbPath);

                if (File.Exists(tempPath)) File.Delete(tempPath);

                throw;
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
            if (!_database.Files.TryGetValue(fileId, out var entry))
                throw new KeyNotFoundException("File not found in database.");

            updateAction(entry);
            entry.UpdatedOn = DateTime.UtcNow;
            _isDirty = true;
        }

        /// <summary>
        ///     Searches for files in the database based on the provided search criteria.
        /// </summary>
        /// <param name="criteria">The criteria used to filter the search results.</param>
        /// <returns>A collection of <c>FileEntry</c> objects that match the specified search criteria.</returns>
        // ReSharper disable once HeapView.ClosureAllocation
        public IEnumerable<FileEntry> Search(SearchCriteria criteria)
        {
            var query = _database.Files.Values.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(criteria?.FileName))
                // ReSharper disable once HeapView.DelegateAllocation
                query = query.Where(e => e.FileName.Contains(criteria?.FileName, StringComparison.OrdinalIgnoreCase));

            return query.ToList();
        }
    }
}
