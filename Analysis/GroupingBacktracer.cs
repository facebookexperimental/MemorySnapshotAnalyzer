using System;
using System.Collections.Generic;

namespace MemorySnapshotAnalyzer.Analysis
{
    public class GroupingBacktracer : IBacktracer
    {
        readonly IBacktracer m_parentBacktracer;
        readonly List<string> m_assemblyNames;
        readonly List<string> m_namespaceNames;
        readonly List<string> m_classNames;
        readonly int m_firstClassIndex;
        readonly int m_firstNamespaceIndex;
        readonly int m_firstAssemblyIndex;
        readonly int m_rootNodeIndex;
        readonly List<int> m_assemblyPredecessors;
        readonly List<int>[] m_namespacePredecessors;
        readonly List<int>[] m_classPredecessors;
        readonly List<int>[] m_rootPredecessors;

        sealed class TupleIntStringComparer : IEqualityComparer<Tuple<int, string>>
        {
            bool IEqualityComparer<Tuple<int, string>>.Equals(Tuple<int, string>? x, Tuple<int, string>? y)
            {
                return x!.Item1 == y!.Item1 && x.Item2 == y.Item2;
            }

            int IEqualityComparer<Tuple<int, string>>.GetHashCode(Tuple<int, string> obj)
            {
                return HashCode.Combine(obj.Item1, obj.Item2.GetHashCode());
            }
        }

        public GroupingBacktracer(IBacktracer parentBacktracer)
        {
            m_parentBacktracer = parentBacktracer;

            // We assign node indices in the following order:
            //   0 ... N-1 : nodes for parent backtracer excluding its root node
            //   N ... N+M-1 : nodes for M classes with static roots
            //   N+M ... N+M+O-1 : nodes for O namespaces with static roots, per assembly
            //   N+M+O ... N+M+O+P-1 : nodes for P assemblies with static roots
            //   N+M+O+P : GCHandles group
            //   N+M+O+P+1 : root node - the containing process
            m_firstClassIndex = m_parentBacktracer.RootNodeIndex;
            m_rootPredecessors = new List<int>[TracedHeap.RootSet.NumberOfRoots];

            var comparer = new TupleIntStringComparer();
            m_assemblyNames = new List<string>();
            var assemblyToIndex = new Dictionary<string, int>();
            m_namespaceNames = new List<string>();
            var namespaceToIndex = new Dictionary<Tuple<int, string>, int>(comparer);
            var namespaceIndexToAssemblyIndex = new List<int>();
            m_classNames = new List<string>();
            var classToIndex = new Dictionary<Tuple<int, string>, int>(comparer);
            var classIndexToNamespaceIndex = new List<int>();
            var rootIndexToClassIndex = new int[TracedHeap.RootSet.NumberOfRoots];
            for (int rootIndex = 0; rootIndex < rootIndexToClassIndex.Length; rootIndex++)
            {
                if (!TracedHeap.RootSet.IsGCHandle(rootIndex))
                {
                    IRootSet.StaticRootInfo info = TracedHeap.RootSet.GetStaticRootInfo(rootIndex);
                    if (!assemblyToIndex.TryGetValue(info.AssemblyName, out int assemblyIndex))
                    {
                        assemblyIndex = m_assemblyNames.Count;
                        m_assemblyNames.Add(info.AssemblyName);
                        assemblyToIndex.Add(info.AssemblyName, assemblyIndex);
                    }

                    var namespaceKey = Tuple.Create(assemblyIndex, info.NamespaceName);
                    if (!namespaceToIndex.TryGetValue(namespaceKey, out int namespaceIndex))
                    {
                        namespaceIndex = m_namespaceNames.Count;
                        m_namespaceNames.Add(info.NamespaceName);
                        namespaceIndexToAssemblyIndex.Add(assemblyIndex);
                        namespaceToIndex.Add(namespaceKey, namespaceIndex);
                    }

                    var classKey = Tuple.Create(namespaceIndex, info.ClassName);
                    if (!classToIndex.TryGetValue(classKey, out int classIndex))
                    {
                        classIndex = m_classNames.Count;
                        m_classNames.Add(info.ClassName);
                        classIndexToNamespaceIndex.Add(namespaceIndex);
                        classToIndex.Add(classKey, classIndex);
                    }

                    m_rootPredecessors[rootIndex] = new List<int>() { m_firstClassIndex + classIndex };
                }
            }

            m_firstNamespaceIndex = m_firstClassIndex + m_classNames.Count;
            m_firstAssemblyIndex = m_firstNamespaceIndex + m_namespaceNames.Count;
            m_rootNodeIndex = m_firstAssemblyIndex + m_assemblyNames.Count;

            m_assemblyPredecessors = new List<int>() { m_rootNodeIndex };

            m_namespacePredecessors = new List<int>[m_namespaceNames.Count];
            for (int i = 0; i < m_namespaceNames.Count; i++)
            {
                m_namespacePredecessors[i] = new List<int>() { m_firstAssemblyIndex + namespaceIndexToAssemblyIndex[i] };
            }

            m_classPredecessors = new List<int>[m_classNames.Count];
            for (int i = 0; i < m_classNames.Count; i++)
            {
                m_classPredecessors[i] = new List<int>() { m_firstNamespaceIndex + classIndexToNamespaceIndex[i] };
            }
        }

