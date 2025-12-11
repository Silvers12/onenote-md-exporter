using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace alxnbl.OneNoteMdExporter.Models
{
    public static class TagsDefMap
    {

        /// <summary>
        /// KEY : id of symbol of OneNote Tag Defs
        /// VALUE : 
        ///     string[0] => default symbol
        ///     string[1] => symbol if tag completed
        /// </summary>
        private static Dictionary<string, string[]> map = new()
        {
            { "39", ["0"] },
            { "70", ["1"] },
            { "51", ["2"] },
            { "33", ["3"] },
            { "3", ["«☐»", "«☑»"] }, // Tag replaced by - [ ] and - [x] md syntax
            { "94", ["«☐»", "«☑»"] }, // Tag replaced by - [ ] and - [x] md syntax
            { "95", ["«☐»", "«☑»"] }, // Tag replaced by - [ ] and - [x] md syntax
            { "28", ["«☐»", "«☑»"] }, // Tag replaced by - [ ] and - [x] md syntax
            { "71", ["«☐»", "«☑»"] }, // Tag replaced by - [ ] and - [x] md syntax
            { "8", ["«☐»", "«☑»"] }, // Tag replaced by - [ ] and - [x] md syntax
            { "12", ["«☐»", "«☑»"] }, // Tag replaced by - [ ] and - [x] md syntax
            { "13", ["★"] },
            { "15", ["?"] },
            { "0", ["=="] },
            { "136", ["🖍"] },
            { "118", ["📬"] },
            { "23", ["🏠"] },
            { "18", ["📞"] },
            { "125", ["🔗"] },
            { "21", ["💡"] },
            { "131", ["🔐"] },
            { "17", ["⚠"] },
            { "100", ["◼"] },
            { "101", ["◼"] },
            { "122", ["🎬"] },
            { "132", ["📖"] },
            { "121", ["🎵"] },
            { "106", ["✉"] },
            { "140", ["⚡"] },
            { "142", ["❤️"] },
            { "24", ["💬"] }
        };

        public static Dictionary<string, string[]> Map { get => map; }
    }
}
