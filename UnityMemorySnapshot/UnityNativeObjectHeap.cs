// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using System.Diagnostics;

namespace MemorySnapshotAnalyzer.UnityBackend
{
    sealed class NativeRootReference
    {
        public ulong Id;
        public string? AreaNeame;
        public string? ObjectName;
        public long AccumulatedSize;
    }

    sealed class NativeObject
    {
        public int TypeIndex;
        public int HideFlags;
        public int InstanceId;
        public string? Name;
        public NativeWord ObjectAddress;
        public long ObjectSize;
        public ulong RootReferenceId;
    }

    sealed class UnityNativeObjectHeap : TraceableHeap
    {
        readonly Dictionary<ulong, NativeRootReference> m_rootReferences;
        readonly Dictionary<int, NativeObject> m_objectsByInstanceId;
        readonly Dictionary<int, List<int>> m_connections;

        public UnityNativeObjectHeap(
            UnityNativeObjectTypeSystem typeSystem,
            NativeRootReference[] rootReferences,
            NativeObject[] objects,
            Dictionary<int, List<int>> connections,
            Native native,
            ulong[] gcHandleTargets)
            : base(typeSystem, native, gcHandleTargets)
        {
            m_rootReferences = new Dictionary<ulong, NativeRootReference>();
            foreach (NativeRootReference reference in rootReferences)
            {
                Debug.Assert(!m_rootReferences.ContainsKey(reference.Id));
                m_rootReferences[reference.Id] = reference;
            }

            m_objectsByInstanceId = new Dictionary<int, NativeObject>();
            foreach (NativeObject obj in objects)
            {
                m_objectsByInstanceId[obj.InstanceId] = obj;
            }

            m_connections = connections;
        }

        public override int TryGetTypeIndex(NativeWord objectAddress)
        {
            throw new NotImplementedException();
        }

        public override int GetObjectSize(NativeWord objectAddress, int typeIndex, bool committedOnly)
        {
            throw new NotImplementedException();
        }

        public override IEnumerable<NativeWord> GetObjectPointers(NativeWord address, int typeIndex)
        {
            throw new NotImplementedException();
        }
    }
}
