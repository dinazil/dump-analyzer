using System.Collections.Generic;
using System.IO;
using System.Linq;
using CommandLine;
using CommandLine.Text;

namespace ProcessProbe
{
    public class ProgramOptions
    {
        [Option("pid", Required=true, HelpText = "Process ID for the process to probe.")]
        public int ProcessId { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            return HelpText.AutoBuild(this, current => HelpText.DefaultParsingErrorsHandler(this, current));
        }
    }
}