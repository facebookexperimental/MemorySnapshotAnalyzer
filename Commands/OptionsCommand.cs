// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using MemorySnapshotAnalyzer.CommandProcessing;
using MemorySnapshotAnalyzer.ReferenceClassifiers;
using System;
using System.Collections.Generic;
using System.IO;

namespace MemorySnapshotAnalyzer.Commands
{
    public class OptionsCommand : Command
    {
        public OptionsCommand(Repl repl) : base(repl) {}

#pragma warning disable CS0649 // Field '...' is never assigned to, and will always have its default value
        [NamedArgument("heap")]
        public string? HeapKind;

        [FlagArgument("fuseobjectpairs")]
        public int FuseObjectPairs = -1;

        [NamedArgument("referenceclassifier")]
        public string? ReferenceClassifier;

        [FlagArgument("noreferenceclassifier")]
        public bool NoReferenceClassifier;

        [FlagArgument("weakgchandles")]
        public int WeakGCHandles = -1;

        [NamedArgument("rootobject")]
        public NativeWord RootObjectAddress;

        [FlagArgument("groupstatics")]
        public int GroupStatics = -1;

        [FlagArgument("fuseroots")]
        public int FuseRoots = -1;

        [FlagArgument("weakdelegates")]
        public int WeakDelegates = -1;
#pragma warning restore CS0649 // Field '...' is never assigned to, and will always have its default value

        public override void Run()
        {
            if (HeapKind != null)
            {
                switch (HeapKind) {
                    case "managed":
                        Context.TraceableHeap_Kind = Context.TraceableHeapKind.Managed;
                        break;
                    case "native":
                        Context.TraceableHeap_Kind = Context.TraceableHeapKind.Native;
                        break;
                    case "stitched":
                        Context.TraceableHeap_Kind = Context.TraceableHeapKind.Stitched;
                        break;
                    default:
                        throw new CommandException($"unknown heap kind \"{HeapKind}\"; must be one of \"managed\", \"native\", or \"stitched\"");
                }
            }

            if (FuseObjectPairs != -1)
            {
                Context.TraceableHeap_FuseObjectPairs = FuseObjectPairs != 0;
            }

            if (ReferenceClassifier != null && NoReferenceClassifier)
            {
                throw new CommandException("only one of 'referenceclassifier and 'noreferenceclassifier may be given");
            }
            else if (ReferenceClassifier != null)
            {
                Context.TraceableHeap_ReferenceClassifier = ReferenceClassifierLoader.Load(ReferenceClassifier);
            }
            else if (NoReferenceClassifier)
            {
                Context.TraceableHeap_ReferenceClassifier = new DefaultReferenceClassifierFactory();
            }

            if (WeakGCHandles != -1)
            {
                Context.TracedHeap_WeakGCHandles = WeakGCHandles != 0;
            }

            if (RootObjectAddress.Size != 0)
            {
                Context.RootSet_SingletonRootAddress = RootObjectAddress;
            }

            if (GroupStatics != -1)
            {
                Context.Backtracer_GroupStatics = GroupStatics != 0;
            }

            if (FuseRoots != -1)
            {
                Context.Backtracer_FuseRoots = FuseRoots != 0;
            }

            if (WeakDelegates != -1)
            {
                Context.Backtracer_WeakDelegates = WeakDelegates != 0;
            }

            Output.WriteLine("* [{0}]", Context.Id);
            Context.Dump(indent: 1);
        }

        public override string HelpText => "options ['heap \"managed\"|\"native\"|\"stitched\"] ['fuseobjectpairs] ['weakgchandles] ['rootobject <address or index>] ['groupstatics] ['fuseroots] ['weakdelegates]";
    }
}
