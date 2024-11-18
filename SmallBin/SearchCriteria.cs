using System;
using System.Collections.Generic;

namespace SmallBin
{
    /// <summary>
    /// Represents a set of criteria to filter search results in the database
    /// </summary>
    public class SearchCriteria
    {
        /// <summary>
        /// Gets or sets the name of the file to be searched.
        /// </summary>
        public string? FileName { get; set; }

        /// <summary>
        /// Gets or sets the list of tags used to filter items in a search.
        /// </summary>
        public List<string>? Tags { get; set; }

        /// <summary>
        /// Gets or sets the start date for the search criteria.
        /// This property specifies the earliest date and time for the search.
        /// Allows filtering of search results based on the starting date range.
        /// </summary>
        public DateTime? StartDate { get; set; }

        /// <summary>
        /// Gets or sets the end date for the search criteria.
        /// This property defines the latest date boundary for filtering search results.
        /// Nullable DateTime to allow for optional specification.
        /// </summary>
        public DateTime? EndDate { get; set; }

        /// <summary>
        /// Gets or sets the content type for the search criteria. This property allows filtering files
        /// based on their MIME type, such as "application/pdf" or "image/jpeg".
        /// </summary>
        public string? ContentType { get; set; }

        /// <summary>
        /// Gets or sets a dictionary containing custom metadata associated with the search criteria.
        /// </summary>
        /// <remarks>
        /// This property allows users to specify additional metadata in the form of key-value pairs
        /// that can be used to further refine or enhance the search criteria.
        /// </remarks>
        public Dictionary<string, string>? CustomMetadata { get; set; }
    }
}