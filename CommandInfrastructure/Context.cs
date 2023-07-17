﻿// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using MemorySnapshotAnalyzer.Analysis;
using MemorySnapshotAnalyzer.ReferenceClassifiers;
using System;
using System.Collections.Generic;
using System.Text;

namespace MemorySnapshotAnalyzer.CommandProcessing
{
    public sealed class Context : IDisposable
    {
        readonly int m_id;
        readonly IOutput m_output;
        readonly ReferenceClassifierStore m_referenceClassifierStore;

        public enum TraceableHeapKind
        {
            Managed,
            Native,
            Stitched,
        }

        // Options for TraceableHeap
        TraceableHeapKind m_traceableHeap_kind;
        bool m_traceableHeap_fuseObjectPairs;
        bool m_traceableHeap_referenceClassificationEnabled;
        readonly SortedSet<string> m_traceableHeap_referenceClassificationGroups;
        // Options for RootSet
        NativeWord m_rootSet_singletonRootAddress;
        // Options for TracedHeap
        bool m_tracedHeap_weakGCHandles;
        // Options for Backtracer
        bool m_backtracer_groupStatics;
        bool m_backtracer_fuseRoots;

        MemorySnapshot? m_currentMemorySnapshot;
        TraceableHeap? m_currentTraceableHeap;
        IRootSet? m_currentRootSet;
        TracedHeap? m_currentTracedHeap;
        IBacktracer? m_currentBacktracer;
        HeapDom? m_currentHeapDom;

        public Context(int id, IOutput output, ReferenceClassifierStore referenceClassifierStore)
        {
            m_id = id;
            m_output = output;
            m_referenceClassifierStore = referenceClassifierStore;

            m_traceableHeap_referenceClassificationEnabled = true;
            m_traceableHeap_referenceClassificationGroups = new();
        }

        public static Context WithSameOptionsAs(Context other, int newId)
        {
            var newContext = new Context(newId, other.m_output, other.m_referenceClassifierStore)
            {
                TraceableHeap_Kind = other.TraceableHeap_Kind,
                TraceableHeap_FuseObjectPairs = other.TraceableHeap_FuseObjectPairs,
                TraceableHeap_ReferenceClassification_Enabled = other.TraceableHeap_ReferenceClassification_Enabled,
                RootSet_SingletonRootAddress = other.RootSet_SingletonRootAddress,
                TracedHeap_WeakGCHandles = other.TracedHeap_WeakGCHandles,
                Backtracer_GroupStatics = other.Backtracer_GroupStatics,
                Backtracer_FuseRoots = other.Backtracer_FuseRoots
            };
            newContext.m_traceableHeap_referenceClassificationGroups.UnionWith(other.m_traceableHeap_referenceClassificationGroups);
            return newContext;
        }

        public void Dispose()
        {
            ClearContext();
        }

        public int Id => m_id;

        public void Dump(int indent)
        {
            foreach (string s in Serialize())
            {
                m_output.WriteLineIndented(indent, s);
            }
        }

