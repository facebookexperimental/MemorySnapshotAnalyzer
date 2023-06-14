// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using System.Collections.Generic;

namespace MemorySnapshotAnalyzer.Analysis
{
    public sealed class RootSet : IRootSet
    {
        struct RootEntry
        {
            // -1 = GCHandle, -2 = cross-heap reference
            public int TypeIndexOrSentinel;

            public int FieldNumberOrReferrerId;
            public int Offset;
            public ulong Value;
        }

        readonly TraceableHeap m_traceableHeap;
        readonly List<RootEntry> m_roots;

        public RootSet(TraceableHeap traceableHeap)
        {
            m_traceableHeap = traceableHeap;

            m_roots = new List<RootEntry>();
            AddGCHandleRoots(traceableHeap);
            AddStaticRoots(traceableHeap.TypeSystem);
        }

        void AddGCHandleRoots(TraceableHeap traceableHeap)
        {
            // Enumerate GCHandle targets as roots.
            for (int gcHandleIndex = 0; gcHandleIndex < traceableHeap.NumberOfGCHandles; gcHandleIndex++)
            {
                AddGCHandleRoot(gcHandleIndex, traceableHeap.GCHandleTarget(gcHandleIndex));
            }
        }

        public void AddGCHandleRoot(int gcHandleIndex, NativeWord targetAddress)
        {
            RootEntry entry;
            entry.TypeIndexOrSentinel = -1;
            entry.FieldNumberOrReferrerId = gcHandleIndex;
            entry.Offset = 0;
            entry.Value = targetAddress.Value;
            m_roots.Add(entry);
        }

        void AddStaticRoots(TypeSystem typeSystem)
        {
            // Enumerate all roots in static fields.
            for (int typeIndex = 0; typeIndex < typeSystem.NumberOfTypeIndices; typeIndex++)
            {
                int numberOfFields = typeSystem.NumberOfFields(typeIndex);
                for (int fieldNumber = 0; fieldNumber < numberOfFields; fieldNumber++)
                {
                    if (typeSystem.FieldIsStatic(typeIndex, fieldNumber))
                    {
                        MemoryView staticFieldBytesView = typeSystem.StaticFieldBytes(typeIndex, fieldNumber);

                        // Check whether the type has been initialized.
                        if (staticFieldBytesView.IsValid)
                        {
                            int fieldTypeIndex = typeSystem.FieldType(typeIndex, fieldNumber);
                            foreach ((int offset, bool isOwningReference) in typeSystem.GetFieldPointerOffsets(fieldTypeIndex, baseOffset: 0))
                            {
                                AddStaticRoot(typeIndex, fieldNumber, offset, staticFieldBytesView.ReadPointer(offset, m_traceableHeap.Native));
                            }
                        }
                    }
                }
            }
        }

        public void AddStaticRoot(int typeIndex, int fieldNumber, int offset, NativeWord reference)
        {
            RootEntry entry;
            entry.TypeIndexOrSentinel = typeIndex;
            entry.FieldNumberOrReferrerId = fieldNumber;
            entry.Offset = offset;
            entry.Value = reference.Value;
            m_roots.Add(entry);
        }

        public void AddForeignHeapReference(int referrerId, NativeWord reference)
        {
            RootEntry entry;
            entry.TypeIndexOrSentinel = -2;
            entry.FieldNumberOrReferrerId = referrerId;
            entry.Offset = 0;
            entry.Value = reference.Value;
            m_roots.Add(entry);
        }

        TraceableHeap IRootSet.TraceableHeap => m_traceableHeap;

        public int NumberOfRoots => m_roots.Count;

        public int NumberOfGCHandles => m_traceableHeap.NumberOfGCHandles;

        public int NumberOfStaticRoots => NumberOfRoots - NumberOfGCHandles;

        NativeWord IRootSet.GetRoot(int rootIndex)
        {
            return m_traceableHeap.Native.From(m_roots[rootIndex].Value);
        }

        bool IRootSet.IsGCHandle(int rootIndex)
        {
            RootEntry entry = m_roots[rootIndex];
            int typeIndex = entry.TypeIndexOrSentinel;
            return typeIndex == -1;
        }

        string IRootSet.DescribeRoot(int rootIndex, bool fullyQualified)
        {
            RootEntry entry = m_roots[rootIndex];
            int typeIndex = entry.TypeIndexOrSentinel;
            if (typeIndex == -1)
            {
                return $"GCHandle#{entry.FieldNumberOrReferrerId}";
            }
            else
            {
                return string.Format("{0}.{1}+0x{2:X}",
                    m_traceableHeap.TypeSystem.UnqualifiedName(typeIndex),
                    m_traceableHeap.TypeSystem.FieldName(typeIndex, entry.FieldNumberOrReferrerId),
                    entry.Offset);
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
            int typeIndex = entry.TypeIndexOrSentinel;
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
