using System;
using SmallBin.Logging;

namespace SmallBin
{
    /// <summary>
    /// Builder class for creating instances of SecureFileDatabase with configurable options.
    /// This provides a fluent interface for setting up database configuration.
    /// </summary>
    public class DatabaseBuilder
    {
        private readonly string _dbPath;
        private readonly string _password;
        private bool _useCompression = true;
        private bool _useAutoSave;
        private ILogger? _logger;

        /// <summary>
        /// Initializes a new instance of the DatabaseBuilder with required parameters.
        /// </summary>
        /// <param name="dbPath">The file path where the database will be stored.</param>
        /// <param name="password">The password used for encrypting the database.</param>
        /// <exception cref="ArgumentNullException">Thrown when dbPath or password is null or empty.</exception>
        public DatabaseBuilder(string dbPath, string password)
        {
            if (string.IsNullOrEmpty(dbPath) || string.IsNullOrWhiteSpace(dbPath))
                throw new ArgumentNullException(nameof(dbPath));

            if (string.IsNullOrEmpty(password) || string.IsNullOrWhiteSpace(password))
                throw new ArgumentNullException(nameof(password));

            _dbPath = dbPath;
            _password = password;
        }

        /// <summary>
        /// Disables compression for stored files. By default, compression is enabled.
        /// </summary>
        /// <returns>The current DatabaseBuilder instance for method chaining.</returns>
        public DatabaseBuilder WithoutCompression()
        {
            _useCompression = false;
            return this;
        }

        /// <summary>
        /// Enables automatic saving of changes. By default, auto-save is disabled.
        /// </summary>
        /// <returns>The current DatabaseBuilder instance for method chaining.</returns>
        public DatabaseBuilder WithAutoSave()
        {
            _useAutoSave = true;
            return this;
        }

        /// <summary>
        /// Sets a custom logger implementation for the database.
        /// </summary>
        /// <param name="logger">The logger implementation to use.</param>
        /// <returns>The current DatabaseBuilder instance for method chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when logger is null.</exception>
        public DatabaseBuilder WithLogger(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            return this;
        }

        /// <summary>
        /// Enables console logging with optional timestamp inclusion.
        /// </summary>
        /// <param name="includeTimestamp">Whether to include timestamps in log messages. Default is true.</param>
        /// <returns>The current DatabaseBuilder instance for method chaining.</returns>
        public DatabaseBuilder WithConsoleLogging(bool includeTimestamp = true)
        {
            _logger = new ConsoleLogger(includeTimestamp);
            return this;
        }

        /// <summary>
        /// Enables file logging with configurable options.
        /// </summary>
        /// <param name="logFilePath">The path where log files will be written. If null, uses [dbPath].log</param>
        /// <param name="includeTimestamp">Whether to include timestamps in log messages. Default is true.</param>
        /// <param name="maxFileSizeBytes">Maximum size of log file before rotation. Default is 10MB. Use 0 to disable rotation.</param>
        /// <returns>The current DatabaseBuilder instance for method chaining.</returns>
        public DatabaseBuilder WithFileLogging(
            string? logFilePath = null,
            bool includeTimestamp = true,
            long maxFileSizeBytes = 10 * 1024 * 1024)
        {
            var path = logFilePath ?? $"{_dbPath}.log";
            _logger = new FileLogger(path, includeTimestamp, maxFileSizeBytes);
            return this;
        }

        /// <summary>
        /// Builds and returns a new instance of SecureFileDatabase with the configured options.
        /// </summary>
        /// <returns>A new instance of SecureFileDatabase.</returns>
        public SecureFileDatabase Build()
        {
            return new SecureFileDatabase(_dbPath, _password, _useCompression, _useAutoSave, _logger);
        }
    }
}
