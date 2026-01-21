using alxnbl.OneNoteMdExporter.Infrastructure;
using alxnbl.OneNoteMdExporter.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace alxnbl.OneNoteMdExporter.Services.Export
{
    /// <summary>
    /// Service for managing export manifests used in incremental exports
    /// </summary>
    public class ExportManifestService
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };

        /// <summary>
        /// Load an existing manifest from the specified path
        /// </summary>
        /// <param name="manifestPath">Path to the manifest file</param>
        /// <returns>The loaded manifest, or null if file doesn't exist or is invalid</returns>
        public ExportManifest LoadManifest(string manifestPath)
        {
            if (!File.Exists(manifestPath))
            {
                Log.Debug("No manifest file found at {ManifestPath}", manifestPath);
                return null;
            }

            try
            {
                var json = File.ReadAllText(manifestPath);
                var manifest = JsonSerializer.Deserialize<ExportManifest>(json, JsonOptions);

                Log.Debug("Loaded manifest from {ManifestPath} with {PageCount} pages",
                    manifestPath, manifest?.Pages?.Count ?? 0);

                // Migrate manifest to current version if needed
                MigrateManifestIfNeeded(manifest);

                return manifest;
            }
            catch (Exception ex)
            {
                Log.Warning("Failed to load manifest from {ManifestPath}: {Error}", manifestPath, ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Migrate manifest from older versions to current version
        /// </summary>
        /// <param name="manifest">The manifest to migrate</param>
        private void MigrateManifestIfNeeded(ExportManifest manifest)
        {
            if (manifest == null) return;

            // Migration from v1.0 to v2.0
            if (manifest.Version == "1.0")
            {
                Log.Information("Migrating manifest from v1.0 to v2.0");

                // Initialize Sections dictionary if null (old manifests won't have it)
                manifest.Sections ??= new Dictionary<string, SectionManifestEntry>();

                // Note: We can't populate Sections from v1.0 manifest because it doesn't have section info
                // The first incremental export after migration will do a full section scan
                // This is expected and documented in the plan

                manifest.Version = ExportManifest.CurrentVersion;
                Log.Information("Manifest migrated to v2.0. First export will scan all sections.");
            }
        }

        /// <summary>
        /// Save a manifest to the specified path
        /// </summary>
        /// <param name="manifest">The manifest to save</param>
        /// <param name="manifestPath">Path where to save the manifest</param>
        public void SaveManifest(ExportManifest manifest, string manifestPath)
        {
            try
            {
                var directory = Path.GetDirectoryName(manifestPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(manifest, JsonOptions);
                File.WriteAllText(manifestPath, json);

                Log.Debug("Saved manifest to {ManifestPath} with {PageCount} pages",
                    manifestPath, manifest.Pages.Count);
            }
            catch (Exception ex)
            {
                Log.Error("Failed to save manifest to {ManifestPath}: {Error}", manifestPath, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Compute the differences between an existing manifest and current OneNote sections
        /// Used to optimize Phase 1 by only loading pages from modified sections
        /// </summary>
        /// <param name="existingManifest">The previously saved manifest (can be null for first export)</param>
        /// <param name="currentSections">List of sections currently in OneNote (excluding section groups)</param>
        /// <returns>The computed section differences</returns>
        public SectionDiff ComputeSectionDiff(ExportManifest existingManifest, List<Section> currentSections)
        {
            var diff = new SectionDiff();

            if (existingManifest == null || existingManifest.Sections == null || existingManifest.Sections.Count == 0)
            {
                // First export or migrated from v1.0: all sections are considered new
                diff.NewSections.AddRange(currentSections.Where(s => !s.IsSectionGroup));
                Log.Debug("Section diff: All {Count} sections marked as new (first export or migration)", diff.NewSections.Count);
                return diff;
            }

            // Create a copy of manifest section IDs to track deleted ones
            var manifestSectionIds = new HashSet<string>(existingManifest.Sections.Keys);

            foreach (var section in currentSections)
            {
                // Skip section groups - we only track actual sections
                if (section.IsSectionGroup) continue;

                if (!existingManifest.Sections.TryGetValue(section.OneNoteId, out var manifestEntry))
                {
                    // Section not in manifest = new section
                    diff.NewSections.Add(section);
                }
                else
                {
                    // Section exists in manifest - check if modified
                    manifestSectionIds.Remove(section.OneNoteId);

                    // Check if section has any pages in manifest (incomplete export protection)
                    bool hasPages = existingManifest.Pages?.Values.Any(p => p.SectionId == section.OneNoteId) ?? false;

                    if (section.LastModificationDate > manifestEntry.LastModificationDate)
                    {
                        // Section has been modified
                        diff.ModifiedSections.Add(section);
                    }
                    else if (!hasPages)
                    {
                        // Section in manifest but no pages - incomplete previous export, reload from OneNote
                        Log.Debug("Section '{Section}' has no pages in manifest - marking as modified for reload", section.Title);
                        diff.ModifiedSections.Add(section);
                    }
                    else if (manifestEntry.HasExportErrors)
                    {
                        // Section had export errors - reload to retry failed pages
                        Log.Debug("Section '{Section}' had export errors - marking as modified for retry", section.Title);
                        diff.ModifiedSections.Add(section);
                    }
                    else
                    {
                        // Section unchanged and has pages
                        diff.UnchangedSections.Add(section);
                    }
                }
            }

            // Remaining sections in manifestSectionIds are deleted
            foreach (var deletedSectionId in manifestSectionIds)
            {
                if (existingManifest.Sections.TryGetValue(deletedSectionId, out var deletedEntry))
                {
                    diff.DeletedSections.Add(deletedEntry);
                }
            }

            Log.Debug("Section diff computed: {New} new, {Modified} modified, {Unchanged} unchanged, {Deleted} deleted",
                diff.NewSections.Count, diff.ModifiedSections.Count, diff.UnchangedSections.Count, diff.DeletedSections.Count);

            return diff;
        }

        /// <summary>
        /// Create a manifest entry for a section
        /// </summary>
        /// <param name="section">The section to create an entry for</param>
        /// <returns>A new section manifest entry</returns>
        public SectionManifestEntry CreateSectionEntry(Section section)
        {
            return new SectionManifestEntry
            {
                Title = section.Title,
                OneNoteId = section.OneNoteId,
                LastModificationDate = section.LastModificationDate,
                Path = section.GetPath(AppSettings.MdMaxFileLength),
                IsSectionGroup = section.IsSectionGroup
            };
        }

        /// <summary>
        /// Get cached pages from manifest for unchanged sections
        /// Creates Page objects with metadata from the manifest (without full OneNote content)
        /// </summary>
        /// <param name="manifest">The existing manifest</param>
        /// <param name="section">The unchanged section to get pages for</param>
        /// <returns>List of Page objects reconstructed from manifest data</returns>
        public List<Page> GetPagesFromManifestForSection(ExportManifest manifest, Section section)
        {
            var pages = new List<Page>();

            if (manifest?.Pages == null) return pages;

            // Find all pages in the manifest that belong to this section
            foreach (var pageEntry in manifest.Pages.Values)
            {
                if (pageEntry.SectionId == section.OneNoteId)
                {
                    // Create a minimal Page object with the cached metadata
                    var page = new Page(section)
                    {
                        Title = pageEntry.Title,
                        OneNoteId = pageEntry.OneNoteId,
                        LastModificationDate = pageEntry.LastModificationDate
                    };
                    pages.Add(page);
                }
            }

            Log.Debug("Retrieved {Count} cached pages from manifest for section '{Section}'", pages.Count, section.Title);
            return pages;
        }

        /// <summary>
        /// Compute the differences between an existing manifest and current OneNote pages
        /// </summary>
        /// <param name="existingManifest">The previously saved manifest (can be null for first export)</param>
        /// <param name="currentPages">List of pages currently in OneNote</param>
        /// <returns>The computed differences</returns>
        public ExportDiff ComputeDiff(ExportManifest existingManifest, List<Page> currentPages)
        {
            var diff = new ExportDiff();

            if (existingManifest == null || existingManifest.Pages == null)
            {
                // First export: all pages are new
                diff.NewPages.AddRange(currentPages);
                return diff;
            }

            // Create a copy of manifest pages to track deleted ones
            var manifestPageIds = new HashSet<string>(existingManifest.Pages.Keys);

            foreach (var page in currentPages)
            {
                if (!existingManifest.Pages.TryGetValue(page.OneNoteId, out var manifestEntry))
                {
                    // Page not in manifest = new page
                    diff.NewPages.Add(page);
                }
                else
                {
                    // Page exists in manifest - check if modified
                    manifestPageIds.Remove(page.OneNoteId);

                    if (page.LastModificationDate > manifestEntry.LastModificationDate)
                    {
                        // Page has been modified
                        diff.ModifiedPages.Add(page);
                    }
                    else
                    {
                        // Page unchanged
                        diff.UnchangedPages.Add(page);
                    }
                }
            }

            // Remaining pages in manifestPageIds are deleted
            foreach (var deletedPageId in manifestPageIds)
            {
                if (existingManifest.Pages.TryGetValue(deletedPageId, out var deletedEntry))
                {
                    diff.DeletedPages.Add(deletedEntry);
                }
            }

            return diff;
        }

        /// <summary>
        /// Create a new manifest entry for a page
        /// </summary>
        /// <param name="page">The page to create an entry for</param>
        /// <param name="exportPath">The relative export path of the page</param>
        /// <param name="sectionId">Optional: The OneNote ID of the parent section (for v2.0+ manifests)</param>
        /// <returns>A new manifest entry</returns>
        public PageManifestEntry CreateEntry(Page page, string exportPath, string sectionId = null)
        {
            return new PageManifestEntry
            {
                Title = page.Title,
                OneNoteId = page.OneNoteId,
                SectionId = sectionId ?? (page.Parent as Section)?.OneNoteId,
                LastModificationDate = page.LastModificationDate,
                ExportPath = exportPath
            };
        }

        /// <summary>
        /// Create a new manifest for a notebook
        /// </summary>
        /// <param name="notebook">The notebook being exported</param>
        /// <param name="exportFormat">The export format code</param>
        /// <returns>A new manifest</returns>
        public ExportManifest CreateManifest(Notebook notebook, string exportFormat)
        {
            return new ExportManifest
            {
                Version = ExportManifest.CurrentVersion,
                NotebookId = notebook.OneNoteId,
                NotebookTitle = notebook.Title,
                ExportFormat = exportFormat,
                LastExportDate = DateTime.Now,
                Sections = new Dictionary<string, SectionManifestEntry>(),
                Pages = new Dictionary<string, PageManifestEntry>()
            };
        }
    }
}
