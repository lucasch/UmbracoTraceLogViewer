﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Hosting;
using Diplo.TraceLogViewer.Models;

namespace Diplo.TraceLogViewer.Services
{
    /// <summary>
    /// Used to parse trace log data from Umbraco trace log files
    /// </summary>
    /// <remarks>
    /// Should now work with old log format and new 7.3.0 format - thanks Seb :)
    /// </remarks>
    public class LogDataService
    {
        // Example: 2014-05-26 15:48:51,750 [5] INFO  Umbraco.Core.PluginManager - [Thread 1] Determining hash of code files on disk
        // Example: 2014-05-26 15:48:51,750 [5] INFO  Umbraco.Core.PluginManager - [Thread 1] Determining hash of code files on disk
        // Example: 2015-07-15 21:58:58,748 [P22252/D3/T67] INFO umbraco.BusinessLogic.Log - Log scrubbed. Removed all items older than 2015-07-14 21:58:58
        private const string CombinedLogEntryPattern = @"\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2},\d{3} (\[(?<PROCESS1>.+)\]|.+) (?<LEVEL>\w+) {1,2}(?<LOGGER>.+) - (\[(?<PROCESS2>.+)\]|.+) (?<MESSAGE>.+)";
        private static readonly Regex LogEntryRegex = new Regex(CombinedLogEntryPattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);

        // Example: T123/D21
        // Example: Thread 123
        // Example: P3996/T48/D4
        private const string ThreadProcessPattern = @"T(?<THREAD>\d+)|D(?<DOMAIN>\d+)|P(?<PROCESS>\d+)|Thread (?<THREADOLD>\d+)";
        private static readonly Regex ThreadProcessRegex = new Regex(ThreadProcessPattern, RegexOptions.IgnoreCase);

        /// <summary>
        /// Gets a collection of log file data items from a given log filename
        /// </summary>
        /// <param name="fileName">The filename of the log file</param>
        /// <returns>An enumerable collection of log file data</returns>
        public IEnumerable<LogDataItem> GetLogDataFromFile(string fileName)
        {
            string fullPath = HostingEnvironment.MapPath(Path.Combine(LogFileService.BaseLogPath, fileName));
            return GetLogData(fullPath);
        }

        /// <summary>
        /// Gets a collection of log file data items from a given filepath to a log file
        /// </summary>
        /// <param name="logFilePath">The full file path to the log file</param>
        /// <returns>An enumerable collection of log file data</returns>
        public IEnumerable<LogDataItem> GetLogData(string logFilePath)
        {
            var logItems = new List<LogDataItem>();

            if (File.Exists(logFilePath))
            {
                string log = File.ReadAllText(logFilePath);

                var allLines = log.Split('\n');

                foreach (var line in allLines)
                {
                    var match = LogEntryRegex.Match(line);

                    if (match.Success)
                    {
                        var date = DateTime.Parse(line.Substring(0, 19));

                        string threadProcess = match.Groups["PROCESS2"].Value;

                        if (String.IsNullOrEmpty(threadProcess))
                        {
                            threadProcess = match.Groups["PROCESS1"].Value;
                        }

                        string threadId = null;
                        string processId = null;
                        string domainId = null;

                        if (!String.IsNullOrEmpty(threadProcess))
                        {
                            var procMatches = ThreadProcessRegex.Matches(threadProcess);

                            foreach (Match procMatch in procMatches)
                            {
                                if (procMatch.Success)
                                {
                                    var grp = procMatch.Groups["THREAD"];
                                    if (grp.Success)
                                    {
                                        threadId = grp.Value;
                                    }

                                    grp = procMatch.Groups["PROCESS"];
                                    if (grp.Success)
                                    {
                                        processId = grp.Value;
                                    }

                                    grp = procMatch.Groups["DOMAIN"];
                                    if (grp.Success)
                                    {
                                        domainId = grp.Value;
                                    }

                                    if (threadId == null)
                                    {
                                        grp = procMatch.Groups["THREADOLD"];
                                        if (grp.Success)
                                        {
                                            threadId = grp.Value;
                                        }
                                    }
                                }
                            }
                        }

                        var logDataItem = new LogDataItem
                        {
                            Date = date,
                            Level = match.Groups["LEVEL"].Value,
                            Logger = match.Groups["LOGGER"].Value,
                            Message = match.Groups["MESSAGE"].Value,
                            DomainId = domainId,
                            ProcessId = processId,
                            ThreadId = threadId
                        };

                        logItems.Add(logDataItem);
                    }
                    else
                    {
                        if (logItems.Any())
                            logItems.Last().Message = logItems.Last().Message + "\n" + line;
                    }
                }
            }
            else
            {
                throw new FileNotFoundException("The requested trace log file '" + logFilePath + "' could not be found", logFilePath);
            }

            return logItems;
        }
    }
}
