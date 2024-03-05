/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using MemorySnapshotAnalyzer.Analysis;
using MemorySnapshotAnalyzer.ReferenceClassifiers;
using System;
using System.Collections.Generic;
using System.Text;

namespace MemorySnapshotAnalyzer.CommandInfrastructure
{
    public sealed class Context : IDisposable
    {
        readonly int m_id;
        readonly IOutput m_interactiveOutput;
        readonly IStructuredOutput m_structuredOutput;
        readonly ILogger m_logger;
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
        readonly SortedSet<string> m_traceableHeap_referenceClassificationGroups;
        // Options for RootSet
        NativeWord m_rootSet_singletonRootAddress;
        bool m_rootSet_weakGCHandles;
        // Options for Backtracer
        bool m_backtracer_groupStatics;
        HashSet<(int childPostorderIndex, int parentPostorderIndex)> m_backtracer_referencesToIgnore;
        bool m_backtracer_fuseRoots;

        MemorySnapshot? m_currentMemorySnapshot;
        TraceableHeap? m_currentTraceableHeap;
        IRootSet? m_currentRootSet;
        TracedHeap? m_currentTracedHeap;
        IBacktracer? m_currentBacktracer;
        HeapDom? m_currentHeapDom;

        public Context(int id, IOutput interactiveOutput, IStructuredOutput structuredOutput, ILogger logger, ReferenceClassifierStore referenceClassifierStore)
        {
            m_id = id;
            m_interactiveOutput = interactiveOutput;
            m_structuredOutput = structuredOutput;
            m_logger = logger;
            m_referenceClassifierStore = referenceClassifierStore;

            m_traceableHeap_referenceClassificationGroups = new();
            m_backtracer_referencesToIgnore = new();
        }

        public static Context WithSameOptionsAs(Context other, ILogger logger, int newId)
        {
            var newContext = new Context(newId, other.m_interactiveOutput, other.m_structuredOutput, logger, other.m_referenceClassifierStore)
            {
                TraceableHeap_Kind = other.TraceableHeap_Kind,
                TraceableHeap_FuseObjectPairs = other.TraceableHeap_FuseObjectPairs,
                RootSet_SingletonRootAddress = other.RootSet_SingletonRootAddress,
                RootSet_WeakGCHandles = other.RootSet_WeakGCHandles,
                Backtracer_GroupStatics = other.Backtracer_GroupStatics,
                Backtracer_FuseRoots = other.Backtracer_FuseRoots
            };
            newContext.m_traceableHeap_referenceClassificationGroups.UnionWith(other.m_traceableHeap_referenceClassificationGroups);
            newContext.Backtracer_ReferencesToIgnore_Replace(other.m_backtracer_referencesToIgnore);
            return newContext;
        }

        public void SummarizeNewWarnings()
        {
            m_logger.SummarizeNew(m_structuredOutput);
        }

        public void FlushWarnings()
        {
            m_logger.Flush(m_structuredOutput);
        }

        public void Dispose()
        {
            ClearContext();
        }

        public int Id => m_id;

        public ILogger Logger => m_logger;

