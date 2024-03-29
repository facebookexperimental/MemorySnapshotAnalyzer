/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using MemorySnapshotAnalyzer.Analysis;
using MemorySnapshotAnalyzer.CommandInfrastructure;
using System.Text;

namespace MemorySnapshotAnalyzer.Commands
{
    public class DumpRootsCommand : Command
    {
        public DumpRootsCommand(Repl repl) : base(repl) {}

#pragma warning disable CS0649 // Field '...' is never assigned to, and will always have its default value
        [NamedArgument("type")]
        public CommandLineArgument? TypeIndexOrPattern;

        [FlagArgument("includederived")]
        public bool IncludeDerived;
#pragma warning restore CS0649 // Field '...' is never assigned to, and will always have its default value

        public override void Run()
        {
            TypeSet? typeSet = null;
            if (TypeIndexOrPattern != null)
            {
                // Only consider statics within the given types.
                typeSet = TypeIndexOrPattern.ResolveTypeIndexOrPattern(Context, IncludeDerived);
            }

            var sb = new StringBuilder();
            IRootSet rootSet = CurrentRootSet;

            Output.BeginArray("roots");

            for (int rootIndex = 0; rootIndex < rootSet.NumberOfRoots; rootIndex++)
            {
                PointerInfo<NativeWord> pointerInfo = rootSet.GetRoot(rootIndex);
                if (typeSet != null && !typeSet.Contains(pointerInfo.TypeIndex))
                {
                    continue;
                }

                Output.BeginElement();

                DescribePointerInfo(pointerInfo, sb);
                if (sb.Length > 0)
                {
                    Output.AddDisplayStringLine("{0}: {1} -> {2}",
                        rootIndex,
                        rootSet.DescribeRoot(rootIndex, Output, fullyQualified: true),
                        sb);
                    sb.Clear();
                }

                Output.EndElement();
            }

            Output.EndArray();
        }

        public override string HelpText => "dumproots";
    }
}
