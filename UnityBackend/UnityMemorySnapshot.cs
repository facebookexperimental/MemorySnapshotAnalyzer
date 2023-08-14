/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;

namespace MemorySnapshotAnalyzer.UnityBackend
{
    sealed class UnityMemorySnapshot : MemorySnapshot
    {
        readonly UnityMemorySnapshotFile m_unityMemorySnapshotFile;

        internal UnityMemorySnapshot(UnityMemorySnapshotFile unityMemorySnapshotFile)
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

        public override TraceableHeap ManagedHeap(ReferenceClassifierFactory referenceClassifierFactory)
        {
            return m_unityMemorySnapshotFile.ManagedHeap(referenceClassifierFactory);
        }

        public override TraceableHeap NativeHeap(ReferenceClassifierFactory referenceClassifierFactory)
        {
            return m_unityMemorySnapshotFile.NativeHeap(referenceClassifierFactory);
        }
    }
}
