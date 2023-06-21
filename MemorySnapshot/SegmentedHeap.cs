// Copyright(c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace MemorySnapshotAnalyzer.AbstractMemorySnapshot
{
    public abstract class SegmentedHeap
    {
        readonly TypeSystem m_typeSystem;
        readonly Native m_native;
        readonly HeapSegment[] m_segments;

        public SegmentedHeap(TypeSystem typeSystem, HeapSegment[] segments)
        {
            m_typeSystem = typeSystem;
            m_native = new Native(typeSystem.PointerSize);

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

        // This method provides an implementation for TraceableHeap.GetIntraHeapPointers, for heaps whose memory we have access to.
        public IEnumerable<PointerInfo<NativeWord>> GetIntraHeapPointers(NativeWord address, int typeIndex)
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
                    yield return pointerInfo.WithValue(objectView.ReadPointer(pointerInfo.Value, m_native));
                }
            }
        }

        public IEnumerable<(NativeWord childObjectAddress, NativeWord parentObjectAddress)> GetOwningReferencesFromAnchor(NativeWord anchorObjectAddress, PointerInfo<NativeWord> pointerInfo)
        {
            List<(int typeIndex, int fieldNumber)[]> fieldPaths = m_typeSystem.GetConditionalAnchorFieldPaths(pointerInfo.TypeIndex, pointerInfo.FieldNumber);
            foreach ((int typeIndex, int fieldNumber)[] fieldPath in fieldPaths)
            {
                foreach ((NativeWord childObjectAddress, NativeWord parentObjectAddress) pair in GetOwningReferencesFromObject(anchorObjectAddress, fieldPath, pathIndex: 0))
                {
                    yield return pair;
                }
            }
        }

        IEnumerable<(NativeWord childObjectAddress, NativeWord parentObjectAddress)> GetOwningReferencesFromObject(NativeWord referrer, (int typeIndex, int fieldNumber)[] fieldPath, int pathIndex)
        {
            MemoryView objectView = GetMemoryViewForAddress(referrer);
            if (!objectView.IsValid)
            {
                yield break;
            }

            (int typeIndex, int fieldNumber) = fieldPath[pathIndex];
            if (fieldNumber == Int32.MaxValue)
            {
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
                    foreach ((NativeWord childObjectAddress, NativeWord parentObjectAddress) in GetOwningReferencesFromField(fieldView, referrer, fieldPath, pathIndex + 1))
                    {
                        yield return (childObjectAddress, parentObjectAddress);
                    }
                }
            }
            else
            {
                int fieldOffset = m_typeSystem.FieldOffset(typeIndex, fieldNumber, withHeader: true);
                MemoryView fieldView = objectView.GetRange(fieldOffset, objectView.Size - fieldOffset);
                foreach ((NativeWord childObjectAddress, NativeWord parentObjectAddress) in GetOwningReferencesFromField(fieldView, referrer, fieldPath, pathIndex + 1))
                {
                    yield return (childObjectAddress, parentObjectAddress);
                }
            }
        }

        IEnumerable<(NativeWord childObjectAddress, NativeWord parentObjectAddress)> GetOwningReferencesFromField(MemoryView fieldView, NativeWord referrer, (int typeIndex, int fieldNumber)[] fieldPath, int pathIndex)
        {
            if (pathIndex == fieldPath.Length)
            {
                NativeWord reference = fieldView.ReadPointer(0, m_native);
                if (reference.Value != 0)
                {
                    yield return (reference, referrer);
                }
            }
            else
            {
                (int typeIndex, int fieldNumber) = fieldPath[pathIndex];

                if (m_typeSystem.IsValueType(typeIndex))
                {
                    int fieldOffset = m_typeSystem.FieldOffset(typeIndex, fieldNumber, withHeader: false);
                    MemoryView subfieldView = fieldView.GetRange(fieldOffset, fieldView.Size - fieldOffset);
                    foreach ((NativeWord childObjectAddress, NativeWord parentObjectAddress) in GetOwningReferencesFromField(subfieldView, referrer, fieldPath, pathIndex + 1))
                    {
                        yield return (childObjectAddress, parentObjectAddress);
                    }
                }
                else
                {
                    NativeWord reference = fieldView.ReadPointer(0, m_native);
                    foreach ((NativeWord, NativeWord) pair in GetOwningReferencesFromObject(reference, fieldPath, pathIndex))
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
