// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.CommandProcessing;
using MemorySnapshotAnalyzer.ReferenceClassifiers;
using System.Collections.Generic;
using System.IO;

namespace MemorySnapshotAnalyzer.Commands
{
    public class ReferenceClassifierCommand : Command
    {
        public ReferenceClassifierCommand(Repl repl) : base(repl) { }

#pragma warning disable CS0649 // Field '...' is never assigned to, and will always have its default value
        [FlagArgument("clear")]
        public bool Clear;

        [NamedArgument("load")]
        public string? ReferenceClassifierFilename;

        [NamedArgument("group")]
        public string? GroupName;

        [FlagArgument("list")]
        public bool List;
#pragma warning restore CS0649 // Field '...' is never assigned to, and will always have its default value

        public override void Run()
        {
            ReferenceClassifierStore store = Repl.ReferenceClassifierStore;

            if (Clear)
            {
                if (GroupName != null)
                {
                    store.ClearGroup(GroupName);
                }
                else
                {
                    store.Clear();
                }
            }

            if (ReferenceClassifierFilename != null)
            {
                try
                {
                    HashSet<string> loadedGroups = store.Load(ReferenceClassifierFilename, GroupName);
                    foreach (string groupName in loadedGroups)
                    {
                        Repl.ForAllContexts(context => context.TraceableHeap_ReferenceClassifier_OnModifiedGroup(groupName));
                        Context.TraceableHeap_ReferenceClassifier_AddGroup(groupName);
                    }

                    Output.WriteLine("* [{0}]", Context.Id);
                    Context.Dump(indent: 1);
                }
                catch (FileFormatException ex)
                {
                    throw new CommandException(ex.Message);
                }
            }

            if (List)
            {
                if (GroupName != null)
                {
                    if (store.TryGetGroup(GroupName, out ReferenceClassifierGroup? group))
                    {
                        Output.WriteLine("reference classifier group \"{0}\": {1} rule(s)", GroupName, group!.NumberOfRules);
                        foreach (Rule rule in group.GetRules())
                        {
                            Output.WriteLine("  {0}", rule);
                        }
                    }
                    else
                    {
                        Output.WriteLine($"unknown reference classifier group \"{group}\"");
                    }
                }
                else
                {
                    foreach (ReferenceClassifierGroup group in store.AllGroups())
                    {
                        Output.WriteLine("reference classifier group \"{0}\": {1} rule(s)", group.Name, group.NumberOfRules);
                    }
                }
            }
        }

        public override string HelpText => "referenceclassifier ['clear ['group <name>]] ['load <filename> ['group <name>]] ['list ['group <name>]]";
    }
}
