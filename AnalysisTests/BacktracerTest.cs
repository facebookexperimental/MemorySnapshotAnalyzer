/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using MemorySnapshotAnalyzer.Analysis;
using NUnit.Framework;
using System;

namespace MemorySnapshotAnalyzer.AnalysisTests
{
    [TestFixture]
    public sealed class BacktracerTest
    {
        MockTraceableHeap? m_traceableHeap;
        MemoryLogger? m_memoryLogger;
        RootSet? m_rootSet;
        TracedHeap? m_tracedHeap;

        [SetUp]
        public void SetUp()
        {
            m_traceableHeap = new MockTraceableHeap();
            m_memoryLogger = new MemoryLogger();
        }

        [TearDown]
        public void TearDown()
        {
            m_traceableHeap = null;
            m_memoryLogger = null;
            m_rootSet = null;
            m_tracedHeap = null;
        }

        Backtracer MakeBacktracer(Backtracer.Options options, int gcHandleWeight)
        {
            m_rootSet = new RootSet(m_traceableHeap!, gcHandleWeight);
            m_tracedHeap = new TracedHeap(m_rootSet!);
            return new Backtracer(m_tracedHeap, options, m_memoryLogger!);
        }

        [Test]
        public void TestBasic()
        {
            Backtracer backtracer = MakeBacktracer(Backtracer.Options.None, gcHandleWeight: 0);

            Assert.That(backtracer.TracedHeap, Is.EqualTo(m_tracedHeap));

            Assert.That(backtracer.NumberOfNodes, Is.EqualTo(5));

            int postorderIndex1 = m_tracedHeap!.ObjectAddressToPostorderIndex(m_traceableHeap!.Native.From(0x100));
            int postorderIndex2 = m_tracedHeap.ObjectAddressToPostorderIndex(m_traceableHeap.Native.From(0x200));
            int postorderIndex3 = m_tracedHeap.ObjectAddressToPostorderIndex(m_traceableHeap.Native.From(0x300));
            int gcHandle0PostorderIndex = m_tracedHeap.ObjectAddressToRootPostorderIndex(m_traceableHeap.Native.From(0x300));

            int nodeIndex1 = backtracer.PostorderIndexToNodeIndex(postorderIndex1);
            Assert.That(backtracer.NodeIndexToPostorderIndex(nodeIndex1), Is.EqualTo(postorderIndex1));
            int nodeIndex2 = backtracer.PostorderIndexToNodeIndex(postorderIndex2);
            int nodeIndex3 = backtracer.PostorderIndexToNodeIndex(postorderIndex3);
            int gcHandle0 = backtracer.PostorderIndexToNodeIndex(gcHandle0PostorderIndex);
            Assert.That(backtracer.NodeIndexToPostorderIndex(nodeIndex1), Is.EqualTo(postorderIndex1));
            Assert.That(backtracer.NodeIndexToPostorderIndex(backtracer.RootNodeIndex), Is.EqualTo(-1));

            Assert.That(backtracer.IsLiveObjectNode(nodeIndex1), Is.True);
            Assert.That(backtracer.IsLiveObjectNode(gcHandle0), Is.False);
            Assert.That(backtracer.IsLiveObjectNode(backtracer.RootNodeIndex), Is.False);

            Assert.That(backtracer.IsRootSentinel(nodeIndex1), Is.False);
            Assert.That(backtracer.IsRootSentinel(gcHandle0), Is.True);
            Assert.That(backtracer.IsRootSentinel(backtracer.RootNodeIndex), Is.False);

            Assert.That(backtracer.Predecessors(nodeIndex1), Is.EquivalentTo(new int[] { nodeIndex2 }));
            Assert.That(backtracer.Predecessors(nodeIndex2), Is.EquivalentTo(new int[] { nodeIndex3 }));
            Assert.That(backtracer.Predecessors(nodeIndex3), Is.EquivalentTo(new int[] { nodeIndex2, gcHandle0 }));
            Assert.That(backtracer.Predecessors(gcHandle0), Is.EquivalentTo(new int[] { backtracer.RootNodeIndex }));
            Assert.That(backtracer.Predecessors(backtracer.RootNodeIndex), Is.EquivalentTo(Array.Empty<int>()));

            // TODO: DescribeNodeIndex
            // TODO: NodeType
        }

        [Test]
        public void TestFuseRoots()
        {
            // TODO:
            Assert.Pass();
        }

        [Test]
        public void TestWeakGCHandles()
        {
            // TODO:
            Assert.Pass();
        }

        [Test]
        public void TestReferenceClassifiers()
        {
            // TODO: multiple references from same object, only one reported
            // TODO: normal then weak, weak is not reported
            // TODO: just weak is reported
            // TODO: weak then normal, just normal is reported
            // TODO: multiple owning at same weight, issue warning

            // TODO: IsOwned
            // TODO: IsWeak
            Assert.Pass();
        }
    }
}
