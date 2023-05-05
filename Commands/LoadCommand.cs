using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using MemorySnapshotAnalyzer.CommandProcessing;
using MemorySnapshotAnalyzer.UnityBackend;
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

            MemorySnapshot memorySnapshot = Load(Filename!);
            SetCurrentMemorySnapshot(memorySnapshot);
            Output.WriteLine("memory snapshot loaded successfully");
        }

        public static MemorySnapshot Load(string filename)
        {
            // TODO: support other file formats, either via auto-detection or an enum argument
            return new UnityMemorySnapshot(filename);
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
