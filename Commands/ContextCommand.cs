/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using MemorySnapshotAnalyzer.CommandInfrastructure;
using System;

namespace MemorySnapshotAnalyzer.Commands
{
    public class ContextCommand : Command
    {
        public ContextCommand(Repl repl) : base(repl) {}

#pragma warning disable CS0649 // Field '...' is never assigned to, and will always have its default value
        [PositionalArgument(0, optional: true)]
        public int Id = -1;

        [FlagArgument("flush")]
        public bool Flush;
#pragma warning restore CS0649 // Field '...' is never assigned to, and will always have its default value

        public override void Run()
        {
            if (Flush)
            {
                Context.FlushWarnings();
            }
            else
            {
                if (Id != -1)
                {
                    Repl.SwitchToContext(Id);
                }
                Repl.DumpContexts();
            }
        }

        public override string HelpText => "context [<id> | 'flush]";
    }
}
