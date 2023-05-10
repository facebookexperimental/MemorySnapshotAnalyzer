// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using System;
using System.Collections.Generic;

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

        struct ObjectAddressToIndexEntry
        {
            public ulong Address;
            public int ObjectIndex;
        }

        struct PostorderEntry
        {
            public ulong Address;
            public int TypeIndex;
        }

        readonly IRootSet m_rootSet;
        readonly ManagedHeap m_managedHeap;
        readonly ulong m_minHeapAddress;
        readonly ulong m_maxHeapAddress;
        readonly Native m_native;
        readonly List<PostorderEntry> m_postorderObjectAddresses;
        readonly Dictionary<ulong, int> m_numberOfPredecessors;
        readonly Stack<MarkStackEntry>? m_markStack;
        readonly int m_rootIndexBeingMarked;
        readonly List<Tuple<int, ulong>> m_invalidRoots;
        readonly List<Tuple<ulong, ulong>> m_invalidPointers;
        readonly List<Tuple<int, ulong>> m_nonHeapRoots;
        readonly List<Tuple<ulong, ulong>> m_nonHeapPointers;
        readonly ObjectAddressToIndexEntry[] m_objectAddressesToIndex;

        public TracedHeap(IRootSet rootSet)
        {
            m_rootSet = rootSet;
            m_managedHeap = rootSet.ManagedHeap;
            m_native = m_managedHeap.Native;

            m_minHeapAddress = m_managedHeap.GetSegment(0).StartAddress.Value;
            m_maxHeapAddress = m_managedHeap.GetSegment(m_managedHeap.NumberOfSegments - 1).EndAddress.Value;

            m_postorderObjectAddresses = new List<PostorderEntry>();
            m_numberOfPredecessors = new Dictionary<ulong, int>();
            m_markStack = new Stack<MarkStackEntry>();

            m_invalidRoots = new List<Tuple<int, ulong>>();
            m_invalidPointers = new List<Tuple<ulong, ulong>>();
            m_nonHeapRoots = new List<Tuple<int, ulong>>();
            m_nonHeapPointers = new List<Tuple<ulong, ulong>>();

            for (int rootIndex = 0; rootIndex < rootSet.NumberOfRoots; rootIndex++)
            {
                m_rootIndexBeingMarked = rootIndex;
                NativeWord address = rootSet.GetRoot(rootIndex);
                Mark(address, address);
            }
            m_rootIndexBeingMarked = -1;

            ProcessMarkStack();
            m_markStack = null;

            // Create a lookup structure for object indices from addresses, suitable for binary search.
            m_objectAddressesToIndex = new ObjectAddressToIndexEntry[m_postorderObjectAddresses.Count];
            for (int i = 0; i < m_postorderObjectAddresses.Count; i++)
            {
                m_objectAddressesToIndex[i].Address = m_postorderObjectAddresses[i].Address;
                m_objectAddressesToIndex[i].ObjectIndex = i;
            }
            Array.Sort(m_objectAddressesToIndex, (x, y) => x.Address.CompareTo(y.Address));
        }

        public IRootSet RootSet => m_rootSet;

        public int NumberOfLiveObjects => m_objectAddressesToIndex.Length;

        public int NumberOfInvalidRoots => m_invalidRoots.Count;

        public int NumberOfInvalidPointers => m_invalidPointers.Count;

        public int NumberOfNonHeapRoots => m_nonHeapRoots.Count;

        public int NumberOfNonHeapPointers => m_nonHeapPointers.Count;

        public IEnumerable<Tuple<int, NativeWord>> GetInvalidRoots()
        {
            foreach (var tuple in m_invalidRoots)
            {
                yield return Tuple.Create(tuple.Item1, m_native.From(tuple.Item2));
            }
        }

        public IEnumerable<Tuple<NativeWord, NativeWord>> GetInvalidPointers()
        {
            foreach (var tuple in m_invalidPointers)
            {
                yield return Tuple.Create(m_native.From(tuple.Item1), m_native.From(tuple.Item2));
            }
        }

        public IEnumerable<Tuple<int, NativeWord>> GetNonHeapRoots()
        {
            foreach (var tuple in m_nonHeapRoots)
            {
                yield return Tuple.Create(tuple.Item1, m_native.From(tuple.Item2));
            }
        }

        public IEnumerable<Tuple<NativeWord, NativeWord>> GetNonHeapPointers()
        {
            foreach (var tuple in m_nonHeapPointers)
            {
                yield return Tuple.Create(m_native.From(tuple.Item1), m_native.From(tuple.Item2));
            }
        }

        public int GetNumberOfPredecessors(int objectIndex)
        {
            return m_numberOfPredecessors[m_postorderObjectAddresses[objectIndex].Address];
        }

        public int ObjectAddressToIndex(NativeWord address)
        {
            // TODO: make this work for interior pointers. We'll need to remember object sizes for that
            ulong addressValue = address.Value;
            int min = 0;
            int max = m_objectAddressesToIndex.Length;
            while (min < max)
            {
                int mid = (min + max) / 2;
                if (m_objectAddressesToIndex[mid].Address == addressValue)
                {
                    return m_objectAddressesToIndex[mid].ObjectIndex;
                }
                else if (m_objectAddressesToIndex[mid].Address < addressValue)
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

        // Note that this returns objects in postorder.
        public NativeWord ObjectAddress(int objectIndex)
        {
            return m_native.From(m_postorderObjectAddresses[objectIndex].Address);
        }

        public int ObjectTypeIndex(int objectIndex)
        {
            return m_postorderObjectAddresses[objectIndex].TypeIndex;
        }

        void Mark(NativeWord reference, NativeWord referrer)
        {
            ulong address = reference.Value;

            // Fast path to avoid heap segment lookup for values that are obviously outside the managed heap.
            if (address < m_minHeapAddress || address >= m_maxHeapAddress)
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

            MemoryView objectView = m_managedHeap.GetMemoryViewForAddress(reference);
            if (!objectView.IsValid)
            {
                // Not a valid object pointer. This could be a pointer to a const or built-in object,
                // which would be allocated off the managed heap.
                if (m_rootIndexBeingMarked != -1)
                {
                    m_nonHeapRoots.Add(Tuple.Create(m_rootIndexBeingMarked, address));
                }
                else
                {
                    m_nonHeapPointers.Add(Tuple.Create(address, referrer.Value));
                }
                return;
            }

            int typeIndex = m_managedHeap.TryGetTypeIndex(objectView);
            if (typeIndex == -1)
            {
                // Object layout is invalid.
                if (m_rootIndexBeingMarked != -1)
                {
                    m_invalidRoots.Add(Tuple.Create(m_rootIndexBeingMarked, address));
                }
                else
                {
                    m_invalidPointers.Add(Tuple.Create(address, referrer.Value));
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
                    postorderEntry.TypeIndex = entry.TypeIndex;
                    m_postorderObjectAddresses.Add(postorderEntry);
                    continue;
                }

                // Put entry back on stack, with a sentinel indicating that its children have already been visited.
                // We'll find this entry when we're done with all nodes reachable from this one, and add it to postorder then.
                entry.Processed = true;
                m_markStack!.Push(entry);

                // Push all of the node's children that are nodes we haven't encountered previously.
                NativeWord address = m_native.From(entry.Address);
                MemoryView objectView = m_managedHeap.GetMemoryViewForAddress(address);
                foreach (int offset in m_managedHeap.GetObjectPointerOffsets(objectView, entry.TypeIndex))
                {
                    NativeWord reference = objectView.ReadPointer(offset, m_native);
                    Mark(reference, address);
                }
            }
        }
    }
}
