// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using MemorySnapshotAnalyzer.CommandProcessing;
using Microsoft.Extensions.Configuration;
using System.Windows.Forms;

namespace MemorySnapshotAnalyzer.Commands
{
    public class LoadCommand : Command
    {
        public LoadCommand(Repl repl) : base(repl) {}

#pragma warning disable CS0649 // Field '...' is never assigned to, and will always have its default value null
        [PositionalArgument(0, optional: true)]
        public string? Filename;
#pragma warning restore CS0649 // Field '...' is never assigned to, and will always have its default value null

        public override void Run()
        {
            if (Filename == null)
            {
                Filename = SelectFileViaDialog();
                if (Filename == null)
                {
                    return;
                }
            }

            MemorySnapshot? memorySnapshot = Repl.TryLoad(Filename!);
            if (memorySnapshot == null)
            {
                throw new CommandException("unable to detect memory snapshot file format");
            }

            SetCurrentMemorySnapshot(memorySnapshot);
            Output.WriteLine($"{memorySnapshot.Format} memory snapshot loaded successfully");
        }

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

        public override string HelpText => "load <filename>";
    }
}
