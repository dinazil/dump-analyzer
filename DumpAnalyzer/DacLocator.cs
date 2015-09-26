using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Diagnostics.Runtime;
using Microsoft.Win32.SafeHandles;

namespace DumpAnalyzer
{
    // taken from here: http://winterdom.com/2013/05/clrmd-fetching-dac-libraries-from-symbol-servers
    public class DacLocator : IDisposable
    {
        private readonly String _searchPath;
        private LibrarySafeHandle _dbghelpModule;
        private Process _ourProcess;

        private DacLocator(String searchPath)
        {
            _searchPath = searchPath;
            _ourProcess = Process.GetCurrentProcess();
            _dbghelpModule = LoadLibrary("dbghelp.dll");
            if (_dbghelpModule.IsInvalid)
            {
                throw new Win32Exception(String.Format("Could not load dbghelp.dll: {0}", Marshal.GetLastWin32Error()));
            }
            if (!SymInitialize(_ourProcess.Handle, searchPath, false))
            {
                throw new Win32Exception(String.Format("SymInitialize() failed: {0}", Marshal.GetLastWin32Error()));
            }
        }

        public void Dispose()
        {
            if (_ourProcess != null)
            {
                SymCleanup(_ourProcess.Handle);
                _ourProcess = null;
            }
            if (_dbghelpModule != null && !_dbghelpModule.IsClosed)
            {
                _dbghelpModule.Dispose();
                _dbghelpModule = null;
            }
        }

        [DllImport("dbghelp.dll", SetLastError = true)]
        private static extern bool SymInitialize(IntPtr hProcess, String symPath, bool fInvadeProcess);

        [DllImport("dbghelp.dll", SetLastError = true)]
        private static extern bool SymCleanup(IntPtr hProcess);

        [DllImport("dbghelp.dll", SetLastError = true)]
        private static extern bool SymFindFileInPath(IntPtr hProcess, String searchPath, String filename, uint id,
                                                     uint two, uint three, uint flags, StringBuilder filePath,
                                                     IntPtr callback, IntPtr context);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern LibrarySafeHandle LoadLibrary(String name);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hModule);

        public static DacLocator FromPublicSymbolServer(String localCache)
        {
            return new DacLocator(String.Format("SRV*{0}*http://msdl.microsoft.com/download/symbols", localCache));
        }

        public static DacLocator FromEnvironment()
        {
            string ntSymbolPath = Environment.GetEnvironmentVariable("_NT_SYMBOL_PATH");
            return new DacLocator(ntSymbolPath);
        }

        public static DacLocator FromSearchPath(String searchPath)
        {
            return new DacLocator(searchPath);
        }

        public String FindDac(ClrInfo clrInfo)
        {
            string dac = clrInfo.TryGetDacLocation();
            if (String.IsNullOrEmpty(dac))
            {
                dac = FindDac(clrInfo.DacInfo.FileName, clrInfo.DacInfo.TimeStamp, clrInfo.DacInfo.FileSize);
            }
            return dac;
        }

        public String FindDac(String dacname, uint timestamp, uint fileSize)
        {
            // attemp using the symbol server
            var symbolFile = new StringBuilder(2048);
            if (SymFindFileInPath(_ourProcess.Handle, _searchPath, dacname,
                                  timestamp, fileSize, 0, 0x02, symbolFile, IntPtr.Zero, IntPtr.Zero))
            {
                return symbolFile.ToString();
            }
            throw new Win32Exception(String.Format("SymFindFileInPath() failed: {0}", Marshal.GetLastWin32Error()));
        }

// ReSharper disable ClassNeverInstantiated.Local
        private class LibrarySafeHandle : SafeHandleZeroOrMinusOneIsInvalid
// ReSharper restore ClassNeverInstantiated.Local
        {
            protected LibrarySafeHandle()
                : base(true)
            {
            }

            protected override bool ReleaseHandle()
            {
                return FreeLibrary(handle);
            }
        }
    }
}