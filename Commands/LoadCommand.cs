/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using MemorySnapshotAnalyzer.CommandInfrastructure;
using Microsoft.Extensions.Configuration;
#if WINDOWS
using System.Windows.Forms;
#endif

namespace MemorySnapshotAnalyzer.Commands
{
    public class LoadCommand : Command
    {
        public LoadCommand(Repl repl) : base(repl) {}

#pragma warning disable CS0649 // Field '...' is never assigned to, and will always have its default value null
#if WINDOWS
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
#if WINDOWS
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

#if WINDOWS
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

#if WINDOWS
        public override string HelpText => "load [<filename>] ['replace]";
#else
        public override string HelpText => "load <filename> ['replace]";
#endif
    }
}
