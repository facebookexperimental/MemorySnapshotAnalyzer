/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

namespace MemorySnapshotAnalyzer.CommandInfrastructure
{
    public interface IOutput
    {
        string Prompt { get; set; }

        void DoPrompt();

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
