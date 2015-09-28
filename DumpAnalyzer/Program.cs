using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using CommandLine;
using Redmine.Net.Api;
using Redmine.Net.Api.Types;
using System.Diagnostics;

namespace DumpAnalyzer
{
    internal class Program
    {
        private static string _user;
        private static string _password;
        private static RedmineManager _redmineManager;
        private static Project _project;
        private static List<IdentifiableName> _projectMembers;

        private static void Main(string[] args)
        {
            var sw = Stopwatch.StartNew();
            var options = new ProgramOptions();
            if (!Parser.Default.ParseArguments(args, options))
            {
                Environment.Exit(1);
            }
            if (!options.IsValid)
            {
                Console.Error.WriteLine("The options are invalid");
                Environment.Exit(1);
            }

            options.Normalize();

            Configuration configuration;
            if (!string.IsNullOrEmpty(options.ConfigFile))
            {
                // load config from file
                var xs = new XmlSerializer(typeof(Configuration));
                using (var fs = new FileStream(options.ConfigFile, FileMode.Open))
                {
                    configuration = (Configuration)xs.Deserialize(fs);
                }
            }
            else
            {
                configuration = Configuration.FromProgramOptions(options);
            }

            while (!GetCredentialsIfNeeded(configuration))
            {
                Console.WriteLine("The credentials you supplied were wrong...");
                Console.WriteLine();
            }

            if (!GetProjectDetailsIfNeeded(configuration))
            {
                Console.WriteLine("The project details you supplied were wrong...");
                Environment.Exit(1);
            }

            Process(configuration);

            Console.WriteLine("Time to process: {0}", sw.Elapsed);
        }

        private static bool GetProjectDetailsIfNeeded(Configuration configuration)
        {
            if (configuration.OpenTickets)
            {
                try
                {
                    _project = _redmineManager.GetObject<Project>(configuration.Project, null);
                    _projectMembers =
                        _redmineManager.GetTotalObjectList<ProjectMembership>(new NameValueCollection
                            {
                                {"project_id", _project.Identifier}
                            }).Select(p => p.User).ToList();
                    return _projectMembers != null && _projectMembers.Any();
                }
                catch (RedmineException)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool GetCredentialsIfNeeded(Configuration configuration)
        {
            if (configuration.OpenTickets)
            {
                // Get use name and password
                Console.Write("Redmine Username: ");
                _user = Console.ReadLine();

                Console.Write("Redmine password: ");
                _password = Console.ReadLine();

                _redmineManager = new RedmineManager(configuration.RedmineUrl, _user, _password);
                try
                {
                    _redmineManager.GetCurrentUser();
                    return true;
                }
                catch (RedmineException)
                {
                    return false;
                }
            }

            return true;
        }

        private static void Process(Configuration configuration)
        {
            string[] dumps;
            if (!string.IsNullOrEmpty(configuration.DumpFile))
            {
                dumps = new[] { configuration.DumpFile };
            }
            else
            {
                SearchOption searchOptions = configuration.RecursiveSearch
                                                 ? SearchOption.AllDirectories
                                                 : SearchOption.TopDirectoryOnly;
                dumps = Directory.GetFiles(configuration.DumpsFolder, "*.dmp", searchOptions);
            }

            List<DumpData> dumpsData = new List<DumpData>();

            int counter = 1;
            foreach (string d in dumps)
            {
                try
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine("Analyzing {0}/{1}: {2}", counter++, dumps.Length, d);
                    Console.ResetColor();
                    dumpsData.Add(Process(d, configuration));
                }
                catch (Exception e)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Error.WriteLine("Error while analyzing {0}: {1}", d, e);
                    Console.ResetColor();
                }
                if (!configuration.OpenTickets)
                {
                    Console.WriteLine("Press <Enter> to continue...");
                    Console.ReadLine();
                }
            }

            ReportStatistics(dumpsData);
        }

        private static void ReportStatistics(List<DumpData> dumpsData)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            ReportByModule(dumpsData);
            Console.ResetColor();
        }

