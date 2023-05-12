// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;

namespace MemorySnapshotAnalyzer.UnityBackend
{
    sealed class UnityMemorySnapshot : MemorySnapshot
    {
        readonly UnityMemorySnapshotFile m_unityMemorySnapshotFile;

        internal UnityMemorySnapshot(
            UnityMemorySnapshotFile unityMemorySnapshotFile,
            TraceableHeap managedHeap,
            TraceableHeap nativeHeap) :
            base(managedHeap, nativeHeap)
        {
            m_unityMemorySnapshotFile = unityMemorySnapshotFile;
        }

        public override void Dispose()
        {
            m_unityMemorySnapshotFile.Dispose();
        }

        public override string Filename => m_unityMemorySnapshotFile.Filename;

        public override string Format => "Unity";

        public override Native Native => m_unityMemorySnapshotFile.Native;

    }
}
