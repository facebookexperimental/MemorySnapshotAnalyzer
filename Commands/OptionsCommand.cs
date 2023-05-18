// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using MemorySnapshotAnalyzer.CommandProcessing;

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

        [FlagArgument("weakgchandles")]
        public int WeakGCHandles = -1;

        [NamedArgument("rootobject")]
        public NativeWord RootObjectAddress;

        [FlagArgument("groupstatics")]
        public int GroupStatics = -1;

        // TODO: this is not a great name anymore, as it fuses all roots with their targets
        [FlagArgument("fusegchandles")]
        public int FuseGCHandles = -1;
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

            if (FuseGCHandles != -1)
            {
                Context.Backtracer_FuseGCHandles = FuseGCHandles != 0;
            }

            Output.WriteLine("* [{0}]", Context.Id);
            Context.Dump(indent: 1);
        }

        public override string HelpText => "options ['heap \"managed\"|\"native\"|\"stitched\"] ['fuseobjectpairs] ['weakgchandles] ['rootobject <address or index>] ['groupstatics] ['fusegchandles]";
    }
}