        private static void ReportByModule(List<DumpData> dumpsData)
        {
            var moduleCount = from d in dumpsData
                              where d.FrameOfInterest != null
                              group d by d.FrameOfInterest.ModuleName into module
                              let count = module.Count()
                              orderby count descending
                              select new { Module = module.Key, Count = count };
                               
            using (var tp = new TablePrinter(50, "Module Name", "#Problems"))
            {
                foreach (var mc in moduleCount)
                {
                    tp.WriteRow(new object[] { mc.Module, mc.Count });
                }
                tp.WriteRow(new object[] { "--Unexpected--", dumpsData.Count(d => d.FrameOfInterest == null) });
            }
        }

        private static DumpData Process(string dump, Configuration configuration)
        {
            var res = Analyze(dump, configuration);
            Report(res);
            OpenTicketIfNeeded(dump, res, configuration);
            return res;
        }

        private static void OpenTicketIfNeeded(string dump, DumpData res,
                                               Configuration configuration)
        {
            if (configuration.OpenTickets)
            {
                OpenTicket(dump, res, configuration);
            }
        }

        private static void Report(DumpData res)
        {
            foreach (StackFrame stackFrame in res.CallStack)
            {
                if (stackFrame == res.FrameOfInterest)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                }
                Console.WriteLine(stackFrame);
                Console.ResetColor();
            }
        }

        private static void OpenTicket(string dump, DumpData res,
                                       Configuration configuration)
        {
            OwnershipData ownershipData = configuration.Owners.FirstOrDefault(o => o.Filter == res.FilterOfInterest);
            Owner assignee = configuration.DefaultOwner;
            if (ownershipData != null)
            {
                assignee = ownershipData.Owner;
            }

            var author = new IdentifiableName { Id = _redmineManager.GetCurrentUser().Id };
            IdentifiableName assignedTo =
                _projectMembers.SingleOrDefault(pm => pm != null && pm.Name == assignee.Name) ??
                _projectMembers.SingleOrDefault(pm => pm != null && pm.Name == configuration.DefaultOwner.Name);
            if (assignedTo == null)
            {
                // TODO: do something about this?
            }

            string subject = "Unexpected exception occurred";
            string description =
                string.Format("Please investigate a dump located at {0}.{1}{2}Here's the call stack for the last event:{3}{4}",
                    dump,
                    Environment.NewLine, Environment.NewLine, Environment.NewLine,
                    string.Join(Environment.NewLine, res.CallStack));

            if (res.FrameOfInterest != null)
            {
                subject = string.Format("A problem occurred in {0}.{1}: {2}",
                res.FrameOfInterest.ModuleName, res.FrameOfInterest.MethodName, res.LastEvent.Description);

                description = string.Format("There was a problem in {0}: {1}.{2}Please investigate a dump located at {3}.{4}{5}Here's the call stack for the last event:{6}{7}",
                    res.FrameOfInterest.ModuleName, res.LastEvent, Environment.NewLine,
                    dump,
                    Environment.NewLine, Environment.NewLine, Environment.NewLine,
                    string.Join(Environment.NewLine, res.CallStack));
            }

            var issue = new Issue
                {
                    Subject = subject.Substring(0, Math.Min(subject.Length, 255)),
                    Description = description,
                    AssignedTo = assignedTo,
                    Author = author,
                    Project = new IdentifiableName { Id = _project.Id },
                };

            _redmineManager.CreateObject(issue);
        }

        private static DumpData Analyze(string dump, Configuration configuration)
        {
            using (var da = new DumpAnalyzer(dump))
            {
                EventInformation lastEvent = da.GetLastEvent();
                IList<StackFrame> st = da.GetStackTrace(lastEvent.ThreadId);
                StackFrame frame = st.FirstOrDefault(f => configuration.Filters.Any(f.Match));
                Filter filter = frame == null ? null : configuration.Filters.First(frame.Match);
                return new DumpData { LastEvent = lastEvent, CallStack = st, FilterOfInterest = filter, FrameOfInterest = frame };
            }
        }
    }
}