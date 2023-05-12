// Copyright(c) Meta Platforms, Inc. and affiliates.

using System;

namespace MemorySnapshotAnalyzer.AbstractMemorySnapshot
{
    public abstract class MemorySnapshot : IDisposable
    {
        readonly TraceableHeap m_managedHeap;
        readonly TraceableHeap m_nativeHeap;

        protected MemorySnapshot(TraceableHeap managedHeap, TraceableHeap nativeHeap)
        {
            m_managedHeap = managedHeap;
            m_nativeHeap = nativeHeap;
        }

        public abstract void Dispose();

        public abstract string Filename { get; }

        public abstract string Format { get; }

        public abstract Native Native { get; }

        public TraceableHeap ManagedHeap => m_managedHeap;

        public TraceableHeap NativeHeap => m_nativeHeap;
    }
}
