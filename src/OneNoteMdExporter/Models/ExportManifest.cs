using System;
using System.Collections.Generic;

namespace alxnbl.OneNoteMdExporter.Models
{
    /// <summary>
    /// Manifest file that tracks exported pages for incremental export
    /// </summary>
    public class ExportManifest
    {
        /// <summary>
        /// Version of the manifest format
        /// </summary>
        public string Version { get; set; } = "1.0";

        /// <summary>
        /// OneNote ID of the notebook
        /// </summary>
        public string NotebookId { get; set; }

        /// <summary>
        /// Title of the notebook
        /// </summary>
        public string NotebookTitle { get; set; }

        /// <summary>
        /// Export format used (md, joplin-raw-dir)
        /// </summary>
        public string ExportFormat { get; set; }

        /// <summary>
        /// Date of the last export
        /// </summary>
        public DateTime LastExportDate { get; set; }

        /// <summary>
        /// Dictionary of exported pages, keyed by OneNote page ID
        /// </summary>
        public Dictionary<string, PageManifestEntry> Pages { get; set; } = new Dictionary<string, PageManifestEntry>();
    }

    /// <summary>
    /// Entry for a single exported page in the manifest
    /// </summary>
    public class PageManifestEntry
    {
        /// <summary>
        /// Title of the page
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// OneNote ID of the page
        /// </summary>
        public string OneNoteId { get; set; }

        /// <summary>
        /// Last modification date from OneNote
        /// </summary>
        public DateTime LastModificationDate { get; set; }

        /// <summary>
        /// Relative path of the exported file within the export folder
        /// </summary>
        public string ExportPath { get; set; }
    }
}