        public void Dump(int indent)
        {
            foreach (string s in Serialize())
            {
                m_interactiveOutput.WriteLineIndented(indent, s);
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
                yield return string.Format("RootSet[rootobject={0}, weakgchandles={1}] not computed",
                    m_rootSet_singletonRootAddress,
                    m_rootSet_weakGCHandles);
            }
            else
            {
                yield return string.Format("RootSet[rootobject={0}, weakgchandles={1}]: {2} roots ({3} GCHandles, {4} statics)",
                    m_rootSet_singletonRootAddress,
                    m_rootSet_weakGCHandles,
                    m_currentRootSet.NumberOfRoots,
                    m_currentRootSet.NumberOfGCHandles,
                    m_currentRootSet.NumberOfStaticRoots);
            }

            if (m_currentTracedHeap == null)
            {
                yield return string.Format("TracedHeap not computed");
            }
            else
            {
                yield return string.Format("TracedHeap: {0} distinct roots ({1} invalid roots, {2} invalid pointers) holding {3} bytes live in {4} objects",
                    m_currentTracedHeap.NumberOfDistinctRoots,
                    m_currentTracedHeap.NumberOfInvalidRoots,
                    m_currentTracedHeap.NumberOfInvalidPointers,
                    m_currentTracedHeap.NumberOfLiveBytes,
                    m_currentTracedHeap.NumberOfLiveObjects);
            }

            if (m_currentBacktracer == null)
            {
                yield return string.Format("Backtracer[groupstatics={0}, referencestoignore={1}, fuseroots={2}] not computed",
                    m_backtracer_groupStatics,
                    Backtracer_ReferencesToIgnore_Stringify(),
                    m_backtracer_fuseRoots);
            }
            else
            {
                yield return string.Format("Backtracer[groupstatics={0}, referencestoignore={1}, fuseroots={2}]",
                    m_backtracer_groupStatics,
                    Backtracer_ReferencesToIgnore_Stringify(),
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

        public void DumpToStructuredOutput(IStructuredOutput output)
        {
            output.BeginChild("heapSnapshot");
            if (m_currentMemorySnapshot != null)
            {
                output.AddProperty("filename", m_currentMemorySnapshot.Filename);
                output.AddProperty("format", m_currentMemorySnapshot.Format);
            }
            output.EndChild();

            output.BeginChild("traceableHeap");
            output.AddProperty("kind", m_traceableHeap_kind.ToString());
            output.AddProperty("fuseObjectPairs", m_traceableHeap_fuseObjectPairs);
            DumpReferenceClassifiersToStructuredOutput(output);
            if (m_currentTraceableHeap != null)
            {
                output.AddProperty("description", m_currentTraceableHeap.Description);
                output.AddProperty("numberOfTypeIndices", m_currentTraceableHeap.TypeSystem.NumberOfTypeIndices);
                output.AddProperty("numberOfObjectPairs", m_currentTraceableHeap.NumberOfObjectPairs);
                output.AddProperty("withMemory", m_currentTraceableHeap.SegmentedHeapOpt != null);
            }
            output.EndChild();

            output.BeginChild("rootSet");
            output.AddProperty("singleRootAddress", m_rootSet_singletonRootAddress.ToString());
            output.AddProperty("weakGChandles", m_rootSet_weakGCHandles);
            if (m_currentRootSet != null)
            {
                output.AddProperty("numberOfRoots", m_currentRootSet.NumberOfRoots);
                output.AddProperty("numberOfGCHandles", m_currentRootSet.NumberOfGCHandles);
                output.AddProperty("numberOfStaticRoots", m_currentRootSet.NumberOfStaticRoots);
            }
            output.EndChild();

            output.BeginChild("tracedHeap");
            if (m_currentTracedHeap != null)
            {
                output.AddProperty("numberOfDistinctRoots", m_currentTracedHeap.NumberOfDistinctRoots);
                output.AddProperty("numberOfInvalidRoots", m_currentTracedHeap.NumberOfInvalidRoots);
                output.AddProperty("numberOfInvalidPointers", m_currentTracedHeap.NumberOfInvalidPointers);
                output.AddProperty("numberOfLiveBytes", m_currentTracedHeap.NumberOfLiveBytes);
                output.AddProperty("numberOfLiveObjects", m_currentTracedHeap.NumberOfLiveObjects);
            }
            output.EndChild();

            output.BeginChild("backtracer");
            output.AddProperty("groupStatics", m_backtracer_groupStatics);
            Backtracer_ReferencesToIgnore_DumpToStructuredOutput(output);
            output.AddProperty("fuseRoots", m_backtracer_fuseRoots);
            output.AddProperty("computed", m_currentBacktracer != null);
            output.EndChild();

            output.BeginChild("heapDom");
            if (m_currentHeapDom != null)
            {
                output.AddProperty("numberOfNonLeafNodes", m_currentHeapDom.NumberOfNonLeafNodes);
            }
            output.EndChild();
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

        public void TraceableHeap_ReferenceClassifier_OnModifiedGroup(string groupName)
        {
            if (m_traceableHeap_referenceClassificationGroups.Contains(groupName))
            {
                ClearTraceableHeap();
            }
        }

        public void TraceableHeap_ReferenceClassifier_EnableGroup(string groupName)
        {
            bool added = m_traceableHeap_referenceClassificationGroups.Add(groupName);
            if (added)
            {
                ClearTraceableHeap();
            }
        }

        public void TraceableHeap_ReferenceClassifier_DisableGroup(string groupName)
        {
            bool removed = m_traceableHeap_referenceClassificationGroups.Remove(groupName);
            if (removed)
            {
                ClearTraceableHeap();
            }
        }

        string GetReferenceClassifierDescription()
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
                    sb.AppendFormat("({0})", group.NumberOfRules);
                }
            }

            return sb.Length == 0 ? "none" : sb.ToString();
        }

        void DumpReferenceClassifiersToStructuredOutput(IStructuredOutput output)
        {
            output.BeginArray("referenceClassifiers");
            foreach (string groupName in m_traceableHeap_referenceClassificationGroups)
            {
                output.BeginElement();
                output.AddProperty("groupName", groupName);

                if (m_referenceClassifierStore.TryGetGroup(groupName, out ReferenceClassifierGroup? group))
                {
                    output.AddProperty("numberOfRules", group.NumberOfRules);
                }
                output.EndElement();
            }
            output.EndArray();
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
                m_interactiveOutput.Write("[context {0}] selecting traceable heap {1} ...", m_id, m_traceableHeap_kind);

                ReferenceClassifierFactory referenceClassifierFactory;
                if (m_traceableHeap_referenceClassificationGroups.Count > 0)
                {
                    referenceClassifierFactory = new RuleBasedReferenceClassifierFactory
                        (m_referenceClassifierStore, m_logger, m_traceableHeap_referenceClassificationGroups);
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
                            m_currentMemorySnapshot.NativeHeap(new DefaultReferenceClassifierFactory()),
                            m_logger,
                            m_traceableHeap_fuseObjectPairs);
                        break;
                    default:
                        throw new IndexOutOfRangeException();
                }

                m_interactiveOutput.WriteLine(" {0} ({1} type indices, {2} object pairs, {3})",
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

        public bool RootSet_WeakGCHandles
        {
            get { return m_rootSet_weakGCHandles; }
            set
            {
                if (m_rootSet_weakGCHandles != value)
                {
                    m_rootSet_weakGCHandles = value;
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
                m_interactiveOutput.Write("[context {0}] enumerating root set ...", m_id);
                if (m_rootSet_singletonRootAddress.Value != 0)
                {
                    m_currentRootSet = new SingletonRootSet(CurrentTraceableHeap!, m_rootSet_singletonRootAddress);
                }
                else
                {
                    m_currentRootSet = new RootSet(CurrentTraceableHeap!, gcHandleWeight: RootSet_WeakGCHandles ? -2 : 0);
                }

                m_interactiveOutput.WriteLine(" {0} roots ({1} GCHandles, {2} statics)",
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

        public TracedHeap? CurrentTracedHeap => m_currentTracedHeap;

        public void EnsureTracedHeap()
        {
            EnsureRootSet();

            if (m_currentTracedHeap == null)
            {
                m_interactiveOutput.Write("[context {0}] tracing heap ...", m_id);
                m_currentTracedHeap = new TracedHeap(CurrentRootSet!, m_logger);
                m_interactiveOutput.WriteLine(" {0} distinct roots ({1} invalid roots, {2} invalid pointers) holding {3} bytes live in {4} objects",
                    m_currentTracedHeap.NumberOfDistinctRoots,
                    m_currentTracedHeap.NumberOfInvalidRoots,
                    m_currentTracedHeap.NumberOfInvalidPointers,
                    m_currentTracedHeap.NumberOfLiveBytes,
                    m_currentTracedHeap.NumberOfLiveObjects);
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

        public void Backtracer_ReferencesToIgnore_Replace(IEnumerable<(int childPostorderIndex, int parentPostorderIndex)> referencesToIgnore)
        {
            if (!m_backtracer_referencesToIgnore.SetEquals(referencesToIgnore))
            {
                m_backtracer_referencesToIgnore = new(referencesToIgnore);
                ClearBacktracer();
            }
        }

        public void Backtracer_ReferencesToIgnore_Add(int childPostorderIndex, int parentPostorderIndex)
        {
            if (m_backtracer_referencesToIgnore.Add((childPostorderIndex, parentPostorderIndex)))
            {
                ClearBacktracer();
            }
        }

        public void Backtracer_ReferencesToIgnore_Add(int childPostorderIndex)
        {
            if (m_backtracer_referencesToIgnore.Add((childPostorderIndex, -1)))
            {
                ClearBacktracer();
            }
        }

        public void Backtracer_ReferencesToIgnore_Remove(int childPostorderIndex, int parentPostorderIndex)
        {
            if (m_backtracer_referencesToIgnore.Remove((childPostorderIndex, parentPostorderIndex)))
            {
                ClearBacktracer();
            }
        }

        public void Backtracer_ReferencesToIgnore_Remove(int childPostorderIndex)
        {
            List<int> parentPostorderIndices = new();
            foreach ((int childPostorderIndex0, int parentPostorderIndex) in m_backtracer_referencesToIgnore)
            {
                if (childPostorderIndex0 == childPostorderIndex)
                {
                    parentPostorderIndices.Add(parentPostorderIndex);
                }
            }

            if (parentPostorderIndices.Count > 0)
            {
                foreach (int parentPostorderIndex in parentPostorderIndices)
                {
                    m_backtracer_referencesToIgnore.Remove((childPostorderIndex, parentPostorderIndex));
                }

                ClearBacktracer();
            }
        }

        string Backtracer_ReferencesToIgnore_Stringify()
        {
            if (m_backtracer_referencesToIgnore == null)
            {
                return "{}";
            }

            StringBuilder sb = new("{ ");
            bool first = true;
            foreach ((int childPostorderIndex, int parentPostorderIndex) in m_backtracer_referencesToIgnore)
            {
                if (!first)
                {
                    sb.Append(", ");
                    first = false;
                }

                sb.AppendFormat("{0} <- {1}", childPostorderIndex, parentPostorderIndex);
            }
            sb.Append(" }");
            return sb.ToString();
        }

        void Backtracer_ReferencesToIgnore_DumpToStructuredOutput(IStructuredOutput output)
        {
            output.BeginArray("referencesToIgnore");

            if (m_backtracer_referencesToIgnore != null)
            {
                foreach ((int childPostorderIndex, int parentPostorderIndex) in m_backtracer_referencesToIgnore)
                {
                    output.BeginElement();
                    output.AddProperty("childPostorderIndex", childPostorderIndex);
                    output.AddProperty("parentPostorderIndex", parentPostorderIndex);
                    output.EndElement();
                }
            }

            output.EndArray();
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
                m_interactiveOutput.Write("[context {0}] computing backtraces ...", m_id);

                IBacktracer backtracer = new Backtracer(CurrentTracedHeap!, m_logger, m_backtracer_referencesToIgnore, fuseRoots: m_backtracer_fuseRoots);
                if (m_backtracer_groupStatics)
                {
                    backtracer = new GroupingBacktracer(backtracer);
                }
                m_currentBacktracer = backtracer;
                m_interactiveOutput.WriteLine(" done");
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
                m_interactiveOutput.Write("[context {0}] computing dominator tree ...", m_id);
                m_currentHeapDom = new HeapDom(CurrentBacktracer!);
                m_interactiveOutput.WriteLine(" {0} non-leaf nodes found", m_currentHeapDom.NumberOfNonLeafNodes);
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

            throw new CommandException($"{addressOrIndex} is neither a live object address, nor is {addressOrIndex.Value} a valid index");
        }

        public int ResolveToNodeIndex(NativeWord addressOrIndex)
        {
            if (m_currentMemorySnapshot == null)
            {
                throw new CommandException("no active memory snapshot");
            }

            EnsureBacktracer();
            int postorderIndex = CurrentTracedHeap!.ObjectAddressToPostorderIndex(addressOrIndex);
            if (postorderIndex != -1)
            {
                return CurrentBacktracer!.PostorderIndexToNodeIndex(postorderIndex);
            }

            if (addressOrIndex.Value < (ulong)CurrentBacktracer!.NumberOfNodes)
            {
                return (int)addressOrIndex.Value;
            }

            throw new CommandException($"{addressOrIndex} is neither a live object address, nor is {addressOrIndex.Value} a valid index");
        }

        #endregion
    }
}
