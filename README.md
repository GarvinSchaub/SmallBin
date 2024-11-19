# SmallBin

SmallBin is a lightweight, secure file storage library for .NET that enables storing multiple files in a single encrypted container with metadata support.

## Features

- Single-file encrypted database
- Password-based encryption (AES-256)
- File metadata and tagging
- File compression support
- Search functionality
- Thread-safe operations

## Installation

```bash
dotnet add package SmallBin
```

## Usage

### Creating a New Database

```csharp
using SmallBin;

// Create or open an encrypted database
using var db = new SecureFileDatabase("mydata.sdb", "password123");
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

## WPF Application Example

```csharp
public partial class MainWindow : Window
{
    private SecureFileDatabase _db;

    private void OpenDatabase()
    {
        _db = new SecureFileDatabase("data.sdb", "password123");
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

## License

MIT License

## Contributing

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
