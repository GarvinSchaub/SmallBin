# SmallBin

[![NuGet](https://img.shields.io/nuget/v/SmallBin.svg)](https://www.nuget.org/packages/SmallBin/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

SmallBin is a lightweight, secure file storage library for .NET that enables storing multiple files in a single encrypted container with metadata support and comprehensive logging capabilities.

## 🔑 Key Features

- Single-file encrypted database
- Password-based encryption (AES-256)
- File metadata and tagging
- File compression support
- Search functionality
- Thread-safe operations
- Flexible logging system with console and file output
- Extensible logging interface

## 📦 Installation

```shell
dotnet add package SmallBin
```

## 🚀 Quick Start

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

// The builder pattern makes it clear which options are being configured
// and allows for future extensibility without breaking changes
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

## 🖥️ WPF Application Example

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

## 🔒 Security Considerations

- Passwords should be strong and securely stored
- Database files contain encrypted content
- Each file has its own encryption IV
- Uses PBKDF2 for key derivation
- All operations are logged for security auditing

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 👥 Contributing

Contributions welcome! NEVER submit pull requests directly to the main branch.

## Version Impact
| Type | Version Change | When to Use |
|------|---------------|-------------|
| `breaking:` | MAJOR (1.0.0) | Incompatible API changes, removing features |
| `major:` | MAJOR (1.0.0) | Same as breaking |
| `feat:` | MINOR (0.1.0) | New features, capabilities, or enhancements |
| `fix:` | PATCH (0.0.1) | Bug fixes, correcting behavior |
| `refactor:` | PATCH | Code restructuring without behavior change |
| `chore:` | PATCH | Build process, dependencies, tooling |
| `style:` | PATCH | Code formatting, naming (no logic change) |
| `test:` | PATCH | Adding/modifying tests |
| `docs:` | NONE | Documentation only |
| `ci:` | NONE | CI/CD changes |

## Examples
```
breaking: remove support for XML config files
feat: add dark mode theme support
fix: prevent crash when user input is empty
chore: update NuGet packages
refactor: simplify login logic
style: fix code indentation
test: add unit tests for auth service
docs: update API documentation
ci: add new deploy stage
```

## Additional Tips
- Use present tense ("add feature" not "added feature")
- Keep first line under 70 characters
- Add scope for clarity: `feat(auth):`, `fix(db):`
- Include ticket number if needed: `feat: add login (#123)`

## ⭐ Show Your Support

If you find this project useful, please consider giving it a star on GitHub!

<a href="https://star-history.com/#GarvinSchaub/SmallBin&Date">
 <picture>
   <source media="(prefers-color-scheme: dark)" srcset="https://api.star-history.com/svg?repos=GarvinSchaub/SmallBin&type=Date&theme=dark" />
   <source media="(prefers-color-scheme: light)" srcset="https://api.star-history.com/svg?repos=GarvinSchaub/SmallBin&type=Date" />
   <img alt="Star History Chart" src="https://api.star-history.com/svg?repos=GarvinSchaub/SmallBin&type=Date" />
 </picture>
</a>
