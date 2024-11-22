using System;
using System.IO;
using SmallBin.Exceptions;
using SmallBin.Logging;

namespace SmallBin.Core
{
    /// <summary>
    ///     Builder class for creating instances of SecureFileDatabase with configurable options
    /// </summary>
    /// <remarks>
    ///     This class implements the builder pattern to provide a fluent interface for configuring
    ///     database instances. It handles validation of paths and permissions, initialization of
    ///     logging systems, and ensures proper configuration before creating the database.
    ///     
    ///     Key features include:
    ///     - Path validation and directory creation
    ///     - Permission checking
    ///     - Configurable compression
    ///     - Auto-save options
    ///     - Flexible logging configuration
    /// </remarks>
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
        ///     Initializes a new instance of the DatabaseBuilder with required parameters
        /// </summary>
        /// <param name="dbPath">The path where the database file will be stored</param>
        /// <param name="password">The password used for encrypting the database content</param>
        /// <exception cref="ArgumentNullException">Thrown when dbPath or password is null, empty, or whitespace</exception>
        /// <exception cref="ArgumentException">Thrown when password length is less than the minimum required</exception>
        /// <exception cref="DatabaseOperationException">Thrown when the database path is invalid or not writable</exception>
        /// <remarks>
        ///     This constructor performs several validations:
        ///     - Checks for null or empty parameters
        ///     - Validates password length
        ///     - Verifies directory existence and creates it if needed
        ///     - Checks write permissions by attempting to create a test file
        /// </remarks>
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
        ///     Disables compression for stored files
        /// </summary>
        /// <returns>The current builder instance for method chaining</returns>
        /// <remarks>
        ///     By default, compression is enabled. Disabling compression may be useful
        ///     for already compressed file types like ZIP or media files.
        /// </remarks>
        public DatabaseBuilder WithoutCompression()
        {
            _useCompression = false;
            return this;
        }

        /// <summary>
        ///     Enables automatic saving of changes
        /// </summary>
        /// <returns>The current builder instance for method chaining</returns>
        /// <remarks>
        ///     By default, auto-save is disabled. Enabling auto-save ensures changes
        ///     are immediately persisted to disk after each operation, but may impact
        ///     performance when performing multiple operations in sequence.
        /// </remarks>
        public DatabaseBuilder WithAutoSave()
        {
            _useAutoSave = true;
            return this;
        }

        /// <summary>
        ///     Sets a custom logger implementation for the database
        /// </summary>
        /// <param name="logger">The logger implementation to use</param>
        /// <returns>The current builder instance for method chaining</returns>
        /// <exception cref="ArgumentNullException">Thrown when logger is null</exception>
        /// <remarks>
        ///     Custom loggers must implement the ILogger interface. This method allows
        ///     for integration with existing logging frameworks or custom logging solutions.
        /// </remarks>
        public DatabaseBuilder WithLogger(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger), "Logger cannot be null");
            return this;
        }

        /// <summary>
        ///     Enables console logging with optional timestamp inclusion
        /// </summary>
        /// <param name="includeTimestamp">Whether to include timestamps in log messages</param>
        /// <returns>The current builder instance for method chaining</returns>
        /// <exception cref="DatabaseOperationException">Thrown when console logger initialization fails</exception>
        /// <remarks>
        ///     Console logging is useful for development and debugging purposes.
        ///     Timestamps are included by default for better log message tracking.
        /// </remarks>
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
        ///     Enables file logging with configurable options
        /// </summary>
        /// <param name="logFilePath">Optional custom path for the log file. If not specified, uses database path with .log extension</param>
        /// <param name="includeTimestamp">Whether to include timestamps in log messages</param>
        /// <param name="maxFileSizeBytes">Maximum size of the log file in bytes before rotation</param>
        /// <returns>The current builder instance for method chaining</returns>
        /// <exception cref="ArgumentException">Thrown when maxFileSizeBytes is negative</exception>
        /// <exception cref="DatabaseOperationException">Thrown when file logger initialization fails or directory is not writable</exception>
        /// <remarks>
        ///     File logging provides persistent logging for production environments.
        ///     Features include:
        ///     - Automatic log file creation
        ///     - Optional timestamps
        ///     - Log file size limiting
        ///     - Directory creation if needed
        ///     - Write permission verification
        /// </remarks>
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
        ///     Builds and returns a new instance of SecureFileDatabase with the configured options
        /// </summary>
        /// <returns>A new SecureFileDatabase instance</returns>
        /// <exception cref="DatabaseOperationException">Thrown when database creation fails</exception>
        /// <exception cref="DatabaseEncryptionException">Thrown when encryption setup fails</exception>
        /// <exception cref="DatabaseCorruptException">Thrown when loading an existing corrupt database</exception>
        /// <remarks>
        ///     This method creates the actual database instance with all configured options.
        ///     If the database file already exists, it will be loaded and validated.
        ///     If the file doesn't exist, a new database will be created.
        /// </remarks>
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
