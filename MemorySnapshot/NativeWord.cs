using System;

namespace MemorySnapshotAnalyzer.AbstractMemorySnapshot
{
    public readonly struct NativeWord
    {
        readonly int m_size;
        readonly ulong m_value;

        public NativeWord(int size, ulong value)
        {
            m_size = size;
            m_value = size == 4 ? value & 0xffffffffUL : value;
        }

        public int Size => m_size;

        public ulong Value => m_value;

        static void CheckSizes(NativeWord left, NativeWord right)
        {
            if (left.Size != right.Size)
            {
                throw new ArgumentException("incompatible native word sizes");
            }
        }

        public static bool operator ==(NativeWord left, NativeWord right)
        {
            CheckSizes(left, right);
            return left.Value == right.Value;
        }

        public static bool operator !=(NativeWord left, NativeWord right)
        {
            CheckSizes(left, right);
            return left.Value != right.Value;
        }

        public static NativeWord operator *(int left, NativeWord right)
        {
            return new NativeWord(right.Size, (ulong)((ulong)left * right.Value));
        }

        public static NativeWord operator +(NativeWord left, long right)
        {
            return new NativeWord(left.Size, left.Value + (ulong)right);
        }

        public static NativeWord operator +(NativeWord left, NativeWord right)
        {
            CheckSizes(left, right);
            return new NativeWord(left.Size, left.Value + right.Value);
        }

        public static NativeWord operator -(NativeWord left, NativeWord right)
        {
            CheckSizes(left, right);
            return new NativeWord(left.Size, left.Value - right.Value);
        }

        public static bool operator <(NativeWord left, NativeWord right)
        {
            CheckSizes(left, right);
            return left.Value < right.Value;
        }

        public static bool operator <=(NativeWord left, NativeWord right)
        {
            CheckSizes(left, right);
            return left.Value <= right.Value;
        }

        public static bool operator >(NativeWord left, NativeWord right)
        {
            CheckSizes(left, right);
            return left.Value > right.Value;
        }

        public static bool operator >=(NativeWord left, NativeWord right)
        {
            CheckSizes(left, right);
            return left.Value >= right.Value;
        }

        public override string ToString()
        {
            if (m_size == 4)
            {
                return $"0x{m_value:X08}";
            }
            else
            {
                return $"0x{m_value:X016}";
            }
        }

        public override bool Equals(object? obj)
        {
            return obj != null && obj is NativeWord && this == (NativeWord)obj;
        }

        public override int GetHashCode()
        {
            return m_value.GetHashCode();
        }
    }
}
