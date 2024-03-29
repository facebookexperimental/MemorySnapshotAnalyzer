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
    public class DumpAssembliesCommand : Command
    {
        public DumpAssembliesCommand(Repl repl) : base(repl) {}

#pragma warning disable CS0649 // Field '...' is never assigned to, and will always have its default value null
        [PositionalArgument(0, optional: true)]
        public string? Substring;
#pragma warning restore CS0649 // Field '...' is never assigned to, and will always have its default value null

        public override void Run()
        {
            TypeSystem typeSystem = CurrentTraceableHeap.TypeSystem;

            var assemblies = new SortedDictionary<string, int>();
            for (int typeIndex = 0; typeIndex < typeSystem.NumberOfTypeIndices; typeIndex++)
            {
                string assemblyName = typeSystem.Assembly(typeIndex);

                if (Substring != null && !assemblyName.Contains(Substring))
                {
                    continue;
                }

                if (assemblies.TryGetValue(assemblyName, out int count))
                {
                    assemblies[assemblyName] = count + 1;
                }
                else
                {
                    assemblies[assemblyName] = 1;
                }
            }

            Output.BeginArray("assemblies");
            foreach (var kvp in assemblies)
            {
                Output.BeginElement();
                Output.AddProperty("assemblyName", kvp.Key);
                Output.AddProperty("typeCount", kvp.Value);
                Output.AddDisplayStringLine("assembly {0}: {1} type{2}",
                    kvp.Key,
                    kvp.Value,
                    kvp.Value != 1 ? "s" : "");
                Output.EndElement();
            }
            Output.EndArray();
        }

        public override string HelpText => "dumpassemblies [<substring>]";
    }
}
