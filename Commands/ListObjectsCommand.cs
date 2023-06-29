// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using MemorySnapshotAnalyzer.Analysis;
using MemorySnapshotAnalyzer.CommandProcessing;
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

        [FlagArgument("sortbysize")]
        public bool SortBySize;

        [NamedArgument("type")]
        public int TypeIndex = -1;

        [FlagArgument("includederived")]
        public bool IncludeDerived;

        [FlagArgument("owned")]
        public bool Owned;

        [FlagArgument("unowned")]
        public bool Unowned;
#pragma warning restore CS0649 // Field '...' is never assigned to, and will always have its default value

        public override void Run()
        {
            if (TypeIndex != -1)
            {
                if (TypeIndex >= CurrentTraceableHeap.TypeSystem.NumberOfTypeIndices)
                {
                    throw new CommandException($"{TypeIndex} is not a valid type index");
                }
            }
            else if (IncludeDerived)
            {
                throw new CommandException("can only provide 'includederived with 'list 'type");
            }

            if (Owned && Unowned)
            {
                throw new CommandException("can provide at most one of 'owned or 'unowned");
            }

            ListObjects();
        }

        sealed class Statistics
        {
            readonly TracedHeap m_tracedHeap;
            readonly bool m_boxing;

            long m_totalSize;
            int m_numberOfObjects;
            readonly Dictionary<int, (int Count, long Size)> m_perTypeCounts;

            internal Statistics(TracedHeap tracedHeap)
            {
                m_tracedHeap = tracedHeap;

                m_totalSize = 0;
                m_numberOfObjects = 0;
                m_perTypeCounts = new Dictionary<int, (int Count, long Size)>();
            }

            internal void Add(int postorderIndex)
            {
                int typeIndex = m_tracedHeap.PostorderTypeIndexOrSentinel(postorderIndex);
                if (typeIndex == -1)
                {
                    return;
                }

                long size = m_tracedHeap.RootSet.TraceableHeap.GetObjectSize(m_tracedHeap.PostorderAddress(postorderIndex), typeIndex, committedOnly: true);
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
            }

            internal void DumpSummary(IOutput output)
            {
                output.WriteLine("number of live objects = {0}, total size = {1}",
                    m_numberOfObjects,
                    m_totalSize);
            }

            internal void Dump(IOutput output, bool sortBySize)
            {
                KeyValuePair<int, (int Count, long Size)>[] kvps = m_perTypeCounts.ToArray();
                if (sortBySize)
                {
                    Array.Sort(kvps, (a, b) => b.Value.Size.CompareTo(a.Value.Size));
                }
                else
                {
                    Array.Sort(kvps, (a, b) => b.Value.Count.CompareTo(a.Value.Count));
                }

                foreach (KeyValuePair<int, (int Count, long Size)> kvp in kvps)
                {
                    output.WriteLine("{0} object(s) of type {1} (type index {2}, total size {3})",
                        kvp.Value.Count,
                        m_tracedHeap.RootSet.TraceableHeap.TypeSystem.QualifiedName(kvp.Key),
                        kvp.Key,
                        kvp.Value.Size);
                }
            }
        }

        void ListObjects()
        {
            // This command can operate in the following modes:
            // - (If neither 'type nor 'owned nor 'unowned are given) List all live objects.
            // - (If 'type and 'owned/'unowned are given) List all owned/unowned objects of the given type.
            // - (If only 'type is given) List all objects of the given type.
            // - (If only 'owned is given) List all owned objects.
            // - (If only 'unowned is given) Find all types of objects that are owned, according to the reference classifier.
            //   Then list all unowned instances of these types.

            var typeSet = new TypeSet(CurrentTraceableHeap.TypeSystem);
            if (TypeIndex != -1)
            {
                // Only consider objects of the given type.
                typeSet.Add(TypeIndex);
                if (IncludeDerived)
                {
                    typeSet.AddDerivedTypes();
                }
            }
            else if (Unowned)
            {
                // Consider all objects of types for which there is at least one owned instance.
                for (int postorderIndex = 0; postorderIndex < CurrentTracedHeap.NumberOfPostorderNodes; postorderIndex++)
                {
                    int typeIndex = CurrentTracedHeap.PostorderTypeIndexOrSentinel(postorderIndex);
                    if (typeIndex != -1 && CurrentBacktracer.IsOwned(postorderIndex))
                    {
                        typeSet.Add(typeIndex);
                    }
                }
            }

            var statistics = new Statistics(CurrentTracedHeap);
            SelectObjects(typeSet, postorderIndex => statistics.Add(postorderIndex));
            statistics.DumpSummary(Output);

            if (StatisticsOnly)
            {
                statistics.Dump(Output, SortBySize);
            }
            else
            {
                var sb = new StringBuilder();
                SelectObjects(typeSet, postorderIndex =>
                {
                    NativeWord address = CurrentTracedHeap.PostorderAddress(postorderIndex);
                    DescribeAddress(address, sb);
                    Output.WriteLine(sb.ToString());
                    sb.Clear();
                });
            }
        }

        void SelectObjects(TypeSet typeSet, Action<int> select)
        {
            for (int postorderIndex = 0; postorderIndex < CurrentTracedHeap.NumberOfPostorderNodes; postorderIndex++)
            {
                int typeIndex = CurrentTracedHeap.PostorderTypeIndexOrSentinel(postorderIndex);
                if (typeIndex != -1 && (typeSet.Count == 0 || typeSet.Contains(typeIndex)))
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

                    if (selected)
                    {
                        select(postorderIndex);
                    }
                }
            }
        }

        public override string HelpText => "listobj ['stats ['sortbysize]] ['type <type index> ['includederived]] ['owned | 'unowned]";
    }
}
