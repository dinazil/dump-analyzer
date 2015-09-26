using Microsoft.Diagnostics.Runtime.Interop;

namespace DumpAnalyzer
{
    internal class EventInformation
    {
        public DEBUG_EVENT Type { get; set; }
        public uint ProcessId { get; set; }
        public uint ThreadId { get; set; }
        public string Description { get; set; }

        public override string ToString()
        {
            return string.Format("{0} in thread {1}: {2}", Type, ThreadId, Description);
        }
    }
}