# SmallBin

SmallBin is a lightweight, secure file storage library for .NET that enables storing multiple files in a single encrypted container with metadata support and comprehensive logging capabilities.

## Features

- Single-file encrypted database
- Password-based encryption (AES-256)
- File metadata and tagging
- File compression support
- Search functionality
- Thread-safe operations
- Flexible logging system with console and file output
- Extensible logging interface

## Installation

```bash
dotnet add package SmallBin
```

## Usage

### Creating a New Database

```csharp
using SmallBin;
using SmallBin.Logging;

// Optional: Configure logging
var logger = new ConsoleLogger(); // or new FileLogger("app.log")

// Create or open an encrypted database using the builder pattern
using var db = SecureFileDatabase.Create("mydata.sdb", "password123")
    .WithoutCompression()  // Optional: disable compression (enabled by default)
    .WithAutoSave()       // Optional: enable auto-save (disabled by default)
    .WithLogger(logger)   // Optional: add logging support
    .Build();
```

### Adding Files

```csharp
// Add a single file with tags
db.SaveFile("document.pdf",
    tags: new List<string> { "work", "reports" },
    contentType: "application/pdf");

// Add multiple files
foreach (string file in Directory.GetFiles("source", "*.jpg"))
{
    db.SaveFile(file,
        tags: new List<string> { "photos" },
        contentType: "image/jpeg");
}
```

### Retrieving Files

```csharp
// Get file by ID
byte[] fileContent = db.GetFile(fileId);
File.WriteAllBytes("exported.pdf", fileContent);
```

### Searching Files

```csharp
// Search by filename
var criteria = new SearchCriteria { FileName = "report" };
var files = db.Search(criteria);

// Search by tags
var photoFiles = db.Search(new SearchCriteria 
{ 
    Tags = new List<string> { "photos" } 
});

// Advanced search with multiple criteria
var searchCriteria = new SearchCriteria
{
    FileName = "report",
    Tags = new List<string> { "important" },
    StartDate = DateTime.Now.AddDays(-7),
    EndDate = DateTime.Now,
    ContentType = "application/pdf",
    CustomMetadata = new Dictionary<string, string>
    {
        { "author", "John Doe" }
    }
};
var results = db.Search(searchCriteria);
```

### Managing Metadata

```csharp
db.UpdateMetadata(fileId, entry => 
{
    entry.Tags.Add("important");
    entry.CustomMetadata["author"] = "John Doe";
    entry.ContentType = "application/pdf";
});
```

### Deleting Files

```csharp
db.DeleteFile(fileId);
```

### Configuring Logging

```csharp
// Console logging
var consoleLogger = new ConsoleLogger();

// File logging
var fileLogger = new FileLogger("app.log");

// Custom logging by implementing ILogger
public class CustomLogger : ILogger
{
    public void Log(string message)
    {
        // Custom logging implementation
    }
}

// Using multiple loggers
var db = SecureFileDatabase.Create("mydata.sdb", "password123")
    .WithLogger(consoleLogger)
    .WithLogger(fileLogger)
    .Build();
```

## WPF Application Example

```csharp
public partial class MainWindow : Window
{
    private SecureFileDatabase _db;
    private ILogger _logger;

    private void OpenDatabase()
    {
        _logger = new FileLogger("app.log");
        _db = SecureFileDatabase.Create("data.sdb", "password123")
            .WithAutoSave() // Enable auto-save for WPF applications
            .WithLogger(_logger)
            .Build();
    }

    private void AddFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Multiselect = true };
        if (dialog.ShowDialog() == true)
        {
            foreach (string filename in dialog.FileNames)
            {
                _db.SaveFile(filename);
            }
        }
    }
}
```

## Security Considerations

- Passwords should be strong and securely stored
- Database files contain encrypted content
- Each file has its own encryption IV
- Uses PBKDF2 for key derivation
- All operations are logged for security auditing
- AES-256 encryption for maximum security
- Thread-safe operations for concurrent access

## License

MIT License

## Contributing

Contributions welcome! NEVER submit pull requests directly to the main branch.
