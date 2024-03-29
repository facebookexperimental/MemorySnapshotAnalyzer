﻿/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

namespace MemorySnapshotAnalyzer.UnityBackend
{
    sealed class UnityMemorySnapshotFile : IDisposable
    {
        readonly string m_filename;
        readonly FileStream m_fileStream;
        readonly long m_fileSize;
        readonly MemoryMappedFile m_memoryMappedFile;
        readonly MemoryMappedViewAccessor m_viewAccessor;

        ChapterObject[]? m_chapters;
        VirtualMachineInformation m_virtualMachineInformation;
        TypeDescription[]? m_typeDescriptions;
        FieldDescription[]? m_fieldDescriptions;
        HeapSegment[]? m_managedHeapSegments;
        NativeType[]? m_nativeTypes;
        ulong[]? m_gcHandleTargets;

        internal UnityMemorySnapshotFile(string filename, FileStream fileStream)
        {
            m_filename = filename;
            m_fileStream = fileStream;

            m_fileSize = fileStream.Length;
            m_memoryMappedFile = MemoryMappedFile.CreateFromFile(fileStream, null, m_fileSize, MemoryMappedFileAccess.Read, HandleInheritability.None, leaveOpen: false);
            m_viewAccessor = m_memoryMappedFile.CreateViewAccessor(0, m_fileSize, MemoryMappedFileAccess.Read);
        }

        internal string Filename => m_filename;

        internal Native Native => new Native(m_virtualMachineInformation.PointerSize);

        public void Dispose()
        {
            m_viewAccessor.Dispose();
            m_memoryMappedFile.Dispose();
            m_fileStream.Dispose();
        }

        internal bool CheckSignature()
        {
            m_viewAccessor.Read(0, out Header header);
            return header.Signature == 0xAEABCDCD;
        }

        internal UnityMemorySnapshot Load()
        {
            m_chapters = ParseChapters();
            m_virtualMachineInformation = ParseVirtualMachineInformation();

            m_typeDescriptions = ParseTypeDescriptions();
            m_fieldDescriptions = ParseFieldDescriptions();
            m_managedHeapSegments = ParseManagedHeapSegments();
            m_gcHandleTargets = ParseGCHandleTargets();
            m_nativeTypes = ParseNativeTypes();

            return new UnityMemorySnapshot(this);
        }

        public UnityManagedHeap ManagedHeap(ReferenceClassifierFactory referenceClassifierFactory)
        {
            var typeSystem = new UnityManagedTypeSystem(m_typeDescriptions!, m_fieldDescriptions!, m_virtualMachineInformation, referenceClassifierFactory);
            return new UnityManagedHeap(typeSystem, m_managedHeapSegments!, m_gcHandleTargets!);
        }

        public UnityNativeObjectHeap NativeHeap(ReferenceClassifierFactory referenceClassifierFactory)
        {
            var nativeObjectTypeSystem = new UnityNativeObjectTypeSystem(m_virtualMachineInformation.PointerSize, m_nativeTypes!, referenceClassifierFactory);
            return ParseNativeObjectHeap(nativeObjectTypeSystem);
        }

