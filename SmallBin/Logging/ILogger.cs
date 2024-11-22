using System;

namespace SmallBin.Logging
{
    /// <summary>
    /// Interface for implementing custom loggers in SmallBin.
    /// </summary>
    public interface ILogger : IDisposable
    {
        /// <summary>
        /// Logs an informational message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        void Info(string message);

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        void Warning(string message);

        /// <summary>
        /// Logs an error message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        /// <param name="exception">Optional exception to include in the log.</param>
        void Error(string message, Exception? exception = null);

        /// <summary>
        /// Logs a debug message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        void Debug(string message);
    }
}
