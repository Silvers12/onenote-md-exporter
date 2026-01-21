using System;
using System.Collections.Generic;

namespace alxnbl.OneNoteMdExporter.Models
{
    /// <summary>
    /// Manifest file that tracks exported pages and sections for incremental export
    /// </summary>
    public class ExportManifest
    {
        /// <summary>
        /// Current manifest format version
        /// </summary>
        public const string CurrentVersion = "2.0";

        /// <summary>
        /// Version of the manifest format
        /// v1.0: Pages only
        /// v2.0: Pages + Sections for Phase 1 optimization
        /// </summary>
        public string Version { get; set; } = CurrentVersion;

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
        /// Dictionary of exported sections, keyed by OneNote section ID (v2.0+)
        /// </summary>
        public Dictionary<string, SectionManifestEntry> Sections { get; set; } = new Dictionary<string, SectionManifestEntry>();

        /// <summary>
        /// Dictionary of exported pages, keyed by OneNote page ID
        /// </summary>
        public Dictionary<string, PageManifestEntry> Pages { get; set; } = new Dictionary<string, PageManifestEntry>();
    }

    /// <summary>
    /// Entry for a single exported section in the manifest (v2.0+)
    /// </summary>
    public class SectionManifestEntry
    {
        /// <summary>
        /// Title of the section
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// OneNote ID of the section
        /// </summary>
        public string OneNoteId { get; set; }

        /// <summary>
        /// Last modification date from OneNote
        /// </summary>
        public DateTime LastModificationDate { get; set; }

        /// <summary>
        /// Relative path of the section within the notebook hierarchy
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// Whether this is a section group (true) or a regular section (false)
        /// </summary>
        public bool IsSectionGroup { get; set; }

        /// <summary>
        /// Whether the last export had errors for pages in this section.
        /// If true, the section will be reloaded from OneNote on next incremental export.
        /// </summary>
        public bool HasExportErrors { get; set; }
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
        /// OneNote ID of the parent section (v2.0+)
        /// </summary>
        public string SectionId { get; set; }

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
