using System.Collections.Generic;

namespace SmallBin.Models
{
    /// <summary>
    ///     Represents the content of a database, including a collection of files and the version of the content
    /// </summary>
    /// <remarks>
    ///     This class serves as the root container for all database content and is serialized/deserialized
    ///     during database persistence operations. It maintains:
    ///     - A dictionary of file entries indexed by their unique identifiers
    ///     - Version information for database schema compatibility
    ///     
    ///     The version property allows for future database schema migrations and backwards compatibility
    ///     checks when loading older database files.
    /// </remarks>
    public class DatabaseContent
    {
        /// <summary>
        ///     Gets or sets the collection of file entries in the database
        /// </summary>
        /// <value>
        ///     A dictionary where the key is a string representing the file identifier,
        ///     and the value is an instance of <see cref="FileEntry" /> containing file details
        /// </value>
        /// <remarks>
        ///     The dictionary structure provides O(1) lookup of files by their ID.
        ///     File entries are stored with their complete metadata and encrypted content.
        ///     The dictionary is initialized as empty to ensure it's never null.
        /// </remarks>
        // ReSharper disable once HeapView.ObjectAllocation.Evident
        public Dictionary<string, FileEntry> Files { get; set; } = new Dictionary<string, FileEntry>();

        /// <summary>
        ///     Gets or sets the version of the database content
        /// </summary>
        /// <remarks>
        ///     The version string follows semantic versioning (MAJOR.MINOR) format.
        ///     This version represents the database schema version, not the application version.
        ///     Changes to this version indicate structural changes to the database format.
        /// </remarks>
        public string Version { get; set; } = "1.0";
    }
}
