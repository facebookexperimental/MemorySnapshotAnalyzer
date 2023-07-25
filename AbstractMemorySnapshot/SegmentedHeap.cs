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
                foreach ((NativeWord childObjectAddress, NativeWord parentObjectAddress) pair in InterpretSelector(anchorObjectAddress, selector))
                {
                    yield return pair;
                }
            }
        }

        public IEnumerable<(NativeWord objectAddress, List<string> tags)> GetTagsFromAnchor(NativeWord anchorObjectAddress, PointerInfo<NativeWord> pointerInfo)
        {
            foreach ((Selector selector, List<string> tags) in m_typeSystem.GetTagAnchorSelectors(pointerInfo.TypeIndex, pointerInfo.FieldNumber))
            {
                foreach ((NativeWord childObjectAddress, NativeWord _) in InterpretSelector(anchorObjectAddress, selector))
                {
                    yield return (childObjectAddress, tags);
                }
            }
        }

        public IEnumerable<(NativeWord childObjectAddress, NativeWord parentObjectAddress)> InterpretSelector(NativeWord referrer, Selector selector)
        {
            MemoryView memoryView = GetMemoryViewForAddress(referrer);
            int typeIndex = m_traceableHeap.TryGetTypeIndex(referrer);
            return InterpretSelector(memoryView, true, typeIndex, referrer, referrer, selector, inStaticPrefix: true, pathIndex: 0);
        }

        public IEnumerable<(NativeWord childObjectAddress, NativeWord parentObjectAddress)> InterpretSelector(MemoryView inputView, bool inputWithHeader, int inputTypeIndex, NativeWord inputObjectAddress, NativeWord inputReferrer, Selector selector, bool inStaticPrefix, int pathIndex)
        {
            MemoryView memoryView = inputView;
            bool withHeader = inputWithHeader;
            int typeIndex = inputTypeIndex;
            NativeWord objectAddress = inputObjectAddress;
            NativeWord referrer = inputReferrer;
            while (true)
            {
                if (inStaticPrefix && pathIndex == selector.StaticPrefix.Count)
                {
                    inStaticPrefix = false;
                    pathIndex = 0;
                    continue;
                }
                else if (!inStaticPrefix && (selector.DynamicTail == null || pathIndex == selector.DynamicTail.Length))
                {
                    if (m_typeSystem.IsValueType(typeIndex))
                    {
                        // TODO: emit warning
                    }

                    yield return (objectAddress, referrer);
                    break;
                }

                int fieldNumber;
                if (inStaticPrefix)
                {
                    (typeIndex, fieldNumber) = selector.StaticPrefix[pathIndex];
                }
                else
                {
                    string fieldName = selector.DynamicTail![pathIndex];
                    if (fieldName.Equals("[]", StringComparison.Ordinal))
                    {
                        fieldNumber = Int32.MaxValue;
                    }
                    else
                    {
                        fieldNumber = m_typeSystem.GetFieldNumber(typeIndex, fieldName);
                        if (fieldNumber == -1)
                        {
                            // TODO: emit warning
                            yield break;
                        }
                    }
                }

                if (fieldNumber == Int32.MaxValue)
                {
                    if (!m_typeSystem.IsArray(typeIndex))
                    {
                        // TODO: emit warning
                        yield break;
                    }

                    int elementTypeIndex = m_typeSystem.BaseOrElementTypeIndex(typeIndex);
                    int arraySize = ReadArraySize(memoryView);
                    int elementSize = m_typeSystem.GetArrayElementSize(elementTypeIndex);
                    for (int i = 0; i < arraySize; i++)
                    {
                        int elementOffset = m_typeSystem.GetArrayElementOffset(elementTypeIndex, i);

                        // Check for partially-committed array.
                        if (elementOffset + elementSize > memoryView.Size)
                        {
                            break;
                        }

                        if (m_typeSystem.IsValueType(elementTypeIndex))
                        {
                            MemoryView elementView = memoryView.GetRange(elementOffset, elementSize);
                            foreach ((NativeWord, NativeWord) pair in InterpretSelector(elementView, false, elementTypeIndex, objectAddress, referrer, selector, inStaticPrefix, pathIndex + 1))
                            {
                                yield return pair;
                            }
                        }
                        else
                        {
                            NativeWord elementObjectAddress = memoryView.ReadPointer(elementOffset, m_native);
                            MemoryView elementView = GetMemoryViewForAddress(elementObjectAddress);
                            int effectiveElementTypeIndex = m_traceableHeap.TryGetTypeIndex(elementObjectAddress);
                            if (!elementView.IsValid || effectiveElementTypeIndex == -1)
                            {
                                yield break;
                            }

                            foreach ((NativeWord, NativeWord) pair in InterpretSelector(elementView, true, effectiveElementTypeIndex, elementObjectAddress, objectAddress, selector, inStaticPrefix, pathIndex + 1))
                            {
                                yield return pair;
                            }
                        }
                    }

                    yield break;
                }

                int fieldTypeIndex = m_typeSystem.FieldType(typeIndex, fieldNumber);
                if (m_typeSystem.FieldIsStatic(typeIndex, fieldNumber))
                {
                    memoryView = m_typeSystem.StaticFieldBytes(typeIndex, fieldNumber);
                    withHeader = !m_typeSystem.IsValueType(fieldTypeIndex);
                    if (withHeader)
                    {
                        // TODO: what should the referrer be?
                        referrer = objectAddress;
                        objectAddress = memoryView.ReadPointer(0, m_native);
                        memoryView = GetMemoryViewForAddress(objectAddress);
                        typeIndex = m_traceableHeap.TryGetTypeIndex(objectAddress);
                    }
                    else
                    {
                        typeIndex = fieldTypeIndex;
                    }
                }
                else
                {
                    int fieldOffset = m_typeSystem.FieldOffset(typeIndex, fieldNumber, withHeader);
                    if (m_typeSystem.IsValueType(fieldTypeIndex))
                    {
                        memoryView = memoryView.GetRange(fieldOffset, memoryView.Size - fieldOffset);
                        typeIndex = fieldTypeIndex;
                    }
                    else
                    {
                        referrer = objectAddress;
                        objectAddress = memoryView.ReadPointer(fieldOffset, m_native);
                        memoryView = GetMemoryViewForAddress(objectAddress);
                        typeIndex = m_traceableHeap.TryGetTypeIndex(objectAddress);
                    }
                }

                if (!memoryView.IsValid)
                {
                    // TODO: warning
                    yield break;
                }
                else if (typeIndex == -1)
                {
                    // TODO: warning
                    yield break;
                }

                pathIndex++;
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
