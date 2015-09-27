using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.Runtime.Interop;

namespace DumpAnalyzer
{
    internal class DumpAnalyzer : IDisposable
    {
        private readonly ClrRuntime _runtime;
        private readonly DataTarget _target;

        public DumpAnalyzer(string dumpPath)
        {
            _target = DataTarget.LoadCrashDump(dumpPath);
            _target.AppendSymbolPath(Environment.GetEnvironmentVariable("_NT_SYMBOL_PATH"));

            Console.WriteLine("Architecture: {0}", _target.Architecture);
            if (_target.Architecture == Architecture.Amd64 && !Environment.Is64BitProcess)
            {
                throw new ApplicationException("Architecture doesn't match. Run this with X64 version.");
            }

            if (_target.ClrVersions.Count > 0)
            {
                ClrInfo dacVersion = _target.ClrVersions[0];
                Console.WriteLine("CLR Version: {0}", dacVersion.Version);

                string dacLocation = dacVersion.TryDownloadDac();

                if (string.IsNullOrEmpty(dacLocation))
                {
                    throw new FileNotFoundException("DAC library could not be found");
                }

                _runtime = _target.CreateRuntime(dacLocation);
            }
        }

        public void Dispose()
        {
            _target.DebuggerInterface.EndSession(DEBUG_END.PASSIVE);
        }

        public void ThreadWalk(Action<ClrThread> action)
        {
            if (action != null)
            {
                foreach (ClrThread t in _runtime.Threads)
                {
                    action(t);
                }
            }
        }

        public EventInformation GetLastEvent()
        {
            var debugControl = (IDebugControl) _target.DebuggerInterface;

            DEBUG_EVENT eventType;
            uint processId, threadIndex;
            IntPtr extraInformation = IntPtr.Zero;
            const uint extraInformationSize = 0;
            uint extraInformationUsed;
            const int descriptionSize = 1024;
            var description = new StringBuilder(descriptionSize);
            uint descriptionUsed;

            int h = debugControl.GetLastEventInformation(out eventType, out processId, out threadIndex, extraInformation,
                                                         extraInformationSize, out extraInformationUsed, description,
                                                         descriptionSize, out descriptionUsed);

            if (h != 0)
            {
                throw new ApplicationException("Could not retrieve last event information: " + h);
            }

            var debugSystemObjects = (IDebugSystemObjects) _target.DebuggerInterface;
            const uint count = 1;
            var sysIds = new uint[count];
            if (0 != (h = debugSystemObjects.GetThreadIdsByIndex(threadIndex, count, null, sysIds)))
            {
                throw new ApplicationException("Could not convert thread index " + threadIndex + " to thread ID: " + h);
            }

            ClrException clrException = null;
            if (IsThreadManaged(sysIds[0]))
            {
                ClrThread thread = _runtime.Threads.First(t => t.OSThreadId == sysIds[0]);
                clrException = thread.CurrentException;
            }

            return new EventInformation
                {
                    Type = eventType,
                    Description = description.ToString(),
                    ProcessId = processId,
                    ThreadId = sysIds[0],
                    ClrException = clrException
                };
        }

        public IList<StackFrame> GetStackTrace(uint threadId)
        {
            IList<StackFrame> all = GetNativeStackTrace(threadId);
            if (!IsThreadManaged(threadId))
                return all;

            IList<StackFrame> managed = GetManagedStackTrace(threadId);

            int allIndex = 0, managedIndex = 0;

            for (; allIndex < all.Count; ++allIndex)
            {
                int temp = managedIndex;
                while (temp < managed.Count && managed[temp].InstructionPointer != all[allIndex].InstructionPointer)
                    ++temp;

                if (temp < managed.Count)
                {
                    all[allIndex] = managed[temp];
                    managedIndex = temp + 1;
                }
            }

            return all;
        }

        private IList<StackFrame> GetNativeStackTrace(uint threadId)
        {
            var debugControl = (IDebugControl) _target.DebuggerInterface;
            var symbols = (IDebugSymbols) _target.DebuggerInterface;
            var systemObjects = (IDebugSystemObjects) _target.DebuggerInterface;

            const int framesSize = 1024;
            uint framesFilled;
            var frames = new DEBUG_STACK_FRAME[framesSize];

            uint engineThreadId;
            systemObjects.GetThreadIdBySystemId(threadId, out engineThreadId);
            systemObjects.SetCurrentThreadId(engineThreadId);
            debugControl.GetStackTrace(0, 0, 0, frames, framesSize, out framesFilled);

            var stackTrace = new List<StackFrame>();

            foreach (DEBUG_STACK_FRAME f in frames.Take((int) framesFilled))
            {
                const int nameBufferSize = 1024;
                var nameBuffer = new StringBuilder(nameBufferSize);
                uint nameSize;
                ulong displacement;
                if (0 !=
                    symbols.GetNameByOffset(f.InstructionOffset, nameBuffer, nameBufferSize, out nameSize,
                                            out displacement))
                {
                    nameBuffer = new StringBuilder("N/A");
                }
                string[] parts = nameBuffer.ToString().Split('!');
                string methodName = parts[0];
                string moduleName = "N/A";
                if (parts.Length > 1)
                {
                    moduleName = parts[0];
                    methodName = parts[1];
                }
                stackTrace.Add(new StackFrame
                    {
                        Type = StackFrameType.Native,
                        InstructionPointer = f.InstructionOffset,
                        StackPointer = f.StackOffset,
                        MethodName = methodName,
                        ModuleName = moduleName,
                        MethodOffset = displacement
                    });
            }

            return stackTrace;
        }

        private IList<StackFrame> GetManagedStackTrace(uint threadId)
        {
            ClrThread thread = _runtime.Threads.First(t => t.OSThreadId == threadId);
            return thread.StackTrace.Select(sf => new StackFrame
                {
                    Type = StackFrameType.Managed,
                    InstructionPointer = sf.InstructionPointer,
                    StackPointer = sf.StackPointer,
                    MethodName = sf.Method == null ? "N/A" : sf.Method.Type.Name + "." + sf.Method.Name,
                    ModuleName =
                        sf.Method == null ? "N/A" : Path.GetFileNameWithoutExtension(sf.Method.Type.Module.AssemblyName),
                    MethodOffset = 0 //TODO: find out real method offset
                }).ToList();
        }

        private bool IsThreadManaged(uint threadId)
        {
            return _runtime != null && _runtime.Threads.Any(t => t.OSThreadId == threadId);
        }
    }
}