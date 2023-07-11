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
                Context.TraceableHeap_ReferenceClassifier = LoadReferenceClassifierConfiguration();
            }
            else if (NoReferenceClassifier)
            {
                Context.TraceableHeap_ReferenceClassifier = new ReferenceClassifierFactory();
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

        static char[] s_assemblySeparator = new char[] { ',', ':' };

        ConfigurableReferenceClassifierFactory LoadReferenceClassifierConfiguration()
        {
            try
            {
                var configurationEntries = new List<ConfigurableReferenceClassifierFactory.Rule>();
                int lineNumber = 1;
                foreach (string line in File.ReadAllLines(ReferenceClassifier!))
                {
                    string lineTrimmed = line.Trim();
                    if (lineTrimmed.Length > 0 && !lineTrimmed.StartsWith("#", StringComparison.OrdinalIgnoreCase))
                    {
                        int firstComma = lineTrimmed.IndexOfAny(s_assemblySeparator);
                        int lastComma = lineTrimmed.LastIndexOf(',');
                        if (firstComma < 0 || lastComma < 0 || firstComma == lastComma)
                        {
                            throw new CommandException($"invalid syntax on line {lineNumber}");
                        }

                        List<string> fieldPattern = ParseFieldPattern(lineTrimmed[(lastComma + 1)..].Trim(), lineNumber);
                        if (fieldPattern.Count == 1)
                        {
                            configurationEntries.Add(new ConfigurableReferenceClassifierFactory.FieldPatternRule
                            {
                                Spec = new ConfigurableReferenceClassifierFactory.ClassSpec
                                {
                                    Assembly = lineTrimmed[..firstComma].Trim(),
                                    ClassName = lineTrimmed[(firstComma + 1)..lastComma].Trim()
                                },
                                FieldPattern = fieldPattern[0]
                            });
                        }
                        else
                        {
                            configurationEntries.Add(new ConfigurableReferenceClassifierFactory.FieldPathRule
                            {
                                Spec = new ConfigurableReferenceClassifierFactory.ClassSpec
                                {
                                    Assembly = lineTrimmed[..firstComma].Trim(),
                                    ClassName = lineTrimmed[(firstComma + 1)..lastComma].Trim()
                                },
                                FieldNames = fieldPattern.ToArray()
                            }); ;
                        }
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

        static List<string> ParseFieldPattern(string fieldPattern, int lineNumber)
        {
            var pieces = new List<string>();
            int startIndex = 0;
            for (int i = 0; i < fieldPattern.Length; i++)
            {
                if (fieldPattern[i] == '[')
                {
                    if (i + 1 == fieldPattern.Length || fieldPattern[i + 1] != ']')
                    {
                        throw new CommandException($"invalid field pattern syntax on line {lineNumber}; '[' must be immediately followed by ']'");
                    }

                    if (i > startIndex)
                    {
                        pieces.Add(fieldPattern[startIndex..i]);
                    }

                    pieces.Add("[]");
                    startIndex = i + 2;
                    i++;
                }
                else if (fieldPattern[i] == '.')
                {
                    if (i > startIndex)
                    {
                        pieces.Add(fieldPattern[startIndex..i]);
                    }

                    startIndex = i + 1;
                }
            }

            if (startIndex != fieldPattern.Length)
            {
                pieces.Add(fieldPattern[startIndex..]);
            }

            return pieces;
        }

        public override string HelpText => "options ['heap \"managed\"|\"native\"|\"stitched\"] ['fuseobjectpairs] ['weakgchandles] ['rootobject <address or index>] ['groupstatics] ['fuseroots] ['weakdelegates]";
    }
}
