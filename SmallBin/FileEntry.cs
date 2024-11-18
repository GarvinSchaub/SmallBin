using System;
using System.Collections.Generic;

namespace SmallBin
{
    /// <summary>
    /// Represents a file entry in the system, which includes metadata
    /// and encrypted content for the file stored in the database.
    /// </summary>
    public class FileEntry
    {
        /// <summary>
        /// Gets or sets the unique identifier for the file entry.
        /// This identifier is automatically generated when the file entry is created.
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Gets or sets the name of the file associated with this entry.
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// Gets or sets the collection of tags associated with the file entry.
        /// Tags can be used to group or categorize files for easier retrieval and organization.
        /// </summary>
        public List<string> Tags { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the date and time when the file entry was created.
        /// </summary>
        public DateTime CreatedOn { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the last update made to this file entry.
        /// </summary>
        public DateTime UpdatedOn { get; set; }

        /// <summary>
        /// Gets or sets the size of the file in bytes.
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// Gets or sets the media type of the content, indicating the file format.
        /// This property typically uses MIME types (e.g., "text/plain", "image/jpeg", "application/pdf").
        /// If not specified, the default value is "application/octet-stream".
        /// </summary>
        public string ContentType { get; set; }

        /// <summary>
        /// Indicates whether the file content is compressed before encryption.
        /// If set to true, the file content was compressed; otherwise, it was not.
        /// </summary>
        public bool IsCompressed { get; set; }

        /// <summary>
        /// Gets or sets custom metadata associated with the file entry.
        /// </summary>
        /// <remarks>
        /// Custom metadata can include user-defined key/value pairs that provide additional information
        /// about the file, such as source, description, or any other relevant data.
        /// </remarks>
        public Dictionary<string, string> CustomMetadata { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Gets or sets the content of the file, which is stored encrypted.
        /// </summary>
        public byte[] EncryptedContent { get; set; }  // Store file content directly in the database

        /// <summary>
        /// Gets or sets the initialization vector (IV) used for the encryption.
        /// </summary>
        public byte[] IV { get; set; }  // Store IV for each file
    }
}