// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using MemorySnapshotAnalyzer.Analysis;
using System;

namespace MemorySnapshotAnalyzer.CommandProcessing
{
    public sealed class Context : IDisposable
    {
        readonly int m_id;
        readonly IOutput m_output;

        // Options for RootSet
        NativeWord m_rootSet_singletonRootAddress;
        // Options for Backtracer
        bool m_backtracer_groupStatics;
        // Options for HeapDom
        bool m_heapDom_weakGCHandles;

        MemorySnapshot? m_currentMemorySnapshot;
        IRootSet? m_currentRootSet;
        ITracedHeap? m_currentTracedHeap;
        IBacktracer? m_currentBacktracer;
        HeapDom? m_currentHeapDom;

        public Context(int id, IOutput output)
        {
            m_id = id;
            m_output = output;
        }

        public static Context WithSameOptionsAs(Context other, int newId)
        {
            var newContext = new Context(newId, other.m_output)
            {
                RootSet_SingletonRootAddress = other.RootSet_SingletonRootAddress,
                Backtracer_GroupStatics = other.Backtracer_GroupStatics,
                HeapDom_WeakGCHandles = other.HeapDom_WeakGCHandles
            };
            return newContext;
        }

        public void Dispose()
        {
            ClearContext();
        }

        public int Id => m_id;

        public void Dump(int indent)
        {
            if (m_currentMemorySnapshot == null)
            {
                m_output.WriteLineIndented(indent, "no heap snapshot loaded");
            }
            else
            {
                m_output.WriteLineIndented(indent, "{0} ({1}; {2} type indices)",
                    m_currentMemorySnapshot.Filename,
                    m_currentMemorySnapshot.Format,
                    m_currentMemorySnapshot.ManagedHeap.TypeSystem.NumberOfTypeIndices);
            }

            if (m_currentRootSet == null)
            {
                m_output.WriteLineIndented(indent, "RootSet[rootobject={0}] not computed",
                    m_rootSet_singletonRootAddress);
            }
            else
            {
                m_output.WriteLineIndented(indent, "RootSet[rootobject={0}]: {1} roots ({2} GCHandles, {3} statics)",
                    m_rootSet_singletonRootAddress,
                    m_currentRootSet.NumberOfRoots,
                    m_currentRootSet.NumberOfGCHandles,
                    m_currentRootSet.NumberOfStaticRoots);
            }

            if (m_currentTracedHeap == null)
            {
                m_output.WriteLineIndented(indent, "TracedHeap not computed");
            }
            else
            {
                m_output.WriteLineIndented(indent, "TracedHeap: {0} live objects ({1} invalid roots, {2} invalid pointers, {3} non-heap roots, {4} non-heap pointers)",
                    m_currentTracedHeap.NumberOfLiveObjects,
                    m_currentTracedHeap.NumberOfInvalidRoots,
                    m_currentTracedHeap.NumberOfInvalidPointers,
                    m_currentTracedHeap.NumberOfNonHeapRoots,
                    m_currentTracedHeap.NumberOfNonHeapPointers);
            }

            if (m_currentBacktracer == null)
            {
                m_output.WriteLineIndented(indent,"Backtracer[groupstatics={0}] not computed",
                    m_backtracer_groupStatics);
            }
            else
            {
                m_output.WriteLineIndented(indent, "Backtracer[groupstatics={0}]",
                    m_backtracer_groupStatics);
            }

            if (m_currentHeapDom == null)
            {
                m_output.WriteLineIndented(indent, "HeapDom[weakgchandles={0}] not computed",
                    m_heapDom_weakGCHandles);
            }
            else
            {
                m_output.WriteLineIndented(indent, "HeapDom[weakgchandles={0}]: {1} non-leaf nodes found",
                    m_heapDom_weakGCHandles,
                    m_currentHeapDom.NumberOfNonLeafNodes);
            }
        }

        public void ClearContext()
        {
            if (m_currentMemorySnapshot != null)
            {
                m_currentMemorySnapshot.Dispose();
                m_currentMemorySnapshot = null;
                m_currentRootSet = null;
                m_currentTracedHeap = null;
                m_currentBacktracer = null;
                m_currentHeapDom = null;
            }
        }

        public MemorySnapshot? CurrentMemorySnapshot
        {
            get
            {
                return m_currentMemorySnapshot;
            }

            set
            {
                if (m_currentMemorySnapshot != null)
                {
                    ClearContext();
                }

                m_currentMemorySnapshot = value;
            }
        }

