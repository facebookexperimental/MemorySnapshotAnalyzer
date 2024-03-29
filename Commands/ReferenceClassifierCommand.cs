/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using MemorySnapshotAnalyzer.CommandInfrastructure;
using MemorySnapshotAnalyzer.ReferenceClassifiers;
using System;
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
        public string? ReferenceClassifierFilenameToLoad;

        [NamedArgument("save")]
        public string? ReferenceClassifierFilenameToSave;

        [NamedArgument("fromdll")]
        public string? DllDirectory;

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
            numberOfModes += ReferenceClassifierFilenameToLoad != null ? 1 : 0;
            numberOfModes += ReferenceClassifierFilenameToSave != null ? 1 : 0;
            numberOfModes += DllDirectory != null ? 1 : 0;
            numberOfModes += List ? 1 : 0;
            numberOfModes += EnableGroup ? 1 : 0;
            numberOfModes += DisableGroup ? 1 : 0;
            if (numberOfModes != 1)
            {
                throw new CommandException("exactly one mode flag must be selected for this command");
            }

            ReferenceClassifierStore store = Repl.ReferenceClassifierStore;

            if (Clear || EnableGroup || DisableGroup || List)
            {
                RunForGroups();
            }
            else if (ReferenceClassifierFilenameToLoad != null)
            {
                try
                {
                    string? groupNamePrefix = GetGroupNamePrefix();
                    HashSet<string> loadedGroups = Repl.ReferenceClassifierStore.LoadFromFile(ReferenceClassifierFilenameToLoad, groupNamePrefix);
                    Repl.EnableGroups(loadedGroups);
                    Repl.DumpCurrentContext();
                }
                catch (IOException ex)
                {
                    throw new CommandException(ex.Message);
                }
            }
            else if (ReferenceClassifierFilenameToSave != null)
            {
                try
                {
                    using (var fileOutput = new FileOutput(ReferenceClassifierFilenameToSave, useUnixNewlines: true))
                    using (RedirectOutput(new PassthroughStructuredOutput(fileOutput)))
                    {
                        List = true;
                        Verbose = true;
                        Output.AddDisplayStringLine("###");
                        Output.AddDisplayStringLine("### THIS FILE WAS GENERATED BY MemorySnapshotAnalyzer USING \"rc 'fromdll\"; DO NOT EDIT");
                        Output.AddDisplayStringLine("###");
                        Output.AddDisplayStringLine(string.Empty);
                        RunForGroups();
                    }
                }
                catch (IOException ex)
                {
                    throw new CommandException(ex.Message);
                }
            }
            else if (DllDirectory != null)
            {
                string? groupNamePrefix = GetGroupNamePrefix();
                HashSet<string> loadedGroups = Repl.ReferenceClassifierStore.LoadFromDllDirectory(DllDirectory, Context.Logger, groupNamePrefix);
                Repl.EnableGroups(loadedGroups);
                Repl.DumpCurrentContext();
            }
        }

        string? GetGroupNamePrefix()
        {
            if (GroupName == null)
            {
                return null;
            }

            List<string> groupNames = ResolveGroupNames();
            if (groupNames.Count == 0)
            {
                return null;
            }
            else if (groupNames.Count == 1)
            {
                return groupNames[0];
            }
            else
            {
                throw new CommandException("at most one group name prefix must be given for this command");
            }
        }

        List<string> ResolveGroupNames()
        {
            List<string> groupNames = new();
            if (GroupName == null)
            {
                foreach (ReferenceClassifierGroup group in Repl.ReferenceClassifierStore.AllGroups())
                {
                    groupNames.Add(group.Name);
                }
            }
            else
            {
                foreach (string groupName in GroupName.Split(new char[] { ',', '+' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (groupName[^1] == '*')
                    {
                        string prefix = groupName[..^1];
                        foreach (ReferenceClassifierGroup group in Repl.ReferenceClassifierStore.AllGroups())
                        {
                            if (group.Name.StartsWith(prefix, StringComparison.Ordinal))
                            {
                                groupNames.Add(group.Name);
                            }
                        }
                    }
                    else
                    {
                        groupNames.Add(groupName);
                    }
                }
            }

            return groupNames;
        }

        void RunForGroups()
        {
            if (List)
            {
                Output.BeginArray("groups");
            }

            bool dumpContext = false;
            foreach (string groupName in ResolveGroupNames())
            {
                if (List)
                {
                    Output.BeginElement();
                }
                RunForGroup(groupName, ref dumpContext);
                if (List)
                {
                    Output.EndElement();
                }
            }

            if (List)
            {
                Output.EndArray();
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
                    DumpGroup(group, verbose: Verbose);
                }
                else
                {
                    throw new CommandException($"unknown group \"{groupName}\"");
                }
            }
        }

        void DumpGroup(ReferenceClassifierGroup group, bool verbose)
        {
            Output.AddProperty("groupName", group.Name);
            Output.AddProperty("numberOfRules", group.NumberOfRules);

            if (verbose)
            {
                Output.AddDisplayStringLine("[{0}]", group.Name);
                Output.AddDisplayStringLine(string.Empty);
                SortedSet<string> ruleTexts = new();
                foreach (Rule rule in group.GetRules())
                {
                    ruleTexts.Add(rule.ToString()!);
                }

                Output.BeginArray("rules");
                foreach (string ruleText in ruleTexts)
                {
                    Output.BeginElement();
                    Output.AddProperty("ruleText", ruleText);
                    Output.AddDisplayStringLine("{0}", ruleText);
                    Output.EndElement();
                }
                Output.EndArray();
                Output.AddDisplayStringLine(string.Empty);
            }
            else
            {
                Output.AddDisplayStringLine("reference classifier group \"{0}\": {1} rule(s)", group.Name, group.NumberOfRules);
            }
        }

        public override string HelpText => "referenceclassifier ('clear | 'load <filename> | 'save <filename> | 'fromdll <directory> | 'list ['verbose] | 'enable | 'disable) (<prefix*>|<name>),...";
    }
}
