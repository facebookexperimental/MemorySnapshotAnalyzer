// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using MemorySnapshotAnalyzer.CommandInfrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MemorySnapshotAnalyzer.Commands
{
    public class ListObjectsCommand : Command
    {
        public ListObjectsCommand(Repl repl) : base(repl) { }

#pragma warning disable CS0649 // Field '...' is never assigned to, and will always have its default value
        [FlagArgument("stats")]
        public bool StatisticsOnly;

        [FlagArgument("sortbycount")]
        public bool SortByCount;

        [FlagArgument("sortbysize")]
        public bool SortBySize;

        [FlagArgument("sortbydomsize")]
        public bool SortByDomSize;

        [NamedArgument("type")]
        public CommandLineArgument? TypeIndexOrPattern;

        [FlagArgument("includederived")]
        public bool IncludeDerived;

        [FlagArgument("owned")]
        public bool Owned;

        [FlagArgument("unowned")]
        public bool Unowned;

        [NamedArgument("dominatedby")]
        public NativeWord DirectlyDominatedBy;

        [FlagArgument("notindom")]
        public bool NotInDominatorTree;

        [NamedArgument("tagged")]
        public string? WithTag;

        [NamedArgument("nottagged")]
        public string? WithoutTag;

        [NamedArgument("exec")]
        public string? ExecCommandLine;
#pragma warning restore CS0649 // Field '...' is never assigned to, and will always have its default value

        public override void Run()
        {
            if (TypeIndexOrPattern == null && IncludeDerived)
            {
                throw new CommandException("can only provide 'includederived with 'list 'type");
            }
            else if (Owned && Unowned)
            {
                throw new CommandException("can provide at most one of 'owned or 'unowned");
            }
            else if (StatisticsOnly && ExecCommandLine != null)
            {
                throw new CommandException("can provide at most one of 'stats or 'exec");
            }

            SortOrder sortOrder;
            if (!SortByCount && !SortBySize && !SortByDomSize)
            {
                sortOrder = SortOrder.SortByIndex;
            }
            else if (SortByCount && !SortBySize && !SortByDomSize)
            {
                sortOrder = SortOrder.SortByCount;
            }
            else if (!SortByCount && SortBySize && !SortByDomSize)
            {
                sortOrder = SortOrder.SortBySize;
            }
            else if (!SortByCount && SortByDomSize && !SortBySize)
            {
                sortOrder = SortOrder.SortByDomSize;
            }
            else
            {
                throw new CommandException("at most one of 'sortbycount, 'sortbysize, and `sortbydomsize may be given");
            }

            int domParentNodeIndex;
            if (DirectlyDominatedBy.Size != 0)
            {
                if (DirectlyDominatedBy.Value == ulong.MaxValue)
                {
                    domParentNodeIndex = CurrentHeapDom.RootNodeIndex;
                }
                else
                {
                    domParentNodeIndex = CurrentBacktracer.PostorderIndexToNodeIndex(Context.ResolveToPostorderIndex(DirectlyDominatedBy));
                }
            }
            else
            {
                domParentNodeIndex = -1;
            }

            ListObjects(sortOrder, domParentNodeIndex);
        }

        enum SortOrder
        {
            SortByIndex,
            SortByCount,
            SortBySize,
            SortByDomSize,
        }

        sealed class Selection
        {
            readonly Context m_context;
            readonly SortOrder m_sortOrder;

            long m_totalSize;
            int m_numberOfObjects;
            readonly Dictionary<int, (int count, long size)> m_perTypeCounts;
            readonly List<int> m_postorderIndices;

            internal Selection(Context context, SortOrder sortOrder)
            {
                m_context = context;
                m_sortOrder = sortOrder;

                if (sortOrder == SortOrder.SortByDomSize)
                {
                    m_context.EnsureHeapDom();
                }
                else
                {
                    m_context.EnsureTracedHeap();
                }

                m_totalSize = 0;
                m_numberOfObjects = 0;
                m_perTypeCounts = new Dictionary<int, (int Count, long Size)>();
                m_postorderIndices = new List<int>();
            }

            internal long GetSize(int postorderIndex, int typeIndex)
            {
                if (m_sortOrder == SortOrder.SortByDomSize)
                {
                    return m_context.CurrentHeapDom!.TreeSize(postorderIndex);
                }
                else
                {
                    return m_context.CurrentTraceableHeap!.GetObjectSize(m_context.CurrentTracedHeap!.PostorderAddress(postorderIndex), typeIndex, committedOnly: true);
                }
            }

            internal void Add(int postorderIndex)
            {
                m_context.EnsureTracedHeap();
                int typeIndex = m_context.CurrentTracedHeap!.PostorderTypeIndexOrSentinel(postorderIndex);
                if (typeIndex == -1)
                {
                    return;
                }

                long size = GetSize(postorderIndex, typeIndex);
                m_totalSize += size;
                m_numberOfObjects++;

                if (m_perTypeCounts.TryGetValue(typeIndex, out (int Count, long Size) data))
                {
                    m_perTypeCounts[typeIndex] = (data.Count + 1, data.Size + size);
                }
                else
                {
                    m_perTypeCounts[typeIndex] = (1, size);
                }

                m_postorderIndices.Add(postorderIndex);
            }

            internal void DumpSummary(IOutput output)
            {
                output.WriteLine("number of live objects = {0}, total size = {1}",
                    m_numberOfObjects,
                    m_totalSize);
            }

            internal void DumpStatistics(IOutput output)
            {
                KeyValuePair<int, (int count, long size)>[] kvps = m_perTypeCounts.ToArray();
                switch (m_sortOrder)
                {
                    case SortOrder.SortByIndex:
                        Array.Sort(kvps, (a, b) => a.Key.CompareTo(b.Key));
                        break;
                    case SortOrder.SortByCount:
                        Array.Sort(kvps, (a, b) => b.Value.count.CompareTo(a.Value.count));
                        break;
                    case SortOrder.SortBySize:
                    case SortOrder.SortByDomSize:
                        Array.Sort(kvps, (a, b) => b.Value.size.CompareTo(a.Value.size));
                        break;
                }

                foreach (KeyValuePair<int, (int count, long size)> kvp in kvps)
                {
                    output.WriteLine("{0} object(s) of type {1} (type index {2}, total size {3})",
                        kvp.Value.count,
                        m_context.CurrentTracedHeap!.RootSet.TraceableHeap.TypeSystem.QualifiedName(kvp.Key),
                        kvp.Key,
                        kvp.Value.size);
                }
            }

            internal IEnumerable<int> ForAll()
            {
                switch (m_sortOrder)
                {
                    case SortOrder.SortByIndex:
                        break;
                    case SortOrder.SortByCount:
                        m_postorderIndices.Sort((postorderIndex1, postorderIndex2) =>
                        {
                            int typeIndex1 = m_context.CurrentTracedHeap!.PostorderTypeIndexOrSentinel(postorderIndex1);
                            int typeIndex2 = m_context.CurrentTracedHeap!.PostorderTypeIndexOrSentinel(postorderIndex2);
                            return m_perTypeCounts[typeIndex2].count.CompareTo(m_perTypeCounts[typeIndex1].count);
                        });
                        break;
                    case SortOrder.SortBySize:
                    case SortOrder.SortByDomSize:
                        m_postorderIndices.Sort((postorderIndex1, postorderIndex2) =>
                        {
                            int typeIndex1 = m_context.CurrentTracedHeap!.PostorderTypeIndexOrSentinel(postorderIndex1);
                            int typeIndex2 = m_context.CurrentTracedHeap!.PostorderTypeIndexOrSentinel(postorderIndex2);
                            return GetSize(postorderIndex2, typeIndex2).CompareTo(GetSize(postorderIndex1, typeIndex1));
                        });
                        break;
                }

                foreach (int postorderIndex in m_postorderIndices)
                {
                    yield return postorderIndex;
                }
            }
        }

        void ListObjects(SortOrder sortOrder, int domParentPostorderIndex)
        {
            // This command can operate in the following modes:
            // - (If neither 'type nor 'owned nor 'unowned are given) List all live objects.
            // - (If 'type and 'owned/'unowned are given) List all owned/unowned objects of the given type.
            // - (If only 'type is given) List all objects of the given type.
            // - (If only 'owned is given) List all owned objects.
            // - (If only 'unowned is given) Find all types of objects that are owned, according to the reference classifier.
            //   Then list all unowned instances of these types.

            TypeSet? typeSet;
            if (TypeIndexOrPattern != null)
            {
                // Only consider objects of the given type.
                typeSet = TypeIndexOrPattern.ResolveTypeIndexOrPattern(Context, IncludeDerived);
            }
            else if (Unowned)
            {
                // Consider all objects of types for which there is at least one owned instance.
                typeSet = new TypeSet(CurrentTraceableHeap.TypeSystem);
                for (int postorderIndex = 0; postorderIndex < CurrentTracedHeap.NumberOfPostorderNodes; postorderIndex++)
                {
                    int typeIndex = CurrentTracedHeap.PostorderTypeIndexOrSentinel(postorderIndex);
                    if (typeIndex != -1 && CurrentBacktracer.IsOwned(postorderIndex))
                    {
                        typeSet.Add(typeIndex);
                    }
                }
            }
            else
            {
                // Consider objects of all types.
                typeSet = null;
            }

            var selection = new Selection(Context, sortOrder);
            SelectObjects(typeSet, domParentPostorderIndex, selection);
            selection.DumpSummary(Output);

            if (StatisticsOnly)
            {
                selection.DumpStatistics(Output);
            }
            else if (ExecCommandLine != null)
            {
                foreach (int postorderIndex in selection.ForAll())
                {
                    Repl.RunCommand($"{ExecCommandLine} {postorderIndex}");
                }
            }
            else
            {
                var sb = new StringBuilder();
                foreach (int postorderIndex in selection.ForAll())
                {
                    NativeWord address = CurrentTracedHeap.PostorderAddress(postorderIndex);
                    DescribeAddress(address, sb);
                    if (sortOrder == SortOrder.SortByDomSize)
                    {
                        int typeIndex = CurrentTracedHeap.PostorderTypeIndexOrSentinel(postorderIndex);
                        sb.AppendFormat(" (dom size {0})", selection.GetSize(postorderIndex, typeIndex));
                    }
                    Output.WriteLine(sb.ToString());
                    sb.Clear();
                }
            }
        }

        void SelectObjects(TypeSet? typeSet, int domParentPostorderIndex, Selection selection)
        {
            for (int postorderIndex = 0; postorderIndex < CurrentTracedHeap.NumberOfPostorderNodes; postorderIndex++)
            {
                int typeIndex = CurrentTracedHeap.PostorderTypeIndexOrSentinel(postorderIndex);
                if (typeIndex != -1 && (typeSet == null || typeSet.Contains(typeIndex)))
                {
                    bool selected = true;
                    if (Unowned && CurrentBacktracer.IsOwned(postorderIndex))
                    {
                        selected = false;
                    }
                    else if (Owned && !CurrentBacktracer.IsOwned(postorderIndex))
                    {
                        selected = false;
                    }
                    else if (domParentPostorderIndex != -1)
                    {
                        int domNodeIndex = CurrentHeapDom.GetDominator(CurrentBacktracer.PostorderIndexToNodeIndex(postorderIndex));
                        selected = domParentPostorderIndex == domNodeIndex;
                    }
                    else if (NotInDominatorTree)
                    {
                        selected = CurrentHeapDom.GetDominator(CurrentBacktracer.PostorderIndexToNodeIndex(postorderIndex)) == -1;
                    }

                    if (selected && WithTag != null)
                    {
                        selected = HasTag(CurrentTracedHeap.PostorderAddress(postorderIndex), WithTag);
                    }

                    if (selected && WithoutTag != null)
                    {
                        selected = !HasTag(CurrentTracedHeap.PostorderAddress(postorderIndex), WithoutTag);
                    }

                    if (selected)
                    {
                        selection.Add(postorderIndex);
                    }
                }
            }
        }

        bool HasTag(NativeWord address, string selectTag)
        {
            foreach (string tag in CurrentTracedHeap.TagsForAddress(address))
            {
                if (tag == selectTag)
                {
                    return true;
                }
            }
            return false;
        }

        public override string HelpText => "listobj ['stats] ['type <type index> ['includederived]] ['owned | 'unowned] ['dominatedby <object address or index or -1 for process>] ['notindom] ['tagged <tag> | 'nottagged <tag>] ['sortbycount | 'sortbysize | 'sortbydomsize]";
    }
}