        public TracedHeap TracedHeap => m_parentBacktracer.TracedHeap;

        int IBacktracer.RootNodeIndex => m_rootNodeIndex;

        int IBacktracer.NumberOfNodes => m_rootNodeIndex + 1;

        bool IBacktracer.IsLiveObjectNode(int nodeIndex)
        {
            return m_parentBacktracer.IsLiveObjectNode(nodeIndex);
        }

        bool IBacktracer.IsRootSetNode(int nodeIndex)
        {
            return m_parentBacktracer.IsRootSetNode(nodeIndex);
        }

        bool IBacktracer.IsGCHandle(int nodeIndex)
        {
            return nodeIndex < m_firstClassIndex && m_parentBacktracer.IsGCHandle(nodeIndex);
        }

        int IBacktracer.NodeIndexToObjectIndex(int nodeIndex)
        {
            return m_parentBacktracer.NodeIndexToObjectIndex(nodeIndex);
        }

        int IBacktracer.NodeIndexToRootIndex(int nodeIndex)
        {
            return m_parentBacktracer.NodeIndexToRootIndex(nodeIndex);
        }

        int IBacktracer.ObjectIndexToNodeIndex(int objectIndex)
        {
            return m_parentBacktracer.ObjectIndexToNodeIndex(objectIndex);
        }

        string IBacktracer.DescribeNodeIndex(int nodeIndex, bool fullyQualified)
        {
            if (nodeIndex == m_rootNodeIndex)
            {
                return m_parentBacktracer.DescribeNodeIndex(m_parentBacktracer.RootNodeIndex, fullyQualified);
            }
            else if (nodeIndex >= m_firstAssemblyIndex)
            {
                return m_assemblyNames[nodeIndex - m_firstAssemblyIndex];
            }
            else if (nodeIndex >= m_firstNamespaceIndex)
            {
                int namespaceIndex = nodeIndex - m_firstNamespaceIndex;
                return m_namespaceNames[namespaceIndex];
            }
            else if (nodeIndex >= m_firstClassIndex)
            {
                int classIndex = nodeIndex - m_firstClassIndex;
                return m_classNames[classIndex];
            }
            else
            {
                return m_parentBacktracer.DescribeNodeIndex(nodeIndex, fullyQualified);
            }
        }

        string IBacktracer.NodeType(int nodeIndex)
        {
            if (nodeIndex == m_rootNodeIndex)
            {
                return m_parentBacktracer.NodeType(m_parentBacktracer.RootNodeIndex);
            }
            else if (nodeIndex >= m_firstClassIndex)
            {
                return "group";
            }
            else
            {
                return m_parentBacktracer.NodeType(nodeIndex);
            }
        }

        List<int> IBacktracer.Predecessors(int nodeIndex)
        {
            if (nodeIndex == m_rootNodeIndex)
            {
                return m_parentBacktracer.Predecessors(m_parentBacktracer.RootNodeIndex);
            }
            else if (nodeIndex >= m_firstAssemblyIndex)
            {
                return m_assemblyPredecessors;
            }
            else if (nodeIndex >= m_firstNamespaceIndex)
            {
                int namespaceIndex = nodeIndex - m_firstNamespaceIndex;
                return m_namespacePredecessors[namespaceIndex];
            }
            else if (nodeIndex >= m_firstClassIndex)
            {
                int classIndex = nodeIndex - m_firstClassIndex;
                return m_classPredecessors[classIndex];
            }
            else if (m_parentBacktracer.IsGCHandle(nodeIndex))
            {
                return m_assemblyPredecessors;
            }
            else if (m_parentBacktracer.IsRootSetNode(nodeIndex))
            {
                int rootIndex = m_parentBacktracer.NodeIndexToRootIndex(nodeIndex);
                return m_rootPredecessors[rootIndex];
            }
            else
            {
                return m_parentBacktracer.Predecessors(nodeIndex);
            }
        }
    }
}
