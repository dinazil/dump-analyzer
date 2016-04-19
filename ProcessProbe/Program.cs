using CommandLine;
using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProcessProbe
{
    class Program
    {
        static void Main(string[] args)
        {
            var options = new ProgramOptions();
            if (!Parser.Default.ParseArguments(args, options))
            {
                Console.Error.WriteLine(options.GetUsage());
                Environment.Exit(1);
            }

            var runtime = CreateRuntime(options.ProcessId);
            var stacks = GetManagedStacks(runtime);
            ProcessStacks(stacks, 0);
        }

        private static IEnumerable<ThreadAndStack> GetManagedStacks(ClrRuntime runtime)
        {
            var allStacks = from thread in runtime.Threads
                            let frames = from frame in thread.StackTrace
                                         where frame.Kind == ClrStackFrameType.ManagedMethod
                                         select String.Format("{0}!{1}", frame.ModuleName, frame.Method)
                            select new ThreadAndStack
                            {
                                ManagedThreadId = thread.ManagedThreadId,
                                Stack = frames.Reverse().ToList()
                            };

            return allStacks.ToList();
        }

        private static ClrRuntime CreateRuntime(int pid)
        {
            // Create the data target.  This tells us the versions of CLR loaded in the target process.
            DataTarget dataTarget = DataTarget.AttachToProcess(pid, 60000, AttachFlag.Passive);
            dataTarget.SymbolLocator.SymbolPath = Environment.GetEnvironmentVariable("_NT_SYMBOL_PATH");

            // Now check bitness of our program/target:
            bool isTarget64Bit = dataTarget.PointerSize == 8;
            if (Environment.Is64BitProcess != isTarget64Bit)
                throw new Exception(string.Format("Architecture mismatch:  Process is {0} but target is {1}", Environment.Is64BitProcess ? "64 bit" : "32 bit", isTarget64Bit ? "64 bit" : "32 bit"));

            // Note I just take the first version of CLR in the process.  You can loop over every loaded
            // CLR to handle the SxS case where both v2 and v4 are loaded in the process.
            ClrInfo version = dataTarget.ClrVersions[0];

            // Now that we have the DataTarget, the version of CLR, and the right dac, we create and return a
            // ClrRuntime instance.
            return version.CreateRuntime();
        }

        private static IEnumerable<ThreadAndStack> TrimOne(IEnumerable<ThreadAndStack> stacks)
        {
            return from stack in stacks
                   select new ThreadAndStack
                   {
                       ManagedThreadId = stack.ManagedThreadId,
                       Stack = stack.Stack.Skip(1)
                   };
        }

        private static void ProcessStacks(IEnumerable<ThreadAndStack> stacks, int depth = 0)
        {
            var grouping = from stack in stacks
                           where stack.Stack.Any()
                           group stack by stack.Stack.First() into g
                           orderby g.Count() descending
                           select g;
            if (grouping.Count() == 1)
            {
                var stackGroup = grouping.First();
                Console.WriteLine("{0}| {1} ({2} threads)", new String(' ', depth * 2), stackGroup.Key, stackGroup.Count());
                ProcessStacks(TrimOne(stackGroup), depth);
            }
            else
            {
                foreach (var stackGroup in grouping)
                {
                    Console.WriteLine("{0}+ {1}  ({2} threads)",
                        new String(' ', depth * 2), stackGroup.Key, stackGroup.Count());
                    ProcessStacks(TrimOne(stackGroup), depth + 1);
                }
            }
        }
    }
}