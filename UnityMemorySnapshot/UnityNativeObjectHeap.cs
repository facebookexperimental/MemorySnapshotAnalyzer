// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using System;
using System.Collections.Generic;
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
        readonly NativeObject[] m_nativeObjects;
        readonly Dictionary<int, NativeObject> m_nativeObjectsByInstanceId;
        readonly Dictionary<ulong, NativeObject> m_nativeObjectsByAddress;
        readonly Dictionary<int, List<int>> m_connections;

        public override SegmentedHeap? SegmentedHeapOpt => null;

        public UnityNativeObjectHeap(
            UnityNativeObjectTypeSystem typeSystem,
            NativeObject[] nativeObjects,
            Dictionary<int, List<int>> connections,
            Native native)
            : base(typeSystem)
        {
            m_nativeObjects = nativeObjects;

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

        public override int NumberOfGCHandles => m_nativeObjects.Length;

        public override NativeWord GCHandleTarget(int gcHandleIndex)
        {
            // TODO: We are considering all objects to be reachable.
            // This is a hack to be able to compute backtraces for the native object heap.
            return m_nativeObjects[gcHandleIndex].ObjectAddress;
        }

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

        public override string GetObjectNodeType(NativeWord address)
        {
            return "native";
        }

        public override string? GetObjectName(NativeWord objectAddress)
        {
            return m_nativeObjectsByAddress[objectAddress.Value].Name;
        }

        public override IEnumerable<PointerInfo<NativeWord>> GetIntraHeapPointers(NativeWord address, int typeIndex)
        {
            int instanceId = m_nativeObjectsByAddress[address.Value].InstanceId;
            if (m_connections.TryGetValue(instanceId, out List<int>? successorInstanceIds))
            {
                foreach (int successorInstanceId in successorInstanceIds)
                {
                    yield return new PointerInfo<NativeWord>
                    {
                        Value = m_nativeObjectsByInstanceId[successorInstanceId].ObjectAddress,
                        PointerFlags = PointerFlags.None,
                        TypeIndex = -1,
                        FieldNumber = -1
                    };
                }
            }
        }

        public override IEnumerable<NativeWord> GetInterHeapPointers(NativeWord address, int typeIndex)
        {
            return Array.Empty<NativeWord>();
        }

        public override IEnumerable<(NativeWord childObjectAddress, NativeWord parentObjectAddress)> GetOwningReferencesFromAnchor(NativeWord anchorObjectAddress, PointerInfo<NativeWord> pointerInfo)
        {
            yield break;
        }

        public override int NumberOfObjectPairs => 0;

        public override bool ContainsAddress(NativeWord address)
        {
            return m_nativeObjectsByAddress.ContainsKey(address.Value);
        }

        public override string? DescribeAddress(NativeWord address)
        {
            return null;
        }
    }
}
