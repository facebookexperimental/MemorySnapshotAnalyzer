// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using MemorySnapshotAnalyzer.Analysis;
using System.Text;

namespace MemorySnapshotAnalyzer.CommandProcessing
{
    public abstract class Command
    {
        readonly Repl m_repl;
        readonly Context m_context;
        IOutput m_output;

        protected Command(Repl repl)
        {
            m_repl = repl;
            m_context = m_repl.CurrentContext;
            m_output = m_repl.Output;
        }

        public Repl Repl => m_repl;

        public Context Context => m_context;

        public IOutput Output => m_output;

        public void RedirectOutputToFilename(IOutput output)
        {
            m_output = output;
        }

        public void UnredirectOutput()
        {
            m_output = m_repl.Output;
        }

        // TODO: convenience functions for tabular output

        public MemorySnapshot CurrentMemorySnapshot
        {
            get
            {
                MemorySnapshot? memorySnapshot = m_context.CurrentMemorySnapshot;
                if (memorySnapshot == null)
                {
                    throw new CommandException("no memory snapshot loaded");
                }
                return memorySnapshot;
            }
        }

        public void SetCurrentMemorySnapshot(MemorySnapshot memorySnapshot)
        {
            m_context.CurrentMemorySnapshot = memorySnapshot;
        }

        public SegmentedHeap CurrentSegmentedHeap => CurrentMemorySnapshot.SegmentedHeap;

        public IRootSet CurrentRootSet
        {
            get
            {
                if (m_context.CurrentRootSet == null)
                {
                    m_context.EnsureRootSet();
                }

                return m_context.CurrentRootSet!;
            }
        }

        public ITracedHeap CurrentTracedHeap
        {
            get
            {
                if (m_context.CurrentTracedHeap == null)
                {
                    m_context.EnsureTracedHeap();
                }

                return m_context.CurrentTracedHeap!;
            }
        }

        public IBacktracer CurrentBacktracer
        {
            get
            {
                if (m_context.CurrentBacktracer == null)
                {
                    m_context.EnsureBacktracer();
                }

                return m_context.CurrentBacktracer!;
            }
        }

        public HeapDom CurrentHeapDom
        {
            get
            {
                if (m_context.CurrentHeapDom == null)
                {
                    m_context.EnsureHeapDom();
                }

                return m_context.CurrentHeapDom!;
            }
        }

        public int ResolveObjectAddressOrIndex(NativeWord addressOrIndex)
        {
            int objectIndex = CurrentTracedHeap.ObjectAddressToIndex(addressOrIndex);
            if (objectIndex != -1)
            {
                return objectIndex;
            }
            else if (addressOrIndex.Value < (ulong)CurrentTracedHeap.NumberOfLiveObjects)
            {
                return (int)addressOrIndex.Value;
            }
            else if (!CurrentMemorySnapshot.SegmentedHeap.GetMemoryViewForAddress(addressOrIndex).IsValid)
            {
                throw new CommandException($"argument ${addressOrIndex} is neither an address in mapped memory, nor is it an object index");
            }
            else
            {
                throw new CommandException($"no live object at address {addressOrIndex}");
            }
        }

