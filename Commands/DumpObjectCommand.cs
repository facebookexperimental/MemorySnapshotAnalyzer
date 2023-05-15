// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using MemorySnapshotAnalyzer.CommandProcessing;
using System.Collections.Generic;
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

            if (TypeIndex != -1 && !Memory)
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
            int numberOfLiveObjects = CurrentTracedHeap.NumberOfLiveObjects;
            Output.WriteLine("number of live objects = {0}", numberOfLiveObjects);
            var perTypeCounts = new SortedDictionary<string, int>();
            for (int objectIndex = 0; objectIndex < numberOfLiveObjects; objectIndex++)
            {
                int typeIndex = CurrentTracedHeap.ObjectTypeIndex(objectIndex);
                string typeName = CurrentTraceableHeap.TypeSystem.QualifiedName(typeIndex);
                if (perTypeCounts.TryGetValue(typeName, out int count))
                {
                    perTypeCounts[typeName] = count + 1;
                }
                else
                {
                    perTypeCounts[typeName] = 1;
                }
            }

            foreach (var kvp in perTypeCounts)
            {
                Output.WriteLine("{0} ({1})", kvp.Key, kvp.Value);
            }
        }

        void ListAllLiveObjects()
        {
            // TODO: also support listing objects of types derived from the given type index?

            int numberOfLiveObjects = CurrentTracedHeap.NumberOfLiveObjects;

            if (TypeIndex != -1)
            {
                int numberOfObjectsFound = 0;
                for (int objectIndex = 0; objectIndex < numberOfLiveObjects; objectIndex++)
                {
                    if (CurrentTracedHeap.ObjectTypeIndex(objectIndex) == TypeIndex)
                    {
                        numberOfObjectsFound++;
                    }
                }

                Output.WriteLine("found {0} object(s) of type {1}",
                    numberOfObjectsFound,
                    CurrentTraceableHeap.TypeSystem.QualifiedName(TypeIndex));
            }
            else
            {
                Output.WriteLine("found {0} object(s)", numberOfLiveObjects);
            }

            var sb = new StringBuilder();
            for (int objectIndex = 0; objectIndex < numberOfLiveObjects; objectIndex++)
            {
                if (TypeIndex != -1 && CurrentTracedHeap.ObjectTypeIndex(objectIndex) != TypeIndex)
                {
                    continue;
                }

                NativeWord address = CurrentTracedHeap.ObjectAddress(objectIndex);
                DescribeAddress(address, sb);
                Output.WriteLine(sb.ToString());
                sb.Clear();
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
            int objectIndex = -1;

            // Try to see what we're asked to operate on - an address or a live object index.
            MemoryView objectView = segmentedHeap.GetMemoryViewForAddress(AddressOrIndex);
            if (objectView.IsValid)
            {
                address = AddressOrIndex;
                if (Context.CurrentTracedHeap != null)
                {
                    objectIndex = CurrentTracedHeap.ObjectAddressToIndex(AddressOrIndex);
                }
            }
            else if (Context.CurrentTracedHeap != null && AddressOrIndex.Value < (ulong)CurrentTracedHeap.NumberOfLiveObjects)
            {
                objectIndex = (int)AddressOrIndex.Value;
                address = CurrentTracedHeap.ObjectAddress(objectIndex);
            }

            // Report what target we found, if any.
            if (address.Size == 0)
            {
                throw new CommandException($"address {AddressOrIndex} is not in mapped memory");
            }
            else if (objectIndex != -1)
            {
                Output.WriteLine("live object with index {0} at address {1}", objectIndex, address);
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
            int objectIndex = ResolveToObjectIndex(AddressOrIndex);
            NativeWord address = CurrentTracedHeap.ObjectAddress(objectIndex);
            Output.WriteLine("live object with index {0} at address {1}", objectIndex, address);
            DumpObjectInformation(address);
        }

        public override string HelpText => "dumpobj 'livestats | 'list ['type <type index>] | <object address or index> | 'memory <object address or index> ['type <type index>]";
    }
}
