// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using MemorySnapshotAnalyzer.CommandProcessing;

namespace MemorySnapshotAnalyzer.Commands
{
    public class OptionsCommand : Command
    {
        public OptionsCommand(Repl repl) : base(repl) {}

#pragma warning disable CS0649 // Field '...' is never assigned to, and will always have its default value
        [NamedArgument("rootobject")]
        public NativeWord Address;

        [FlagArgument("groupstatics")]
        public int GroupStatics = -1;

        [FlagArgument("weakgchandles")]
        public int WeakGCHandles = -1;
#pragma warning restore CS0649 // Field '...' is never assigned to, and will always have its default value

        public override void Run()
        {
            if (Address.Size != 0)
            {
                Context.RootSet_SingletonRootAddress = Address;
            }

            if (GroupStatics != -1)
            {
                Context.Backtracer_GroupStatics = GroupStatics != 0;
            }

            if (WeakGCHandles != -1)
            {
                Context.HeapDom_WeakGCHandles = WeakGCHandles != 0;
            }

            Output.WriteLine("Root Set: rootobject is {0}",
                Context.RootSet_SingletonRootAddress.Value == 0 ? "not set" : Context.RootSet_SingletonRootAddress.ToString());
            Output.WriteLine("Backtracer: groupstatics is {0}",
                Context.Backtracer_GroupStatics);
            Output.WriteLine("Dominator computation: weakgchandles is {0}",
                Context.HeapDom_WeakGCHandles);
        }

        public override string HelpText => "options ['rootobject <address or index>] ['groupstatics] ['weakgchandles]";
    }
}
