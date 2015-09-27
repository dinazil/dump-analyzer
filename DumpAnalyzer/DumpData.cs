using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DumpAnalyzer
{
    class DumpData
    {
        public EventInformation LastEvent { get; set; }
        public IList<StackFrame> CallStack { get; set; }
        public Filter FilterOfInterest { get; set; }
        public StackFrame FrameOfInterest { get; set; }
    }
}
