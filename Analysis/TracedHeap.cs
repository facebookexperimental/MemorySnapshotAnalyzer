// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MemorySnapshotAnalyzer.Analysis
{
    public sealed class TracedHeap
    {
        struct MarkStackEntry
        {
            public ulong Address;
            public int TypeIndex;
            public bool Processed;
        }

        struct ObjectAddressToPostorderIndexEntry
        {
            public ulong Address;
            public int PostorderIndex;
        }

        struct PostorderEntry
        {
            public ulong Address;

            // A type index if this entry represents an object, or -1 if this entry represents a (set of) root nodes.
            // The root nodes can be looked up using the address in m_objectAddressToRoots.
            public int TypeIndexOrRootSentinel;
        }

        readonly IRootSet m_rootSet;
        readonly TraceableHeap m_traceableHeap;
        readonly Native m_native;
        readonly List<PostorderEntry> m_postorderEntries;
        readonly Dictionary<ulong, int> m_numberOfPredecessors;
        readonly Dictionary<ulong, List<int>> m_objectAddressToRootIndices;
        readonly Stack<MarkStackEntry>? m_markStack;
        readonly int m_rootIndexBeingMarked;
        readonly List<(int, ulong)> m_invalidRoots;
        readonly List<(ulong, ulong)> m_invalidPointers;
        readonly ObjectAddressToPostorderIndexEntry[] m_objectAddressToPostorderIndex;

        public TracedHeap(IRootSet rootSet, bool weakGCHandles)
        {
            m_rootSet = rootSet;
            m_traceableHeap = rootSet.TraceableHeap;
            m_native = m_traceableHeap.Native;

            m_postorderEntries = new List<PostorderEntry>();
            m_numberOfPredecessors = new Dictionary<ulong, int>();
            m_markStack = new Stack<MarkStackEntry>();

            m_invalidRoots = new List<(int, ulong)>();
            m_invalidPointers = new List<(ulong, ulong)>();

            m_objectAddressToRootIndices = new Dictionary<ulong, List<int>>();
            for (int rootIndex = 0; rootIndex < rootSet.NumberOfRoots; rootIndex++)
            {
                NativeWord address = rootSet.GetRoot(rootIndex);
                if (address.Value != 0)
                {
                    int typeIndex = m_traceableHeap.TryGetTypeIndex(address);
                    if (typeIndex != -1)
                    {
                        if (m_objectAddressToRootIndices.TryGetValue(address.Value, out List<int>? rootIndices))
                        {
                            rootIndices!.Add(rootIndex);
                        }
                        else
                        {
                            m_objectAddressToRootIndices.Add(address.Value, new List<int>() { rootIndex });
                        }
                    }
                }
            }

            if (weakGCHandles)
            {
                // If weakGCHandles is false, return all predecessors.
                // Otherwise, if the only predecessors for this node are GCHandles, return those GCHandles.
                // Otherwise, skip the GCHandles.
                foreach (ulong address in m_objectAddressToRootIndices.Keys.ToArray())
                {
                    bool foundStatic = false;
                    bool foundGCHandle = false;
                    foreach (int rootIndex in m_objectAddressToRootIndices[address])
                    {
                        if (m_rootSet.IsGCHandle(rootIndex))
                        {
                            foundGCHandle = true;
                        }
                        else
                        {
                            foundStatic = true;
                        }
                    }

                    if (foundStatic && foundGCHandle)
                    {
                        var newRootIndices = new List<int>();
                        foreach (int rootIndex in m_objectAddressToRootIndices[address])
                        {
                            if (!m_rootSet.IsGCHandle(rootIndex))
                            {
                                newRootIndices.Add(rootIndex);
                            }
                        }

                        m_objectAddressToRootIndices[address] = newRootIndices;
                    }
                }
            }

            for (int rootIndex = 0; rootIndex < rootSet.NumberOfRoots; rootIndex++)
            {
                m_rootIndexBeingMarked = rootIndex;
                Mark(rootSet.GetRoot(rootIndex), default);
            }
            m_rootIndexBeingMarked = -1;

            ProcessMarkStack();
            m_markStack = null;

            // Create a lookup structure for object indices from addresses, suitable for binary search.
            m_objectAddressToPostorderIndex = new ObjectAddressToPostorderIndexEntry[m_postorderEntries.Count - m_objectAddressToRootIndices.Count];
            int lookupEntryIndex = 0;
            for (int postorderIndex = 0; postorderIndex < m_postorderEntries.Count; postorderIndex++)
            {
                if (m_postorderEntries[postorderIndex].TypeIndexOrRootSentinel != -1)
                {
                    m_objectAddressToPostorderIndex[lookupEntryIndex].Address = m_postorderEntries[postorderIndex].Address;
                    m_objectAddressToPostorderIndex[lookupEntryIndex].PostorderIndex = postorderIndex;
                    lookupEntryIndex++;
                }
            }
            Array.Sort(m_objectAddressToPostorderIndex, (x, y) => x.Address.CompareTo(y.Address));
        }

        public IRootSet RootSet => m_rootSet;

        public int NumberOfPostorderNodes => m_postorderEntries.Count;

        public int NumberOfLiveObjects => m_objectAddressToPostorderIndex.Length;

        public int NumberOfDistinctRoots => m_objectAddressToRootIndices.Count;

        public int NumberOfInvalidRoots => m_invalidRoots.Count;

        public int NumberOfInvalidPointers => m_invalidPointers.Count;

        public IEnumerable<(int, NativeWord)> GetInvalidRoots()
        {
            foreach ((int rootIndex, ulong reference) in m_invalidRoots)
            {
                yield return (rootIndex, m_native.From(reference));
            }
        }

        public IEnumerable<(NativeWord, NativeWord)> GetInvalidPointers()
        {
            foreach ((ulong reference, ulong objectAddress) in m_invalidPointers)
            {
                yield return (m_native.From(reference), m_native.From(objectAddress));
            }
        }

        public int GetNumberOfPredecessors(int postorderIndex)
        {
            return m_numberOfPredecessors[m_postorderEntries[postorderIndex].Address];
        }

        // Returns -1 if address is not the address of a live object.
        public int ObjectAddressToPostorderIndex(NativeWord address)
        {
            // TODO: make this work for interior pointers. We'll need to remember object sizes for that
            ulong addressValue = address.Value;
            int min = 0;
            int max = m_objectAddressToPostorderIndex.Length;
            while (min < max)
            {
                int mid = (min + max) / 2;
                if (m_objectAddressToPostorderIndex[mid].Address == addressValue)
                {
                    return m_objectAddressToPostorderIndex[mid].PostorderIndex;
                }
                else if (m_objectAddressToPostorderIndex[mid].Address < addressValue)
                {
                    min = mid + 1;
                }
                else
                {
                    max = mid;
                }
            }

            return -1;
        }

        // Returns the postorder index for the node representing the roots that have the given address as a target,
        // or -1 if the address is not the target of any roots.
        public int ObjectAddressToRootPostorderIndex(NativeWord address)
        {
            int postorderIndex = ObjectAddressToPostorderIndex(address);
            if (postorderIndex != -1
                && m_objectAddressToRootIndices.ContainsKey(m_postorderEntries[postorderIndex].Address))
            {
                // This relies on the knowledge that ProcessMarkStack reserves a postorder index for the root
                // immediately following the postorder index for the target object.
                return postorderIndex + 1;
            }
            else
            {
                return -1;
            }
        }

        public bool IsRootSentinel(int postorderIndex)
        {
            return m_postorderEntries[postorderIndex].TypeIndexOrRootSentinel == -1;
        }

        public NativeWord PostorderAddress(int postorderIndex)
        {
            return m_native.From(m_postorderEntries[postorderIndex].Address);
        }

        public int PostorderTypeIndexOrSentinel(int postorderIndex)
        {
            return m_postorderEntries[postorderIndex].TypeIndexOrRootSentinel;
        }

        public List<int> PostorderRootIndices(int postorderIndex)
        {
            return m_objectAddressToRootIndices[m_postorderEntries[postorderIndex].Address];
        }

        public void DescribeRootIndices(int postorderIndex, StringBuilder sb)
        {
            List<int> rootIndices = PostorderRootIndices(postorderIndex);
            sb.AppendFormat("roots#{0}{{", postorderIndex);
            for (int i = 0; i < rootIndices.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(", ");
                }
                sb.Append(m_rootSet.DescribeRoot(rootIndices[i], fullyQualified: true));
            }
            sb.Append('}');
        }

        void Mark(NativeWord reference, NativeWord referrer)
        {
            ulong address = reference.Value;

            // Fast path to avoid heap segment lookup for null pointers.
            if (address == 0)
            {
                return;
            }

            // Check whether this node has already been seen in the Mark() function.
            if (m_numberOfPredecessors.TryGetValue(address, out int count))
            {
                m_numberOfPredecessors[reference.Value] = count + 1;
                return;
            }
            m_numberOfPredecessors.Add(reference.Value, 1);

            int typeIndex = m_traceableHeap.TryGetTypeIndex(reference);
            if (typeIndex == -1)
            {
                // Object layout is invalid.
                if (m_rootIndexBeingMarked != -1)
                {
                    m_invalidRoots.Add((m_rootIndexBeingMarked, address));
                }
                else
                {
                    m_invalidPointers.Add((address, referrer.Value));
                }
                return;
            }

            MarkStackEntry entry;
            entry.Address = reference.Value;
            entry.TypeIndex = typeIndex;
            entry.Processed = false;
            m_markStack!.Push(entry);
        }

        void ProcessMarkStack()
        {
            while (m_markStack!.TryPop(out MarkStackEntry entry))
            {
                if (entry.Processed)
                {
                    // Reached an entry with a sentinel indicating that all of its children have already
                    // been fully marked. Add this to the postorder.
                    PostorderEntry postorderEntry;
                    postorderEntry.Address = entry.Address;
                    postorderEntry.TypeIndexOrRootSentinel = entry.TypeIndex;
                    m_postorderEntries.Add(postorderEntry);

                    if (m_objectAddressToRootIndices.ContainsKey(entry.Address))
                    {
                        PostorderEntry rootPostorderEntry;
                        rootPostorderEntry.Address = entry.Address;
                        rootPostorderEntry.TypeIndexOrRootSentinel = -1;
                        m_postorderEntries.Add(rootPostorderEntry);
                    }

                    continue;
                }

                // Put entry back on stack, with a sentinel indicating that its children have already been visited.
                // We'll find this entry when we're done with all nodes reachable from this one, and add it to postorder then.
                entry.Processed = true;
                m_markStack!.Push(entry);

                // Push all of the node's children that are nodes we haven't encountered previously.
                NativeWord address = m_native.From(entry.Address);
                foreach ((NativeWord reference, bool isOwningReference) in m_traceableHeap.GetIntraHeapPointers(address, entry.TypeIndex))
                {
                    Mark(reference, address);
                }
            }
        }
    }
}
