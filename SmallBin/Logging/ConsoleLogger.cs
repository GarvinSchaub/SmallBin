using System;

namespace SmallBin.Logging
{
    /// <summary>
    /// A logger implementation that writes to the console with color-coded output.
    /// </summary>
    public class ConsoleLogger : ILogger
    {
        private readonly bool _includeTimestamp;
        private readonly object _lock = new object();

        /// <summary>
        /// Initializes a new instance of ConsoleLogger.
        /// </summary>
        /// <param name="includeTimestamp">Whether to include timestamps in log messages. Default is true.</param>
        public ConsoleLogger(bool includeTimestamp = true)
        {
            _includeTimestamp = includeTimestamp;
        }

        /// <summary>
        /// Logs an informational message in white.
        /// </summary>
        public void Info(string message)
        {
            lock (_lock)
            {
                Console.ForegroundColor = ConsoleColor.White;
                WriteMessage("INFO", message);
                Console.ResetColor();
            }
        }

        /// <summary>
        /// Logs a warning message in yellow.
        /// </summary>
        public void Warning(string message)
        {
            lock (_lock)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                WriteMessage("WARN", message);
                Console.ResetColor();
            }
        }

        /// <summary>
        /// Logs an error message in red, optionally including exception details.
        /// </summary>
        public void Error(string message, Exception? exception = null)
        {
            lock (_lock)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                WriteMessage("ERROR", message);
                if (exception != null)
                {
                    Console.WriteLine($"Exception: {exception.Message}");
                    Console.WriteLine($"Stack Trace: {exception.StackTrace}");
                }
                Console.ResetColor();
            }
        }

        /// <summary>
        /// Logs a debug message in gray.
        /// </summary>
        public void Debug(string message)
        {
            lock (_lock)
            {
                Console.ForegroundColor = ConsoleColor.Gray;
                WriteMessage("DEBUG", message);
                Console.ResetColor();
            }
        }

        private void WriteMessage(string level, string message)
        {
            var timestamp = _includeTimestamp ? $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} " : "";
            Console.WriteLine($"{timestamp}[{level}] {message}");
        }

        public void Dispose()
        {
            // Nothing to dispose
            GC.SuppressFinalize(this);
        }
    }
}
