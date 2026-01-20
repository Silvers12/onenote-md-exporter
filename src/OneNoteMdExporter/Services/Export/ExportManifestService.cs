using alxnbl.OneNoteMdExporter.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
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

                return manifest;
            }
            catch (Exception ex)
            {
                Log.Warning("Failed to load manifest from {ManifestPath}: {Error}", manifestPath, ex.Message);
                return null;
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
        /// <returns>A new manifest entry</returns>
        public PageManifestEntry CreateEntry(Page page, string exportPath)
        {
            return new PageManifestEntry
            {
                Title = page.Title,
                OneNoteId = page.OneNoteId,
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
                NotebookId = notebook.OneNoteId,
                NotebookTitle = notebook.Title,
                ExportFormat = exportFormat,
                LastExportDate = DateTime.Now,
                Pages = new Dictionary<string, PageManifestEntry>()
            };
        }
    }
}
