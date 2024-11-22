using System;
using System.Collections.Generic;
using System.Linq;
using SmallBin.Exceptions;
using SmallBin.Logging;
using SmallBin.Models;

namespace SmallBin.Services
{
    /// <summary>
    ///     Provides search functionality for the secure file database
    /// </summary>
    /// <remarks>
    ///     This service handles searching through file entries based on various criteria
    ///     such as filename, tags, and metadata. It supports optional logging of search
    ///     operations for debugging and auditing purposes.
    /// </remarks>
    internal class SearchService
    {
        private readonly ILogger? _logger;

        /// <summary>
        ///     Initializes a new instance of the SearchService class
        /// </summary>
        /// <param name="logger">Optional logger for tracking search operations</param>
        public SearchService(ILogger? logger = null)
        {
            _logger = logger;
        }

        /// <summary>
        ///     Searches through a collection of file entries using the specified criteria
        /// </summary>
        /// <param name="files">The collection of file entries to search through</param>
        /// <param name="criteria">The search criteria to apply</param>
        /// <returns>A collection of file entries matching the search criteria</returns>
        /// <exception cref="ArgumentNullException">Thrown when files collection is null</exception>
        /// <exception cref="DatabaseOperationException">Thrown when the search operation fails</exception>
        /// <remarks>
        ///     If no criteria is specified, all files are returned.
        ///     The search is case-insensitive and supports partial matches for filenames.
        ///     Search operations are logged if a logger was provided during initialization.
        /// </remarks>
        public IEnumerable<FileEntry> Search(IEnumerable<FileEntry> files, SearchCriteria? criteria)
        {
            if (files == null)
                throw new ArgumentNullException(nameof(files));

            _logger?.Debug($"Searching files with criteria: {criteria?.FileName ?? "all"}");

            try
            {
                var query = files.AsEnumerable();

                if (!string.IsNullOrWhiteSpace(criteria?.FileName))
                    query = query.Where(e => e.FileName.Contains(criteria.FileName, StringComparison.OrdinalIgnoreCase));

                if (criteria?.Tags?.Any() == true)
                    query = query.Where(e => e.Tags.Any(t => criteria.Tags.Contains(t)));

                if (!string.IsNullOrWhiteSpace(criteria?.ContentType))
                    query = query.Where(e => e.ContentType.Equals(criteria.ContentType, StringComparison.OrdinalIgnoreCase));

                if (criteria?.StartDate.HasValue == true)
                    query = query.Where(e => e.CreatedOn >= criteria.StartDate.Value);

                if (criteria?.EndDate.HasValue == true)
                    query = query.Where(e => e.CreatedOn <= criteria.EndDate.Value);

                if (criteria?.CustomMetadata?.Any() == true)
                    query = query.Where(e => criteria.CustomMetadata.All(cm => 
                        e.CustomMetadata.ContainsKey(cm.Key) && 
                        e.CustomMetadata[cm.Key].Equals(cm.Value, StringComparison.OrdinalIgnoreCase)));

                var results = query.ToList();
                _logger?.Info($"Search completed. Found {results.Count} matches");
                return results;
            }
            catch (Exception ex)
            {
                _logger?.Error("Search operation failed", ex);
                throw new DatabaseOperationException("Search operation failed", ex);
            }
        }
    }
}
