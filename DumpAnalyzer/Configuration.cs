using System.Collections.Generic;
using System.Linq;

namespace DumpAnalyzer
{
    public class Configuration
    {
        public string DumpFile { get; set; }

        public string DumpsFolder { get; set; }

        public bool RecursiveSearch { get; set; }

        public List<Filter> Filters { get; set; }

        public List<Filter> Ignores { get; set; }

        public string RedmineUrl { get; set; }

        public bool OpenTickets { get; set; }

        public string Project { get; set; }

        public List<OwnershipData> Owners { get; set; }

        public Owner DefaultOwner { get; set; }

        public static Configuration FromProgramOptions(ProgramOptions options)
        {
            return new Configuration
                {
                    DumpFile = options.DumpFile,
                    DumpsFolder = options.DumpsFolder,
                    RecursiveSearch = options.RecursiveSearch,
                    Filters = options.Filters.Select(filter => new Filter {ModuleName = filter}).ToList()
                };
        }
    }
}