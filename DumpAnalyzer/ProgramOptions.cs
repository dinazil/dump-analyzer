using System.Collections.Generic;
using System.IO;
using System.Linq;
using CommandLine;
using CommandLine.Text;

namespace DumpAnalyzer
{
    public class ProgramOptions
    {
        [Option("config-file", HelpText = "Path of a configuration file.")]
        public string ConfigFile { get; set; }

        [Option("dump-file", HelpText = "Path of a dump file.", MutuallyExclusiveSet = "DumpSource")]
        public string DumpFile { get; set; }

        [Option("dump-folder", HelpText = "Path of folder containitn dump files.", MutuallyExclusiveSet = "DumpSource")]
        public string DumpsFolder { get; set; }

        [Option("recursive-search", DefaultValue = false,
            HelpText = "Specifies whether to recursively search the specified dumps folder")]
        public bool RecursiveSearch { get; set; }

        [Option("filters-file", HelpText = "Path of file containing the stack frame filter",
            MutuallyExclusiveSet = "Filters")]
        public string FilterFile { get; set; }

        [OptionList("filters", Separator = ',', HelpText = "List of stack frame filters, separated by a comma",
            MutuallyExclusiveSet = "Filters")]
        public IList<string> Filters { get; set; }

        public bool IsValid
        {
            get
            {
                // if using config file all the rest should not be supplied
                if (!string.IsNullOrEmpty(ConfigFile) &&
                    !(string.IsNullOrEmpty(DumpFile) && string.IsNullOrEmpty(DumpsFolder) &&
                      string.IsNullOrEmpty(FilterFile) && Filters == null))
                {
                    return false;
                }

                if (string.IsNullOrEmpty(ConfigFile))
                {
                    // must have either dump file or dump folder but not both
                    if (!(!string.IsNullOrEmpty(DumpFile) ^ !string.IsNullOrEmpty(DumpsFolder)))
                    {
                        return false;
                    }

                    // must have filters-file or filters but not both
                    if (!(!string.IsNullOrEmpty(FilterFile) ^ Filters != null))
                    {
                        return false;
                    }

                    // paths must exist
                    if (!string.IsNullOrEmpty(DumpFile))
                    {
                        if (!File.Exists(DumpFile))
                        {
                            return false;
                        }
                    }

                    if (!string.IsNullOrEmpty(DumpsFolder))
                    {
                        if (!Directory.Exists(DumpsFolder))
                        {
                            return false;
                        }
                    }

                    if (!string.IsNullOrEmpty(FilterFile))
                    {
                        if (!File.Exists(FilterFile))
                        {
                            return false;
                        }
                    }
                }
                else
                {
                    if (!string.IsNullOrEmpty(ConfigFile))
                    {
                        if (!File.Exists(ConfigFile))
                        {
                            return false;
                        }
                    }
                }
                return true;
            }
        }

        public void Normalize()
        {
            if (!string.IsNullOrEmpty(FilterFile))
            {
                // read file and insert lined into Filters list
                Filters = File.ReadAllLines(FilterFile).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
            }
        }

        [HelpOption]
        public string GetUsage()
        {
            return HelpText.AutoBuild(this, current => HelpText.DefaultParsingErrorsHandler(this, current));
        }
    }
}