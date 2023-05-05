namespace MemorySnapshotAnalyzer.UnityBackend
{
    /*
    Chapter Metadata_Version: value 0x000C
    Chapter Metadata_RecordDate: value 0x8DB348A37D69C80
    Chapter Metadata_UserMetadata: value of size 68
    Chapter Metadata_CaptureFlags: value 0x0007
    + Chapter Metadata_VirtualMachineInformation: value of size 24

    Chapter NativeTypes_Name: array of length 319, variable element size
    Chapter NativeTypes_NativeBaseTypeArrayIndex: array of length 319, element size 4

    Chapter NativeObjects_NativeTypeArrayIndex: array of length 175046, element size 4
    Chapter NativeObjects_HideFlags: array of length 175046, element size 4
    Chapter NativeObjects_Flags: array of length 175046, element size 4
    Chapter NativeObjects_InstanceId: array of length 175046, element size 4
    Chapter NativeObjects_Name: array of length 175046, variable element size
    Chapter NativeObjects_NativeObjectAddress: array of length 175046, element size 8
    Chapter NativeObjects_Size: array of length 175046, element size 8
    Chapter NativeObjects_RootReferenceId: array of length 175046, element size 8

    + Chapter GCHandles_Target: array of length 149370, element size 8

    Chapter Connections_From: array of length 175417, element size 4
    Chapter Connections_To: array of length 175417, element size 4

    + Chapter ManagedHeapSections_StartAddress: array of length 23309, element size 8
    + Chapter ManagedHeapSections_Bytes: array of length 23309, variable element size

    Chapter ManagedStacks_StartAddress:
    Chapter ManagedStacks_Bytes:

    + Chapter TypeDescriptions_Flags: array of length 117068, element size 4
    + Chapter TypeDescriptions_Name: array of length 117068, variable element size
    + Chapter TypeDescriptions_Assembly: array of length 117068, variable element size
    + Chapter TypeDescriptions_FieldIndices: array of length 117068, variable element size
    + Chapter TypeDescriptions_StaticFieldBytes: array of length 117068, variable element size
    + Chapter TypeDescriptions_BaseOrElementTypeIndex: array of length 117068, element size 4
    + Chapter TypeDescriptions_Size: array of length 117068, element size 4
    + Chapter TypeDescriptions_TypeInfoAddress: array of length 117068, element size 8
    + Chapter TypeDescriptions_TypeIndex: array of length 117068, element size 4

    + Chapter FieldDescriptions_Offset: array of length 311189, element size 4
    + Chapter FieldDescriptions_TypeIndex: array of length 311189, element size 4
    + Chapter FieldDescriptions_Name: array of length 311189, variable element size
    + Chapter FieldDescriptions_IsStatic: array of length 311189, element size 1

    Chapter NativeRootReferences_Id: array of length 175890, element size 8
    Chapter NativeRootReferences_AreaName: array of length 175890, variable element size
    Chapter NativeRootReferences_ObjectName: array of length 175890, variable element size
    Chapter NativeRootReferences_AccumulatedSize: array of length 175890, element size 8

    Chapter NativeAllocations_MemoryRegionIndex: array of length 1092389, element size 4
    Chapter NativeAllocations_RootReferenceId: array of length 1092389, element size 8
    Chapter NativeAllocations_AllocationSiteId: array of length 1092389, element size 8
    Chapter NativeAllocations_Address: array of length 1092389, element size 8
    Chapter NativeAllocations_Size: array of length 1092389, element size 8
    Chapter NativeAllocations_OverheadSize: array of length 1092389, element size 4
    Chapter NativeAllocations_PaddingSize: array of length 1092389, element size 4

    Chapter NativeMemoryRegions_Name: array of length 777, variable element size
    Chapter NativeMemoryRegions_ParentIndex: array of length 777, element size 4
    Chapter NativeMemoryRegions_AddressBase: array of length 777, element size 8
    Chapter NativeMemoryRegions_AddressSize: array of length 777, element size 8
    Chapter NativeMemoryRegions_FirstAllocationIndex: array of length 777, element size 4
    Chapter NativeMemoryRegions_NumAllocations: array of length 777, element size 4

    Chapter NativeMemoryLabels_Name: array of length 163, variable element size

    Chapter NativeAllocationSites_Id:
    Chapter NativeAllocationSites_MemoryLabelIndex:
    Chapter NativeAllocationSites_CallstackSymbols:

    Chapter NativeCallstackSymbol_Symbol:
    Chapter NativeCallstackSymbol_ReadableStackTrace:

    Chapter NativeObjects_GCHandleIndex: array of length 175046, element size 4

    Chapter 59: value of size 520
    Chapter 60: value of size 264
    Chapter 61: array of length 163, element size 8
    */

    public enum ChapterType : ushort
    {
        Metadata_Version,
        Metadata_RecordDate,
        Metadata_UserMetadata,
        Metadata_CaptureFlags,
        Metadata_VirtualMachineInformation,
        NativeTypes_Name,
        NativeTypes_NativeBaseTypeArrayIndex,
        NativeObjects_NativeTypeArrayIndex,
        NativeObjects_HideFlags,
        NativeObjects_Flags,
        NativeObjects_InstanceId,
        NativeObjects_Name,
        NativeObjects_NativeObjectAddress,
        NativeObjects_Size,
        NativeObjects_RootReferenceId,
        GCHandles_Target,
        Connections_From,
        Connections_To,
        ManagedHeapSections_StartAddress,
        ManagedHeapSections_Bytes,
        ManagedStacks_StartAddress,
        ManagedStacks_Bytes,
        TypeDescriptions_Flags,
        TypeDescriptions_Name,
        TypeDescriptions_Assembly,
        TypeDescriptions_FieldIndices,
        TypeDescriptions_StaticFieldBytes,
        TypeDescriptions_BaseOrElementTypeIndex,
        TypeDescriptions_Size,
        TypeDescriptions_TypeInfoAddress,
        TypeDescriptions_TypeIndex,
        FieldDescriptions_Offset,
        FieldDescriptions_TypeIndex,
        FieldDescriptions_Name,
        FieldDescriptions_IsStatic,
        NativeRootReferences_Id,
        NativeRootReferences_AreaName,
        NativeRootReferences_ObjectName,
        NativeRootReferences_AccumulatedSize,
        NativeAllocations_MemoryRegionIndex,
        NativeAllocations_RootReferenceId,
        NativeAllocations_AllocationSiteId,
        NativeAllocations_Address,
        NativeAllocations_Size,
        NativeAllocations_OverheadSize,
        NativeAllocations_PaddingSize,
        NativeMemoryRegions_Name,
        NativeMemoryRegions_ParentIndex,
        NativeMemoryRegions_AddressBase,
        NativeMemoryRegions_AddressSize,
        NativeMemoryRegions_FirstAllocationIndex,
        NativeMemoryRegions_NumAllocations,
        NativeMemoryLabels_Name,
        NativeAllocationSites_Id,
        NativeAllocationSites_MemoryLabelIndex,
        NativeAllocationSites_CallstackSymbols,
        NativeCallstackSymbol_Symbol,
        NativeCallstackSymbol_ReadableStackTrace,
        NativeObjects_GCHandleIndex,
    }
}
