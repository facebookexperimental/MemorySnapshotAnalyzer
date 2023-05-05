// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

namespace MemorySnapshotAnalyzer.UnityBackend
{
    abstract class ChapterObject
    {
        internal static ChapterObject Create(MemoryMappedViewAccessor viewAccessor, long position, ChunkedMemoryBlock[] memoryBlocks)
        {
            Chapter chapter;
            viewAccessor.Read(position, out chapter);
            switch (chapter.Format)
            {
                case ChapterFormat.Value:
                    return new ChapterValue(viewAccessor, position, memoryBlocks);
                case ChapterFormat.ArrayOfConstantSizeElements:
                    return new ChapterArrayOfConstantSizeElements(viewAccessor, position, memoryBlocks);
                case ChapterFormat.ArrayOfVariableSizeElements:
                    return new ChapterArrayOfVariableSizeElements(viewAccessor, position, memoryBlocks);
                default:
                    throw new InvalidSnapshotFormatException(string.Format("Invalid chapter format 0x{0:X}", (ushort)chapter.Format));
            }
        }
    }

    sealed class ChapterValue : ChapterObject
    {
        readonly MemoryView m_memoryView;

        internal ChapterValue(MemoryMappedViewAccessor viewAccessor, long position, ChunkedMemoryBlock[] memoryBlocks)
        {
            ValueChapter valueChapter;
            viewAccessor.Read(position + Marshal.SizeOf(typeof(Chapter)), out valueChapter);
            m_memoryView = memoryBlocks[valueChapter.BlockIndex].GetRange(valueChapter.PositionInBlock, valueChapter.ElementSizeInBytes);
        }

        internal MemoryView MemoryView { get { return m_memoryView; } }

        public override string ToString()
        {
            if (m_memoryView.Size == 4)
            {
                m_memoryView.Read(0, out uint value);
                return string.Format("value 0x{0:X08}", value);
            }
            else if (m_memoryView.Size == 8)
            {
                m_memoryView.Read(0, out ulong value);
                return string.Format("value 0x{0:X016}", value);
            }
            else
            {
                return string.Format("value of size {0}", m_memoryView.Size);
            }
        }
    }

    sealed class ChapterArrayOfConstantSizeElements : ChapterObject
    {
        readonly MemoryView m_arrayContents;
        readonly int m_elementSizeInBytes;
        readonly int m_length;

        internal ChapterArrayOfConstantSizeElements(MemoryMappedViewAccessor viewAccessor, long position, ChunkedMemoryBlock[] memoryBlocks)
        {
            ArrayOfConstantSizeElementsChapter valueArrayChapter;
            viewAccessor.Read(position + Marshal.SizeOf(typeof(Chapter)), out valueArrayChapter);
            long arraySizeInBytes = valueArrayChapter.ArrayLength * valueArrayChapter.ElementSizeInBytes;
            m_arrayContents = memoryBlocks[valueArrayChapter.BlockIndex].GetRange(0, arraySizeInBytes);
            m_elementSizeInBytes = valueArrayChapter.ElementSizeInBytes;
            m_length = valueArrayChapter.ArrayLength;
        }

        internal long Length { get { return m_length; } }

        internal MemoryView this[int index]
        {
            get
            {
                return m_arrayContents.GetElementAtIndex(index, m_elementSizeInBytes);
            }
        }

        public override string ToString()
        {
            return string.Format("array of length {0}, element size {1}", m_length, m_elementSizeInBytes);
        }
    }

    sealed class ChapterArrayOfVariableSizeElements : ChapterObject
    {
        readonly MemoryView[] m_elements;

        internal ChapterArrayOfVariableSizeElements(MemoryMappedViewAccessor viewAccessor, long position, ChunkedMemoryBlock[] memoryBlocks)
        {
            ArrayOfVariableSizeElementsChapter referenceArrayChapter;
            viewAccessor.Read(position + Marshal.SizeOf(typeof(Chapter)), out referenceArrayChapter);
            long afterReferenceArrayChapterPosition = position + Marshal.SizeOf(typeof(Chapter)) + Marshal.SizeOf(typeof(ArrayOfVariableSizeElementsChapter));

            m_elements = new MemoryView[referenceArrayChapter.ArrayLength];
            long lastElementPosition;
            viewAccessor.Read(afterReferenceArrayChapterPosition, out lastElementPosition);
            for (int i = 0; i < m_elements.Length; i++)
            {
                long nextElementPosition;
                viewAccessor.Read(afterReferenceArrayChapterPosition + (i + 1) * Marshal.SizeOf(typeof(long)), out nextElementPosition);
                m_elements[i] = memoryBlocks[referenceArrayChapter.BlockIndex].GetRange(lastElementPosition, nextElementPosition - lastElementPosition);
                lastElementPosition = nextElementPosition;
            }
        }

        internal long Length { get { return m_elements.Length; } }

        internal MemoryView this[int index]
        {
            get
            {
                return m_elements[index];
            }
        }

        public override string ToString()
        {
            return string.Format("array of length {0}, variable element size", m_elements.Length);
        }
    }
}
