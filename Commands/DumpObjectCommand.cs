// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using MemorySnapshotAnalyzer.CommandInfrastructure;

namespace MemorySnapshotAnalyzer.Commands
{
    public class DumpObjectCommand : Command
    {
        public DumpObjectCommand(Repl repl) : base(repl) { }

#pragma warning disable CS0649 // Field '...' is never assigned to, and will always have its default value
        [PositionalArgument(0, optional: true)]
        public NativeWord AddressOrIndex;

        [FlagArgument("pointers")]
        public bool Pointers;

        [NamedArgument("field")]
        public string? Field;

        [NamedArgument("astype")]
        public CommandLineArgument? TypeIndexOrPattern;
#pragma warning restore CS0649 // Field '...' is never assigned to, and will always have its default value

        public override void Run()
        {
            if (AddressOrIndex.Size == 0)
            {
                throw new CommandException("missing address or index");
            }

            if (Pointers && (Field != null || TypeIndexOrPattern != null))
            {
                throw new CommandException("'field or 'astype may only be given if 'memory is given");
            }

            if (Pointers)
            {
                // Dump information for a specific object
                DumpObjectPointers();
            }
            else
            {
                // Dump memory for a specific object
                DumpObjectMemory();
            }
        }

        void DumpObjectPointers()
        {
            int postorderIndex = Context.ResolveToPostorderIndex(AddressOrIndex);
            NativeWord address = CurrentTracedHeap.PostorderAddress(postorderIndex);
            Output.WriteLine("live object with index {0} at address {1}", postorderIndex, address);
            DumpObjectPointers(address);
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
                objectView = segmentedHeap.GetMemoryViewForAddress(address);
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
            else if (Context.CurrentTracedHeap != null)
            {
                Output.WriteLine("address {0} is not a live object", address);
            }

            // If no type index is given, infer the type from memory contents.
            if (TypeIndexOrPattern == null)
            {
                DumpObjectMemory(address, objectView, Field);
                return;
            }

            // If a type index is given, dump memory as if it was an object of that type.
            TypeSet typeSet = TypeIndexOrPattern.ResolveTypeIndexOrPattern(Context, includeDerived: false);
            if (typeSet.Count != 1)
            {
                throw new CommandException($"type pattern does not match exactly one type");
            }

            foreach (int typeIndex in typeSet.TypeIndices)
            {
                if (CurrentTraceableHeap.TypeSystem.IsValueType(typeIndex))
                {
                    DumpValueTypeMemory(objectView, typeIndex, 0, Field);
                }
                else
                {
                    DumpObjectMemory(objectView, typeIndex, 0, Field);
                }
            }
        }

        public override string HelpText => "dumpobj <object address or index> (['astype <type index>] ['field <field name>] | 'pointers)";
    }
}
