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
    public class PrintCommand : Command
    {
        public PrintCommand(Repl repl) : base(repl) {}

#pragma warning disable CS0649 // Field '...' is never assigned to, and will always have its default value null
        [PositionalArgument(0, optional: false)]
        public CommandLineArgument? Value;
#pragma warning restore CS0649 // Field '...' is never assigned to, and will always have its default value null

        public override void Run()
        {
            switch (Value!.ArgumentType)
            {
                case CommandLineArgumentType.Atom:
                    Output.WriteLine("(Atom) {0}", Value.AtomValue);
                    break;
                case CommandLineArgumentType.String:
                    Output.WriteLine("(String) \"{0}\"", Value.StringValue);
                    break;
                case CommandLineArgumentType.Integer:
                    Output.WriteLine("(Integer) {0}", Value.IntegerValue);
                    if (Context.CurrentMemorySnapshot != null)
                    {
                        Output.WriteLine("(NativeWord) {0}", Value.AsNativeWord(CurrentMemorySnapshot.Native));
                    }
                    break;
                default:
                    throw new ArgumentException($"unknown value type {Value.ArgumentType}");
            }
        }

        public override string HelpText => "print <expression>";
    }
}
