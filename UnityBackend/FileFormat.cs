/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Runtime.InteropServices;

namespace MemorySnapshotAnalyzer.UnityBackend
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Header
    {
        public uint Signature;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Footer
    {
        public long DirectoryPosition;
        public uint Signature;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Directory
    {
        public uint Signature;
        public uint Version;
        public long BlockSectionPosition;
        public uint NumberOfChapters;
        // followed by array of chapter positions
    }

    public enum ChapterFormat : ushort
    {
        Value = 1,
        ArrayOfConstantSizeElements = 2,
        ArrayOfVariableSizeElements = 3,
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Chapter
    {
        public ChapterFormat Format;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ValueChapter
    {
        public int BlockIndex;
        public int ElementSizeInBytes;
        public long PositionInBlock;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ArrayOfConstantSizeElementsChapter
    {
        public int BlockIndex;
        public int ElementSizeInBytes;
        public int ArrayLength;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ArrayOfVariableSizeElementsChapter
    {
        public int BlockIndex;
        public int ArrayLength;
        // followed by array of element positions
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct BlockSection
    {
        public uint Version;
        public uint NumberOfBlocks;
        // followed by array of block positions
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Block
    {
        public long ChunkSizeInBytes;
        public long TotalSizeInBytes;
        // followed by array of chunk positions
    }

    //Pointer Size = 8
    //Object Header Size = 16
    //Array Header Size = 32
    //Array Bounds Offset In Header = 16
    //Array Size Offset In Header = 24
    //Allocation Granularity = 16
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct VirtualMachineInformation
    {
        public int PointerSize;
        public int ObjectHeaderSize;
        public int ArrayHeaderSize;
        public int ArrayBoundsOffsetInHeader;
        public int ArraySizeOffsetInHeader;
        public int AllocationGranularity;
    }
}
