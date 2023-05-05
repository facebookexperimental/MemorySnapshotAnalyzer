// Copyright(c) Meta Platforms, Inc. and affiliates.

namespace MemorySnapshotAnalyzer.CommandProcessing
{
    public interface IOutput
    {
        void Prompt();

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
