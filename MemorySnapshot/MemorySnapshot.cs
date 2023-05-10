// Copyright(c) Meta Platforms, Inc. and affiliates.

using System;

namespace MemorySnapshotAnalyzer.AbstractMemorySnapshot
{
    public abstract class MemorySnapshot : IDisposable
    {
        protected MemorySnapshot() {}

        public abstract void Dispose();

        public abstract string Filename { get; }

        public abstract string Format { get; }

        public abstract Native Native { get; }

        public abstract ManagedHeap ManagedHeap { get; }
    }
}
