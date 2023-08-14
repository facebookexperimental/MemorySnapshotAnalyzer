/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using MemorySnapshotAnalyzer.CommandInfrastructure;
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
        }

        void Dump(IEnumerable<(int, NativeWord)> roots, IEnumerable<(NativeWord, NativeWord)> pointers, string kind)
        {
            var references = new HashSet<ulong>();
            int numberOfRoots = 0;
            foreach ((int _, NativeWord reference) in roots)
            {
                references.Add(reference.Value);
                numberOfRoots++;
            }

            var objects = new HashSet<ulong>();
            foreach ((NativeWord reference, NativeWord objectAddress) in pointers)
            {
                references.Add(reference.Value);
                objects.Add(objectAddress.Value);
            }

            Output.WriteLine("Found {0} {1} targets referenced from {2} roots and {3} separate objects",
                references.Count,
                kind,
                numberOfRoots,
                objects.Count);

            if (Roots)
            {
                foreach ((int rootIndex, NativeWord reference) in roots)
                {
                    Output.WriteLine("Root with index {0} ({1}) contains {2} reference {3}",
                        rootIndex,
                        CurrentRootSet.DescribeRoot(rootIndex, fullyQualified: true),
                        kind,
                        reference);
                }
            }

            if (Objects)
            {
                // Dump information about the objects that contain fields with invalid pointers.
                foreach ((NativeWord reference, NativeWord objectAddress) in pointers)
                {
                    int postorderIndex = CurrentTracedHeap.ObjectAddressToPostorderIndex(objectAddress);
                    int typeIndex = CurrentTracedHeap.PostorderTypeIndexOrSentinel(postorderIndex);
                    Output.WriteLine("Object at {0} contains {1} reference {2} (type {3} with index {4})",
                        objectAddress,
                        kind,
                        reference,
                        CurrentTraceableHeap.TypeSystem.QualifiedName(typeIndex),
                        typeIndex);
                }
            }
        }

        public override string HelpText => "dumpinvalidrefs ['invalid|'nonheap] ['roots] ['objects]";
    }
}
