/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using MemorySnapshotAnalyzer.ReferenceClassifiers;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace MemorySnapshotAnalyzer.CommandInfrastructure
{
    public sealed class Repl : IDisposable
    {
        enum NamedArgumentKind
        {
            PositiveFlag,
            NegativeFlag,
            NamedArgument
        }

        readonly IConfiguration m_configuration;
        readonly IOutput m_output;
        readonly IStructuredOutput m_structuredOutput;
        readonly ILoggerFactory m_loggerFactory;
        readonly bool m_isInteractive;
        readonly List<MemorySnapshotLoader> m_memorySnapshotLoaders;
        readonly SortedDictionary<string, Type> m_commands;
        readonly Dictionary<Type, Dictionary<string, NamedArgumentKind>> m_commandNamedArgumentNames;
        readonly Dictionary<Type, int> m_commandRemainderStartIndex;
        readonly ReferenceClassifierStore m_referenceClassifierStore;
        readonly SortedDictionary<int, Context> m_contexts;
        string? m_currentCommandLine;
        int m_currentContextId;

        public Repl(IConfiguration configuration, IOutput output, IStructuredOutput structuredOutput, ILoggerFactory loggerFactory, bool isInteractive)
        {
            m_configuration = configuration;
            m_output = output;
            m_structuredOutput = structuredOutput;
            m_loggerFactory = loggerFactory;
            m_isInteractive = isInteractive;
            m_memorySnapshotLoaders = new();
            m_commands = new();
            m_referenceClassifierStore = new();
            m_commandNamedArgumentNames = new();
            m_commandRemainderStartIndex = new();

            m_contexts = new();
            m_contexts.Add(0, new Context(0, m_output, m_structuredOutput, m_loggerFactory.MakeLogger(), m_referenceClassifierStore)
            {
                // TODO: read TraceableHeap_Kind value
                TraceableHeap_FuseObjectPairs = configuration.GetValue<bool>("FuseObjectPairs"),
                RootSet_WeakGCHandles = configuration.GetValue<bool>("WeakGCHandles"),
                Backtracer_GroupStatics = configuration.GetValue<bool>("GroupStatics"),
                Backtracer_FuseRoots = configuration.GetValue<bool>("FuseRoots")
            });
            m_currentContextId = 0;
            Output.Prompt = "[0]> ";

            string? initialReferenceClassifierFiles = configuration.GetValue<string>("InitialReferenceClassifierFiles");
            if (initialReferenceClassifierFiles != null)
            {
                foreach (string initialReferenceClassifierFile in initialReferenceClassifierFiles.Split(';'))
                {
                    try
                    {
                        HashSet<string> loadedGroups = m_referenceClassifierStore.LoadFromFile(initialReferenceClassifierFile, groupNamePrefix: null);
                        EnableGroups(loadedGroups);
                    }
                    catch (IOException ex)
                    {
                        Output.WriteLine($"error loading initial reference classifier file \"{initialReferenceClassifierFile}\": {ex.Message}");
                    }
                }
            }
        }

        public void Dispose()
        {
            foreach (var kvp in m_contexts)
            {
                kvp.Value.Dispose();
            }
        }

        public void AddMemorySnapshotLoader(MemorySnapshotLoader loader)
        {
            m_memorySnapshotLoaders.Add(loader);
        }

        public void EnableGroups(HashSet<string> groupNames)
        {
            foreach (string groupName in groupNames)
            {
                ForAllContexts(context => context.TraceableHeap_ReferenceClassifier_OnModifiedGroup(groupName));
                CurrentContext.TraceableHeap_ReferenceClassifier_EnableGroup(groupName);
            }
        }

        public MemorySnapshot? TryLoadMemorySnapshot(string filename)
        {
            foreach (MemorySnapshotLoader loader in m_memorySnapshotLoaders)
            {
                MemorySnapshot? memorySnapshot = loader.TryLoad(filename);
                if (memorySnapshot != null)
                {
                    return memorySnapshot;
                }
            }
            return null;
        }

        public void AddCommand(Type commandType, params string[] names)
        {
            CheckValidityOfArgumentDeclarations(commandType);
            foreach (string name in names)
            {
                m_commands[name] = commandType;
            }
        }

        public IConfiguration Configuration => m_configuration;

        public IOutput Output => m_output;

        public IStructuredOutput StructuredOutput => m_structuredOutput;

        public bool IsInteractive => m_isInteractive;

        public string CurrentCommandLine => m_currentCommandLine!;

        public ReferenceClassifierStore ReferenceClassifierStore => m_referenceClassifierStore;

        public Context CurrentContext => m_contexts[m_currentContextId];

        public Context SwitchToContext(int id)
        {
            if (!m_contexts.ContainsKey(id))
            {
                m_contexts.Add(id, Context.WithSameOptionsAs(m_contexts[m_currentContextId], m_loggerFactory.MakeLogger(), id));
            }
            m_currentContextId = id;
            Output.Prompt = $"[{id}]> ";
            return m_contexts[id];
        }

        public Context SwitchToNewContext()
        {
            int id = 0;
            while (m_contexts.ContainsKey(id))
            {
                id++;
            }
            return SwitchToContext(id);
        }

        public Context? GetContext(int id)
        {
            if (m_contexts.TryGetValue(id, out Context? context))
            {
                return context!;
            }
            else
            {
                return null;
            }
        }

        public void ForAllContexts(Action<Context> action)
        {
            foreach ((_, Context context) in m_contexts)
            {
                action(context);
            }
        }

        public void RunBatchFile(string batchFilename)
        {
            foreach (string line in File.ReadAllLines(batchFilename))
            {
                string lineTrimmed = line.Trim();
                if (lineTrimmed.Length > 0 && lineTrimmed[0] != '#')
                {
                    RunCommandNonInteractively(lineTrimmed);
                }
            }
        }

        public void RunCommandNonInteractively(string line)
        {
            // Output the command we are running so it's easy for a human reader to follow along.
            Output.WriteLine($"{Output.Prompt}{line}");

            // Let exceptions escape, to be reported adequately as an error in the main method.
            RunCommand(line);
        }

        public void RunInteractively()
        {
#if !WINDOWS
            ReadLine.HistoryEnabled = true;
#endif
            while (true)
            {
                Output.DoPrompt();
#if WINDOWS
                string? line = Console.ReadLine();
#else
                string? line = ReadLine.Read();
#endif
                if (line == null)
                {
                    continue;
                }

                RunCommandInteractively(line);
            }
        }

        public void RunCommandInteractively(string line)
        {
            // Handle exceptions that are expected, so the user is able to correct their mistake and run the command again.
            // Let unexpected exceptions crash the app.
            RunWithHandler(() =>
            {
                try
                {
                    Output.ExecutionStart();
                    RunCommand(line);
                    Output.ExecutionEnd(0);
                }
                catch (OperationCanceledException)
                {
                    // This exception is not expected within RunWithHandler, as it can only ever be thrown if we run interactively.
                    Output.WriteLine("canceled");
                    Output.ExecutionEnd(1);
                }
            }, ex =>
            {
                Output.WriteLine(ex.Message);
                Output.ExecutionEnd(1);
            });
        }

        // Runs a command with handlers in place for the expected exception types (those that should be caught and handled, not crash the app).
        public static void RunWithHandler(Action command, Action<Exception> handler)
        {
            try
            {
                command();
            }
            catch (IOException ex)
            {
                handler(ex);
            }
            catch (InvalidSnapshotFormatException ex)
            {
                handler(ex);
            }
            catch (CommandException ex)
            {
                handler(ex);
            }
        }

        public void RunCommand(string line)
        {
            CommandLine? commandLine = CommandLineParser.Parse(line, CurrentContext);
            if (commandLine == null)
            {
                return;
            }

            Type? commandType;
            if (!m_commands.TryGetValue(commandLine.CommandName, out commandType))
            {
                throw new CommandException($"unknown command '{commandLine.CommandName}'");
            }

            try
            {
                m_structuredOutput.BeginElement();
                m_structuredOutput.AddProperty("commandName", commandType.Name);
                m_structuredOutput.AddProperty("commandLine", line);

                var commandConstructorSignature = new Type[] { typeof(Repl) };
                var commandConstructorArguments = new object[] { this };

                Command command = (Command)commandType.GetConstructor(commandConstructorSignature)!.Invoke(commandConstructorArguments);
                try
                {
                    m_structuredOutput.BeginChild("commandLineArguments");
                    AssignArgumentValues(command, commandLine);
                }
                finally
                {
                    m_structuredOutput.EndChild();
                }

                try
                {
                    m_currentCommandLine = line;
                    m_structuredOutput.BeginChild("commandOutput");
                    command.Run();
                }
                finally
                {
                    m_structuredOutput.EndChild();
                    m_currentCommandLine = null;

                    foreach (var kvp in m_contexts)
                    {
                        kvp.Value.SummarizeNewWarnings();
                    }
                }
            }
            finally
            {
                m_structuredOutput.BeginChild("context");
                m_structuredOutput.AddProperty("currentContextId", m_currentContextId);
                CurrentContext.DumpToStructuredOutput(m_structuredOutput);
                m_structuredOutput.EndChild();

                m_structuredOutput.EndElement();
            }
        }

        public void OutputHelpText()
        {
            var commandConstructorSignature = new Type[1];
            commandConstructorSignature[0] = typeof(Repl);
            var commandConstructorArguments = new object[1];
            commandConstructorArguments[0] = this;
            foreach (var kvp in m_commands)
            {
                Command command = (Command)kvp.Value.GetConstructor(commandConstructorSignature)!.Invoke(commandConstructorArguments);
                Output.WriteLine("{0,-15}: {1}", kvp.Key, command.HelpText);
            }
        }

        public void DumpContexts()
        {
            foreach (var kvp in m_contexts)
            {
                Output.WriteLine("{0} [{1}]",
                    kvp.Key == m_currentContextId ? "*" : " ",
                    kvp.Value.Id);
                kvp.Value.Dump(indent: 1);
            }
        }

        public void DumpCurrentContext()
        {
            Output.WriteLine("* [{0}]", m_currentContextId);
            CurrentContext.Dump(indent: 1);
        }

        void CheckValidityOfArgumentDeclarations(Type commandType)
        {
            TypeInfo typeInfo = commandType.GetTypeInfo();
            var positionalArguments = new Dictionary<int, bool>();
            int lowestOptionalFound = int.MaxValue;
            int highestRequiredFound = -1;
            bool remainderFound = false;
            var namedArgumentNames = new Dictionary<string, NamedArgumentKind>();
            foreach (FieldInfo field in typeInfo.GetRuntimeFields())
            {
                int numberOfAttributesFound = 0;
                foreach (CustomAttributeData attribute in field.CustomAttributes)
                {
                    if (attribute.AttributeType == typeof(PositionalArgumentAttribute)
                        || attribute.AttributeType == typeof(NamedArgumentAttribute))
                    {
                        numberOfAttributesFound++;

                        if (attribute.AttributeType == typeof(PositionalArgumentAttribute))
                        {
                            int index = (int)attribute.ConstructorArguments[0].Value!;
                            bool optional = (bool)attribute.ConstructorArguments[1].Value!;

                            if (optional && index < lowestOptionalFound)
                            {
                                lowestOptionalFound = index;
                            }
                            else if (!optional && index > highestRequiredFound)
                            {
                                highestRequiredFound = index;
                            }

                            positionalArguments[index] = true;
                        }
                        else
                        {
                            string namedArgumentName = (string)attribute.ConstructorArguments[0].Value!;
                            if (namedArgumentNames.ContainsKey(namedArgumentName))
                            {
                                throw new ArgumentException($"command {commandType.Name} field {field.Name}: duplicate argument name {namedArgumentName}");
                            }

                            namedArgumentNames.Add(namedArgumentName, NamedArgumentKind.NamedArgument);
                        }

                        if (field.FieldType != typeof(string)
                            && field.FieldType != typeof(int)
                            && field.FieldType != typeof(NativeWord)
                            && field.FieldType != typeof(CommandLineArgument))
                        {
                            throw new ArgumentException($"command {commandType.Name} field {field.Name}: unsupported type");
                        }
                    }
                    else if (attribute.AttributeType == typeof(FlagArgumentAttribute))
                    {
                        numberOfAttributesFound++;

                        string flagName = (string)attribute.ConstructorArguments[0].Value!;
                        if (namedArgumentNames.ContainsKey(flagName))
                        {
                            throw new ArgumentException($"command {commandType.Name} field {field.Name}: duplicate argument name {flagName}");
                        }

                        namedArgumentNames.Add(flagName, NamedArgumentKind.PositiveFlag);

                        string noflagName = $"no{flagName}";
                        if (namedArgumentNames.ContainsKey(noflagName))
                        {
                            throw new ArgumentException($"command {commandType.Name} field {field.Name}: duplicate argument name {noflagName}");
                        }

                        namedArgumentNames.Add(noflagName, NamedArgumentKind.NegativeFlag);

                        if (field.FieldType != typeof(bool) && field.FieldType != typeof(int))
                        {
                            throw new ArgumentException($"command {commandType.Name} field {field.Name}: flag fields must be of bool or int type");
                        }
                    }
                    else if (attribute.AttributeType == typeof(RemainingArgumentsAttribute))
                    {
                        numberOfAttributesFound++;
                        remainderFound = true;

                        if (field.FieldType != typeof(List<string>)
                            && field.FieldType != typeof(List<int>)
                            && field.FieldType != typeof(List<NativeWord>)
                            && field.FieldType != typeof(List<CommandLineArgument>))
                        {
                            throw new ArgumentException($"command {commandType.Name} field {field.Name}: unsupported type");
                        }
                    }
                }

                if (numberOfAttributesFound > 1)
                {
                    throw new ArgumentException($"command {commandType.Name} field {field.Name}: conflicting attributes");
                }

                if (lowestOptionalFound < highestRequiredFound)
                {
                    throw new ArgumentException($"command {commandType.Name} field {field.Name}: positional argument {lowestOptionalFound} is optional, but later argument {highestRequiredFound} is not");
                }

                if (remainderFound && lowestOptionalFound < int.MaxValue)
                {
                    throw new ArgumentException($"command {commandType.Name} field {field.Name}: cannot have both a remainder field and an optional positional field");
                }
            }

            for (int i = 0; i < positionalArguments.Count; i++)
            {
                if (!positionalArguments.GetValueOrDefault(i))
                {
                    throw new ArgumentException($"command {commandType.Name}: missing positional argument with index {i}");
                }
            }

            m_commandNamedArgumentNames[commandType] = namedArgumentNames;
            m_commandRemainderStartIndex[commandType] = remainderFound ? highestRequiredFound + 1 : -1;
        }

        void AssignArgumentValues(Command command, CommandLine commandLine)
        {
            var flags = new Dictionary<string, bool>();
            var namedArguments = new Dictionary<string, CommandLineArgument>();
            var positionalArguments = new List<CommandLineArgument>();
            for (int i = 0; i < commandLine.NumberOfArguments; i++)
            {
                if (commandLine[i].ArgumentType == CommandLineArgumentType.Atom)
                {
                    string atom = commandLine[i].AtomValue;
                    if (m_commandNamedArgumentNames[command.GetType()].TryGetValue(atom, out NamedArgumentKind kind))
                    {
                        switch (kind)
                        {
                            case NamedArgumentKind.NamedArgument:
                                if (i + 1 == commandLine.NumberOfArguments)
                                {
                                    throw new CommandException($"missing value for named argument {atom}");
                                }
                                namedArguments.Add(atom, commandLine[i + 1]);
                                i++;
                                break;
                            case NamedArgumentKind.PositiveFlag:
                                flags.Add(atom, true);
                                break;
                            case NamedArgumentKind.NegativeFlag:
                                flags.Add(atom.Substring(2), false); // remove "no" prefix
                                break;
                        }
                        continue;
                    }
                }
                positionalArguments.Add(commandLine[i]);
            }

            TypeInfo typeInfo = command.GetType().GetTypeInfo();
            int argumentsConsumed = 0;
            int remainderStartIndex = m_commandRemainderStartIndex[command.GetType()];
            foreach (FieldInfo field in typeInfo.GetRuntimeFields())
            {
                foreach (CustomAttributeData attribute in field.CustomAttributes)
                {
                    if (attribute.AttributeType == typeof(PositionalArgumentAttribute))
                    {
                        int index = (int)attribute.ConstructorArguments[0].Value!;
                        bool optional = (bool)attribute.ConstructorArguments[1].Value!;

                        if (index < positionalArguments.Count)
                        {
                            SetFieldValueScalar(commandLine.CommandName, command, field, positionalArguments[index]);
                        }
                        else if (!optional)
                        {
                            throw new CommandException($"missing argument at position {index}: {field.Name}");
                        }

                        argumentsConsumed++;
                    }
                    else if (attribute.AttributeType == typeof(NamedArgumentAttribute))
                    {
                        string namedArgumentName = (string)attribute.ConstructorArguments[0].Value!;
                        if (namedArguments.TryGetValue(namedArgumentName, out var value))
                        {
                            SetFieldValueScalar(commandLine.CommandName, command, field, value);
                        }
                    }
                    else if (attribute.AttributeType == typeof(FlagArgumentAttribute))
                    {
                        string flagName = (string)attribute.ConstructorArguments[0].Value!;
                        if (flags.TryGetValue(flagName, out var value))
                        {
                            SetFieldValueFlag(command, field, value);
                        }
                    }
                    else if (attribute.AttributeType == typeof(RemainingArgumentsAttribute))
                    {
                        SetFieldValueRepeated(commandLine.CommandName, command, field, positionalArguments, remainderStartIndex);
                    }
                }
            }

            if (remainderStartIndex == -1 && argumentsConsumed < positionalArguments.Count)
            {
                throw new CommandException($"extraneous arguments given: {argumentsConsumed} expected, {positionalArguments.Count} found");
            }
        }

        void SetFieldValueFlag(Command command, FieldInfo field, bool value)
        {
            m_structuredOutput.BeginChild(field.Name);

            if (field.FieldType == typeof(bool))
            {
                field.SetValue(command, value);
                m_structuredOutput.AddProperty("kind", "boolean");
                m_structuredOutput.AddProperty("value", value);
            }
            else // field.FieldType == typeof(int)
            {
                field.SetValue(command, value ? 1 : 0);
                m_structuredOutput.AddProperty("kind", "int");
                m_structuredOutput.AddProperty("value", value ? 1 : 0);
            }

            m_structuredOutput.EndChild();
        }

        void SetFieldValueScalar(string commandName, Command command, FieldInfo field, CommandLineArgument value)
        {
            m_structuredOutput.BeginChild(field.Name);

            if (field.FieldType == typeof(string))
            {
                field.SetValue(command, AsString(value));
            }
            else if (field.FieldType == typeof(int))
            {
                field.SetValue(command, AsInteger(value));
            }
            else if (field.FieldType == typeof(NativeWord))
            {
                field.SetValue(command, AsNativeWord(commandName, field, value));
            }
            else if (field.FieldType == typeof(CommandLineArgument))
            {
                field.SetValue(command, AsCommandLineArgument(value));
            }

            m_structuredOutput.EndChild();
        }

        void SetFieldValueRepeated(string commandName, Command command, FieldInfo field, List<CommandLineArgument> positionalArguments, int startIndex)
        {
            m_structuredOutput.BeginArray(field.Name);

            if (field.FieldType == typeof(List<string>))
            {
                SetFieldValueList(command, field, positionalArguments, startIndex, AsString);
            }
            else if (field.FieldType == typeof(List<int>))
            {
                SetFieldValueList(command, field, positionalArguments, startIndex, AsInteger);
            }
            else if (field.FieldType == typeof(List<NativeWord>))
            {
                SetFieldValueList(command, field, positionalArguments, startIndex, value => AsNativeWord(commandName, field, value));
            }
            else if (field.FieldType == typeof(List<CommandLineArgument>))
            {
                SetFieldValueList(command, field, positionalArguments, startIndex, AsCommandLineArgument);
            }

            m_structuredOutput.EndArray();
        }

        void SetFieldValueList<T>(Command command, FieldInfo field, List<CommandLineArgument> positionalArguments, int startIndex, Func<CommandLineArgument, T> convert)
        {
            List<T>? values = (List<T>?)field.GetValue(command);
            if (values == null)
            {
                values = new();
                field.SetValue(command, values);
            }

            for (int index = startIndex; index < positionalArguments.Count; index++)
            {
                m_structuredOutput.BeginElement();
                values.Add(convert(positionalArguments[index]));
                m_structuredOutput.EndElement();
            }
        }

        string AsString(CommandLineArgument value)
        {
            m_structuredOutput.AddProperty("kind", "string");
            m_structuredOutput.AddProperty("value", value.StringValue);
            return value.StringValue;
        }

        int AsInteger(CommandLineArgument value)
        {
            m_structuredOutput.AddProperty("kind", "integer");
            m_structuredOutput.AddProperty("value", (int)value.IntegerValue);
            return (int)value.IntegerValue;
        }

        NativeWord AsNativeWord(string commandName, FieldInfo field, CommandLineArgument value)
        {
            if (CurrentContext.CurrentMemorySnapshot == null)
            {
                throw new CommandException($"command {commandName} argument {field.Name} can only be given when there is an active memory snapshot");
            }

            m_structuredOutput.AddProperty("kind", "integer");
            m_structuredOutput.AddProperty("value", (long)value.IntegerValue);
            return value.AsNativeWord(CurrentContext.CurrentMemorySnapshot.Native);
        }

        CommandLineArgument AsCommandLineArgument(CommandLineArgument value)
        {
            value.Describe(m_structuredOutput);
            return value;
        }
    }
}
