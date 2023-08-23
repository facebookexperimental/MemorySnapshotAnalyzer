/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using System.Collections.Generic;

namespace MemorySnapshotAnalyzer.Analysis
{
    public sealed class RootSet : IRootSet
    {
        struct RootEntry
        {
            public int Offset;
            // We use the contained TypeIndex as a sentinel; -1 represents a GCHandle.
            // In this case, the contained FieldNumber will be the GC handle index.
            public PointerInfo<NativeWord> PointerInfo;
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

        void AddGCHandleRoot(int gcHandleIndex, NativeWord targetAddress)
        {
            m_roots.Add(new RootEntry
            {
                Offset = 0,
                PointerInfo = new PointerInfo<NativeWord>
                {
                    Value = targetAddress,
                    PointerFlags = PointerFlags.None,
                    TypeIndex = -1,
                    FieldNumber = gcHandleIndex
                }
            });
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
                            foreach (PointerInfo<int> pointerInfo in typeSystem.GetStaticFieldPointerOffsets(typeIndex, fieldNumber))
                            {
                                int offset = pointerInfo.Value;
                                AddStaticRoot(pointerInfo, staticFieldBytesView.ReadPointer(offset, m_traceableHeap.Native));
                            }
                        }
                    }
                }
            }
        }

        void AddStaticRoot(PointerInfo<int> pointerInfo, NativeWord targetAddress)
        {
            m_roots.Add(new RootEntry
            {
                Offset = pointerInfo.Value,
                PointerInfo = pointerInfo.WithValue(targetAddress)
            });
        }

        TraceableHeap IRootSet.TraceableHeap => m_traceableHeap;

        public int NumberOfRoots => m_roots.Count;

        public int NumberOfGCHandles => m_traceableHeap.NumberOfGCHandles;

        public int NumberOfStaticRoots => NumberOfRoots - NumberOfGCHandles;

        PointerInfo<NativeWord> IRootSet.GetRoot(int rootIndex)
        {
            return m_roots[rootIndex].PointerInfo;
        }

        bool IRootSet.IsGCHandle(int rootIndex)
        {
            return m_roots[rootIndex].PointerInfo.TypeIndex == -1;
        }

        string IRootSet.DescribeRoot(int rootIndex, bool fullyQualified)
        {
            RootEntry entry = m_roots[rootIndex];
            int typeIndex = entry.PointerInfo.TypeIndex;
            if (typeIndex == -1)
            {
                return $"GCHandle#{entry.PointerInfo.FieldNumber}";
            }
            else
            {
                string typeName = fullyQualified ?
                    $"{m_traceableHeap.TypeSystem.Assembly(typeIndex)}:{m_traceableHeap.TypeSystem.QualifiedName(typeIndex)}" :
                    m_traceableHeap.TypeSystem.UnqualifiedName(typeIndex);
                return string.Format("{0}.{1}+0x{2:X}",
                    typeName,
                    m_traceableHeap.TypeSystem.FieldName(typeIndex, entry.PointerInfo.FieldNumber),
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
            int typeIndex = entry.PointerInfo.TypeIndex;
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
