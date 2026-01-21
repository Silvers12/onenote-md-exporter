using alxnbl.OneNoteMdExporter.Infrastructure;
using Microsoft.Office.Interop.OneNote;
using Microsoft.VisualBasic.FileIO;
using Serilog;
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace alxnbl.OneNoteMdExporter.Models
{
    /// <summary>
    /// TemporaryNotebook used to temporary store XML OneNote pages at temporary section for pre-processing such as:
    /// Sections unfold, Convert OneNote tags to #hash-tags, Keep checkboxes, etc.
    /// Reference - https://github.com/idvorkin/onom/blob/master/OneNoteObjectModelTests/TemporaryNoteBookHelper.cs
    /// </summary>
    public class TemporaryNotebook : Notebook
    {
        public static new string Title { get; } = "__TempExporterNotebook__";

        private static new string OneNoteId { get; set; }

        private static new string OneNotePath { get; set; }

        private static string SectionOneNoteId { get; set; }

        /// <summary>
        /// Create page clone at temporary notebook
        /// Reference - https://github.com/idvorkin/onom/blob/master/OneNoteObjectModel/OneNoteApplication.cs#L160
        /// </summary>
        /// <param name="xmlPageContent">Page to clone</param>
        /// <returns>Temporary OneNote ID of cloned page</returns>
        public static string ClonePage(XElement xmlPageContent)
        {
            Log.Debug($"Start cloning page {xmlPageContent.Attribute("ID")}");

            var oneNoteApp = OneNoteApp.Instance;

            if (OneNoteId == null)
            {
                OneNotePath = Path.GetFullPath(@$"{Localizer.GetString("ExportFolder")}\{Title}");

                // Create Temporary notebook if not exists and open at OneNote
                oneNoteApp.OpenHierarchy(OneNotePath, null, out string tempNotebookId, CreateFileType.cftNotebook);
                OneNoteId = tempNotebookId;

                // Create new Section at Temporary notebook if not exists and open at OneNote
                var sectionName = $"{DateTime.Now:yyyy-MM-dd HH-mm}.one";
                oneNoteApp.OpenHierarchy(sectionName, OneNoteId, out string tempSectionId, CreateFileType.cftSection);
                SectionOneNoteId = tempSectionId;

                Log.Debug(@$"Created temporary section: {OneNotePath}\{sectionName}");
            }

            // When cloning a page need to remove all object ID's as OneNote needs to write them out
            foreach (var xmlElement in xmlPageContent.Descendants())
            {
                xmlElement.Attribute("objectID")?.Remove();
            }

            // Create the new Page and write it to OneNote
            oneNoteApp.CreateNewPage(SectionOneNoteId, out string tempPageId);

            // Update the XML as it still points to the page to clone
            xmlPageContent.Attribute("ID").Value = tempPageId;

            // Handle problematic page titles (starting with special characters like ---- or other)
            // OneNote API throws 0x8004202B error when UpdatePageContent is called with such titles
            var nameAttribute = xmlPageContent.Attribute("name");
            var originalTitle = nameAttribute?.Value;
            var titleWasModified = false;

            if (!string.IsNullOrEmpty(originalTitle) && IsProblematicTitle(originalTitle))
            {
                Log.Debug($"Page title '{originalTitle}' contains problematic characters, using temporary title for cloning");
                nameAttribute.Value = "_TEMP_" + originalTitle.TrimStart('-', ' ');
                titleWasModified = true;
            }

            // Replace created temp page with our page content
            oneNoteApp.UpdatePageContent(xmlPageContent.ToString(SaveOptions.DisableFormatting));

            // Restore original title if it was modified
            if (titleWasModified && nameAttribute != null)
            {
                Log.Debug($"Restoring original page title '{originalTitle}'");
                nameAttribute.Value = originalTitle;
                oneNoteApp.UpdatePageContent(xmlPageContent.ToString(SaveOptions.DisableFormatting));
            }

            Log.Debug($"Page successfully cloned to the temp page {tempPageId}");

            return tempPageId;
        }

        /// <summary>
        /// Check if a page title might cause issues with OneNote API
        /// Titles starting with dashes (---) or certain special characters can cause 0x8004202B errors
        /// </summary>
        private static bool IsProblematicTitle(string title)
        {
            if (string.IsNullOrEmpty(title))
                return false;

            // Titles starting with dashes are problematic
            if (title.StartsWith("-"))
                return true;

            // Titles that are only special characters
            if (title.Trim().All(c => !char.IsLetterOrDigit(c)))
                return true;

            return false;
        }

        /// <summary>
        /// Close temporary notebook and move its folder to Recycle Bin
        /// </summary>
        public static void CleanUp()
        {
            if (OneNoteId != null)
            {
                OneNoteApp.Instance.CloseNotebook(OneNoteId, true);
                FileSystem.DeleteDirectory(OneNotePath, UIOption.OnlyErrorDialogs, RecycleOption.DeletePermanently);
                
                OneNoteId = null;
            }
        }
    }
}
