/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;
using System.IO;

namespace MemorySnapshotAnalyzer.CommandInfrastructure
{
    public sealed class FileOutput : IOutput, IDisposable
    {
        readonly Dictionary<int, string> m_indents;
        FileStream? m_fileStream;
        readonly StreamWriter m_writer;

        public FileOutput(string outputFilename)
        {
            m_indents = new Dictionary<int, string>();
            m_fileStream = new FileStream(outputFilename, FileMode.Create, FileAccess.Write);
            m_writer = new StreamWriter(m_fileStream);
        }

        public void Dispose()
        {
            if (m_fileStream != null)
            {
                m_writer.Close();
                m_fileStream.Dispose();
                m_fileStream = null;
            }
        }

        void IOutput.SetPrompt(string prompt)
        {
        }

        void IOutput.Prompt()
        {
        }

        void IOutput.ExecutionStart()
        {
        }

        void IOutput.ExecutionEnd(int exitCode)
        {
        }

        void IOutput.Clear()
        {
        }

        bool IOutput.CancellationRequested()
        {
            return false;
        }

        void IOutput.Write(string message)
        {
            m_writer.Write(message);
        }

        void IOutput.Write(string format, params object[] args)
        {
            m_writer.Write(format, args);
        }

        void IOutput.WriteLine()
        {
            m_writer.WriteLine();
        }

        void IOutput.WriteLine(string message)
        {
            m_writer.WriteLine(message);
        }

        void IOutput.WriteLine(string format, params object[] args)
        {
            m_writer.WriteLine(format, args);
        }

        void IOutput.WriteLineIndented(int indent, string format, params object[] args)
        {
            string? indentString;
            if (!m_indents.TryGetValue(indent, out indentString))
            {
                indentString = new string(' ', indent * 2);
                m_indents[indent] = indentString;
            }
            m_writer.Write(indentString);
            m_writer.WriteLine(format, args);
        }

        public void CheckForCancellation()
        {
        }
    }
}