        ChapterObject[] ParseChapters()
        {
            // Footer
            m_viewAccessor.Read(m_fileSize - Marshal.SizeOf(typeof(Footer)), out Footer footer);
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

        HeapSegment[] ParseManagedHeapSegments()
        {
            var startAddresses = GetChapter<ChapterArrayOfConstantSizeElements>(ChapterType.ManagedHeapSections_StartAddress);
            var contents = GetArrayOfVariableSizeElementsChapter(ChapterType.ManagedHeapSections_Bytes, (int)startAddresses.Length);

            var segments = new HeapSegment[startAddresses.Length];
            for (int i = 0; i < startAddresses.Length; i++)
            {
                ulong startAddress = startAddresses[i].ReadInteger();
                segments[i] = new HeapSegment(Native.From(startAddress & 0x7fffffffffffffff), contents[i], (startAddress >> 63) != 0);
            }

            // Unity memory profiler does not dump memory segments sorted by start address.
            Array.Sort(segments, (segment1, segment2) => segment1.StartAddress.Value.CompareTo(segment2.StartAddress.Value));

            return segments;
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

        NativeType[] ParseNativeTypes()
        {
            var names = GetChapter<ChapterArrayOfVariableSizeElements>(ChapterType.NativeTypes_Name);
            int count = (int)names!.Length;

            var baseTypeIndices = GetArrayOfConstantSizeElementsChapter(ChapterType.NativeTypes_NativeBaseTypeArrayIndex, count);

            var nativeTypes = new NativeType[count];
            for (int i = 0; i < count; i++)
            {
                nativeTypes[i] = new NativeType();

                nativeTypes[i].Name = names[i].ReadString();
                baseTypeIndices[i].Read(0, out nativeTypes[i].BaseTypeIndex);
            }

            return nativeTypes;
        }

        UnityNativeObjectHeap ParseNativeObjectHeap(UnityNativeObjectTypeSystem typeSystem)
        {
            NativeObject[] nativeObjects = ParseNativeObjects();

            return new UnityNativeObjectHeap(
                typeSystem,
                nativeObjects,
                ParseConnections(),
                Native);
        }

        NativeRootReference[]? ParseNativeRootReferences()
        {
            var ids = GetChapter<ChapterArrayOfConstantSizeElements>(ChapterType.NativeRootReferences_Id);
            if (ids == null)
            {
                return null;
            }

            int count = (int)ids.Length;

            var areaNames = GetArrayOfVariableSizeElementsChapter(ChapterType.NativeRootReferences_AreaName, count);
            var objectNames = GetArrayOfVariableSizeElementsChapter(ChapterType.NativeRootReferences_ObjectName, count);
            var accumulatedSizes = GetArrayOfConstantSizeElementsChapter(ChapterType.NativeRootReferences_AccumulatedSize, count);

            var nativeRootReferences = new NativeRootReference[count];
            for (int i = 0; i < count; i++)
            {
                nativeRootReferences[i] = new NativeRootReference();

                ids[i].Read(0, out nativeRootReferences[i].Id);
                nativeRootReferences[i].AreaNeame = areaNames[i].ReadString();
                nativeRootReferences[i].ObjectName = objectNames[i].ReadString();
                accumulatedSizes[i].Read(0, out nativeRootReferences[i].AccumulatedSize);
            }

            return nativeRootReferences;
        }

        NativeObject[] ParseNativeObjects()
        {
            var typeIndices = GetChapter<ChapterArrayOfConstantSizeElements>(ChapterType.NativeObjects_NativeTypeArrayIndex);
            int count = (int)typeIndices!.Length;

            var instanceIds = GetArrayOfConstantSizeElementsChapter(ChapterType.NativeObjects_InstanceId, count);
            var names = GetArrayOfVariableSizeElementsChapter(ChapterType.NativeObjects_Name, count);
            var objectAddresses = GetArrayOfConstantSizeElementsChapter(ChapterType.NativeObjects_NativeObjectAddress, count);
            var objectSizes = GetArrayOfConstantSizeElementsChapter(ChapterType.NativeObjects_Size, count);
            var rootReferenceIds = GetArrayOfConstantSizeElementsChapter(ChapterType.NativeObjects_RootReferenceId, count);

            var nativeObjects = new NativeObject[count];
            for (int i = 0; i < count; i++)
            {
                nativeObjects[i] = new NativeObject();

                typeIndices[i].Read(0, out nativeObjects[i].TypeIndex);
                instanceIds[i].Read(0, out nativeObjects[i].InstanceId);
                nativeObjects[i].Name = names[i].ReadString();
                nativeObjects[i].ObjectAddress = Native.From(objectAddresses[i].ReadInteger());
                objectSizes[i].Read(0, out nativeObjects[i].ObjectSize);
                rootReferenceIds[i].Read(0, out nativeObjects[i].RootReferenceId);
            }

            return nativeObjects;
        }

        Dictionary<int, List<int>> ParseConnections()
        {
            var from = GetChapter<ChapterArrayOfConstantSizeElements>(ChapterType.Connections_From);
            int count = (int)from!.Length;

            var to = GetArrayOfConstantSizeElementsChapter(ChapterType.Connections_To, count);

            var connections = new Dictionary<int, List<int>>();
            for (int i = 0; i < count; i++)
            {
                from[i].Read(0, out int fromIndex);
                to[i].Read(0, out int toIndex);
                if (connections.TryGetValue(fromIndex, out List<int>? tos))
                {
                    tos!.Add(toIndex);
                }
                else
                {
                    connections[fromIndex] = new List<int> { toIndex };
                }
            }

            return connections;
        }

        static void ExpectValue(uint value, uint expectedValue, string name)
        {
            if (value != expectedValue)
            {
                throw new InvalidSnapshotFormatException(string.Format("Invalid {0}; expected: 0x{1:X08}, found: 0x{2:X08}", name, value, expectedValue));
            }
        }
    }
}
