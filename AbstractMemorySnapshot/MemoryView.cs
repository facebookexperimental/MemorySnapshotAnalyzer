/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Runtime.InteropServices;
using System.Text;

namespace MemorySnapshotAnalyzer.AbstractMemorySnapshot
{
    public readonly struct MemoryView
    {
        readonly IMemoryAccessor m_memoryAccessor;
        readonly long m_offset;
        readonly long m_size;

        public MemoryView(IMemoryAccessor memoryAccessor, long offset, long size)
        {
            m_memoryAccessor = memoryAccessor;
            m_offset = offset;
            m_size = size;
        }

        public override string ToString()
        {
            return $"{m_memoryAccessor} offset {m_offset} size 0x{m_size:X} ({m_size})";
        }

        public long Size => m_size;

        public bool IsValid => m_size > 0;

        public MemoryView GetElementAtIndex(int index, int elementSizeInBytes)
        {
            if (index < 0 || elementSizeInBytes < 0)
            {
                throw new ArgumentException("negative index and/or element size not allowed");
            }

            return GetRange(index * elementSizeInBytes, elementSizeInBytes);
        }

        public void CheckRange(long offset, long size)
        {
            if (offset < 0 || size < 0 || offset + size > m_size)
            {
                throw new ArgumentException($"invalid memory range {m_memoryAccessor}: offset {offset}, size {size}");
            }

            m_memoryAccessor.CheckRange(m_offset + offset, size);
        }

        public MemoryView GetRange(long offset, long size)
        {
            CheckRange(offset, size);
            m_memoryAccessor.CheckRange(m_offset + offset, size);
            return new MemoryView(m_memoryAccessor, m_offset + offset, size);
        }

        public void Read<T>(long position, out T structure) where T : struct
        {
            CheckRange(position, Marshal.SizeOf(typeof(T)));
            m_memoryAccessor.Read(m_offset + position, out structure);
        }

        public NativeWord ReadNativeWord(long position, Native native)
        {
            if (native.Size == 4)
            {
                Read(position, out uint value);
                return native.From(value);
            }
            else
            {
                Read(position, out ulong value);
                return native.From(value);
            }
        }

        public NativeWord ReadPointer(long position, Native native)
        {
            return ReadNativeWord(position, native);
        }

        public ulong ReadInteger()
        {
            if (m_size == 4)
            {
                Read(0, out uint value);
                return value;
            }
            else if (m_size == 8)
            {
                Read(0, out ulong value);
                return value;
            }
            else
            {
                throw new InvalidSnapshotFormatException("memory view is not integer-sized");
            }
        }

        public string ReadString()
        {
            var sb = new StringBuilder((int)m_size);
            for (int i = 0; i < m_size; i++)
            {
                byte c;
                Read(i, out c);
                sb.Append((char)c);
            }
            return sb.ToString();
        }
    }
}
