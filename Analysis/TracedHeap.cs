/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

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

        static readonly string LOG_SOURCE = "TracedHeap";

        readonly IRootSet m_rootSet;
        readonly ILogger m_logger;
        readonly TraceableHeap m_traceableHeap;
        readonly Native m_native;
        readonly Dictionary<ulong, SortedSet<string>> m_tags;
        readonly List<PostorderEntry> m_postorderEntries;
        readonly Dictionary<ulong, int> m_numberOfPredecessors;
        readonly Dictionary<ulong, List<(int rootIndex, PointerInfo<NativeWord> pointerInfo)>> m_objectAddressToRootIndices;
        readonly Stack<MarkStackEntry>? m_markStack;
        readonly Action<string, string> m_logWarning;
        readonly int m_rootIndexBeingMarked;
        readonly List<(int rootIndex, ulong invalidReference)> m_invalidRoots;
        readonly List<(ulong reference, ulong objectAddress)> m_invalidPointers;
        readonly ObjectAddressToPostorderIndexEntry[] m_objectAddressToPostorderIndex;
        long m_numberOfLiveBytes;

        public TracedHeap(IRootSet rootSet, ILogger logger)
        {
            m_rootSet = rootSet;
            m_logger = logger;
            m_traceableHeap = rootSet.TraceableHeap;
            m_native = m_traceableHeap.Native;

            m_tags = new();
            m_postorderEntries = new();
            m_numberOfPredecessors = new();
            m_markStack = new();

            m_invalidRoots = new();
            m_invalidPointers = new();

            // Group all the roots that reference each individual target objects into a single node.
            m_objectAddressToRootIndices = new();
            for (int rootIndex = 0; rootIndex < rootSet.NumberOfRoots; rootIndex++)
            {
                PointerInfo<NativeWord> pointerInfo = rootSet.GetRoot(rootIndex);
                NativeWord address = pointerInfo.Value;
                if (address.Value != 0 && m_traceableHeap.TryGetTypeIndex(address) != -1)
                {
                    if (m_objectAddressToRootIndices.TryGetValue(address.Value, out List<(int rootIndex, PointerInfo<NativeWord> pointerInfo)>? rootInfos))
                    {
                        rootInfos.Add((rootIndex, pointerInfo));
                    }
                    else
                    {
                        m_objectAddressToRootIndices.Add(address.Value, new List<(int rootIndex, PointerInfo<NativeWord> pointerInfo)>() { (rootIndex, pointerInfo) });
                    }
                }
            }

            m_logWarning = LogWarning;

            for (int rootIndex = 0; rootIndex < rootSet.NumberOfRoots; rootIndex++)
            {
                m_rootIndexBeingMarked = rootIndex;
                PointerInfo<NativeWord> pointerInfo = rootSet.GetRoot(rootIndex);
                Mark(pointerInfo, referrer: m_native.From(0));
            }
            m_rootIndexBeingMarked = -1;

            ProcessMarkStack();
            m_markStack = null;

            // Create a lookup structure for object indices from addresses, suitable for binary search.
            m_objectAddressToPostorderIndex = new ObjectAddressToPostorderIndexEntry[m_postorderEntries.Count - m_objectAddressToRootIndices.Count];
            m_numberOfLiveBytes = 0;
            int lookupEntryIndex = 0;
            for (int postorderIndex = 0; postorderIndex < m_postorderEntries.Count; postorderIndex++)
            {
                int typeIndex = m_postorderEntries[postorderIndex].TypeIndexOrRootSentinel;
                if (typeIndex != -1)
                {
                    ulong address = m_postorderEntries[postorderIndex].Address;
                    m_objectAddressToPostorderIndex[lookupEntryIndex].Address = address;
                    m_objectAddressToPostorderIndex[lookupEntryIndex].PostorderIndex = postorderIndex;
                    lookupEntryIndex++;

                    int objectSize = m_traceableHeap.GetObjectSize(m_native.From(address), typeIndex, committedOnly: true);
                    if (objectSize < 0)
                    {
                        // This indicates a memory corruption (array or string with invalid length field)
                        LogWarning(m_traceableHeap.TypeSystem.QualifiedName(typeIndex), $"object {postorderIndex} has negative size {objectSize}");
                    }
                    else
                    {
                        m_numberOfLiveBytes += objectSize;
                    }
                }
            }
            Array.Sort(m_objectAddressToPostorderIndex, (x, y) => x.Address.CompareTo(y.Address));
        }

        public IRootSet RootSet => m_rootSet;

        public int NumberOfPostorderNodes => m_postorderEntries.Count;

        public long NumberOfLiveBytes => m_numberOfLiveBytes;

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

        public List<(int rootIndex, PointerInfo<NativeWord> pointerFlags)> PostorderRootIndices(int postorderIndex)
        {
            return m_objectAddressToRootIndices[m_postorderEntries[postorderIndex].Address];
        }

        public void DescribeRootIndices(int postorderIndex, StringBuilder sb, IStructuredOutput output)
        {
            List<(int rootIndex, PointerInfo<NativeWord> pointerFlags)> rootInfos = PostorderRootIndices(postorderIndex);
            output.BeginArray("roots");
            sb.AppendFormat("roots#{0}{{", postorderIndex);
            for (int i = 0; i < rootInfos.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(", ");
                }
                output.BeginElement();
                sb.Append(m_rootSet.DescribeRoot(rootInfos[i].rootIndex, output, fullyQualified: true));
                output.EndElement();
            }
            sb.Append('}');
            output.EndArray();
        }

        public IEnumerable<string> TagsForAddress(NativeWord address)
        {
            if (m_tags.TryGetValue(address.Value, out SortedSet<string>? tags))
            {
                foreach (string tag in tags!)
                {
                    yield return tag;
                }
            }
        }

        public bool HasTag(int postorderIndex, string tag)
        {
            NativeWord address = PostorderAddress(postorderIndex);
            return m_tags.TryGetValue(address.Value, out SortedSet<string>? tags) && tags.Contains(tag);
        }

        void Mark(PointerInfo<NativeWord> pointerInfo, NativeWord referrer)
        {
            NativeWord reference = pointerInfo.Value;
            if ((pointerInfo.PointerFlags & PointerFlags.IsTagAnchor) != 0)
            {
                CheckTagSelector(referrer, pointerInfo);
            }

            if ((pointerInfo.PointerFlags & (PointerFlags.TagIfZero | PointerFlags.TagIfNonZero)) != 0)
            {
                CheckTag(referrer, pointerInfo);
            }

            if ((pointerInfo.PointerFlags & (PointerFlags.IsExternalReference | PointerFlags.Untraced)) != 0)
            {
                return;
            }

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
            entry.Address = address;
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
                foreach (PointerInfo<NativeWord> pointerInfo in m_traceableHeap.GetPointers(address, entry.TypeIndex))
                {
                    Mark(pointerInfo, address);
                }
            }
        }

        void CheckTagSelector(NativeWord address, PointerInfo<NativeWord> pointerInfo)
        {
            foreach ((NativeWord taggedObjectAddress, List<string> tags) in m_traceableHeap.GetTagsFromAnchor(m_logWarning, address, pointerInfo))
            {
                RecordTags(taggedObjectAddress, tags);
            }
        }

        void CheckTag(NativeWord address, PointerInfo<NativeWord> pointerInfo)
        {
            (List<string> zeroTags, List<string> nonZeroTags) = m_traceableHeap.TypeSystem.GetTags(pointerInfo.TypeIndex, pointerInfo.FieldNumber);
            if ((pointerInfo.PointerFlags & PointerFlags.TagIfZero) != 0 && pointerInfo.Value.Value == 0)
            {
                RecordTags(address, zeroTags);
            }

            if ((pointerInfo.PointerFlags & PointerFlags.TagIfNonZero) != 0 && pointerInfo.Value.Value != 0)
            {
                RecordTags(address, nonZeroTags);
            }
        }

        public void RecordTags(NativeWord address, List<string> tags)
        {
            if (!m_tags.TryGetValue(address.Value, out SortedSet<string>? objectTags))
            {
                objectTags = new();
                m_tags.Add(address.Value, objectTags);
            }

            foreach (string tag in tags)
            {
                objectTags.Add(tag);
            }
        }

        void LogWarning(string location, string message)
        {
            m_logger.Log(LOG_SOURCE, location, message);
        }
    }
}
