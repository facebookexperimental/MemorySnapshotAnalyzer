// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using System.Collections.Generic;

namespace MemorySnapshotAnalyzer.Analysis
{
    public sealed class RootSet : IRootSet
    {
        struct RootEntry
        {
            public int TypeIndexOrNegativeOne;
            public int FieldNumberOrGCHandleIndex;
            public int Offset;
            public ulong Value;
        }

        readonly TraceableHeap m_traceableHeap;
        readonly RootEntry[] m_roots;

        public RootSet(TraceableHeap traceableHeap)
        {
            m_traceableHeap = traceableHeap;

            var roots = new List<RootEntry>();
            AddGCHandleRoots(traceableHeap, roots);
            m_roots = roots.ToArray();
        }

        void AddGCHandleRoots(TraceableHeap traceableHeap, List<RootEntry> roots)
        {
            // Enumerate GCHandle targets as roots.
            for (int gcHandleIndex = 0; gcHandleIndex < traceableHeap.NumberOfGCHandles; gcHandleIndex++)
            {
                RootEntry entry;
                entry.TypeIndexOrNegativeOne = -1;
                entry.FieldNumberOrGCHandleIndex = gcHandleIndex;
                entry.Value = traceableHeap.GCHandleTarget(gcHandleIndex).Value;
                entry.Offset = 0;
                roots.Add(entry);
            }
        }

        public RootSet(SegmentedHeap segmentedHeap)
        {
            m_traceableHeap = segmentedHeap;

            var roots = new List<RootEntry>();
            AddGCHandleRoots(segmentedHeap, roots);
            AddStaticRoots(segmentedHeap, roots);
            m_roots = roots.ToArray();
        }

        void AddStaticRoots(SegmentedHeap segmentedHeap, List<RootEntry> roots)
        {
            ITypeSystem typeSystem = segmentedHeap.TypeSystem;

            // Enumerate all roots in static fields.
            for (int typeIndex = 0; typeIndex < typeSystem.NumberOfTypeIndices; typeIndex++)
            {
                int numberOfFields = typeSystem.NumberOfFields(typeIndex);
                for (int fieldNumber = 0; fieldNumber < numberOfFields; fieldNumber++)
                {
                    if (typeSystem.FieldIsStatic(typeIndex, fieldNumber))
                    {
                        MemoryView staticFieldBytesView = segmentedHeap.StaticFieldBytes(typeIndex, fieldNumber);

                        // Check whether the type has been initialized.
                        if (staticFieldBytesView.IsValid)
                        {
                            int fieldTypeIndex = typeSystem.FieldType(typeIndex, fieldNumber);
                            foreach (int offset in segmentedHeap.GetFieldPointerOffsets(fieldTypeIndex, baseOffset: 0))
                            {
                                RootEntry entry;
                                entry.TypeIndexOrNegativeOne = typeIndex;
                                entry.FieldNumberOrGCHandleIndex = fieldNumber;
                                entry.Offset = offset;
                                entry.Value = staticFieldBytesView.ReadPointer(offset, m_traceableHeap.Native).Value;
                                roots.Add(entry);
                            }
                        }
                    }
                }
            }
        }

        TraceableHeap IRootSet.TraceableHeap => m_traceableHeap;

        public int NumberOfRoots => m_roots.Length;

        public int NumberOfGCHandles => m_traceableHeap.NumberOfGCHandles;

        public int NumberOfStaticRoots => NumberOfRoots - NumberOfGCHandles;

        NativeWord IRootSet.GetRoot(int rootIndex)
        {
            return m_traceableHeap.Native.From(m_roots[rootIndex].Value);
        }

        bool IRootSet.IsGCHandle(int rootIndex)
        {
            RootEntry entry = m_roots[rootIndex];
            int typeIndex = entry.TypeIndexOrNegativeOne;
            return typeIndex == -1;
        }

        string IRootSet.DescribeRoot(int rootIndex, bool fullyQualified)
        {
            RootEntry entry = m_roots[rootIndex];
            int typeIndex = entry.TypeIndexOrNegativeOne;
            if (typeIndex == -1)
            {
                return $"GCHandle#{entry.FieldNumberOrGCHandleIndex}";
            }
            else
            {
                return string.Format("{0}.{1}+0x{2:X}",
                    m_traceableHeap.TypeSystem.UnqualifiedName(typeIndex),
                    m_traceableHeap.TypeSystem.FieldName(typeIndex, entry.FieldNumberOrGCHandleIndex),
                    entry.Offset);
            }
        }

        string IRootSet.RootType(int rootIndex)
        {
            RootEntry entry = m_roots[rootIndex];
            int typeIndex = entry.TypeIndexOrNegativeOne;
            if (typeIndex == -1)
            {
                return "gchandle";
            }
            else
            {
                return "static";
            }
        }

        static int IndexOfLastNamespaceDot(string qualifiedName)
        {
            int indexOfLastDot = -1;
            for (int i = 0; i < qualifiedName.Length; i++)
            {
                if (qualifiedName[i] == '.')
                {
                    indexOfLastDot = i;
                }
                else if (qualifiedName[i] == '<')
                {
                    break;
                }
            }
            return indexOfLastDot;
        }

        IRootSet.StaticRootInfo IRootSet.GetStaticRootInfo(int rootIndex)
        {
            RootEntry entry = m_roots[rootIndex];
            int typeIndex = entry.TypeIndexOrNegativeOne;
            if (typeIndex == -1)
            {
                return default;
            }

            IRootSet.StaticRootInfo info;
            info.AssemblyName = m_traceableHeap.TypeSystem.Assembly(typeIndex);

            string qualifiedName = m_traceableHeap.TypeSystem.QualifiedName(typeIndex);
            int indexOfDot = IndexOfLastNamespaceDot(qualifiedName);
            if (indexOfDot == -1)
            {
                info.NamespaceName = "";
                info.ClassName = qualifiedName;
            }
            else
            {
                info.NamespaceName = qualifiedName.Substring(0, indexOfDot);
                info.ClassName = qualifiedName.Substring(indexOfDot + 1);
            }

            return info;
        }
    }
}
