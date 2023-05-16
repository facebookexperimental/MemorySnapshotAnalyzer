// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MemorySnapshotAnalyzer.CommandProcessing
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
        readonly List<MemorySnapshotLoader> m_memorySnapshotLoaders;
        readonly SortedDictionary<string, Type> m_commands;
        readonly Dictionary<Type, Dictionary<string, NamedArgumentKind>> m_commandNamedArgumentNames;
        readonly SortedDictionary<int, Context> m_contexts;
        string? m_currentCommandLine;
        int m_currentContextId;

        public Repl(IConfiguration configuration)
        {
            m_configuration = configuration;
            m_output = new ConsoleOutput();
            m_memorySnapshotLoaders = new List<MemorySnapshotLoader>();
            m_commands = new SortedDictionary<string, Type>();
            m_commandNamedArgumentNames = new Dictionary<Type, Dictionary<string, NamedArgumentKind>>();
            m_contexts = new SortedDictionary<int, Context>();
            m_contexts.Add(0, new Context(0, m_output)
            {
                // TODO: read TraceableHeap_Kind value
                TraceableHeap_FuseObjectPairs = configuration.GetValue<bool>("FuseObjectPairs"),
                Backtracer_GroupStatics = configuration.GetValue<bool>("GroupStatics"),
                Backtracer_FuseGCHandles = configuration.GetValue<bool>("FuseGCHandles"),
                HeapDom_WeakGCHandles = configuration.GetValue<bool>("WeakGCHandles")
            });
            m_currentContextId = 0;
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

        public MemorySnapshot? TryLoad(string filename)
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

        public string CurrentCommandLine => m_currentCommandLine!;

        public Context CurrentContext => m_contexts[m_currentContextId];

        public Context SwitchToContext(int id)
        {
            if (!m_contexts.ContainsKey(id))
            {
                m_contexts.Add(id, Context.WithSameOptionsAs(m_contexts[m_currentContextId], id));
            }
            m_currentContextId = id;
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

        public void Run()
        {
            while (true)
            {
                Output.Prompt();
                string? line = Console.ReadLine();
                if (line == null)
                {
                    continue;
                }

                RunCommand(line);
            }
        }

        public void RunCommand(string line)
        {
            var commandConstructorSignature = new Type[1];
            commandConstructorSignature[0] = typeof(Repl);
            var commandConstructorArguments = new object[1];
            commandConstructorArguments[0] = this;

            try
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

                Command command = (Command)commandType.GetConstructor(commandConstructorSignature)!.Invoke(commandConstructorArguments);
                AssignArgumentValues(command, commandLine);

                // TODO: store in history - make this return a "CommandLineArgument?"
                m_currentCommandLine = line;
                command.Run();
                m_currentCommandLine = null;
            }
            catch (OperationCanceledException)
            {
                Output.WriteLine("canceled");
            }
            catch (InvalidSnapshotFormatException ex)
            {
                Output.WriteLine(ex.Message);
            }
            catch (CommandException ex)
            {
                Output.WriteLine(ex.Message);
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

        void CheckValidityOfArgumentDeclarations(Type commandType)
        {
            TypeInfo typeInfo = commandType.GetTypeInfo();
            var positionalArguments = new Dictionary<int, bool>();
            int lowestOptionalFound = int.MaxValue;
            int highestRequiredFound = -1;
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
                }

                if (numberOfAttributesFound > 1)
                {
                    throw new ArgumentException($"command {commandType.Name}: conflicting attributes");
                }

                if (lowestOptionalFound < highestRequiredFound)
                {
                    throw new ArgumentException($"command {commandType.Name} field {field.Name}: positional argument {lowestOptionalFound} is marked optional, but later argument {highestRequiredFound} is not");
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
                            SetFieldValue(commandLine.CommandName, command, field, positionalArguments[index]);
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
                            SetFieldValue(commandLine.CommandName, command, field, value);
                        }
                    }
                    else if (attribute.AttributeType == typeof(FlagArgumentAttribute))
                    {
                        string flagName = (string)attribute.ConstructorArguments[0].Value!;
                        if (flags.TryGetValue(flagName, out var value))
                        {
                            if (field.FieldType == typeof(bool))
                            {
                                field.SetValue(command, value);
                            }
                            else
                            {
                                field.SetValue(command, value ? 1 : 0);
                            }
                        }
                    }
                }
            }

            if (argumentsConsumed < positionalArguments.Count)
            {
                throw new CommandException($"extraneous arguments given: {argumentsConsumed} expected, {positionalArguments.Count} found");
            }
        }

        void SetFieldValue(string commandName, Command command, FieldInfo field, CommandLineArgument value)
        {
            if (field.FieldType == typeof(string))
            {
                field.SetValue(command, value.StringValue);
            }
            else if (field.FieldType == typeof(int))
            {
                field.SetValue(command, (int)value.IntegerValue);
            }
            else if (field.FieldType == typeof(NativeWord))
            {
                if (CurrentContext.CurrentMemorySnapshot == null)
                {
                    throw new CommandException($"command {commandName} can only be run with an active memory snapshot");
                }
                field.SetValue(command, value.AsNativeWord(CurrentContext.CurrentMemorySnapshot.Native));
            }
            else if (field.FieldType == typeof(CommandLineArgument))
            {
                field.SetValue(command, value);
            }
        }
    }
}
