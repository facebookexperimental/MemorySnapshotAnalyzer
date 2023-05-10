// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;

namespace MemorySnapshotAnalyzer.UnityBackend
{
    public class UnityMemorySnapshotLoader : MemorySnapshotLoader
    {
        public override MemorySnapshot? TryLoad(string filename)
        {
            var fileStream = new FileStream(filename, FileMode.Open, FileAccess.Read);
            var memorySnapshot = new UnityMemorySnapshot(filename, fileStream);
            if (memorySnapshot.CheckSignature())
            {
                memorySnapshot.Load();
                return memorySnapshot;
            }
            return null;
        }
    }
}
