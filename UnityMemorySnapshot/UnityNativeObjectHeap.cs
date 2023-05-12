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
        readonly Dictionary<int, NativeObject> m_nativeObjectsByInstanceId;
        readonly Dictionary<ulong, NativeObject> m_nativeObjectsByAddress;
        readonly Dictionary<int, List<int>> m_connections;

        public override SegmentedHeap? SegmentedHeapOpt => null;

        public UnityNativeObjectHeap(
            UnityNativeObjectTypeSystem typeSystem,
            NativeObject[] nativeObjects,
            Dictionary<int, List<int>> connections,
            Native native,
            ulong[] gcHandleTargets)
            : base(typeSystem, native, gcHandleTargets)
        {
            m_nativeObjectsByInstanceId = new Dictionary<int, NativeObject>();
            m_nativeObjectsByAddress = new Dictionary<ulong, NativeObject>();
            foreach (NativeObject nativeObject in nativeObjects)
            {
                m_nativeObjectsByInstanceId[nativeObject.InstanceId] = nativeObject;
                m_nativeObjectsByAddress[nativeObject.ObjectAddress.Value] = nativeObject;
            }

            m_connections = connections;
        }

        public override string Description => "Unity native objects";

        public override int TryGetTypeIndex(NativeWord objectAddress)
        {
            if (m_nativeObjectsByAddress.TryGetValue(objectAddress.Value, out NativeObject? nativeObject))
            {
                return nativeObject!.TypeIndex;
            }
            return -1;
        }

        public override int GetObjectSize(NativeWord objectAddress, int typeIndex, bool committedOnly)
        {
            return checked((int)m_nativeObjectsByAddress[objectAddress.Value].ObjectSize);
        }

        public override string? GetObjectName(NativeWord objectAddress)
        {
            return m_nativeObjectsByAddress[objectAddress.Value].Name;
        }

        public override IEnumerable<NativeWord> GetObjectPointers(NativeWord address, int typeIndex)
        {
            int instanceId = m_nativeObjectsByAddress[address.Value].InstanceId;
            if (m_connections.TryGetValue(instanceId, out List<int>? successorInstanceIds))
            {
                foreach (int successorInstanceId in successorInstanceIds)
                {
                    yield return m_nativeObjectsByInstanceId[successorInstanceId].ObjectAddress;
                }
            }
        }

        public override string? DescribeAddress(NativeWord address)
        {
            return null;
        }
    }
}
