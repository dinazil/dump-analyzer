using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DumpAnalyzer
{
    class ConsoleForegroundFormatter : IDisposable
    {
        public ConsoleForegroundFormatter(ConsoleColor color)
        {
            Console.ForegroundColor = color;
        }

        public void Dispose()
        {
            Console.ResetColor();
        }
    }
}
