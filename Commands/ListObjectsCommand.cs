/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using MemorySnapshotAnalyzer.Analysis;
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

        [NamedArgument("execall")]
        public string? ExecAllCommandLine;

        [NamedArgument("count")]
        public int MaxCount;
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

            int numberOfModes = 0;
            numberOfModes += StatisticsOnly ? 1 : 0;
            numberOfModes += ExecCommandLine != null ? 1 : 0;
            numberOfModes += ExecAllCommandLine != null ? 1 : 0;
            if (numberOfModes > 1)
            {
                throw new CommandException("can provide at most one of 'stats or 'exec or 'execall");
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
                Context.EnsureHeapDom();
                sortOrder = SortOrder.SortByDomSize;
            }
            else
            {
                throw new CommandException("at most one of 'sortbycount, 'sortbysize, and `sortbydomsize may be given");
            }

            if (DirectlyDominatedBy.Size != 0 && NotInDominatorTree)
            {
                throw new CommandException("at most one of 'dominatedby and 'notindom may be given");
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
                    domParentNodeIndex = Context.ResolveToNodeIndex(DirectlyDominatedBy);
                }
            }
            else if (NotInDominatorTree)
            {
                domParentNodeIndex = CurrentBacktracer.UnreachableNodeIndex;
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
            readonly HeapDomSizes? m_heapDomSizes;

            long m_totalSize;
            int m_numberOfObjects;
            readonly Dictionary<int, (int count, long size)> m_perTypeCounts;
            readonly List<int> m_postorderIndices;

            internal Selection(Context context, SortOrder sortOrder)
            {
                m_context = context;
                m_sortOrder = sortOrder;
                if (m_sortOrder == SortOrder.SortByDomSize)
                {
                    // We use the default HeapDomSizes here because the existing 'type argument is not what we want.
                    // If we want to support sorting by dominator size for a type-filtered dominator tree,
                    // we can decide to add 'domtype and 'domincludederived arguments.
                    m_heapDomSizes = m_context.CurrentHeapDom!.DefaultHeapDomSizes;
                }

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
                    return m_heapDomSizes!.TreeSize(postorderIndex);
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

            internal void DumpSummary(IStructuredOutput output)
            {
                output.AddProperty("numberOfObjects", m_numberOfObjects);
                output.AddProperty("totalSize", m_totalSize);
                output.AddDisplayStringLine("number of live objects = {0}, total size = {1}",
                    m_numberOfObjects,
                    m_totalSize);
            }

            internal void DumpStatistics(IStructuredOutput output)
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

                output.BeginArray("statistics");

                TypeSystem typeSystem = m_context.CurrentTraceableHeap!.TypeSystem;
                foreach (KeyValuePair<int, (int count, long size)> kvp in kvps)
                {
                    output.BeginElement();

                    output.AddProperty("numberOfObjects", kvp.Value.count);
                    output.AddProperty("assembly", typeSystem.Assembly(kvp.Key));
                    output.AddProperty("qualifiedName", typeSystem.QualifiedName(kvp.Key));
                    output.AddProperty("typeIndex", kvp.Key);
                    output.AddProperty("totalSize", kvp.Value.size);
                    output.AddDisplayStringLine("{0} object(s) of type {1}:{2} (type index {3}, total size {4})",
                        kvp.Value.count,
                        typeSystem.Assembly(kvp.Key),
                        typeSystem.QualifiedName(kvp.Key),
                        kvp.Key,
                        kvp.Value.size);

                    output.EndElement();
                }

                output.EndArray();
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
                    if (typeIndex != -1 && CurrentBacktracer.Weight(postorderIndex) > 0)
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
                Output.BeginArray("commandExecutions");
                try
                {
                    int i = 0;
                    foreach (int postorderIndex in selection.ForAll())
                    {
                        Repl.RunCommand($"{ExecCommandLine} {postorderIndex}");
                        i++;
                        if (MaxCount > 0 && i >= MaxCount)
                        {
                            break;
                        }
                    }
                }
                finally
                {
                    Output.EndArray();
                }
            }
            else if (ExecAllCommandLine != null)
            {
                Output.BeginArray("commandExecution");
                try
                {
                    StringBuilder sb = new(ExecAllCommandLine);
                    int i = 0;
                    foreach (int postorderIndex in selection.ForAll())
                    {
                        sb.Append(' ');
                        sb.Append(postorderIndex.ToString());
                        i++;
                        if (MaxCount > 0 && i >= MaxCount)
                        {
                            break;
                        }
                    }
                    Repl.RunCommand(sb.ToString());
                }
                finally
                {
                    Output.EndArray();
                }
            }
            else
            {
                Output.BeginArray("objects");

                var sb = new StringBuilder();
                int i = 0;
                foreach (int postorderIndex in selection.ForAll())
                {
                    Output.BeginElement();

                    NativeWord address = CurrentTracedHeap.PostorderAddress(postorderIndex);
                    DescribeAddress(address, sb);
                    if (sortOrder == SortOrder.SortByDomSize)
                    {
                        int typeIndex = CurrentTracedHeap.PostorderTypeIndexOrSentinel(postorderIndex);
                        long size = selection.GetSize(postorderIndex, typeIndex);
                        sb.AppendFormat(" (dom size {0})", size);
                        Output.AddProperty("domSize", size);
                    }

                    Output.AddDisplayStringLine(sb.ToString());
                    sb.Clear();

                    Output.EndElement();

                    i++;
                    if (MaxCount > 0 && i >= MaxCount)
                    {
                        break;
                    }
                }

                Output.EndArray();
            }
        }

        void SelectObjects(TypeSet? typeSet, int domParentPostorderIndex, Selection selection)
        {
            string[] withTags = (WithTag ?? string.Empty).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            string[] withoutTags = (WithoutTag ?? string.Empty).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            for (int postorderIndex = 0; postorderIndex < CurrentTracedHeap.NumberOfPostorderNodes; postorderIndex++)
            {
                int typeIndex = CurrentTracedHeap.PostorderTypeIndexOrSentinel(postorderIndex);
                if (typeIndex != -1 && (typeSet == null || typeSet.Contains(typeIndex)))
                {
                    bool selected = true;
                    if (Unowned && CurrentBacktracer.Weight(postorderIndex) > 0)
                    {
                        selected = false;
                    }
                    else if (Owned && CurrentBacktracer.Weight(postorderIndex) <= 0)
                    {
                        selected = false;
                    }
                    else if (domParentPostorderIndex != -1)
                    {
                        int domNodeIndex = CurrentHeapDom.GetDominator(CurrentBacktracer.PostorderIndexToNodeIndex(postorderIndex));
                        selected = domParentPostorderIndex == domNodeIndex;
                    }

                    if (selected && withTags.Length > 0)
                    {
                        // To be selected, the object must have all of the specified tags.
                        foreach (string withTag in withTags)
                        {
                            if (!CurrentTracedHeap.HasTag(postorderIndex, withTag))
                            {
                                selected = false;
                            }
                        }
                    }

                    if (selected && withoutTags.Length > 0)
                    {
                        // To be selected, the object must not have any of the specified tags.
                        foreach (string withoutTag in withoutTags)
                        {
                            if (CurrentTracedHeap.HasTag(postorderIndex, withoutTag))
                            {
                                selected = false;
                            }
                        }
                    }

                    if (selected)
                    {
                        selection.Add(postorderIndex);
                    }
                }
            }
        }

        public override string HelpText => "listobj ['stats] ['type <type index> ['includederived]] ['owned | 'unowned] ['dominatedby <object address or index or -1 for process>] ['notindom] ['tagged <tag,...> | 'nottagged <tag,...>] ['sortbycount | 'sortbysize | 'sortbydomsize] ['count <max>] ['exec <command> | 'execall <command>]";
    }
}
