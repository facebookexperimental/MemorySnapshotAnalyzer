// Copyright(c) Meta Platforms, Inc. and affiliates.

namespace MemorySnapshotAnalyzer.AbstractMemorySnapshot
{
    public sealed class HeapSegment
    {
        readonly NativeWord m_startAddress;
        readonly MemoryView m_memoryView;
        readonly bool m_isRuntimeTypeInformation;

        public HeapSegment(NativeWord startAddress, MemoryView memoryView, bool isRuntimeTypeInformation)
        {
            m_startAddress = startAddress;
            m_memoryView = memoryView;
            m_isRuntimeTypeInformation = isRuntimeTypeInformation;
        }

        public NativeWord StartAddress => m_startAddress;

        public long Size => m_memoryView.Size;

        public NativeWord EndAddress => m_startAddress + Size;

        public MemoryView MemoryView => m_memoryView;

        public bool IsRuntimeTypeInformation => m_isRuntimeTypeInformation;

        public override string ToString()
        {
            return string.Format("{0} segment at {1} ({2})",
                m_isRuntimeTypeInformation ? "runtime type information" : "managed heap",
                m_startAddress,
                m_memoryView);
        }
    }
}
