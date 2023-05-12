// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;

namespace MemorySnapshotAnalyzer.UnityBackend
{
    public sealed class UnityMemorySnapshotLoader : MemorySnapshotLoader
    {
        public override MemorySnapshot? TryLoad(string filename)
        {
            var fileStream = new FileStream(filename, FileMode.Open, FileAccess.Read);
            var memorySnapshotFile = new UnityMemorySnapshotFile(filename, fileStream);
            if (memorySnapshotFile.CheckSignature())
            {
                return memorySnapshotFile.Load();
            }
            return null;
        }
    }
}