        public void DescribeAddress(NativeWord addressOfValue, StringBuilder sb)
        {
            MemoryView memoryView = CurrentMemorySnapshot.SegmentedHeap.GetMemoryViewForAddress(addressOfValue);
            if (!memoryView.IsValid)
            {
                sb.AppendFormat("{0}: not in mapped memory", addressOfValue);
                return;
            }

            NativeWord nativeValue = memoryView.ReadNativeWord(0, CurrentMemorySnapshot.Native);
            sb.AppendFormat("{0}: {1}", addressOfValue, nativeValue);
            NativeWord pointerValue = memoryView.ReadPointer(0, CurrentMemorySnapshot.Native);

            // If we have traced the heap, check whether it's a live object or a pointer to a live object.
            if (m_context.CurrentTracedHeap != null)
            {
                // TODO: also support interior pointers, and if found, print field name if found
                int objectIndex = CurrentTracedHeap.ObjectAddressToIndex(addressOfValue);
                if (objectIndex != -1)
                {
                    sb.Append("  start of ");
                    DescribeObject(objectIndex, addressOfValue, sb);
                    return;
                }

                objectIndex = CurrentTracedHeap.ObjectAddressToIndex(nativeValue);
                if (objectIndex != -1)
                {
                    sb.Append("  pointer to ");
                    DescribeObject(objectIndex, nativeValue, sb);
                    return;
                }
            }

            // Avoid clutter in the output for null pointers.
            if (nativeValue.Value == 0)
            {
                return;
            }

            string? typeDescription = CurrentSegmentedHeap.DescribeAddress(addressOfValue);
            if (typeDescription != null)
            {
                sb.AppendFormat("  {0}", typeDescription);
                return;
            }

            HeapSegment? segment = CurrentMemorySnapshot.SegmentedHeap.GetSegmentForAddress(nativeValue);
            if (segment == null)
            {
                sb.Append("  not a pointer to mapped memory");
            }
            else if (segment.IsRuntimeTypeInformation)
            {
                sb.AppendFormat("  pointer into rtti[segment @ {0:X016}]", segment.StartAddress);
            }
            else
            {
                sb.AppendFormat("  pointer into managed heap[segment @ {0:X016}]", segment.StartAddress);
            }
        }

        void DescribeObject(int objectIndex, NativeWord objectAddress, StringBuilder sb)
        {
            int typeIndex = CurrentSegmentedHeap.TryGetTypeIndex(objectAddress);
            int objectSize = CurrentSegmentedHeap.GetObjectSize(objectAddress, typeIndex, committedOnly: false);
            int committedSize = CurrentSegmentedHeap.GetObjectSize(objectAddress, typeIndex, committedOnly: true);
            sb.AppendFormat("live object[object index {0}, size {1}",
                objectIndex,
                objectSize);
            if (committedSize != objectSize)
            {
                sb.AppendFormat(" (committed {0})",
                    committedSize);
            }
            sb.AppendFormat(", {0}, type index {1}]",
                CurrentSegmentedHeap.TypeSystem.QualifiedName(typeIndex),
                typeIndex);
        }

        protected void DumpObject(NativeWord address, MemoryView objectView)
        {
            // TODO: if the address is not valid, later reads into the assumed object memory range can throw

            ITypeSystem typeSystem = CurrentSegmentedHeap.TypeSystem;

            int typeIndex = CurrentSegmentedHeap.TryGetTypeIndex(address);
            if (typeIndex < 0)
            {
                throw new CommandException($"unable to determine object type");
            }

            DumpObject(objectView, typeIndex, 0);
        }

