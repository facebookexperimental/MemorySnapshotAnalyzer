/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using MemorySnapshotAnalyzer.CommandInfrastructure;
using System.Collections.Generic;

namespace MemorySnapshotAnalyzer.Commands
{
    public class WriteDotCommand : Command
    {
        public WriteDotCommand(Repl repl) : base(repl) { }

#pragma warning disable CS0649 // Field '...' is never assigned to, and will always have its default value
        [PositionalArgument(0, optional: false)]
        public NativeWord AddressOrIndex;

        [PositionalArgument(1, optional: false)]
        public string? OutputDotFilename;

        [NamedArgument("depth")]
        public int MaxDepth;

        [FlagArgument("fullyqualified")]
        public bool FullyQualified;
#pragma warning restore CS0649 // Field '...' is never assigned to, and will always have its default value

        int m_numberOfNodesOutput;
        int m_numberOfEdgesOutput;

        public override void Run()
        {
            int postorderIndex = Context.ResolveToPostorderIndex(AddressOrIndex);
            int nodeIndex = CurrentBacktracer.PostorderIndexToNodeIndex(postorderIndex);

            using (var fileOutput = new FileOutput(OutputDotFilename!, useUnixNewlines: false))
            using (RedirectOutput(new PassthroughStructuredOutput(fileOutput)))
            {
                DumpBacktracesToDot(nodeIndex);
            }

            Output.AddProperty("dotFilename", OutputDotFilename!);
            Output.AddProperty("numberOfNodesOutput", m_numberOfNodesOutput);
            Output.AddProperty("numberOfEdgesOutput", m_numberOfEdgesOutput);
            Output.AddDisplayStringLine("{0} nodes and {1} edges output",
                m_numberOfNodesOutput,
                m_numberOfEdgesOutput);
        }

        void DumpBacktracesToDot(int nodeIndex)
        {
            Output.AddDisplayStringLine("digraph BT {");
            DumpBacktracesToDot(nodeIndex, -1, new HashSet<int>(), 0);
            Output.AddDisplayStringLine("}");
        }

        void DumpBacktracesToDot(int nodeIndex, int optSuccessorNodeIndex, HashSet<int> seen, int depth)
        {
            if (optSuccessorNodeIndex != -1)
            {
                // declare the edge
                Output.AddDisplayStringLine("  n{0} -> n{1}",
                    nodeIndex,
                    optSuccessorNodeIndex);
                m_numberOfEdgesOutput++;
            }

            if (!seen.Contains(nodeIndex))
            {
                // declare the node to give it a recognizable label
                Output.AddDisplayStringLine("  n{0} [label=\"{1}\"]",
                    nodeIndex,
                    CurrentBacktracer.DescribeNodeIndex(nodeIndex, Output, FullyQualified));
                m_numberOfNodesOutput++;

                seen.Add(nodeIndex);

                if (MaxDepth <= 0 || depth < MaxDepth)
                {
                    foreach (int predIndex in CurrentBacktracer.Predecessors(nodeIndex))
                    {
                        DumpBacktracesToDot(predIndex, nodeIndex, seen, depth + 1);
                    }
                }
            }
        }

        public override string HelpText => "writedot <object address or index> <output dot filename> ['depth <max depth>] ['fullyqualified]";
    }
}
