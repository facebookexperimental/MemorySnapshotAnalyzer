/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using MemorySnapshotAnalyzer.Analysis;
using System;
using System.Text;

namespace MemorySnapshotAnalyzer.CommandInfrastructure
{
    public abstract class Command
    {
        readonly Repl m_repl;
        readonly Context m_context;
        IStructuredOutput m_output;

        protected Command(Repl repl)
        {
            m_repl = repl;
            m_context = m_repl.CurrentContext;
            m_output = m_repl.StructuredOutput;
        }

        public Repl Repl => m_repl;

        public Context Context => m_context;

        public IStructuredOutput Output => m_output;

        public sealed class Unredirector : IDisposable
        {
            private readonly Command m_command;

            public Unredirector(Command command)
            {
                m_command = command;
            }

            public void Dispose()
            {
                m_command.m_output = m_command.m_repl.StructuredOutput;
            }
        }

        public Unredirector RedirectOutput(IStructuredOutput structuredOutput)
        {
            m_output = structuredOutput;
            return new(this);
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

        public TraceableHeap CurrentTraceableHeap
        {
            get
            {
                if (m_context.CurrentTraceableHeap == null)
                {
                    m_context.EnsureTraceableHeap();
                }

                return m_context.CurrentTraceableHeap!;
            }
        }

        public SegmentedHeap? CurrentSegmentedHeapOpt => CurrentTraceableHeap.SegmentedHeapOpt;

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

        public TracedHeap CurrentTracedHeap
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

        public HeapDomSizes MakeHeapDomSizes(CommandLineArgument? typeIndexOrPattern, bool includeDerived)
        {
            TypeSet? typeSet;
            if (typeIndexOrPattern != null)
            {
                typeSet = typeIndexOrPattern.ResolveTypeIndexOrPattern(Context, includeDerived);
            }
            else
            {
                typeSet = null;
            }

            return new HeapDomSizes(CurrentHeapDom, typeSet);
        }

        public void DescribeAddress(NativeWord addressOfValue, StringBuilder sb)
        {
            Output.AddProperty("address", addressOfValue.ToString());

            if (addressOfValue.Value == 0)
            {
                sb.AppendFormat("{0}", addressOfValue);
                return;
            }

            NativeWord nativeValue = default;
            SegmentedHeap? segmentedHeap = CurrentSegmentedHeapOpt;
            if (segmentedHeap != null)
            {
                MemoryView memoryView = segmentedHeap.GetMemoryViewForAddress(addressOfValue);
                if (!memoryView.IsValid)
                {
                    Output.AddProperty("addressMapped", false);
                    sb.AppendFormat("{0}: not in mapped memory", addressOfValue);
                    return;
                }

                nativeValue = memoryView.ReadNativeWord(0, CurrentMemorySnapshot.Native);
                Output.AddProperty("addressContents", nativeValue.ToString());
                sb.AppendFormat("{0}: {1}  ", addressOfValue, nativeValue);
            }

            // If we have traced the heap, check whether it's a live object or a pointer to a live object.
            if (m_context.CurrentTracedHeap != null)
            {
                // TODO: also support interior pointers, and if found, print field name if found
                int postorderIndex = CurrentTracedHeap.ObjectAddressToPostorderIndex(addressOfValue);
                if (postorderIndex != -1)
                {
                    DescribeObject(postorderIndex, addressOfValue, sb);
                    return;
                }

                postorderIndex = CurrentTracedHeap.ObjectAddressToPostorderIndex(nativeValue);
                if (postorderIndex != -1)
                {
                    Output.AddProperty("pointerTo", true);
                    sb.Append("pointer to ");
                    DescribeObject(postorderIndex, nativeValue, sb);
                    return;
                }
            }

            // Avoid clutter in the output for null pointers.
            if (nativeValue.Value == 0)
            {
                return;
            }

            string? typeDescription = CurrentTraceableHeap.DescribeAddress(addressOfValue, Output);
            if (typeDescription != null)
            {
                sb.AppendFormat("{0}", typeDescription);
                return;
            }

            if (segmentedHeap != null)
            {

                // If the address is not in mapped memory, then we already returned from this method, further above.
                HeapSegment segment = segmentedHeap.GetSegmentForAddress(nativeValue)!;
                sb.Append("pointer into ");
                segment.Describe(Output, sb);
            }
        }

        protected void DescribePointerInfo(PointerInfo<NativeWord> pointerInfo, StringBuilder sb)
        {
            if (pointerInfo.Value.Value != 0)
            {
                DescribeAddress(pointerInfo.Value, sb);
                if (pointerInfo.PointerFlags != default)
                {
                    PointerFlags baseFlags = pointerInfo.PointerFlags.WithoutWeight();
                    int weight = pointerInfo.PointerFlags.Weight();
                    Output.AddProperty("pointerFlags", baseFlags.ToString());
                    Output.AddProperty("referenceWeight", weight);
                    if (weight != 0)
                    {
                        sb.AppendFormat(" ({0}, weight {1})", baseFlags, weight);
                    }
                    else
                    {
                        sb.AppendFormat(" ({0})", baseFlags);
                    }
                }
            }
        }

        // Append a one-line description about the object to the given string builder.
        void DescribeObject(int postorderIndex, NativeWord objectAddress, StringBuilder sb)
        {
            int typeIndex = CurrentTraceableHeap.TryGetTypeIndex(objectAddress);
            if (typeIndex == -1)
            {
                CurrentTracedHeap.DescribeRootIndices(postorderIndex, sb, Output);
                return;
            }

            if (Context.CurrentBacktracer != null)
            {
                int nodeIndex = CurrentBacktracer.PostorderIndexToNodeIndex(postorderIndex);
                AppendWeight(CurrentBacktracer.Weight(nodeIndex), sb);
            }

            Output.AddProperty("objectIndex", postorderIndex);
            sb.AppendFormat("live object[index {0}", postorderIndex);

            string? name = CurrentTraceableHeap.GetObjectName(objectAddress);
            if (name != null)
            {
                Output.AddProperty("objectName", name);
                sb.AppendFormat(" \"{0}\"", name);
            }

            int objectSize = CurrentTraceableHeap.GetObjectSize(objectAddress, typeIndex, committedOnly: false);
            Output.AddProperty("objectSize", objectSize);
            sb.AppendFormat(", size {0}", objectSize);

            int committedSize = CurrentTraceableHeap.GetObjectSize(objectAddress, typeIndex, committedOnly: true);
            if (committedSize != objectSize)
            {
                Output.AddProperty("committedSize", committedSize);
                sb.AppendFormat(" (committed {0})", committedSize);
            }

            CurrentTraceableHeap.TypeSystem.OutputType(Output, "objectType", typeIndex);
            sb.AppendFormat(", type {0}:{1} (type index {2})]",
                CurrentTraceableHeap.TypeSystem.Assembly(typeIndex),
                CurrentTraceableHeap.TypeSystem.QualifiedName(typeIndex),
                typeIndex);

            if (typeIndex == CurrentTraceableHeap.TypeSystem.SystemStringTypeIndex)
            {
                SegmentedHeap? segmentedHeap = CurrentSegmentedHeapOpt;
                if (segmentedHeap != null)
                {
                    MemoryView objectView = segmentedHeap.GetMemoryViewForAddress(objectAddress);
                    if (objectView.IsValid)
                    {
                        (int stringLength, string s) = ReadString(objectView, maxLength: 80);
                        Output.AddProperty("stringLength", stringLength);
                        Output.AddProperty("stringValue", s);
                        sb.AppendFormat("  String of length {0} = \"{1}\"", stringLength, s);
                    }
                }
            }

            AppendTags(objectAddress, sb);
        }

        protected void AppendWeight(int weight, StringBuilder sb)
        {
            Output.AddProperty("referenceWeight", weight);
            if (weight == 1)
            {
                sb.Append("** ");
            }
            else if (weight > 1)
            {
                sb.AppendFormat("**({0}) ", weight);
            }
            else if (weight == -1)
            {
                sb.Append(".. ");
            }
            else if (weight < -1)
            {
                sb.AppendFormat("..({0}) ", weight);
            }
        }

        protected void AppendFields(int postorderIndex, NativeWord targetAddress, StringBuilder sb)
        {
            NativeWord address = CurrentTracedHeap.PostorderAddress(postorderIndex);
            int typeIndex = CurrentTracedHeap.PostorderTypeIndexOrSentinel(postorderIndex);
            bool first = true;
            foreach (PointerInfo<NativeWord> pointerInfo in CurrentTraceableHeap.GetPointers(address, typeIndex))
            {
                // Just compare the .Values, not the NativeAddresses themselves - for untraced pointers, the pointerInfo's address
                // can have a different size than the native word size (and an assertion would fire).
                if (pointerInfo.FieldNumber != -1 && pointerInfo.Value.Value == targetAddress.Value)
                {
                    if (!first)
                    {
                        sb.Append(", ");
                    }
                    else
                    {
                        Output.BeginArray("fields");
                        sb.Append(' ');
                        first = false;
                    }

                    string fieldName = CurrentTraceableHeap.TypeSystem.FieldName(pointerInfo.TypeIndex, pointerInfo.FieldNumber);
                    Output.BeginElement();
                    Output.AddProperty("fieldName", CurrentTraceableHeap.TypeSystem.FieldName(pointerInfo.TypeIndex, pointerInfo.FieldNumber));
                    sb.Append(fieldName);
                    Output.EndElement();
                }
            }

            if (!first)
            {
                Output.EndArray();
            }
        }

        protected void AppendTags(NativeWord address, StringBuilder sb)
        {
            bool first = true;
            foreach (string tag in CurrentTracedHeap.TagsForAddress(address))
            {
                if (first)
                {
                    Output.BeginArray("tags");

                    sb.Append(" tags(");
                    first = false;
                }
                else
                {
                    sb.Append(',');
                }

                Output.BeginElement();
                Output.AddProperty("tag", tag);
                sb.Append(tag);
                Output.EndElement();
            }

            if (!first)
            {
                sb.Append(')');

                Output.EndArray();
            }
        }

        protected void DumpObjectPointers(NativeWord address)
        {
            int typeIndex = CurrentTraceableHeap.TryGetTypeIndex(address);
            if (typeIndex < 0)
            {
                throw new CommandException($"unable to determine object type");
            }

            CurrentTraceableHeap.TypeSystem.OutputType(Output, "objectType", typeIndex);
            Output.AddDisplayStringLine("Object of type {0}:{1} (type index {2})",
                CurrentTraceableHeap.TypeSystem.Assembly(typeIndex),
                CurrentTraceableHeap.TypeSystem.QualifiedName(typeIndex),
                typeIndex);

            Output.BeginArray("objectPointers");
            var sb = new StringBuilder();
            foreach (PointerInfo<NativeWord> pointerInfo in CurrentTraceableHeap.GetPointers(address, typeIndex))
            {
                Output.BeginElement();

                DescribePointerInfo(pointerInfo, sb);
                if (sb.Length > 0)
                {
                    string s = sb.ToString();
                    Output.AddProperty("pointer", s);
                    Output.AddDisplayStringLineIndented(1, s);
                    sb.Clear();
                }

                Output.EndElement();
            }
            Output.EndArray();
        }

        protected void DumpObjectMemory(NativeWord address, MemoryView objectView, int indent, string? fieldNameOpt = null)
        {
            // TODO: if the address is not valid, later reads into the assumed object memory range can throw

            int typeIndex = CurrentTraceableHeap.TryGetTypeIndex(address);
            if (typeIndex < 0)
            {
                throw new CommandException($"unable to determine object type");
            }

            DumpObjectMemory(objectView, typeIndex, indent, fieldNameOpt);
        }

        protected void DumpObjectMemory(MemoryView objectView, int typeIndex, int indent, string? fieldNameOpt = null)
        {
            // TODO: if the address is not valid, later reads into the assumed object memory range can throw

            TypeSystem typeSystem = CurrentTraceableHeap.TypeSystem;

            if (typeIndex == typeSystem.SystemStringTypeIndex)
            {
                (int stringLength, string s) = ReadString(objectView, maxLength: int.MaxValue);
                Output.AddProperty("kind", "string");
                Output.AddProperty("stringLength", stringLength);
                Output.AddProperty("stringValue", s);
                Output.AddDisplayStringLineIndented(indent, "String of length {0} = \"{1}\"", stringLength, s);
            }
            else if (typeSystem.IsArray(typeIndex))
            {
                int elementTypeIndex = typeSystem.BaseOrElementTypeIndex(typeIndex);
                int arraySize = CurrentSegmentedHeapOpt!.ReadArraySize(objectView);
                Output.AddProperty("kind", "array");
                Output.AddProperty("arrayLength", arraySize);
                CurrentTraceableHeap.TypeSystem.OutputType(Output, "elementType", elementTypeIndex);
                Output.AddDisplayStringLineIndented(indent, "Array of length {0} with element type {1}:{2} (type index {3})",
                    arraySize,
                    typeSystem.Assembly(elementTypeIndex),
                    typeSystem.QualifiedName(elementTypeIndex),
                    elementTypeIndex);

                Output.BeginArray("elements");
                int elementSize = typeSystem.GetArrayElementSize(elementTypeIndex);
                for (int i = 0; i < arraySize; i++)
                {
                    int elementOffset = typeSystem.GetArrayElementOffset(elementTypeIndex, i);
                    // The backing store of arrays does not have to be fully committed.
                    if (elementOffset + elementSize > objectView.Size)
                    {
                        break;
                    }

                    Output.BeginElement();
                    Output.AddProperty("elementOffset", elementOffset);
                    MemoryView elementView = objectView.GetRange(elementOffset, elementSize);
                    Output.AddDisplayStringLineIndented(indent + 1, "Element {0} at offset {1}", i, elementOffset);
                    DumpFieldMemory(elementView, elementTypeIndex, indent + 2);
                    Output.EndElement();
                }
                Output.EndArray();
            }
            else
            {
                Output.AddProperty("kind", "object");
                CurrentTraceableHeap.TypeSystem.OutputType(Output, "objectType", typeIndex);
                Output.AddDisplayStringLineIndented(indent, "Object of type {0}:{1} (type index {2})",
                    typeSystem.Assembly(typeIndex),
                    typeSystem.QualifiedName(typeIndex),
                    typeIndex);

                Output.BeginArray("fields");
                int numberOfFields = typeSystem.NumberOfFields(typeIndex);
                for (int fieldNumber = 0; fieldNumber < numberOfFields; fieldNumber++)
                {
                    if (typeSystem.FieldIsStatic(typeIndex, fieldNumber))
                    {
                        continue;
                    }

                    string fieldName = typeSystem.FieldName(typeIndex, fieldNumber);
                    if (fieldNameOpt != null && !fieldName.Equals(fieldNameOpt, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    int fieldOffset = typeSystem.FieldOffset(typeIndex, fieldNumber, withHeader: true);
                    int fieldTypeIndex = typeSystem.FieldType(typeIndex, fieldNumber);
                    string kind = typeSystem.Kind(fieldTypeIndex);

                    Output.BeginElement();
                    Output.AddProperty("fieldOffset", fieldOffset);
                    Output.AddProperty("fieldName", fieldName);
                    Output.AddProperty("kind", kind);
                    CurrentTraceableHeap.TypeSystem.OutputType(Output, "fieldType", fieldTypeIndex);
                    Output.AddDisplayStringLineIndented(indent + 1, "+{0}  {1} : {2} {3}:{4} (type index {5})",
                        fieldOffset,
                        fieldName,
                        kind,
                        typeSystem.Assembly(fieldTypeIndex),
                        typeSystem.QualifiedName(fieldTypeIndex),
                        fieldTypeIndex);
                    DumpFieldMemory(objectView.GetRange(fieldOffset, objectView.Size - fieldOffset), fieldTypeIndex, indent + 2);
                    Output.EndElement();
                }
                Output.EndArray();

                int baseTypeIndex = typeSystem.BaseOrElementTypeIndex(typeIndex);
                if (baseTypeIndex >= 0)
                {
                    DumpObjectMemory(objectView, baseTypeIndex, indent + 1, fieldNameOpt);
                }
            }
        }

        (int stringLength, string s) ReadString(MemoryView objectView, int maxLength)
        {
            objectView.Read(CurrentTraceableHeap.TypeSystem.SystemStringLengthOffset, out int stringLength);

            var sb = new StringBuilder(stringLength);
            int length = maxLength < stringLength ? maxLength : stringLength;
            for (int i = 0; i < length; i++)
            {
                objectView.Read(CurrentTraceableHeap.TypeSystem.SystemStringFirstCharOffset + i * 2, out char c);
                sb.Append(c);
            }

            if (maxLength < stringLength)
            {
                sb.Append("...");
            }

            return (stringLength, sb.ToString());
        }

        protected void DumpFieldMemory(MemoryView objectView, int fieldTypeIndex, int indent)
        {
            TypeSystem typeSystem = CurrentTraceableHeap.TypeSystem;

            if (typeSystem.IsValueType(fieldTypeIndex) && fieldTypeIndex != CurrentTraceableHeap.TypeSystem.SystemVoidStarTypeIndex)
            {
                DumpValueTypeMemory(objectView, fieldTypeIndex, indent);
            }
            else
            {
                NativeWord reference = objectView.ReadPointer(0, CurrentMemorySnapshot.Native);
                var sb = new StringBuilder();
                DescribeAddress(reference, sb);
                Output.AddDisplayStringLineIndented(indent, sb.ToString());
            }
        }

        protected void DumpValueTypeMemory(MemoryView objectView, int typeIndex, int indent, string? fieldNameOpt = null)
        {
            TypeSystem typeSystem = CurrentTraceableHeap.TypeSystem;

            int numberOfFields = typeSystem.NumberOfFields(typeIndex);
            int numberOfFieldsDumped = 0;
            Output.BeginArray("fields");
            for (int fieldNumber = 0; fieldNumber < numberOfFields; fieldNumber++)
            {
                if (typeSystem.FieldIsStatic(typeIndex, fieldNumber))
                {
                    continue;
                }

                string fieldName = typeSystem.FieldName(typeIndex, fieldNumber);
                if (fieldNameOpt != null && !fieldName.Equals(fieldNameOpt, StringComparison.Ordinal))
                {
                    continue;
                }

                int fieldOffset = typeSystem.FieldOffset(typeIndex, fieldNumber, withHeader: false);
                int fieldTypeIndex = typeSystem.FieldType(typeIndex, fieldNumber);

                Output.BeginElement();

                // Avoid infinite recursion due to the way that primitive types (such as System.Int32) are defined.
                if (fieldTypeIndex != typeIndex)
                {
                    string kind = typeSystem.Kind(fieldTypeIndex);

                    Output.AddProperty("fieldOffset", fieldOffset);
                    Output.AddProperty("fieldName", fieldName);
                    Output.AddProperty("kind", kind);
                    CurrentTraceableHeap.TypeSystem.OutputType(Output, "fieldType", fieldTypeIndex);
                    Output.AddDisplayStringLineIndented(indent, "+{0}  {1} : {2} {3}:{4} (type index {5})",
                        fieldOffset,
                        fieldName,
                        kind,
                        typeSystem.Assembly(fieldTypeIndex),
                        typeSystem.QualifiedName(fieldTypeIndex),
                        fieldTypeIndex);
                    DumpFieldMemory(objectView.GetRange(fieldOffset, objectView.Size - fieldOffset), fieldTypeIndex, indent + 1);
                    numberOfFieldsDumped++;
                }
                else
                {
                    object? valueOpt = ReadValue(objectView, typeIndex);
                    if (valueOpt != null)
                    {
                        if (valueOpt is char c)
                        {
                            Output.AddProperty("charValue", (int)c);
                            Output.AddDisplayStringLineIndented(indent, "Value {0}  0x{0:X04}  '{1}'", (int)c, char.IsControl(c) ? '.' : c);
                        }
                        else if (valueOpt is byte b)
                        {
                            Output.AddProperty("byteValue", b);
                            Output.AddDisplayStringLineIndented(indent, "Value {0}  0x{0:X02}  '{1}'", b, char.IsControl((char)b) ? '.' : (char)b);
                        }
                        else
                        {
                            Output.AddProperty("value", valueOpt.ToString()!);
                            Output.AddDisplayStringLineIndented(indent, "Value {0}", valueOpt);
                        }
                        numberOfFieldsDumped++;
                    }
                }

                Output.EndElement();
            }
            Output.EndArray();

            if (numberOfFieldsDumped == 0)
            {
                Output.AddDisplayStringLineIndented(indent, "No fields that could be dumped");
            }
        }

        object? ReadValue(MemoryView objectView, int typeIndex)
        {
            string typeName = CurrentTraceableHeap.TypeSystem.QualifiedName(typeIndex);
            switch (typeName)
            {
                case "System.Boolean":
                    {
                        // Read as a byte, which is the representation for a boolean stored in a field.
                        objectView.Read(0, out byte value);
                        return value != 0;
                    }
                case "System.Byte":
                    {
                        objectView.Read(0, out byte value);
                        return value;
                    }
                case "System.SByte":
                    {
                        objectView.Read(0, out sbyte value);
                        return value;
                    }
                case "System.Char":
                    {
                        objectView.Read(0, out Char value);
                        return value;
                    }
                case "System.Int16":
                    {
                        objectView.Read(0, out Int16 value);
                        return value;
                    }
                case "System.UInt16":
                    {
                        objectView.Read(0, out UInt16 value);
                        return value;
                    }
                case "System.Int32":
                    {
                        objectView.Read(0, out Int32 value);
                        return value;
                    }
                case "System.UInt32":
                    {
                        objectView.Read(0, out UInt32 value);
                        return value;
                    }
                case "System.Int64":
                    {
                        objectView.Read(0, out Int64 value);
                        return value;
                    }
                case "System.UInt64":
                    {
                        objectView.Read(0, out UInt64 value);
                        return value;
                    }
                case "System.Single":
                    {
                        objectView.Read(0, out Single value);
                        return value;
                    }
                case "System.Double":
                    {
                        objectView.Read(0, out Double value);
                        return value;
                    }
                default:
                    return null;
            }
        }

        public abstract void Run();

        public abstract string HelpText { get; }
    }
}
