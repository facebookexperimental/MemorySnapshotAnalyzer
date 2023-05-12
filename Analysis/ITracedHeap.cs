// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using System.Collections.Generic;
using System;

namespace MemorySnapshotAnalyzer.Analysis
{
    public interface ITracedHeap
    {
        IRootSet RootSet { get; }

        int NumberOfLiveObjects { get; }

        int NumberOfInvalidRoots { get; }

        int NumberOfInvalidPointers { get; }

        int NumberOfNonHeapRoots { get; }

        int NumberOfNonHeapPointers { get; }

        IEnumerable<Tuple<int, NativeWord>> GetInvalidRoots();

        IEnumerable<Tuple<NativeWord, NativeWord>> GetInvalidPointers();

        IEnumerable<Tuple<int, NativeWord>> GetNonHeapRoots();

        IEnumerable<Tuple<NativeWord, NativeWord>> GetNonHeapPointers();

        int GetNumberOfPredecessors(int objectIndex);

        int ObjectAddressToIndex(NativeWord address);

        // Note that this returns objects in postorder.
        NativeWord ObjectAddress(int objectIndex);

        int ObjectTypeIndex(int objectIndex);
    }
}
