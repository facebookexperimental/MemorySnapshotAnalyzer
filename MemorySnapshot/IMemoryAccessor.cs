namespace MemorySnapshotAnalyzer.AbstractMemorySnapshot
{
    public interface IMemoryAccessor
    {
        void CheckRange(long offset, long size);

        MemoryView GetRange(long offset, long size);

        void Read<T>(long position, out T sructure) where T : struct;
    }
}
