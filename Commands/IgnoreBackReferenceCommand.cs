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
    public class IgnoreBackReferenceCommand : Command
    {
        public IgnoreBackReferenceCommand(Repl repl) : base(repl) { }

#pragma warning disable CS0649 // Field '...' is never assigned to, and will always have its default value
        [FlagArgument("unignore")]
        public bool Unignore;

        [PositionalArgument(0, optional: true)]
        public NativeWord ChildAddressOrIndex;

        [PositionalArgument(1, optional: true)]
        public NativeWord ParentAddressOrIndex;

        [FlagArgument("clear")]
        public bool Clear;
#pragma warning restore CS0649 // Field '...' is never assigned to, and will always have its default value

        public override void Run()
        {
            if (Clear)
            {
                if (ChildAddressOrIndex.Size != 0 || ParentAddressOrIndex.Size != 0)
                {
                    throw new CommandException("'clear takes no arguments");
                }

                Context.Backtracer_ReferencesToIgnore_Replace(new HashSet<(int childNodeIndex, int parentNodeIndex)>());
                return;
            }

            if (ChildAddressOrIndex.Size == 0)
            {
                throw new CommandException("at least one argument required");
            }

            int childPostorderIndex = Context.ResolveToPostorderIndex(ChildAddressOrIndex);

            if (ParentAddressOrIndex.Size == 0)
            {
                if (Unignore)
                {
                    Context.Backtracer_ReferencesToIgnore_Remove(childPostorderIndex);
                }
                else
                {
                    Context.Backtracer_ReferencesToIgnore_Add(childPostorderIndex);
                }
            }
            else
            {
                int parentPostorderIndex = Context.ResolveToPostorderIndex(ParentAddressOrIndex);

                if (Unignore)
                {
                    Context.Backtracer_ReferencesToIgnore_Remove(childPostorderIndex, parentPostorderIndex);
                }
                else
                {
                    Context.Backtracer_ReferencesToIgnore_Add(childPostorderIndex, parentPostorderIndex);
                }
            }
        }

        public override string HelpText => "ignorebackref (['unignore] <child object address or index> [<parent object address or index>] | 'clear)";
    }
}
