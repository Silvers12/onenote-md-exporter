using System;

namespace alxnbl.OneNoteMdExporter.Models
{
    public class OneNoteLinkMetadata
    {
        // MdExporter internal Id
        public string NodeId { get; set; }

        // Original OneNote link Id
        public string OriginalId { get; set; }

        // OneNote API link Id
        public string ProgrammaticId { get; set; }
        public string MdFilePath { get; set; }
        public string Title { get; set; }
    }
} 