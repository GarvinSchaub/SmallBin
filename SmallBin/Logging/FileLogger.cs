using System;
using System.IO;
using System.Text;

namespace SmallBin.Logging
{
    /// <summary>
    /// A logger implementation that writes to a file with optional file rotation.
    /// </summary>
    public class FileLogger : ILogger
    {
        private readonly string _logFilePath;
        private readonly bool _includeTimestamp;
        private readonly long _maxFileSizeBytes;
        private readonly object _lock = new object();
        private readonly Encoding _encoding;
        private FileStream? _fileStream;
        private StreamWriter? _writer;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of FileLogger.
        /// </summary>
        /// <param name="logFilePath">The path where log files will be written.</param>
        /// <param name="includeTimestamp">Whether to include timestamps in log messages. Default is true.</param>
        /// <param name="maxFileSizeBytes">Maximum size of log file before rotation. Default is 10MB. Use 0 to disable rotation.</param>
        /// <param name="encoding">The encoding to use for writing logs. Default is UTF-8.</param>
        public FileLogger(
            string logFilePath,
            bool includeTimestamp = true,
            long maxFileSizeBytes = 10 * 1024 * 1024, // 10MB default
            Encoding? encoding = null)
        {
            if (string.IsNullOrWhiteSpace(logFilePath))
                throw new ArgumentNullException(nameof(logFilePath));

            _logFilePath = logFilePath;
            _includeTimestamp = includeTimestamp;
            _maxFileSizeBytes = maxFileSizeBytes;
            _encoding = encoding ?? new UTF8Encoding(false); // Use UTF8 without BOM

            var directory = Path.GetDirectoryName(logFilePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            // If file exists and is over size limit, rotate it immediately
            if (_maxFileSizeBytes > 0 && File.Exists(_logFilePath))
            {
                var fileInfo = new FileInfo(_logFilePath);
                if (fileInfo.Length >= _maxFileSizeBytes)
                {
                    RotateExistingLog();
                }
            }

            InitializeWriter();
        }

        /// <summary>
        /// Logs an informational message.
        /// </summary>
        void ILogger.Info(string message) => Info(message);

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        void ILogger.Warning(string message) => Warning(message);

        /// <summary>
        /// Logs an error message, optionally including exception details.
        /// </summary>
        void ILogger.Error(string message, Exception? exception) => Error(message, exception);

        /// <summary>
        /// Logs a debug message.
        /// </summary>
        void ILogger.Debug(string message) => Debug(message);

        /// <summary>
        /// Releases all resources used by the logger.
        /// </summary>
        void IDisposable.Dispose() => Dispose();

        /// <summary>
        /// Logs an informational message.
        /// </summary>
        public void Info(string message)
        {
            WriteMessage("INFO", message);
        }

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        public void Warning(string message)
        {
            WriteMessage("WARN", message);
        }

        /// <summary>
        /// Logs an error message, optionally including exception details.
        /// </summary>
        public void Error(string message, Exception? exception = null)
        {
            var sb = new StringBuilder(message);
            if (exception != null)
            {
                sb.AppendLine($"Exception: {exception.Message}");
                sb.AppendLine($"Stack Trace: {exception.StackTrace}");
            }
            WriteMessage("ERROR", sb.ToString());
        }

        /// <summary>
        /// Logs a debug message.
        /// </summary>
        public void Debug(string message)
        {
            WriteMessage("DEBUG", message);
        }

        private void WriteMessage(string level, string message)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(FileLogger));

            lock (_lock)
            {
                var timestamp = _includeTimestamp ? $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} " : "";
                var fullMessage = $"{timestamp}[{level}] {message}{Environment.NewLine}";

                // Check if we need to rotate
                if (_maxFileSizeBytes > 0 && _fileStream != null)
                {
                    var currentSize = _fileStream.Length;
                    if (currentSize >= _maxFileSizeBytes)
                    {
                        Console.WriteLine($"Rotating log file at size: {currentSize} bytes"); // Debug output
                        RotateLog();
                    }
                }

                _writer?.Write(fullMessage);
                _writer?.Flush();
            }
        }

        private void InitializeWriter()
        {
            CloseWriter();

            var fileMode = File.Exists(_logFilePath) ? FileMode.Append : FileMode.Create;
            _fileStream = new FileStream(_logFilePath, fileMode, FileAccess.Write, FileShare.Read | FileShare.Delete);
            _writer = new StreamWriter(_fileStream, _encoding, 1024, true) // Use 1KB buffer and leave stream open
            {
                AutoFlush = true
            };
        }

        private void CloseWriter()
        {
            if (_writer != null)
            {
                _writer.Flush();
                _writer.Dispose();
                _writer = null;
            }
            if (_fileStream != null)
            {
                _fileStream.Dispose();
                _fileStream = null;
            }
        }

        private void RotateExistingLog()
        {
            if (!File.Exists(_logFilePath)) return;

            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var rotatedPath = $"{_logFilePath}.{timestamp}";
            
            // Ensure unique filename for rotation
            int counter = 0;
            while (File.Exists(rotatedPath))
            {
                counter++;
                rotatedPath = $"{_logFilePath}.{timestamp}.{counter}";
            }

            try
            {
                File.Move(_logFilePath, rotatedPath);
                Console.WriteLine($"Rotated existing log to: {rotatedPath}"); // Debug output
            }
            catch (IOException)
            {
                // If move fails, try to create a new file anyway
                try { File.Delete(_logFilePath); } catch { }
            }
        }

        private void RotateLog()
        {
            if (_writer == null) return;

            // Ensure everything is written before rotating
            _writer.Flush();
            CloseWriter();

            RotateExistingLog();
            InitializeWriter();
        }

        /// <summary>
        /// Releases all resources used by the logger.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            lock (_lock)
            {
                if (_disposed)
                    return;

                CloseWriter();
                _disposed = true;
            }

            GC.SuppressFinalize(this);
        }
    }
}
