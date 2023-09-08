/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using MemorySnapshotAnalyzer.CommandInfrastructure;
using MemorySnapshotAnalyzer.ReferenceClassifiers;
using System.Text;

namespace MemorySnapshotAnalyzer.Commands
{
    public class DumpObjectCommand : Command
    {
        public DumpObjectCommand(Repl repl) : base(repl) { }

#pragma warning disable CS0649 // Field '...' is never assigned to, and will always have its default value
        [PositionalArgument(0, optional: false)]
        public NativeWord AddressOrIndex;

        [NamedArgument("selector")]
        public string? Selector;

        [NamedArgument("field")]
        public string? Field;

        [FlagArgument("pointers")]
        public bool Pointers;

        [NamedArgument("astype")]
        public CommandLineArgument? TypeIndexOrPattern;
#pragma warning restore CS0649 // Field '...' is never assigned to, and will always have its default value

        public override void Run()
        {
            int numberOfModes = 0;
            numberOfModes += Selector != null ? 1 : 0;
            numberOfModes += Pointers ? 1 : 0;
            numberOfModes += TypeIndexOrPattern != null ? 1 : 0;
            if (numberOfModes > 1)
            {
                throw new CommandException("only one mode may be selected");
            }

            if (Pointers)
            {
                if (Field != null)
                {
                    throw new CommandException($"'field may not be given with 'astype");
                }

                DumpObjectPointers();
            }
            else
            {
                SegmentedHeap? segmentedHeap = CurrentSegmentedHeapOpt;
                if (segmentedHeap == null)
                {
                    throw new CommandException("memory contents for active heap not available");
                }

                if (TypeIndexOrPattern != null)
                {
                    // If a type index is given, dump memory as if it was an object of that type.
                    DumpObjectMemoryAsType(segmentedHeap);
                }
                else
                {
                    DumpObjectMemory(segmentedHeap);
                }
            }
        }

        void DumpObjectPointers()
        {
            int postorderIndex = Context.ResolveToPostorderIndex(AddressOrIndex);
            NativeWord address = CurrentTracedHeap.PostorderAddress(postorderIndex);

            StringBuilder sb = new();
            DescribeAddress(address, sb);
            Output.WriteLine(sb.ToString());

            DumpObjectPointers(address);
        }

        void DumpObjectMemoryAsType(SegmentedHeap segmentedHeap)
        {
            NativeWord address = default;

            // Try to see what we're asked to operate on - an address or a postorder index.
            // Note that we don't use Context.ResolveToPostorderIndex here, as we want to support
            // addresses that do not correspond to live objects.
            MemoryView objectView = segmentedHeap.GetMemoryViewForAddress(AddressOrIndex);
            if (objectView.IsValid)
            {
                address = AddressOrIndex;
            }
            else if (Context.CurrentTracedHeap != null && AddressOrIndex.Value < (ulong)CurrentTracedHeap.NumberOfPostorderNodes)
            {
                int postorderIndex = (int)AddressOrIndex.Value;
                address = CurrentTracedHeap.PostorderAddress(postorderIndex);
                objectView = segmentedHeap.GetMemoryViewForAddress(address);
            }
            else
            {
                throw new CommandException($"address {AddressOrIndex} is not in mapped memory");
            }

            TypeSet typeSet = TypeIndexOrPattern!.ResolveTypeIndexOrPattern(Context, includeDerived: false);
            if (typeSet.Count != 1)
            {
                throw new CommandException($"type pattern does not match exactly one type");
            }

            foreach (int typeIndex in typeSet.TypeIndices)
            {
                Output.WriteLine("dumping address {0} as type {1}:{2} (type index {3})",
                    address,
                    CurrentTraceableHeap.TypeSystem.Assembly(typeIndex),
                    CurrentTraceableHeap.TypeSystem.QualifiedName(typeIndex),
                    typeIndex);

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

        void DumpObjectMemory(SegmentedHeap segmentedHeap)
        {
            int postorderIndex = Context.ResolveToPostorderIndex(AddressOrIndex);
            NativeWord address = CurrentTracedHeap.PostorderAddress(postorderIndex);

            if (Selector != null)
            {
                int typeIndex = CurrentTracedHeap.PostorderTypeIndexOrSentinel(postorderIndex);
                if (typeIndex == -1)
                {
                    throw new CommandException($"index {postorderIndex} is not the index of an object");
                }

                StringBuilder sb = new();
                DescribeAddress(address, sb);
                Output.WriteLine(sb.ToString());

                string[] fields = Rule.ParseSelector(Selector);
                Selector selector = CurrentTraceableHeap.TypeSystem.BindSelector(Output.WriteLine, typeIndex, fields, expectDynamic: false, expectReferenceType: false);
                if (selector.StaticPrefix != null)
                {
                    foreach ((SegmentedHeap.ValueReference valueReference, NativeWord _) in segmentedHeap.InterpretSelector((_, message) => Output.WriteLine(message), address, "command line", selector))
                    {
                        if (valueReference.WithHeader)
                        {
                            DumpObjectMemory(valueReference.AddressOfContainingObject, segmentedHeap, 1);
                        }
                        else
                        {
                            sb.Clear();
                            DescribeAddress(valueReference.AddressOfContainingObject, sb);
                            Output.WriteLineIndented(1, sb.ToString());

                            DumpValueTypeMemory(valueReference.ValueView, valueReference.TypeIndex, 2, Field);
                        }
                    }
                }
            }
            else
            {
                DumpObjectMemory(address, segmentedHeap, 0);
            }
        }

        void DumpObjectMemory(NativeWord address, SegmentedHeap segmentedHeap, int indent)
        {
            StringBuilder sb = new();
            DescribeAddress(address, sb);
            Output.WriteLineIndented(indent, sb.ToString());

            MemoryView objectView = segmentedHeap.GetMemoryViewForAddress(address);
            DumpObjectMemory(address, objectView, indent, Field);
        }

        public override string HelpText => "dumpobj <object address or index> ((['selector <selector>] | 'astype <type index or pattern>) ['field <field name>] | 'pointers)";
    }
}
