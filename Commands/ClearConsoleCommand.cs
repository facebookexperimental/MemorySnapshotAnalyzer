/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using MemorySnapshotAnalyzer.CommandInfrastructure;

namespace MemorySnapshotAnalyzer.Commands
{
    public class ClearConsoleCommand : Command
    {
        public ClearConsoleCommand(Repl repl) : base(repl) { }

        public override void Run()
        {
            Output.Clear();
        }

        public override string HelpText => "cls";
    }
}
