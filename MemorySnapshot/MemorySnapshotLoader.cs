// Copyright(c) Meta Platforms, Inc. and affiliates.

namespace MemorySnapshotAnalyzer.AbstractMemorySnapshot
{
    public abstract class MemorySnapshotLoader
    {
        public abstract MemorySnapshot? TryLoad(string filename);
    }
}
