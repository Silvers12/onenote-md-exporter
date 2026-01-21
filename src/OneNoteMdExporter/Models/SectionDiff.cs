using System.Collections.Generic;

namespace alxnbl.OneNoteMdExporter.Models
{
    /// <summary>
    /// Represents the differences between current OneNote sections and last export manifest
    /// Used to optimize Phase 1 by only loading pages from modified sections
    /// </summary>
    public class SectionDiff
    {
        /// <summary>
        /// Sections that are new (not in the previous manifest)
        /// </summary>
        public List<Section> NewSections { get; set; } = new List<Section>();

        /// <summary>
        /// Sections that have been modified since the last export
        /// </summary>
        public List<Section> ModifiedSections { get; set; } = new List<Section>();

        /// <summary>
        /// Sections that have not changed since the last export
        /// </summary>
        public List<Section> UnchangedSections { get; set; } = new List<Section>();

        /// <summary>
        /// Sections that were in the previous manifest but no longer exist in OneNote
        /// </summary>
        public List<SectionManifestEntry> DeletedSections { get; set; } = new List<SectionManifestEntry>();

        /// <summary>
        /// Total number of sections requiring page loading (new + modified)
        /// </summary>
        public int SectionsToLoad => NewSections.Count + ModifiedSections.Count;

        /// <summary>
        /// Total number of sections
        /// </summary>
        public int TotalSections => NewSections.Count + ModifiedSections.Count + UnchangedSections.Count;
    }
}