        public IEnumerable<string> Serialize()
        {
            if (m_currentMemorySnapshot == null)
            {
                yield return "no heap snapshot loaded";
            }
            else
            {
                yield return string.Format("{0} ({1})",
                    m_currentMemorySnapshot.Filename,
                    m_currentMemorySnapshot.Format);
            }

            if (m_currentTraceableHeap == null)
            {
                yield return string.Format("TraceableHeap[kind={0}, fuseobjectpairs={1}, referenceclassifier={2}] not computed",
                    m_traceableHeap_kind,
                    m_traceableHeap_fuseObjectPairs,
                    GetReferenceClassifierDescription());
            }
            else
            {
                yield return string.Format("TraceableHeap[kind={0}, fuseobjectpairs={1}, referenceclassifier={2}] {3} ({4} type indices, {5} object pairs, {6})",
                    m_traceableHeap_kind,
                    m_traceableHeap_fuseObjectPairs,
                    GetReferenceClassifierDescription(),
                    m_currentTraceableHeap.Description,
                    m_currentTraceableHeap.TypeSystem.NumberOfTypeIndices,
                    m_currentTraceableHeap.NumberOfObjectPairs,
                    m_currentTraceableHeap.SegmentedHeapOpt != null ? "with memory" : "without memory");
            }

            if (m_currentRootSet == null)
            {
                yield return string.Format("RootSet[rootobject={0}] not computed",
                    m_rootSet_singletonRootAddress);
            }
            else
            {
                yield return string.Format("RootSet[rootobject={0}]: {1} roots ({2} GCHandles, {3} statics)",
                    m_rootSet_singletonRootAddress,
                    m_currentRootSet.NumberOfRoots,
                    m_currentRootSet.NumberOfGCHandles,
                    m_currentRootSet.NumberOfStaticRoots);
            }

            if (m_currentTracedHeap == null)
            {
                yield return string.Format("TracedHeap[weakgchandles={0}] not computed",
                    m_tracedHeap_weakGCHandles);
            }
            else
            {
                yield return string.Format("TracedHeap[weakgchandles={0}]: {1} live objects and {2} distinct roots ({3} invalid roots, {4} invalid pointers)",
                    m_tracedHeap_weakGCHandles,
                    m_currentTracedHeap.NumberOfLiveObjects,
                    m_currentTracedHeap.NumberOfDistinctRoots,
                    m_currentTracedHeap.NumberOfInvalidRoots,
                    m_currentTracedHeap.NumberOfInvalidPointers);
            }

            if (m_currentBacktracer == null)
            {
                yield return string.Format("Backtracer[groupstatics={0}, fuseroots={1}] not computed",
                    m_backtracer_groupStatics,
                    m_backtracer_fuseRoots);
            }
            else
            {
                yield return string.Format("Backtracer[groupstatics={0}, fuseroots={1}]",
                    m_backtracer_groupStatics,
                    m_backtracer_fuseRoots);
            }

            if (m_currentHeapDom == null)
            {
                yield return "HeapDom not computed";
            }
            else
            {
                yield return string.Format("HeapDom: {0} non-leaf nodes found",
                    m_currentHeapDom.NumberOfNonLeafNodes);
            }
        }

