/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;

namespace MemorySnapshotAnalyzer.AbstractMemorySnapshot
{
    public abstract class SegmentedTraceableHeap : TraceableHeap
    {
        SegmentedHeap? m_segmentedHeap;

        protected SegmentedTraceableHeap(TypeSystem typeSystem) : base(typeSystem)
        {
        }

        protected void Init(SegmentedHeap segmentedHeap)
        {
            m_segmentedHeap = segmentedHeap;
        }

        public override string GetObjectNodeType(NativeWord address)
        {
            return "object";
        }

        public override string? GetObjectName(NativeWord objectAddress)
        {
            return null;
        }

        public override IEnumerable<PointerInfo<NativeWord>> GetPointers(NativeWord address, int typeIndex)
        {
            return m_segmentedHeap!.GetPointers(address, typeIndex);
        }

        public override IEnumerable<(NativeWord childObjectAddress, NativeWord parentObjectAddress, int weight)> GetWeightedReferencesFromAnchor(Action<string, string> logWarning, NativeWord anchorObjectAddress, PointerInfo<NativeWord> pointerInfo)
        {
            return m_segmentedHeap!.GetWeightedReferencesFromAnchor(logWarning, anchorObjectAddress, pointerInfo);
        }

        public override IEnumerable<(NativeWord objectAddress, List<string> tags)> GetTagsFromAnchor(Action<string, string> logWarning, NativeWord anchorObjectAddress, PointerInfo<NativeWord> pointerInfo)
        {
            return m_segmentedHeap!.GetTagsFromAnchor(logWarning, anchorObjectAddress, pointerInfo);
        }

        public override int NumberOfObjectPairs => 0;

        public override bool ContainsAddress(NativeWord address)
        {
            return m_segmentedHeap!.GetSegmentForAddress(address) != null;
        }

        public override SegmentedHeap? SegmentedHeapOpt => m_segmentedHeap;
    }
}
