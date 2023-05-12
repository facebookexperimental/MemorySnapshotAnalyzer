// Copyright(c) Meta Platforms, Inc. and affiliates.

using System;

namespace MemorySnapshotAnalyzer.AbstractMemorySnapshot
{
    public abstract class MemorySnapshot : IDisposable
    {
        readonly TraceableHeap[] m_traceableHeaps;

        protected MemorySnapshot(TraceableHeap[] traceableHeaps)
        {
            m_traceableHeaps = traceableHeaps;
        }

        public abstract void Dispose();

        public abstract string Filename { get; }

        public abstract string Format { get; }

        public abstract Native Native { get; }

        public int NumberOfTraceableHeaps => m_traceableHeaps.Length;

        public TraceableHeap GetTraceableHeap(int heapIndex)
        {
            return m_traceableHeaps[heapIndex];
        }
    }
}
