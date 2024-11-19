# SmallBin

[![NuGet](https://img.shields.io/nuget/v/SmallBin.svg)](https://www.nuget.org/packages/SmallBin/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A lightweight, secure file storage library for .NET that enables storing multiple files in a single encrypted container with metadata support.

## 🔑 Key Features

- Single-file encrypted database
- Password-based encryption (AES-256)
- File metadata and tagging
- File compression support
- Search functionality
- Thread-safe operations

## 📦 Installation

```shell
dotnet add package SmallBin
```

## 🚀 Quick Start

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

## 🖥️ WPF Application Example

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

## 🔒 Security Considerations

- Passwords should be strong and securely stored
- Database files contain encrypted content
- Each file has its own encryption IV
- Uses PBKDF2 for key derivation

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 👥 Contributing

Contributions are welcome! Please note:
- NEVER submit pull requests directly to the main branch
- Follow the existing code style
- Include appropriate unit tests
- Update documentation as needed

## ⭐ Show Your Support

If you find this project useful, please consider giving it a star on GitHub!

<a href="https://star-history.com/#GarvinSchaub/SmallBin&Date">
 <picture>
   <source media="(prefers-color-scheme: dark)" srcset="https://api.star-history.com/svg?repos=GarvinSchaub/SmallBin&type=Date&theme=dark" />
   <source media="(prefers-color-scheme: light)" srcset="https://api.star-history.com/svg?repos=GarvinSchaub/SmallBin&type=Date" />
   <img alt="Star History Chart" src="https://api.star-history.com/svg?repos=GarvinSchaub/SmallBin&type=Date" />
 </picture>
</a>
