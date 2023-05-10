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

        [NamedArgument("astype")]
        public int TypeIndex = -1;

        [FlagArgument("live")]
        public bool LiveOnly;

        [FlagArgument("stats")]
        public bool Statistics;
#pragma warning restore CS0649 // Field '...' is never assigned to, and will always have its default value

        public override void Run()
        {
            ITypeSystem typeSystem = CurrentManagedHeap.TypeSystem;

            if (AddressOrIndex.Size == 0 && !LiveOnly)
            {
                throw new CommandException("invalid arguments");
            }

            NativeWord address;
            if (LiveOnly)
            {
                if (AddressOrIndex.Size == 0)
                {
                    if (Statistics)
                    {
                        DumpStatistics();
                    }
                    else
                    {
                        ListAllLiveObjects();
                    }
                    return;
                }

                int objectIndex = ResolveObjectAddressOrIndex(AddressOrIndex);
                address = CurrentTracedHeap.ObjectAddress(objectIndex);
                Output.WriteLine("{0} is a live object (index {1})", address, objectIndex);
            }
            else
            {
                address = AddressOrIndex;
            }

            MemoryView objectView = CurrentManagedHeap.GetMemoryViewForAddress(address);
            if (!objectView.IsValid)
            {
                throw new CommandException($"address {address} is not in mapped memory");
            }

            if (TypeIndex == -1)
            {
                DumpObject(objectView);
            }
            else
            {
                if (TypeIndex < 0 || TypeIndex >= typeSystem.NumberOfTypeIndices)
                {
                    throw new CommandException($"invalid type index {TypeIndex}");
                }

                if (typeSystem.IsValueType(TypeIndex))
                {
                    DumpValueType(objectView, TypeIndex, 0);
                }
                else
                {
                    // Pretend it's an object of the explicitly-given type.
                    DumpObject(objectView, TypeIndex, 0);
                }
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
                string typeName = CurrentManagedHeap.TypeSystem.QualifiedName(typeIndex);
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
            // TODO: support listing by type index
            var sb = new StringBuilder();
            int numberOfLiveObjects = CurrentTracedHeap.NumberOfLiveObjects;
            for (int objectIndex = 0; objectIndex < numberOfLiveObjects; objectIndex++)
            {
                NativeWord address = CurrentTracedHeap.ObjectAddress(objectIndex);
                DescribeAddress(address, sb);
                Output.WriteLine(sb.ToString());
                sb.Clear();
            }
        }

        public override string HelpText => "dumpobj [<object address or index> ['astype <type index>]] ['live ['stats]]";
    }
}
