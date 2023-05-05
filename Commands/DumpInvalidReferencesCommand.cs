using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using MemorySnapshotAnalyzer.CommandProcessing;
using System;
using System.Collections.Generic;

namespace MemorySnapshotAnalyzer.Commands
{
    public class DumpInvalidReferencesCommand : Command
    {
        public DumpInvalidReferencesCommand(Repl repl) : base(repl) {}

        public override void Run()
        {
            var references = new HashSet<ulong>();
            foreach (NativeWord reference in CurrentTracedHeap.GetInvalidRoots())
            {
                references.Add(reference.Value);
            }
            var objects = new HashSet<ulong>();
            foreach (Tuple<NativeWord, NativeWord> tuple in CurrentTracedHeap.GetInvalidPointers())
            {
                references.Add(tuple.Item1.Value);
                objects.Add(tuple.Item2.Value);
            }
            Output.WriteLine("Found {0} invalid targets referenced from {1} separate objects",
                references.Count,
                objects.Count);

            int i = 0;
            foreach (Tuple<NativeWord, NativeWord> tuple in CurrentTracedHeap.GetInvalidPointers())
            {
                int objectIndex = CurrentTracedHeap.ObjectAddressToIndex(tuple.Item2);
                int typeIndex = CurrentTracedHeap.ObjectTypeIndex(objectIndex);
                Output.WriteLine("{0}: object at {1} contains invalid reference {2} (type {3} with index {4})",
                    i,
                    tuple.Item2,
                    tuple.Item1,
                    CurrentMemorySnapshot.TypeSystem.QualifiedName(typeIndex),
                    typeIndex);
                i++;
            }
        }

        public override string HelpText => "dumpinvalidrefs";
    }
}
