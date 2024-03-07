/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Collections.Generic;

namespace MemorySnapshotAnalyzer.Analysis
{
    public sealed class HeapDom
    {
        readonly IBacktracer m_backtracer;
        readonly int m_rootNodeIndex;
        readonly int[] m_doms;
        readonly Dictionary<int, List<int>> m_domTree;
        readonly int m_numberOfNonLeafNodes;
        HeapDomSizes? m_defaultHeapDomSizes;

        public HeapDom(IBacktracer backtracer)
        {
            m_backtracer = backtracer;
            m_rootNodeIndex = m_backtracer.RootNodeIndex;

            m_doms = ComputeDominators();
            m_domTree = BuildDomTree(out m_numberOfNonLeafNodes);
        }

        public IBacktracer Backtracer => m_backtracer;

        public int RootNodeIndex => m_rootNodeIndex;

        public int NumberOfNonLeafNodes => m_numberOfNonLeafNodes;

        public int GetDominator(int nodeIndex)
        {
            return m_doms[nodeIndex];
        }

        public List<int>? GetChildren(int nodeIndex)
        {
            m_domTree.TryGetValue(nodeIndex, out List<int>? children);
            return children;
        }

        public HeapDomSizes DefaultHeapDomSizes
        {
            get
            {
                if (m_defaultHeapDomSizes == null)
                {
                    m_defaultHeapDomSizes = new HeapDomSizes(this, typeSet: null);
                }

                return m_defaultHeapDomSizes;
            }
        }

        int[] ComputeDominators()
        {
            // Engineered algorithm from https://www.cs.rice.edu/~keith/EMBED/dom.pdf

            // Given that indices are in postorder, the root node is the node with the highest index.
            int numberOfNodes = m_backtracer.NumberOfNodes;
            var doms = new int[numberOfNodes];
            int unreachableNodeIndex = m_backtracer.UnreachableNodeIndex;
            for (int i = 0; i < m_rootNodeIndex; i++)
            {
                doms[i] = unreachableNodeIndex;
            }
            doms[m_rootNodeIndex] = m_rootNodeIndex;

            bool changed = true;
            while (changed)
            {
                changed = false;
                // Note that Backtracer assigned node indices in postorder.
                for (int nodeIndex = m_rootNodeIndex - 1; nodeIndex >= 0; nodeIndex--)
                {
                    int newIdom = unreachableNodeIndex;
                    foreach (int predIndex in m_backtracer.Predecessors(nodeIndex))
                    {
                        if (doms[predIndex] != unreachableNodeIndex)
                        {
                            if (newIdom == unreachableNodeIndex)
                            {
                                newIdom = predIndex;
                            }
                            else
                            {
                                newIdom = Intersect(predIndex, newIdom, doms);
                            }
                        }
                    }

                    if (doms[nodeIndex] != newIdom)
                    {
                        doms[nodeIndex] = newIdom;
                        changed = true;
                    }
                }
            }

            return doms;
        }

        Dictionary<int, List<int>> BuildDomTree(out int numberOfNonLeafNodes)
        {
            m_doms[m_backtracer.UnreachableNodeIndex] = m_rootNodeIndex;

            var domTree = new Dictionary<int, List<int>>();
            numberOfNonLeafNodes = 0;
            for (int nodeIndex = 0; nodeIndex < m_rootNodeIndex; nodeIndex++)
            {
                int parentNodeIndex = m_doms[nodeIndex];
                if (domTree.TryGetValue(parentNodeIndex, out List<int>? children))
                {
                    children!.Add(nodeIndex);
                }
                else
                {
                    domTree.Add(parentNodeIndex, new List<int>() { nodeIndex });
                    numberOfNonLeafNodes++;
                }
            }

            return domTree;
        }

        static int Intersect(int finger1, int finger2, int[] doms)
        {
            while (finger1 != finger2)
            {
                while (finger1 < finger2)
                {
                    finger1 = doms[finger1];
                }
                while (finger2 < finger1)
                {
                    finger2 = doms[finger2];
                }
            }
            return finger1;
        }
    }
}
