// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.CommandProcessing;
using MemorySnapshotAnalyzer.Commands;
using MemorySnapshotAnalyzer.UnityBackend;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;

static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        using IHost host = Host.CreateDefaultBuilder(args).Build();
        IConfiguration configuration = host.Services.GetRequiredService<IConfiguration>();

        using Repl repl = new(configuration);

        repl.AddMemorySnapshotLoader(new UnityMemorySnapshotLoader());

        repl.AddCommand(typeof(HelpCommand), "help");
        repl.AddCommand(typeof(ExitCommand), "exit");
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
