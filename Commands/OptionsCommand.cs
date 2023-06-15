// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using MemorySnapshotAnalyzer.Analysis;
using MemorySnapshotAnalyzer.CommandProcessing;
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

            if (ReferenceClassifier != null)
            {
                Context.TraceableHeap_ReferenceClassifier = LoadReferenceClassifierConfiguration();
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

        ConfigurableReferenceClassifierFactory LoadReferenceClassifierConfiguration()
        {
            try
            {
                var configurationEntries = new List<ConfigurableReferenceClassifierFactory.ConfigurationEntry>();
                int lineNumber = 1;
                foreach (string line in File.ReadAllLines(ReferenceClassifier!))
                {
                    string lineTrimmed = line.Trim();
                    if (lineTrimmed.Length > 0 && !lineTrimmed.StartsWith("#", StringComparison.OrdinalIgnoreCase))
                    {
                        int firstComma = lineTrimmed.IndexOf(',');
                        int lastComma = lineTrimmed.LastIndexOf(',');
                        if (firstComma < 0 || lastComma < 0 || firstComma == lastComma)
                        {
                            throw new CommandException($"invalid syntax on line {lineNumber}");
                        }

                        configurationEntries.Add(new ConfigurableReferenceClassifierFactory.ConfigurationEntry
                        {
                            Assembly = lineTrimmed[..firstComma].Trim(),
                            ClassName = lineTrimmed[(firstComma + 1)..lastComma].Trim(),
                            FieldPattern = lineTrimmed[(lastComma + 1)..].Trim()
                        });
                    }

                    lineNumber++;
                }

                return new ConfigurableReferenceClassifierFactory(ReferenceClassifier!, configurationEntries);
            }
            catch (IOException ex)
            {
                throw new CommandException(ex.Message);
            }
        }

        public override string HelpText => "options ['heap \"managed\"|\"native\"|\"stitched\"] ['fuseobjectpairs] ['weakgchandles] ['rootobject <address or index>] ['groupstatics] ['fusegchandles]";
    }
}
