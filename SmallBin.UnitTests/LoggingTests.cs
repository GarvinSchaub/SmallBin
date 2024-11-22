using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using Xunit;
using SmallBin.Logging;

namespace SmallBin.UnitTests
{
    public class LoggingTests : IDisposable
    {
        private readonly string _testDbPath;
        private readonly string _testPassword;
        private readonly string _testFilesDir;
        private readonly string _testLogPath;
        private readonly List<IDisposable> _disposables;

        public LoggingTests()
        {
            _testDbPath = Path.Combine(Path.GetTempPath(), $"test_db_{Guid.NewGuid()}.sdb");
            _testPassword = "TestPassword123!";
            _testFilesDir = Path.Combine(Path.GetTempPath(), $"test_files_{Guid.NewGuid()}");
            _testLogPath = Path.Combine(Path.GetTempPath(), $"test_log_{Guid.NewGuid()}.log");
            _disposables = new List<IDisposable>();
            Directory.CreateDirectory(_testFilesDir);
        }

        public void Dispose()
        {
            // First dispose all resources
            foreach (var disposable in _disposables)
            {
                try { disposable.Dispose(); } catch { }
            }
            _disposables.Clear();

            // Then clean up files
            try
            {
                if (File.Exists(_testDbPath))
                    File.Delete(_testDbPath);

                // Clean up any rotated log files
                var directory = Path.GetDirectoryName(_testLogPath)!;
                var filePattern = Path.GetFileNameWithoutExtension(_testLogPath) + "*" + Path.GetExtension(_testLogPath);
                foreach (var file in Directory.GetFiles(directory, filePattern))
                {
                    try { File.Delete(file); } catch { }
                }

                if (Directory.Exists(_testFilesDir))
                    Directory.Delete(_testFilesDir, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        [Fact]
        public void FileLogger_ShouldCreateLogFile()
        {
            // Arrange & Act
            var logger = new FileLogger(_testLogPath);
            _disposables.Add(logger);

            logger.Info("Test message");
            logger.Dispose();

            // Assert
            Assert.True(File.Exists(_testLogPath));
            var logContent = ReadLogFile(_testLogPath);
            Assert.Contains("Test message", logContent);
            Assert.Contains("[INFO]", logContent);
        }

        [Fact]
        public void FileLogger_WithoutTimestamp_ShouldNotIncludeTimestamp()
        {
            // Arrange & Act
            var logger = new FileLogger(_testLogPath, includeTimestamp: false);
            _disposables.Add(logger);

            logger.Info("Test message");
            logger.Dispose();

            // Assert
            var logContent = ReadLogFile(_testLogPath);
            Assert.DoesNotMatch(@"\d{4}-\d{2}-\d{2}", logContent); // No date format
        }

        [Fact]
        public void DatabaseBuilder_WithFileLogging_ShouldCreateLogFile()
        {
            // Arrange
            var testFilePath = CreateTestFile("test.txt", "Test content");

            // Act
            using var db = SecureFileDatabase.Create(_testDbPath, _testPassword)
                .WithFileLogging(_testLogPath)
                .Build();
            
            db.SaveFile(testFilePath);

            // Assert
            Assert.True(File.Exists(_testLogPath));
            var logContent = ReadLogFile(_testLogPath);
            Assert.Contains("Saving file:", logContent);
            Assert.Contains("File saved successfully", logContent);
        }

        [Fact]
        public void DatabaseBuilder_WithCustomLogger_ShouldUseCustomLogger()
        {
            // Arrange
            var testFilePath = CreateTestFile("test.txt", "Test content");
            var customLogger = new TestLogger();
            _disposables.Add(customLogger);

            // Act
            using var db = SecureFileDatabase.Create(_testDbPath, _testPassword)
                .WithLogger(customLogger)
                .Build();
            
            db.SaveFile(testFilePath);

            // Assert
            Assert.Contains(customLogger.Messages, m => m.Contains("Saving file:"));
            Assert.Contains(customLogger.Messages, m => m.Contains("File saved successfully"));
        }

        private string CreateTestFile(string fileName, string content)
        {
            var filePath = Path.Combine(_testFilesDir, fileName);
            File.WriteAllText(filePath, content);
            return filePath;
        }

        private string ReadLogFile(string path)
        {
            // Retry a few times in case the file is still being written
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    using var reader = new StreamReader(stream, Encoding.UTF8);
                    return reader.ReadToEnd();
                }
                catch (IOException)
                {
                    if (i == 2) throw;
                    Thread.Sleep(100);
                }
            }
            throw new IOException("Failed to read log file after retries");
        }

        private class TestLogger : ILogger
        {
            public List<string> Messages { get; } = new List<string>();

            public void Info(string message) => Messages.Add($"[INFO] {message}");
            public void Warning(string message) => Messages.Add($"[WARN] {message}");
            public void Error(string message, Exception? exception = null) => 
                Messages.Add($"[ERROR] {message}{(exception != null ? $" - {exception.Message}" : "")}");
            public void Debug(string message) => Messages.Add($"[DEBUG] {message}");
            public void Dispose() { }
        }
    }
}
