/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using MemorySnapshotAnalyzer.Analysis;
using MemorySnapshotAnalyzer.CommandInfrastructure;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace MemorySnapshotAnalyzer.Commands
{
    public class HeapDomCommand : Command
    {
        public HeapDomCommand(Repl repl) : base(repl) {}

#pragma warning disable CS0649 // Field '...' is never assigned to, and will always have its default value
        [PositionalArgument(0, optional: false)]
        public string? OutputFilename;

        [NamedArgument("relativeto")]
        public int OtherContextId = -1;

        // In Chrome, we get an error if the data structure is too deep.
        [NamedArgument("depth")]
        public int MaxDepth = 128;

        [NamedArgument("width")]
        public int MaxWidth;

        [NamedArgument("minsize")]
        public int MinSize;

        [FlagArgument("objectsonly")]
        public bool ToplevelObjectsOnly;

        [FlagArgument("nonleaves")]
        public bool NonLeafNodesOnly;

        [FlagArgument("elideunchanged")]
        public bool ElideUnchangedSubtrees;

        [FlagArgument("nodetype")]
        public bool NodeTypes;

        [FlagArgument("start")]
        public bool StartBrowser = true;

        [NamedArgument("type")]
        public CommandLineArgument? TypeIndexOrPattern;

        [FlagArgument("includederived")]
        public bool IncludeDerived;
#pragma warning restore CS0649 // Field '...' is never assigned to, and will always have its default value

        enum Diff
        {
            Incomparable = 0,
            New = 1,
            Modified = 2,
            Same = 3,
        }

        Dictionary<int, Diff>? m_diffTree;
        int m_numberOfNodesWritten;

        public override void Run()
        {
            HeapDomSizes heapDomSizes = MakeHeapDomSizes(TypeIndexOrPattern, IncludeDerived);

            if (OtherContextId != -1)
            {
                Context? otherContext = Repl.GetContext(OtherContextId);
                if (otherContext == null || otherContext.CurrentMemorySnapshot == null)
                {
                    throw new CommandException("nonexistent context");
                }

                otherContext.EnsureTracedHeap();
                m_diffTree = new Dictionary<int, Diff>();
                _ = ComputeDiffTree(CurrentHeapDom.RootNodeIndex, otherContext);
            }

            m_numberOfNodesWritten = 0;
            try
            {
                string installDirName = AppDomain.CurrentDomain.BaseDirectory;
                string htmlSource = Path.Combine(installDirName, "treemap.html");
                string[] lines = File.ReadAllLines(htmlSource);

                Regex re = new(Regex.Escape("<script src=\"data.js\"></script>"), RegexOptions.Compiled);

                using (var fileOutput = new FileOutput(OutputFilename!, useUnixNewlines: true))
                using (RedirectOutput(new PassthroughStructuredOutput(fileOutput)))
                {
                    foreach (string line in lines)
                    {
                        if (re.IsMatch(line))
                        {
                            Output.AddDisplayString("<script>data=");
                            DumpTree(heapDomSizes);
                            Output.AddDisplayStringLine("</script>");
                        }
                        else
                        {
                            Output.AddDisplayStringLine(line);
                        }
                    }
                }

                if (StartBrowser)
                {
                    StartHtmlFile(OutputFilename!);
                }
            }
            catch (IOException ex)
            {
                throw new CommandException(ex.Message);
            }

            long rootNodeInclusiveSize = heapDomSizes.TreeSize(CurrentHeapDom.RootNodeIndex);
            Output.AddProperty("numberOfNodesWritten", m_numberOfNodesWritten);
            Output.AddProperty("numberOfBytesInNodesWritten", rootNodeInclusiveSize);
            Output.AddDisplayStringLine("wrote {0} nodes with a total of {1} bytes",
                m_numberOfNodesWritten,
                rootNodeInclusiveSize);
        }

        void StartHtmlFile(string htmlDestination)
        {
            Process process = new()
            {
                StartInfo = {
                    UseShellExecute = true,
                    FileName = htmlDestination
                }
            };
            process.Start();
        }

        Diff ComputeDiffTree(int nodeIndex, Context previousContext)
        {
            Diff diff;
            if (CurrentBacktracer.IsLiveObjectNode(nodeIndex))
            {
                int postorderIndex = CurrentBacktracer.NodeIndexToPostorderIndex(nodeIndex);
                NativeWord objectAddress = CurrentTracedHeap.PostorderAddress(postorderIndex);
                // TODO: this only works with non-relocating garbage collectors.
                int previousPostorderIndex = previousContext!.CurrentTracedHeap!.ObjectAddressToPostorderIndex(objectAddress);
                if (previousPostorderIndex == -1)
                {
                    diff = Diff.New;
                }
                else
                {
                    int typeIndex = previousContext.CurrentTracedHeap.PostorderTypeIndexOrSentinel(previousPostorderIndex);
                    if (typeIndex != CurrentTracedHeap.PostorderTypeIndexOrSentinel(postorderIndex))
                    {
                        diff = Diff.Same;
                    }
                    else
                    {
                        diff = Diff.New;
                    }
                }
            }
            else
            {
                diff = Diff.Incomparable;
            }

            List<int>? children = CurrentHeapDom.GetChildren(nodeIndex);
            int numberOfChildren = children == null ? 0 : children.Count;
            for (int i = 0; i < numberOfChildren; i++)
            {
                var childDiff = ComputeDiffTree(children![i], previousContext);
                if (diff == Diff.Same && childDiff != Diff.Incomparable && childDiff != Diff.Same)
                {
                    diff = Diff.Modified;
                }
            }

            m_diffTree!.Add(nodeIndex, diff);
            return diff;
        }

        void DumpTree(HeapDomSizes heapDomSizes)
        {
            IComparer<int> sizeComparer = heapDomSizes.MakeComparer();
            _ = DumpTree(CurrentHeapDom.RootNodeIndex, heapDomSizes, sizeComparer, false, 0, out _);
        }

        long DumpTree(int nodeIndex, HeapDomSizes heapDomSizes, IComparer<int> comparer, bool needComma, int depth, out int numberOfElidedNodes)
        {
            // TODO: better limiting functions:
            // - specifically omit a long tail of leaf nodes in high-width nodes
            // - omit tail of nodes whose size is only a fraction of the (undumped) parent tree size

            if (MaxDepth > 0 && depth >= MaxDepth)
            {
                numberOfElidedNodes = NumberOfNodesInTree(nodeIndex);
                return -1;
            }

            List<int>? children = CurrentHeapDom.GetChildren(nodeIndex);
            int numberOfChildren = children == null ? 0 : children.Count;

            if (NonLeafNodesOnly && numberOfChildren == 0)
            {
                numberOfElidedNodes = 1;
                return -1;
            }

            if (heapDomSizes.TreeSize(nodeIndex) < MinSize)
            {
                numberOfElidedNodes = NumberOfNodesInTree(nodeIndex);
                return -1;
            }

            m_numberOfNodesWritten++;
            if (needComma)
            {
                Output.AddDisplayString(",");
            }
            Output.AddDisplayString("{{\"name\":\"{0}\",", CurrentBacktracer.DescribeNodeIndex(nodeIndex, Output, fullyQualified: true));

            if (nodeIndex == CurrentHeapDom.RootNodeIndex)
            {
                Output.AddDisplayString("\"filename\":{0},",
                    JsonConvert.ToString(CurrentMemorySnapshot.Filename));
                Output.AddDisplayString("\"heapDomCommandLine\":{0},",
                    JsonConvert.ToString(Repl.CurrentCommandLine));
                Output.AddDisplayString("\"context\":{0},",
                    JsonConvert.ToString(string.Join('\n', Context.Serialize())));
            }

            if (NodeTypes)
            {
                Output.AddDisplayString("\"nodetype\":\"{0}\",", CurrentBacktracer.NodeType(nodeIndex));
            }

            bool elideChildren = false;
            if (m_diffTree != null)
            {
                string? diffString;
                switch (m_diffTree[nodeIndex])
                {
                    case Diff.New:
                        diffString = "new";
                        break;
                    case Diff.Modified:
                        diffString = "modified";
                        break;
                    case Diff.Same:
                        diffString = "same";
                        break;
                    default:
                        diffString = null;
                        break;
                }

                if (diffString != null)
                {
                    Output.AddDisplayString("\"diff\":\"{0}\",", diffString);
                }
            }

            if (nodeIndex == CurrentHeapDom.RootNodeIndex && ToplevelObjectsOnly)
            {
                var toplevelObjects = new List<int>();
                long newSize = 0;
                for (int i = 0; i < children!.Count; i++)
                {
                    int childNodeIndex = children[i];
                    if (CurrentBacktracer.IsLiveObjectNode(childNodeIndex))
                    {
                        toplevelObjects.Add(childNodeIndex);
                        newSize += heapDomSizes.TreeSize(childNodeIndex);
                    }
                }

                DumpChildren(toplevelObjects, newSize, heapDomSizes, comparer, depth);
            }
            else if (numberOfChildren == 0)
            {
                Output.AddDisplayString("\"value\":{0}", heapDomSizes.NodeSize(nodeIndex));
            }
            else if (elideChildren)
            {
                Output.AddDisplayString("\"value\":{0}", heapDomSizes.TreeSize(nodeIndex));
            }
            else
            {
                DumpChildren(children!, heapDomSizes.TreeSize(nodeIndex), heapDomSizes, comparer, depth);
            }
            Output.AddDisplayString("}");

            numberOfElidedNodes = 0;
            return heapDomSizes.TreeSize(nodeIndex);
        }

        void DumpChildren(List<int> children, long treeSize, HeapDomSizes heapDomSizes, IComparer<int> comparer, int depth)
        {
            Output.AddDisplayString("\"children\":[");

            var sortedChildren = new int[children.Count];
            for (int i = 0; i < children.Count; i++)
            {
                sortedChildren[i] = children![i];
            }
            Array.Sort(sortedChildren, comparer);

            long sizeDumped = 0;
            bool needComma = false;
            int numberOfElidedNodes = 0;
            for (int i = 0; i < children.Count; i++)
            {
                if (MaxWidth > 0 && i >= MaxWidth)
                {
                    numberOfElidedNodes += children.Count - i;
                    break;
                }
                long childSize = DumpTree(sortedChildren[i], heapDomSizes, comparer, needComma, depth + 1, out int numberOfElidedNodesInChild);
                numberOfElidedNodes += numberOfElidedNodesInChild;
                sizeDumped += childSize;
                if (childSize >= 0)
                {
                    needComma = true;
                }
            }

            long intrinsicSize = treeSize - sizeDumped;
            if (intrinsicSize > 0)
            {
                if (needComma)
                {
                    Output.AddDisplayString(",");
                }

                if (numberOfElidedNodes > 0)
                {
                    Output.AddDisplayString("{{\"name\":\"elided+{0}\",", numberOfElidedNodes);
                    if (NodeTypes)
                    {
                        Output.AddDisplayString("\"nodetype\":\"elided\",");
                    }
                }
                else
                {
                    Output.AddDisplayString("{\"name\":\"intrinsic\",");
                    if (NodeTypes)
                    {
                        Output.AddDisplayString("\"nodetype\":\"intrinsic\",");
                    }
                }
                Output.AddDisplayString("\"value\":{0}}}", intrinsicSize);
            }

            Output.AddDisplayString("]");
        }

        int NumberOfNodesInTree(int nodeIndex)
        {
            int numberOfNodes = 1;
            List<int>? children = CurrentHeapDom.GetChildren(nodeIndex);
            int numberOfChildren = children == null ? 0 : children.Count;
            for (int i = 0; i < numberOfChildren; i++)
            {
                numberOfNodes += NumberOfNodesInTree(children![i]);
            }
            return numberOfNodes;
        }

        public override string HelpText => "heapdom <output filename> ['relativeto <context id> ['elideunchanged]] ['depth <depth>] ['width <width>] ['minsize <node size in bytes>] ['objectsonly] ['nonleaves] ['nodetype] ['start|'nostart] ['type [<type index or pattern>] ['includederived]]";
    }
}
