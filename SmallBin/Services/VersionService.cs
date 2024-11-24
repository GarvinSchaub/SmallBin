using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using SmallBin.Models;
using SmallBin.Exceptions;
using SmallBin.Logging;

namespace SmallBin.Services
{
    /// <summary>
    /// Service for managing file versions in the system.
    /// </summary>
    internal class VersionService
    {
        private readonly FileOperationService _fileOperationService;
        private readonly ILogger? _logger;
        private readonly Dictionary<string, FileEntry> _fileEntries;

        /// <summary>
        /// Initializes a new instance of the VersionService class.
        /// </summary>
        /// <param name="fileOperationService">The service used for file operations</param>
        /// <param name="logger">Optional logger for tracking version operations</param>
        public VersionService(FileOperationService fileOperationService, ILogger? logger = null)
        {
            _fileOperationService = fileOperationService ?? throw new ArgumentNullException(nameof(fileOperationService));
            _logger = logger;
            _fileEntries = new Dictionary<string, FileEntry>();
        }

        /// <summary>
        /// Creates a new version of a file.
        /// </summary>
        /// <param name="baseEntry">The base file entry to create a version from</param>
        /// <param name="filePath">The path to the new version's file</param>
        /// <param name="comment">Optional comment describing the version changes</param>
        /// <returns>The new version's FileEntry</returns>
        /// <exception cref="ArgumentNullException">Thrown when baseEntry or filePath is null</exception>
        public FileEntry CreateVersion(FileEntry baseEntry, string filePath, string? comment = null)
        {
            if (baseEntry == null)
                throw new ArgumentNullException(nameof(baseEntry));
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentNullException(nameof(filePath));

            _logger?.Info($"Creating new version for file: {baseEntry.FileName}");

            // If this is already a version, get the base entry
            if (baseEntry.IsVersion)
            {
                throw new InvalidOperationException("Cannot create a version from another version. Use the base file instead.");
            }

            // Save the new version file
            var versionEntry = _fileOperationService.SaveFile(filePath, baseEntry.Tags, baseEntry.ContentType);

            // Update version metadata
            _fileOperationService.UpdateMetadata(versionEntry, entry =>
            {
                entry.BaseFileId = baseEntry.Id;
                entry.Version = baseEntry.HasVersions ? baseEntry.VersionIds.Count + 2 : 2;
                entry.VersionComment = comment;
            });

            // Update base entry's version list
            _fileOperationService.UpdateMetadata(baseEntry, entry =>
            {
                entry.VersionIds.Add(versionEntry.Id);
            });

            // Store entries in local dictionary
            _fileEntries[versionEntry.Id] = versionEntry;
            _fileEntries[baseEntry.Id] = baseEntry;

            _logger?.Info($"Created version {versionEntry.Version} for file: {baseEntry.FileName}");
            return versionEntry;
        }

        /// <summary>
        /// Gets all versions of a file, ordered by version number.
        /// </summary>
        /// <param name="entry">The file entry to get versions for</param>
        /// <returns>A list of all versions, including the base version</returns>
        /// <exception cref="ArgumentNullException">Thrown when entry is null</exception>
        public List<FileEntry> GetVersionHistory(FileEntry entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            var history = new List<FileEntry>();

            // If this is a version, get the base entry
            if (entry.IsVersion && _fileEntries.TryGetValue(entry.BaseFileId!, out var baseEntry))
            {
                entry = baseEntry;
            }

            // Add the base version
            history.Add(entry);

            // Add all subsequent versions
            foreach (var versionId in entry.VersionIds)
            {
                if (_fileEntries.TryGetValue(versionId, out var versionEntry))
                {
                    history.Add(versionEntry);
                }
            }

            // Sort by version number
            return history.OrderBy(v => v.Version).ToList();
        }

        /// <summary>
        /// Gets a specific version of a file.
        /// </summary>
        /// <param name="entry">The file entry</param>
        /// <param name="version">The version number to retrieve</param>
        /// <returns>The requested version's FileEntry</returns>
        /// <exception cref="ArgumentNullException">Thrown when entry is null</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when version number is invalid</exception>
        /// <exception cref="FileNotFoundException">Thrown when the requested version is not found</exception>
        public FileEntry GetVersion(FileEntry entry, int version)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));
            if (version < 1)
                throw new ArgumentOutOfRangeException(nameof(version), "Version number must be greater than 0");

            var history = GetVersionHistory(entry);
            var requestedVersion = history.FirstOrDefault(v => v.Version == version);

            if (requestedVersion == null)
                throw new FileNotFoundException($"Version {version} not found for file: {entry.FileName}");

            return requestedVersion;
        }

        /// <summary>
        /// Gets the latest version of a file.
        /// </summary>
        /// <param name="entry">The file entry</param>
        /// <returns>The latest version's FileEntry</returns>
        /// <exception cref="ArgumentNullException">Thrown when entry is null</exception>
        public FileEntry GetLatestVersion(FileEntry entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            var history = GetVersionHistory(entry);
            return history.Last();
        }

        /// <summary>
        /// Gets the content of a specific version.
        /// </summary>
        /// <param name="entry">The file entry</param>
        /// <param name="version">The version number to retrieve</param>
        /// <returns>The content of the requested version</returns>
        public byte[] GetVersionContent(FileEntry entry, int version)
        {
            var versionEntry = GetVersion(entry, version);
            return _fileOperationService.GetFile(versionEntry);
        }
    }
}
