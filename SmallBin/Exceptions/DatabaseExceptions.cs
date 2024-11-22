using System;

namespace SmallBin.Exceptions
{
    public class DatabaseCorruptException : Exception
    {
        public DatabaseCorruptException(string message) : base(message) { }
        public DatabaseCorruptException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class DatabaseEncryptionException : Exception
    {
        public DatabaseEncryptionException(string message) : base(message) { }
        public DatabaseEncryptionException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class DatabaseOperationException : Exception
    {
        public DatabaseOperationException(string message) : base(message) { }
        public DatabaseOperationException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class InvalidDatabaseStateException : Exception
    {
        public InvalidDatabaseStateException(string message) : base(message) { }
        public InvalidDatabaseStateException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class FileValidationException : Exception
    {
        public FileValidationException(string message) : base(message) { }
        public FileValidationException(string message, Exception innerException) : base(message, innerException) { }
    }
}
