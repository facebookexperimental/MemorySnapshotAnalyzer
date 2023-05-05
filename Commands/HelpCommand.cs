using MemorySnapshotAnalyzer.CommandProcessing;

namespace MemorySnapshotAnalyzer.Commands
{
    public class HelpCommand : Command
    {
        public HelpCommand(Repl repl) : base(repl) {}

        public override void Run()
        {
            Repl.OutputHelpText();
        }

        public override string HelpText => "help";
    }
}
