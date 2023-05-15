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

        [NamedArgument("rootobject")]
        public NativeWord Address;

        [FlagArgument("groupstatics")]
        public int GroupStatics = -1;

        [FlagArgument("fuseobjectpairs")]
        public int FuseObjectPairs = -1;

        [FlagArgument("fusegchandles")]
        public int FuseGCHandles = -1;

        [FlagArgument("weakgchandles")]
        public int WeakGCHandles = -1;
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

            if (Address.Size != 0)
            {
                Context.RootSet_SingletonRootAddress = Address;
            }

            if (GroupStatics != -1)
            {
                Context.Backtracer_GroupStatics = GroupStatics != 0;
            }

            if (FuseObjectPairs != -1)
            {
                Context.Backtracer_FuseObjectPairs = FuseObjectPairs != 0;
            }

            if (FuseGCHandles != -1)
            {
                Context.Backtracer_FuseGCHandles = FuseGCHandles != 0;
            }

            if (WeakGCHandles != -1)
            {
                Context.HeapDom_WeakGCHandles = WeakGCHandles != 0;
            }

            Output.WriteLine("* [{0}]", Context.Id);
            Context.Dump(indent: 1);
        }

        public override string HelpText => "options ['heap \"managed\"|\"native\"|\"stitched\"] ['rootobject <address or index>] ['groupstatics] ['fuseobjectpairs] ['fusegchandles]";
    }
}
