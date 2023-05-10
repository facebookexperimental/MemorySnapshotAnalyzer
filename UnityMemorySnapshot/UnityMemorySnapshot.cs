// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

namespace MemorySnapshotAnalyzer.UnityBackend
{
    sealed class UnityMemorySnapshot : MemorySnapshot
    {
        readonly FileStream m_fileStream;
        readonly string m_filename;
        readonly MemoryMappedFile m_memoryMappedFile;
        readonly MemoryMappedViewAccessor m_viewAccessor;
        ChapterObject[]? m_chapters;
        VirtualMachineInformation m_virtualMachineInformation;
        ManagedHeap? m_managedHeap;

        internal UnityMemorySnapshot(string filename, FileStream fileStream)
        {
            m_filename = filename;
            m_fileStream = fileStream;

            long fileSize = fileStream.Length;
            m_memoryMappedFile = MemoryMappedFile.CreateFromFile(fileStream, null, fileSize, MemoryMappedFileAccess.Read, HandleInheritability.None, leaveOpen: false);
            m_viewAccessor = m_memoryMappedFile.CreateViewAccessor(0, fileSize, MemoryMappedFileAccess.Read);
        }

        internal bool CheckSignature()
        {
            m_viewAccessor.Read(0, out Header header);
            return header.Signature == 0xAEABCDCD;
        }

        internal void Load()
        {
            m_chapters = ParseChapters();
            m_virtualMachineInformation = ParseVirtualMachineInformation();
            var typeSystem = new UnityTypeSystem(ParseTypeDescriptions(), ParseFieldDescriptions(), m_virtualMachineInformation);
            m_managedHeap = ParseManagedHeap(typeSystem);
        }

        ChapterObject[] ParseChapters()
        {
            // Footer
            m_viewAccessor.Read(m_fileStream.Length - Marshal.SizeOf(typeof(Footer)), out Footer footer);
            ExpectValue(footer.Signature, 0xABCDCDAE, "footer signature");

            // Directory
            m_viewAccessor.Read(footer.DirectoryPosition, out Directory directory);
            ExpectValue(directory.Signature, 0xCDCDAEAB, "directory signature");
            ExpectValue(directory.Version, 0x20170724, "chapter section version");
            long afterDirectoryPosition = footer.DirectoryPosition + Marshal.SizeOf(typeof(Directory));

            // Block Section
            m_viewAccessor.Read(directory.BlockSectionPosition, out BlockSection blockSection);
            ExpectValue(blockSection.Version, 0x20170724, "block section version");
            long afterBlockSectionPosition = directory.BlockSectionPosition + Marshal.SizeOf(typeof(BlockSection));

            // Blocks
            var memoryBlocks = new ChunkedMemoryBlock[blockSection.NumberOfBlocks];
            for (int i = 0; i < memoryBlocks.Length; i++)
            {
                long blockPosition;
                m_viewAccessor.Read(afterBlockSectionPosition + i * Marshal.SizeOf(typeof(long)), out blockPosition);
                memoryBlocks[i] = new ChunkedMemoryBlock(m_viewAccessor, blockPosition, string.Format("memory block {0}", i));
            }

            // Chapters
            var chapters = new ChapterObject[directory.NumberOfChapters];
            for (int i = 0; i < chapters.Length; i++)
            {
                long chapterPosition;
                m_viewAccessor.Read(afterDirectoryPosition + i * Marshal.SizeOf(typeof(long)), out chapterPosition);
                if (chapterPosition != 0)
                {
                    chapters[i] = ChapterObject.Create(m_viewAccessor, chapterPosition, memoryBlocks);
                }
            }

            return chapters;
        }

        T GetChapter<T>(ChapterType chapterIndex) where T : class
        {
            if ((int)chapterIndex >= m_chapters!.Length)
            {
                throw new InvalidSnapshotFormatException($"chapter {chapterIndex} missing");
            }
            var value = m_chapters[(int)chapterIndex] as T;
            if (value == null)
            {
                throw new InvalidSnapshotFormatException($"chapter {chapterIndex} has unexpected type; found {m_chapters[(int)chapterIndex]}");
            }
            return value;
        }

        ChapterArrayOfConstantSizeElements GetArrayOfConstantSizeElementsChapter(ChapterType chapterIndex, int expectedLength)
        {
            var array = GetChapter<ChapterArrayOfConstantSizeElements>(chapterIndex);
            if (array == null)
            {
                throw new InvalidSnapshotFormatException($"chapter {chapterIndex} missing");
            }
            if (array.Length != expectedLength)
            {
                throw new InvalidSnapshotFormatException("inconsistent chapter sizes");
            }
            return array;
        }

        ChapterArrayOfVariableSizeElements GetArrayOfVariableSizeElementsChapter(ChapterType chapterIndex, int expectedLength)
        {
            var array = GetChapter<ChapterArrayOfVariableSizeElements>(chapterIndex);
            if (array == null)
            {
                throw new InvalidSnapshotFormatException($"chapter {chapterIndex} missing");
            }
            if (array.Length != expectedLength)
            {
                throw new InvalidSnapshotFormatException("inconsistent chapter sizes");
            }
            return array;
        }

        VirtualMachineInformation ParseVirtualMachineInformation()
        {
            var value = GetChapter<ChapterValue>(ChapterType.Metadata_VirtualMachineInformation);
            VirtualMachineInformation virtualMachineInformation;
            value.MemoryView.Read(0, out virtualMachineInformation);
            return virtualMachineInformation;
        }

