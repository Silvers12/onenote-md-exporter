using System.Collections.Generic;

namespace alxnbl.OneNoteMdExporter.Models
{
    /// <summary>
    /// Represents the differences between current OneNote state and last export
    /// </summary>
    public class ExportDiff
    {
        /// <summary>
        /// Pages that are new (not in the previous manifest)
        /// </summary>
        public List<Page> NewPages { get; set; } = new List<Page>();

        /// <summary>
        /// Pages that have been modified since the last export
        /// </summary>
        public List<Page> ModifiedPages { get; set; } = new List<Page>();

        /// <summary>
        /// Pages that have not changed since the last export
        /// </summary>
        public List<Page> UnchangedPages { get; set; } = new List<Page>();

        /// <summary>
        /// Pages that were in the previous manifest but no longer exist in OneNote
        /// </summary>
        public List<PageManifestEntry> DeletedPages { get; set; } = new List<PageManifestEntry>();

        /// <summary>
        /// Total number of pages to process (new + modified)
        /// </summary>
        public int PagesToExport => NewPages.Count + ModifiedPages.Count;

        /// <summary>
        /// Total number of pages
        /// </summary>
        public int TotalPages => NewPages.Count + ModifiedPages.Count + UnchangedPages.Count;
    }
}
