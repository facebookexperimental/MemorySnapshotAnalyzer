// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using MemorySnapshotAnalyzer.CommandProcessing;
using System;
using System.Collections.Generic;

namespace MemorySnapshotAnalyzer.Commands
{
    public class DumpInvalidReferencesCommand : Command
    {
        public DumpInvalidReferencesCommand(Repl repl) : base(repl) {}

#pragma warning disable CS0649 // Field '...' is never assigned to, and will always have its default value null
        [FlagArgument("invalid")]
        public bool Invalid;

        [FlagArgument("nonheap")]
        public bool NonHeap;

        [FlagArgument("roots")]
        public bool Roots;

        [FlagArgument("objects")]
        public bool Objects;
#pragma warning restore CS0649 // Field '...' is never assigned to, and will always have its default value null

        public override void Run()
        {
            if (!Invalid && !NonHeap)
            {
                throw new CommandException("at least one of 'invalid or 'nonheap must be given");
            }

            if (Invalid)
            {
                Dump(CurrentTracedHeap.GetInvalidRoots(), CurrentTracedHeap.GetInvalidPointers(), "invalid");
            }

            if (NonHeap)
            {
                Dump(CurrentTracedHeap.GetNonHeapRoots(), CurrentTracedHeap.GetNonHeapPointers(), "non-heap");
            }
        }

        void Dump(IEnumerable<Tuple<int, NativeWord>> roots, IEnumerable<Tuple<NativeWord, NativeWord>> pointers, string kind)
        {
            var references = new HashSet<ulong>();
            int numberOfRoots = 0;
            foreach (Tuple<int, NativeWord> tuple in roots)
            {
                references.Add(tuple.Item2.Value);
                numberOfRoots++;
            }

            var objects = new HashSet<ulong>();
            foreach (Tuple<NativeWord, NativeWord> tuple in pointers)
            {
                references.Add(tuple.Item1.Value);
                objects.Add(tuple.Item2.Value);
            }

            Output.WriteLine("Found {0} {1} targets referenced from {2} roots and {3} separate objects",
                references.Count,
                kind,
                numberOfRoots,
                objects.Count);

            if (Roots)
            {
                foreach (Tuple<int, NativeWord> tuple in roots)
                {
                    Output.WriteLine("Root with index {0} contains {1} reference {2}",
                        tuple.Item1,
                        kind,
                        tuple.Item2);
                }
            }

            if (Objects)
            {
                foreach (Tuple<NativeWord, NativeWord> tuple in pointers)
                {
                    int objectIndex = CurrentTracedHeap.ObjectAddressToIndex(tuple.Item2);
                    int typeIndex = CurrentTracedHeap.ObjectTypeIndex(objectIndex);
                    Output.WriteLine("Object at {0} contains {1} reference {2} (type {3} with index {4})",
                        tuple.Item2,
                        kind,
                        tuple.Item1,
                        CurrentManagedHeap.TypeSystem.QualifiedName(typeIndex),
                        typeIndex);
                }
            }
        }

        public override string HelpText => "dumpinvalidrefs ['invalid|'nonheap]";
    }
}
