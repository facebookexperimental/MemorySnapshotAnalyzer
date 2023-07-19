// Copyright(c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace MemorySnapshotAnalyzer.AbstractMemorySnapshot
{
    public abstract class SegmentedHeap
    {
        readonly TraceableHeap m_traceableHeap;
        readonly TypeSystem m_typeSystem;
        readonly Native m_native;
        readonly HeapSegment[] m_segments;

        public SegmentedHeap(TraceableHeap traceableHeap, HeapSegment[] segments)
        {
            m_traceableHeap = traceableHeap;
            m_typeSystem = traceableHeap.TypeSystem;
            m_native = new Native(m_typeSystem.PointerSize);

            // Consistency check that the heap segments are ordered and non-overlapping
            for (int i = 1; i < segments.Length; i++)
            {
                if (segments[i].StartAddress < segments[i - 1].EndAddress)
                {
                    throw new InvalidSnapshotFormatException("unordered or overlapping heap segments");
                }
            }

            m_segments = segments;
        }

        // This method provides an implementation for TraceableHeap.GetPointers, for heaps whose memory we have access to.
        public IEnumerable<PointerInfo<NativeWord>> GetPointers(NativeWord address, int typeIndex)
        {
            MemoryView objectView = GetMemoryViewForAddress(address);
            if (m_typeSystem.IsArray(typeIndex))
            {
                int elementTypeIndex = m_typeSystem.BaseOrElementTypeIndex(typeIndex);
                int arraySize = ReadArraySize(objectView);
                for (int i = 0; i < arraySize; i++)
                {
                    foreach (PointerInfo<int> pointerInfo in m_typeSystem.GetArrayElementPointerOffsets(elementTypeIndex, m_typeSystem.GetArrayElementOffset(elementTypeIndex, i)))
                    {
                        // We can find arrays whose backing store has not been fully committed.
                        if (pointerInfo.Value + m_typeSystem.PointerSize > objectView.Size)
                        {
                            break;
                        }

                        yield return pointerInfo.WithValue(objectView.ReadPointer(pointerInfo.Value, m_native));
                    }
                }
            }
            else
            {
                foreach (PointerInfo<int> pointerInfo in m_typeSystem.GetPointerOffsets(typeIndex, m_typeSystem.ObjectHeaderSize(typeIndex)))
                {
                    yield return pointerInfo.WithValue(ReadValue(objectView, pointerInfo));
                }
            }
        }

        public NativeWord ReadValue(MemoryView objectView, PointerInfo<int> pointerInfo)
        {
            if ((pointerInfo.PointerFlags & PointerFlags.Untraced) != 0)
            {
                int typeIndex = m_typeSystem.FieldType(pointerInfo.TypeIndex, pointerInfo.FieldNumber);
                int fieldSize = m_typeSystem.BaseSize(typeIndex);
                switch (fieldSize)
                {
                    case 1:
                        {
                            objectView.Read<byte>(pointerInfo.Value, out byte value);
                            return new NativeWord(1, value);
                        }
                    case 2:
                        {
                            objectView.Read<ushort>(pointerInfo.Value, out ushort value);
                            return new NativeWord(2, value);
                        }
                    case 4:
                        {
                            objectView.Read<uint>(pointerInfo.Value, out uint value);
                            return new NativeWord(4, value);
                        }
                    case 8:
                    default:
                        {
                            objectView.Read<ulong>(pointerInfo.Value, out ulong value);
                            return new NativeWord(8, value);
                        }
                }
            }
            else
            {
                return objectView.ReadPointer(pointerInfo.Value, m_native);
            }
        }

        public IEnumerable<(NativeWord childObjectAddress, NativeWord parentObjectAddress)> GetOwningReferencesFromAnchor(NativeWord anchorObjectAddress, PointerInfo<NativeWord> pointerInfo)
        {
            foreach (Selector selector in m_typeSystem.GetConditionAnchorSelectors(pointerInfo.TypeIndex, pointerInfo.FieldNumber))
            {
                foreach ((NativeWord childObjectAddress, NativeWord parentObjectAddress) pair in GetOwningReferencesFromObject(anchorObjectAddress, selector, pathIndex: 0))
                {
                    yield return pair;
                }
            }
        }

        IEnumerable<(NativeWord childObjectAddress, NativeWord parentObjectAddress)> GetOwningReferencesFromObject(NativeWord referrer, Selector selector, int pathIndex)
        {
            (int typeIndex, int fieldNumber) = selector.StaticPrefix[pathIndex];
            if (fieldNumber == Int32.MaxValue)
            {
                MemoryView objectView = GetMemoryViewForAddress(referrer);
                if (!objectView.IsValid)
                {
                    yield break;
                }

                Debug.Assert(m_typeSystem.IsArray(typeIndex));

                int elementTypeIndex = m_typeSystem.BaseOrElementTypeIndex(typeIndex);
                int arraySize = ReadArraySize(objectView);
                int elementSize = m_typeSystem.GetArrayElementSize(elementTypeIndex);
                for (int i = 0; i < arraySize; i++)
                {
                    int elementOffset = m_typeSystem.GetArrayElementOffset(elementTypeIndex, i);

                    // Check for partially-committed array.
                    if (elementOffset + elementSize > objectView.Size)
                    {
                        break;
                    }

                    MemoryView fieldView = objectView.GetRange(elementOffset, elementSize);
                    foreach ((NativeWord, NativeWord) pair in GetOwningReferencesFromField(fieldView, referrer, selector, pathIndex + 1))
                    {
                        yield return pair;
                    }
                }
            }
            else if (m_typeSystem.FieldIsStatic(typeIndex, fieldNumber))
            {
                MemoryView fieldView = m_typeSystem.StaticFieldBytes(typeIndex, fieldNumber);
                foreach ((NativeWord, NativeWord) pair in GetOwningReferencesFromField(fieldView, referrer, selector, pathIndex + 1))
                {
                    yield return pair;
                }
            }
            else
            {
                MemoryView objectView = GetMemoryViewForAddress(referrer);
                if (!objectView.IsValid)
                {
                    yield break;
                }

                int fieldOffset = m_typeSystem.FieldOffset(typeIndex, fieldNumber, withHeader: true);
                MemoryView fieldView = objectView.GetRange(fieldOffset, objectView.Size - fieldOffset);
                foreach ((NativeWord, NativeWord) pair in GetOwningReferencesFromField(fieldView, referrer, selector, pathIndex + 1))
                {
                    yield return pair;
                }
            }
        }

        IEnumerable<(NativeWord childObjectAddress, NativeWord parentObjectAddress)> GetOwningReferencesFromField(MemoryView fieldView, NativeWord referrer, Selector selector, int pathIndex)
        {
            if (pathIndex == selector.StaticPrefix.Count)
            {
                if (selector.DynamicTail != null)
                {
                    NativeWord reference = fieldView.ReadPointer(0, m_native);
                    foreach ((NativeWord, NativeWord) pair in GetOwningReferencesFromObjectDynamic(reference, selector, pathIndex: 0))
                    {
                        yield return pair;
                    }
                }
                else
                {
                    NativeWord reference = fieldView.ReadPointer(0, m_native);
                    if (reference.Value != 0)
                    {
                        yield return (reference, referrer);
                    }
                }
            }
            else
            {
                (int typeIndex, int fieldNumber) = selector.StaticPrefix[pathIndex];

                if (m_typeSystem.IsValueType(typeIndex))
                {
                    int fieldOffset = m_typeSystem.FieldOffset(typeIndex, fieldNumber, withHeader: false);
                    MemoryView subfieldView = fieldView.GetRange(fieldOffset, fieldView.Size - fieldOffset);
                    foreach ((NativeWord, NativeWord) pair in GetOwningReferencesFromField(subfieldView, referrer, selector, pathIndex + 1))
                    {
                        yield return pair;
                    }
                }
                else
                {
                    NativeWord reference = fieldView.ReadPointer(0, m_native);
                    foreach ((NativeWord, NativeWord) pair in GetOwningReferencesFromObject(reference, selector, pathIndex))
                    {
                        yield return pair;
                    }
                }
            }
        }

        IEnumerable<(NativeWord childObjectAddress, NativeWord parentObjectAddress)> GetOwningReferencesFromObjectDynamic(NativeWord referrer, Selector selector, int pathIndex)
        {
            int typeIndex = m_traceableHeap.TryGetTypeIndex(referrer);
            if (typeIndex == -1)
            {
                yield break;
            }

            string fieldName = selector.DynamicTail![pathIndex];
            if (fieldName.Equals("[]", StringComparison.Ordinal))
            {
                MemoryView objectView = GetMemoryViewForAddress(referrer);
                if (!objectView.IsValid)
                {
                    yield break;
                }

                Debug.Assert(m_typeSystem.IsArray(typeIndex));

                int elementTypeIndex = m_typeSystem.BaseOrElementTypeIndex(typeIndex);
                int arraySize = ReadArraySize(objectView);
                int elementSize = m_typeSystem.GetArrayElementSize(elementTypeIndex);
                for (int i = 0; i < arraySize; i++)
                {
                    int elementOffset = m_typeSystem.GetArrayElementOffset(elementTypeIndex, i);

                    // Check for partially-committed array.
                    if (elementOffset + elementSize > objectView.Size)
                    {
                        break;
                    }

                    MemoryView fieldView = objectView.GetRange(elementOffset, elementSize);
                    foreach ((NativeWord, NativeWord) pair in GetOwningReferencesFromField(fieldView, referrer, selector, pathIndex + 1))
                    {
                        yield return pair;
                    }
                }

                yield break;
            }

            int fieldNumber = m_typeSystem.GetFieldNumber(typeIndex, fieldName);
            if (fieldNumber == -1)
            {
                yield break;
            }

            if (m_typeSystem.FieldIsStatic(typeIndex, fieldNumber))
            {
                MemoryView fieldView = m_typeSystem.StaticFieldBytes(typeIndex, fieldNumber);
                foreach ((NativeWord, NativeWord) pair in GetOwningReferencesFromFieldDynamic(fieldView, referrer, selector, pathIndex + 1))
                {
                    yield return pair;
                }
            }
            else
            {
                MemoryView objectView = GetMemoryViewForAddress(referrer);
                if (!objectView.IsValid)
                {
                    yield break;
                }

                int fieldOffset = m_typeSystem.FieldOffset(typeIndex, fieldNumber, withHeader: true);
                MemoryView fieldView = objectView.GetRange(fieldOffset, objectView.Size - fieldOffset);
                foreach ((NativeWord, NativeWord) pair in GetOwningReferencesFromFieldDynamic(fieldView, referrer, selector, pathIndex + 1))
                {
                    yield return pair;
                }
            }
        }

        IEnumerable<(NativeWord childObjectAddress, NativeWord parentObjectAddress)> GetOwningReferencesFromFieldDynamic(MemoryView fieldView, NativeWord referrer, Selector selector, int pathIndex)
        {
            if (pathIndex == selector.DynamicTail!.Length)
            {
                NativeWord reference = fieldView.ReadPointer(0, m_native);
                if (reference.Value != 0)
                {
                    yield return (reference, referrer);
                }
            }
            else
            {
                int typeIndex = m_traceableHeap.TryGetTypeIndex(referrer);
                if (typeIndex == -1)
                {
                    yield break;
                }

                string fieldName = selector.DynamicTail![pathIndex];
                if (fieldName.Equals("[]", StringComparison.Ordinal))
                {
                    yield break;
                }

                int fieldNumber = m_typeSystem.GetFieldNumber(typeIndex, fieldName);
                if (fieldNumber == -1)
                {
                    yield break;
                }

                if (m_typeSystem.IsValueType(typeIndex))
                {
                    int fieldOffset = m_typeSystem.FieldOffset(typeIndex, fieldNumber, withHeader: false);
                    MemoryView subfieldView = fieldView.GetRange(fieldOffset, fieldView.Size - fieldOffset);
                    foreach ((NativeWord, NativeWord) pair in GetOwningReferencesFromFieldDynamic(subfieldView, referrer, selector, pathIndex + 1))
                    {
                        yield return pair;
                    }
                }
                else
                {
                    NativeWord reference = fieldView.ReadPointer(0, m_native);
                    foreach ((NativeWord, NativeWord) pair in GetOwningReferencesFromObjectDynamic(reference, selector, pathIndex))
                    {
                        yield return pair;
                    }
                }
            }
        }

        public int NumberOfSegments { get { return m_segments.Length; } }

        public HeapSegment GetSegment(int index)
        {
            return m_segments[index];
        }

        public HeapSegment? GetSegmentForAddress(NativeWord address)
        {
            int min = 0;
            int max = m_segments.Length;
            while (min < max)
            {
                int mid = (min + max) / 2;
                if (m_segments[mid].StartAddress <= address && address < m_segments[mid].EndAddress)
                {
                    return m_segments[mid];
                }
                else if (m_segments[mid].StartAddress < address)
                {
                    min = mid + 1;
                }
                else
                {
                    max = mid;
                }
            }

            return null;
        }

        public MemoryView GetMemoryViewForAddress(NativeWord address)
        {
            HeapSegment? segment = GetSegmentForAddress(address);
            if (segment == null)
            {
                return default;
            }

            long offset = (long)(address - segment.StartAddress).Value;
            return segment.MemoryView.GetRange(offset, segment.Size - offset);
        }

        public abstract int ReadArraySize(MemoryView objectView);
    }
}
