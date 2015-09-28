using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DumpAnalyzer
{
    // based on http://stackoverflow.com/questions/856845/how-to-best-way-to-draw-table-in-console-app-c
    class TablePrinter : IDisposable
    {
        public TablePrinter(int tableWidth, params string[] titles)
        {
            TableWidth = tableWidth;
            WriteSeparator();
            WriteRow(titles);
            WriteSeparator();
        }

        public int TableWidth { get; set; }

        public void WriteSeparator()
        {
            Console.WriteLine(new string('-', TableWidth));
        }

        public void WriteRow(params object[] columns)
        {
            int width = (TableWidth - columns.Length) / columns.Length;
            string row = "|";

            foreach (var column in columns)
            {
                row += AlignCentre(column.ToString(), width) + "|";
            }

            Console.WriteLine(row);
        }

        private static string AlignCentre(string text, int width)
        {
            text = text.Length > width ? text.Substring(0, width - 3) + "..." : text;

            if (string.IsNullOrEmpty(text))
            {
                return new string(' ', width);
            }
            else
            {
                return text.PadRight(width - (width - text.Length) / 2).PadLeft(width);
            }
        }

        public void Dispose()
        {
            WriteSeparator();
        }
    }
}
