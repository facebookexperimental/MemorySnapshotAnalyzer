// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.CommandProcessing;
using MemorySnapshotAnalyzer.ReferenceClassifiers;
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

        [FlagArgument("verbose")]
        public bool Verbose;
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
                    Repl.LoadReferenceClassifierFile(ReferenceClassifierFilename, GroupName);
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
                        DumpGroup(group!, verbose: true);
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
                        DumpGroup(group, Verbose);
                    }
                }
            }
        }

        void DumpGroup(ReferenceClassifierGroup group, bool verbose)
        {
            Output.WriteLine("reference classifier group \"{0}\": {1} rule(s)", group.Name, group.NumberOfRules);
            if (verbose)
            {
                foreach (Rule rule in group.GetRules())
                {
                    Output.WriteLine("  {0}", rule);
                }
            }
        }

        public override string HelpText => "referenceclassifier ['clear ['group <name>]] ['load <filename> ['group <name>]] ['list ['verbose | 'group <name>]]";
    }
}
