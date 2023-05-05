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

        readonly MemorySnapshot m_memorySnapshot;
        readonly IRootSet m_rootSet;
        readonly ulong m_minHeapAddress;
        readonly ulong m_maxHeapAddress;
        readonly Native m_native;
        readonly List<PostorderEntry> m_postorderObjectAddresses;
        readonly Dictionary<ulong, int> m_numberOfPredecessors;
        readonly Stack<MarkStackEntry>? m_markStack;
        bool m_markingRoots;
        readonly List<ulong> m_invalidRoots;
        readonly List<Tuple<ulong, ulong>> m_invalidPointers;
        readonly ObjectAddressToIndexEntry[] m_objectAddressesToIndex;

        public TracedHeap(IRootSet rootSet)
        {
            m_memorySnapshot = rootSet.MemorySnapshot;
            m_native = rootSet.MemorySnapshot.Native;
            m_rootSet = rootSet;

            ManagedHeap managedHeap = rootSet.MemorySnapshot.ManagedHeap;
            m_minHeapAddress = managedHeap.GetSegment(0).StartAddress.Value;
            m_maxHeapAddress = managedHeap.GetSegment(managedHeap.NumberOfSegments - 1).EndAddress.Value;

            m_postorderObjectAddresses = new List<PostorderEntry>();
            m_numberOfPredecessors = new Dictionary<ulong, int>();
            m_markStack = new Stack<MarkStackEntry>();

            m_invalidRoots = new List<ulong>();
            m_invalidPointers = new List<Tuple<ulong, ulong>>();

            m_markingRoots = true;
            for (int rootIndex = 0; rootIndex < rootSet.NumberOfRoots; rootIndex++)
            {
                NativeWord address = rootSet.GetRoot(rootIndex);
                Mark(address, address);
            }
            m_markingRoots = false;

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

        public IEnumerable<NativeWord> GetInvalidRoots()
        {
            foreach (var address in m_invalidRoots)
            {
                yield return m_native.From(address);
            }
        }

        public IEnumerable<Tuple<NativeWord, NativeWord>> GetInvalidPointers()
        {
            foreach (var tuple in m_invalidPointers)
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

            int typeIndex = m_memorySnapshot.TryGetTypeIndex(reference);
            if (typeIndex == -1)
            {
                // Not a valid object pointer; ignore.
                // TODO: when can this happen? Often these seem to be pointers to builtin strings/objects, and Unity reports the same.
                if (m_markingRoots)
                {
                    m_invalidRoots.Add(address);
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
                MemoryView objectView = m_memorySnapshot.GetMemoryViewForAddress(address);
                foreach (int offset in m_memorySnapshot.GetObjectPointerOffsets(objectView, entry.TypeIndex))
                {
                    NativeWord reference = objectView.ReadPointer(offset, m_native);
                    Mark(reference, address);
                }
            }
        }
    }
}
