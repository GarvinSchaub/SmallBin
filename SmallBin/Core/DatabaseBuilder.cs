using System;
using System.IO;
using SmallBin.Exceptions;
using SmallBin.Logging;

namespace SmallBin.Core
{
    /// <summary>
    /// Builder class for creating instances of SecureFileDatabase with configurable options.
    /// </summary>
    public class DatabaseBuilder
    {
        private readonly string _dbPath;
        private readonly string _password;
        private bool _useCompression = true;
        private bool _useAutoSave;
        private ILogger? _logger;

        private const int MinPasswordLength = 8;
        private const long DefaultMaxLogSize = 10 * 1024 * 1024; // 10MB

        /// <summary>
        /// Initializes a new instance of the DatabaseBuilder with required parameters.
        /// </summary>
        public DatabaseBuilder(string dbPath, string password)
        {
            // Check for null or empty strings first
            if (dbPath == null)
                throw new ArgumentNullException(nameof(dbPath));
            if (password == null)
                throw new ArgumentNullException(nameof(password));

            // Then check for whitespace
            if (string.IsNullOrWhiteSpace(dbPath))
                throw new ArgumentNullException(nameof(dbPath), "Database path cannot be empty or whitespace");
            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentNullException(nameof(password), "Password cannot be empty or whitespace");
            
            // Password length check is separate from null/empty validation
            if (password.Length < MinPasswordLength)
                throw new ArgumentException($"Password must be at least {MinPasswordLength} characters long", nameof(password));

            try
            {
                // Validate and normalize the path
                dbPath = Path.GetFullPath(dbPath);
                
                // Check if the directory exists or can be created
                var directory = Path.GetDirectoryName(dbPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    // Verify write permissions by attempting to create a temporary file
                    var testFile = Path.Combine(directory, $"test_{Guid.NewGuid()}.tmp");
                    try
                    {
                        File.WriteAllText(testFile, string.Empty);
                        File.Delete(testFile);
                    }
                    catch (Exception ex)
                    {
                        if (ex is UnauthorizedAccessException || ex is IOException)
                        {
                            throw new DatabaseOperationException(
                                $"Directory is not writable: {directory}", ex);
                        }
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex is DatabaseOperationException)
                    throw;
                    
                throw new DatabaseOperationException(
                    $"Invalid database path: {dbPath}", ex);
            }

            _dbPath = dbPath;
            _password = password;
        }

        /// <summary>
        /// Disables compression for stored files. By default, compression is enabled.
        /// </summary>
        public DatabaseBuilder WithoutCompression()
        {
            _useCompression = false;
            return this;
        }

        /// <summary>
        /// Enables automatic saving of changes. By default, auto-save is disabled.
        /// </summary>
        public DatabaseBuilder WithAutoSave()
        {
            _useAutoSave = true;
            return this;
        }

        /// <summary>
        /// Sets a custom logger implementation for the database.
        /// </summary>
        public DatabaseBuilder WithLogger(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger), "Logger cannot be null");
            return this;
        }

        /// <summary>
        /// Enables console logging with optional timestamp inclusion.
        /// </summary>
        public DatabaseBuilder WithConsoleLogging(bool includeTimestamp = true)
        {
            try
            {
                _logger = new ConsoleLogger(includeTimestamp);
                return this;
            }
            catch (Exception ex)
            {
                throw new DatabaseOperationException("Failed to initialize console logger", ex);
            }
        }

        /// <summary>
        /// Enables file logging with configurable options.
        /// </summary>
        public DatabaseBuilder WithFileLogging(
            string? logFilePath = null,
            bool includeTimestamp = true,
            long maxFileSizeBytes = DefaultMaxLogSize)
        {
            try
            {
                var path = logFilePath ?? $"{_dbPath}.log";

                // Validate and normalize the log file path
                path = Path.GetFullPath(path);
                
                // Validate max file size
                if (maxFileSizeBytes < 0)
                    throw new ArgumentException("Max file size cannot be negative", nameof(maxFileSizeBytes));

                // Check if the directory exists or can be created
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    // Verify write permissions
                    var testFile = Path.Combine(directory, $"test_{Guid.NewGuid()}.tmp");
                    try
                    {
                        File.WriteAllText(testFile, string.Empty);
                        File.Delete(testFile);
                    }
                    catch (Exception ex)
                    {
                        if (ex is UnauthorizedAccessException || ex is IOException)
                        {
                            throw new DatabaseOperationException(
                                $"Log directory is not writable: {directory}", ex);
                        }
                        throw;
                    }
                }

                _logger = new FileLogger(path, includeTimestamp, maxFileSizeBytes);
                return this;
            }
            catch (Exception ex)
            {
                if (ex is DatabaseOperationException)
                    throw;
                    
                throw new DatabaseOperationException("Failed to initialize file logger", ex);
            }
        }

        /// <summary>
        /// Builds and returns a new instance of SecureFileDatabase with the configured options.
        /// </summary>
        public SecureFileDatabase Build()
        {
            try
            {
                return new SecureFileDatabase(_dbPath, _password, _useCompression, _useAutoSave, _logger);
            }
            catch (Exception ex)
            {
                if (ex is DatabaseOperationException || 
                    ex is DatabaseEncryptionException || 
                    ex is DatabaseCorruptException)
                {
                    throw;
                }
                throw new DatabaseOperationException("Failed to build database", ex);
            }
        }
    }
}
