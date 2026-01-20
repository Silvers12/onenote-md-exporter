using alxnbl.OneNoteMdExporter.Helpers;
using alxnbl.OneNoteMdExporter.Infrastructure;
using alxnbl.OneNoteMdExporter.Models;
using Serilog;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.Converters;
using YamlDotNet.Serialization.NamingConventions;

namespace alxnbl.OneNoteMdExporter.Services.Export
{
    /// <summary>
    /// Markdown exporter Service
    /// </summary>
    public class MdExportService : ExportServiceBase
    {
        protected override string ExportFormatCode { get; } = "md";

        protected override string GetResourceFolderPath(Page page)
        {
            if (AppSettings.ResourceFolderLocation == ResourceFolderLocationEnum.RootFolder)
                return Path.Combine(page.GetNotebook().ExportFolder, AppSettings.ResourceFolderName);
            else
                return Path.Combine(Path.GetDirectoryName(GetPageMdFilePath(page)), AppSettings.ResourceFolderName);
        }

        protected override string GetPageMdFilePath(Page page)
        {
            if (page.OverridePageFilePath == null)
            {
                var defaultPath = Path.Combine(page.GetNotebook().ExportFolder, page.GetPageFileRelativePath(AppSettings.MdMaxFileLength) + ".md");

                if (AppSettings.ProcessingOfPageHierarchy == PageHierarchyEnum.HierarchyAsFolderTree)
                {
                    if (page.ParentPage != null)
                        return Path.Combine(Path.ChangeExtension(GetPageMdFilePath(page.ParentPage), null), page.TitleWithNoInvalidChars(AppSettings.MdMaxFileLength) + ".md");
                    else
                        return defaultPath;
                }
                else if (AppSettings.ProcessingOfPageHierarchy == PageHierarchyEnum.HierarchyAsPageTitlePrefix)
                {
                    if (page.ParentPage != null)
                        return String.Concat(Path.ChangeExtension(GetPageMdFilePath(page.ParentPage), null), AppSettings.PageHierarchyFileNamePrefixSeparator, page.TitleWithNoInvalidChars(AppSettings.MdMaxFileLength) + ".md");
                    else
                        return defaultPath;
                }
                else
                    return defaultPath;
            }
            else
            {
                return page.OverridePageFilePath;
            }
        }

        protected override string GetAttachmentFilePath(Attachement attachment)
        {
            if (attachment.OverrideExportFilePath == null)
                return Path.Combine(GetResourceFolderPath(attachment.ParentPage), attachment.Id + Path.GetExtension(attachment.FriendlyFileName));
            else
                return attachment.OverrideExportFilePath;
        }

        /// <summary>
        /// Get relative path from Image's folder to attachment folder
        /// </summary>
        /// <param name="attachment"></param>
        /// <returns></returns>
        protected override string GetAttachmentMdReference(Attachement attachment)
            => Path.GetRelativePath(Path.GetDirectoryName(GetPageMdFilePath(attachment.ParentPage)), GetAttachmentFilePath(attachment)).Replace("\\", "/");

