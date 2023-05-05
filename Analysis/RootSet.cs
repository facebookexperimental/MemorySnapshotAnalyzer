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

        readonly MemorySnapshot m_memorySnapshot;
        readonly RootEntry[] m_roots;

        public RootSet(MemorySnapshot memorySnapshot)
        {
            m_memorySnapshot = memorySnapshot;

            ITypeSystem typeSystem = m_memorySnapshot.TypeSystem;

            var roots = new List<RootEntry>();

            // Enumerate GCHandle targets as roots.
            for (int gcHandleIndex = 0; gcHandleIndex < m_memorySnapshot.NumberOfGCHandles; gcHandleIndex++)
            {
                RootEntry entry;
                entry.TypeIndexOrNegativeOne = -1;
                entry.FieldNumberOrGCHandleIndex = gcHandleIndex;
                entry.Value = m_memorySnapshot.GCHandleTarget(gcHandleIndex).Value;
                entry.Offset = 0;
                roots.Add(entry);
            }

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
                            foreach (int offset in m_memorySnapshot.GetFieldPointerOffsets(fieldTypeIndex, baseOffset: 0))
                            {
                                RootEntry entry;
                                entry.TypeIndexOrNegativeOne = typeIndex;
                                entry.FieldNumberOrGCHandleIndex = fieldNumber;
                                entry.Offset = offset;
                                entry.Value = staticFieldBytesView.ReadPointer(offset, m_memorySnapshot.Native).Value;
                                roots.Add(entry);
                            }
                        }
                    }
                }
            }

            m_roots = roots.ToArray();
        }

        MemorySnapshot IRootSet.MemorySnapshot => m_memorySnapshot;

        public int NumberOfRoots => m_roots.Length;

        public int NumberOfGCHandles => m_memorySnapshot.NumberOfGCHandles;

        public int NumberOfStaticRoots => NumberOfRoots - NumberOfGCHandles;

        NativeWord IRootSet.GetRoot(int rootIndex)
        {
            return m_memorySnapshot.Native.From(m_roots[rootIndex].Value);
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
                    m_memorySnapshot.TypeSystem.UnqualifiedName(typeIndex),
                    m_memorySnapshot.TypeSystem.FieldName(typeIndex, entry.FieldNumberOrGCHandleIndex),
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
            info.AssemblyName = m_memorySnapshot.TypeSystem.Assembly(typeIndex);

            string qualifiedName = m_memorySnapshot.TypeSystem.QualifiedName(typeIndex);
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
