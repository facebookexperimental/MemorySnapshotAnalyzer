// Copyright(c) Meta Platforms, Inc. and affiliates.

namespace MemorySnapshotAnalyzer.CommandInfrastructure
{
    public interface IOutput
    {
        void SetPrompt(string prompt);

        void Prompt();

        void ExecutionStart();

        void ExecutionEnd(int exitCode);

        void Clear();

        bool CancellationRequested();

        void CheckForCancellation();

        void Write(string message);

        void Write(string format, params object[] args);

        void WriteLine();

        void WriteLine(string message);

        void WriteLine(string format, params object[] args);

        void WriteLineIndented(int indent, string format, params object[] args);
    }
}
