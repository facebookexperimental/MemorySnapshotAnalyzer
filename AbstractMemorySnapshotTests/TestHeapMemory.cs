/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using System;
using System.Runtime.InteropServices;

namespace MemorySnapshotAnalyzer.AbstractMemorySnapshotTests
{
    public sealed class TestHeapMemory : IMemoryAccessor
    {
        internal static readonly long ArraySizeOffsetInHeader = 8;

        private readonly ulong m_startAddress;
        private readonly byte[] m_contents;

        public TestHeapMemory(ulong startAddress, long size)
        {
            m_startAddress = startAddress;
            m_contents = new byte[size];
        }

        #region IMemoryAccessor implementation

        public void CheckRange(long offset, long size)
        {
            if (offset < 0 || size < 0 || offset + size > m_contents.Length)
            {
                throw new InvalidSnapshotFormatException(string.Format("accessing TestHeapMemory past its end: offset {0}, size {1}", offset, size));
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
            fixed (byte* p = &m_contents[position])
            {
#pragma warning disable CS8605 // Unboxing a possibly null value.
                structure = (T)Marshal.PtrToStructure(new IntPtr(p), typeof(T));
#pragma warning restore CS8605 // Unboxing a possibly null value.
            }
        }

        #endregion

        #region Test initialization methods

        public void WriteObjectHeader(ulong address, TestTypeIndex typeIndex)
        {
            Write(address, (ulong)typeIndex);
        }

        public void WriteArrayHeader(ulong address, TestTypeIndex typeIndex, int length)
        {
            Write(address, (ulong)typeIndex);
            Write(address + (ulong)ArraySizeOffsetInHeader, length);
        }

        public unsafe void Write<T>(ulong address, T value) where T : struct
        {
            long position = (long)address - (long)m_startAddress;
            CheckRange(position, Marshal.SizeOf(typeof(T)));
            fixed (byte* p = &m_contents[position])
            {
#pragma warning disable CS8605 // Unboxing a possibly null value.
                Marshal.StructureToPtr(value, new IntPtr(p), fDeleteOld: false);
#pragma warning restore CS8605 // Unboxing a possibly null value.
            }
        }

        #endregion
    }
}