        ManagedHeap ParseManagedHeap(ITypeSystem typeSystem)
        {
            var startAddresses = GetChapter<ChapterArrayOfConstantSizeElements>(ChapterType.ManagedHeapSections_StartAddress);
            var contents = GetArrayOfVariableSizeElementsChapter(ChapterType.ManagedHeapSections_Bytes, (int)startAddresses.Length);

            var segments = new ManagedHeapSegment[startAddresses.Length];
            for (int i = 0; i < startAddresses.Length; i++)
            {
                ulong startAddress = startAddresses[i].ReadInteger();
                segments[i] = new ManagedHeapSegment(Native.From(startAddress & 0x7fffffffffffffff), contents[i], (startAddress >> 63) != 0);
            }

            // Unity memory profiler does not dump memory segments sorted by start address.
            Array.Sort(segments, (segment1, segment2) => segment1.StartAddress.Value.CompareTo(segment2.StartAddress.Value));

            var gcHandleTargets = ParseGCHandleTargets();
            return new UnityManagedHeap(typeSystem, Native, segments, gcHandleTargets);
        }

        TypeDescription[] ParseTypeDescriptions()
        {
            var flags = GetChapter<ChapterArrayOfConstantSizeElements>(ChapterType.TypeDescriptions_Flags);
            int count = (int)flags!.Length;

            var names = GetArrayOfVariableSizeElementsChapter(ChapterType.TypeDescriptions_Name, count);
            var assemblies = GetArrayOfVariableSizeElementsChapter(ChapterType.TypeDescriptions_Assembly, count);
            var fieldIndices = GetArrayOfVariableSizeElementsChapter(ChapterType.TypeDescriptions_FieldIndices, count);
            var staticFieldBytes = GetArrayOfVariableSizeElementsChapter(ChapterType.TypeDescriptions_StaticFieldBytes, count);
            var baseOrElementTypeIndices = GetArrayOfConstantSizeElementsChapter(ChapterType.TypeDescriptions_BaseOrElementTypeIndex, count);
            var sizes = GetArrayOfConstantSizeElementsChapter(ChapterType.TypeDescriptions_Size, count);
            var typeInfoAddresses = GetArrayOfConstantSizeElementsChapter(ChapterType.TypeDescriptions_TypeInfoAddress, count);
            var typeIndices = GetArrayOfConstantSizeElementsChapter(ChapterType.TypeDescriptions_TypeIndex, count);

            var typeDescriptions = new TypeDescription[count];
            for (int i = 0; i < count; i++)
            {
                typeDescriptions[i] = new TypeDescription();

                uint flagsValue;
                flags[i].Read(0, out flagsValue);
                typeDescriptions[i].Flags = (TypeFlags)flagsValue;

                typeDescriptions[i].Name = names[i].ReadString();
                typeDescriptions[i].Assembly = assemblies[i].ReadString();
                typeDescriptions[i].FieldIndices = fieldIndices[i];
                typeDescriptions[i].StaticFieldBytes = staticFieldBytes[i];
                baseOrElementTypeIndices[i].Read(0, out typeDescriptions[i].BaseOrElementTypeIndex);
                sizes[i].Read(0, out typeDescriptions[i].Size);
                typeDescriptions[i].TypeInfoAddress = Native.From(typeInfoAddresses[i].ReadInteger());
                typeIndices[i].Read(0, out typeDescriptions[i].TypeIndex);
            }

            return typeDescriptions;
        }

        FieldDescription[] ParseFieldDescriptions()
        {
            var offsets = GetChapter<ChapterArrayOfConstantSizeElements>(ChapterType.FieldDescriptions_Offset);
            int count = (int)offsets!.Length;

            var typeIndices = GetArrayOfConstantSizeElementsChapter(ChapterType.FieldDescriptions_TypeIndex, count);
            var names = GetArrayOfVariableSizeElementsChapter(ChapterType.FieldDescriptions_Name, count);
            var isStatics = GetArrayOfConstantSizeElementsChapter(ChapterType.FieldDescriptions_IsStatic, count);

            var fieldDescriptions = new FieldDescription[count];
            for (int i = 0; i < count; i++)
            {
                fieldDescriptions[i] = new FieldDescription();

                offsets[i].Read(0, out fieldDescriptions[i].Offset);
                typeIndices[i].Read(0, out fieldDescriptions[i].TypeIndex);
                fieldDescriptions[i].Name = names[i].ReadString();
                isStatics[i].Read(0, out byte isStatic);
                fieldDescriptions[i].IsStatic = isStatic != 0;
            }

            return fieldDescriptions;
        }

        ulong[] ParseGCHandleTargets()
        {
            var gcHandleTargets = GetChapter<ChapterArrayOfConstantSizeElements>(ChapterType.GCHandles_Target);
            var targets = new ulong[gcHandleTargets.Length];
            for (int i = 0; i < targets.Length; i++)
            {
                targets[i] = gcHandleTargets[i].ReadInteger();
            }
            return targets;
        }

        public override void Dispose()
        {
            m_memoryMappedFile.Dispose();
        }

        public override string Filename => m_filename;

        public override string Format => "Unity";

        public override Native Native => new Native(m_virtualMachineInformation.PointerSize);

        public override ManagedHeap ManagedHeap => m_managedHeap!;

        static void ExpectValue(uint value, uint expectedValue, string name)
        {
            if (value != expectedValue)
            {
                throw new InvalidSnapshotFormatException(string.Format("Invalid {0}; expected: 0x{1:X08}, found: 0x{2:X08}", name, value, expectedValue));
            }
        }
    }
}
