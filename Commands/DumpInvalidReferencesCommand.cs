/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using MemorySnapshotAnalyzer.CommandInfrastructure;
using System.Collections.Generic;
using System.Text;

namespace MemorySnapshotAnalyzer.Commands
{
    public class DumpInvalidReferencesCommand : Command
    {
        public DumpInvalidReferencesCommand(Repl repl) : base(repl) {}

#pragma warning disable CS0649 // Field '...' is never assigned to, and will always have its default value null
        [FlagArgument("roots")]
        public bool Roots;

        [FlagArgument("objects")]
        public bool Objects;

        [FlagArgument("verbose")]
        public bool Verbose;
#pragma warning restore CS0649 // Field '...' is never assigned to, and will always have its default value null

        public override void Run()
        {
            IEnumerable<(int, NativeWord)> roots = CurrentTracedHeap.GetInvalidRoots();
            IEnumerable<(NativeWord, NativeWord)> pointers = CurrentTracedHeap.GetInvalidPointers();

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

            Output.WriteLine("Found {0} invalid targets referenced from {1} roots and {2} separate objects",
                references.Count,
                numberOfRoots,
                objects.Count);

            if (Roots)
            {
                foreach ((int rootIndex, NativeWord reference) in roots)
                {
                    Output.WriteLine("Root with index {0} ({1}) contains invalid reference {2}",
                        rootIndex,
                        CurrentRootSet.DescribeRoot(rootIndex, fullyQualified: true),
                        reference);
                }
            }

            if (Objects || !Roots)
            {
                // Dump information about the objects that contain fields with invalid pointers.
                SortedDictionary<int, List<(NativeWord reference, NativeWord objectAddress)>> invalidReferencesByType = new();
                foreach ((NativeWord reference, NativeWord objectAddress) in pointers)
                {
                    int postorderIndex = CurrentTracedHeap.ObjectAddressToPostorderIndex(objectAddress);
                    int typeIndex = CurrentTracedHeap.PostorderTypeIndexOrSentinel(postorderIndex);
                    if (!invalidReferencesByType.TryGetValue(typeIndex, out List<(NativeWord reference, NativeWord objectAddress)>? byType))
                    {
                        byType = new();
                        invalidReferencesByType.Add(typeIndex, byType);
                    }

                    byType.Add((reference, objectAddress));
                }

                StringBuilder sb = new();
                foreach ((int typeIndex, List<(NativeWord reference, NativeWord objectAddress)> byType) in invalidReferencesByType)
                {
                    for (int i = 0; i < byType.Count; i++)
                    {
                        (NativeWord reference, NativeWord objectAddress) = byType[i];
                        int postorderIndex = CurrentTracedHeap.ObjectAddressToPostorderIndex(objectAddress);
                        AppendFields(postorderIndex, reference, sb);
                        Output.WriteLine("Object {0} (index {1}) of type {2}:{3} (type index {4}) contains invalid reference {5}{6}",
                            objectAddress,
                            postorderIndex,
                            CurrentTraceableHeap.TypeSystem.Assembly(typeIndex),
                            CurrentTraceableHeap.TypeSystem.QualifiedName(typeIndex),
                            typeIndex,
                            reference,
                            sb.ToString());
                        sb.Clear();

                        if (!Verbose && byType.Count > 2)
                        {
                            Output.WriteLine("... and {0} more with this type", byType.Count - 1);
                            break;
                        }
                    }
                }
            }
        }

        public override string HelpText => "dumpinvalidrefs ['roots] ['objects ['verbose]]";
    }
}
