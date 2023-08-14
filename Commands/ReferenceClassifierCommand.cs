/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using MemorySnapshotAnalyzer.CommandInfrastructure;
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

        [FlagArgument("list")]
        public bool List;

        [FlagArgument("verbose")]
        public bool Verbose;

        [FlagArgument("enable")]
        public bool EnableGroup;

        [FlagArgument("disable")]
        public bool DisableGroup;

        [PositionalArgument(index: 0, optional: true)]
        public string? GroupName;
#pragma warning restore CS0649 // Field '...' is never assigned to, and will always have its default value

        public override void Run()
        {
            int numberOfModes = 0;
            numberOfModes += Clear ? 1 : 0;
            numberOfModes += ReferenceClassifierFilename != null ? 1 : 0;
            numberOfModes += List ? 1 : 0;
            numberOfModes += EnableGroup ? 1 : 0;
            numberOfModes += DisableGroup ? 1 : 0;
            if (numberOfModes != 1)
            {
                throw new CommandException("only one mode flag may be given");
            }

            ReferenceClassifierStore store = Repl.ReferenceClassifierStore;

            if (Clear || EnableGroup || DisableGroup || List)
            {
                RunForGroups();
            }
            else if (ReferenceClassifierFilename != null)
            {
                try
                {
                    Repl.LoadReferenceClassifierFile(ReferenceClassifierFilename, GroupName);
                    Repl.DumpCurrentContext();
                }
                catch (FileFormatException ex)
                {
                    throw new CommandException(ex.Message);
                }
            }
        }

        void RunForGroups()
        {
            List<string> groupNames = new();
            if (GroupName == null)
            {
                foreach (ReferenceClassifierGroup group in Repl.ReferenceClassifierStore.AllGroups())
                {
                    groupNames.Add(group.Name);
                }
            }
            else if (GroupName.Length > 0 && GroupName[^1] == '*')
            {
                string prefix = GroupName[..^1];
                foreach (ReferenceClassifierGroup group in Repl.ReferenceClassifierStore.AllGroups())
                {
                    if (group.Name.StartsWith(prefix, System.StringComparison.Ordinal))
                    {
                        groupNames.Add(group.Name);
                    }
                }
            }
            else
            {
                foreach (string groupName in GroupName.Split(new char[] { ',', '+' }))
                {
                    groupNames.Add(groupName);
                }
            }

            bool dumpContext = false;
            foreach (string groupName in groupNames)
            {
                RunForGroup(groupName, ref dumpContext);
            }

            if (dumpContext)
            {
                Repl.DumpCurrentContext();
            }
        }

        void RunForGroup(string groupName, ref bool dumpContext)
        {
            ReferenceClassifierStore store = Repl.ReferenceClassifierStore;

            if (Clear)
            {
                store.ClearGroup(groupName);
                Context.TraceableHeap_ReferenceClassifier_DisableGroup(groupName);
                dumpContext = true;
            }

            if (EnableGroup)
            {
                if (store.TryGetGroup(groupName, out var _))
                {
                    Context.TraceableHeap_ReferenceClassifier_EnableGroup(groupName);
                    dumpContext = true;
                }
                else
                {
                    throw new CommandException($"unknown group \"{groupName}\"");
                }
            }

            if (DisableGroup)
            {
                Context.TraceableHeap_ReferenceClassifier_DisableGroup(groupName);
                dumpContext = true;
            }

            if (List)
            {
                if (store.TryGetGroup(groupName, out ReferenceClassifierGroup? group))
                {
                    DumpGroup(group!, verbose: true);
                }
                else
                {
                    Output.WriteLine($"unknown reference classifier group \"{group}\"");
                }
            }
        }

        void DumpGroup(ReferenceClassifierGroup group, bool verbose)
        {
            if (verbose)
            {
                Output.WriteLine("[{0}]", group.Name);
                foreach (Rule rule in group.GetRules())
                {
                    Output.WriteLine("  {0}", rule);
                }
            }
            else
            {
                Output.WriteLine("reference classifier group \"{0}\": {1} rule(s)", group.Name, group.NumberOfRules);
            }
        }

        public override string HelpText => "referenceclassifier ('clear | 'load <filename> | 'list ['verbose] | 'enable | 'disable) (<prefix*>|<name>),...";
    }
}
