/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using MemorySnapshotAnalyzer.CommandInfrastructure;

namespace MemorySnapshotAnalyzer.Commands
{
    public class HelpCommand : Command
    {
        public HelpCommand(Repl repl) : base(repl) {}

        public override void Run()
        {
            Repl.OutputHelpText();
        }

        public override string HelpText => "help";
    }
}
