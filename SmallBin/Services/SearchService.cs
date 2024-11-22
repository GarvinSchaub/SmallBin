using System;
using System.Collections.Generic;
using System.Linq;
using SmallBin.Exceptions;
using SmallBin.Logging;

namespace SmallBin.Services
{
    internal class SearchService
    {
        private readonly ILogger? _logger;

        public SearchService(ILogger? logger = null)
        {
            _logger = logger;
        }

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

                // Add additional search criteria here as needed
                // For example:
                // if (criteria?.Tags?.Any() == true)
                //     query = query.Where(e => e.Tags.Any(t => criteria.Tags.Contains(t)));
                // if (criteria?.ContentType != null)
                //     query = query.Where(e => e.ContentType.Equals(criteria.ContentType, StringComparison.OrdinalIgnoreCase));

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