        protected void DumpObject(MemoryView objectView, int typeIndex, int indent)
        {
            ITypeSystem typeSystem = CurrentSegmentedHeap.TypeSystem;

            if (typeIndex == typeSystem.SystemStringTypeIndex)
            {
                objectView.Read(typeSystem.SystemStringLengthOffset, out int stringLength);

                var sb = new StringBuilder(stringLength);
                for (int i = 0; i < stringLength; i++)
                {
                    objectView.Read(typeSystem.SystemStringFirstCharOffset + i * 2, out char c);
                    sb.Append(c);
                }

                Output.WriteLineIndented(indent, "String of length {0} = \"{1}\"",
                    stringLength,
                    sb.ToString());
            }
            else if (typeSystem.IsArray(typeIndex))
            {
                int elementTypeIndex = typeSystem.BaseOrElementTypeIndex(typeIndex);
                int arraySize = CurrentSegmentedHeap.ReadArraySize(objectView);
                Output.WriteLineIndented(indent, "Array of length {0} with element type {1}",
                    arraySize,
                    typeSystem.QualifiedName(elementTypeIndex));

                int elementSize = typeSystem.GetArrayElementSize(elementTypeIndex);
                for (int i = 0; i < arraySize; i++)
                {
                    int elementOffset = typeSystem.GetArrayElementOffset(elementTypeIndex, i);
                    // The backing store of arrays does not have to be fully committed.
                    if (elementOffset + elementSize > objectView.Size)
                    {
                        break;
                    }

                    MemoryView elementView = objectView.GetRange(elementOffset, elementSize);
                    Output.WriteLineIndented(indent + 1, "Element {0} at offset {1}", i, elementOffset);
                    DumpFieldValue(elementView, elementTypeIndex, indent + 2);
                }
            }
            else
            {
                Output.WriteLineIndented(indent, "Object of type {0} (type index {1})",
                    typeSystem.QualifiedName(typeIndex),
                    typeIndex);

                int numberOfFields = typeSystem.NumberOfFields(typeIndex);
                for (int fieldNumber = 0; fieldNumber < numberOfFields; fieldNumber++)
                {
                    if (!typeSystem.FieldIsStatic(typeIndex, fieldNumber))
                    {
                        int fieldOffset = typeSystem.FieldOffset(typeIndex, fieldNumber, withHeader: true);
                        int fieldTypeIndex = typeSystem.FieldType(typeIndex, fieldNumber);
                        Output.WriteLineIndented(indent + 1, "+{0}  {1} : {2} {3} (type index {4})",
                            fieldOffset,
                            typeSystem.FieldName(typeIndex, fieldNumber),
                            typeSystem.IsValueType(fieldTypeIndex) ? "value" : typeSystem.IsArray(fieldTypeIndex) ? "array" : "object",
                            typeSystem.QualifiedName(fieldTypeIndex),
                            fieldTypeIndex);
                        DumpFieldValue(objectView.GetRange(fieldOffset, objectView.Size - fieldOffset), fieldTypeIndex, indent + 2);
                    }
                }

                int baseTypeIndex = typeSystem.BaseOrElementTypeIndex(typeIndex);
                if (baseTypeIndex >= 0)
                {
                    DumpObject(objectView, baseTypeIndex, indent + 1);
                }
            }
        }

        protected void DumpFieldValue(MemoryView objectView, int fieldTypeIndex, int indent)
        {
            ITypeSystem typeSystem = CurrentSegmentedHeap.TypeSystem;

            if (typeSystem.IsValueType(fieldTypeIndex))
            {
                DumpValueType(objectView, fieldTypeIndex, indent);
            }
            else
            {
                NativeWord reference = objectView.ReadPointer(0, CurrentMemorySnapshot.Native);
                Output.WriteLineIndented(indent, "Pointer to {0}", reference);
            }
        }

        protected void DumpValueType(MemoryView objectView, int typeIndex, int indent)
        {
            ITypeSystem typeSystem = CurrentSegmentedHeap.TypeSystem;

            int numberOfFields = typeSystem.NumberOfFields(typeIndex);
            if (numberOfFields == 0)
            {
                Output.WriteLineIndented(indent, "No fields");
                return;
            }

            for (int fieldNumber = 0; fieldNumber < numberOfFields; fieldNumber++)
            {
                if (!typeSystem.FieldIsStatic(typeIndex, fieldNumber))
                {
                    int fieldOffset = typeSystem.FieldOffset(typeIndex, fieldNumber, withHeader: false);
                    int fieldTypeIndex = typeSystem.FieldType(typeIndex, fieldNumber);

                    // Avoid infinite recursion due to the way that primitive types (such as System.Int32) are defined.
                    if (fieldTypeIndex != typeIndex)
                    {
                        Output.WriteLineIndented(indent, "+{0}  {1} : {2} {3} (type index {4})",
                            fieldOffset,
                            typeSystem.FieldName(typeIndex, fieldNumber),
                            typeSystem.IsValueType(fieldTypeIndex) ? "value" : typeSystem.IsArray(fieldTypeIndex) ? "array" : "object",
                            typeSystem.QualifiedName(fieldTypeIndex),
                            fieldTypeIndex);
                        DumpFieldValue(objectView.GetRange(fieldOffset, objectView.Size - fieldOffset), fieldTypeIndex, indent + 1);
                    }
                }
            }
        }

        public abstract void Run();

        public abstract string HelpText { get; }
    }
}