        public void ClearContext()
        {
            if (m_currentMemorySnapshot != null)
            {
                m_currentMemorySnapshot.Dispose();

                m_currentMemorySnapshot = null;
                m_currentTraceableHeap = null;
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

        public TraceableHeapKind TraceableHeap_Kind
        {
            get { return m_traceableHeap_kind; }
            set
            {
                if (m_traceableHeap_kind != value)
                {
                    m_traceableHeap_kind = value;
                    ClearTraceableHeap();
                }
            }
        }

        public bool TraceableHeap_FuseObjectPairs
        {
            get { return m_traceableHeap_fuseObjectPairs; }
            set
            {
                if (m_traceableHeap_fuseObjectPairs != value)
                {
                    m_traceableHeap_fuseObjectPairs = value;
                    if (m_traceableHeap_kind == TraceableHeapKind.Stitched)
                    {
                        ClearTraceableHeap();
                    }
                }
            }
        }

        public bool TraceableHeap_ReferenceClassification_Enabled
        {
            get { return m_traceableHeap_referenceClassificationEnabled; }
            set
            {
                if (m_traceableHeap_referenceClassificationEnabled != value)
                {
                    m_traceableHeap_referenceClassificationEnabled = value;
                    ClearTraceableHeap();
                }
            }
        }

        public void TraceableHeap_ReferenceClassifier_OnModifiedGroup(string groupName)
        {
            if (TraceableHeap_ReferenceClassification_Enabled && m_traceableHeap_referenceClassificationGroups.Contains(groupName))
            {
                ClearTraceableHeap();
            }
        }

        public void TraceableHeap_ReferenceClassifier_AddGroup(string groupName)
        {
            bool added = m_traceableHeap_referenceClassificationGroups.Add(groupName);
            if (added && m_traceableHeap_referenceClassificationEnabled)
            {
                ClearTraceableHeap();
            }
        }

        public void OnReferenceClassifierStoreUpdated(List<string> affectedGroups)
        {
            if (m_traceableHeap_referenceClassificationEnabled)
            {
                // TODO: check whether any of the groups enabled for this context are part of affectedGroups
                ClearTraceableHeap();
            }
        }

        string GetReferenceClassifierDescription()
        {
            if (m_traceableHeap_referenceClassificationEnabled)
            {
                StringBuilder sb = new();
                foreach (string groupName in m_traceableHeap_referenceClassificationGroups)
                {
                    if (sb.Length > 0)
                    {
                        sb.Append('+');
                    }
                    sb.Append(groupName);

                    if (m_referenceClassifierStore.TryGetGroup(groupName, out ReferenceClassifierGroup? group))
                    {
                        sb.AppendFormat("({0})", group!.NumberOfRules);
                    }
                }
                return $"enabled[{sb}]";
            }
            else
            {
                return "disabled";
            }
        }

        public TraceableHeap? CurrentTraceableHeap => m_currentTraceableHeap;

        public void EnsureTraceableHeap()
        {
            if (m_currentMemorySnapshot == null)
            {
                throw new CommandException($"context {m_id} has no memory snapshot loaded");
            }

            if (m_currentTraceableHeap == null)
            {
                m_output.Write("[context {0}] selecting traceable heap {1} ...", m_id, m_traceableHeap_kind);

                ReferenceClassifierFactory referenceClassifierFactory;
                if (m_traceableHeap_referenceClassificationEnabled && m_traceableHeap_referenceClassificationGroups.Count > 0)
                {
                    referenceClassifierFactory = new RuleBasedReferenceClassifierFactory
                        (m_referenceClassifierStore,
                        m_traceableHeap_referenceClassificationGroups);
                }
                else
                {
                    referenceClassifierFactory = new DefaultReferenceClassifierFactory();
                }

                switch (m_traceableHeap_kind)
                {
                    case TraceableHeapKind.Managed:
                        m_currentTraceableHeap = m_currentMemorySnapshot.ManagedHeap(referenceClassifierFactory);
                        break;
                    case TraceableHeapKind.Native:
                        m_currentTraceableHeap = m_currentMemorySnapshot.NativeHeap(referenceClassifierFactory);
                        break;
                    case TraceableHeapKind.Stitched:
                        m_currentTraceableHeap = new StitchedTraceableHeap(
                            m_currentMemorySnapshot.ManagedHeap(referenceClassifierFactory),
                            m_currentMemorySnapshot.NativeHeap(referenceClassifierFactory),
                            m_traceableHeap_fuseObjectPairs);
                        break;
                    default:
                        throw new IndexOutOfRangeException();
                }

                m_output.WriteLine(" {0} ({1} type indices, {2} object pairs, {3})",
                    m_currentTraceableHeap.Description,
                    m_currentTraceableHeap.TypeSystem.NumberOfTypeIndices,
                    m_currentTraceableHeap.NumberOfObjectPairs,
                    m_currentTraceableHeap.SegmentedHeapOpt != null ? "with memory" : "without memory");
            }
        }

        void ClearTraceableHeap()
        {
            m_currentTraceableHeap = null;
            ClearRootSet();
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
            EnsureTraceableHeap();

            if (m_currentRootSet == null)
            {
                m_output.Write("[context {0}] enumerating root set ...", m_id);
                if (m_rootSet_singletonRootAddress.Value != 0)
                {
                    m_currentRootSet = new SingletonRootSet(CurrentTraceableHeap!, m_rootSet_singletonRootAddress);
                }
                else
                {
                    m_currentRootSet = new RootSet(CurrentTraceableHeap!);
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

        public bool TracedHeap_WeakGCHandles
        {
            get { return m_tracedHeap_weakGCHandles; }
            set
            {
                if (m_tracedHeap_weakGCHandles != value)
                {
                    m_tracedHeap_weakGCHandles = value;
                    ClearTracedHeap();
                }
            }
        }

        public TracedHeap? CurrentTracedHeap => m_currentTracedHeap;

        public void EnsureTracedHeap()
        {
            EnsureRootSet();

            if (m_currentTracedHeap == null)
            {
                m_output.Write("[context {0}] tracing heap ...", m_id);
                m_currentTracedHeap = new TracedHeap(CurrentRootSet!, TracedHeap_WeakGCHandles);
                m_output.WriteLine(" {0} live objects and {1} distinct roots ({2} invalid roots, {3} invalid pointers)",
                    m_currentTracedHeap.NumberOfLiveObjects,
                    m_currentTracedHeap.NumberOfDistinctRoots,
                    m_currentTracedHeap.NumberOfInvalidRoots,
                    m_currentTracedHeap.NumberOfInvalidPointers);
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

        public bool Backtracer_FuseRoots
        {
            get { return m_backtracer_fuseRoots; }
            set
            {
                if (m_backtracer_fuseRoots != value)
                {
                    m_backtracer_fuseRoots = value;
                    ClearBacktracer();
                }
            }
        }

        public IBacktracer? CurrentBacktracer => m_currentBacktracer;

        public void EnsureBacktracer()
        {
            EnsureTracedHeap();

            if (m_currentBacktracer == null)
            {
                m_output.Write("[context {0}] computing backtraces ...", m_id);

                var backtracerOptions = Backtracer.Options.None;
                if (m_backtracer_fuseRoots)
                {
                    backtracerOptions |= Backtracer.Options.FuseRoots;
                }

                if (m_tracedHeap_weakGCHandles)
                {
                    backtracerOptions |= Backtracer.Options.WeakGCHandles;
                }

                IBacktracer backtracer = new Backtracer(CurrentTracedHeap!, backtracerOptions);
                if (m_backtracer_groupStatics)
                {
                    backtracer = new GroupingBacktracer(backtracer);
                }
                m_currentBacktracer = backtracer;
                m_output.WriteLine(" done");
            }
        }

        void ClearBacktracer()
        {
            m_currentBacktracer = null;
            ClearHeapDom();
        }

        public HeapDom? CurrentHeapDom => m_currentHeapDom;

        public void EnsureHeapDom()
        {
            EnsureBacktracer();

            if (m_currentHeapDom == null)
            {
                m_output.Write("[context {0}] computing dominator tree ...", m_id);
                m_currentHeapDom = new HeapDom(CurrentBacktracer!);
                m_output.WriteLine(" {0} non-leaf nodes found", m_currentHeapDom.NumberOfNonLeafNodes);
            }
        }

        void ClearHeapDom()
        {
            m_currentHeapDom = null;
        }

        #region Helpers for command line processing

        public int ResolveToPostorderIndex(NativeWord addressOrIndex)
        {
            if (m_currentMemorySnapshot == null)
            {
                throw new CommandException("no active memory snapshot");
            }

            EnsureTracedHeap();
            int postorderIndex = CurrentTracedHeap!.ObjectAddressToPostorderIndex(addressOrIndex);
            if (postorderIndex != -1)
            {
                return postorderIndex;
            }

            if (addressOrIndex.Value < (ulong)CurrentTracedHeap.NumberOfPostorderNodes)
            {
                return (int)addressOrIndex.Value;
            }

            SegmentedHeap? segmentedHeap = m_currentTraceableHeap!.SegmentedHeapOpt;
            if (segmentedHeap != null && !segmentedHeap.GetMemoryViewForAddress(addressOrIndex).IsValid)
            {
                throw new CommandException($"{addressOrIndex} is neither an address in mapped memory, nor is {addressOrIndex.Value} a valid index");
            }

            throw new CommandException($"no live object at address {addressOrIndex}");
        }

        #endregion
    }
}