        public override NotebookExportResult ExportNotebookInTargetFormat(Notebook notebook, ExportManifest existingManifest, string sectionNameFilter = "", string pageNameFilter = "")
        {
            var result = new NotebookExportResult();

            // Get all sections and section groups, or the one specified in parameter if any
            var sections = notebook.GetSections().Where(s => string.IsNullOrEmpty(sectionNameFilter) || s.Title == sectionNameFilter).ToList();

            Log.Information(String.Format(Localizer.GetString("FoundXSections"), sections.Count));

            // Phase 1: Build complete tree and collect metadata
            Log.Information(Localizer.GetString("NotebookProcessingStartingPhase1"));
            var allPages = new List<Page>();

            // Export each section
            int cmptSect = 0;
            foreach (Section section in sections)
            {
                Log.Information($"- {Localizer.GetString("Section")} ({++cmptSect}/{sections.Count}) :  {section.GetPath(AppSettings.MdMaxFileLength)}\\{section.Title}");

                if (section.IsSectionGroup)
                    throw new InvalidOperationException("Cannot call ExportSection on section group with MdExport");

                // Get pages list and collect metadata
                var pages = OneNoteApp.Instance.FillSectionPages(section).Where(p => string.IsNullOrEmpty(pageNameFilter) || p.Title == pageNameFilter).ToList();
                allPages.AddRange(pages);
            }

            // Compute diff if incremental mode is enabled
            ExportDiff diff = null;
            ExportManifest newManifest = null;

            if (AppSettings.IncrementalExport)
            {
                diff = ManifestService.ComputeDiff(existingManifest, allPages);
                newManifest = ManifestService.CreateManifest(notebook, ExportFormatCode);

                // Log statistics
                Log.Information(String.Format(Localizer.GetString("IncrementalStats"),
                    allPages.Count, diff.NewPages.Count, diff.ModifiedPages.Count,
                    diff.UnchangedPages.Count, diff.DeletedPages.Count));
            }

            // Phase 2: Export content and convert to markdown
            Log.Information("\n" + Localizer.GetString("NotebookProcessingStartingPhase2"));
            int cmptPage = 0;
            int skippedCount = 0;

            foreach (Page page in allPages)
            {
                cmptPage++;
                var pageRelPath = page.Parent.Title + " / " + page.TitleWithPageLevelTabulation;

                // Check if we should skip this page in incremental mode
                if (AppSettings.IncrementalExport && diff != null)
                {
                    if (diff.UnchangedPages.Contains(page))
                    {
                        Log.Information($"- [SKIP] {Localizer.GetString("Page")} {cmptPage}/{allPages.Count} : {pageRelPath}");
                        skippedCount++;

                        // Add existing manifest entry to new manifest
                        if (existingManifest?.Pages != null && existingManifest.Pages.TryGetValue(page.OneNoteId, out var existingEntry))
                        {
                            newManifest.Pages[page.OneNoteId] = existingEntry;
                        }
                        continue;
                    }

                    var status = diff.NewPages.Contains(page) ? "[NEW]" : "[UPDATE]";
                    Log.Information($"- {status} {Localizer.GetString("Page")} {cmptPage}/{allPages.Count} : {pageRelPath}");
                }
                else
                {
                    Log.Information($"- {Localizer.GetString("Page")} {cmptPage}/{allPages.Count} : {pageRelPath}");
                }

                var success = ExportPage(page);

                if (!success)
                {
                    result.PagesOnError++;
                }
                else if (AppSettings.IncrementalExport && newManifest != null)
                {
                    // Add page to manifest after successful export
                    var exportPath = Path.GetRelativePath(notebook.ExportFolder, GetPageMdFilePath(page));
                    newManifest.Pages[page.OneNoteId] = ManifestService.CreateEntry(page, exportPath);
                }
            }

            // Handle deleted pages in incremental mode
            if (AppSettings.IncrementalExport && diff != null && AppSettings.CleanupDeletedPages)
            {
                foreach (var deletedPage in diff.DeletedPages)
                {
                    var deletedFilePath = Path.Combine(notebook.ExportFolder, deletedPage.ExportPath);
                    if (File.Exists(deletedFilePath))
                    {
                        try
                        {
                            File.Delete(deletedFilePath);
                            Log.Information($"- [DELETE] {deletedPage.Title} ({deletedPage.ExportPath})");

                            // Try to clean up empty parent directories
                            CleanupEmptyDirectories(Path.GetDirectoryName(deletedFilePath), notebook.ExportFolder);
                        }
                        catch (Exception ex)
                        {
                            Log.Warning($"Failed to delete file {deletedFilePath}: {ex.Message}");
                        }
                    }
                }
            }

            // Save manifest in incremental mode
            if (AppSettings.IncrementalExport && newManifest != null)
            {
                var manifestPath = GetManifestPath(notebook);
                ManifestService.SaveManifest(newManifest, manifestPath);
                Log.Information(String.Format(Localizer.GetString("ManifestSaved"), newManifest.Pages.Count));
            }

            // Log summary for incremental mode
            if (AppSettings.IncrementalExport && skippedCount > 0)
            {
                Log.Information(String.Format(Localizer.GetString("IncrementalSummary"), skippedCount, cmptPage - skippedCount));
            }

            return result;
        }

        /// <summary>
        /// Clean up empty directories up to the root export folder
        /// </summary>
        private static void CleanupEmptyDirectories(string directory, string stopAtDirectory)
        {
            if (string.IsNullOrEmpty(directory) || directory == stopAtDirectory)
                return;

            try
            {
                if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
                {
                    Directory.Delete(directory);
                    CleanupEmptyDirectories(Path.GetDirectoryName(directory), stopAtDirectory);
                }
            }
            catch
            {
                // Ignore errors when cleaning up directories
            }
        }

        protected override void WritePageMdFile(Page page, string pageMd)
        {
            File.WriteAllText(GetPageMdFilePath(page), pageMd);
        }

        protected override void FinalizeExportPageAttachments(Page page, Attachement attachment)
        {
            return; // No markdown file generated for attachments
        }

        protected override void PrepareFolders(Page page)
        {
            var pageDirectory = Path.GetDirectoryName(GetPageMdFilePath(page));

            if (!Directory.Exists(pageDirectory))
                Directory.CreateDirectory(pageDirectory);
        }

        protected override string FinalizePageMdPostProcessing(Page page, string md)
        {
            var res = md;

            if (AppSettings.AddFrontMatterHeader)
                res = AddFrontMatterHeader(page, md);

            return res;
        }

        protected override string GetPageWikilink(string linkText, string mdFilePath, string pageId)
        {
            var normalizedPath = mdFilePath.Replace('\\', '/');

            if (AppSettings.OneNoteLinksHandling == OneNoteLinksHandlingEnum.ConvertToWikilink)
            {
                // For Wikilinks, we use the format [[MdFilePath]] or [[MdFilePath|Display Text]]
                return normalizedPath == linkText ?
                    $"[[{normalizedPath}]]" :
                    $"[[{normalizedPath}|{linkText}]]";
            }
            else // ConvertToMarkdown
            {
                normalizedPath = normalizedPath.Replace(" ", "%20");
                return $"[{linkText}]({normalizedPath}.md)";
            }
        }

        private static string AddFrontMatterHeader(Page page, string pageMd)
        {
            var headerModel = new FrontMatterHeader
            {
                Title = page.Title,
                Created = page.CreationDate,
                Updated = page.LastModificationDate
            };

            var serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .WithTypeConverter(new DateTimeConverter(formats: AppSettings.FrontMatterDateFormat, kind: DateTimeKind.Local))
                .Build();
            var headerYaml = serializer.Serialize(headerModel);

            return "---\n" + headerYaml + "---\n\n" + pageMd;
        }

        private class FrontMatterHeader
        {
            public string Title { get; set; }
            public DateTime Updated { get; set; }
            public DateTime Created { get; set; }
        }
    }

}
