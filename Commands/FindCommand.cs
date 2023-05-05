using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using MemorySnapshotAnalyzer.CommandProcessing;

namespace MemorySnapshotAnalyzer.Commands
{
    public class FindCommand : Command
    {
        public FindCommand(Repl repl) : base(repl) {}

#pragma warning disable CS0649 // Field '...' is never assigned to, and will always have its default value
        [PositionalArgument(0, optional: false)]
        public NativeWord NativeWord;
#pragma warning restore CS0649 // Field '...' is never assigned to, and will always have its default value

        public override void Run()
        {
            ManagedHeap managedHeap = CurrentMemorySnapshot.ManagedHeap;
            Native native = CurrentMemorySnapshot.Native;

            long instancesFound = 0;
            for (int i = 0; i < managedHeap.NumberOfSegments; i++)
            {
                ManagedHeapSegment segment = managedHeap.GetSegment(i);
                MemoryView memoryView = segment.MemoryView;
                long limit = memoryView.Size - native.Size;
                for (long offset = 0; offset <= limit; offset += native.Size)
                {
                    NativeWord value = memoryView.ReadNativeWord(offset, native);
                    if (value == NativeWord)
                    {
                        Output.WriteLine("{0} : {1}", segment.StartAddress + offset, segment);
                        instancesFound++;
                    }
                }
            }

            Output.WriteLine();
            Output.WriteLine("total instances found = {0}", instancesFound);
        }

        public override string HelpText => "find <pointer-sized value>";
    }
}
