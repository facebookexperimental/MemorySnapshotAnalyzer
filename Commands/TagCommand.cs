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
    public class TagCommand : Command
    {
        public TagCommand(Repl repl) : base(repl) { }

#pragma warning disable CS0649 // Field '...' is never assigned to, and will always have its default value 0
        [PositionalArgument(0, optional: false)]
        public string? Tag;

        [PositionalArgument(1, optional: false)]
        public NativeWord AddressOrIndex;
#pragma warning restore CS0649 // Field '...' is never assigned to, and will always have its default value 0

        public override void Run()
        {
            int postorderIndex = Context.ResolveToPostorderIndex(AddressOrIndex);
            NativeWord address = CurrentTracedHeap.PostorderAddress(postorderIndex);
            CurrentTracedHeap.RecordTags(address, new List<string> { Tag! });
        }

        public override string HelpText => "tag <tag> <object address or index>";
    }
}
