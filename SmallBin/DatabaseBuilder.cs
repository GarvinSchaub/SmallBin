using System;

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
        /// Builds and returns a new instance of SecureFileDatabase with the configured options.
        /// </summary>
        /// <returns>A new instance of SecureFileDatabase.</returns>
        public SecureFileDatabase Build()
        {
            return new SecureFileDatabase(_dbPath, _password, _useCompression, _useAutoSave);
        }
    }
}
