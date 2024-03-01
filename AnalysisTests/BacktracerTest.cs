/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using MemorySnapshotAnalyzer.AbstractMemorySnapshotTests;
using MemorySnapshotAnalyzer.Analysis;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace MemorySnapshotAnalyzer.AnalysisTests
{
    [TestFixture]
    public sealed class BacktracerTest
    {
        MockTraceableHeap? m_traceableHeap;
        MemoryLogger? m_memoryLogger;
        RootSet? m_rootSet;
        TracedHeap? m_tracedHeap;
        Backtracer? m_backtracer;

        [SetUp]
        public void SetUp()
        {
            m_memoryLogger = new MemoryLogger();
        }

        [TearDown]
        public void TearDown()
        {
            m_traceableHeap = null;
            m_memoryLogger = null;
            m_rootSet = null;
            m_tracedHeap = null;
            m_backtracer = null;
        }

        Backtracer MakeBacktracer(MockTraceableHeap traceableHeap, int gcHandleWeight, bool fuseRoots)
        {
            m_traceableHeap = traceableHeap;
            m_rootSet = new RootSet(m_traceableHeap, gcHandleWeight);
            m_tracedHeap = new TracedHeap(m_rootSet!, m_memoryLogger!);
            m_backtracer = new Backtracer(m_tracedHeap, m_memoryLogger!, new HashSet<(int childPostorderIndex, int parentPostorderIndex)>(), fuseRoots);
            return m_backtracer;
        }

        int NodeIndex(NativeWord address)
        {
            int postorderIndex = m_tracedHeap!.ObjectAddressToPostorderIndex(address);
            int nodeIndex = m_backtracer!.PostorderIndexToNodeIndex(postorderIndex);
            return nodeIndex;
        }

        List<string> GetLog()
        {
            List<string> log = new();
            m_memoryLogger!.Flush(s => log.Add(s));
            return log;
        }

        sealed class BasicTraceableHeap : MockTraceableHeap
        {
            internal BasicTraceableHeap()
            {
                HeapObject leafObject = AddHeapObject(0x100, TestTypeIndex.Primitive, new());
                HeapObject innerObject1 = AddHeapObject(0x200, TestTypeIndex.ObjectTwoPointers, new()
                {
                    { 16, leafObject },
                    { 24, null }, // will be replaced by a pointer to innerObject2
                });
                HeapObject innerObject2 = AddHeapObject(0x300, TestTypeIndex.ObjectTwoPointers, new()
                {
                    { 16, innerObject1 },
                    { 24, null },
                });
                innerObject1.Fields[24] = innerObject2;

                AddGCHandle(innerObject2);
            }
        }

        [Test]
        public void TestBasic()
        {
            Backtracer backtracer = MakeBacktracer(new BasicTraceableHeap(), gcHandleWeight: 0, fuseRoots: false);
            IStructuredOutput output = new MockStructuredOutput();

            Assert.That(backtracer.TracedHeap, Is.EqualTo(m_tracedHeap));

            Assert.That(backtracer.NumberOfNodes, Is.EqualTo(6));

            int postorderIndex1 = m_tracedHeap!.ObjectAddressToPostorderIndex(m_traceableHeap!.Native.From(0x100));
            int postorderIndex2 = m_tracedHeap.ObjectAddressToPostorderIndex(m_traceableHeap.Native.From(0x200));
            int postorderIndex3 = m_tracedHeap.ObjectAddressToPostorderIndex(m_traceableHeap.Native.From(0x300));
            int gcHandle0PostorderIndex = m_tracedHeap.ObjectAddressToRootPostorderIndex(m_traceableHeap.Native.From(0x300));

            int nodeIndex1 = backtracer.PostorderIndexToNodeIndex(postorderIndex1);
            int nodeIndex2 = backtracer.PostorderIndexToNodeIndex(postorderIndex2);
            int nodeIndex3 = backtracer.PostorderIndexToNodeIndex(postorderIndex3);
            int gcHandle0 = backtracer.PostorderIndexToNodeIndex(gcHandle0PostorderIndex);

            Assert.Multiple(() =>
            {
                Assert.That(backtracer.NodeIndexToPostorderIndex(nodeIndex1), Is.EqualTo(postorderIndex1));
                Assert.That(backtracer.NodeIndexToPostorderIndex(backtracer.RootNodeIndex), Is.EqualTo(-1));
                Assert.That(backtracer.NodeIndexToPostorderIndex(backtracer.UnreachableNodeIndex), Is.EqualTo(-1));

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

                Assert.That(backtracer.DescribeNodeIndex(backtracer.RootNodeIndex, output, fullyQualified: true), Is.EqualTo("Process"));
                Assert.That(backtracer.DescribeNodeIndex(backtracer.RootNodeIndex, output, fullyQualified: true), Is.EqualTo("Process"));
                Assert.That(backtracer.DescribeNodeIndex(gcHandle0, output, fullyQualified: true), Is.EqualTo("GCHandle#0"));
                Assert.That(backtracer.DescribeNodeIndex(nodeIndex1, output, fullyQualified: true), Is.EqualTo("Test.Assembly:System.Int64#0"));
                Assert.That(backtracer.DescribeNodeIndex(nodeIndex1, output, fullyQualified: false), Is.EqualTo("Int64#0"));

                Assert.That(backtracer.NodeType(backtracer.RootNodeIndex), Is.EqualTo("root"));
                Assert.That(backtracer.NodeType(backtracer.UnreachableNodeIndex), Is.EqualTo("unreachable"));
                Assert.That(backtracer.NodeType(gcHandle0), Is.EqualTo("gchandle"));
                Assert.That(backtracer.NodeType(nodeIndex1), Is.EqualTo("box"));
                Assert.That(backtracer.NodeType(nodeIndex2), Is.EqualTo("object"));
            });

            // TODO: DescribeNodeIndex for object without native name, fully qualified and not
            // TODO: DescribeNodeIndex for object with name
            // TODO: DescribeNodeIndex for static root
            // TODO: DescribeNodeIndex for multiple roots referencing same object
            // TODO: NodeType for array
            // TODO: NodeType for static root
            // TODO: NodeType for multiple roots referencing same object where all are GC handles
            // TODO: NodeType for multiple roots referencing same object where one is static
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

        sealed class MultipleReferencesFromDifferentObjectsTraceableHeap : MockTraceableHeap
        {
            public readonly NativeWord Target;
            public readonly NativeWord Reference1;
            public readonly NativeWord Reference2;

            public MultipleReferencesFromDifferentObjectsTraceableHeap()
            {
                Target = Native.From(0x1000);
                Reference1 = Native.From(0x1100);
                Reference2 = Native.From(0x1200);

                HeapObject target = AddHeapObject(Target.Value, TestTypeIndex.ObjectNoPointers, new());
                HeapObject reference1 = AddHeapObject(Reference1.Value, TestTypeIndex.WeightedReferences, new()
                {
                    { 16, target }, // regular
                    { 24, null }, // strong
                    { 32, null }, // weak
                });
                HeapObject reference2 = AddHeapObject(Reference2.Value, TestTypeIndex.WeightedReferences, new()
                {
                    { 16, target }, // egular
                    { 24, null }, // strong
                    { 32, null }, // weak
                });
                HeapObject driver = AddHeapArray(0x1300, TestTypeIndex.ObjectArray, 2, new() {
                    { 32, reference1 },
                    { 40, reference2 },
                });

                AddGCHandle(driver);
            }
        }

        [Test]
        public void TestMultipleReferencesFromDifferentObjects()
        {
            // Scenario: multiple references from different objects, both are reported

            var traceableHeap = new MultipleReferencesFromDifferentObjectsTraceableHeap();
            Backtracer backtracer = MakeBacktracer(traceableHeap, gcHandleWeight: 0, fuseRoots: false);

            Assert.That(backtracer.Predecessors(NodeIndex(traceableHeap.Target)),
                Is.EquivalentTo(new int[] { NodeIndex(traceableHeap.Reference1), NodeIndex(traceableHeap.Reference2) }));
            Assert.That(backtracer.Weight(NodeIndex(traceableHeap.Target)), Is.EqualTo(0));
            Assert.That(GetLog(), Has.Exactly(0).Items);
        }

        sealed class MultipleReferencesFromSameObjectTraceableHeap : MockTraceableHeap
        {
            public readonly NativeWord Target;
            public readonly NativeWord Reference;

            public MultipleReferencesFromSameObjectTraceableHeap()
            {
                Target = Native.From(0x1000);
                Reference = Native.From(0x1100);

                HeapObject target = AddHeapObject(Target.Value, TestTypeIndex.ObjectNoPointers, new());
                HeapObject driver = AddHeapArray(Reference.Value, TestTypeIndex.ObjectArray, 2, new() {
                    { 32, target },
                    { 40, target },
                });

                AddGCHandle(driver);
            }
        }

        [Test]
        public void TestMultipleReferencesFromSameObject()
        {
            // Scenario: multiple references from same object, only one reported

            var traceableHeap = new MultipleReferencesFromSameObjectTraceableHeap();
            Backtracer backtracer = MakeBacktracer(traceableHeap, gcHandleWeight: 0, fuseRoots: false);

            Assert.That(backtracer.Predecessors(NodeIndex(traceableHeap.Target)),
                Is.EquivalentTo(new int[] { NodeIndex(traceableHeap.Reference) }));
            Assert.That(backtracer.Weight(NodeIndex(traceableHeap.Target)), Is.EqualTo(0));
            Assert.That(GetLog(), Has.Exactly(0).Items);
        }

        sealed class SubsequentWeakReferenceTraceableHeap : MockTraceableHeap
        {
            public readonly NativeWord Target;
            public readonly NativeWord Reference;

            public SubsequentWeakReferenceTraceableHeap()
            {
                Target = Native.From(0x1000);
                Reference = Native.From(0x1100);

                HeapObject target = AddHeapObject(Target.Value, TestTypeIndex.ObjectNoPointers, new());
                HeapObject reference1 = AddHeapObject(Reference.Value, TestTypeIndex.WeightedReferences, new()
                {
                    { 16, target }, // regular
                    { 24, null }, // strong
                    { 32, null }, // weak
                });
                HeapObject reference2 = AddHeapObject(0x1200, TestTypeIndex.WeightedReferences, new()
                {
                    { 16, null }, // regular
                    { 24, null }, // strong
                    { 32, target }, // weak
                });
                HeapObject driver = AddHeapArray(0x1300, TestTypeIndex.ObjectArray, 2, new() {
                    { 32, reference1 },
                    { 40, reference2 },
                });

                AddGCHandle(driver);
            }
        }

        [Test]
        public void TestSubsequentWeakReference()
        {
            // Scenario: weight 0 then weight -1, weight -1 is not reported

            var traceableHeap = new SubsequentWeakReferenceTraceableHeap();
            Backtracer backtracer = MakeBacktracer(traceableHeap, gcHandleWeight: 0, fuseRoots: false);

            Assert.That(backtracer.Predecessors(NodeIndex(traceableHeap.Target)),
                Is.EquivalentTo(new int[] { NodeIndex(traceableHeap.Reference) }));
            Assert.That(backtracer.Weight(NodeIndex(traceableHeap.Target)), Is.EqualTo(0));
            Assert.That(GetLog(), Has.Exactly(0).Items);
        }

        sealed class MultipleWeakReferencesTraceableHeap : MockTraceableHeap
        {
            public readonly NativeWord Target;
            public readonly NativeWord Reference1;
            public readonly NativeWord Reference2;

            public MultipleWeakReferencesTraceableHeap()
            {
                Target = Native.From(0x1000);
                Reference1 = Native.From(0x1100);
                Reference2 = Native.From(0x1200);

                HeapObject target = AddHeapObject(Target.Value, TestTypeIndex.ObjectNoPointers, new());
                HeapObject reference1 = AddHeapObject(Reference1.Value, TestTypeIndex.WeightedReferences, new()
                {
                    { 16, null }, // regular
                    { 24, null }, // strong
                    { 32, target }, // weak
                });
                HeapObject reference2 = AddHeapObject(Reference2.Value, TestTypeIndex.WeightedReferences, new()
                {
                    { 16, null }, // regular
                    { 24, null }, // strong
                    { 32, target }, // weak
                });
                HeapObject driver = AddHeapArray(0x1300, TestTypeIndex.ObjectArray, 2, new() {
                    { 32, reference1 },
                    { 40, reference2 },
                });

                AddGCHandle(driver);
            }
        }

        [Test]
        public void TestMultipleWeakReferences()
        {
            // Scenario: multiple references with weight -1, no warning

            var traceableHeap = new MultipleWeakReferencesTraceableHeap();
            Backtracer backtracer = MakeBacktracer(traceableHeap, gcHandleWeight: 0, fuseRoots: false);

            Assert.That(backtracer.Predecessors(NodeIndex(traceableHeap.Target)),
                Is.EquivalentTo(new int[] { NodeIndex(traceableHeap.Reference1), NodeIndex(traceableHeap.Reference2) }));
            Assert.That(backtracer.Weight(NodeIndex(traceableHeap.Target)), Is.EqualTo(-1));
            Assert.That(GetLog(), Has.Exactly(0).Items);
        }

        sealed class WeakThenRegularReferenceTraceableHeap : MockTraceableHeap
        {
            public readonly NativeWord Target;
            public readonly NativeWord Reference;

            public WeakThenRegularReferenceTraceableHeap()
            {
                Target = Native.From(0x1000);
                Reference = Native.From(0x1200);

                HeapObject target = AddHeapObject(Target.Value, TestTypeIndex.ObjectNoPointers, new());
                HeapObject reference1 = AddHeapObject(0x1100, TestTypeIndex.WeightedReferences, new()
                {
                    { 16, null }, // regular
                    { 24, null }, // strong
                    { 32, target }, // weak
                });
                HeapObject reference2 = AddHeapObject(Reference.Value, TestTypeIndex.WeightedReferences, new()
                {
                    { 16, target }, // regular
                    { 24, null }, // strong
                    { 32, null }, // weak
                });
                HeapObject driver = AddHeapArray(0x1300, TestTypeIndex.ObjectArray, 2, new() {
                    { 32, reference1 },
                    { 40, reference2 },
                });

                AddGCHandle(driver);
            }
        }

        [Test]
        public void TestWeakThenRegularReference()
        {
            // Scenario: weight -1 then weight 0, just weight 0 is reported

            var traceableHeap = new WeakThenRegularReferenceTraceableHeap();
            Backtracer backtracer = MakeBacktracer(traceableHeap, gcHandleWeight: 0, fuseRoots: false);

            Assert.That(backtracer.Predecessors(NodeIndex(traceableHeap.Target)),
                Is.EquivalentTo(new int[] { NodeIndex(traceableHeap.Reference) }));
            Assert.That(backtracer.Weight(NodeIndex(traceableHeap.Target)), Is.EqualTo(0));
            Assert.That(GetLog(), Has.Exactly(0).Items);
        }

        sealed class RegularThenStrongReferenceTraceableHeap : MockTraceableHeap
        {
            public readonly NativeWord Target;
            public readonly NativeWord Reference;

            public RegularThenStrongReferenceTraceableHeap()
            {
                Target = Native.From(0x1000);
                Reference = Native.From(0x1200);

                HeapObject target = AddHeapObject(Target.Value, TestTypeIndex.ObjectNoPointers, new());
                HeapObject reference1 = AddHeapObject(0x1100, TestTypeIndex.WeightedReferences, new()
                {
                    { 16, target }, // regular
                    { 24, null }, // strong
                    { 32, null }, // weak
                });
                HeapObject reference2 = AddHeapObject(Reference.Value, TestTypeIndex.WeightedReferences, new()
                {
                    { 16, null }, // regular
                    { 24, target }, // strong
                    { 32, null }, // weak
                });
                HeapObject driver = AddHeapArray(0x1300, TestTypeIndex.ObjectArray, 2, new() {
                    { 32, reference1 },
                    { 40, reference2 },
                });

                AddGCHandle(driver);
            }
        }

        [Test]
        public void TestRegularThenStrongReference()
        {
            // Scenario: weight 0 then weight 1, just weight 1 is reported

            var traceableHeap = new RegularThenStrongReferenceTraceableHeap();
            Backtracer backtracer = MakeBacktracer(traceableHeap, gcHandleWeight: 0, fuseRoots: false);

            Assert.That(backtracer.Predecessors(NodeIndex(traceableHeap.Target)),
                Is.EquivalentTo(new int[] { NodeIndex(traceableHeap.Reference) }));
            Assert.That(backtracer.Weight(NodeIndex(traceableHeap.Target)), Is.EqualTo(1));
            Assert.That(GetLog(), Has.Exactly(0).Items);
        }

        sealed class MultipleStrongReferencesTraceableHeap : MockTraceableHeap
        {
            public readonly NativeWord Target;
            public readonly NativeWord Reference1;
            public readonly NativeWord Reference2;

            public MultipleStrongReferencesTraceableHeap()
            {
                Target = Native.From(0x1000);
                Reference1 = Native.From(0x1100);
                Reference2 = Native.From(0x1200);

                HeapObject target = AddHeapObject(Target.Value, TestTypeIndex.ObjectNoPointers, new());
                HeapObject reference1 = AddHeapObject(Reference1.Value, TestTypeIndex.WeightedReferences, new()
                {
                    { 16, target }, // regular
                    { 24, target }, // strong
                    { 32, null }, // weak
                });
                HeapObject reference2 = AddHeapObject(Reference2.Value, TestTypeIndex.WeightedReferences, new()
                {
                    { 16, null }, // regular
                    { 24, target }, // strong
                    { 32, null }, // weak
                });
                HeapObject driver = AddHeapArray(0x1300, TestTypeIndex.ObjectArray, 2, new() {
                    { 32, reference1 },
                    { 40, reference2 },
                });

                AddGCHandle(driver);
            }
        }

        [Test]
        public void TestMultipleStrongReferences()
        {
            // Scenario: multiple references with weight 1, warning issued

            var traceableHeap = new MultipleStrongReferencesTraceableHeap();
            Backtracer backtracer = MakeBacktracer(traceableHeap, gcHandleWeight: 0, fuseRoots: false);

            Assert.That(backtracer.Predecessors(NodeIndex(traceableHeap.Target)),
                Is.EquivalentTo(new int[] { NodeIndex(traceableHeap.Reference1), NodeIndex(traceableHeap.Reference2) }));
            Assert.That(backtracer.Weight(NodeIndex(traceableHeap.Target)), Is.EqualTo(1));
            Assert.That(GetLog(), Has.Exactly(1).Items);
        }
    }
}
