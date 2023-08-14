/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using System;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

namespace MemorySnapshotAnalyzer.UnityBackend
{
    sealed class ChunkedMemoryBlock : IMemoryAccessor
    {
        readonly string m_name;
        readonly MemoryMappedViewAccessor m_viewAccessor;
        readonly long m_chunkSizeInBytes;
        readonly long m_totalSizeInBytes;
        readonly long[] m_chunkPositions;

        internal ChunkedMemoryBlock(MemoryMappedViewAccessor viewAccessor, long position, string name)
        {
            m_name = name;
            m_viewAccessor = viewAccessor;

            Block block;
            m_viewAccessor.Read(position, out block);
            m_chunkSizeInBytes = block.ChunkSizeInBytes;
            m_totalSizeInBytes = block.TotalSizeInBytes;

            int numberOfChunks = (int)(block.TotalSizeInBytes / block.ChunkSizeInBytes);
            if (block.TotalSizeInBytes % block.ChunkSizeInBytes != 0)
            {
                numberOfChunks++;
            }
            m_chunkPositions = new long[numberOfChunks];
            m_viewAccessor.ReadArray(position + Marshal.SizeOf(typeof(Block)), m_chunkPositions, 0, m_chunkPositions.Length);

            // TODO: remove once done with decoding RTTI segments
            //Console.WriteLine("Chunked Memory Block: chunk size = {0}, number of chunks = {1}, total size = {2}",
            //    m_chunkSizeInBytes,
            //    m_chunkPositions.Length,
            //    m_totalSizeInBytes);
            //for (int i = 0; i < m_chunkPositions.Length; i++)
            //{
            //    Console.WriteLine("  Chunk {0}: position {1}", i, m_chunkPositions[i]);
            //    if (i >= 15)
            //    {
            //        break;
            //    }
            //}
        }

        internal long TotalSizeInBytes { get { return m_totalSizeInBytes; } }

        public void CheckRange(long offset, long size)
        {
            if (offset < 0 || size < 0 || offset + size > m_totalSizeInBytes)
            {
                throw new InvalidSnapshotFormatException(string.Format("accessing {0} past its end: offset {1}, size {2}", m_name, offset, size));
            }
        }

        public MemoryView GetRange(long offset, long size)
        {
            CheckRange(offset, size);
            return new MemoryView(this, offset, size);
        }

        public unsafe void Read<T>(long position, out T structure) where T : struct
        {
            CheckRange(position, Marshal.SizeOf(typeof(T)));

            int chunkIndex = (int)(position / m_chunkSizeInBytes);
            long chunkOffset = position % m_chunkSizeInBytes;
            if (chunkOffset + Marshal.SizeOf(typeof(T)) > m_chunkSizeInBytes)
            {
                // straddling read: copy into intermediate byte array before marshaling to target type
                var tempArray = ReadByteArray(chunkIndex, chunkOffset, Marshal.SizeOf(typeof(T)));
                fixed (byte* p = tempArray)
                {
#pragma warning disable CS8605 // Unboxing a possibly null value.
                    structure = (T)Marshal.PtrToStructure(new IntPtr(p), typeof(T));
#pragma warning restore CS8605 // Unboxing a possibly null value.
                }
            }
            else
            {
                m_viewAccessor.Read(m_chunkPositions[chunkIndex] + chunkOffset, out structure);
            }
        }

        byte[] ReadByteArray(int chunkIndex, long chunkOffset, int length)
        {
            var result = new byte[length];
            int bytesToRead = length;
            while (bytesToRead > 0)
            {
                int bytesAvailableInChunk = (int)(m_chunkSizeInBytes - chunkOffset);
                if (bytesAvailableInChunk > bytesToRead)
                {
                    bytesAvailableInChunk = bytesToRead;
                }
                m_viewAccessor.ReadArray(m_chunkPositions[chunkIndex] + chunkOffset, result, length - bytesToRead, bytesAvailableInChunk);
                chunkIndex++;
                chunkOffset = 0;
                bytesToRead -= bytesAvailableInChunk;
            }
            return result;
        }

        public override string ToString()
        {
            return m_name;
        }
    }
}
