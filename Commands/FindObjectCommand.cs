using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using MemorySnapshotAnalyzer.CommandProcessing;
using System.Text;

namespace MemorySnapshotAnalyzer.Commands
{
    public class FindObjectCommand : Command
    {
        public FindObjectCommand(Repl repl) : base(repl) {}

#pragma warning disable CS0649 // Field '...' is never assigned to, and will always have its default value null
        [PositionalArgument(0, optional: false)]
        public int TypeIndex;
#pragma warning restore CS0649 // Field '...' is never assigned to, and will always have its default value null

        public override void Run()
        {
            int numberOfLiveObjects = CurrentTracedHeap.NumberOfLiveObjects;

            int numberOfObjectsFound = 0;
            for (int objectIndex = 0; objectIndex < numberOfLiveObjects; objectIndex++)
            {
                int typeIndex = CurrentTracedHeap.ObjectTypeIndex(objectIndex);
                if (typeIndex == TypeIndex)
                {
                    numberOfObjectsFound++;
                }
            }

            Output.WriteLine("found {0} object(s) of type {1}",
                numberOfObjectsFound,
                CurrentMemorySnapshot.TypeSystem.QualifiedName(TypeIndex));

            var sb = new StringBuilder();
            for (int objectIndex = 0; objectIndex < numberOfLiveObjects; objectIndex++)
            {
                int typeIndex = CurrentTracedHeap.ObjectTypeIndex(objectIndex);
                if (typeIndex == TypeIndex)
                {
                    NativeWord address = CurrentTracedHeap.ObjectAddress(objectIndex);
                    DescribeAddress(address, sb);
                    Output.WriteLine(sb.ToString());
                    sb.Clear();
                }
            }
        }

        public override string HelpText => "findobj <type index>";
    }
}
