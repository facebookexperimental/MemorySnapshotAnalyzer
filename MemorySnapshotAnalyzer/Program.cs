/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using MemorySnapshotAnalyzer.CommandInfrastructure;
using MemorySnapshotAnalyzer.Commands;
using MemorySnapshotAnalyzer.UnityBackend;
using Microsoft.Extensions.Configuration;
using System;

static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .Build();

        using Repl repl = new(configuration);

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

        repl.Run();

        return 0;
    }
}
