// Copyright(c) Meta Platforms, Inc. and affiliates.

namespace MemorySnapshotAnalyzer.CommandProcessing
{
    sealed class CommandLine
    {
        readonly string m_commandName;
        readonly CommandLineArgument[] m_arguments;

        public CommandLine(string commandName, CommandLineArgument[] arguments)
        {
            m_commandName = commandName;
            m_arguments = arguments;
        }

        public string CommandName => m_commandName;

        public int NumberOfArguments => m_arguments.Length;

        public CommandLineArgument this[int index] => m_arguments[index];
    }
}
