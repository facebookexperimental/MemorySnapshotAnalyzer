// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using MemorySnapshotAnalyzer.CommandProcessing;
using Microsoft.Extensions.Configuration;
#if Windows
using System.Windows.Forms;
#endif

namespace MemorySnapshotAnalyzer.Commands
{
    public class LoadCommand : Command
    {
        public LoadCommand(Repl repl) : base(repl) {}

#pragma warning disable CS0649 // Field '...' is never assigned to, and will always have its default value null
#if Windows
        [PositionalArgument(0, optional: true)]
#else
        [PositionalArgument(0, optional: false)]
#endif
        public string? Filename;

        [FlagArgument("replace")]
        public bool ReplaceCurrentContext;
#pragma warning restore CS0649 // Field '...' is never assigned to, and will always have its default value null

        public override void Run()
        {
#if Windows
            if (Filename == null)
            {
                Filename = SelectFileViaDialog();
                if (Filename == null)
                {
                    return;
                }
            }
#endif

            MemorySnapshot? memorySnapshot = Repl.TryLoadMemorySnapshot(Filename!);
            if (memorySnapshot == null)
            {
                throw new CommandException("unable to detect memory snapshot file format");
            }

            Context context;
            if (Context.CurrentMemorySnapshot != null && !ReplaceCurrentContext)
            {
                context = Repl.SwitchToNewContext();
            }
            else
            {
                context = Context;
            }

            context.CurrentMemorySnapshot = memorySnapshot;
            Output.WriteLine($"{memorySnapshot.Format} memory snapshot loaded successfully");

            Repl.DumpContexts();
        }

#if Windows
        string? SelectFileViaDialog()
        {
            using (var openFileDialog = new OpenFileDialog())
            {
                openFileDialog.InitialDirectory = Repl.Configuration.GetValue<string>("SnapshotDirectory") ?? "";
                openFileDialog.Filter = "Unity Memory Snapshots (*.snap)|*.snap|All files (*.*)|*.*";
                openFileDialog.FilterIndex = 1;
                openFileDialog.RestoreDirectory = true;

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    return openFileDialog.FileName;
                }
            }
            return null;
        }
#endif

#if Windows
        public override string HelpText => "load [<filename>] ['replace]";
#else
        public override string HelpText => "load <filename> ['replace]";
#endif
    }
}
