// Copyright(c) Meta Platforms, Inc. and affiliates.

namespace MemorySnapshotAnalyzer.AbstractMemorySnapshot
{
    public enum PointerFlags
    {
        None = 0,
        IsOwningReference = 1 << 0,
        IsConditionalOwningReference = 1 << 1
    }
}
