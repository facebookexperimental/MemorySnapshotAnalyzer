/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;

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

        public HeapSegment[] HeapSegments => m_segments;

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

        public struct ValueReference
        {
            public NativeWord AddressOfContainingObject;
            public MemoryView ValueView;
            public int TypeIndex;
            public bool WithHeader;
        }

        public IEnumerable<(NativeWord childObjectAddress, NativeWord parentObjectAddress, int weight)> GetWeightedReferencesFromAnchor(Action<string, string> logWarning, NativeWord anchorObjectAddress, PointerInfo<NativeWord> pointerInfo)
        {
            foreach ((Selector selector, int weight, string location) in m_typeSystem.GetWeightAnchorSelectors(pointerInfo.TypeIndex, pointerInfo.FieldNumber))
            {
                foreach ((ValueReference valueReference, NativeWord parentObjectAddress) in InterpretSelector(logWarning, anchorObjectAddress, location, selector))
                {
                    if (!valueReference.WithHeader)
                    {
                        logWarning(location, string.Format("weighted reference selector returned non-reference type value of type {0}:{1} (type index {2})",
                            m_typeSystem.Assembly(valueReference.TypeIndex),
                            m_typeSystem.QualifiedName(valueReference.TypeIndex),
                            valueReference.TypeIndex));
                    }

                    yield return (valueReference.AddressOfContainingObject, parentObjectAddress, weight);
                }
            }
        }

        public IEnumerable<(NativeWord objectAddress, List<string> tags)> GetTagsFromAnchor(Action<string, string> logWarning, NativeWord anchorObjectAddress, PointerInfo<NativeWord> pointerInfo)
        {
            foreach ((Selector selector, List<string> tags, string location) in m_typeSystem.GetTagAnchorSelectors(pointerInfo.TypeIndex, pointerInfo.FieldNumber))
            {
                foreach ((ValueReference valueReference, NativeWord _) in InterpretSelector(logWarning, anchorObjectAddress, location, selector))
                {
                    if (!valueReference.WithHeader)
                    {
                        logWarning(location, string.Format("tag selector returned non-reference type value of type {0}:{1} (type index {2})",
                            m_typeSystem.Assembly(valueReference.TypeIndex),
                            m_typeSystem.QualifiedName(valueReference.TypeIndex),
                            valueReference.TypeIndex));
                    }

                    yield return (valueReference.AddressOfContainingObject, tags);
                }
            }
        }

        public IEnumerable<(ValueReference valueReference, NativeWord parentObjectAddress)> InterpretSelector(Action<string, string> logWarning, NativeWord referrer, string location, Selector selector)
        {
            ValueReference valueReference = new()
            {
                AddressOfContainingObject = referrer,
                ValueView = GetMemoryViewForAddress(referrer),
                TypeIndex = m_traceableHeap.TryGetTypeIndex(referrer),
                WithHeader = true,
            };
            return InterpretSelector(logWarning, valueReference, referrer, location, selector, inStaticPrefix: true, pathIndex: 0);
        }

        IEnumerable<(ValueReference valueReference, NativeWord parentObjectAddress)> InterpretSelector(Action<string, string> logWarning, ValueReference inputValueReference, NativeWord inputReferrer, string location, Selector selector, bool inStaticPrefix, int pathIndex)
        {
            ValueReference valueReference = inputValueReference;
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
                    yield return (valueReference, referrer);
                    yield break;
                }

                int fieldNumber;
                if (inStaticPrefix)
                {
                    (valueReference.TypeIndex, fieldNumber) = selector.StaticPrefix[pathIndex];
                }
                else
                {
                    string fieldName = selector.DynamicTail![pathIndex];
                    if (fieldName.Equals("[]", StringComparison.Ordinal))
                    {
                        fieldNumber = Selector.FieldNumberArraySentinel;
                    }
                    else
                    {
                        (valueReference.TypeIndex, fieldNumber) = m_typeSystem.GetFieldNumber(valueReference.TypeIndex, fieldName);
                        if (fieldNumber == -1)
                        {
                            logWarning(location, string.Format("field {0} not found in field or object at address {1} of type {2}:{3} (type index {4})",
                                fieldName,
                                valueReference.AddressOfContainingObject,
                                m_typeSystem.Assembly(valueReference.TypeIndex),
                                m_typeSystem.QualifiedName(valueReference.TypeIndex),
                                valueReference.TypeIndex));
                            yield break;
                        }
                    }
                }

                if (fieldNumber == Selector.FieldNumberArraySentinel)
                {
                    if (!m_typeSystem.IsArray(valueReference.TypeIndex))
                    {
                        logWarning(location, string.Format("object at address {0} is expected to be of array type, but found type {1}:{2} (type index {3})",
                            valueReference.AddressOfContainingObject,
                            m_typeSystem.Assembly(valueReference.TypeIndex),
                            m_typeSystem.QualifiedName(valueReference.TypeIndex),
                            valueReference.TypeIndex));
                        yield break;
                    }

                    int elementTypeIndex = m_typeSystem.BaseOrElementTypeIndex(valueReference.TypeIndex);
                    int arraySize = ReadArraySize(valueReference.ValueView);
                    int elementSize = m_typeSystem.GetArrayElementSize(elementTypeIndex);
                    for (int i = 0; i < arraySize; i++)
                    {
                        int elementOffset = m_typeSystem.GetArrayElementOffset(elementTypeIndex, i);

                        // Check for partially-committed array.
                        if (elementOffset + elementSize > valueReference.ValueView.Size)
                        {
                            break;
                        }

                        if (m_typeSystem.IsValueType(elementTypeIndex))
                        {
                            ValueReference elementReference = new()
                            {
                                AddressOfContainingObject = valueReference.AddressOfContainingObject,
                                ValueView = valueReference.ValueView.GetRange(elementOffset, elementSize),
                                TypeIndex = elementTypeIndex,
                                WithHeader = false,
                            };

                            foreach ((ValueReference, NativeWord) pair in InterpretSelector(logWarning, elementReference, referrer, location, selector, inStaticPrefix, pathIndex + 1))
                            {
                                yield return pair;
                            }
                        }
                        else
                        {
                            NativeWord elementObjectAddress = valueReference.ValueView.ReadPointer(elementOffset, m_native);
                            MemoryView elementView = GetMemoryViewForAddress(elementObjectAddress);
                            int effectiveElementTypeIndex = m_traceableHeap.TryGetTypeIndex(elementObjectAddress);
                            if (!elementView.IsValid || effectiveElementTypeIndex == -1)
                            {
                                yield break;
                            }

                            ValueReference elementReference = new()
                            {
                                AddressOfContainingObject = elementObjectAddress,
                                ValueView = elementView,
                                TypeIndex = effectiveElementTypeIndex,
                                WithHeader = true,
                            };

                            foreach ((ValueReference, NativeWord) pair in InterpretSelector(logWarning, elementReference, valueReference.AddressOfContainingObject, location, selector, inStaticPrefix, pathIndex + 1))
                            {
                                yield return pair;
                            }
                        }
                    }

                    yield break;
                }

                int fieldTypeIndex = m_typeSystem.FieldType(valueReference.TypeIndex, fieldNumber);
                if (m_typeSystem.FieldIsStatic(valueReference.TypeIndex, fieldNumber))
                {
                    valueReference.ValueView = m_typeSystem.StaticFieldBytes(valueReference.TypeIndex, fieldNumber);
                    valueReference.WithHeader = !m_typeSystem.IsValueType(fieldTypeIndex);
                    if (valueReference.WithHeader)
                    {
                        referrer = default;
                        valueReference.AddressOfContainingObject = valueReference.ValueView.ReadPointer(0, m_native);
                        if (valueReference.AddressOfContainingObject.Value == 0)
                        {
                            yield break;
                        }
                        valueReference.ValueView = GetMemoryViewForAddress(valueReference.AddressOfContainingObject);
                        valueReference.TypeIndex = m_traceableHeap.TryGetTypeIndex(valueReference.AddressOfContainingObject);
                    }
                    else
                    {
                        valueReference.TypeIndex = fieldTypeIndex;
                    }
                }
                else
                {
                    int fieldOffset = m_typeSystem.FieldOffset(valueReference.TypeIndex, fieldNumber, valueReference.WithHeader);
                    valueReference.WithHeader = !m_typeSystem.IsValueType(fieldTypeIndex);
                    if (valueReference.WithHeader)
                    {
                        referrer = valueReference.AddressOfContainingObject;
                        valueReference.AddressOfContainingObject = valueReference.ValueView.ReadPointer(fieldOffset, m_native);
                        if (valueReference.AddressOfContainingObject.Value == 0)
                        {
                            yield break;
                        }
                        valueReference.ValueView = GetMemoryViewForAddress(valueReference.AddressOfContainingObject);
                        valueReference.TypeIndex = m_traceableHeap.TryGetTypeIndex(valueReference.AddressOfContainingObject);
                    }
                    else
                    {
                        valueReference.ValueView = valueReference.ValueView.GetRange(fieldOffset, valueReference.ValueView.Size - fieldOffset);
                        valueReference.TypeIndex = fieldTypeIndex;
                    }
                }

                if (valueReference.TypeIndex == -1 || !valueReference.ValueView.IsValid)
                {
                    int parentTypeIndex = m_traceableHeap.TryGetTypeIndex(referrer);
                    string fieldName;

                    if (inStaticPrefix)
                    {
                        (int staticTypeIndex, int staticFieldNumber) = selector.StaticPrefix[pathIndex];
                        fieldName = m_typeSystem.FieldName(staticTypeIndex, staticFieldNumber);
                    }
                    else
                    {
                        fieldName = selector.DynamicTail![pathIndex];
                    }

                    logWarning(location, string.Format("object at address {0} of type {1}:{2} (type index {3}) refers to address {4} which is in unmapped memory (field name \"{5}\", {6} selector path index {7})",
                        referrer,
                        m_typeSystem.Assembly(parentTypeIndex),
                        m_typeSystem.QualifiedName(parentTypeIndex),
                        parentTypeIndex,
                        valueReference.AddressOfContainingObject,
                        fieldName,
                        inStaticPrefix ? "static" : "dynamic",
                        pathIndex));
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
