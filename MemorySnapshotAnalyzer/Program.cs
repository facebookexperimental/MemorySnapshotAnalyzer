/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using MemorySnapshotAnalyzer.CommandInfrastructure;
using MemorySnapshotAnalyzer.Commands;
using MemorySnapshotAnalyzer.UnityBackend;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;

static class Program
{
    class CommandLineArguments
    {
        internal bool Help;
        internal bool NonInteractive;
        internal string? LogOutputFilename;
        internal string? StartupSnapshotFilename;
        internal List<string> BatchFilenames = new();

        internal CommandLineArguments(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--help":
                        Help = true;
                        break;
                    case "--noninteractive":
                        NonInteractive = true;
                        break;
                    case "--log":
                        if (LogOutputFilename != null)
                        {
                            throw new CommandException("--log may be specified at most once");
                        }

                        if (i + 1 >= args.Length)
                        {
                            throw new CommandException("--log must be given an argument");
                        }

                        LogOutputFilename = args[++i];
                        break;
                    case "--load":
                        if (StartupSnapshotFilename != null)
                        {
                            throw new CommandException("--load may be specified at most once");
                        }

                        if (i + 1 >= args.Length)
                        {
                            throw new CommandException("--load must be given an argument");
                        }

                        StartupSnapshotFilename = args[++i];
                        break;
                    case "--run":
                        if (i + 1 >= args.Length)
                        {
                            throw new CommandException("--run must be given an argument");
                        }

                        BatchFilenames.Add(args[++i]);
                        break;
                    default:
                        throw new CommandException($"unknown command line option \"{args[i]}\"");
                }
            }
        }

        internal static void Usage()
        {
            Console.WriteLine("Usage: MemorySnapshotAnalyzer [--help] [--noninteractive] [--log <output file>] [--load <snapshot file>] [--run <batch file>]");
        }
    }

    [STAThread]
    public static int Main(string[] args)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .Build();

        int returnValue = 0;
        FileOutput? errorOutput = null;
        Repl.RunWithHandler(() =>
        {
            CommandLineArguments commandLineArguments = new(args);
            if (commandLineArguments.Help)
            {
                CommandLineArguments.Usage();
                returnValue = 2;
            }

            IOutput output;
            ILoggerFactory loggerFactory;
            if (commandLineArguments.LogOutputFilename != null)
            {
                errorOutput = new FileOutput(commandLineArguments.LogOutputFilename, useUnixNewlines: false);
                output = errorOutput;
                loggerFactory = new MemoryLoggerFactory();
            }
            else if (commandLineArguments.NonInteractive)
            {
                output = new FileOutput(Console.Out);
                loggerFactory = new MemoryLoggerFactory();
            }
            else
            {
                output = new ConsoleOutput();
                loggerFactory = new ConsoleLoggerFactory();
            }

            using Repl repl = new(configuration, output, loggerFactory, isInteractive: !commandLineArguments.NonInteractive);

            repl.AddMemorySnapshotLoader(new UnityMemorySnapshotLoader());

            repl.AddCommand(typeof(HelpCommand), "help");
            repl.AddCommand(typeof(ExitCommand), "exit");
            repl.AddCommand(typeof(ClearConsoleCommand), "cls");
            repl.AddCommand(typeof(ReferenceClassifierCommand), "referenceclassifier", "rc");
            repl.AddCommand(typeof(ContextCommand), "context");
            repl.AddCommand(typeof(OptionsCommand), "options");
            repl.AddCommand(typeof(LoadCommand), "load");
            repl.AddCommand(typeof(ListSegmentsCommand), "listsegs", "ls");
            repl.AddCommand(typeof(StatsCommand), "stats");
            repl.AddCommand(typeof(PrintCommand), "print", "p");
            repl.AddCommand(typeof(DescribeCommand), "describe");
            repl.AddCommand(typeof(DumpCommand), "dump", "d");
            repl.AddCommand(typeof(DumpSegmentCommand), "dumpseg", "ds");
            repl.AddCommand(typeof(DumpObjectCommand), "dumpobj", "do");
            repl.AddCommand(typeof(ListObjectsCommand), "listobjs", "lo");
            repl.AddCommand(typeof(DumpInvalidReferencesCommand), "dumpinvalidrefs");
            repl.AddCommand(typeof(DumpAssembliesCommand), "dumpassemblies", "da");
            repl.AddCommand(typeof(DumpTypeCommand), "dumptype", "dt");
            repl.AddCommand(typeof(FindCommand), "find", "f");
            repl.AddCommand(typeof(DumpRootsCommand), "dumproots");
            repl.AddCommand(typeof(BacktraceCommand), "backtrace", "bt");
            repl.AddCommand(typeof(HeapDomCommand), "heapdom");
            repl.AddCommand(typeof(HeapDomStatsCommand), "heapdomstats");

            if (commandLineArguments.StartupSnapshotFilename != null)
            {
                // Since our command parser doesn't currently doesn't support escapes in strings, we can just pass the filename through verbatim.
                repl.RunCommandNonInteractively($"load \"{commandLineArguments.StartupSnapshotFilename}\"");
            }

            foreach (string batchFilename in commandLineArguments.BatchFilenames)
            {
                repl.RunBatchFile(batchFilename);
            }

            if (!commandLineArguments.NonInteractive)
            {
                repl.RunInteractively();
            }
        }, ex =>
        {
            // We only see this exception when run non-interactively.
            if (errorOutput != null)
            {
                errorOutput.WriteLine($"fatal error: {ex.Message}");

                // If running with a log file, include detailed crash information.
                errorOutput.WriteLine(ex.ToString());
            }

            // When running non-interactively, provide error output that's clearly attributed to this tool.
            Console.Error.WriteLine($"ERROR: MemorySnapshotAnalyzer: {ex.Message}");

            returnValue = -1;
        });

        if (errorOutput != null)
        {
            errorOutput.Dispose();
        }

        return returnValue;
    }
}