        public NativeWord RootSet_SingletonRootAddress
        {
            get { return m_rootSet_singletonRootAddress; }
            set
            {
                if (m_rootSet_singletonRootAddress.Size == 0 || m_rootSet_singletonRootAddress != value)
                {
                    m_rootSet_singletonRootAddress = value;
                    ClearRootSet();
                }
            }
        }

        public IRootSet? CurrentRootSet => m_currentRootSet;

        public void EnsureRootSet()
        {
            if (m_currentMemorySnapshot == null)
            {
                throw new CommandException($"context {m_id} has no memory snapshot loaded");
            }

            if (m_currentRootSet == null)
            {
                m_output.Write("[context {0}] enumerating root set ...", m_id);
                if (m_rootSet_singletonRootAddress.Value != 0)
                {
                    m_currentRootSet = new SingletonRootSet(CurrentMemorySnapshot!.ManagedHeap, m_rootSet_singletonRootAddress);
                }
                else
                {
                    m_currentRootSet = new RootSet(CurrentMemorySnapshot!.ManagedHeap);
                }
                m_output.WriteLine(" {0} roots ({1} GCHandles, {2} statics)",
                    m_currentRootSet.NumberOfRoots,
                    m_currentRootSet.NumberOfGCHandles,
                    m_currentRootSet.NumberOfStaticRoots);
            }
        }

        void ClearRootSet()
        {
            m_currentRootSet = null;
            ClearTracedHeap();
        }

        public ITracedHeap? CurrentTracedHeap => m_currentTracedHeap;

        public void EnsureTracedHeap()
        {
            EnsureRootSet();

            if (m_currentTracedHeap == null)
            {
                m_output.Write("[context {0}] tracing heap ...", m_id);
                m_currentTracedHeap = new TracedHeap(CurrentRootSet!);
                m_output.WriteLine(" {0} live objects ({1} invalid roots, {2} invalid pointers, {3} non-heap roots, {4} non-heap pointers)",
                    m_currentTracedHeap.NumberOfLiveObjects,
                    m_currentTracedHeap.NumberOfInvalidRoots,
                    m_currentTracedHeap.NumberOfInvalidPointers,
                    m_currentTracedHeap.NumberOfNonHeapRoots,
                    m_currentTracedHeap.NumberOfNonHeapPointers);
            }
        }

        void ClearTracedHeap()
        {
            m_currentTracedHeap = null;
            ClearBacktracer();
        }

        public bool Backtracer_GroupStatics
        {
            get { return m_backtracer_groupStatics; }
            set
            {
                if (m_backtracer_groupStatics != value)
                {
                    m_backtracer_groupStatics = value;
                    ClearBacktracer();
                }
            }
        }

        public IBacktracer? CurrentBacktracer => m_currentBacktracer;

        public void EnsureBacktracer()
        {
            EnsureTracedHeap();

            if (m_currentHeapDom == null)
            {
                m_output.Write("[context {0}] computing backtraces ...", m_id);
                if (m_backtracer_groupStatics)
                {
                    var backtracer = new Backtracer(CurrentTracedHeap!);
                    m_currentBacktracer = new GroupingBacktracer(backtracer);
                }
                else
                {
                    m_currentBacktracer = new Backtracer(CurrentTracedHeap!);
                }
                m_output.WriteLine(" done");
            }
        }

        void ClearBacktracer()
        {
            m_currentBacktracer = null;
            ClearHeapDom();
        }

        public bool HeapDom_WeakGCHandles
        {
            get { return m_heapDom_weakGCHandles; }
            set
            {
                if (m_heapDom_weakGCHandles != value)
                {
                    m_heapDom_weakGCHandles = value;
                    ClearHeapDom();
                }
            }
        }

        public HeapDom? CurrentHeapDom => m_currentHeapDom;

        public void EnsureHeapDom()
        {
            EnsureBacktracer();

            if (m_currentHeapDom == null)
            {
                m_output.Write("[context {0}] computing dominator tree ...", m_id);
                m_currentHeapDom = new HeapDom(CurrentBacktracer!, weakGCHandles: HeapDom_WeakGCHandles);
                m_output.WriteLine(" {0} non-leaf nodes found", m_currentHeapDom.NumberOfNonLeafNodes);
            }
        }

        void ClearHeapDom()
        {
            m_currentHeapDom = null;
        }
    }
}
