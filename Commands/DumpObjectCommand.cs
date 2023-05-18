// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using MemorySnapshotAnalyzer.CommandProcessing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MemorySnapshotAnalyzer.Commands
{
    public class DumpObjectCommand : Command
    {
        public DumpObjectCommand(Repl repl) : base(repl) { }

#pragma warning disable CS0649 // Field '...' is never assigned to, and will always have its default value
        [PositionalArgument(0, optional: true)]
        public NativeWord AddressOrIndex;

        [FlagArgument("livestats")]
        public bool Statistics;

        [FlagArgument("sortbysize")]
        public bool SortBySize;

        [FlagArgument("list")]
        public bool ListLive;

        [FlagArgument("memory")]
        public bool Memory;

        [NamedArgument("type")]
        public int TypeIndex = -1;
#pragma warning restore CS0649 // Field '...' is never assigned to, and will always have its default value

        public override void Run()
        {
            // Only one command mode can be selected
            int numberOfModes = 0;
            numberOfModes += Statistics ? 1 : 0;
            numberOfModes += ListLive ? 1 : 0;
            numberOfModes += Memory ? 1 : 0;
            if (numberOfModes > 1)
            {
                throw new CommandException("only one command mode can be selected");
            }

            if (TypeIndex != -1 && !(ListLive || Memory))
            {
                throw new CommandException("can only provide a type index if dumping object memory");
            }

            if (Statistics)
            {
                // Dump statistics about live objects
                if (AddressOrIndex.Size != 0)
                {
                    throw new CommandException("cannot provide an address or index when dumping statistics");
                }

                DumpStatistics();
                return;
            }

            if (ListLive)
            {
                // List all live objects, or a specific live object
                if (AddressOrIndex.Size != 0)
                {
                    throw new CommandException("cannot provide an address or index when listing live objects");
                }

                ListAllLiveObjects();
                return;
            }

            if (AddressOrIndex.Size == 0)
            {
                throw new CommandException("missing address or index");
            }

            if (Memory)
            {
                // Dump memory for a specific object
                DumpObjectMemory();
            }
            else
            {
                // Dump information for a specific object
                DumpObjectInformation();
            }
        }

        void DumpStatistics()
        {
            int numberOfPostorderNodes = CurrentTracedHeap.NumberOfPostorderNodes;
            long totalSize = 0;
            int numberOfLiveObjects = 0;
            var perTypeCounts = new Dictionary<int, Tuple<int, long>>();
            for (int postorderIndex = 0; postorderIndex < numberOfPostorderNodes; postorderIndex++)
            {
                int typeIndex = CurrentTracedHeap.PostorderTypeIndexOrSentinel(postorderIndex);
                if (typeIndex == -1)
                {
                    continue;
                }

                long size = CurrentTraceableHeap.GetObjectSize(CurrentTracedHeap.PostorderAddress(postorderIndex), typeIndex, committedOnly: true);
                totalSize += size;
                numberOfLiveObjects++;

                if (perTypeCounts.TryGetValue(typeIndex, out Tuple<int, long>? tuple))
                {
                    perTypeCounts[typeIndex] = Tuple.Create(tuple!.Item1 + 1, tuple.Item2 + size);
                }
                else
                {
                    perTypeCounts[typeIndex] = Tuple.Create(1, size);
                }
            }

            Output.WriteLine("number of live objects = {0}, total size = {1}",
                numberOfLiveObjects,
                totalSize);

            KeyValuePair<int, Tuple<int, long>>[] kvps = perTypeCounts.ToArray();
            if (SortBySize)
            {
                Array.Sort(kvps, (a, b) => b.Value.Item1.CompareTo(a.Value.Item2));
            }
            else
            {
                Array.Sort(kvps, (a, b) => b.Value.Item1.CompareTo(a.Value.Item1));
            }

            foreach (KeyValuePair<int, Tuple<int, long>> kvp in kvps)
            {
                Output.WriteLine("{0} object(s) of type {1} (type index {2}, total size {3})",
                    kvp.Value.Item1,
                    CurrentTraceableHeap.TypeSystem.QualifiedName(kvp.Key),
                    kvp.Key,
                    kvp.Value.Item2);
            }
        }

        void ListAllLiveObjects()
        {
            // TODO: also support listing objects of types derived from the given type index?

            int numberOfPostorderNodes = CurrentTracedHeap.NumberOfPostorderNodes;

            int numberOfObjectsFound = 0;
            for (int postorderIndex = 0; postorderIndex < numberOfPostorderNodes; postorderIndex++)
            {
                int typeIndex = CurrentTracedHeap.PostorderTypeIndexOrSentinel(postorderIndex);
                if (TypeIndex != -1 && typeIndex == TypeIndex || TypeIndex == -1 && typeIndex != -1)
                {
                    numberOfObjectsFound++;
                }
            }

            if (TypeIndex != -1)
            {
                Output.WriteLine("found {0} object(s) of type {1}",
                    numberOfObjectsFound,
                    CurrentTraceableHeap.TypeSystem.QualifiedName(TypeIndex));
            }
            else
            {
                Output.WriteLine("found {0} object(s)", numberOfObjectsFound);
            }

            var sb = new StringBuilder();
            for (int postorderIndex = 0; postorderIndex < numberOfPostorderNodes; postorderIndex++)
            {
                int typeIndex = CurrentTracedHeap.PostorderTypeIndexOrSentinel(postorderIndex);
                if (TypeIndex != -1 && typeIndex == TypeIndex || TypeIndex == -1 && typeIndex != -1)
                {
                    NativeWord address = CurrentTracedHeap.PostorderAddress(postorderIndex);
                    DescribeAddress(address, sb);
                    Output.WriteLine(sb.ToString());
                    sb.Clear();
                }
            }
        }

        void DumpObjectMemory()
        {
            SegmentedHeap? segmentedHeap = CurrentSegmentedHeapOpt;
            if (segmentedHeap == null)
            {
                throw new CommandException("memory contents for active heap not available");
            }

            NativeWord address = default;
            int postorderIndex = -1;

            // Try to see what we're asked to operate on - an address or a postorder index.
            MemoryView objectView = segmentedHeap.GetMemoryViewForAddress(AddressOrIndex);
            if (objectView.IsValid)
            {
                address = AddressOrIndex;
                if (Context.CurrentTracedHeap != null)
                {
                    postorderIndex = CurrentTracedHeap.ObjectAddressToPostorderIndex(AddressOrIndex);
                }
            }
            else if (Context.CurrentTracedHeap != null && AddressOrIndex.Value < (ulong)CurrentTracedHeap.NumberOfPostorderNodes)
            {
                postorderIndex = (int)AddressOrIndex.Value;
                address = CurrentTracedHeap.PostorderAddress(postorderIndex);
            }

            // Report what target we found, if any.
            if (address.Size == 0)
            {
                throw new CommandException($"address {AddressOrIndex} is not in mapped memory");
            }
            else if (postorderIndex != -1)
            {
                Output.WriteLine("live object with index {0} at address {1}", postorderIndex, address);
            }
            else
            {
                Output.WriteLine("address {0} is not a live object", address);
            }

            // If no type index is given, infer the type from memory contents.
            if (TypeIndex == -1)
            {
                DumpObjectMemory(address, objectView);
            }

            // If a type index is given, dump memory as if it was an object of that type.
            if (TypeIndex < 0 || TypeIndex >= CurrentTraceableHeap.TypeSystem.NumberOfTypeIndices)
            {
                throw new CommandException($"invalid type index {TypeIndex}");
            }

            if (CurrentTraceableHeap.TypeSystem.IsValueType(TypeIndex))
            {
                DumpValueTypeMemory(objectView, TypeIndex, 0);
            }
            else
            {
                DumpObjectMemory(objectView, TypeIndex, 0);
            }
        }

        void DumpObjectInformation()
        {
            int postorderIndex = ResolveToPostorderIndex(AddressOrIndex);
            NativeWord address = CurrentTracedHeap.PostorderAddress(postorderIndex);
            Output.WriteLine("live object with index {0} at address {1}", postorderIndex, address);
            DumpObjectInformation(address);
        }

        public override string HelpText => "dumpobj 'livestats ['sortbysize] | 'list ['type <type index>] | <object address or index> | 'memory <object address or index> ['type <type index>]";
    }
}
