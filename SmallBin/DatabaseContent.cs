using System.Collections.Generic;

namespace SmallBin
{
    /// <summary>
    ///     Represents the content of a database, including a collection of files and the version of the content.
    /// </summary>
    public class DatabaseContent
    {
        /// <summary>
        ///     Gets or sets the collection of file entries in the database.
        /// </summary>
        /// <value>
        ///     A dictionary where the key is a string representing the file identifier,
        ///     and the value is an instance of <see cref="FileEntry" /> containing file details.
        /// </value>
        public Dictionary<string, FileEntry> Files { get; set; } = new Dictionary<string, FileEntry>();

        /// <summary>
        ///     Gets or sets the version of the database content.
        /// </summary>
        public string Version { get; set; } = "1.0";
    }
}